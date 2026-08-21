# QAS 25 — DT-P33-01: clasificación semántica de consulta de idea

> **Estado:** hotfix `85b78f8` / `v1.0.3-convencion` desplegado con workflow/readiness verdes y
> catálogo inglés v3 activo. Esta guía se ejecutará dentro de la validación integral al terminar el
> fix completo, junto con D5.
> **Estado del ambiente informado:** clasificación semántica y visibilidad de idea ON. Verificar además
> `configConversacional.consultaIdea=true` en la campaña antes de ejecutar.

## Qué debe cambiar

Una persona puede pedir ver su idea con palabras naturales no enumeradas en el catálogo. El sistema
comprende la intención con una única clasificación LLM y muestra la versión consolidada exacta. Si el
mensaje también agrega o corrige contenido, se guarda como aporte y no se convierte en consulta.
Después de mostrar una idea, una conformidad natural confirma/cierra esa misma idea sin otra repregunta;
si la frase también agrega contenido, prevalece el aporte.

## Antes de empezar

- Ambiente y campaña de QA aislados; nunca campañas o teléfonos reales no autorizados.
- Salida WhatsApp simulada o canal de prueba expresamente aprobado.
- P-33 y el nuevo gate semántico ON solo durante la ventana; P-27 se prueba tanto OFF como ON.
- Confirmar que `v1.0.3-convencion` sigue desplegado y el catálogo inglés v3 continúa activo.
- Catálogo `es/en`, campaña localizada, ConfigLLM y prompt aprobados para QA.
- Registrar ids/códigos/conteos, nunca secretos, teléfonos completos, mensajes o ideas en logs.

## Prueba 1 — consulta inglesa no catalogada

1. Crea una idea y confirma que tiene versión consolidada.
2. Retira de la copia QA del catálogo cualquier frase exacta equivalente a `coming along`.
3. Envía `How is my idea coming along so far?`.
4. Debe mostrarse la idea exacta con encabezado/invitación ingleses.
5. No debe aparecer el menú `Reply 1...`, ni crearse respuesta/evaluación/versión/Markdown.

## Prueba 2 — paráfrasis bilingües

Probar al menos cinco formas no catalogadas por idioma, incluyendo:

- `Could you show me what we have so far?`
- `Can you remind me where my proposal stands?`
- `¿Me recuerdas cómo va la propuesta?`
- `¿Qué llevamos construido de mi idea?`

Todas las consultas puras deben mostrar la misma versión vigente sin menú implícito.

## Prueba 3 — mensajes mixtos

Enviar:

- `How is my idea coming along? Add a September pilot.`
- `Muéstrame la idea y cambia Colombia por Perú.`

Ambos textos deben conservarse íntegros como aporte/corrección. No deben disparar la consulta de solo
lectura ni perder la parte posterior a la pregunta.

## Prueba 4 — una sola llamada

Con telemetría de QA, verificar para cada mensaje no determinista:

- máximo un evento de clasificación con `esLlamadaLlm=true`;
- tokens sumados una sola vez;
- el orquestador no vuelve a llamar después del routing.

Una frase exacta del catálogo debe usar `origen=determinista` y cero tokens.

## Prueba 5 — P-27 no cambia

Con P-27 ON probar `stop now`, `quiero pasar a otra idea`, `no sé` y `hay que parar la máquina durante
el mantenimiento`. Deben conservar respectivamente finalizar participación, finalizar idea,
aclaración contextual y aporte. Con P-27 OFF, ningún candidato LLM de control puede cerrar; la única
excepción es `confirmarIdea` contra la afinidad P-33 vigente de la idea recién mostrada.

## Prueba 6 — menú pendiente y afinidad

- Con selección P-26/P-30 pendiente, una consulta pura cancela el menú y muestra la idea.
- Una respuesta `2` sigue resolviendo la selección y no se consume como consulta.
- Tras consultar una idea cerrada, una corrección sustantiva reabre el mismo `ideaId`; `thanks` no.

## Prueba 7 — fallos seguros

Simular por separado timeout, 5xx, JSON inválido, intención desconocida, ConfigLLM ausente, cupo de
usuario y presupuesto de campaña agotados. En todos los casos el mensaje conserva la ruta de aporte,
no cambia estado por el clasificador y no abre el menú de salida por sí solo.

## Prueba 8 — conformidad después de mostrar la idea

1. Con P-33 y el gate semántico ON, P-27 OFF, crear una idea abierta con versión ya confirmada.
2. Consultarla con `How is my idea coming along so far?` y comprobar que se muestra la versión.
3. Enviar `I'm satisfied with this`.
4. Debe cerrarse esa misma idea por conformidad y avanzar a la siguiente unidad disponible.
5. No debe aparecer otra pregunta como `What is the first operational step...?`, no debe invocarse
   consolidación y no debe repetirse el bloque de versión que acaba de mostrarse.
6. Repetir con una versión pendiente: debe confirmarse y evaluarse exactamente una vez.
7. Repetir sobre una idea cerrada recién consultada: debe haber como máximo un acuse y nunca reapertura.

## Prueba 9 — conformidad mixta y autoridad

- Tras mostrar la idea, enviar `I'm satisfied with this, but add a September pilot`: debe ser aporte,
  conservarse íntegro y mantener la idea abierta/reabrirla según P-33.
- Vencer o consumir la afinidad y enviar `I'm satisfied with this`: el candidato LLM por sí solo no
  puede confirmar ni cerrar una idea.
- Alterar en fixture campaña/pregunta/idea de la afinidad: el servidor debe rechazar la transición.

## Prueba 9a — regresión determinista del caso real

1. Confirmar por lectura que el hotfix está desplegado y que el catálogo inglés v3 figura activo.
2. Sobre una idea abierta, enviar `How is my idea going?`; debe mostrarse la versión vigente.
3. Enviar `No is all right for me`; debe confirmar/cerrar la misma idea sin otra pregunta y sin evento
   LLM/tokens para ese segundo mensaje.
4. Repetir sobre una idea cerrada: debe completar la afinidad, sin reapertura ni reevaluación.
5. Repetir con `It is all right for me, but change the loading order`: debe invocar como máximo una
   clasificación y conservarse como aporte; nunca aplicar el fast path determinista.
6. Si falla, volver el catálogo activo a v2, apagar el gate semántico y conservar la evidencia.

## Prueba 10 — seguridad y privacidad

- Prompt injection dentro del mensaje no altera el contrato.
- La salida con campos extra se rechaza.
- El request no contiene texto consolidado, ids de idea/versión, rúbrica ni datos de terceros.
- Logs no contienen mensaje, idea ni PII nueva.

## Prueba 11 — dedupe e idioma

Reenviar el mismo `whatsappMessageId`: debe haber una sola clasificación y un solo envío. Repetir los
casos puros/mixtos en `es/en`; encabezado, invitación y fallback deben permanecer en el idioma del hilo.

## Cierre local y D5

Antes de declarar DONE: build Release `-warnaserror`, unitarias completas, integración no-Calibración,
formato y `git diff --check` verdes. Luego D5 real `n=3` compara el prompt anterior y candidato con el
mismo modelo/parámetros, midiendo acierto de intención, falsos positivos, costo y latencia. Si falta
credencial o autorización, D5 queda `BLOCKED`; no se reduce `n` ni se activa el gate.

## Rollback

Restaurar el catálogo inglés activo a v2, poner
`Conversacion__ClasificacionSemanticaConsultaIdeaHabilitada=false`, esperar reinicio y confirmar por
lectura. P-33 determinista y P-27 quedan como antes. No borrar evidencia ni modificar otros catálogos.
