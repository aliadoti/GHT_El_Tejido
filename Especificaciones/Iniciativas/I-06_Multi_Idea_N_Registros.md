# I-06 — Detección de múltiples ideas → N registros

> **Origen:** hoja `Iniciativas` de `Presentacion/Plan_Trabajo_El_Tejido.xlsx`.
> **Tipo:** Desarrollo · **Prioridad:** Alta (gran apuesta, ruta crítica) · **Ventana:** Sprint 1a
> diseño / Sprint 1b implementación · **Dependencia:** — · **Riesgo:** Alto (no determinismo RL-2).
> Cubre REQ §9/§22, ARQ §4.2/§6; specs base `03 §3.8`, `05 §4`, `08`, `09`.

## 1. Qué pide GHT / por qué
Detectar **varias ideas** en una misma respuesta, separarlas en **registros distintos** (BD +
Markdown) y recorrer la rúbrica **por idea**. Hoy `Respuesta` guarda 1 idea por registro.

## 2. Estado actual del build
Nuevo. `Domain/Respuestas/Respuesta.cs` es un único texto; el orquestador crea una `Respuesta`
por mensaje entrante y compila un Markdown por respuesta evaluada.

## 3. Diseño técnico (D3: segmentador validado + fallback)
1. **Puerto** `ISegmentadorIdeas` (Application/Evaluacion o Conversacion):
   `Task<SalidaSegmentacion> SegmentarAsync(string texto, CancellationToken ct)` con contrato fijo
   `{ "ideas": [ { "texto": "string", "resumen": "string" } ] }` (1..N).
2. **Implementación** `SegmentadorIdeasLlm` (Infrastructure/Llm): reutiliza `ILlmClient` con la
   misma disciplina instrucción/dato y `response_format` JSON; validación estricta de esquema
   (patrón de `EvaluadorLlm`).
3. **Orquestador:** tras persistir el `Mensaje(in)` y ANTES de evaluar, si la **campaña** tiene
   `segmentacionIdeas=true` (campo aditivo de campaña, default `false`; ajuste 2026-07-13 —
   parametrización por campaña, ver `00_Indice §4.2`) **y** el kill-switch global
   `Conversacion:SegmentacionIdeas` no lo apaga, llama al segmentador. Por **cada idea** crea una
   `Respuesta` con campos aditivos `ideaIndice` (1..N) y `respuestaPadreId` (id lógico del mensaje
   origen) y ejecuta el ciclo de evaluación + Markdown **por idea**.
4. **Idempotencia de escritura:** id determinista `resp_<whatsappMessageId>_<ideaIndice>`; reintentos
   del webhook no duplican registros; el Markdown sigue siendo regenerable (REQ §22.4.6) — un fallo
   a mitad se completa al regenerar, no corrompe.
5. **Guardas anti-fragmentación (deterministas):**
   - `Conversacion:LongitudMinimaIdea` (default 30 chars): fragmentos menores se descartan.
   - `Conversacion:MaxIdeasPorMensaje` (default 5): excedentes se ignoran + `LogSeguridad(AnomaliaLlm)`.
   - Segmentador falla / salida inválida / 0 ideas → **fallback: 1 idea = mensaje completo**
     (comportamiento actual, cero regresión).
6. **Interacción con cupos (P-10):** la llamada de segmentación cuenta en el cupo de llamadas LLM;
   con multi-idea el consumo por turno es `1 + N` — dimensionar `maxLlamadasLlmPorUsuario` acorde.
7. **Observabilidad:** log estructurado con la distribución *ideas por mensaje* (detecta sobre/
   sub-fragmentación en pruebas y día-D).

## 4. Contratos y configuración
- `03 §3.8`: campos aditivos `ideaIndice`, `respuestaPadreId` en el doc de `responses`; `03 §3.3`
  + `04 §5.3`: campo aditivo `segmentacionIdeas` (bool, default false) en campaña —
  **actualizar specs en commit aparte**. Los GET de resultados devuelven los campos nuevos de
  forma aditiva. Portal: checkbox en la pestaña Configuración (mismo patrón que `tejidoColectivo`, I-10).
- Config global: `Conversacion:SegmentacionIdeas` (kill-switch de operación, **default true** =
  respeta lo que diga la campaña; `false` apaga la segmentación en TODAS las campañas sin tocar BD),
  `Conversacion:MaxIdeasPorMensaje`, `Conversacion:LongitudMinimaIdea`.

## 5. Riesgos y mitigación (RL-2 / R-01a)
- *Sobre-fragmentar / sub-fragmentar* → guardas deterministas + métrica de distribución + banco de
  calibración con casos multi-idea.
- *Estado parcial (N escrituras sin transacción)* → ids idempotentes + Markdown regenerable.
- *Costo LLM extra* → flag off por defecto; cupos P-10 contemplan la segmentación.

## 6. Criterios de aceptación / pruebas
- Unit: mensaje con 2 ideas claras → 2 `Respuesta` + 2 evaluaciones + 2 Markdown, `ideaIndice` 1 y 2.
- Unit: mensaje de 1 idea → 1 registro; "Hola" no genera fragmentos triviales (longitud mínima).
- Unit: salida inválida del segmentador → fallback 1-idea, flujo idéntico al actual.
- Unit: >`MaxIdeasPorMensaje` ideas → se registran las N primeras + anomalía logueada.
- Unit: campaña con `segmentacionIdeas=false` (o doc viejo sin el campo) → cero llamadas al
  segmentador y comportamiento byte a byte actual; kill-switch global en `false` anula una campaña con `true`.
- Build/test/format verdes; spec `03` actualizada en commit aparte.

## 7. Degradación
Dos niveles, ambos sin redeploy: por campaña (`segmentacionIdeas=false`, editable en portal) y
global (kill-switch `Conversacion:SegmentacionIdeas=false`, App Setting). Cualquiera devuelve el
modo 1-idea probado. El Hito no depende de esta pieza; I-09 no depende de I-06.
