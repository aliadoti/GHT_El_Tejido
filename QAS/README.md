# QAS — Paquete de QA · El Tejido

Paquete completo de pruebas E2E para validar **El Tejido** antes de producción (**Hito 10-ago-2026**), ejecutable por **1 tester manual**, enfoque **risk-based**.

## Contenido

| # | Documento | Para qué |
|---|---|---|
| 00 | [Plan de Pruebas](00_Plan_de_Pruebas.md) | Alcance, estrategia risk-based, ambientes SIM/REAL, roles (1 persona), cronograma freeze/día-D, criterios entrada/salida, **gestión del no determinismo**. |
| 01 | [Matriz de Trazabilidad](01_Matriz_Trazabilidad.md) | Iniciativa/REQ → caso(s) → prioridad → estado. Cobertura obligatoria de seguridad y guardrails. |
| 02 | [Casos de Prueba E2E](02_Casos_de_Prueba_E2E.md) | **50+ casos** CNV/SEC/AUT/ADM/GRD/ROB/FLG. Cada uno con resultado esperado + criterio tolerante. **CORE vs Ext**. |
| 03 | [Smoke y Checklist Día-D](03_Smoke_y_Checklist_Dia_D.md) | Smoke post-deploy (12 chequeos) + gate go/no-go + **acta de flags**. |
| 04 | [Datos de Prueba y Reinicio](04_Datos_de_Prueba_y_Reinicio.md) | Campañas/rúbrica/prompts/participantes de ejemplo, CSV de carga masiva, y procedimiento **P-03** para reiniciar entre corridas. |
| 05 | [Plantillas Defecto y Bitácora](05_Plantillas_Defecto_y_Bitacora.md) | Reporte de defecto (severidad/prioridad), bitácora de ejecución, resumen de cierre. |
| 06 | [Criterios de Aceptación LLM](06_Criterios_Aceptacion_LLM.md) | Cómo juzgar salidas no deterministas; detección de fuga de rúbrica/PII; qué NO es defecto. |
| 07 | [Runbook Rollback / Contingencia](07_Runbook_Rollback_Contingencia.md) | Síntoma → qué flag/kill-switch apagar en producción, sin hotfix en caliente. |

## Cómo empezar (tester)

1. Lee **00** (plan) y **06** (criterios cualitativos).
2. Carga los datos de **04** y verifica ambiente con el **smoke** de **03 §1**.
3. Ejecuta **02** en orden: primero **CORE en SIM**, registrando en la bitácora de **05**. Reinicia con **P-03** (04 §6) entre corridas.
4. Confirma los casos `Ambiente: real` en WhatsApp real.
5. Llena el **checklist día-D** (03 §2) para el go/no-go. Ten a mano el **runbook 07**.

## Prioridad de un vistazo

- **CORE (bloquea go-live):** todo el flujo del coach (CNV), seguridad/privacidad (SEC), guardrails deterministas (GRD-01..04/06), robustez (ROB-01..07/09), auth (AUT), CRUD/envíos/carga/reinicio (ADM).
- **Extendido:** funciones bajo flag (FLG), filtros/límites secundarios, consultas.

## Riesgo #1

El **no determinismo del LLM** se gestiona en todo el paquete con **criterios cualitativos tolerantes**; lo determinista (cupos, dedupe, firma, umbral, estados) se valida al pie de la letra. Regla: *"el LLM propone, el sistema dispone"*.

## Nota de estado (2026-07-21)

Verificar antes de ejecutar: **P-13** (override de umbral por campaña) estaba en implementación; si no está en el build, calibrar el umbral I-01 por el **global** y marcar el caso GRD-06/FLG-05. Baseline **D5 real** pendiente → la calidad conversacional se arbitra en UAT; la **seguridad** no espera al banco (filtro determinista).
