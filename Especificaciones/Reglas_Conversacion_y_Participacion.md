# Reglas de conversación y participación — El Tejido

> Documento de consulta de las **reglas de negocio** del flujo de interacción con el participante por
> WhatsApp. Resume el comportamiento implementado en `OrquestadorConversacion` y servicios asociados.
> Fuente de verdad del código: `05_Backend_WhatsApp_y_Conversacion.md` (§2, §4), `08` (evaluación LLM)
> y `09` (Markdown). Última revisión: 2026-06-23.

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
   `Conversacion:UmbralCierreAnticipado` (fraccion de la escala de la rubrica en `[0,1]`; **0 = desactivado**,
   default), el sistema **no insiste con una revision** aunque queden repreguntas: antepone una felicitacion
   (`Conversacion:Mensajes:MensajeCalificacionAlta`) al cierre, compila el Markdown y avanza a la siguiente
   pregunta. El umbral se compara como `CalificacionTotal >= Min + Umbral * (Max - Min)`.
2. **Continuar por intencion del participante (salida conversacional).** Estando en `esperandoRepregunta`
   (ya se ofrecio una mejora), si el participante responde con una frase de conformidad
   (`Conversacion:FrasesContinuar`, p. ej. *"asi esta bien"*, *"sigamos"*, *"listo"*), el mensaje **se
   registra como `recibida`, no se evalua**, el sistema antepone un acuse calido
   (`Conversacion:Mensajes:AcuseContinuar`) al `MensajeCierre` y avanza. La deteccion es **hibrida
   determinista**: igualdad exacta con una frase, o contencion de la frase solo si el mensaje es corto
   (`Conversacion:MaxCaracteresIntencionContinuar`, default 40), comparando sin mayusculas/acentos/puntuacion.
   Esta deteccion **solo** aplica a la respuesta de revision; el primer mensaje (la respuesta real) siempre
   se evalua. La invitacion a mejorar (§3) ya ensena la frase de salida para que el camino feliz coincida.

### 2.4 Cierre y Markdown
Hay dos cierres normales: si la evaluacion valida decide cerrar sin ofrecer revision, se envia
**retro + mensaje de cierre** y se compila el **Markdown** del aporte (`09`); si el participante responde
despues de agotar revisiones, se envia **solo el mensaje de cierre** y no se compila un Markdown nuevo.
Una conversacion **cerrada ignora** cualquier mensaje posterior (se descarta en silencio).

### 2.5 Ventana de 24 h y respuestas tardías
- WhatsApp solo permite **texto libre** dentro de las **24 h** posteriores al último mensaje del
  participante. El sistema responde siempre con texto libre.
- Cuando el participante escribe (aunque sea **días después**), su mensaje **reabre** la ventana de 24 h,
  así que la respuesta del sistema (retro/cierre) **se entrega sin problema**.
- **No hay** mensajes proactivos fuera de ventana (recordatorios): requerirían una plantilla (HSM)
  aprobada por Meta, no implementada.

### 2.6 Expiración por inactividad (parametrizable)
Para blindar el sistema, un hilo **abierto** sin actividad pasado un plazo configurable se **cierra
automáticamente** (cierre silencioso, sin mensaje). Lo ejecuta un barrido periódico
(`TrabajadorExpiracionConversaciones`). Si el participante no contesta, su hilo no queda abierto para
siempre. Ver parámetros abajo. La última evaluación registrada (si la hubo) queda como definitiva.

### 2.7 Rechazo de no autorizados
Un número que no resuelve a un participante válido (no matriculado, inactivo, rol no participante, sin
campaña activa o sin pregunta vigente) se **rechaza de forma neutral**; el motivo solo se registra en
`LogSeguridad` y en el log del webhook (nunca se revela al usuario).

## 3. Parámetros configurables

| Parámetro | Dónde se configura | Default | Efecto |
|---|---|---|---|
| `MaxRepreguntas` (pregunta / campaña) | Portal admin (campaña/pregunta) | 1 | Cuántas revisiones/mejoras se ofrecen antes de cerrar (0 = ninguna). |
| `Conversacion:UmbralCierreAnticipado` | App config / env `Conversacion__UmbralCierreAnticipado` | 0 (**desactivado**) | Fracción de la escala de la rúbrica `[0,1]`; si la calificación la alcanza, cierra/avanza sin ofrecer más revisiones. |
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
| `Conversacion:HorasExpiracionSinRespuesta` | App config / env `Conversacion__HorasExpiracionSinRespuesta` | 0 (**desactivado**) | Horas sin actividad tras las que un hilo abierto se cierra solo. Recomendado p. ej. `72`. |
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
