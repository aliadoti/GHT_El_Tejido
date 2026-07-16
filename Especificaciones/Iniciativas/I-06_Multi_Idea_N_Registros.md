# I-06 — Detección de múltiples ideas → N registros

> **Origen:** hoja `Iniciativas` de `Presentacion/Plan_Trabajo_El_Tejido.xlsx`.
> **Tipo:** Desarrollo · **Prioridad:** Alta (gran apuesta, ruta crítica) · **Ventana:** Sprint 1a
> diseño / Sprint 1b implementación · **Dependencia:** `P-10` para cupos/costo y `D5` para calibración.
> **Riesgo:** Alto (no determinismo RL-2).
> Cubre REQ §9/§22/§25, ARQ §4.2/§6/§7/§12/§13; specs base `03 §3.3/§3.8`,
> `04 §5.3/§5.8`, `05 §4`, `08`, `09`, `10`.
> **Estado:** implementación local terminada 2026-07-15; flags apagados. Falta validación operativa en staging (D5 real, UAT y medición de costo) antes de activarla.

## 1. Qué pide GHT / por qué

Detectar **varias ideas** en una misma respuesta, separarlas en **registros distintos** (BD +
Markdown) y recorrer la rúbrica **por idea**. Hoy `Respuesta` guarda 1 idea por registro y el
orquestador evalúa una sola vez por mensaje entrante.

El diseño de Sprint 1a se implementó en Sprint 1b sin reabrir decisiones de arquitectura. La capacidad
permanece apagada por defecto y su activación exige la validación operativa indicada en los criterios de salida.

## 2. Estado actual del build

- `OrquestadorConversacion` (`src/ElTejido.Application/Conversacion/OrquestadorConversacion.cs`) usa
  `ISegmentadorIdeas` antes de evaluar cuando ambos flags están activos; procesa y compila cada idea
  válida de forma independiente y responde una sola vez al participante.
- `Respuesta` tiene `ideaIndice` y `respuestaPadreId` aditivos; para multi-idea se generan ids
  determinísticos `resp_<padre>_<indice>`.
- `Campania.ConfigConversacional.segmentacionIdeas` existe, persiste en Cosmos, se expone por API admin
  y se configura en el portal; su default es `false`.
- P-10 ya entrega cupos por usuario/campaña y presupuesto de tokens; D5 ya tiene golden set con casos
  multi-idea, pero el baseline real contra staging sigue pendiente operativo.

## 3. Decisiones de diseño cerradas

1. **Flag por campaña + kill-switch global.** La decisión de parametrización por campaña del
   2026-07-13 (`00_Indice §4.2`) aplica aquí: el comportamiento del coach vive en campaña y nace
   apagado. Campo aditivo: `Campania.configConversacional.segmentacionIdeas` (`false` por defecto).
   Kill-switch global: `Conversacion:SegmentacionIdeas` (`true` por defecto = respeta la campaña;
   `false` apaga todo sin tocar BD).
2. **Segmentador antes de evaluar.** Si ambos flags permiten segmentar, el orquestador llama un puerto
   `ISegmentadorIdeas` después de persistir el `Mensaje(in)` y antes de construir `ContextoEvaluacion`.
3. **Una idea = una respuesta/evaluación/Markdown.** Cada segmento validado produce su propia
   `Respuesta`, su propia `Evaluacion` y su propio `ArtefactoMarkdown`. El mensaje original sigue
   guardado como `Mensaje` del hilo.
4. **Fallback sin regresión.** Segmentador caído, salida inválida, 0 ideas válidas o flag apagado
   significa **1 idea = mensaje completo**, exactamente el flujo actual.
5. **Sin transacción distribuida.** La consistencia se protege con ids deterministas por idea y Markdown
   regenerable (`REQ §22.4.6`), no con infraestructura nueva.

## 4. Contratos aditivos

### 4.1 `03 §3.3` — Campaña

Agregar `segmentacionIdeas` dentro de `configConversacional`:

```json
"configConversacional": {
  "maxRepreguntas": 1,
  "mensajeCierre": "Gracias. Tu aporte quedó registrado correctamente.",
  "segmentacionIdeas": false
}
```

Regla de compatibilidad: documento viejo o campo ausente = `false`.

### 4.2 `03 §3.8` — Respuesta

Agregar campos opcionales:

```json
{
  "id": "resp_wamidabc_1",
  "texto": "Primera idea segmentada...",
  "ideaIndice": 1,
  "respuestaPadreId": "wamid.abc"
}
```

- `ideaIndice`: índice 1-based dentro del mensaje original. Ausente/null = respuesta histórica de una
  sola idea.
