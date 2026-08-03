# P-30 — Retomar ideas del pasado sin importar el estado

**Estado:** ESPECIFICADA — lista para implementación por cortes; **sin código implementado**.
**Requerimiento de negocio:** `Client_partner/.../Nuevas iniciativas/REQ-014_Retomar_ideas_del_pasado.md`.
**Fecha de decisión:** 2026-07-31 (reunión con Felipe Arango, GHT).
**Áreas afectadas:** orquestador conversacional, consulta de ideas por participante, persistencia
Cosmos, guardrails, observabilidad y pruebas.
**Contratos relacionados:** `03 §3.3/§3.6/§3.8`, `05 §4.4`, `08 §2.2`, `10 §6`, `13 §3`,
`Reglas §2/§3`. **Extiende:** I-19 §(reapertura) y P-26 §5.8. **Se coordina con:** P-28 (despertar).

---

## 1. Resumen ejecutivo

Hoy un participante solo puede reabrir una idea dentro del alcance vigente y mientras la campaña esté
activa; la reapertura de I-19/P-26 opera sobre ideas candidatas del ciclo en curso. GHT pidió que el
participante pueda **volver a una idea que ya aportó o que ya fue evaluada en el pasado —sin importar
su estado— y seguir trabajándola**, o bien crear una idea nueva.

P-30 extiende la reapertura de I-19 para que el participante pueda **listar y elegir cualquiera de sus
ideas anteriores** (madura, en incubación, cerrada o rechazada), dentro del alcance de campaña/pregunta
al que pertenece, y continuar la conversación sobre ella conservando su `ideaId` y su historial. La
selección es **determinista** (lista numerada por número/título); la búsqueda **semántica** por
lenguaje natural (base vectorial) queda para una fase posterior.

Este comportamiento complementa el despertar (P-28): al reactivar, el coach puede ofrecer "continuar
una idea previa o crear una nueva", y P-30 resuelve la primera opción.

---

## 2. Decisiones confirmadas

| Decisión | Regla aprobada |
|---|---|
| Alcance de "retomar" | Cualquier idea previa del participante, **sin importar su estado** (madura, incubación, cerrada, rechazada). |
| Cómo se elige | Lista **determinista** numerada de ideas del participante en el alcance vigente (número o título exacto no ambiguo). |
| Identidad de la idea | La reapertura conserva el mismo `ideaId` (I-19); no crea una idea nueva. |
| Aislamiento | Nunca mezcla ideas de otra campaña o de otra pregunta; primero se resuelve el alcance (P-26). |
| Idea madura reabierta | Suspende su curaduría hasta cerrar la nueva evaluación (coherente con I-19/P-26 §5.8). |
| Re-evaluación | La nueva versión completa se vuelve a evaluar con la rúbrica vigente (I-19). |
| Búsqueda por lenguaje natural | **Fuera de alcance** (requiere base vectorial); se hará en una fase posterior. |
| Interruptor | Kill-switch global `Conversacion:RetomarIdeasHabilitado` (default `false` hasta UAT). |
| Compatibilidad | Con el interruptor apagado se conserva la reapertura vigente de I-19/P-26. |

---

## 3. Alcance

### 3.1 Incluido
- Consulta de las ideas de un participante en un alcance (campaña/pregunta) **sin filtrar por estado**.
- Menú determinista para elegir una idea previa a retomar (número/título exacto no ambiguo).
- Reapertura conservando `ideaId`, historial y versiones (I-19); re-evaluación de la versión completa.
- Oferta "continuar previa o crear nueva" al reactivar (handoff desde P-28).
- Aislamiento por campaña/pregunta y suspensión de curaduría en ideas maduras reabiertas.
- Observabilidad y control por interruptor.

### 3.2 Fuera de alcance
- **Búsqueda semántica / vectorial** para ubicar la idea por descripción en lenguaje natural.
- Recuperar ideas de **otra campaña o pregunta** sin pasar por la resolución de alcance (P-26).
- Publicación o curaduría automática (la curaduría experta sigue siendo obligatoria).
- El despertar/reactivación en sí (P-28) y el cierre por tiempo (P-29).

---

## 4. Conceptos funcionales

| Concepto | Significado |
|---|---|
| Idea previa | Cualquier idea del participante ya registrada, en cualquier estado. |
| Retomar | Reabrir una idea previa conservando su `ideaId` para seguir trabajándola. |
| Lista de ideas | Menú numerado, determinista, de ideas del participante en el alcance vigente. |
| Alcance | Campaña y pregunta a las que pertenece la idea; se resuelve antes de listar (P-26). |

---

## 5. Flujo funcional

### 5.1 Orden determinista
1. El participante pide explícitamente retomar/continuar una idea previa (vocabulario configurable,
   como en P-26 §6) o elige "continuar previa" en el menú de reactivación (P-28).
2. Se resuelve el **alcance** (campaña/pregunta) con P-26 si no hay afinidad vigente.
3. El servidor consulta las ideas del participante en ese alcance **sin filtrar por estado** y
   construye una lista numerada (título/resumen corto + estado visible de forma neutral).
4. Si hay **una** candidata, se confirma brevemente; si hay **varias**, se presenta el menú; si **no
   hay** ideas previas, se ofrece crear una nueva.
5. Elegida la idea, I-19 la reabre con el mismo `ideaId`; los aportes siguientes construyen una nueva
   versión completa que se vuelve a evaluar.
6. Una idea madura reabierta suspende su curaduría hasta cerrar la nueva evaluación.

### 5.2 Selección
- Acepta número de la lista vigente o título exacto no ambiguo; coincidencias parciales ambiguas se
  rechazan y se vuelve a pedir (patrón de P-26 §5.3).
