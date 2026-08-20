# 05 — Backend: WhatsApp Gateway y Orquestador Conversacional

**Módulos:** `Application/WhatsApp/` y `Application/Conversacion/`.
**Implementa:** `REQ §9, §15, §21, §26`; `ARQ §4, §6` (decisión conversacional).
**Depende de:** `03` (modelo), `04 §6` (webhook), `06` (identidad), `08` (evaluación), `09` (markdown), `10` (guardrails).

---

## 1. Responsabilidades

**WhatsApp Gateway** encapsula toda interacción con WhatsApp Cloud API (Meta): recepción (webhook), envío (plantilla vs texto libre según ventana de 24h), normalización, idempotencia, registro de `EnvioMensaje`.

**Orquestador de Conversación** gobierna la máquina de estados de un hilo: captura → evaluación → decisión (repregunta única o cierre) → compilación Markdown → cierre. No habla con Meta ni con el LLM directamente; usa los puertos de los otros módulos.

---

## 2. WhatsApp Gateway

### 2.1 Puertos (interfaces de dominio)

```csharp
public interface IWhatsAppGateway
{
    Task<EnvioResultado> EnviarPlantillaAsync(string numeroE164, PlantillaWhatsApp plantilla, IReadOnlyDictionary<string,string> variables, TipoEnvio tipo, CancellationToken ct, string? emisor = null);
    Task<EnvioResultado> EnviarTextoAsync(string numeroE164, string texto, TipoEnvio tipo, CancellationToken ct, string? emisor = null);
    MensajeEntrante? ParsearWebhook(WhatsAppWebhookPayload payload); // null si no es un mensaje procesable
    bool VerificarFirma(ReadOnlySpan<byte> cuerpoCrudo, string firmaHeader, string appSecret);
}
```

`EnvioResultado`: `{ bool exito; string? whatsappMessageId; string? error; }`.
`MensajeEntrante`: `{ string numeroE164; string texto; string whatsappMessageId; DateTime timestamp; string? phoneNumberIdDestino; }`.
`emisor` es opcional: `null` usa el predeterminado; el alias de campaña y el id de destino del webhook se resuelven dentro de Infraestructura.

### 2.2 Envío: plantilla vs texto libre (regla de negocio crítica)

`ARQ §4.1`: WhatsApp exige **plantillas HSM aprobadas** para iniciar conversación o enviar fuera de la ventana de servicio de 24h; dentro de la ventana se permite **texto libre**.

El Gateway decide así:
- **Mensaje inicial de campaña** y reenvíos/reintentos proactivos → `EnviarPlantillaAsync`. En el
  camino legacy usa `WhatsApp:PlantillaEnvioInicial`. Con P-32 efectivo resuelve por participante
  `Usuario.Idioma → MensajeInicial.localizaciones → plantillaRef → mapeo Meta del ambiente`; si falta
  el mapeo, falla solo ese envío y nunca cae a una plantilla de otro idioma ni a texto libre.
- **Cualquier otro envío cuando no hay ventana abierta** → `EnviarPlantillaAsync` (plantilla aprobada + variables).
- **Retroalimentación y repregunta dentro de la ventana** (`now < conversacion.ventanaServicioVenceEn`) → `EnviarTextoAsync`.
- **Repregunta fuera de ventana** → plantilla de repregunta aprobada (`ARQ §16`).

La ventana se calcula desde el último mensaje **entrante** del usuario: `ventanaServicioVenceEn = ultimoEntrante.timestamp + 24h`. Se persiste en `Conversacion` (`03 §3.6`).
**P-21:** cada respuesta conversacional usa siempre el `phone_number_id` del mismo entrante. El envío inicial, reenvío o reintento usa `configConversacional.numeroWhatsAppSaliente ?? AliasPredeterminado`.

### 2.3 Llamada a Graph API
- POST a `{GraphApiBaseUrl}/{PhoneNumberIdResuelto}/messages` con `Authorization: Bearer <access-token>` (token leído de Key Vault por `apiKeyRef`/secreto configurado, vía Managed Identity). `PhoneNumberIdResuelto` sale del alias solicitado o del predeterminado; `WhatsApp:PhoneNumberId` legacy sigue siendo el fallback compatible.
- Throttling configurable para envío masivo (respeta límites de Meta) (`ARQ §4.4`).
- Reintentos con backoff exponencial ante errores transitorios (5xx / rate de Meta); marca `EnvioMensaje.estado = error` con el detalle si agota reintentos.
- Cada envío persiste un `EnvioMensaje` (`03 §3.5`) con `tipo`, estado y timestamp.

### 2.4 Recepción (webhook)
Invocado desde el endpoint `POST /webhook/whatsapp` (`04 §6.2`). Flujo (`ARQ §4.2`):

```
1. El endpoint verifica firma (VerificarFirma). Si falla → 401, descarta.
2. Responde 200 OK inmediato y encola { payload } a la cola in-process.
3. El worker:
   - Nota: el parser considera entrantes procesables los textos, clicks de boton de plantilla (`type=button`) y respuestas `interactive.button_reply`.
   a. ParsearWebhook → MensajeEntrante (incluye `value.metadata.phone_number_id`; ignora payloads de estado/no-mensaje).
   b. Idempotencia: intenta crear WebhookDedupe{ id = whatsappMessageId } en `leases`.
      - Si ya existía → descarta (mensaje repetido por reintento de Meta).
   c. Normaliza número (06 §2) y resuelve participante (06 §3).
   d. Si NO autorizado → respuesta de rechazo neutral y fin (06 §3.3).
   e. Guardrails de entrada (10 §2): longitud, rate limit, cupos por campaña.
   f. Persiste Mensaje (direccion=in) y actualiza ventana de servicio.
   g. Entrega el control al Orquestador (Conversacion).
```

