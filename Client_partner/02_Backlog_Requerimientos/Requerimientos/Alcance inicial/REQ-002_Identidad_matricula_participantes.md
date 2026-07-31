# REQ-002 · Identidad y matrícula de participantes

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT (alcance base MVP) · 2026-06-12 |
| **Estimación** | Alcance base del MVP (no facturable por T&M) |

## Qué se necesita
Reconocer a cada participante por su número de WhatsApp y validar que está matriculado y activo en una campaña antes de continuar.

## Alcance
- Incluye: resolución por número normalizado, asociación a área/empresa/tags, rechazo neutral a no matriculados.
- No incluye: auto-registro del participante.

## Criterios de aceptación
- [x] Al responder, el sistema reconoce al participante por su número normalizado.
- [x] Un no matriculado recibe mensaje neutral de no-acceso.
- [x] Un participante activo y asociado continúa el flujo.

## Aprobación
Entregado como parte del MVP base · Ref. spec: `Especificaciones/base/06 §3`
