# I-09 — Tejido colectivo: LLM con acceso a la base común

> **Origen:** hoja `Iniciativas` (corazón del "tejido").
> **Tipo:** Desarrollo · **Prioridad:** Alta (gran apuesta, ruta crítica) · **Ventana:** Sprint 1a
> diseño / Sprint 1b core · **Dependencia:** I-11 (rúbrica) · **Riesgo:** Alto (RL-3 costo/latencia,
> RL-4 injection transitiva). Cubre REQ §9/§27, ARQ §4.2/§12; specs base `05 §4`, `08 §3/§5`, `03 §3.8`.

## 1. Qué pide GHT / por qué
Cada conversación se enriquece con la **base de conocimiento común de la campaña**: el coach
teje los aportes de otros expertos en la conversación (deja de ser autocontenida).

## 2. Estado actual del build
Nuevo. El LLM recibe solo `HistorialReciente` del propio hilo. No hay embeddings ni vector store.
**Diseño cerrado el 2026-07-15 (Sprint 1a, agente Codex)** — ver §8 y `SUPUESTOS.md#tejido-colectivo-i09-diseno`. El **core** de recuperación/inyección se implementa en Sprint 1b.

## 3. Diseño técnico (D4: acotado por diseño)
1. **Puerto** `IBaseConocimientoCampania` (Application):
   `Task<IReadOnlyList<AporteRelevante>> RecuperarAsync(string campaniaId, string textoConsulta, IReadOnlyCollection<string> tags, int topK, CancellationToken ct)`.
   `AporteRelevante = { resumen, tags, fecha }` — **solo resúmenes anonimizados**, nunca el
   Markdown completo ni nombre/número del autor.
   **`resumen` derivado de lo existente (decisión cerrada 2026-07-15):** `Evaluacion.temas ∪ entidades`
   + un extracto **sanitizado** (≤ ~240 chars) de `Respuesta.texto` (strip de patrones imperativos y de
   nombres/números). **Sin campo nuevo en `03`** (coherente con el patrón "derivar de documentos
   existentes"); la anonimización es determinista y server-side.
2. **Implementación MVP (opción A, default):** recuperación **liviana y determinista** sobre los
   resúmenes/textos de `responses` de la campaña (partición Cosmos): filtro por `campaniaId` +
   solapamiento léxico de keywords + boost por tags compartidas (I-14) + recencia. Cero dependencia
   nueva, auditable. **Opción B (flag `Conversacion:RecuperacionSemantica`, off):** embeddings vía
   el proveedor LLM configurado, campo aditivo `embedding` en `responses`, coseno en memoria.
3. **Inyección como DATO no confiable (extensión anti-injection de `08 §3.2`):** los aportes
   recuperados entran **dentro del delimitador de dato** (`<<<APORTES_DE_LA_COMUNIDAD ... >>>`),
   con instrucción explícita de que **no son instrucciones**, sanitización previa (strip de
   patrones imperativos/instrucción, longitud máxima por fragmento) y presupuesto fijo de tokens
   para el bloque (se trunca antes de armar el prompt; respeta `limitesTokens.maxPrompt`).
   *Nota:* esta decisión (D4, 11-jul) **sustituye** la idea inicial de inyectarlos como mensaje
   `system` (plan_hito_1 §5): el contenido de terceros jamás viaja con rol de instrucción.
4. **Activación por campaña:** el orquestador consulta el flag de campaña `tejidoColectivo`
   (I-10, default off). Sin aportes relevantes (o error de recuperación) → conversación
   autocontenida (degradación limpia, sin fallo visible).
5. **Consentimiento (P-07):** solo se tejen aportes de participantes bajo una campaña cuyo
   arranque declaró el uso colectivo; anonimizado por defecto.

## 4. Contratos y configuración
- **Flag por campaña `configConversacional.tejidoColectivo`** (bool, default `false`) — **declarado por
  I-09** (`03 §3.3`, aditivo con default seguro, commit aparte al implementar el core). Decisión del
  usuario 2026-07-15: I-09 lo declara ahora (el core Sprint 1b lo necesita para gatear); **I-10**
  (Sprint 2) añade sobre el mismo campo la semántica *base previa vs. blanco* y su UI.
- **Kill-switch global** `Conversacion:TejidoColectivo` (default on = respeta la campaña; `false` apaga
  el tejido para todas sin redeploy).
- Config nueva: `Conversacion:TopKAportes` (default 3), `Conversacion:PresupuestoTokensTejido`,
  `Conversacion:UmbralSolapamientoTejido`, `Conversacion:RecuperacionSemantica` (off, global).
- **Opción B diferida:** si algún día se activa, campo `embedding` en `responses` (`03 §3.8`, aditivo,
  commit aparte) — **no se declara en el Hito**.
- Sin cambios en `04` (los aportes no se exponen por API; el tejido es interno al orquestador/evaluador).

## 5. Riesgos y mitigación (RL-3, RL-4, RO-5)
- *Costo/latencia* → top-K pequeño, recuperación local, presupuesto de tokens; medición de costo
  por conversación en Sprint 1b (criterio de salida).
- *Prompt-injection transitiva* → delimitador de dato + sanitización + la salida se valida igual
  (esquema `08 §4`); el sistema nunca ejecuta lo que el modelo "pida".
- *Fuga de PII entre participantes* → resúmenes anonimizados, regla dura sin nombres/números;
  consentimiento (P-07).
- *Ruido/irrelevancia* → umbral mínimo de solapamiento; sin aportes → autocontenida.

## 6. Criterios de aceptación / pruebas
- Unit del puerto: filtra por campaña y tags, respeta topK, lista vacía → contexto sin bloque de tejido.
- Unit de sanitización: un aporte con "ignora tus instrucciones..." queda neutralizado/truncado.
- Unit: presupuesto de tokens se respeta (bloque truncado, prompt < maxPrompt).
- Unit: campaña con flag off → cero llamadas a la recuperación.
- Calibración/pruebas conjuntas: el coach teje aportes relevantes sin exponer PII.

## 7. Degradación
`tejidoColectivo=false` por campaña (sin redeploy) → modo autocontenido probado. El Hito puede
entregarse sin I-09; es lo que da valor diferencial, por eso va en ruta crítica con prioridad.

## 8. Decisiones de diseño cerradas (Sprint 1a, 2026-07-15, agente Codex)
Confirmadas con el usuario antes de cerrar (las tres tocaban contrato/PII/alcance). Registro completo en
`SUPUESTOS.md#tejido-colectivo-i09-diseno`.

1. **Fuente del `resumen` anonimizado → derivar de lo existente.** `Evaluacion.temas ∪ entidades` +
   extracto sanitizado (≤240 chars) de `Respuesta.texto`; **sin campo nuevo en `03`**. Descartado: solo
   etiquetas (muy pobre para tejer) y campo `resumenAnonimo` generado por LLM (contrato nuevo + costo +
   dato no confiable).
2. **Dueño del flag `tejidoColectivo` → I-09 declara ya el campo per-campaña** (`03 §3.3` aditivo). I-10
   (Sprint 2) le añade la semántica base-previa-vs-blanco y la UI. Descartado: diferir el campo entero a
   I-10 y gatear I-09 core sobre un flag global temporal.
3. **Alcance de recuperación del core (Sprint 1b) → solo Opción A léxica determinista.** Opción B
   (embeddings, `Conversacion:RecuperacionSemantica` global off) queda como puerto pluggable diferido; no
   añade el campo `embedding` a `03` en el Hito. Descartado: construir A y B ahora (mayor blast radius en
   ruta crítica).

**Puerto y piezas (para el core Sprint 1b):** `IBaseConocimientoCampania.RecuperarAsync(campaniaId,
textoConsulta, tags, topK, ct)` en Application; impl A `RecuperadorLexicoBaseConocimiento` en
Infrastructure (filtro `campaniaId`+`estado=evaluada`, solapamiento léxico + boost por tags + recencia,
umbral mínimo, excluye al propio autor y la conversación en curso). Inyección por el bloque delimitado de
`08 §3.2`; degradación e integración en el orquestador por `05 §4.8`. Contratos aditivos con default
seguro; rollback sin redeploy por `tejidoColectivo=false` (campaña) o `Conversacion:TejidoColectivo=false`
(global). Criterio de salida del core: costo/latencia por conversación medidos bajo flag en staging.