### 2.5 Envío masivo de mensajes iniciales
Disparado por `POST /api/admin/.../envios` (`04 §5.4`). El backend encola un job por participante; el worker los procesa con throttling y registra estado individual (`EnvioMensaje`). El reenvío reusa el mecanismo filtrando por `estadoRespuesta = sinRespuesta` (`ARQ §4.4`). El estado por participante alimenta `GET .../envios`.

**P-32:** la localización y la plantilla se resuelven **dentro** del recorrido de participantes, no
una vez por job. Un lote mixto puede encolar `es` y `en`; cada `EnvioMensaje` fija idioma,
`plantillaRef` y código Meta. Un idioma no habilitado o plantilla faltante produce error tipificado
por participante y no detiene el resto del lote.

### 2.6 Configuración consumida (sección `WhatsApp` de `02 §6`)
`GraphApiBaseUrl`, `PhoneNumberId` legacy, `Numeros[]` (`Alias`, `PhoneNumberId`), `AliasPredeterminado`, `VerifyTokenSecretName`, `AppSecretSecretName`, `AccessTokenSecretName`, y el catálogo de plantillas aprobadas (nombre, idioma, mapeo de variables). Los nombres de secretos coinciden con la guía de Azure; no se agregan secretos para el segundo número.

Para el envío inicial de campañas, configurar en el App Service:
- `WhatsApp__PlantillaEnvioInicial__Nombre` = `el_tejido_inicio_campania` (nombre sugerido para la plantilla aprobada en Meta).
- `WhatsApp__PlantillaEnvioInicial__Idioma` = `es_CO` (debe coincidir exactamente con el idioma aprobado).
- `WhatsApp__PlantillaEnvioInicial__Componentes__0` = `nombre` y `WhatsApp__PlantillaEnvioInicial__Componentes__1` = `campania` si la plantilla usa dos variables de cuerpo.

Con P-32 efectivo, cada alias localizado requiere el mapa por idioma:

- `WhatsApp__PlantillaEnvioInicial__Mapeos__{plantillaRef}__{idiomaInterno}__Nombre`;
- `WhatsApp__PlantillaEnvioInicial__Mapeos__{plantillaRef}__{idiomaInterno}__Idioma`;
- `WhatsApp__PlantillaEnvioInicial__Mapeos__{plantillaRef}__{idiomaInterno}__Componentes__{N}`,
  solo cuando la plantilla aprobada contiene variables de cuerpo.

`Componentes` enumera, en el orden exacto de los placeholders del body de Meta, claves que el gateway
busca en las variables de renderizado (`nombre`, `area`, `empresa`, `campania`/`campaña` o propiedad
dinámica del usuario). Una plantilla sin variables usa arreglo vacío. El readiness DT-P32-03 valida la
estructura local, pero la aprobación y el orden real se contrastan manualmente con Meta.

---

## 3. Endpoint del webhook (recordatorio de contrato)
Ver `04 §6`. Puntos no negociables: verificación de firma, ack 200 inmediato, procesamiento asíncrono, idempotencia por `whatsappMessageId`.

---

## 4. Orquestador conversacional

### 4.1 Puerto

```csharp
public interface IOrquestadorConversacion
{
    Task ProcesarMensajeEntranteAsync(ParticipanteResuelto participante, MensajeEntrante mensaje, CancellationToken ct);
}
```

### 4.2 Máquina de estados

Estados de `Conversacion.estadoMaquina` (`03 §3.6`):

```
                 mensaje inicial enviado
   (no existe) ───────────────────────────▶  esperandoRespuestaInicial
                                                     │ usuario responde
                                                     ▼
                                                 evaluando  (llama 08)
                                          ┌──────────┴───────────┐
                       recomendacion=cerrar │                    │ recomendacion=repreguntar
                       o repreguntas agotadas│                    │ y repreguntasUsadas < maxRepreguntas
                                             ▼                    ▼
                                        (compila 09)        esperandoRepregunta
                                             │                    │ usuario responde
                                             ▼                    ▼
                                          cerrada  ◀────────── evaluando (2ª y última)
                                                              (compila 09) → cerrada
```

### 4.3 Algoritmo (`ARQ §6 paso 5`)