- `respuestaPadreId`: id lógico del mensaje origen; preferir `whatsappMessageId`. Si el canal no lo
  trae, usar el `Mensaje.id` persistido. Ausente/null = respuesta histórica de una sola idea.
- `id`: para multi-idea debe ser determinístico: `resp_<respuestaPadreIdNormalizado>_<ideaIndice>`.
  En modo 1-idea actual puede conservarse el id vigente hasta que la implementación lo endurezca.

### 4.3 `04 §5.3` y `04 §5.8` — API admin

- `GET/POST/PUT /api/admin/campanias*` exponen/aceptan `configConversacional.segmentacionIdeas` de forma
  aditiva. Campo ausente en request = `false` o conserva valor existente, según la operación actual.
- `GET /api/admin/respuestas` y `GET /api/admin/respuestas/{id}` devuelven `ideaIndice` y
  `respuestaPadreId` cuando existan.
- `GET /api/admin/markdown*` no cambia de ruta: cada Markdown de idea sigue siendo un artefacto tipo
  `respuesta`, con `respuestaRef` distinto.

### 4.4 `08 §4` — Salida del evaluador LLM

No cambia. La segmentación es un paso previo y separado; cada idea se evalúa con el contrato existente
de evaluación. Esto reduce blast radius y mantiene D5 como árbitro de prompts/evaluación.

## 5. Puerto y salida del segmentador

Ubicación propuesta: `Application/Evaluacion` si se considera parte del pipeline LLM, con wiring en
`Infrastructure/Llm`; el orquestador lo consume por interfaz pública, igual que `IEvaluadorLlm`.

```csharp
public interface ISegmentadorIdeas
{
    Task<ResultadoSegmentacionIdeas> SegmentarAsync(ContextoSegmentacionIdeas contexto, CancellationToken ct);
}

public sealed record ContextoSegmentacionIdeas(
    string CampaniaId,
    string PreguntaId,
    string Texto,
    IReadOnlyList<string> HistorialReciente);

public sealed record IdeaSegmentada(int Indice, string Texto, string? Resumen);
```

Contrato JSON solicitado al LLM:

```json
{
  "ideas": [
    { "texto": "string", "resumen": "string | null" }
  ]
}
```

Validaciones deterministas:

- `ideas` debe existir y ser arreglo.
- Cada `texto` debe estar no vacío tras trim.
- Se descartan segmentos con menos de `Conversacion:LongitudMinimaIdea` caracteres (default 30).
- Se limita a `Conversacion:MaxIdeasPorMensaje` ideas (default 5). Excedentes se ignoran y se registra
  `LogSeguridad(anomaliaLlm, "segmentacion_excede_maximo")`.
- Textos duplicados normalizados se colapsan conservando el primero.
- Si tras validar quedan 0 ideas, fallback a mensaje completo.

## 6. Flujo del orquestador

Dentro de `ProcesarMensajeEntranteAsync`, solo en el camino que hoy evalúa:

1. Resolver hilo, cupos y salidas anticipadas como hoy.
2. Persistir `Mensaje(in)` y marcar participante respondió como hoy.
3. Si `configConversacional.segmentacionIdeas != true` o `Conversacion:SegmentacionIdeas=false`:
   ejecutar flujo actual 1-idea.
4. Si multi-idea está activo:
   - llamar `ISegmentadorIdeas`;
   - validar/recortar ideas;
   - por cada idea, crear `Respuesta` con `ideaIndice` y `respuestaPadreId`;
   - construir `ContextoEvaluacion` con `RespuestaTexto = idea.Texto`;
   - llamar `IEvaluadorLlm`, persistir `Evaluacion` y compilar Markdown si no hay fallback.
5. Decisión conversacional:
   - para Sprint 1b, responder al participante **un único mensaje agregado** por el turno, no N mensajes
     separados. Debe ser breve y operacional: confirma que se registraron N ideas y, si aplica,
     incluye una invitación de mejora general. El detalle por idea queda en Resultados/Markdown.
   - `MaxRepreguntas` sigue siendo por hilo, no por idea, para preservar terminación.

La implementación debe extraer un helper interno tipo `ProcesarIdeaAsync(...)` para evitar duplicar el
camino actual de evaluación/Markdown; no introducir microservicios ni colas nuevas.

## 7. Idempotencia y estado parcial

- El webhook sigue deduplicando por `whatsappMessageId` en `leases`.
- Los ids de `Respuesta` multi-idea son determinísticos (`respuestaPadreId + ideaIndice`) y los repos
  usan upsert, así que un reintento no duplica.