- Cada intento queda auditado sin copiar el texto libre a logs técnicos.
- Al aceptar, se revalida que la campaña siga `activa` y el participante habilitado.

### 5.3 Relación con crear idea nueva
Si el participante no pide retomar y aporta contenido sustantivo, se comporta como idea nueva (P-26).
"Retomar" solo ocurre ante intención explícita o selección en el menú de reactivación.

---

## 6. Parte determinista y parte LLM

| Parte del flujo | Tipo | Responsable |
|---|---|---|
| Detectar intención explícita de retomar | Determinista con vocabulario configurable | Servidor |
| Resolver alcance (campaña/pregunta) | Determinista | Servidor (P-26) |
| Consultar y listar ideas del participante | Determinista | Servidor/Cosmos |
| Validar selección (número/título no ambiguo) | Determinista | Servidor |
| Reabrir con el mismo `ideaId` y re-evaluar | Determinista + no determinista | Servidor (I-19) + LLM (evaluación validada) |

El LLM nunca elige por el participante qué idea retomar.

---

## 7. Contratos de datos y configuración

Sin contenedores nuevos. Reutiliza ideas/versiones de I-19 en `conversations`/`responses`.

- **Reutilización de I-19 (no se crea):** la reapertura conserva `ideaId` e incrementa el contador
  **existente** `Conversacion.reaperturas` (`03`); estados `estadoResultado`/`estadoCuraduria`/
  `nivelMadurez` y el campo `motivoCierre` ya existen y se respetan.
- **Consulta aditiva:** capacidad de listar ideas por `usuarioId + campaniaId (+ preguntaId)` **sin
  filtro de estado**. Si el patrón de indexado actual (`03 §3.8`) no la soporta con eficiencia, se
  añade una política de índice **aditiva**; no cambia la forma de los documentos.
- **Interruptor global (aditivo):** `Conversacion:RetomarIdeasHabilitado` (`bool`, default `false`).
- **Vocabulario configurable (aditivo):** frases de "retomar/continuar la anterior" análogas a
  `FrasesContinuar` / al vocabulario de cambio de P-26; ausente ⇒ conjunto por defecto.
- **Telemetría:** `LogSeguridad(retomarIdea)` con
  `accion=ofrecido|seleccionado|invalido|reabierto`, conteo de opciones, ids internos y
  `correlationId`; nunca texto ni títulos completos del participante (`10 §6`).

No cambia el contrato de API administrativa.

---

## 8. Seguridad, privacidad y observabilidad
- El menú solo muestra ideas **del propio participante** en el alcance vigente; nunca de terceros ni de
  otras campañas/preguntas.
- Estado de la idea mostrado de forma neutral, sin exponer puntajes de rúbrica (I-20).
- Revalidación de elegibilidad al mostrar y al aceptar la selección (evita carreras con cambios admin).
- No registrar texto ni títulos completos en telemetría técnica.

---

## 9. Manejo de condiciones especiales

| Caso | Comportamiento |
|---|---|
| El participante no tiene ideas previas en el alcance | Se ofrece crear una nueva. |
| Título ambiguo | Se pide responder con el número. |
| Idea pertenece a otra campaña/pregunta | Se resuelve primero el alcance; nunca se mezcla. |
| Idea madura reabierta | Suspende curaduría hasta cerrar la nueva evaluación. |
| Campaña se cerró tras mostrar la lista | Se rechaza la selección y se recalcula; no se reabre allí. |
| Interruptor apagado | Se conserva la reapertura vigente de I-19/P-26. |
| Selección inválida | Conserva el contexto y vuelve a pedir; no adivina la idea. |

---

## 10. Criterios de aceptación
1. El participante puede retomar una idea previa **aunque ya haya sido evaluada, cerrada o rechazada**.
2. La lista de ideas se construye **sin filtrar por estado**, dentro del alcance de campaña/pregunta.
3. La reapertura conserva el mismo `ideaId` y el historial; los aportes forman una versión nueva que
   se re-evalúa (I-19).
4. El participante puede elegir entre **continuar una idea previa** o **crear una nueva**.
5. Número y título exacto no ambiguo funcionan; una selección ambigua/ inválida se comporta como §5.2.
6. Nunca aparecen ideas de otra campaña/pregunta ni de otros participantes.
7. Una idea madura reabierta suspende su curaduría hasta cerrar la nueva evaluación.
8. Con el interruptor apagado se conserva la reapertura vigente de I-19/P-26.
9. Una prueba simulada cubre: intención de retomar → alcance → lista → selección → reapertura →
   re-evaluación, sin WhatsApp real.

---

## 11. Plan de implementación por cortes

| Corte | Entrega verificable | Pruebas mínimas |
|---|---|---|
| 1 | Consulta aditiva de ideas por participante/alcance sin filtro de estado, interruptor y vocabulario configurable. | Índice/consulta, histórico, default OFF. |
| 2 | Menú determinista, selección validada y reapertura I-19 con re-evaluación; aislamiento y telemetría. | 0/1/N ideas, ambigüedad, aislamiento por campaña/pregunta, madura→suspende curaduría. |
| 3 | Handoff desde P-28 (continuar previa vs crear nueva), E2E simulada, QA y cierre documental. | Flujo completo simulado, build/test/format/diff. |

Cada corte deja `TODO.md` y `AVANCES.md` actualizados. No desplegar sin instrucción posterior.

---

## 12. Rollback
1. Apagar `Conversacion:RetomarIdeasHabilitado`.
2. Vuelve a operar la reapertura vigente de I-19/P-26; nada persistido se borra.
3. La consulta y el vocabulario son aditivos y no afectan los flujos anteriores.