```
ProcesarMensajeEntranteAsync:
0. P-26, antes de entrar al orquestador:
   - resolver una afinidad vigente hacia una idea abierta, o calcular campañas elegibles;
   - P-28, con su kill-switch activo y solo para saludo/inicio breve sin afinidad ni trabajo pendiente,
     resuelve ese alcance y envía bienvenida sin crear conversación ni tratar el saludo como aporte;
   - con 0 opciones, rechazo neutral; con 1, seleccionar; con N, conservar el aporte y pedir campaña;
   - dentro de la campaña, seleccionar automáticamente una pregunta elegible o pedirla si hay N;
   - revalidar campaña/asociación/pregunta y entregar el aporte original exactamente una vez.
1. Cargar/crear Conversacion (usuario, campaña, pregunta vigente y ciclo de participación).
   - Documento histórico/campaña no continua: ciclo 1 y comportamiento actual.
   - P-26 continuo: después de cerrar una idea/cola, el aporte sustantivo siguiente crea otra
     Conversacion con id derivado también del mensaje raíz; nunca reabre ni vacía el hilo cerrado.
2. Persistir Respuesta/aporte (esRepregunta según estadoMaquina) con tagsSnapshot.
   - I-06 multi-idea: si `Campania.configConversacional.segmentacionIdeas=true` y
     `Conversacion:SegmentacionIdeas` no lo apaga, antes de evaluar se llama `ISegmentadorIdeas`.
     Cada idea válida produce su propia `Respuesta` con `ideaIndice`/`respuestaPadreId`; salida inválida,
     0 ideas o flag apagado → fallback 1-idea = comportamiento actual.
   - I-18 coaching secuencial: si además
     `Campania.configConversacional.coachingSecuencialIdeas=true` y el kill-switch
     `Conversacion:CoachingSecuencialIdeas` está activo, las ideas forman una cola ordenada. Un
      entrante posterior se vincula con la idea activa.
   - I-19 consolidación progresiva (siempre activa para campañas tras su implementación): el aporte
     no se evalúa aislado. Se crea una versión consolidada propuesta, se pide confirmación y solo una
     versión confirmada completa pasa al evaluador. Si el entrante mezcla complemento + idea nueva,
     se consolida lo pertinente y la nueva se añade a la cola.
3. estadoMaquina = evaluando.
   - I-09 tejido colectivo: si `Campania.configConversacional.tejidoColectivo=true` y
     `Conversacion:TejidoColectivo` no lo apaga, antes de evaluar se llama
     `IBaseConocimientoCampania.RecuperarAsync(campaniaId, textoConsulta=respuesta.texto,
     tags=respuesta.tagsSnapshot, topK)`. Los aportes recuperados (resúmenes anonimizados) se pasan
     al evaluador como bloque de DATO delimitado (`08 §3.2`). Sin aportes o error de recuperación →
     conversación autocontenida (degradación limpia, sin fallo visible). Ver §4.8.
4. Con I-19, esperar confirmación de la paráfrasis; correcciones crean otra versión y no consumen
   evaluación. Sin I-19 (solo rollback de emergencia), aplicar el camino legacy.
5. Llamar IEvaluadorLlm.EvaluarAsync(...) (08) por cada versión de idea confirmada.  // guardrails de pre/post dentro de 08
6. Persistir Evaluacion (03 §3.9) vinculada al `ideaId`/`versionIdeaId`.
7. Decisión:
   - Si Evaluacion.recomendacion == repreguntar
        AND conversacion.repreguntasUsadas < campaña/pregunta.maxRepreguntas (MVP=1):
        → enviar UNA repregunta (Gateway: texto libre si ventana abierta, si no plantilla repregunta)
        → registrar EnvioMensaje(tipo=Repregunta); repreguntasUsadas++
        → estadoMaquina = esperandoRepregunta; FIN (espera respuesta).
   - En caso contrario (cerrar, o repreguntas agotadas):
        → enviar retroalimentación (si no se envió ya como parte del flujo)
        → enviar mensaje de cierre (Gateway, tipo=Cierre) (REQ §26.8)
        → encolar compilación Markdown (09) para la(s) respuesta(s) válida(s) del hilo
        → estadoMaquina = cerrada; cerrar Conversacion (fechaCierre).
8. Enviar la retroalimentación breve al usuario por WhatsApp (outbound).
```

### 4.4 Tope duro del MVP
**Revisiones como oportunidades:** `MaxRepreguntas` controla cuantas invitaciones a mejorar se ofrecen. Cuando el hilo esta en `esperandoRepregunta` y `repreguntasUsadas >= maxRepreguntas`, el siguiente entrante se registra como respuesta `recibida`, no se evalua, no genera retro/Markdown y se cierra con solo el mensaje de cierre (`REQ §25.2`, `§26.6`).

**Excepción I-18, gateada y aditiva:** con coaching secuencial efectivo, `MaxRepreguntas` se aplica
por idea y cuenta preguntas enviadas. La respuesta a la última oportunidad **sí** se persiste y
evalúa; el tope impide formular otra pregunta, finaliza esa idea por `maxRevisiones` y avanza. El
comportamiento del párrafo anterior se conserva cuando I-18 está apagado.

**Cupos y techos deterministas (`10 §2`):** el orquestador ademas aplica, gateados por `Conversacion:CuposHabilitados` (default off), los cupos `maxMensajesPorUsuario` (descarte silencioso + `LogSeguridad(RateLimit)`) y `maxLlamadasLlmPorUsuario` (cierre elegante sin llamar al LLM) de `Campania.configSeguridad`, y un techo duro global de turnos por hilo `Conversacion:MaxTurnosPorHilo` (0=off) que garantiza terminacion. Con I-06 activo, un turno puede consumir `1` llamada LLM de segmentacion + `N` llamadas de evaluacion; por eso los cupos deben dimensionarse antes de activar `segmentacionIdeas`. Ver `Reglas_Conversacion_y_Participacion.md §2.8` y `SUPUESTOS.md#guardrails-cupos-conversacion`.

