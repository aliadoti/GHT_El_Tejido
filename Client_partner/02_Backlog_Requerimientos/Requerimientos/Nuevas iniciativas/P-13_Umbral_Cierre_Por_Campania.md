# P-13 · Umbral de cierre por campaña

| | |
|---|---|
| **Tipo** | Cambio |
| **Prioridad** | Alta |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT · — |
| **Estimación** | Por estimar (T&M) |

## Qué se necesita
Permitir configurar el umbral de cierre anticipado por campaña, con un default global y un kill-switch.

## Alcance
- Incluye: override `umbralCierreAnticipado` por campaña; default numérico global heredable; kill-switch booleano global
- No incluye: Reversible por campaña; la calibración del valor es actividad operativa (I-01).

## Criterios de aceptación
- [x] Cada campaña puede fijar su umbral de cierre.
- [x] Sin override, hereda el default global.

## Aprobación / Referencia
Estado de desarrollo: DONE local 2026-07-21; D5 + calibración I-01 pendientes
Spec detallada: `Especificaciones/Iniciativas/P-13_Umbral_Cierre_Por_Campania.md`
