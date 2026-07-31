# REQ-011 · Plataforma base: arquitectura .NET 8, datos Cosmos, API REST y CI/CD

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT (alcance base MVP) · 2026-06-12 |
| **Estimación** | Alcance base del MVP (no facturable por T&M) |

## Qué se necesita
Fundaciones técnicas del MVP: monolito modular .NET 8, modelo de datos en Cosmos DB, contrato de API REST y pipelines de CI/CD a Azure.

## Alcance
- Incluye: scaffolding y fronteras de módulos, contenedores/esquemas Cosmos con idempotencia y TTL, endpoints/DTOs/errores del contrato de API, pipelines build/test/deploy con `/health`.
- No incluye: infraestructura como código (Bicep) — fuera del MVP.

## Criterios de aceptación
- [x] Mantiene la separación entre configuración, conversación, evaluación, envío, seguridad, persistencia y Markdown.
- [x] El modelo de datos y el contrato de API son la fuente de verdad de las interfaces entre módulos.
- [x] CI verde en `main` (build + test + lint) y despliegue con `/health` OK.

## Aprobación
Entregado como parte del MVP base · Ref. spec: `Especificaciones/base/02`, `03`, `04`, `12`