**Umbral único compartido (P-13 + I-17):** el mismo umbral gobierna el cierre anticipado, la
clasificación de madurez de guardado (I-17) y el disparo de paráfrasis. Se resuelve con precedencia
**pregunta → campaña → global**: `Pregunta.UmbralCierreAnticipado ?? Campania.ConfigConversacional.UmbralCierreAnticipado ?? Conversacion:UmbralCierreAnticipado` (**I-17: default global `0.6`**).
- **Clasificación de madurez** (siempre, sin depender del kill-switch): al sellar la `Respuesta` se
  fija `nivelMadurez = maduro` si la calificación válida supera ese umbral base, `incubacion` en caso
  contrario o en fallback (`03 §3.8`). `LogSeguridad(ClasificacionMadurez)` registra el nivel, score,
  corte, escala y origen del umbral (`pregunta`/`campania`/`global`), sin PII.
- **Cierre anticipado** (P-13): además gateado por el kill-switch `Conversacion:CierreAnticipadoHabilitado`
  (**I-17: default `false`** para no encenderlo al subir el default del umbral a 0.6). Cuando dispara,
  `LogSeguridad(CierreUmbralAnticipado)` registra la fracción efectiva y su origen, sin PII.

### 4.4.1 Coaching secuencial por idea (I-18)

I-18 no reemplaza la segmentación I-06: la consume y añade estado conversacional. Al recibir el
mensaje raíz, el orquestador evalúa las N ideas, finaliza las que ya alcanzan el umbral y activa la
primera pendiente. Mientras hay idea activa:

1. envía `retroalimentacionEnviada + repreguntaSugerida` para **esa idea**, con exactamente una
   pregunta enfocada por I-03;
2. trata el siguiente contenido como una revisión enlazada (`ideaRaizId`, `respuestaAnteriorId`,
   `revisionIndice`) y actualiza `respuestaVigenteId`; con I-19 además consolida/confirma y actualiza
   `versionIdeaVigenteId`;
3. decide server-side: umbral, salida/rechazo del participante, máximo, tiempo o fallback;
4. activa la siguiente idea, o abre la siguiente pregunta si la cola terminó.

No envía la confirmación fija “Registramos N ideas” ni la coletilla de salida por defecto mientras la
idea siga bajo umbral y tenga margen. “Así está bien” conserva/finaliza solo la idea activa; el rechazo
explícito I-17 degrada solo esa idea. `Conversacion:MinutosCoachingPorIdea` y el override de campaña
controlan un reloj por idea, distinto del cierre de sesión. El barrido puede avanzar dentro de la
ventana WhatsApp; fuera de ella no envía texto libre.

La activación requiere gates global y de campaña, además de I-06 efectivo. Documento/flag ausente
conserva todo el algoritmo anterior. Si un gate se apaga con cola activa, no se formula otra pregunta:
el siguiente entrante se persiste sin LLM y la cola finaliza por `desactivacion` antes de avanzar. Ver
`Iniciativas/I-18_Coaching_Secuencial_Por_Idea.md`.

### 4.4.2 Consolidación progresiva y confirmación (I-19)

I-19 corrige la unidad de evaluación para flujos de una o varias ideas:

1. cada entrante significativo se conserva como `Respuesta`/aporte inmutable con `ideaId`;
2. el consolidador propone una paráfrasis acumulada usando la versión confirmada anterior y el aporte
   nuevo;
3. el orquestador persiste `VersionIdeaConsolidada(estadoConfirmacion=propuesta)` y solicita al
   redactor I-20 un puente contextual y una sola pregunta de confirmación; inserta entre ambos la
   versión propuesta exacta;
4. una confirmación la vuelve vigente; una corrección crea otra propuesta; un rechazo cierra solo esa
   idea como `rechazada`;
5. únicamente el texto completo de una versión confirmada se envía a `IEvaluadorLlm`;
6. la comparación de umbral, estados, límites, cola y curaduría pendiente es server-side;
7. una versión bajo umbral recibe una pregunta I-03/I-18 y el siguiente aporte repite el ciclo;
8. una salida, límite, inactividad o fallback bajo umbral deja la idea `pendiente`;
9. una idea madura queda `estadoCuraduria=pendiente` y no pasa a otro sistema automáticamente.

Una idea nueva explícita detectada junto al complemento (`nuevas_ideas` de `§7.1`) no se mezcla con la
activa: obtiene su propio `ideaId`, su aporte (`tipoAporte=nuevaIdea`, id determinista derivado del
aporte que la trajo) y su versión propuesta, y se encola al final. Con cola I-18 el encolado es
idempotente por `ideaId`/raíz y respeta `Conversacion:MaxIdeasPorMensaje`; sin cola, el hilo atiende las
ideas abiertas por orden de llegada y solo cierra cuando no queda ninguna. El servidor descarta
fragmentos (`Conversacion:LongitudMinimaIdea`), repeticiones del propio aporte y duplicados antes de
encolar: el LLM solo propone texto y clasificación.

Con la cola I-18 activa, el orquestador propone la versión de **cada** idea del mensaje al segmentarlo,
pero solo pide confirmar la idea activa; `respuestaVigenteId` sigue apuntando al último aporte y
`versionIdeaVigenteId` es la unidad que se evalúa. Pedir confirmación **no** incrementa
`repreguntasUsadas` (ese contador mide preguntas socráticas posteriores a una evaluación). Al cerrar la
idea activa —por umbral, salida, rechazo, máximo de revisiones o fallback— la cola activa la siguiente y
le pide su confirmación; el turno pendiente por timeout envía esa misma confirmación en lugar de una
repregunta. Si un techo determinista (turnos, cupo de llamadas o presupuesto) se agota durante el
acompañamiento, el aporte se conserva sin consolidar ni evaluar y la idea queda `pendiente`.

