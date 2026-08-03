# P-28 — Despertar proactivo del coach (conversación iniciada por el participante)

**Estado:** ESPECIFICADA — lista para implementación por cortes; **sin código implementado**.
**Requerimiento de negocio:** `Client_partner/.../Nuevas iniciativas/REQ-012_Despertar_proactivo_coach.md`.
**Fecha de decisión:** 2026-07-31 (reunión de retroalimentación con Felipe Arango, GHT).
**Áreas afectadas:** webhook, resolución de participante, orquestador conversacional, persistencia
Cosmos, guardrails, observabilidad y pruebas E2E simuladas.
**Contratos relacionados:** `03 §3.3/§3.6`, `05 §4.3/§4.4`, `06 §3`, `10 §2/§6`, `13 §3`,
`Reglas §2/§3`. **Depende de:** P-26 (selección de campaña/pregunta), I-19 (ideas/versiones).
**Habilita:** campañas continuas (P-26) en la práctica.

---

## 1. Resumen ejecutivo

Hoy, si un participante escribe al Tejido de Red y **no hay una conversación activa** —porque nunca
recibió el mensaje inicial, o porque su última conversación se cerró o expiró por inactividad— el
sistema **no responde**. Esto rompe dos escenarios que GHT necesita para la convención y para las
campañas continuas:

1. **Primer contacto por iniciativa del participante:** a alguien le comparten el número de contacto
   y escribe por su cuenta, antes o en lugar de recibir el mensaje inicial de la campaña.
2. **Reanudar una conversación dormida:** el participante vuelve horas o días después y escribe para
   seguir aportando; el coach debe "despertar" y retomar, no quedarse mudo.

P-28 hace que **todo mensaje entrante de un participante reconocido y habilitado reciba respuesta**,
aunque no exista un flujo activo. El servidor decide de forma determinista si crea, reabre o continúa
un hilo, y delega en P-26 la resolución de campaña/pregunta cuando haya más de una opción. El LLM solo
redacta el saludo de reactivación; nunca decide acceso, campaña ni estado.

Este comportamiento es **prerrequisito de las campañas continuas (P-26)**: sin despertar, un
participante no puede volver a abrir una idea nueva después de que su recorrido terminó.

---

## 2. Decisiones confirmadas

| Decisión | Regla aprobada |
|---|---|
| Disparo | Lo inicia **siempre el participante** con un mensaje entrante; el sistema no envía nada proactivo por su cuenta. |
| Elegibilidad para responder | Usuario `activo`, rol `participante`, asociado a ≥1 campaña `activa`. Si no cumple, aplica el rechazo neutral vigente (`06 §3`). |
| Sin flujo activo | El coach saluda breve y ofrece **continuar una idea previa** o **crear una idea nueva**; no hace recuento largo. |
| Primer contacto | Un participante matriculado en campaña activa puede iniciar aunque no haya recibido el mensaje inicial. |
| Resolución de campaña/pregunta | Se delega en P-26 (menú determinista) cuando hay varias; con una sola, avanza sin menú. |
| Reanudación | Si hay una idea reciente reanudable, se ofrece continuarla (la selección detallada es de P-30). |
| Ventana de WhatsApp | La respuesta libre solo es válida dentro de la ventana de servicio de 24 h que abre el propio mensaje entrante; el sistema **no** fuerza plantillas (HSM) para despertar. |
| Parte LLM | Solo la **redacción** del saludo de reactivación, con fallback determinista. |
| Interruptor | Kill-switch global `Conversacion:DespertarProactivoHabilitado` (default `false` hasta UAT). |
| Compatibilidad | Con el interruptor apagado se conserva el comportamiento actual (sin respuesta si no hay flujo activo). |

---

## 3. Alcance

### 3.1 Incluido
- Manejar un mensaje entrante cuando **no hay conversación abierta** para el participante.
- Bienvenida/reactivación redactada por LLM con **fallback** determinista.
- Enganche con P-26 para resolver campaña y pregunta (0 → rechazo neutral, 1 → automático, N → menú).
- Oferta explícita de **continuar idea previa** (handoff a P-30) **o crear una nueva**.
- Idempotencia ante reintentos de Meta (no crear dos hilos por el mismo `whatsappMessageId`).
- Observabilidad del evento de despertar y control por interruptor.

### 3.2 Fuera de alcance
- **Envío proactivo iniciado por el sistema** (recordatorios/nudges fuera de la ventana de 24 h):
  eso es P-08 y requiere plantilla HSM aprobada.
- Identificación **semántica** de a qué idea se refiere el participante por lenguaje natural
  (base de datos vectorial): fase posterior; ver P-30 §3.2.
- Selección entre múltiples campañas simultáneas: el mecanismo es de P-26 (P-28 solo lo invoca).
- Cambios en la máquina de estados de evaluación (I-19) o en el cierre por tiempo (P-29).

---

## 4. Conceptos funcionales

| Concepto | Significado |
|---|---|
| Conversación dormida | Conversación del participante en estado `cerrada` (por finalización o por inactividad, ver P-29) o inexistente. |
| Reactivación | Respuesta del sistema a un mensaje entrante cuando no había flujo activo. |
| Primer contacto | Mensaje entrante de un participante matriculado que aún no había interactuado. |
| Saludo de reactivación | Mensaje breve del coach que confirma presencia y ofrece continuar o crear idea. |

---

## 5. Flujo funcional

### 5.1 Orden determinista ante un mensaje entrante
1. Deduplicar el webhook (antes de crear cualquier documento).
2. Normalizar número; validar usuario `activo`, rol `participante` y asociación a campaña `activa`
   (`06 §3`). Si falla → rechazo neutral vigente. Fin.
