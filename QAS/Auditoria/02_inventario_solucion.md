# 02 — Inventario de solución

## Solución y proyectos

| Proyecto | Rol | Referencias directas observadas |
|---|---|---|
| `src/ElTejido.Api` | Host ASP.NET Core, APIs, webhook, SPA y composición | Application, Infrastructure |
| `src/ElTejido.Application` | Casos de uso y puertos | Domain |
| `src/ElTejido.Domain` | Entidades, value objects y contratos de dominio | Ninguna |
| `src/ElTejido.Infrastructure` | Adaptadores Cosmos, Blob, Key Vault, LLM, WhatsApp, seguridad y persistencia | Domain, Application |
| `src/ElTejido.Calibracion` | Utilidades de calibración | Application |
| `tests/ElTejido.UnitTests` | Pruebas unitarias | Solución productiva según `ElTejido.sln` |
| `tests/ElTejido.IntegrationTests` | Pruebas de integración | Solución productiva según `ElTejido.sln` |
| `src/ElTejido.Web` | Portal Angular 22 | Consume la API vía servicios core |

## Puntos de entrada y flujos principales

- `src/ElTejido.Api/Program.cs`: composition root, middleware de errores y correlación, HTTPS/HSTS fuera de Development, archivos estáticos, rate limiting, `/health`, readiness, autenticación, APIs administrativas, webhook WhatsApp y fallback SPA.
- API pública/operativa: `/health`, `/webhook/whatsapp`, `/api/auth/*`, `/api/admin/*`; el detalle contractual está en `Especificaciones/base/04_Contrato_API_REST.md`.
- SPA: `src/ElTejido.Web/src/main.ts`, rutas y características en `src/ElTejido.Web/src/app/`.

## Configuración e integraciones

- `appsettings.json` contiene valores no secretos, flags conversacionales, rate limits, nombres de secretos y parámetros de WhatsApp/diagnóstico.
- `appsettings.Development.json` selecciona persistencia en memoria.
- Se detectaron dependencias Azure Identity, Key Vault, Storage Blobs y Cosmos; BCrypt; `Microsoft.Extensions.*`; `Microsoft.IdentityModel.JsonWebTokens`; Newtonsoft.Json.
- Integraciones previstas: Azure Cosmos DB, Blob Storage, Key Vault, Application Insights, Azure App Service, WhatsApp Graph API y LLM configurable (Azure OpenAI, OpenAI-compatible, OpenRouter o Anthropic).

## Estructura relevante

- `Arquitectura/`: arquitectura técnica del MVP.
- `Especificaciones/base/`: contratos y convenciones canónicas 00–13.
- `Especificaciones/Iniciativas/`: iniciativa, estado y backlog.
- `Guias_Implementacion/`: guías de Azure y WhatsApp.
- `QAS/`: plan y casos E2E del producto; `QAS/Audit/` es la fuente del encargo y `QAS/Auditoria/` esta memoria/resultados.
- `.github/workflows/`: CI y CD.

## Automatización y scripts

- CI backend: restore, build Release con warnings como errores, formato y pruebas sin categoría `Calibracion`.
- CI frontend: Node 22, `npm ci`, lint, pruebas y build de producción.
- CD: compila SPA, publica API con SPA incluida y despliega a Azure App Service por OIDC; termina con smoke de `/health`.
- Scripts frontend: `start`, `build`, `lint`, `watch`, `test`.
- No se encontraron `AGENTS.md`, `CLAUDE.md` ni skills locales que definan reglas adicionales de auditoría. Se encontró configuración local en `.claude/`.

## Tamaño aproximado observado

- C# productivo: Api 34, Application 102, Domain 54, Infrastructure 92 y Calibracion 17 archivos.
- Pruebas C#: 72 archivos unitarios y 24 de integración.
- Portal: 28 TypeScript, 2 HTML y 2 SCSS bajo `src/ElTejido.Web/src`.

