# P-28 — Despertar proactivo del coach (conversación iniciada por el participante)

**Estado:** DONE local (2026-08-04, 3/3): entrada de una o varias campañas, flag, vocabulario,
redacción/fallback, telemetría, round-trip Cosmos, E2E simulada, QAS y cierre documental implementados.
El interruptor global permanece `false`; no hubo push, despliegue ni configuración remota.
**Requerimiento de negocio:** `Client_partner/.../Nuevas iniciativas/REQ-012_Despertar_proactivo_coach.md`.
**Fecha de decisión:** 2026-07-31 (reunión de retroalimentación con Felipe Arango, GHT).
**Áreas afectadas:** webhook, resolución de participante, orquestador conversacional, persistencia
Cosmos, guardrails, observabilidad y pruebas E2E simuladas.
**Contratos relacionados:** `03 §3.3/§3.6`, `05 §4.3/§4.4`, `06 §3`, `10 §2/§6`, `13 §3`,
`Reglas §2/§3`. **Reutiliza:** P-26 (selección de campaña/pregunta y ciclos) e I-19
(ideas/versiones). **No es prerrequisito técnico de P-26:** la participación continua ya procesa un
aporte sustantivo y abre un ciclo nuevo.

---

## 1. Resumen ejecutivo

P-26 ya resuelve el primer contacto con trabajo pendiente y, en una campaña continua, un aporte
sustantivo posterior abre un ciclo nuevo. El vacío real no es “permitir otro aporte”, sino dar una
entrada humana cuando no hay idea activa y el participante escribe un **saludo o petición breve de
iniciar/continuar**, sin convertir ese texto en una idea accidental. Los escenarios son:

1. **Primer contacto por iniciativa del participante:** a alguien le comparten el número de contacto
   y escribe por su cuenta, antes o en lugar de recibir el mensaje inicial de la campaña.
2. **Reingreso tras cierre o inactividad:** el participante vuelve horas o días después y primero
   quiere saber cómo continuar, crear una idea nueva o —cuando P-30 esté activo— retomar una previa.

P-28 añade una entrada determinista para esos mensajes breves. El servidor decide si continúa,
ofrece opciones o delega el aporte a P-26; el LLM solo puede redactar el saludo, con fallback. Un
mensaje sustantivo no se retiene esperando una elección: conserva la regla P-26 de crear un ciclo
nuevo, salvo una petición explícita de retomar.

---

## 2. Decisiones confirmadas

| Decisión | Regla aprobada |
|---|---|
| Disparo | Lo inicia **siempre el participante** con un mensaje entrante; el sistema no envía nada proactivo por su cuenta. |
| Elegibilidad para responder | Usuario `activo`, rol `participante`, asociado a ≥1 campaña `activa`. Si no cumple, aplica el rechazo neutral vigente (`06 §3`). |
| Sin flujo activo + saludo/petición breve | El coach saluda y ofrece solo acciones realmente elegibles: continuar una previa cuando P-30 esté activo o crear una nueva cuando P-26 lo permita. |
| Aporte sustantivo sin flujo | Se entrega directamente a P-26; no se confunde con un saludo ni se obliga a reescribirlo. |
| Primer contacto con trabajo pendiente | Ya lo cubre el flujo base/P-26; P-28 no lo reimplementa. |
| Resolución de campaña/pregunta | Se delega en P-26 (menú determinista) cuando hay varias; con una sola, avanza sin menú. |
| Reanudación | Solo se ofrece retomar si P-30 está habilitada y hay candidatas; la selección detallada pertenece a P-30. P-28 no la presupone. |
| Ventana de WhatsApp | La respuesta libre solo es válida dentro de la ventana de servicio de 24 h que abre el propio mensaje entrante; el sistema **no** fuerza plantillas (HSM) para despertar. |
| Parte LLM | Solo la **redacción** del saludo de reactivación, con fallback determinista. |
| Interruptor | Kill-switch global `Conversacion:DespertarProactivoHabilitado` (default `false` hasta UAT). |
| Compatibilidad | Con el interruptor apagado se conserva el comportamiento actual (sin respuesta si no hay flujo activo). |

---

## 3. Alcance

### 3.1 Incluido
- Reconocer, con vocabulario determinista y acotado, un saludo o petición breve de reingreso cuando
  no hay idea activa.
- Bienvenida/reactivación redactada por LLM con **fallback** determinista.
- Enganche con P-26 para resolver campaña y pregunta (0 → rechazo neutral, 1 → automático, N → menú).
- Oferta explícita de **continuar idea previa** solo cuando P-30 esté habilitado y existan candidatas;
  en caso contrario, ofrecer solo una acción que el sistema pueda ejecutar.