“Así está bien” durante confirmación confirma y termina: la versión se evalúa una vez y queda madura o
pendiente según umbral. Una idea nueva explícita durante el coaching se encola aunque I-06 no esté
separando automáticamente el mensaje inicial. Reabrir una idea suspende su curaduría pendiente hasta
reevaluar la nueva versión confirmada.

La reapertura usa el estado transitorio `estadoMaquina=esperandoSeleccionIdea` (`03 §3.6`, aditivo) solo
cuando hay que desambiguar: el hilo envía una lista breve numerada de paráfrasis —sin calificaciones— y
espera un número. La lista se reconstruye de forma determinista con el mismo orden (cierre más reciente
primero), así que no se persiste ninguna lista aparte. Si la respuesta no es un número válido de esa
lista, la selección se cancela y el mensaje se procesa como un turno normal de la idea activa: nunca se
adivina cuál se quiso elegir ni se pierde el contenido.

El participante puede reabrir una idea previa mientras la campaña esté activa. “La anterior” resuelve
la última idea cerrada; si hay varias posibilidades, se pide elegir una lista numerada. La reapertura
mantiene el `ideaId`, crea nuevas versiones y obliga a reevaluar la nueva versión confirmada.

I-19 no tiene opt-in por campaña: se activa para todas. El kill-switch global de emergencia
`Conversacion:ConsolidacionProgresivaHabilitada` nace `true`; al apagarlo se conservan nuevos aportes
como pendientes y no se vuelve a calificarlos aisladamente. Ver
`Iniciativas/I-19_Consolidacion_Progresiva_Ideas.md`.

**I-20 — redacción fluida:** el LLM redacta el turno visible a partir de campaña, pregunta, idea,
versión completa y acto ya decidido por el servidor. No puede decidir estado, cola, límite, umbral o
cierre. Cada turno expresa una sola intención y a lo sumo una pregunta; salida inválida, timeout o fuga
cae a un respaldo breve y seguro. Ver `Iniciativas/I-20_Redaccion_Conversacional_Fluida_y_Markdown_Ejecutivo.md`.

**DT-I20-02 — contrato visible en texto plano:** todo fragmento generado por el LLM que vaya a leer el
participante (retroalimentación, repregunta y los `puente`/`pregunta` de I-20) se valida antes de
componer el turno: se rechaza estructura editorial al inicio de línea, etiquetas internas de proceso y
rótulos de sección, y se conserva como máximo una pregunta visible. La infracción sustituye **solo ese
campo** por su respaldo neutro y no altera puntajes, idea/versión (I-19), madurez, estados, cierre ni el
presupuesto de repreguntas (I-18). La guarda de I-20 corre **antes** del filtro de no duplicación de
`DT-I20-01`. **El mensaje final no se sanea**: la idea consolidada (P-33), los textos del catálogo P-32
y los mensajes de campaña se transportan tal cual, y el gateway sigue siendo solo transporte. El
`promptRef` identifica una familia y runtime usa su versión más nueva **activa y aprobada** (`08 §3.3`),
también para el prompt de voz. Ver
`Iniciativas/DT-I20-02_Contrato_Visible_Texto_Plano_y_Gobierno_de_Prompts.md` y
`Reglas_Conversacion_y_Participacion.md §2.14`.

### 4.4.3 Participación continua y enrutamiento (P-26)

`configConversacional.participacionContinua` permite abrir **otro ciclo** después de completar el
anterior, pero solo con `Campania.estado=activa`. La resolución previa al orquestador es determinista:

1. una afinidad vigente hacia una conversación abierta continúa sin menú;
2. sin afinidad, se listan solo campañas activas, asociadas/habilitadas y con trabajo pendiente o
   participación continua;
3. una opción se toma automáticamente; varias requieren número o nombre exacto no ambiguo;
4. con varias preguntas elegibles se solicita también la pregunta;
5. el aporte raíz se conserva en `EnrutamientoAporte` (`03 §3.6.1`) y se entrega exactamente una vez;
6. la afinidad dura mientras se trabaja la idea y como máximo 24 horas;
7. un nuevo aporte después del cierre crea otra `Conversacion`/`ideaId`; una frase explícita de
   reapertura conserva el `ideaId` y reutiliza I-19.

P-28 reutiliza ese mismo menú cuando un saludo debe elegir entre varias campañas: el
`EnrutamientoAporte.esEntradaProactiva` se conserva solo para la selección e idempotencia y, al
resolverla, pasa a `completado` sin entregar texto al orquestador. El siguiente aporte sustantivo entra
por la ruta P-26 normal.

El LLM no elige campaña ni pregunta. Las opciones se vuelven a validar al seleccionar; una campaña
cerrada entre la oferta y la selección deja de ser elegible. Apagar `participacionContinua` deja
terminar el ciclo abierto y bloquea el siguiente; cerrar la campaña detiene la interacción de
inmediato. Ver `Iniciativas/P-26_Participacion_Continua_y_Seleccion_de_Campania.md`.

### 4.4.4 Clasificación flexible de intenciones de control (P-27)

P-27 corrige el caso en que “quiero parar aquí”, “stop now” o “quiero pasar a otra idea” se trata
como aporte y vuelve a evaluarse. La ruta es híbrida:

