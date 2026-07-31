# D5 · Banco de calibración

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT · — |
| **Estimación** | Por estimar (T&M) |

## Qué se necesita
Librería y golden set para calibrar la evaluación LLM y arbitrar decisiones de umbral y follow-ups.

## Alcance
- Incluye: librería de calibración; golden set (24); runner opt-in fuera de CI
- No incluye: El baseline real contra staging es actividad operativa pendiente.

## Criterios de aceptación
- [x] Existe un banco de calibración con golden set y runner.
- [x] Sirve de árbitro para I-03/I-05 y el umbral I-01.

## Aprobación / Referencia
Estado de desarrollo: DONE 2026-07-14; baseline real (corrido pagado) pendiente
Spec detallada: `Especificaciones/Iniciativas/D5_Banco_Calibracion.md`
