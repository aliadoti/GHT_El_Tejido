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

## 3. Diseño técnico (D4: acotado por diseño)
1. **Puerto** `IBaseConocimientoCampania` (Application):
   `Task<IReadOnlyList<AporteRelevante>> RecuperarAsync(string campaniaId, string textoConsulta, IReadOnlyCollection<string> tags, int topK, CancellationToken ct)`.
   `AporteRelevante = { resumen, tags, fecha }` — **solo resúmenes anonimizados**, nunca el
   Markdown completo ni nombre/número del autor.
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
- Config nueva: `Conversacion:TopKAportes` (default 3), `Conversacion:PresupuestoTokensTejido`,
  `Conversacion:RecuperacionSemantica` (off). El flag por campaña lo define I-10 (contrato `03`
  aditivo, commit aparte). Si se usa opción B: campo `embedding` en `responses` (spec `03 §3.8`,
  commit aparte).

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