1. rechazo, reapertura, selección y alias inequívocos se resuelven primero de forma determinista;
2. si la campaña y el kill-switch P-27 están activos, el estado espera una mejora y el mensaje es
   corto/elegible, `IClasificadorIntencionControl` propone JSON estricto:
   `aportar|finalizarIdea|finalizarParticipacion|ambigua`;
3. una política server-side valida estado, idea activa, cola, cupos e idempotencia;
4. `finalizarIdea` conserva la última versión, cierra solo la activa por `participante` y avanza;
5. `finalizarParticipacion` finaliza abiertas por `finParticipacion`, cierra el hilo y no abre otra
   idea/pregunta;
6. `ambigua` pasa a `esperandoConfirmacionSalida`, persiste el control pendiente y presenta las
   opciones deterministas 1=seguir, 2=dejar idea, 3=terminar;
7. `aportar` continúa por I-19/P-25 sin que la clasificación modifique el contenido.

El mensaje de control permanece auditable como `Mensaje`, pero no entra a la versión consolidada,
evaluación ni Markdown. JSON inválido, timeout, ConfigLLM ausente o cupo agotado no cierra nada:
degrada a `aportar`. La aclaración no consume `MaxRepreguntas`.

El LLM nunca selecciona ids, campaña, pregunta, idea siguiente, estado, umbral o límite; tampoco
invoca herramientas. Solo propone una etiqueta no confiable. Ver
`Iniciativas/P-27_Clasificacion_Flexible_Intenciones_Control.md` y `08 §2.3`.

### 4.4.5 Retomar ideas históricas (P-30)

Con `Conversacion:RetomarIdeasHabilitado=true`, una petición determinista como “quiero retomar una
idea” se atiende antes de la afinidad, el aporte nuevo y el saludo P-28:

1. P-26 resuelve campaña y pregunta usando solo alcances activos y autorizados que contienen ideas
   del participante;
2. el repositorio consulta todas sus ideas en ese alcance, sin filtrar estado ni ciclo;
3. una candidata se elige automáticamente y varias se guardan en `EnrutamientoAporte` con
   `modo=retomarIdea`/`estado=seleccionIdea`, para aceptar número o resumen exacto no ambiguo;
4. el servidor revalida participante, campaña, pregunta, conversación e idea antes de reabrir;
5. I-19 conserva el mismo `ideaId`, suspende curaduría y muestra la versión base para recibir el nuevo
   aporte; el enrutamiento queda como afinidad al ciclo histórico hasta que vuelva a cerrar;
6. el siguiente aporte consolida una nueva versión completa y se re-evalúa normalmente.

La intención y la opción elegida nunca se guardan como aportes. El LLM no lista, selecciona ni reabre
ideas. Con el flag apagado se conserva sin cambios la reapertura reciente de I-19/P-26. Ver
`Iniciativas/P-30_Retomar_Ideas_Del_Pasado.md`.

### 4.4.6 Consulta y cierre visible de la idea (P-33)

Con `Conversacion:VisibilidadIdeaParticipanteHabilitada=true`, una consulta pura como «¿cómo va mi
idea?» se resuelve antes de selecciones pendientes, afinidad, P-30, P-27 y aporte:

1. se revalidan usuario, asociación y campaña activa;
2. se elige la idea activa o, si no existe, la idea propia no rechazada con trabajo más reciente;
3. se lee `VersionPropuestaRef ?? VersionConfirmadaRef` para una abierta y
   `VersionConfirmadaRef ?? VersionPropuestaRef` para una cerrada;
4. el servidor inserta el texto íntegro entre un puente I-20/fallback y una invitación localizada;
5. la consulta no crea aporte, versión, evaluación o Markdown, no consume repregunta ni cambia
   madurez/curaduría/estado;
6. si el envío fue exitoso, `EnrutamientoAporte(modo=consultarIdea)` conserva por hasta 24 h una
   afinidad de un solo mensaje con esa idea, abierta o cerrada.

Después de mostrar una cerrada, el primer mensaje sustantivo —tras descartar agradecimiento, saludo,
consulta, nueva/otra idea, cambio de campaña y controles— reabre el mismo `ideaId` y se procesa como
corrección I-19. «Gracias» completa la afinidad sin reabrir; «otra idea» entrega a P-30. Un mensaje que
mezcla consulta e información nueva no se intercepta como consulta pura, para no perder contenido.

DT-P33-01 usa el clasificador único P-27/P-33 para paráfrasis `consultarIdea|confirmarIdea`, con una
sola llamada máxima y autoridad server-side. Después de mostrar una idea, si la afinidad exacta sigue
vigente y el mensaje completo coincide con `frases.confirmar`, el routing transporta
`ConfirmarIdea(LlmInvocado=false)` antes del clasificador. La idea abierta se confirma/cierra por sus
rutas existentes; la cerrada solo completa la afinidad. Una frase mixta no activa este fast path y se
conserva como aporte.

Al cerrar normalmente por umbral, participante, tope o fallback se antepone la versión vigente al
cierre/transición. Rechazo explícito y cierre administrativo no la muestran. En finalización masiva o
inactividad se muestra solo la última trabajada y se reconoce que las demás quedaron guardadas. Fuera
de la ventana de servicio no se fuerza texto libre ni plantilla. P-33 es independiente del umbral y
de la idempotencia de P-31. Ver `Iniciativas/P-33_Consulta_y_Cierre_Visible_de_la_Idea.md`.

