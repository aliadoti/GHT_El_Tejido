# Plan de implementación — DT-P32-04

> **Estado vigente:** 3/3 DONE y congelado condicionadamente para Convención 2026. La instrucción
> histórica de implementar `DT-P32-05`/`DT-QA-03` antes de otra QA simulada no aplica al camino de
> convención, que usa una campaña inmutable y WhatsApp real. Ver
> `../Decision_Congelamiento_Codigo_Convencion_2026.md`.

## Precondición

**Cumplida el 2026-08-16:** DT-P32-03-01 tiene smoke green y DT-I20-02 quedó desplegada con QAS/21
pruebas 1–8 PASS. La decisión humana vigente prioriza este refactor sobre las deudas diferidas de
DT-RUB-01. Antes del primer cambio, capturar una línea base local de pruebas y conservar como evidencia
los reportes P-32 existentes; el refactor no corrige resultados funcionales nuevos.

No implementar en estos cortes selección/versionado de rúbricas, compatibilidad legacy ni validaciones
de asignación de DT-RUB-01.

## Corte 1/3 — idioma central — DONE local 2026-08-16

1. Crear `IdiomaConversacion` con pruebas exhaustivas de normalización, soportados y default histórico.
2. Adaptar una frontera por vez: Usuario, Campania, Conversacion, EnrutamientoAporte y EnvioMensaje.
3. Mantener strings en DTO y Cosmos; agregar pruebas de round-trip histórico.
4. Sustituir listas `es/en` y normalizadores duplicados.

Evidencia: baseline 992 unitarias + 120 integración; cierre 1011 + 120 sin Calibración, build Release
`-warnaserror`, formato y `git diff --check` verdes. Sin cambio de DTO/Cosmos ni operación remota.

## Corte 2/3 — contenido efectivo de campaña — DONE local 2026-08-16

1. Crear el resultado inmutable y el puerto en Application.
2. Cubrir snapshot legacy y localización completa sin mezcla de fuentes.
3. Migrar primero `ServicioEnvios`, luego contexto LLM y finalmente salidas visibles del orquestador.
4. Eliminar helpers privados duplicados solo cuando no queden consumidores.

Evidencia: `ContenidoCampaniaEfectivo`, `ContextoLocalizacion` e `IResolutorContenidoCampania`
registrados en Application/composition root; `ServicioEnvios`, contexto LLM y salidas visibles del
orquestador consumen el snapshot único. Gate OFF conserva legacy; gate ON devuelve
`NoDisponible` tipificado ante cualquier faltante, sin fallback cruzado. Pruebas focales 151/151;
cierre 1018 unitarias + 120 integración sin Calibración, build Release `-warnaserror`, formato y
`git diff --check` verdes. Sin DTO/Cosmos, frontend ni operación remota.

## Corte 3/3 — resolutores y readiness compuesto — DONE local 2026-08-16

1. Encapsular catálogo global existente tras la interfaz acordada, sin reescribir cache/LKG.
2. Encapsular mapeos Meta y directiva LLM.
3. Hacer que readiness consulte los mismos resolutores en modo diagnóstico sin efectos.
4. Añadir prueba arquitectónica contra nuevas resoluciones directas.
5. Actualizar la tabla multidioma y el checklist de tercer idioma.

Evidencia: `IResolutorTextosGlobales` delega el runtime al catálogo existente y diagnostica sin
alterar cache/LKG; `IResolverPlantillaCanal` es el único puente a códigos Meta;
`IPoliticaIdiomaLlm` produce las directivas de evaluación, redacción, clasificación, segmentación y
consolidación. `IReadinessMultiidioma` compone catálogo, contenido de campaña, Meta e idioma LLM con
las mismas políticas. Guardas arquitectónicas impiden reconstrucciones directas. Focales 123/123;
cierre 1030 unitarias + 120 integración sin Calibración, build Release `-warnaserror`, formato y
`git diff --check` verdes. Sin frontend, DTO/Cosmos ni operación remota.

## Verificación por corte

- unitarias focalizadas y round-trip de persistencia;
- integración de campaña/envío/orquestador;
- build Release con warnings como error;
- suite no-Calibración y formato;
- portal solo si cambia la proyección de readiness;
- `git diff --check`.

Históricamente, la corrida controlada posterior se ejecutó contra `28c3cb1` y quedó **BLOCKED — NO
ACTIVAR** (reporte `QAS/resultados/Resultados_P32_Multidioma_2026-08-16_corrida-P32-20260816-1955.md`).
Para una futura QA simulada, primero `DT-P32-05` corrige `DEF-P32-04-01` y luego `DT-QA-03` aporta
salida observable. Para la convención ambos quedan post-evento; D5 y smoke/UAT real permanecen gates.
