# P-15 · Refactor del orquestador conversacional

| | |
|---|---|
| **Tipo** | Cambio |
| **Prioridad** | Media |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT · — |
| **Estimación** | Por estimar (T&M) |

## Qué se necesita
Remediación de calidad (CAL-001): separar políticas, transición y efectos detrás de una interfaz IOrquestadorConversacion.

## Alcance
- Incluye: separación de políticas/transición/efectos; interfaz IOrquestadorConversacion
- No incluye: No amplía el comportamiento de producto; preserva contratos y permisos.

## Criterios de aceptación
- [x] El orquestador queda desacoplado tras la interfaz, con pruebas verdes.

## Aprobación / Referencia
Estado de desarrollo: DONE local 2026-07-24 (remediación auditoría)
Spec detallada: `Especificaciones/Iniciativas/P-15_Refactor_Orquestador_Conversacional.md`
