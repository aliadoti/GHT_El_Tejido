# Reglas de conversación y participación — El Tejido

> Documento de consulta de las **reglas de negocio** del flujo de interacción con el participante por
> WhatsApp. Resume el comportamiento implementado en `OrquestadorConversacion` y servicios asociados.
> Fuente de verdad del código: `05_Backend_WhatsApp_y_Conversacion.md` (§2, §4), `08` (evaluación LLM)
> y `09` (Markdown). Última revisión: 2026-08-13 (`DT-I20-01` implementada localmente; variación
> natural y no duplicación dentro de un mismo envío).

## 1. Visión general del flujo

```
Participante                         El Tejido
     │  "Hola" (primer contacto)         │
     │ ────────────────────────────────► │  (no evalúa el saludo)
     │  ◄──────── Saludo + PREGUNTA ───── │
     │  Su respuesta                      │
     │ ────────────────────────────────► │  Consolida + evalúa la idea completa
     │  ◄──── Retro + pregunta de mejora ─│
     │  Aporta un complemento             │
     │ ────────────────────────────────► │  Consolida + reevalúa la versión completa
     │  ◄──── Retro + CIERRE ──────────── │  (Markdown canónico por idea)
```

Una **conversación** es el hilo de `(participante, campaña, pregunta, ciclo)`. El primer ciclo conserva
el id histórico `conv_<campaniaId>_<usuarioId>_<preguntaId>`; una campaña continua puede abrir ciclos
posteriores con id determinista derivado del aporte raíz. Esto evita mezclar ideas ya cerradas con una
idea nueva.

## 2. Reglas detalladas

### 2.1 Primer entrante de un hilo nuevo → enviar la pregunta
Si el participante escribe y aún no existe conversación para el trío `(participante, campaña,
pregunta)`, el sistema **responde con la pregunta vigente** (saludo + texto de la pregunta) y **NO
evalúa** ese primer mensaje. Esta regla aplica aunque el envío inicial de campaña ya esté marcado como
`enviado`, porque ese envío puede haber entregado solo el `MensajeInicial`/saludo y no la pregunta
evaluable. El **siguiente** mensaje ya se evalúa como respuesta según la máquina de estados. (Supuesto:
`SUPUESTOS.md#primer-contacto-pregunta`.)

El **saludo** de este primer entrante es el `MensajeInicial` **activo de la campaña** (el de menor
`orden`), guardado en la base de datos y editable desde el portal, con sus variables `{{nombre}}`,
`{{campania}}`, etc. resueltas. Solo si la campaña no tiene un `MensajeInicial` activo se usa, como
respaldo, el texto configurable `Conversacion:Mensajes:SaludoPrimerContacto`. (No confundir con la
**plantilla de Meta** del primer contacto proactivo, que es global y se gobierna por
`WhatsApp:PlantillaEnvioInicial`; ver `SUPUESTOS.md#plantilla-envio-inicial-campania`.)

En campanias con varias preguntas activas, el orquestador resuelve la pregunta de trabajo por `orden`:
mantiene el hilo abierto actual hasta completar sus revisiones disponibles; cuando ese hilo se cierra,
crea el hilo de la siguiente pregunta activa y la envia como texto libre en la misma ventana. Si un
participante escribe despues de una pregunta cerrada y aun hay preguntas pendientes, el entrante se usa
para abrir/enviar la siguiente pregunta y no se evalua como respuesta. Si todas las preguntas activas ya
estan cerradas, I-19 permite interpretar una petición explícita de revisitar una idea; otro mensaje
posterior se aclara o se ignora según su intención, sin crear una respuesta huérfana.

**P-21 — número de WhatsApp:** el mensaje inicial, reenvío o reintento sale por el alias guardado en
`configConversacional.numeroWhatsAppSaliente` o, si está vacío, por el número predeterminado. Desde que
un participante escribe, toda pregunta, retroalimentación y cierre de ese hilo responde por el mismo
número que recibió el entrante. Si Meta no informa ese destino o el alias no existe, se usa el
predeterminado sin bloquear la conversación.

El avance entre preguntas no exige siempre agotar las revisiones: una pregunta puede cerrarse antes por
**calificacion alta** o porque el **participante pide continuar** (ver §2.3, "Dos salidas anticipadas").

### 2.2 Evaluación con LLM
Con I-19/P-25, cada aporte se integra en una versión acumulada que el servidor confirma internamente y
evalúa en el mismo turno. Solo la **versión consolidada confirmada completa** se evalúa con el LLM usando la
**rúbrica**, el **prompt** aprobado y la **ConfigLLM** activos de la pregunta/campaña. El último
complemento nunca sustituye la idea completa. El modelo debe devolver un JSON con el esquema acordado
(el sistema le incrusta el esquema y la escala). El historial acotado ayuda a evitar repeticiones,
pero no reemplaza la versión canónica. Requisitos
para evaluar:
- ConfigLLM en estado **activo**, prompt **activo y aprobado**, rúbrica **activa**.
- Si falta rubrica, prompt o ConfigLLM valida, no se llama al LLM: se informa al participante que hay
  un problema de configuracion y que debe contactar al administrador; la respuesta queda
  `evaluacionPendiente`, **no se genera Markdown** y la conversacion se cierra.
- Si el proveedor falla o la salida es invalida -> **fallback seguro** (`08 §6`):
  se envia una retro neutra, la respuesta queda `evaluacionPendiente`, **no se genera Markdown** y la
  conversacion se cierra. El motivo queda en `LogSeguridad` y en el detalle tecnico de Resultados
  (`error_proveedor`, `config_llm_no_activa`, `salida_invalida:<razon>`, ...).

**Parafraseo I-05 (apagado por defecto):** si la campaña activa `configConversacional.parafraseo=true`
y el kill-switch `Conversacion:Parafraseo` está activo, el coach puede iniciar su retro con un resumen
de 2–3 frases de lo que entendió. Debe ser fiel al aporte, sin agregar información. Si el modelo no lo
devuelve, viene vacío o no cabe una frase completa en `Conversacion:MaxCaracteresParafraseo` (400 por
defecto), el participante recibe exactamente la retroalimentación de siempre. Operación puede apagarlo
por campaña o globalmente sin redeploy.

**Coaching directo P-25:** la paráfrasis acumulada ya no se muestra para preguntar “¿Es correcto?” en
cada turno. Un aporte sustantivo crea una versión, el servidor la confirma automáticamente y la evalúa
de inmediato. Solo si el consolidador detecta una ambigüedad real se pide una aclaración antes de
evaluar. El flujo anterior de I-19/P-24 queda disponible mediante
`Conversacion:ConfirmacionExplicitaIdeasHabilitada=true` como rollback. Las ideas semilla I-12 se usan
cuando existan, pero no crean criterios de calificación fuera de la rúbrica.

**Redacción fluida I-20 (implementada localmente):** la forma de confirmar, acompañar, aclarar,
reabrir o cerrar se redacta con LLM según campaña, pregunta, idea consolidada y contexto reciente. El
servidor decide el acto; el LLM solo redacta. Cada mensaje tiene una sola intención y como máximo una
pregunta. **El cuerpo —la versión propuesta completa, la retroalimentación validada— lo inserta el
servidor** entre el puente y la pregunta, así que el redactor no puede sustituirlo ni esconderlo. Si la
salida es inválida, larga, con dos preguntas o con cualquier rastro de rúbrica, puntaje, umbral o
promesa de implementación, se **descarta entera** y el turno sale con su **respaldo, que es exactamente
el texto anterior a I-20**; lo mismo si se apaga el kill-switch o si el cupo de llamadas está agotado.
Nunca cambia evaluación, estados ni límites.

**Variación y no duplicación DT-I20-01 (implementada localmente 2026-08-13, sin desplegar):** una expresión como “Queda claro
que...” es válida cuando corresponde, pero no debe convertirse en la apertura fija del coach. Las
instrucciones de evaluación y redacción piden alternar reconocimiento concreto, conexión con el aporte,
pregunta directa o transición breve. Al armar un mensaje, el servidor elimina un puente que repita una
oración o apertura ya presente en el cuerpo validado; así no pueden salir dos “Ya queda claro...” en el
mismo envío. Esta regla aplica a los mensajes nuevos de todas las campañas después del despliegue y no
modifica conversaciones, ideas ni envíos históricos.

