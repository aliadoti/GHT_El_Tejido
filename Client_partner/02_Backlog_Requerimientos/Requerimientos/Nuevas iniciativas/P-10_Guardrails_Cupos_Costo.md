# P-10 · Guardrails: cupos y costo

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | En curso |
| **Solicitado por / Fecha** | GHT · — |
| **Estimación** | Por estimar (T&M) |

## Qué se necesita
Salvaguardas de abuso y costo: cupos por usuario, rate por número y control de costo LLM por campaña.

## Alcance
- Incluye: cupos por usuario; rate por número; presupuesto/alerta de costo LLM por campaña
- No incluye: Los valores son por campaña; los kill-switch son globales. Portal de configuración pendiente.

## Criterios de aceptación
- [ ] El sistema aplica cupos y rate configurados.
- [ ] El costo LLM por campaña tiene alerta/presupuesto.

## Aprobación / Referencia
Estado de desarrollo: Backend HECHO 2026-07-14; portal pendiente
Spec detallada: `Especificaciones/Iniciativas/P-10_Guardrails_Cupos_Costo.md`