- Idempotencia ante reintentos de Meta (no crear dos hilos por el mismo `whatsappMessageId`).
- Observabilidad del evento de despertar y control por interruptor.

### 3.2 Fuera de alcance
- **Envío proactivo iniciado por el sistema** (recordatorios/nudges fuera de la ventana de 24 h):
  eso es P-08 y requiere plantilla HSM aprobada.
- Identificación **semántica** de a qué idea se refiere el participante por lenguaje natural
  (base de datos vectorial): fase posterior; ver P-30 §3.2.
- El primer contacto con trabajo pendiente, el enrutamiento de un aporte sustantivo o la creación del
  ciclo: ya los cubre P-26 y no se reimplementan aquí.
- Selección entre múltiples campañas simultáneas: el mecanismo es de P-26 (P-28 solo lo invoca).
- Cambios en la máquina de estados de evaluación (I-19) o en el cierre por tiempo (P-29).

---

## 4. Conceptos funcionales

| Concepto | Significado |
|---|---|
| Conversación dormida | Conversación del participante en estado `cerrada` o inexistente; no implica por sí misma que una idea se reabra. |
| Reactivación | Entrada humana ante saludo/petición breve cuando no había idea activa; no es sinónimo de reapertura de idea. |
| Primer contacto | Mensaje entrante de un participante matriculado que aún no había interactuado. |
| Saludo de reactivación | Mensaje breve del coach que confirma presencia y ofrece continuar o crear idea. |

---

## 5. Flujo funcional

### 5.1 Orden determinista ante un mensaje entrante
1. Deduplicar el webhook (antes de crear cualquier documento).
2. Normalizar número; validar usuario `activo`, rol `participante` y asociación a campaña `activa`
   (`06 §3`). Si falla → rechazo neutral vigente. Fin.
3. Si existe una **conversación abierta / afinidad vigente** (P-26 §5.6) → continúa ese hilo. Fin.
4. Si el mensaje pide explícitamente retomar una idea → delegar a P-30 cuando esté habilitado; con P-30
   apagado se conserva la reapertura acotada de I-19/P-26. Fin.
5. Si el mensaje es un aporte sustantivo → P-26 resuelve alcance y lo procesa una vez como aporte raíz.
   Fin.
6. Si el mensaje coincide con el vocabulario breve de saludo/inicio y el interruptor
   `DespertarProactivoHabilitado` está encendido → resolver alcance con P-26 y enviar el saludo de
   reingreso con acciones elegibles. Si el interruptor está apagado, se conserva el comportamiento
   previo para ese mensaje sin flujo. Fin.

### 5.2 Primer contacto
Un participante matriculado con trabajo pendiente ya recibe pregunta por el flujo base. P-28 solo
aplica si no hay trabajo/idea activa y el texto es un saludo o petición de entrada. Un participante no
matriculado sigue el rechazo neutral y nunca conoce campañas ajenas.

### 5.3 Ventana de servicio de WhatsApp
El mensaje entrante del participante abre una ventana de servicio de 24 h; dentro de ella el saludo de
reactivación y la conversación se envían como mensajes libres. P-28 **no** envía mensajes fuera de esa
ventana ni dispara plantillas HSM para "despertar" por su cuenta (eso es P-08).

---

## 6. Parte determinista y parte LLM

| Parte del flujo | Tipo | Responsable |
|---|---|---|
| Deduplicación, validación de acceso y estado | Determinista | Servidor |
| Distinguir saludo/petición breve de un aporte sustantivo | Determinista, vocabulario acotado | Servidor |
| Detectar ausencia de flujo activo y decidir ofrecer entrada | Determinista | Servidor |
| Resolver campaña/pregunta y elegibilidad | Determinista | Servidor (P-26) |
| Listar ideas reanudables | Determinista | Servidor (P-30) |
| Redactar el saludo de reactivación | No determinista, validado | LLM propone; servidor valida y aplica fallback |

El LLM nunca decide acceso, campaña, pregunta ni estado.

---

## 7. Contratos de datos y configuración

P-28 **no introduce contenedores nuevos**. Reutiliza `Conversacion`, `EnrutamientoAporte` (P-26) e
ideas/versiones (I-19).

- **Interruptor global (aditivo):** `Conversacion:DespertarProactivoHabilitado` (`bool`, default
  `false`). Kill-switch de operación; cuando está apagado, la entrada de saludo/inicio de §5.1 paso 6
  conserva el comportamiento previo.
- **Prompt de reactivación (aditivo, opcional):** `promptRefs.reactivacion` por campaña/pregunta;
  ausente ⇒ usa un texto determinista de fallback. Versionado como el resto de `promptRefs` (`07 §4`).
