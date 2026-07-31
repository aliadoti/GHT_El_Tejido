# REQ-010 · Seguridad, guardrails, trazabilidad y observabilidad

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT (alcance base MVP) · 2026-06-12 |
| **Estimación** | Alcance base del MVP (no facturable por T&M) |

## Qué se necesita
Controles transversales de seguridad y observabilidad que protegen el sistema y garantizan trazabilidad completa de la operación.

## Alcance
- Incluye: rate limiting y límites de consumo, anti prompt-injection, verificación de firma del webhook e idempotencia, cupos móviles de 24h, no fuga de secretos en logs/telemetría/Markdown, logging y telemetría (Application Insights), snapshots reproducibles.
- No incluye: integración con SIEM externo.

## Criterios de aceptación
- [x] Aplica límites de seguridad (longitud, cupos, rate limit, intentos).
- [x] Verifica la firma del webhook y es idempotente ante reintentos de Meta.
- [x] No filtra secretos; auth neutral; anti prompt-injection efectivo; trazabilidad completa con snapshots.

## Aprobación
Entregado como parte del MVP base · Ref. spec: `Especificaciones/base/10`