3. Si existe una **conversación abierta / afinidad vigente** (P-26 §5.6) → continúa ese hilo. Fin.
4. Si el interruptor `DespertarProactivoHabilitado` está **apagado** → comportamiento actual
   (no responder si no hay flujo activo). Fin.
5. No hay flujo activo y el interruptor está encendido → **reactivar**:
   - resolver alcance con P-26 (0 campañas → rechazo neutral; 1 → seleccionada; N → menú de campaña);
   - una vez con alcance, **enviar el saludo de reactivación** ofreciendo continuar una idea previa
     (si existen ideas reanudables, handoff a P-30) o crear una nueva;
   - si el mensaje entrante ya es un aporte sustantivo, se conserva como aporte raíz (P-26 §5.5) y se
     procesa una vez resuelto el alcance.

### 5.2 Primer contacto
Un participante matriculado que escribe sin haber recibido el mensaje inicial entra por el mismo
camino de §5.1: si está asociado a una campaña activa, se le da la bienvenida y se inicia; si no,
recibe el rechazo neutral. **No** se revela la existencia de campañas a las que no pertenece.

### 5.3 Ventana de servicio de WhatsApp
El mensaje entrante del participante abre una ventana de servicio de 24 h; dentro de ella el saludo de
reactivación y la conversación se envían como mensajes libres. P-28 **no** envía mensajes fuera de esa
ventana ni dispara plantillas HSM para "despertar" por su cuenta (eso es P-08).

---

## 6. Parte determinista y parte LLM

| Parte del flujo | Tipo | Responsable |
|---|---|---|
| Deduplicación, validación de acceso y estado | Determinista | Servidor |
| Detectar ausencia de flujo activo y decidir reactivar | Determinista | Servidor |
| Resolver campaña/pregunta y elegibilidad | Determinista | Servidor (P-26) |
| Listar ideas reanudables | Determinista | Servidor (P-30) |
| Redactar el saludo de reactivación | No determinista, validado | LLM propone; servidor valida y aplica fallback |

El LLM nunca decide acceso, campaña, pregunta ni estado.

---

## 7. Contratos de datos y configuración

P-28 **no introduce contenedores nuevos**. Reutiliza `Conversacion`, `EnrutamientoAporte` (P-26) e
ideas/versiones (I-19).

- **Interruptor global (aditivo):** `Conversacion:DespertarProactivoHabilitado` (`bool`, default
  `false`). Kill-switch de operación; cuando está apagado, el flujo de §5.1 paso 4 corta.
- **Prompt de reactivación (aditivo, opcional):** `promptRefs.reactivacion` por campaña/pregunta;
  ausente ⇒ usa un texto determinista de fallback. Versionado como el resto de `promptRefs` (`07 §4`).
- **Telemetría:** `LogSeguridad(despertarProactivo)` con
  `accion=primerContacto|reactivacion|rechazoNeutral`, `correlationId`, ids internos; **nunca** texto
  del participante (`10 §6`).

No cambian los esquemas de Cosmos ni el contrato de API administrativa; el `promptRef` opcional viaja
por los endpoints de configuración ya existentes (`07 §4`).

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
| Existe idea reciente reanudable | Se ofrece continuarla (P-30) o crear nueva; el participante decide. |
| Falla el LLM al redactar el saludo | Se usa el texto de fallback determinista; el flujo continúa. |
| Mensaje entrante fuera de toda ventana previa | El propio mensaje abre la ventana de 24 h; se responde libre. |

---

## 10. Criterios de aceptación
1. Con el interruptor encendido, un participante matriculado que escribe **primero** recibe bienvenida
   e inicia la interacción.
2. Tras un cierre/timeout, un nuevo mensaje del participante **reactiva** la conversación sin perder su
   contexto ni su historial.
3. Al reactivar, el coach ofrece **continuar una idea previa o crear una nueva**, sin recuento largo.
4. Un no matriculado o inactivo recibe el rechazo neutral vigente, sin revelar campañas ajenas.
5. Con dos o más campañas activas, se usa el menú de P-26; no se elige una en silencio.
6. Un reintento de Meta del mismo `whatsappMessageId` no crea dos hilos ni dos ideas.
7. Con el interruptor apagado se conserva el comportamiento actual (sin respuesta si no hay flujo
   activo).
8. Si el LLM falla, el saludo de reactivación usa el fallback determinista y la conversación continúa.
9. Una prueba E2E simulada cubre: mensaje entrante sin flujo activo → reactivación → oferta
   continuar/crear → aporte procesado, sin WhatsApp real.

---

## 11. Plan de implementación por cortes

| Corte | Entrega verificable | Pruebas mínimas |
|---|---|---|
| 1 | Interruptor + `promptRefs.reactivacion` (aditivos, default seguro) y detección de "sin flujo activo" en el webhook (05 §4.3). | Round-trip config, histórico = comportamiento actual. |
| 2 | Reactivación: saludo LLM con fallback, enganche con P-26 y oferta continuar/crear; idempotencia y telemetría. | Primer contacto, reactivación, reintento Meta, rechazo neutral, fallback LLM. |
| 3 | E2E simulada, QA y cierre documental (AVANCES/SUPUESTOS/TODO). | Flujo completo simulado, build/test/format/diff. |

Cada corte deja `TODO.md` y `AVANCES.md` actualizados. No desplegar ni cambiar configuración remota
sin instrucción posterior del usuario.

---

## 12. Rollback
1. Apagar `Conversacion:DespertarProactivoHabilitado`.
2. El sistema vuelve a no responder cuando no hay flujo activo; nada persistido se borra.
3. Las conversaciones e ideas ya creadas siguen siendo legibles por los flujos anteriores.