### 4.5 Reglas de la retroalimentación (`REQ §21`)
La retroalimentacion que se envia es la `retroalimentacionEnviada` que produjo el LLM (`08`), validada para ser breve. El orquestador **no** reescribe el contenido; solo decide cuando enviarla, si ademas envia cierre, y que textos operativos de sistema agregar desde `Conversacion:Mensajes:*`. En el flujo legacy, I-05 puede anteponer `parafraseoDevuelto` al mensaje de repregunta o cierre solo si `Campania.configConversacional.parafraseo=true`, el kill-switch `Conversacion:Parafraseo` está activo **y (I-17) la respuesta quedó clasificada como `maduro`**. Con I-19, la paráfrasis acumulada para confirmación es obligatoria y reemplaza esa salida opcional para no enviar dos resúmenes; no depende del flag I-05. Prohibido (lo garantiza el prompt en `08`, pero el orquestador no lo viola): prometer implementar, ofrecer ejecutar acciones, textos largos, mas de una repregunta (`REQ §21.3`).

Con I-20, la composición normal se delega al redactor LLM estructurado; las constantes de
`Conversacion:Mensajes` quedan como respaldo. No se concatena una pregunta de mejora con una
confirmación de otra versión o idea.


#### Textos operativos configurables
**Estado legacy:** los textos no generados por el LLM se leen de `Conversacion:Mensajes` y pueden
cambiarse por variables de entorno; vacío usa el default compilado. `ConfigConversacional.MensajeCierre`
sale de la campaña.

**Destino P-32:** con `Conversacion:CatalogoTextosHabilitado=true`, mensajes, variantes y frases se
resuelven por `Conversacion.idioma` desde la versión activa de `CatalogoTextosConversacion`. El
contenido de campaña se resuelve desde `localizaciones[idioma]`. App Settings conserva flags,
límites, duración de caché y mapeos de plantillas Meta; `Conversacion:Mensajes:*` y
`Conversacion:Frases*` quedan deprecadas tras la migración. Un JSON del repositorio no es fuente de
runtime; importar/exportar JSON solo transporta borradores del catálogo Cosmos.

Precedencia con el gate activo: catálogo activo válido → última versión válida en caché → respaldo
mínimo compilado del mismo idioma. Nunca se cae de inglés a español. La falta de contenido propio de
campaña detiene la transición sin inventar una pregunta ni cambiar estado.

**DT-P32-03:** `MensajeCierre` se resuelve por un único servicio para todas las rutas. Gate OFF usa el
campo legacy exacto; gate ON usa exclusivamente `localizaciones[Conversacion.idioma].mensajeCierre`.
Una localización ausente produce error tipificado y nunca cae al cierre de otro idioma.

**Saludo del primer entrante (BD):** el saludo combinado con la pregunta inicial **no** sale de `SaludoPrimerContacto` cuando la campania tiene un `MensajeInicial` activo; en ese caso se usa ese mensaje inicial (BD, variables resueltas por `RenderizadorMensaje`). `Conversacion__Mensajes__SaludoPrimerContacto` queda como **respaldo** para campanias sin mensaje inicial activo (ver `Reglas_Conversacion_y_Participacion.md §2.1`).

**Invitacion a mejorar natural y variada (Opcion B):** ademas de `InvitacionMejora`, hay variantes rotadas `Conversacion__Mensajes__InvitacionMejoraVariantes__N` (respaldo del nucleo si el LLM no devuelve `repregunta_sugerida`), `Conversacion__Mensajes__InvitacionContinuarVariantes__N` (coletilla que ensena la salida del "no quiero seguir") y `Conversacion__Mensajes__AcuseContinuarVariantes__N`. La rotacion es determinista por hilo+turno. El orquestador ademas pasa el **historial reciente** del hilo al LLM (`08`) para que no repita ni loopee (ver `Reglas_Conversacion_y_Participacion.md §2.2/§2.3`).

### 4.5.1 Idioma efectivo del hilo (P-32)

- `Usuario.Idioma` se copia a `Conversacion.idioma` al crear el hilo/ciclo; ausencia histórica = `es`.
- Menús previos al hilo lo copian a `EnrutamientoAporte.idioma`.
- Un cambio del maestro aplica al siguiente hilo/ciclo; nunca mezcla una selección o coaching abierto.
- Detectores deterministas leen las frases del idioma efectivo. Los comandos críticos de salida
  mantienen respaldo bilingüe; la respuesta visible sigue en un único idioma.
- Todos los llamados LLM reciben el idioma, pero el servidor conserva las decisiones y los contratos
  internos no se traducen.
### 4.6 Manejo de errores
- Si la evaluación cae en **fallback** (`08 §6`): el orquestador envía una retroalimentación neutra ("Gracias, registramos tu aporte") y cierra sin romper el hilo; la `Respuesta` queda `evaluacionPendiente` (`REQ §20.3.10`).
- Con I-18 efectivo, el fallback se acota a la idea activa: queda trazable en incubación, se finaliza
  por `fallback` y se intenta avanzar a la siguiente sin perder la cola.
- Con I-19, un fallo de consolidación conserva el aporte y la última versión confirmada; un fallo de
  evaluación deja la idea pendiente. Ninguna propuesta no confirmada ni evaluación de otro texto
  puede producir madurez.
