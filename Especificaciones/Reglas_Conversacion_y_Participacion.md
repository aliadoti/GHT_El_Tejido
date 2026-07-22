# Reglas de conversación y participación — El Tejido

> Documento de consulta de las **reglas de negocio** del flujo de interacción con el participante por
> WhatsApp. Resume el comportamiento implementado en `OrquestadorConversacion` y servicios asociados.
> Fuente de verdad del código: `05_Backend_WhatsApp_y_Conversacion.md` (§2, §4), `08` (evaluación LLM)
> y `09` (Markdown). Última revisión: 2026-07-13.

## 1. Visión general del flujo

```
Participante                         El Tejido
     │  "Hola" (primer contacto)         │
     │ ────────────────────────────────► │  (no evalúa el saludo)
     │  ◄──────── Saludo + PREGUNTA ───── │
     │  Su respuesta                      │
     │ ────────────────────────────────► │  Evalúa con el LLM (rúbrica + prompt)
     │  ◄──── Retro + INVITACIÓN a mejorar│  (compila Markdown del intento)
     │  Versión mejorada (opcional)       │
     │ ────────────────────────────────► │  Re-evalúa (cuenta esta última)
     │  ◄──── Retro + CIERRE ──────────── │  (compila Markdown, cierra el hilo)
```

Una **conversación** es el hilo de un trío `(participante, campaña, pregunta)`. Su id es determinista
(`conv_<campaniaId>_<usuarioId>_<preguntaId>`): hay **una por trío** en el MVP.

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
estan cerradas, los mensajes posteriores se ignoran.

El avance entre preguntas no exige siempre agotar las revisiones: una pregunta puede cerrarse antes por
**calificacion alta** o porque el **participante pide continuar** (ver §2.3, "Dos salidas anticipadas").

### 2.2 Evaluación con LLM
Cada respuesta se evalúa con el LLM usando la **rúbrica**, el **prompt** aprobado y la **ConfigLLM**
activos de la pregunta/campaña. El modelo debe devolver un JSON con el esquema acordado (el sistema le
incrusta el esquema y la escala). El sistema le pasa además el **historial reciente** del hilo (turnos
previos persistidos, acotado en cantidad y longitud, **sin** duplicar la respuesta que se está
evaluando) para que el LLM **no repita** preguntas/retroalimentación ni entre en bucles. Requisitos
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

### 2.3 Revision determinista (revisiones como oportunidades)
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
   (`Conversacion:FrasesContinuar`, p. ej. *"asi esta bien"*, *"sigamos"*, *"listo"*), el mensaje **se
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
   evalua), para no cortar al participante por una negacion sin contexto de guardado. La degradacion nunca
   promueve (idempotente) y no toca contratos compartidos.

### 2.4 Cierre y Markdown
Hay dos cierres normales: si la evaluacion valida decide cerrar sin ofrecer revision, se envia
**retro + mensaje de cierre** y se compila el **Markdown** del aporte (`09`); si el participante responde
despues de agotar revisiones, se envia **solo el mensaje de cierre** y no se compila un Markdown nuevo.
Una conversacion **cerrada ignora** cualquier mensaje posterior (se descarta en silencio).

### 2.4.1 Multi-idea por mensaje (I-06, implementado; flags apagados)
Si la campaña tiene `configConversacional.segmentacionIdeas=true` y el kill-switch global
`Conversacion:SegmentacionIdeas` no está apagado, una respuesta puede separarse en varias ideas antes
de evaluarse. Cada idea válida se guarda como una `Respuesta` independiente, con su propia evaluación y
Markdown, pero el participante no recibe N mensajes técnicos: el sistema debe contestar de forma breve
y agregada para confirmar el registro del turno. Si el segmentador falla, devuelve una salida inválida
o no quedan ideas válidas después de las guardas, el sistema vuelve al modo probado: **1 mensaje = 1
respuesta**.

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

## 3. Parámetros configurables

