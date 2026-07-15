# D5 — Banco de calibración de prompts (regresión de comportamiento)

> **Origen:** decisión **D5** del plan de riesgos (`Presentacion/20260711_Plan_Desarrollo_Mitigacion_Riesgos.md`,
> mitiga RL-5/RL-7). **Tipo:** Tooling/QA (SDET + LLM) · **Prioridad:** Alta — **habilita tocar
> prompts con evidencia** (I-03/I-04/I-05) y fija el umbral de I-01 · **Ventana:** Sprint 1a ·
> **Dependencia:** `ILlmClient`/`IEvaluadorLlm` (ya existen) · **Riesgo:** costo LLM (corre contra el
> proveedor real). Cubre la regla transversal del equipo: "nada es *hecho* sin banco de calibración o
> suite de regresión en verde". Cubre REQ §20 (evaluación), ARQ §12/§13.

## 1. Qué pide / por qué
Antes de modificar prompts/umbral/rúbrica, necesitamos un **golden set** y un **harness** que conviertan
la calibración de una impresión subjetiva en **datos comparables versión-a-versión**. Sin él, cada
ajuste de I-03/I-04/I-05 y la fijación del umbral de I-01 se harían a ciegas.

## 2. Estado actual del build
Nuevo. Ya existen las piezas que consume: `IEvaluadorLlm.EvaluarAsync(ContextoEvaluacion)` (08),
`ILlmClient` (con `LlmRespuesta.Uso` de tokens tras P-10), snapshots de rúbrica/prompt/ConfigLLM y el
modelo de salida `08 §4`. **No hay** golden set ni runner de regresión.

## 3. Diseño técnico
### 3.1 Golden set (dato versionado)
- 20–30 entradas representativas en un archivo versionado (p. ej. `tests/Calibracion/golden-set.json`),
  cada una: `{ id, categoria, textoRespuesta, esperado: { ejeDebil?, decision (cerrar|repreguntar)?, ideasEsperadas?, esHostil }, notas }`.
- Mezcla **reales** (respuestas de las demos, anonimizadas, **sin PII**) + **sintéticas** que cubran los
  casos límite: **multi-idea**, **texto muy corto**, **texto hostil con instrucciones embebidas**
  (prompt-injection), **"no quiero seguir"**, respuesta fuerte y respuesta vacía/ruido.
- El set es **dato**, no lógica: se amplía sin tocar el harness.

### 3.2 Harness de calibración (runner)
- Corre el set **N veces** (config `N`, default 1) contra el **`ILlmClient` real** con una tripleta fija
  (rúbrica+prompt+ConfigLLM versionados, tomada de una campaña de staging) y agrega por eje/criterio:
  - **distribución de scores** por criterio y total (min/max/media/desv),
  - conteo **cerrar vs repreguntar**,
  - **% de salida inválida** (tasa de fallback `salida_invalida:*`, con la razón),
  - **ideas/temas/entidades detectadas** por entrada,
  - **tokens** por entrada y total (usando `LlmRespuesta.Uso`/`Evaluacion.usoTokens` de P-10) → costo del corrido.
- Emite un **reporte** determinista (JSON + Markdown legible) con `campaniaId`/rúbrica/prompt/versión y timestamp; **sin secretos ni PII**.

### 3.3 Regresión (árbitro)
- Se **congela un baseline** (reporte de referencia versionado). Un corrido nuevo **compara contra el
  baseline** y marca deltas que excedan tolerancias configurables (p. ej. Δmedia de score por eje,
  Δ% inválido, cambios de decisión) → salida no-cero / lista de regresiones. Así, toda versión de
  prompt/umbral se valida contra la anterior con el banco como árbitro (T-47).

### 3.4 Aislamiento y costo (crítico)
- El harness **llama al LLM real y cuesta dinero**: NO corre en el `dotnet test` por defecto ni en CI.
  Gatéalo de forma explícita (proyecto de herramienta `tools/CalibracionBanco` **o** test con
  `[Trait("Category","Calibracion")]` excluido por defecto + variable de entorno con la config del
  proveedor de staging). Documenta cómo dispararlo. La API key sigue solo en Key Vault / user-secrets.

## 4. Contratos y configuración
- **Sin cambio de contratos `03`/`04`/`08 §4`.** Reusa `IEvaluadorLlm`/`ILlmClient`/`ContextoEvaluacion`
  y el uso de tokens de P-10. Config nueva solo del harness (N, tolerancias, ruta de salida, ref de la
  tripleta de staging), fuera de los contratos compartidos.

## 5. Riesgos y mitigación
- *Costo/latencia del LLM real* → set acotado (20–30), N configurable, harness opt-in fuera de CI, costo reportado.
- *No determinismo del modelo* → correr N veces y reportar **distribución** (no un solo valor); el baseline es la referencia.
- *PII en el golden set* → solo respuestas anonimizadas + sintéticas; revisión antes de versionar.

## 6. Criterios de aceptación / pruebas
- Golden set versionado (≥20 entradas con los casos límite del §3.1) **sin PII**.
- Harness corre el set contra el `ILlmClient` real (staging), produce reporte con score por eje,
  cerrar/repreguntar, % inválido, ideas y **tokens/costo**; **no** corre en el `dotnet test` por defecto.
- Baseline congelado + un segundo corrido que compara y marca regresiones sobre tolerancias.
- Pruebas unitarias del **agregador/comparador** con entradas mockeadas (sin LLM real): agregación,
  cálculo de % inválido y detección de regresión con datos deterministas → verdes en CI.
- Doc de cómo disparar el banco contra staging (runbook corto) en `AVANCES.md`/README del tooling.

## 7. Secuencia de implementación sugerida (pasos pequeños)
1. Esquema del golden set + 5–8 entradas semilla (incluye 1 hostil y 1 multi-idea) + carga tipada.
2. Agregador/reporte (JSON+MD) con `IEvaluadorLlm` **mockeado** + unit del agregador (verde en CI).
3. Comparador contra baseline + unit de detección de regresión (verde en CI).
4. Runner opt-in contra el `ILlmClient` real (gateado, fuera de CI) + doc de disparo.
5. Completar el set a 20–30, congelar baseline, registrar en `AVANCES.md`/`SUPUESTOS.md`.