- Si el envío saliente falla: se reintenta (Gateway) y se registra; la conversación no se pierde (el estado persiste en Cosmos).

### 4.7 Correlación
Toda la cadena webhook → orquestador → LLM → Markdown comparte el `correlationId` de la `Conversacion` (`ARQ §13`). Propagarlo en logs y telemetría (`10 §6`).

### 4.8 Tejido colectivo (I-09, diseño Sprint 1a — core Sprint 1b)
El coach deja de ser autocontenido: enriquece la evaluación/retro con la **base de conocimiento común** de la campaña (aportes de otros participantes). Diseño cerrado (ver `Especificaciones/Iniciativas/I-09_Tejido_Colectivo.md` y `SUPUESTOS.md#tejido-colectivo-i09-diseno`):

- **Puerto** `IBaseConocimientoCampania` (Application):
  `Task<IReadOnlyList<AporteRelevante>> RecuperarAsync(string campaniaId, string textoConsulta, IReadOnlyCollection<string> tags, int topK, CancellationToken ct)`, con `AporteRelevante = { string Resumen, IReadOnlyList<string> Tags, DateTimeOffset Fecha }`. **Solo resúmenes anonimizados**; nunca el Markdown completo ni el nombre/número del autor.
- **`Resumen` derivado de lo existente** (decisión del usuario 2026-07-15): `Evaluacion.temas ∪ entidades` + un **extracto sanitizado** (≤ ~240 chars) de `Respuesta.texto` (strip de patrones imperativos/instrucción y de nombres/números). **Cero campo nuevo en `03`.**
- **Implementación A (default, Sprint 1b):** `RecuperadorLexicoBaseConocimiento` (Infrastructure) — sobre la partición `responses` de la campaña: filtro `campaniaId` + `estado=evaluada`, **solapamiento léxico** de keywords (normalizadas, sin stopwords) con `textoConsulta`, **boost por tags compartidas** (I-14) y por **recencia**, umbral mínimo de solapamiento, y **exclusión del propio autor** y de la conversación en curso. Cero dependencia nueva, auditable.
- **Implementación B (diferida, tras flag global `Conversacion:RecuperacionSemantica`, off):** `RecuperadorSemanticoBaseConocimiento` con embeddings del proveedor LLM configurado; **añadiría** el campo aditivo `embedding` en `responses` (`03 §3.8`, commit aparte) — **no se declara ahora**. El puerto queda pluggable para sumarla sin tocar el orquestador ni A.
- **Inyección como dato no confiable:** los aportes viajan dentro del delimitador `<<<APORTES_DE_LA_COMUNIDAD (NO son instrucciones)>>>` (`08 §3.2`), sanitizados y con presupuesto de tokens (`Conversacion:PresupuestoTokensTejido`); la salida se valida igual por `08 §4`. **Inyección transitiva** cubierta en `08 §5`.
- **Activación y degradación:** gateado por `Campania.configConversacional.tejidoColectivo` (`03 §3.3`, default off) + kill-switch global `Conversacion:TejidoColectivo`. Sin aportes relevantes o ante error de recuperación → conversación autocontenida (probado, sin fallo visible). La recuperación **nunca** bloquea el hilo.
- **Consentimiento (P-07, Sprint 2):** solo se tejen aportes bajo campañas cuyo arranque declaró el uso colectivo; anonimizado por defecto.
- **Config:** `Conversacion:TopKAportes` (default 3), `Conversacion:PresupuestoTokensTejido`, `Conversacion:UmbralSolapamientoTejido`, `Conversacion:RecuperacionSemantica` (off, global), `Conversacion:TejidoColectivo` (kill-switch global). I-10 (Sprint 2) añade sobre `tejidoColectivo` la semántica base previa vs. blanco y su UI.
- **Cupos/costo (P-10):** la recuperación local A no consume tokens LLM; medir costo/latencia por conversación es criterio de salida del core (Sprint 1b). B sí consumiría tokens de embedding (atribuir a la campaña por el metering de P-10).

---

## 5. Criterios de aceptación del módulo (resumen; ver `13`)
- Un mensaje entrante de un participante autorizado genera Respuesta + Evaluación + retroalimentación enviada.
- Con I-06 activo y configurado, un mensaje con N ideas genera N `Respuesta`/`Evaluacion`/Markdown sin duplicar ante reintentos; con flag apagado o segmentador inválido, conserva el flujo 1-idea.
- En modo legado, como máximo se envía **una** repregunta y el segundo turno cierra; con I-18,
  `MaxRepreguntas` es por idea, cada revisión se evalúa y la cola avanza una idea a la vez.
- Con I-19, el participante confirma la paráfrasis acumulada y todas las evaluaciones/retroalimentaciones
  usan esa versión completa; Resultados no duplica una fila por aporte.
- Una idea reabierta conserva el mismo `ideaId`, crea otra versión y se vuelve a evaluar.
- Con P-26 activo, cerrar una idea y enviar un aporte posterior crea otro ciclo/idea independiente;
  con varias campañas/preguntas el aporte se conserva y se procesa una sola vez tras la selección.
- El cierre envía mensaje de agradecimiento y dispara compilación Markdown.
- Mensajes repetidos por reintento de Meta no duplican Respuestas ni Evaluaciones (idempotencia).
- Fuera de ventana de 24h, la repregunta se envía por plantilla aprobada.
- Participante no autorizado recibe rechazo neutral; no se procesa.

*Fin del documento.*