- **Telemetría:** `LogSeguridad(despertarProactivo)` con
  `accion=primerContacto|reactivacion|rechazoNeutral`, `correlationId`, ids internos; **nunca** texto
  del participante (`10 §6`).

No cambia el contrato de API administrativa ni se crea un contenedor. Se añade de forma compatible el
booleano opcional `EnrutamientoAporte.esEntradaProactiva` en el documento existente de `conversations`:
solo conserva la intención de saludo mientras se selecciona una campaña; al resolverla pasa a
`completado`, sin `procesadoEn`, conversación ni idea. Ausente en documentos históricos equivale a
`false`. El `promptRef` opcional viaja por los endpoints de configuración ya existentes (`07 §4`).

---

## 8. Seguridad, privacidad y observabilidad
- El rechazo neutral se mantiene idéntico para no matriculados/inactivos (`06 §3`); no se filtra la
  existencia de campañas ajenas.
- Deduplicación del webhook **antes** de crear documentos; un reintento de Meta no crea dos hilos.
- No registrar texto libre, nombres ni números en telemetría técnica.
- El saludo de reactivación pasa por los mismos guardrails de salida del coaching (I-20): sin revelar
  rúbrica ni puntajes.

---

## 9. Manejo de condiciones especiales

| Caso | Comportamiento |
|---|---|
| Mensaje de un no matriculado | Rechazo neutral; no se despierta nada. |
| Interruptor apagado | Sin flujo activo, no se responde (comportamiento actual). |
| Reintento de Meta del mismo mensaje | Se reutiliza el hilo/enrutamiento; no se duplica. |
| Varias campañas activas | Se delega el menú a P-26; no se elige silenciosamente. |
| P-30 habilitada y con candidatas | Se ofrece retomar una idea o crear una nueva; P-30 define el selector. Sin esas condiciones, P-28 no promete reanudación. |
| Falla el LLM al redactar el saludo | Se usa el texto de fallback determinista; el flujo continúa. |
| Mensaje entrante fuera de toda ventana previa | El propio mensaje abre la ventana de 24 h; se responde libre. |

---

## 10. Criterios de aceptación
1. Con el interruptor encendido, un participante matriculado que escribe **primero** recibe bienvenida
   e inicia la interacción.
2. Tras un cierre/timeout, un saludo o inicio no sustantivo recibe la entrada P-28 bajo la ventana de
   WhatsApp; un aporte sustantivo sigue directamente el ciclo nuevo de P-26.
3. El coach ofrece **retomar una idea previa** solo si P-30 está habilitada y encuentra candidatas;
   en cualquier caso ofrece crear una nueva, sin recuento largo.
4. Un no matriculado o inactivo recibe el rechazo neutral vigente, sin revelar campañas ajenas.
5. Con dos o más campañas activas, se usa el menú de P-26; no se elige una en silencio.
6. Un reintento de Meta del mismo `whatsappMessageId` no crea dos hilos ni dos ideas.
7. Con el interruptor apagado se conserva el comportamiento actual para saludos e inicios no
   sustantivos; P-26 conserva su ruta directa para aportes sustantivos elegibles.
8. Si el LLM falla, el saludo de reingreso usa el fallback determinista y la conversación continúa.
9. Una prueba E2E simulada cubre: saludo sin flujo → entrada → acción elegible; y aporte sustantivo
   sin flujo → ciclo P-26 directo, sin WhatsApp real.

---

## 11. Plan de implementación por cortes

| Corte | Entrega verificable | Pruebas mínimas |
|---|---|---|
| 1 | Interruptor, `promptRefs.reactivacion` y vocabulario de saludo/inicio; no duplicar primer contacto ni ciclo P-26. | Round-trip, histórico y aporte sustantivo sin secuestro. |
| 2 | Entrada: saludo LLM con fallback, enganche con P-26, acciones elegibles, idempotencia y telemetría. | Saludo, reintento Meta, rechazo neutral, fallback y ausencia/presencia de P-30. |
| 3 | E2E simulada, QA y cierre documental. | Saludo → entrada; aporte → ciclo P-26, build/test/format/diff. |

Cada corte deja `TODO.md` y `AVANCES.md` actualizados. No desplegar ni cambiar configuración remota
sin instrucción posterior del usuario.

---

## 12. Rollback
1. Apagar `Conversacion:DespertarProactivoHabilitado`.
2. Para saludos e inicios no sustantivos, el sistema vuelve a no responder cuando no hay flujo activo;
   P-26 conserva su ruta directa para aportes sustantivos. Nada persistido se borra.
3. Las conversaciones e ideas ya creadas siguen siendo legibles por los flujos anteriores.