### 2.3 Revision determinista (revisiones como oportunidades)
> **Flujo legado/single-idea:** las reglas de contador único, coletilla siempre visible y cierre de la
> pregunta descritas en esta sección se conservan cuando I-18 está apagado. Con coaching secuencial
> efectivo aplica `§2.4.2`: contador y salida por idea, sin coletilla de cierre automática bajo umbral.

Tras una **evaluacion valida**, el sistema ofrece al participante una oportunidad de mejorar su respuesta
con base en la retroalimentacion (envia retro + invitacion) mientras
`RepreguntasUsadas < MaxRepreguntas`.

La **invitacion a mejorar** se arma de forma conversacional y **variada**, no con una frase fija
(Opcion B, 2026-06-23): el **nucleo** es la `repregunta_sugerida` que el LLM devuelve cuando existe
(natural y distinta cada turno); si el LLM no la trae, se usa una variante de respaldo rotada
(`Conversacion:Mensajes:InvitacionMejoraVariantes`, o `InvitacionMejora` si la lista esta vacia). A ese
nucleo se le **anexa siempre** una coletilla rotada (`Conversacion:Mensajes:InvitacionContinuarVariantes`,
o una lista compilada por defecto) que **ensena la salida** del "no quiero seguir" (p. ej. *"si ya te
sientes conforme, escribeme 'asi esta bien' y seguimos"*). Asi el participante **nunca queda atrapado**:
si quiere, pule su respuesta; si no, una frase corta de conformidad cierra el punto (ver salida 2 abajo).
La rotacion es **determinista** por hilo+turno (reproducible y testeable).

Cuando el hilo esta en `esperandoRepregunta` y
`RepreguntasUsadas >= MaxRepreguntas`, el siguiente mensaje del participante **se registra como
`recibida`, no se manda al LLM, no genera retroalimentacion ni Markdown**, y el sistema envia solo el
`MensajeCierre`. Luego, si hay otra pregunta activa pendiente, continua con esa pregunta.

El numero de revisiones lo controla `MaxRepreguntas` (default **1**); con `MaxRepreguntas = 0` se cierra
sin ofrecer mejora. En fallback **no** se ofrece mejora (se cierra con retro neutra). Cada evaluacion
valida compila su propio Markdown; el ultimo intento evaluado es el definitivo.

**Dos salidas anticipadas** evitan que el participante quede atrapado en revisiones cuando ya esta bien
(ambas conviven con `MaxRepreguntas`):

1. **Cierre por calificacion alta (decision del sistema).** Si una evaluacion valida alcanza el umbral
   efectivo (fraccion de la escala de la rubrica en `[0,1]`; **`<= 0` = desactivado**), el sistema **no
   insiste con una revision** aunque queden repreguntas: antepone una felicitacion
   (`Conversacion:Mensajes:MensajeCalificacionAlta`) al cierre, compila el Markdown y avanza a la siguiente
   pregunta. El valor es `configConversacional.umbralCierreAnticipado ?? Conversacion:UmbralCierreAnticipado`;
   pero `Conversacion:CierreAnticipadoHabilitado=false` lo apaga para todas las campañas. El umbral se
   compara como `CalificacionTotal >= Min + Umbral * (Max - Min)`.
2. **Continuar por intencion del participante (salida conversacional).** Estando en `esperandoRepregunta`
   (ya se ofrecio una mejora), si el participante responde con una frase de conformidad
   (`Conversacion:FrasesContinuar`, p. ej. *"asi esta bien"*, *"creo que ya esta bien"*, *"sigamos"*, *"listo"*), el mensaje **se
   registra como `recibida`, no se evalua**, el sistema antepone un acuse calido
   (`Conversacion:Mensajes:AcuseContinuar`) al `MensajeCierre` y avanza. La deteccion es **hibrida
   determinista**: igualdad exacta con una frase, o contencion de la frase solo si el mensaje es corto
   (`Conversacion:MaxCaracteresIntencionContinuar`, default 40), comparando sin mayusculas/acentos/puntuacion.
   Esta deteccion **solo** aplica a la respuesta de revision; el primer mensaje (la respuesta real) siempre
   se evalua. La invitacion a mejorar (§3) ya ensena la frase de salida para que el camino feliz coincida.
3. **Rechazo explicito del guardado (I-17 §5.4, "guardar salvo que diga no").** Una idea que supera el
   umbral se clasifica **madura** y se guarda automaticamente (y solo entonces se le antepone la parafrasis
   "esto es lo que entendi", §2.2). Si, estando en `esperandoRepregunta` con al menos una respuesta madura
   en el hilo, el participante responde con una **frase de rechazo** (`Conversacion:FrasesRechazoGuardado`,
   p. ej. *"no"*, *"no es eso"*, *"borralo"*; misma deteccion hibrida que la salida por conformidad), el
   sistema **degrada esa(s) respuesta(s) de madura a incubacion** (regenera su Markdown y registra
   telemetria), **no evalua** el mensaje, antepone el acuse `Conversacion:Mensajes:AcuseRechazoGuardado` al
   `MensajeCierre` y cierra. Si no hay ninguna idea madura que rechazar, el mensaje cae al flujo normal (se
    evalua), para no cortar al participante por una negacion sin contexto de guardado. El alias de una sola
    palabra *"no"* solo coincide si es todo el mensaje: *"no mas"* y *"no quiero continuar"* son salidas,
    no rechazos de guardado. La degradacion nunca
    promueve (idempotente) y no toca contratos compartidos.

### 2.3.1 Intenciones de parar o avanzar escritas libremente (P-27, especificada)

P-27 corrige primero el detector determinista con alias inequívocos como “quiero parar aquí”, “stop
now”, “no quiero continuar”, “no más” y “quiero pasar a otra idea”. Esta corrección no depende del LLM. Para expresiones cortas que no
coincidan con el catálogo y solo cuando el servidor ya espera una mejora o una aclaración, una función
opcional puede proponer una de cuatro intenciones cerradas:
`aportar|finalizarIdea|finalizarParticipacion|ambigua`.

El modelo **no ejecuta** la transición: el servidor valida estado, autorización, precedencia,
idempotencia, cupos e idea activa. `finalizarIdea` termina solo la idea activa y avanza;
`finalizarParticipacion` cierra la cola/hilo sin abrir otra unidad; `aportar` continúa por
consolidación/evaluación; y `ambigua` abre una confirmación determinista con opciones 1/2/3. El
mensaje de control se conserva para auditoría, pero nunca se incorpora a la idea, evaluación o
Markdown.

La ruta flexible nace apagada globalmente y por campaña. No aplica al primer aporte, mensajes largos,
selecciones P-26/I-19, rechazo o reapertura. Si el clasificador falla, devuelve JSON inválido o no hay
cupo, el servidor no cierra nada y conserva el mensaje como aporte. Ver
`Iniciativas/P-27_Clasificacion_Flexible_Intenciones_Control.md`.

### 2.4 Cierre y Markdown
Para que “cierre” no sea ambiguo, estas son las reglas canónicas:

| Evento | Qué cierra | Resultado | Siguiente paso |
|---|---|---|---|
| La versión alcanza el umbral | La **idea activa** | `madura`, pendiente de curaduría | Siguiente idea; si no hay, siguiente pregunta |
| “Así está bien” / finalizar idea | La **idea activa** | `pendiente` si no era madura | Siguiente idea; si no hay, siguiente pregunta |
| “No lo guardes” | La **idea activa** | `rechazada` y auditable | Siguiente idea; si no hay, siguiente pregunta |
| Tope, fallback o tiempo por idea | La **idea activa** | `pendiente` | Siguiente idea; si no hay, siguiente pregunta |
| Finalizar participación | Cola/hilo actual | Conserva las ideas ya cerradas y finaliza las abiertas de forma segura | No abre otra pregunta ni otro ciclo |
| Inactividad de sesión | Conversación abierta | Ideas abiertas quedan `pendiente` con motivo `inactividad` | Queda cerrada; P-29 solo añadirá el aviso humano |
| Cierre administrativo de campaña | Toda interacción de esa campaña | Conserva trazabilidad | No recibe ni reabre aportes |

**Finalizar una idea no finaliza la participación; finalizar la participación no cierra la campaña.**
El umbral se denomina históricamente `umbralCierreAnticipado`, pero en el flujo canónico I-19 su
efecto de negocio es la **finalización por madurez de la idea**. El kill-switch
`CierreAnticipadoHabilitado` conserva el atajo del flujo legado; no autoriza al LLM a cerrar ni cambia
la clasificación server-side de madurez.

En I-19, el cierre deja una única idea como `madura`, `pendiente` o `rechazada` y compila su Markdown
canónico. Una idea madura queda `pendiente` de curaduría experta; no se publica ni prioriza
automáticamente. Una conversación cerrada no acepta contenido nuevo como otra respuesta independiente,
pero puede reabrir la misma idea ante una petición explícita mientras la campaña siga activa. Con
P-26 y `participacionContinua=true`, un aporte posterior **no modifica esa conversación cerrada**:
crea otro ciclo/conversación y otra idea.

El Markdown de la idea muestra para curaduría campaña, pregunta, umbral de madurez efectivo con origen
y calificación total como `X de Y puntos` —por ejemplo `Umbral de madurez: 3,4 de 5 puntos (60 %;
global)` y `Calificación total: 4 de 5 puntos`—; si esa versión aún no tiene evaluación, dice
`pendiente de evaluación` y no muestra umbral. Es información **administrativa**: no se envían puntajes
ni rúbrica al participante durante el acompañamiento.

### 2.4.1 Multi-idea por mensaje (I-06, implementado; flags apagados)
Si la campaña tiene `configConversacional.segmentacionIdeas=true` y el kill-switch global
`Conversacion:SegmentacionIdeas` no está apagado, una respuesta puede separarse en varias ideas antes
de evaluarse. Cada idea válida se guarda como una `Respuesta` independiente, con su propia evaluación y
Markdown, pero el participante no recibe N mensajes técnicos: el sistema debe contestar de forma breve
y agregada para confirmar el registro del turno. Si el segmentador falla, devuelve una salida inválida
o no quedan ideas válidas después de las guardas, el sistema vuelve al modo probado: **1 mensaje = 1
respuesta**.

### 2.4.2 Coaching secuencial por idea (I-18, implementado; apagado por defecto)

Cuando I-06 es efectivo y la campaña activa `configConversacional.coachingSecuencialIdeas`, el sistema
crea una cola en el orden original y trabaja con **una idea activa**:

1. evalúa las raíces y omite de la cola de mejora las que ya alcanzan el umbral;
2. envía para la primera pendiente una retro breve y exactamente una pregunta sobre su criterio más
   débil, sin puntajes, rúbrica, respuesta propuesta ni ejemplo;
3. toma el siguiente contenido como revisión de esa idea, conserva el linaje y lo evalúa;
4. finaliza por `umbral`, `participante`, `rechazo`, `maxRevisiones`, `tiempo`, `fallback` o
   `desactivacion`;
5. activa la siguiente idea y, al acabar, abre la siguiente pregunta.

`MaxRepreguntas` se cuenta por idea y la respuesta a la última pregunta siempre se evalúa. “Así está
bien” finaliza solo la idea activa; “no lo guardes” degrada solo esa idea. Mientras siga bajo umbral y
pueda mejorar no se anexa automáticamente “si ya te sientes conforme…”. La transición la decide el
servidor; `recomendacion` del LLM no cierra por sí sola. P-27 amplía las expresiones naturales de
salida, pero conserva esta autoridad server-side.

Activación: kill-switch `Conversacion:CoachingSecuencialIdeas=true`, campo de campaña en `true` e I-06
efectivo. El reloj opcional por idea usa `MinutosCoachingPorIdea`; es distinto del cierre de sesión de
`§2.6`. Al vencer, finaliza solo la idea activa y avanza; si la ventana de servicio está abierta,
envía una sola repregunta sobre la siguiente idea mediante el flujo saliente normal. Fuera de la
ventana no envía texto libre y espera un nuevo entrante. Ver
`Iniciativas/I-18_Coaching_Secuencial_Por_Idea.md`.
Si un gate se apaga con una cola activa, no se envía otra repregunta: el siguiente entrante se
conserva sin evaluación, la cola finaliza por `desactivacion` y el flujo avanza de forma segura.

### 2.4.3 Consolidación progresiva y coaching directo por idea (I-19/P-25)

I-19 aplica a todas las campañas. El recorrido está implementado localmente para una idea única, para la
cola I-18/multi-idea y para la reapertura de una idea anterior del mismo hilo. El comportamiento para
ideas únicas o múltiples es:

1. cada mensaje significativo queda como aporte original enlazado a un `ideaId`;
2. el sistema crea una versión que acumula la versión confirmada anterior y el aporte nuevo;
3. si no hay ambigüedad, el servidor confirma esa versión automáticamente y la evalúa en el mismo turno;
4. solo la versión completa confirmada se evalúa y gobierna retroalimentación, umbral, madurez y
   Markdown;
5. bajo umbral, la idea continúa con una pregunta socrática; al terminar queda `pendiente`;
6. al superar el umbral queda `madura` y `pendiente de curaduría`;
7. “no lo guardes” deja la idea `rechazada`, conservada solo para auditoría;
8. complemento + idea nueva actualiza la activa y añade la nueva al final de la cola.

Con varias ideas en un mismo mensaje, el sistema propone la versión de cada una pero **solo trabaja y
evalúa la idea activa**; las demás esperan su turno en silencio y se trabajan al cerrarse la
anterior. Lo mismo ocurre con una idea nueva que aparece durante el acompañamiento: se registra aparte,
no se mezcla con la idea en curso y no se anuncia hasta que llega su turno. El servidor limita cuántas
ideas caben (`Conversacion:MaxIdeasPorMensaje`), descarta fragmentos y repeticiones, y mantiene una sola
idea activa. La confirmación automática no consume una revisión: el tope de repreguntas sigue contando
solo las preguntas socráticas posteriores a una evaluación. Si se agota un techo determinista (turnos, cupo de
llamadas o presupuesto de la campaña) durante el acompañamiento, el aporte se conserva, no se evalúa y
la idea activa queda `pendiente` antes de pasar a la siguiente.

Después de una retroalimentación, “así está bien” termina la mejora y deja la idea pendiente si todavía
no alcanzó el umbral. Un complemento se integra y se reevalúa inmediatamente; “vamos a mejorarla” sigue
siendo compatible con conversaciones históricas que ya esperaban confirmación y no se agrega a la
versión. Una idea nueva explícita durante el coaching se encola aunque la segmentación automática
inicial I-06 esté apagada.

Mientras la campaña esté activa, el participante puede pedir “quiero complementar la anterior”. Se
reabre el mismo `ideaId`; si la referencia es ambigua, el sistema muestra una lista breve numerada. La
nueva versión confirmada se reevalúa completa y puede subir o bajar de madurez. Una campaña cerrada no
admite cambios del participante. Si estaba pendiente de curaduría, la reapertura suspende ese estado
hasta reevaluar.

Al reabrir, el mensaje recuerda cómo quedó registrada la idea y pide qué cambiar o agregar; la versión
confirmada anterior sigue siendo la oficial y **no se sobrescribe ninguna versión**. La idea que estaba
en curso se conserva en su estado y espera turno: sigue habiendo una sola idea activa. Cuando el sistema
ofrece la lista numerada, solo cuenta como elección un número corto dentro de esa lista; cualquier otra
respuesta cancela la selección y se procesa como un turno normal de la idea en curso, sin adivinar ni
perder el mensaje. Las frases de estas dos intenciones son configurables
(`Conversacion:FrasesRevisitarAnterior` y `Conversacion:FrasesRevisitarIdea`). **Alcance actual:** se
reabren ideas del mismo hilo; volver a la idea de otra pregunta todavía no está implementado.

No hay flag por campaña. `Conversacion:ConsolidacionProgresivaHabilitada=true` es solo un kill-switch
global de emergencia y nace activo; al apagarlo, los aportes nuevos quedan pendientes y no se
califican de forma aislada. Ver `Iniciativas/I-19_Consolidacion_Progresiva_Ideas.md`.

### 2.5 Ventana de 24 h y respuestas tardías
- WhatsApp solo permite **texto libre** dentro de las **24 h** posteriores al último mensaje del
  participante. El sistema responde siempre con texto libre.
- Cuando el participante escribe (aunque sea **días después**), su mensaje **reabre** la ventana de 24 h,
  así que la respuesta del sistema (retro/cierre) **se entrega sin problema**.
- **No hay** mensajes proactivos fuera de ventana (recordatorios): requerirían una plantilla (HSM)
  aprobada por Meta, no implementada.

### 2.6 Expiración por inactividad (parametrizable, con granularidad sub-hora — I-17 §7)
Para blindar el sistema, un hilo **abierto** sin actividad pasada su ventana se **cierra
automáticamente** (cierre silencioso, sin mensaje). Lo ejecuta un barrido periódico
(`TrabajadorExpiracionConversaciones`). Si el participante no contesta, su hilo no queda abierto para
siempre. La última evaluación registrada (si la hubo) queda como definitiva.
**La ventana es parametrizable por campaña con granularidad en minutos (I-17, cierre por inactividad
~5 min del 20-jul):** la ventana efectiva de cada campaña se resuelve como
`ConfigConversacional.MinutosInactividadSesion` (override por campaña; `<= 0` la apaga para esa campaña)
→ default global `Conversacion:MinutosInactividadSesion` → `Conversacion:HorasExpiracionSinRespuesta`
(legacy, en horas). El barrido cierra **por campaña** con su propia ventana. El interruptor **maestro**
es global: si tanto `MinutosInactividadSesion` como `HorasExpiracionSinRespuesta` globales son 0, el
barrido no corre (default off, D1) y los overrides por campaña quedan inactivos. Ver parámetros abajo.

### 2.7 Rechazo de no autorizados
Un número que no resuelve a un participante válido (no matriculado, inactivo, rol no participante, sin
campaña activa o sin pregunta vigente) se **rechaza de forma neutral**; el motivo solo se registra en
`LogSeguridad` y en el log del webhook (nunca se revela al usuario).

### 2.8 Cupos y techos deterministas (guardrails, `10 §2` / D2 del plan de Hito 1)
Tres límites deterministas acotan el consumo por participante. **El LLM propone, el sistema dispone**:
estos techos garantizan terminación y costo acotado con independencia del comportamiento del modelo.
Todos dejan rastro `RateLimit` en `LogSeguridad` con el motivo interno; nada de esto se revela al
participante más allá del cierre normal.

1. **Cupo de mensajes por usuario/campaña** (`Campania.ConfigSeguridad.maxMensajesPorUsuario`, editable
   por el portal). Al exceder, el entrante se **descarta con rechazo neutral silencioso** (como una
   conversación cerrada): no se persiste, no se responde, no se evalúa. Motivo `cupo_mensajes_usuario`.
2. **Cupo de llamadas LLM por usuario/campaña** (`Campania.ConfigSeguridad.maxLlamadasLlmPorUsuario`).
   El contador es el número de `Evaluacion` registradas (cada llamada al LLM persiste exactamente una,
   válida o fallback). Al exceder, **no se llama al LLM**: la respuesta se registra como `recibida` y el
   hilo **cierra elegante** con el `MensajeCierre`, **sin** abrir la siguiente pregunta (tampoco podría
   evaluarse). Motivo `cupo_llamadas_llm_usuario`.
3. **Techo duro de turnos por hilo** (`Conversacion:MaxTurnosPorHilo`, global). Cuenta los entrantes del
   hilo (incluido el primer contacto). Al alcanzarlo, el siguiente entrante se registra como `recibida`
   sin evaluar y el hilo cierra elegante, avanzando a la siguiente pregunta si la hay. Motivo
   `tope_turnos_hilo`. Dimensionar ≈ `2 + MaxRepreguntas` + margen.

Los cupos 1 y 2 están **gateados por `Conversacion:CuposHabilitados` (default `false`)**: los límites ya
viven en la campaña (contrato `03`), pero no se aplican hasta encender el flag (regla D1: nada nuevo
activo por defecto; el flag se enciende en staging y en el freeze). **Antes de habilitarlo hay que
dimensionar los límites de la campaña**: `maxLlamadasLlmPorUsuario ≈ preguntas × (1 + MaxRepreguntas)` y
`maxMensajesPorUsuario ≈ preguntas × (2 + MaxRepreguntas)` + margen (los defaults del portal, 10 y 2,
se pensaron para una campaña de una pregunta). El techo 3 es independiente del flag (0 = desactivado).
Regla del equipo (D2): **no se retira el tope determinístico de revisiones (I-01) hasta que estos cupos
estén activos en producción.**

**P-26 — ventana móvil para campañas continuas:** si
`configConversacional.participacionContinua=true`, los cupos 1 y 2 cuentan únicamente los eventos de
las últimas 24 horas para ese usuario/campaña. La ventana es móvil y compartida por todos sus ciclos y
preguntas. En campañas no continuas se conserva el acumulado actual. El presupuesto total de tokens
de la campaña nunca se reinicia.

### 2.9 Tejido colectivo (I-09, diseño Sprint 1a — core Sprint 1b)
> **⚠️ DIFERIDO del MVP (reunión GHT 20-jul → Capa 3 post-convención).** El comportamiento está
> implementado pero el flag `tejidoColectivo` queda **OFF para el Hito**: en el go-live el coach es
> **siempre autocontenido**. Esta sección describe el comportamiento para cuando se reactive en la
> Capa 3. Ver `Iniciativas/00_Indice §1.3`.

Cuando la campaña tiene `configConversacional.tejidoColectivo=true` y el kill-switch global
`Conversacion:TejidoColectivo` no está apagado, el coach **deja de ser autocontenido**: antes de
evaluar/retroalimentar, recupera resúmenes **anonimizados** de aportes de otros participantes de la
**misma campaña** (relevantes por solapamiento de tema y tags) y los teje en la conversación. El
participante nunca ve nombres ni números de terceros; solo percibe un coach que conecta su aporte con
lo que otros han dicho. Reglas duras de esta función:

- Los aportes entran al modelo como **dato no confiable delimitado** (`08 §3.2`), nunca como
  instrucción; se sanitizan y se acotan por presupuesto de tokens (inyección transitiva, `08 §5.9`).
- **Anonimización obligatoria:** solo `temas/entidades` + un extracto sanitizado del texto; jamás el
  autor. Solo se teje bajo campañas con consentimiento de uso colectivo (P-07).
- **Degradación limpia:** si no hay aportes relevantes o falla la recuperación, la conversación es
  **autocontenida** (modo probado), sin fallo visible. La recuperación nunca bloquea el hilo.
- **Apagado por defecto:** `tejidoColectivo=false` por campaña → autocontenido, sin redeploy.

### 2.10 Participación continua y selección de campaña/pregunta (P-26)

El interruptor por campaña `configConversacional.participacionContinua` controla si un participante
puede iniciar ideas nuevas después de completar su recorrido. Campo ausente/`false` conserva el flujo
actual. `true` solo tiene efecto si la campaña está `activa`; no permite escribir en campañas
cerradas, archivadas o en borrador.

Orden del flujo:

1. una respuesta de coaching continúa en la afinidad vigente sin volver a preguntar campaña;
2. sin idea activa se calculan campañas autorizadas con trabajo pendiente o participación continua;
3. una campaña se elige automáticamente; varias producen una lista numerada;
4. una pregunta elegible se elige automáticamente; varias producen otra lista;
5. el aporte original se conserva y se procesa exactamente una vez después de elegir;
6. la selección vence en 24 horas, pero el aporte queda auditable;
7. después de cerrar una idea, otro aporte sustantivo crea conversación/idea/Markdown independientes;
8. solo una intención explícita de complementar/revisitar conserva el `ideaId` anterior.

La selección acepta número o nombre/texto exacto no ambiguo y se vuelve a validar contra campaña,
asociación y pregunta. El LLM no elige el alcance. Apagar el interruptor deja terminar una idea ya
activa y bloquea otra; cerrar la campaña detiene inmediatamente la interacción. Ver P-26.

### 2.11 Reingreso, pausa y retomar una idea (P-28/P-29/P-30 implementadas localmente)

Estas iniciativas no crean otra variante de participación continua. Completan vacíos concretos sobre
la base ya implementada:

1. **P-28 — reingreso:** con `Conversacion:DespertarProactivoHabilitado=true`, si no hay afinidad ni
   trabajo pendiente y el mensaje es un saludo o una petición determinista de iniciar/continuar,
   presenta una bienvenida breve. Una campaña se resuelve automáticamente; varias usan el menú P-26 y
   el saludo queda marcado como entrada, no como aporte. Al escoger, ese registro se completa sin crear
   conversación, idea ni respuesta. Un aporte sustantivo nuevo no espera este saludo: P-26 resuelve
   alcance y abre el ciclo nuevo directamente. P-28 no es requisito técnico para que una campaña
   continua reciba una idea nueva.
2. **P-29 — pausa por tiempo:** I-17/I-19 ya miden inactividad y cierran de forma idempotente. P-29
   solo agrega un aviso de pausa, con fallback, cuando la ventana de WhatsApp lo permite; no crea otro
   temporizador ni cambia el estado de la idea. Con
   `Conversacion:CierrePorTiempoHabilitado=true`, cada hilo que el barrido acaba de cerrar recibe **un
   solo** aviso: el hilo ya quedó cerrado, así que el barrido siguiente no vuelve a listarlo. Si la
   ventana de 24 h venció o la campaña se cerró administrativamente, el aviso se omite y el cierre se
   conserva igual. Apagado, el cierre por inactividad opera exactamente como hoy. El texto lo redacta
   el LLM y cae al respaldo determinista si no está disponible; nunca menciona rúbrica ni puntajes y
   **no hace preguntas**, porque no espera respuesta: quien quiera seguir simplemente vuelve a
   escribir y el reingreso lo resuelven P-26/P-28/P-30.
3. **P-30 — retomar:** I-19/P-26 ya reabren explícitamente la idea reciente del alcance vigente y
   conservan su `ideaId`. P-30 añade la lista determinista de ideas históricas del propio participante,
   sin filtrar por estado, dentro de campaña y pregunta ya resueltas. “Sin importar el estado” nunca
   permite ignorar autorización, campaña activa ni aislamiento entre preguntas. La intención y la
   opción de menú no son aportes. Tras elegir, la ruta conserva afinidad con el ciclo histórico para
   que el siguiente texto modifique esa idea aunque exista un ciclo iniciado después.

Ante un mensaje sin flujo activo, la precedencia final será: petición explícita de retomar → P-30;
aporte sustantivo nuevo elegible → P-26; saludo/petición de entrada → P-28; sin elegibles → rechazo
neutral. El LLM puede redactar un saludo o una pausa, pero no decide cuál de esas transiciones aplicar.

### 2.12 Idioma del participante y textos editables (P-32, especificada)

1. El idioma sale de `Usuario.Idioma` (`es|en`, default `es`); no se autodetecta ni lo decide el LLM.
2. Al crear un hilo/ciclo se guarda un snapshot. Editar el maestro no cambia una conversación abierta;
   el siguiente hilo/ciclo usa el nuevo valor.
3. Todo texto visible se resuelve en ese idioma: campaña/pregunta/cierre desde sus localizaciones;
   saludos, ayudas, menús, errores y frases desde el catálogo global activo.
4. Para `en` no existe fallback silencioso a `es`. Una campaña incompleta se bloquea antes de activar
   o enviar; en runtime no se inventa contenido ni se avanza estado.
5. Los aportes e historial se guardan en su idioma original. No hay traducción automática.
6. Evaluación, consolidación, segmentación, intención y redacción reciben el idioma, pero estados,
   límites, ids, JSON y decisiones siguen siendo invariantes y server-side.
7. El contenido global se versiona en Cosmos `config` y puede activarse/revertirse sin build. JSON es
   formato de importación/exportación de borradores; no es la fuente primaria del repositorio.
8. Variables de entorno conservan flags, límites, timeouts, caché y mapeos Meta. Los textos/frases
   editoriales se migran al catálogo y sus claves legacy quedan deprecadas.
9. **DT-P32-02:** una base curada `es/en` no depende de legacy. El administrador puede descargar,
   editar y reimportar el JSON completo; primero se prevalida y luego crea una versión nueva en
   borrador. Nunca sobrescribe o activa. Una campaña bilingüe exige catálogo activo por idioma.
10. **DT-P32-03 (corte 1/2):** el cierre visible se resuelve una sola vez para **todas** las rutas
    (cierre normal, umbral/tope, intención de salida, rechazo/avance, cupo LLM, fallback de
    evaluación, inactividad y cierre visible P-33). Con el catálogo apagado se conserva
    `configConversacional.mensajeCierre` tal cual; encendido manda
    `localizaciones.{idioma}.mensajeCierre` del snapshot del hilo. Si ese texto falta, el hilo cierra
    con el mensaje de configuración no disponible de su propio idioma y no se abre la siguiente
    pregunta: **jamás se responde con el cierre de otro idioma ni se traduce automáticamente**.

### 2.13 Consulta y cierre visible de la idea (P-33, especificada)

1. Una consulta pura como «¿cómo va mi idea?» se resuelve antes de menús, afinidades y aportes. No
   crea respuesta, versión, evaluación ni Markdown, no consume repregunta y no cambia madurez.
2. «Mi idea» significa la idea activa. Si no existe, el servidor elige la idea propia no rechazada con
   trabajo más reciente dentro de campañas activas y asociaciones vigentes. No muestra menú por
   defecto; «otra idea» o «la anterior» conserva la selección explícita de P-30.
3. Se muestra carácter a carácter la versión vigente de I-19. El LLM solo puede proponer un puente
   breve; nunca elige la idea, la versión ni una transición, y no resume ni traduce el contenido.
4. Una consulta sobre una idea cerrada crea afinidad por un mensaje significativo y máximo 24 horas.
   Una corrección o complemento claro reabre esa misma idea; «gracias», un saludo, otra consulta o una
   intención de cambio/control consume o desvía la afinidad sin reabrir.
5. Un mensaje mixto («muéstrame mi idea y agrega…») no se intercepta como consulta pura: se procesa
   como aporte para no perder la corrección.
6. Antes de los cierres normales se muestra la última versión disponible. El rechazo explícito y el
   cierre administrativo no la muestran; participación/inactividad con varias ideas muestra solo la
   última y reconoce que las demás quedaron guardadas.
7. Fuera de la ventana de servicio no se fuerza texto libre ni plantilla. Usuario, asociación y
   campaña se revalidan tanto al consultar como al reabrir.

La precedencia queda: dedupe/identidad/autorización → consulta P-33 → afinidad P-33 → P-26/P-30 →
controles P-27 → aporte normal. P-33 tiene gate propio y no depende de P-27 ni del umbral de P-31.

## 3. Parámetros configurables

| Parámetro | Dónde se configura | Default | Efecto |
|---|---|---|---|
| `Usuario.idioma` | Portal/carga masiva/API | `es` | **P-32:** fuente de verdad `es|en`; se copia al hilo/ciclo y al envío. |
| Catálogo global `CatalogoTextosConversacion` | Portal/API, Cosmos `config` | respaldo compilado `es/en` | **P-32:** mensajes y frases versionados por idioma; una versión activa por idioma, rollback sin build. |
| `Campania.idiomasHabilitados` + `localizaciones` | Portal admin (campaña/mensajes/preguntas) | `["es"]` / campos legacy españoles | **P-32:** contenido propio por idioma bajo los mismos ids; inglés incompleto no se activa. |
| `Conversacion:CatalogoTextosHabilitado` | App config / env | `false` | **P-32:** gate de migración. OFF conserva exacto el camino actual; ON usa catálogo/caché/respaldo del mismo idioma. |
| `Conversacion:CatalogoTextos:CacheSegundos` | App config / env | `60` recomendado | **P-32:** expiración de caché; valor operativo, no contenido editorial. |
| `Conversacion:CatalogoTextos:MaxFrasesPorGrupo` | App config / env | `100` (techo 500) | **DT-P32-02:** permite ampliar listas existentes sin compilar; exceso se rechaza, nunca se trunca. |
| `Conversacion:CatalogoTextos:MaxBytesImportacionJson` | App config / env | `262144` (techo 1 MiB) | **DT-P32-02:** límite previo a deserializar edición masiva; no contiene contenido en logs. |
| `MaxRepreguntas` (pregunta / campaña) | Portal admin (campaña/pregunta) | 1 | Techo técnico de preguntas socráticas por idea (0 = ninguna). Puede configurarse alto para acompañar hasta madurez; no es la salida normal de una idea. |
| `Conversacion:UmbralCierreAnticipado` | App config / env `Conversacion__UmbralCierreAnticipado` | 0 (**desactivado**) | Default numérico heredable para campañas sin override; fracción de la escala `[0,1]`. |
| `configConversacional.umbralCierreAnticipado` | Portal admin (campaña) | `null` (**hereda global**) | Override opcional por campaña; `<= 0` apaga solo esa campaña. |
| `Conversacion:CierreAnticipadoHabilitado` | App config / env `Conversacion__CierreAnticipadoHabilitado` | `true` | Kill-switch global: `false` apaga el cierre anticipado para todas las campañas, incluidos sus overrides. |
| `Conversacion:FrasesContinuar` | App config / env `Conversacion__FrasesContinuar__0`, `...__1` | (lista compilada) | Frases con las que el participante pide continuar a la siguiente pregunta. Vacío = usa la lista por defecto. |
| `Conversacion:FrasesFinalizarIdea` | App config / env `Conversacion__FrasesFinalizarIdea__0`, `...__1` | (lista compilada) | **P-27 / DT-P27-01 DONE** — alias para terminar la idea actual. Ausente/vacía usa el default; vacíos, duplicados normalizados o exceso de límite descartan la lista completa y hacen fallback seguro. |
| `Conversacion:FrasesFinalizarParticipacion` | App config / env `Conversacion__FrasesFinalizarParticipacion__0`, `...__1` | (lista compilada) | **P-27 / DT-P27-01 DONE** — alias para terminar la participación. Ausente/vacía usa el default; vacíos, duplicados normalizados o exceso de límite descartan la lista completa y hacen fallback seguro. |
| `Conversacion:MaxFrasesFinalizacion` / `:VersionFrasesFinalizacion` | App config / env `Conversacion__…` | `20` / huella segura | **DT-P27-01** — límite por lista y etiqueta opcional de la versión conjunta. Al iniciar se deja historial append-only de aplicada/default/descartada sin exponer aliases; el rollback restaura una revisión previa del origen de configuración o vacía ambas listas para el default. |
| `Conversacion:FrasesSolicitarMejora` | App config / env `Conversacion__FrasesSolicitarMejora__0`, `...__1` | (lista compilada) | **P-24** — frases cortas como “vamos a mejorarla”. Solo con propuesta pendiente: confirman implícitamente su versión completa para evaluarla y abrir coaching; no crean un aporte. Vacío = usa la lista por defecto. |
| `Conversacion:ConfirmacionExplicitaIdeasHabilitada` | App config / env `Conversacion__ConfirmacionExplicitaIdeasHabilitada` | `false` en la aplicación distribuida | **P-25** — `false` confirma internamente y evalúa cada versión sustantiva en el mismo turno; `true` restaura temporalmente la confirmación explícita I-19/P-24. No tiene opt-in por campaña. |
| `Conversacion:FrasesRevisitarAnterior` | App config / env `Conversacion__FrasesRevisitarAnterior__0`, `...__1` | (lista compilada) | **I-19 §4.7** — frases que piden volver a la **idea cerrada más reciente** ("la anterior"). Resuelven sin lista de opciones. Vacío = usa la lista por defecto. |
| `Conversacion:FrasesRevisitarIdea` | App config / env `Conversacion__FrasesRevisitarIdea__0`, `...__1` | (lista compilada) | **I-19 §4.7** — frases que piden revisitar **alguna** idea previa sin señalar cuál; con varias candidatas se ofrece la lista numerada. Vacío = usa la lista por defecto. |
| `Conversacion:Mensajes:AcuseReaperturaIdea` / `:InvitacionReaperturaIdea` / `:PreguntaSeleccionIdea` | App config / env `Conversacion__Mensajes__…` | (textos por defecto) | **I-19 §4.7** — acuse y invitación del mensaje de reapertura, y encabezado de la lista numerada. Nunca incluyen calificaciones. |
| `Conversacion:Mensajes:EncabezadoSeleccionCampania` / `:InstruccionSeleccionCampania` / `:AyudaSeleccionCampaniaInvalida` | App config / env `Conversacion__Mensajes__…` | (textos por defecto) | **P-26 §2.10** — encabezado e instrucción del menú numerado de campañas elegibles, y ayuda que antecede al menú tras una opción inválida. El menú solo muestra campañas autorizadas. |
| `Conversacion:Mensajes:EncabezadoSeleccionPregunta` / `:InstruccionSeleccionPregunta` | App config / env `Conversacion__Mensajes__…` | (textos por defecto) | **P-26 §2.10** — encabezado e instrucción del menú numerado de preguntas dentro de la campaña ya elegida. Solo aparece con varias preguntas elegibles; con una sola se avanza automáticamente. |
| `Conversacion:FrasesCambiarCampania` | App config / env `Conversacion__FrasesCambiarCampania__0`, `...__1` | (lista compilada) | **P-26 §2.10** — frases con las que el participante pide explícitamente cambiar de campaña ("otra campaña"). Suspenden la afinidad vigente **sin cerrar ni rechazar** la idea y recalculan las opciones. Vacío = usa la lista por defecto. |
| `Conversacion:DespertarProactivoHabilitado` | App config / env `Conversacion__DespertarProactivoHabilitado` | `false` | **P-28** — habilita la bienvenida para saludo/inicio breve sin flujo; OFF conserva P-26 para aportes sustantivos. |
| `Conversacion:MaxCaracteresDespertarProactivo` / `:FrasesDespertarProactivo` | App config / env `Conversacion__…` | `80` / lista compilada | **P-28** — límite y vocabulario deterministas; texto largo o no coincidente se trata por la ruta normal. |
| `Conversacion:Mensajes:SaludoReactivacion` | App config / env `Conversacion__Mensajes__SaludoReactivacion` | texto de respaldo | **P-28** — texto seguro si la redacción LLM del acto de reactivación no está disponible o es inválida. |
| `Conversacion:CierrePorTiempoHabilitado` | App config / env `Conversacion__CierrePorTiempoHabilitado` | `false` | **P-29** — habilita el aviso de pausa posterior al cierre por inactividad. Gobierna **solo el mensaje**: apagado, el cierre de I-17 sigue operando igual. No cambia umbral, estado ni motivo de cierre. |
| `Conversacion:Mensajes:PausaPorInactividad` | App config / env `Conversacion__Mensajes__PausaPorInactividad` | texto de respaldo | **P-29** — texto determinista del aviso de pausa; también es el fallback cuando la redacción LLM no está disponible. Nunca menciona rúbrica ni puntajes. |
| `promptRefs.cierre` (campaña / pregunta) | Portal admin (campaña/pregunta) | ausente | **P-29** — voz opcional del aviso de pausa; ausente, hereda la voz general del hilo (`conversacion` → `retro`) y, si tampoco existe, el texto de respaldo. |
| `Conversacion:RetomarIdeasHabilitado` | App config / env `Conversacion__RetomarIdeasHabilitado` | `false` | **P-30** — habilita el selector histórico. OFF conserva la reapertura reciente I-19/P-26. |
| `Conversacion:Mensajes:InstruccionSeleccionIdea` / `:SinIdeasHistoricas` | App config / env `Conversacion__Mensajes__…` | textos de respaldo | **P-30** — instrucción de número/resumen exacto y respuesta neutral cuando no hay candidatas. |
| `Conversacion:VisibilidadIdeaParticipanteHabilitada` | App config / env `Conversacion__VisibilidadIdeaParticipanteHabilitada` | `false` | **P-33** — kill-switch de consulta bajo demanda y visibilidad al cierre; OFF conserva el flujo anterior. |
| `configConversacional.consultaIdea` / `mostrarIdeaAlCerrar` | Portal admin (campaña) | `true` / `true` | **P-33** — opt-out independiente por campaña para consulta y cierre; solo tienen efecto con el gate global ON. |
| `Conversacion:MaxCaracteresConsultaIdea` / catálogo `frases.consultarIdea` | App config + catálogo P-32 | `220` / lista `es|en` | **P-33** — límite y vocabulario de consulta pura; un mensaje mixto conserva la ruta de aporte. |
| Catálogo P-32: `encabezadoConsultaIdea`, `invitacionConsultaIdea`, `encabezadoCierreIdea`, `otrasIdeasGuardadas`, `sinIdeaDisponible`; frases `consultarIdea`, `acuseConsultaIdea`, `nuevaIdea` | Portal/API, Cosmos `config` | respaldo compilado `es/en` | **P-33** — amplía el registro a 29 mensajes y 16 listas sin mutar versiones históricas. |
| `Conversacion:MaxCaracteresIntencionContinuar` | App config / env `Conversacion__MaxCaracteresIntencionContinuar` | 40 | Largo máximo (normalizado) para que una frase contenida cuente como intención; la igualdad exacta siempre cuenta. |
| `Conversacion:Mensajes:MensajeCalificacionAlta` | App config / env `Conversacion__Mensajes__MensajeCalificacionAlta` | "¡Excelente! Tu respuesta ya está muy completa…" | Felicitación que antecede al cierre por calificación alta. |
| `Conversacion:Mensajes:AcuseContinuar` | App config / env `Conversacion__Mensajes__AcuseContinuar` | "¡Perfecto, sigamos!" | Acuse que antecede al cierre cuando el participante pide continuar. |
| `Conversacion:Mensajes:AcuseContinuarVariantes` | App config / env `Conversacion__Mensajes__AcuseContinuarVariantes__0`, `...__1` | (vacía) | Variantes del acuse de continuar; se rotan por hilo para no repetir. Vacía = usa `AcuseContinuar`. |
| `MensajeCierre` (config conversacional) | Portal admin (campaña) | "Gracias. Tu aporte quedó registrado…" | Texto que acompaña la retro al cerrar. |
| **`MensajeInicial` (campaña)** | Portal admin (campaña) | — | **Saludo del primer entrante**: el mensaje inicial activo (menor `orden`) de la campaña, con variables `{{nombre}}`/`{{campania}}`… resueltas. Es la fuente del saludo; `SaludoPrimerContacto` es solo respaldo. |
| `Conversacion:Mensajes:SaludoPrimerContacto` | App config / env `Conversacion__Mensajes__SaludoPrimerContacto` | "Hola! Gracias por escribirnos..." | **Respaldo** del saludo del primer entrante cuando la campaña no tiene `MensajeInicial` activo. |
| `Conversacion:Mensajes:SaludoSiguientePregunta` | App config / env `Conversacion__Mensajes__SaludoSiguientePregunta` | "Continuemos con la siguiente pregunta:" | Texto que antecede una pregunta pendiente posterior. |
| `Conversacion:Mensajes:InvitacionMejora` | App config / env `Conversacion__Mensajes__InvitacionMejora` | Invitacion operativa a mejorar | Núcleo de respaldo de la invitación a mejorar cuando el LLM no devuelve `repregunta_sugerida` y `InvitacionMejoraVariantes` está vacía. |
| `Conversacion:Mensajes:InvitacionMejoraVariantes` | App config / env `Conversacion__Mensajes__InvitacionMejoraVariantes__0`, `...__1` | (vacía) | Variantes de respaldo del núcleo de la invitación; se rotan por hilo+turno. Vacía = usa `InvitacionMejora`. |
| `Conversacion:Mensajes:InvitacionContinuarVariantes` | App config / env `Conversacion__Mensajes__InvitacionContinuarVariantes__0`, `...__1` | (lista compilada) | Coletillas que enseñan la salida del "no quiero seguir"; se anexan a la invitación y se rotan. Vacía = usa la lista por defecto. |
| `Conversacion:Mensajes:MensajeConfiguracionNoDisponible` | App config / env `Conversacion__Mensajes__MensajeConfiguracionNoDisponible` | "Hay un problema con la configuracion..." | Texto visible cuando falta rubrica, prompt o ConfigLLM valida y no se llama al LLM. |
| `Conversacion:CuposHabilitados` | App config / env `Conversacion__CuposHabilitados` | `false` (**desactivado**) | Enciende la aplicación de `maxMensajesPorUsuario`/`maxLlamadasLlmPorUsuario` de la campaña (§2.8). Dimensionar los límites de la campaña antes de activar. |
| `Conversacion:MaxTurnosPorHilo` | App config / env `Conversacion__MaxTurnosPorHilo` | 0 (**desactivado**) | Techo duro de entrantes por hilo (§2.8); garantiza terminación. Recomendado ≈ `2 + MaxRepreguntas`. |
| `configConversacional.segmentacionIdeas` | Portal admin (campaña) | `false` | Habilita I-06 para esa campaña: separar un mensaje con varias ideas en N respuestas/evaluaciones/Markdown. Campo ausente = `false`. |
| `Conversacion:SegmentacionIdeas` | App config / env `Conversacion__SegmentacionIdeas` | `true` | Kill-switch global de I-06. `true` respeta la campaña; `false` apaga multi-idea para todas las campañas sin redeploy. |
| `Conversacion:MaxIdeasPorMensaje` | App config / env `Conversacion__MaxIdeasPorMensaje` | 5 | Máximo de ideas segmentadas por mensaje; excedentes se ignoran y se registra anomalía sin PII. |
| `Conversacion:LongitudMinimaIdea` | App config / env `Conversacion__LongitudMinimaIdea` | 30 | Fragmentos más cortos se descartan para evitar sobre-fragmentación trivial. |
| `configConversacional.coachingSecuencialIdeas` | Portal admin (campaña) | `false` | **I-18** — con I-06 efectivo, afina una idea a la vez. Campo ausente = flujo agregado anterior. |
| `Conversacion:CoachingSecuencialIdeas` | App config / env `Conversacion__CoachingSecuencialIdeas` | `true` | **I-18** — kill-switch global; `false` apaga el coaching secuencial sin borrar trazabilidad. |
| `Conversacion:MinutosCoachingPorIdea` | App config / env `Conversacion__MinutosCoachingPorIdea` | 0 (**desactivado**) | **I-18** — default global del reloj por idea. |
| `configConversacional.minutosCoachingPorIdea` | Portal admin (campaña) | ausente (**hereda global**) | **I-18** — override en minutos; `<=0` lo apaga para esa campaña. |
| `Conversacion:ConsolidacionProgresivaHabilitada` | App config / env `Conversacion__ConsolidacionProgresivaHabilitada` | `true` | **I-19** — kill-switch global de emergencia. No hay flag por campaña: `true` consolida/confirma en todas; `false` conserva aportes nuevos como pendientes y no los evalúa aisladamente. |
| `Conversacion:MaxCaracteresIdeaConsolidada` | App config / env `Conversacion__MaxCaracteresIdeaConsolidada` | 4000 | **I-19** — límite de la versión acumulada; al exceder, pide acotar/aclarar y nunca trunca silenciosamente hechos confirmados. |
| `Conversacion:MaxTokensSeedThoughts` | App config / env `Conversacion__MaxTokensSeedThoughts` | 800 (**calibrar con el insumo real**) | **I-12/I-19** — presupuesto del contexto orientador; campo de campaña vacío o valor `<=0` omite el bloque sin afectar el flujo. |
| `configConversacional.tejidoColectivo` | Portal admin (campaña) | `false` | Habilita I-09 para esa campaña: el coach teje aportes anonimizados de otros participantes (§2.9). Campo ausente = `false` (autocontenido). |
| `Conversacion:TejidoColectivo` | App config / env `Conversacion__TejidoColectivo` | `true` | Kill-switch global de I-09. `true` respeta la campaña; `false` apaga el tejido para todas sin redeploy. |
| `Conversacion:TopKAportes` | App config / env `Conversacion__TopKAportes` | 3 | Máximo de aportes recuperados que se tejen por turno. |
| `Conversacion:PresupuestoTokensTejido` | App config / env `Conversacion__PresupuestoTokensTejido` | 300 | Presupuesto de tokens del bloque de aportes; se trunca antes de armar el prompt (respeta `maxPrompt`). `0` o negativo omite el bloque (tejido apagado). |
| `Conversacion:UmbralSolapamientoTejido` | App config / env `Conversacion__UmbralSolapamientoTejido` | 0.1 | Fracción mínima `[0,1]` de keywords de la consulta que un aporte debe cubrir para tejerse; por debajo → no se teje. |
| `Conversacion:RecuperacionSemantica` | App config / env `Conversacion__RecuperacionSemantica` | `false` (**global, diferido**) | Opción B de I-09 (embeddings). Off en el Hito; su activación añadiría el campo `embedding` en `responses` (`03 §3.8`, commit aparte). |
| `maxMensajesPorUsuario` / `maxLlamadasLlmPorUsuario` (campaña) | Portal admin (campaña, `configSeguridad`) | 10 / 2 | Cupos por usuario dentro de la campaña (§2.8); solo se aplican con `CuposHabilitados=true`. **P-26:** con `participacionContinua=true` cuentan solo lo ocurrido en las **últimas 24 h** (ventana móvil, compartida por ciclos y preguntas de la campaña); con el interruptor apagado conservan su semántica acumulada. No afecta a `presupuestoTokensCampania` (sigue acumulado toda la campaña) ni a `MaxTurnosPorHilo` (sigue por conversación/ciclo). |
| `Conversacion:HorasExpiracionSinRespuesta` | App config / env `Conversacion__HorasExpiracionSinRespuesta` | 0 (**desactivado**) | Horas sin actividad tras las que un hilo abierto se cierra solo (legacy; se usa si no hay minutos configurados). Recomendado p. ej. `72`. |
| `Conversacion:MinutosInactividadSesion` | App config / env `Conversacion__MinutosInactividadSesion` | 0 (**desactivado**) | **I-17 §7** — default global de la ventana de inactividad **en minutos** (granularidad sub-hora; interruptor maestro). Recomendado `5` en el acta del día-D. |
| `configConversacional.minutosInactividadSesion` | Portal admin (campaña) | ausente (**hereda global**) | **I-17 §7** — override por campaña de la ventana de inactividad en minutos; `<= 0` la apaga solo para esa campaña. |
| `configConversacional.numeroWhatsAppSaliente` | Portal admin (campaña) | ausente (**usa predeterminado**) | **P-21** — alias lógico para el envío inicial/reenvío; nunca guarda el id de Meta. |
| `configConversacional.participacionContinua` | Portal admin (campaña, creación/edición) | `false` | **P-26** — permite ciclos/ideas nuevos después de finalizar, solo mientras la campaña esté `activa`. Campo ausente = recorrido único. |
| Ventana de selección P-26 | Regla fija del flujo | 24 horas | Afinidad y selección pendientes vencen; el aporte raíz permanece auditable. |
| `configConversacional.clasificacionIntencionControl` | Portal admin (campaña, creación/edición) | `false` | **P-27** — permite clasificar expresiones libres de parar/avanzar; requiere kill-switch global y ConfigLLM. |
| `Conversacion:ClasificacionIntencionControl` | App config / env `Conversacion__ClasificacionIntencionControl` | `false` (**desactivado**) | **P-27** — kill-switch global; no apaga los alias deterministas que corrigen el bug. |
| `Conversacion:MaxCaracteresClasificacionIntencionControl` | App config / env `Conversacion__MaxCaracteresClasificacionIntencionControl` | 160 | **P-27** — longitud máxima elegible; `<=0` deshabilita la ruta LLM. |
| `pregunta.umbralCierreAnticipado` | Portal admin (pregunta) | ausente (**hereda campaña**) | **I-17** — override del umbral compartido (madurez + cierre) por pregunta; precedencia pregunta → campaña → global. |
| `Conversacion:IntervaloRevisionMinutos` | App config / env `Conversacion__IntervaloRevisionMinutos` | 15 | Cada cuánto corre el barrido de expiración (mín. 1). |
| Rúbrica / Prompt / ConfigLLM | Portal admin | — | Deben estar activos (y el prompt aprobado) para evaluar; si no, fallback. |

> **Legacy hasta P-32:** si un texto de `Conversacion:Mensajes:*` queda vacío, el orquestador usa el
> default compilado. Con el catálogo activo se valida la versión completa y se usa catálogo → última
> versión válida → respaldo del **mismo idioma**. `Conversacion:Mensajes:*` y `Conversacion:Frases*`
> dejan de ser la vía editorial después de la migración.

> Para **activar la expiración** en Azure: agregar el App Setting
> `Conversacion__HorasExpiracionSinRespuesta` con el número de horas deseado (p. ej. `72`). Con `0` o sin
> definir, la expiración queda desactivada.

## 4. Estados de la conversación

- **Vida:** `abierta` → `cerrada`.
- **Máquina de turnos:** `esperandoRespuestaInicial` → `evaluando` → (`esperandoRepregunta` →
  `evaluando`)\* → `cerrada`, acotado por `MaxRepreguntas`.
- **Cola I-18 (opcional):** `pendiente → activa → finalizada` por idea; solo una activa. La
  conversación se cierra o avanza de pregunta únicamente cuando no quedan ideas pendientes.
- **Idea I-19:** flujo `pendienteConfirmacion ↔ enMejora|enRevision → cerrada`; resultado
  `madura|pendiente|rechazada`. Una idea madura recibe `estadoCuraduria=pendiente`.
- **Enrutamiento P-26:** `seleccionCampania|seleccionPregunta|listo → enIdea → completado`;
  puede terminar `expirado|cancelado`. Cada ciclo nuevo crea otra conversación.
- **Entrada P-28:** un `EnrutamientoAporte` marcado `esEntradaProactiva` puede recorrer el mismo menú
  P-26, pero al resolverlo hace `listo → completado` y envía solo bienvenida: no llega a `enIdea` ni
  crea conversación.
- **Retomar P-30:** `modo=retomarIdea` recorre
  `seleccionCampania|seleccionPregunta → seleccionIdea → listo → enIdea → completado`; `enIdea`
  mantiene la afinidad al ciclo histórico reabierto y nunca significa que la intención fue un aporte.
- **Aclaración P-27 (opcional):** `esperandoRepregunta → esperandoConfirmacionSalida →
  esperandoRepregunta|cerrada`; las opciones 1/2/3 son deterministas y no consumen una repregunta.
- **Respuesta:** `evaluada` (evaluación válida) o `evaluacionPendiente` (fallback / sin evaluación).
- **Idioma P-32:** `Usuario.Idioma` se fija al crear `Conversacion`/`EnrutamientoAporte`; ninguna
  transición de la máquina cambia ese snapshot.

## 5. Referencias
- `05_Backend_WhatsApp_y_Conversacion.md` §2 (ventana, envío), §4 (orquestador, tope de repregunta).
- `08_*` (evaluación LLM, esquema de salida, fallback).
- `09_*` (compilación Markdown).
- `SUPUESTOS.md#primer-contacto-pregunta`, `#orquestador-conversacional`.
- `AVANCES.md` (tablero por fases, estado real).
- `Iniciativas/P-32_Conversacion_Multidioma_y_Catalogo_Textos.md` y
  `planes/P-32_Inventario_y_Migracion_Textos.md`.
- `Iniciativas/DT-P32-02_Semillas_Edicion_Masiva_y_Readiness_Catalogo_Textos.md`,
  `planes/DT-P32-02_Plan_Implementacion_Semillas_y_JSON.md` y `QAS/22_*`.
