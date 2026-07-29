# P-25 — Coaching directo sin confirmación repetitiva

> Estado: **DONE local el 2026-07-29; pendiente validación operativa y despliegue.**
>
> Problema observado: después de cada aporte el sistema muestra la versión consolidada y pregunta
> “¿Es correcto?”. La información queda bien trazada, pero la conversación se siente como un formulario
> de validación y no como un coach que ayuda a madurar la idea.

## 1. Resultado esperado

Cada respuesta sustantiva del participante debe incorporarse a la idea consolidada y evaluarse de
inmediato contra la rúbrica, el prompt y las semillas disponibles. El participante recibe una respuesta
natural: un reconocimiento breve, la oportunidad de mejora más importante y una sola pregunta abierta.

Ejemplo esperado:

> Hacer una presentación en PowerPoint es un punto de partida válido. Vamos a concretarla un poco más:
> ¿qué debería contener para que los gerentes entiendan y recuerden el mensaje principal?

La conversación no debe mostrar “Entendí que propones… ¿Es correcto?” después de cada aporte.

## 2. Reglas

1. El aporte original se conserva inmutable y se integra en una nueva versión consolidada.
2. Si la consolidación es válida, el servidor confirma automáticamente esa versión y la evalúa completa
   en el mismo turno.
3. Bajo umbral, la idea permanece activa y se formula una sola pregunta socrática basada en la
   evaluación de la versión completa.
4. Al alcanzar el umbral, la idea queda madura y pendiente de curaduría; luego continúa la siguiente
   idea de la cola, si existe.
5. Si el consolidador marca una ambigüedad real (`requiereAclaracion`), no se evalúa: se hace una
   pregunta aclaratoria breve. Este es el único caso normal donde se pide validar qué quiso decir.
6. Un mensaje con varias ideas mantiene una sola idea activa. Las demás esperan su turno y, al
   activarse, se evalúan directamente sin pedir una confirmación mecánica.
7. “No lo guardes”, “así está bien”, reapertura, inactividad, cupos y demás reglas conservan su
   prioridad.
8. `MaxRepreguntas` permanece alto cuando la campaña lo requiera. Es un techo técnico; las salidas
   normales son madurez o decisión expresa del participante.

## 3. Compatibilidad y operación

Se añade `Conversacion:ConfirmacionExplicitaIdeasHabilitada`.

- `false` — comportamiento normal P-25: consolidar, confirmar automáticamente y evaluar.
- `true` — rollback al flujo I-19 anterior: mostrar la versión y pedir confirmación explícita.

La configuración distribuida queda en `false`. No hay opt-in por campaña ni cambio de contrato público,
Cosmos, portal o datos históricos. Las versiones continúan usando el estado `confirmada`; la auditoría
distingue la transición como `confirmadaAutomatica`.

Las conversaciones que ya estaban esperando confirmación siguen aceptando “sí”, “vamos a mejorarla” o
una corrección. Los aportes nuevos usan coaching directo.

## 4. Flujo

```text
Aporte del participante
        |
        v
Consolidar la idea completa
        |
        +-- ambigua --> una aclaración breve
        |
        v
Confirmar automáticamente + evaluar versión completa
        |
        +-- bajo umbral --> reconocimiento + una pregunta de coaching
        |
        +-- alcanza umbral --> madura + curaduría pendiente + siguiente idea
```

## 5. Criterios de aceptación

1. El primer aporte sustantivo se evalúa en el mismo turno y no genera “¿Es correcto?”.
2. La evaluación usa la versión consolidada completa, no solo el último mensaje.
3. Una respuesta al coaching crea otra versión completa, la evalúa inmediatamente y vuelve a preguntar
   solo si sigue bajo umbral.
4. En multi-idea, se trabaja una sola idea y las demás conservan su orden.
5. Una ambigüedad real pregunta antes de evaluar.
6. El rollback con confirmación explícita conserva las regresiones I-19/P-24.
7. Build, pruebas no calibración, formato y `git diff --check` quedan verdes.

## 6. Cómo probarlo

1. Inicia una campaña y responde: “Hagamos una presentación en PowerPoint y mostremosla”.
2. El asistente debe reconocer el punto de partida y preguntar qué debería contener o qué resultado debe
   lograr. No debe responder “Entendí que propones… ¿Es correcto?”.
3. Responde a esa pregunta con un detalle nuevo. El asistente debe usar tanto la idea inicial como el
   nuevo detalle y continuar ayudando a mejorarla.
4. Repite el intercambio. Mientras falte madurez, debe hacer una sola pregunta útil por turno.
5. Cuando la idea alcance el umbral, debe cerrarla naturalmente y pasar a la siguiente idea, si existe.
6. Es un fallo si repite la idea para pedir confirmación, evalúa solo el último mensaje o avanza de idea
   antes de madurar sin que el participante lo pida.

## 7. Evidencia de cierre local

- Build Release con advertencias como errores: 0 errores y 0 advertencias.
- 583 pruebas no calibración: 522 unitarias y 61 de integración.
- Regresiones P-25: primer aporte simple, complemento acumulado, cola multi-idea y recorrido webhook
  simulado, todos sin “¿Es correcto?”.
- `dotnet format --verify-no-changes` y `git diff --check` limpios.
- Sin push, despliegue ni cambio de configuración remota.