- Si el proceso cae tras escribir algunas ideas, una re-ejecución completa las faltantes o actualiza las
  ya escritas con el mismo id.
- Markdown es regenerable desde datos operativos; un fallo de compilación no debe abortar el hilo.

## 8. Cupos, costo y observabilidad

- La llamada de segmentación **también cuenta como llamada LLM** para efectos operativos, aunque no cree
  `Evaluacion`. La implementación emite `LogSeguridad(segmentacionIdeas)` con cantidades, fallback,
  truncamiento y tokens, sin texto/PII. El contador persistente de P-10 sigue derivando de
  `Evaluacion`; antes de activar en staging, operaciones debe dimensionar `1 + N` llamadas por turno y
  observar estos eventos. No se agregan documentos contadores nuevos.
- Consumo esperado por turno con multi-idea: `1 segmentación + N evaluaciones`; dimensionar
  `maxLlamadasLlmPorUsuario` y `presupuestoTokensCampania` antes de encender la campaña.
- Métricas mínimas:
  - distribución de `ideasPorMensaje` por campaña;
  - tasa de fallback de segmentación;
  - tasa de truncamiento por `MaxIdeasPorMensaje`;
  - tokens/latencia de segmentación separados de evaluación.
- Logs sin PII: no registrar textos completos de ideas en telemetría técnica.

## 9. Markdown y resultados

- Cada idea genera Markdown independiente con `respuestaRef = resp_<padre>_<indice>`.
- `09 §5` agrega trazabilidad opcional:
  - `Idea índice: {{respuesta.ideaIndice}}`
  - `Respuesta padre: {{respuesta.respuestaPadreId}}`
- El listado de resultados debe poder ordenar por `fecha`, `respuestaPadreId`, `ideaIndice` para que N
  ideas del mismo mensaje aparezcan juntas. Esto es presentación/consulta; no requiere nueva ruta.

## 10. Seguridad y anti prompt-injection

El segmentador usa las mismas defensas de `08`:

- instrucciones en `system`, texto del participante delimitado como dato;
- respuesta JSON estricta;
- sin secretos ni datos innecesarios;
- salida del modelo tratada como dato no confiable;
- fallback al mensaje completo ante cualquier incumplimiento.

El prompt del segmentador debe prohibir inferir datos personales, inventar ideas ausentes o mejorar el
texto; solo separa ideas explícitas. La evaluación sigue aplicando la rúbrica a cada texto segmentado.

## 11. Criterios de aceptación / pruebas Sprint 1b

Verificación local completada: build Release con warnings-as-errors, 291 pruebas unitarias y 51 de
integración, y format limpio. Permanecen pendientes D5 real, UAT y medición de costo en staging bajo
flag antes de encender el feature.

- Unit: campaña con `segmentacionIdeas=false` o documento viejo sin campo → cero llamadas al segmentador
  y mismo comportamiento 1-idea.
- Unit: kill-switch global `Conversacion:SegmentacionIdeas=false` anula una campaña con `true`.
- Unit: mensaje con 2 ideas claras → 2 `Respuesta`, 2 `Evaluacion`, 2 Markdown; `ideaIndice` 1/2 y
  mismo `respuestaPadreId`.
- Unit: segmentador inválido/timeout/0 ideas válidas → fallback 1-idea; se registra anomalía sin PII.
- Unit: ideas menores a `LongitudMinimaIdea` se descartan; si todas caen, fallback 1-idea.
- Unit: >`MaxIdeasPorMensaje` ideas → se procesan las primeras N y se loguea truncamiento.
- Unit/integration: ids determinísticos evitan duplicados si se reejecuta el mismo `whatsappMessageId`.
- Integration: contrato `GET /respuestas` incluye campos nuevos de forma aditiva.
- Markdown: artefacto por idea con trazabilidad de índice/padre.
- Calibración: agregar/correr casos multi-idea del golden set D5 contra staging antes de activar el flag.
- Gates de calidad: `dotnet build -c Release -warnaserror`, `dotnet test -c Release --filter
  "Category!=Calibracion"`, `dotnet format --verify-no-changes`; frontend lint/test/build si se agrega
  checkbox en portal.

## 12. Degradación y rollback

Dos niveles sin redeploy:

1. Por campaña: `configConversacional.segmentacionIdeas=false`.
2. Global: `Conversacion:SegmentacionIdeas=false`.

Ambos devuelven el modo 1-idea ya probado. Si hay dudas de costo/no determinismo en UAT, el acta del
día-D debe dejar multi-idea apagado y conservar el flujo actual.