| Parámetro | Dónde se configura | Default | Efecto |
|---|---|---|---|
| `MaxRepreguntas` (pregunta / campaña) | Portal admin (campaña/pregunta) | 1 | Cuántas revisiones/mejoras se ofrecen antes de cerrar (0 = ninguna). |
| `Conversacion:UmbralCierreAnticipado` | App config / env `Conversacion__UmbralCierreAnticipado` | 0 (**desactivado**) | Default numérico heredable para campañas sin override; fracción de la escala `[0,1]`. |
| `configConversacional.umbralCierreAnticipado` | Portal admin (campaña) | `null` (**hereda global**) | Override opcional por campaña; `<= 0` apaga solo esa campaña. |
| `Conversacion:CierreAnticipadoHabilitado` | App config / env `Conversacion__CierreAnticipadoHabilitado` | `true` | Kill-switch global: `false` apaga el cierre anticipado para todas las campañas, incluidos sus overrides. |
| `Conversacion:FrasesContinuar` | App config / env `Conversacion__FrasesContinuar__0`, `...__1` | (lista compilada) | Frases con las que el participante pide continuar a la siguiente pregunta. Vacío = usa la lista por defecto. |
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
| `configConversacional.tejidoColectivo` | Portal admin (campaña) | `false` | Habilita I-09 para esa campaña: el coach teje aportes anonimizados de otros participantes (§2.9). Campo ausente = `false` (autocontenido). |
| `Conversacion:TejidoColectivo` | App config / env `Conversacion__TejidoColectivo` | `true` | Kill-switch global de I-09. `true` respeta la campaña; `false` apaga el tejido para todas sin redeploy. |
| `Conversacion:TopKAportes` | App config / env `Conversacion__TopKAportes` | 3 | Máximo de aportes recuperados que se tejen por turno. |
| `Conversacion:PresupuestoTokensTejido` | App config / env `Conversacion__PresupuestoTokensTejido` | 300 | Presupuesto de tokens del bloque de aportes; se trunca antes de armar el prompt (respeta `maxPrompt`). `0` o negativo omite el bloque (tejido apagado). |
| `Conversacion:UmbralSolapamientoTejido` | App config / env `Conversacion__UmbralSolapamientoTejido` | 0.1 | Fracción mínima `[0,1]` de keywords de la consulta que un aporte debe cubrir para tejerse; por debajo → no se teje. |
| `Conversacion:RecuperacionSemantica` | App config / env `Conversacion__RecuperacionSemantica` | `false` (**global, diferido**) | Opción B de I-09 (embeddings). Off en el Hito; su activación añadiría el campo `embedding` en `responses` (`03 §3.8`, commit aparte). |
| `maxMensajesPorUsuario` / `maxLlamadasLlmPorUsuario` (campaña) | Portal admin (campaña, `configSeguridad`) | 10 / 2 | Cupos por usuario dentro de la campaña (§2.8); solo se aplican con `CuposHabilitados=true`. |
| `Conversacion:HorasExpiracionSinRespuesta` | App config / env `Conversacion__HorasExpiracionSinRespuesta` | 0 (**desactivado**) | Horas sin actividad tras las que un hilo abierto se cierra solo (legacy; se usa si no hay minutos configurados). Recomendado p. ej. `72`. |
| `Conversacion:MinutosInactividadSesion` | App config / env `Conversacion__MinutosInactividadSesion` | 0 (**desactivado**) | **I-17 §7** — default global de la ventana de inactividad **en minutos** (granularidad sub-hora; interruptor maestro). Recomendado `5` en el acta del día-D. |
| `configConversacional.minutosInactividadSesion` | Portal admin (campaña) | ausente (**hereda global**) | **I-17 §7** — override por campaña de la ventana de inactividad en minutos; `<= 0` la apaga solo para esa campaña. |
| `pregunta.umbralCierreAnticipado` | Portal admin (pregunta) | ausente (**hereda campaña**) | **I-17** — override del umbral compartido (madurez + cierre) por pregunta; precedencia pregunta → campaña → global. |
| `Conversacion:IntervaloRevisionMinutos` | App config / env `Conversacion__IntervaloRevisionMinutos` | 15 | Cada cuánto corre el barrido de expiración (mín. 1). |
| Rúbrica / Prompt / ConfigLLM | Portal admin | — | Deben estar activos (y el prompt aprobado) para evaluar; si no, fallback. |

> Si un texto de `Conversacion:Mensajes:*` se deja vacio o con espacios, el orquestador usa el default
> compilado para evitar mensajes salientes vacios.

> Para **activar la expiración** en Azure: agregar el App Setting
> `Conversacion__HorasExpiracionSinRespuesta` con el número de horas deseado (p. ej. `72`). Con `0` o sin
> definir, la expiración queda desactivada.

## 4. Estados de la conversación

- **Vida:** `abierta` → `cerrada`.
- **Máquina de turnos:** `esperandoRespuestaInicial` → `evaluando` → (`esperandoRepregunta` →
  `evaluando`)\* → `cerrada`, acotado por `MaxRepreguntas`.
- **Respuesta:** `evaluada` (evaluación válida) o `evaluacionPendiente` (fallback / sin evaluación).

## 5. Referencias
- `05_Backend_WhatsApp_y_Conversacion.md` §2 (ventana, envío), §4 (orquestador, tope de repregunta).
- `08_*` (evaluación LLM, esquema de salida, fallback).
- `09_*` (compilación Markdown).
- `SUPUESTOS.md#primer-contacto-pregunta`, `#orquestador-conversacional`.
- `AVANCES.md` (tablero por fases, estado real).
