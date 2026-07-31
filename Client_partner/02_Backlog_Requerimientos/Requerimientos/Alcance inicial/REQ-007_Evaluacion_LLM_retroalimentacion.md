# REQ-007 · Evaluación de ideas con LLM y retroalimentación

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT (alcance base MVP) · 2026-06-12 |
| **Estimación** | Alcance base del MVP (no facturable por T&M) |

## Qué se necesita
Evaluar la versión confirmada de la idea con el LLM y la rúbrica configurada, devolviendo calificación, explicación y una retroalimentación corta con un máximo de una repregunta.

## Alcance
- Incluye: construcción de contexto, llamada al proveedor, salida JSON validada, fallback, control del máximo de repreguntas, snapshots reproducibles (prompt + rúbrica + config).
- No incluye: publicación automática; la idea madura queda pendiente de curaduría humana.

## Criterios de aceptación
- [x] Solo la versión consolidada confirmada se evalúa con el LLM y la rúbrica.
- [x] El participante recibe retro corta y útil; una corrección no se evalúa hasta confirmarse.
- [x] Se respeta el máximo configurado de repreguntas y se guardan los snapshots usados.

## Aprobación
Entregado como parte del MVP base · Ref. spec: `Especificaciones/base/08`, `05 §4`
