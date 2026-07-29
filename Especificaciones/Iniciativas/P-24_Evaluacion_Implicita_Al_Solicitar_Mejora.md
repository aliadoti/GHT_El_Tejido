# P-24 — Evaluación implícita al solicitar mejora

> **Actualización P-25 (2026-07-29):** el flujo normal ya evalúa cualquier aporte sustantivo en el
> mismo turno, sin pedir confirmación. P-24 se conserva como compatibilidad para conversaciones
> históricas pendientes y para el rollback
> `Conversacion:ConfirmacionExplicitaIdeasHabilitada=true`.

> Estado: **DONE local el 2026-07-29.**
>
> Origen: observación en una campaña activa. Después de recibir una propuesta consolidada y pedir
> “Vamos a mejorarla”, el sistema guardaba esa frase como una corrección nueva, volvía a pedir
> confirmación y nunca llegaba a evaluar contra la rúbrica.

## 1. Resultado esperado

Una solicitud corta de mejorar una versión ya propuesta significa: “esta es la idea que quiero trabajar”.
El sistema debe confirmar **implícitamente** esa versión, evaluarla completa contra la rúbrica, el prompt
y las semillas disponibles, y continuar con una única pregunta socrática si aún no alcanza el umbral.

No significa que la frase corta sea contenido de la idea. Por eso no crea `Respuesta`, aporte, ni una nueva
versión. El mensaje entrante se conserva en el historial conversacional/auditable, como los demás mensajes,
pero no contamina la idea consolidada.

## 2. Reglas de negocio

1. Aplica solo cuando la idea activa está en `pendienteConfirmacion` y el mensaje corto coincide con una
   frase de solicitud de mejora configurada.
2. El servidor confirma la versión propuesta vigente y la evalúa como unidad canónica completa. El último
   mensaje (“vamos a mejorarla”) nunca sustituye ni se concatena a esa versión.
3. Si la nota alcanza el umbral efectivo, la idea queda `madura`, `pendiente de curaduría`, se cierra de
   forma natural y se activa la siguiente idea en cola, si existe.
4. Si queda bajo el umbral, conserva la evaluación, el Markdown y el historial; permanece en mejora y el
   agente formula una sola pregunta abierta basada en la retroalimentación aprobada y el aspecto más débil.
   No responde por el participante ni revela rúbrica, puntajes o umbral.
5. Una respuesta que sí aporta información continúa siendo una corrección/complemento: crea un aporte
   inmutable, una propuesta nueva y vuelve a mostrar la versión completa para confirmar.
6. Rechazo explícito, reapertura, cupos, inactividad y demás transiciones actuales conservan su prioridad.
7. `MaxRepreguntas` **no se reduce** para esta corrección. En campañas de acompañamiento puede ser alto;
   es un techo técnico excepcional, no el mecanismo normal para terminar una idea. Las salidas normales
   son madurez, decisión explícita del participante, rechazo, inactividad/fallback o los cupos de seguridad.

## 3. Diseño acordado

### 3.1 Detección determinista y configurable

Se añade `Conversacion:FrasesSolicitarMejora`, con lista compilada si la configuración está vacía. Ejemplos:
`vamos a mejorarla`, `vamos a mejorar`, `quiero mejorarla`, `quiero mejorar`, `ayúdame a mejorarla`.
El mismo comparador existente normaliza mayúsculas, tildes y puntuación; solo acepta contención en mensajes
cortos. Una frase larga o ambigua sigue el camino seguro de aporte nuevo.

La detección es determinista y se limita al estado `pendienteConfirmacion`. El LLM no decide si se confirma,
se evalúa, madura o cambia de idea.

### 3.2 Trazabilidad

La versión se marca confirmada y se registra la acción de consolidación
`confirmadaImplicitaMejora`. Evaluación, versión, `ideaId`, Markdown, curaduría y cola conservan las mismas
referencias canónicas de I-19. No se modifica ningún contrato público, esquema Cosmos ni dato histórico.

### 3.3 Conversación fluida

I-20 sigue siendo responsable de redactar el puente y la pregunta; el servidor inserta la
retroalimentación o la versión exacta y decide el acto. Si el redactor no está disponible, el respaldo
determinista conserva el mismo flujo funcional.

## 4. Flujo

```text
Versión propuesta completa
        |
        +-- “sí / así está bien” --> confirma, evalúa y termina como participante si no madura
        |
        +-- “vamos a mejorarla” --> confirma implícitamente, evalúa la versión completa
        |                               |
        |                               +-- madura --> curaduría pendiente / siguiente idea
        |                               +-- bajo umbral --> una pregunta socrática
        |
        +-- contenido nuevo --> aporte + nueva versión propuesta + confirmación
```

## 5. Alcance técnico

- `OpcionesConversacion`: lista opcional de frases de solicitud de mejora.
- `DetectorIntencionContinuar`: catálogo por defecto reutilizando su normalización y guarda de longitud.
- `OrquestadorConversacion`: transición anterior a `ProponerVersionComplementariaAsync`; confirma y evalúa
  sin persistir el mensaje como aporte, con auditoría diferenciada.
- Pruebas unitarias y de integración del orquestador; no hay cambio de portal, API, DTO, permisos ni
  configuración remota automática.

## 6. Criterios de aceptación

1. Tras una propuesta, “Vamos a mejorarla” produce una evaluación con el texto de la versión completa,
   nunca con esa frase sola.
2. El mensaje no crea una `Respuesta` ni una versión adicional.
3. Bajo umbral se envía una pregunta de coaching y la misma idea queda activa; otra idea en cola no se toca.
4. Al alcanzar el umbral se conserva como madura y pendiente de curaduría, luego se avanza de forma natural.
5. Una corrección real conserva el comportamiento I-19: acumula contenido y exige confirmar la nueva versión.
6. La lista configurable y sus valores por defecto reconocen tildes, mayúsculas y puntuación sin capturar
   mensajes largos por accidente.
7. Build, pruebas no calibración, formato y `git diff --check` quedan verdes.

## 7. Verificación manual en lenguaje simple

1. Inicia una campaña y responde una idea que todavía necesite detalle.
2. Cuando el asistente muestre la idea consolidada, responde: **“Vamos a mejorarla”**.
3. Debe hacer una pregunta concreta para ayudarte a completar esa misma idea; no debe repetir la misma
   confirmación ni tomar la frase como parte de la propuesta.
4. Agrega el dato solicitado. Debe mostrar la idea completa actualizada y pedir confirmar esa nueva versión.
5. Repite hasta que la idea esté suficientemente completa. Debe cerrarla como madura y pasar, con naturalidad,
   a la siguiente idea si la había.
6. Sería un fallo que “Vamos a mejorarla” aparezca dentro de la idea guardada, que se repita la confirmación
   sin una pregunta útil, o que se abandone la idea antes de madurar sin que el participante lo pida.

## 8. Operación pendiente

La configuración de una campaña debe separar el saludo inicial de la pregunta que se quiere responder:
el texto de bienvenida no debe ser la única pregunta activa. Esta corrección no altera campañas desplegadas
ni cambia valores remotos; su validación operativa sigue requiriendo D5, UAT y revisión de costo/latencia.
