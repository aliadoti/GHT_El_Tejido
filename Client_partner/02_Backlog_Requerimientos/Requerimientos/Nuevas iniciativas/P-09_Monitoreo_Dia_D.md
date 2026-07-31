# P-09 · Monitoreo del día D

| | |
|---|---|
| **Tipo** | Cambio |
| **Prioridad** | Baja |
| **Estado** | En curso |
| **Solicitado por / Fecha** | GHT · — |
| **Estimación** | Por estimar (T&M) |

## Qué se necesita
Observabilidad para el go-live: health-check, logs de entrega, acta de flags y runbook de rollback (el panel en vivo queda diferido).

## Alcance
- Incluye: /health(/ready); logs de entrega; acta de flags del día-D; runbook de rollback
- No incluye: El panel de monitoreo en vivo y las métricas de tokens quedan diferidos.

## Criterios de aceptación
- [ ] El go-live cuenta con health-check y runbook de rollback.
- [ ] Existe acta de flags para el día-D.

## Aprobación / Referencia
Estado de desarrollo: Panel DIFERIDO; se conservan health-check + logs + acta de flags + runbook
Spec detallada: `Especificaciones/Iniciativas/P-09_Monitoreo_Dia_D.md`
