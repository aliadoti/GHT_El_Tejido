# Reglas de conversación y participación — El Tejido

> Documento de consulta de las **reglas de negocio** del flujo de interacción con el participante por
> WhatsApp. Resume el comportamiento implementado en `OrquestadorConversacion` y servicios asociados.
> Fuente de verdad del código: `05_Backend_WhatsApp_y_Conversacion.md` (§2, §4), `08` (evaluación LLM)
> y `09` (Markdown). Última revisión: 2026-06-17.

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

En campanias con varias preguntas activas, el orquestador resuelve la pregunta de trabajo por `orden`:
mantiene el hilo abierto actual hasta completar sus revisiones disponibles; cuando ese hilo se cierra,
crea el hilo de la siguiente pregunta activa y la envia como texto libre en la misma ventana. Si un
participante escribe despues de una pregunta cerrada y aun hay preguntas pendientes, el entrante se usa
para abrir/enviar la siguiente pregunta y no se evalua como respuesta. Si todas las preguntas activas ya
estan cerradas, los mensajes posteriores se ignoran.

### 2.2 Evaluación con LLM
Cada respuesta se evalúa con el LLM usando la **rúbrica**, el **prompt** aprobado y la **ConfigLLM**
activos de la pregunta/campaña. El modelo debe devolver un JSON con el esquema acordado (el sistema le
incrusta el esquema y la escala). Requisitos para evaluar:
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
`RepreguntasUsadas < MaxRepreguntas`. Cuando el hilo esta en `esperandoRepregunta` y
`RepreguntasUsadas >= MaxRepreguntas`, el siguiente mensaje del participante **se registra como
`recibida`, no se manda al LLM, no genera retroalimentacion ni Markdown**, y el sistema envia solo el
`MensajeCierre`. Luego, si hay otra pregunta activa pendiente, continua con esa pregunta.

El numero de revisiones lo controla `MaxRepreguntas` (default **1**); con `MaxRepreguntas = 0` se cierra
sin ofrecer mejora. En fallback **no** se ofrece mejora (se cierra con retro neutra). Cada evaluacion
valida compila su propio Markdown; el ultimo intento evaluado es el definitivo.

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
| `MensajeCierre` (config conversacional) | Portal admin (campaña) | "Gracias. Tu aporte quedó registrado…" | Texto que acompaña la retro al cerrar. |
| `Conversacion:Mensajes:SaludoPrimerContacto` | App config / env `Conversacion__Mensajes__SaludoPrimerContacto` | "Hola! Gracias por escribirnos..." | Texto que antecede la primera pregunta cuando el participante abre un hilo nuevo. |
| `Conversacion:Mensajes:SaludoSiguientePregunta` | App config / env `Conversacion__Mensajes__SaludoSiguientePregunta` | "Continuemos con la siguiente pregunta:" | Texto que antecede una pregunta pendiente posterior. |
| `Conversacion:Mensajes:InvitacionMejora` | App config / env `Conversacion__Mensajes__InvitacionMejora` | Invitacion operativa a mejorar | Texto adicional que acompana la retro cuando aun quedan revisiones. |
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
