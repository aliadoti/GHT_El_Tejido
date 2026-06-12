# AVANCES - El Tejido MVP

> Documento de traspaso de contexto entre sistemas (Codex / Claude Code / opencode).
> Es la fuente del estado real del desarrollo y debe coincidir con el codigo.

## Estado global
- Fase actual: **Fase 1 - Dominio y persistencia (iniciada)**
- Ultima actualizacion: 2026-06-12T19:23:13Z por Codex
- Repo compilable y en verde: **si** (backend build/test/format verificados; frontend sin cambios desde Fase 0)
- Branch de trabajo: **main**

## Proximo paso (lo primero que debe hacer quien retome)
- [ ] Continuar Fase 1 con entidad/puerto inicial del contenedor `campaigns` para `Campania` con mensajes iniciales y preguntas embebidos segun `03_Modelo_de_Datos_Cosmos.md` seccion 3.3 y `07_Backend_Campanas_y_Configuracion.md` seccion 2.
- Como continuar: leer `03` secciones 2 y 3.3, `07` secciones 2.1-2.5; modelar dominio puro de `Campania` y un puerto pequeno en `Application` para guardar/consultar por id/listar por estado. Ejecutar `dotnet build -c Release -warnaserror`, `dotnet test -c Release` y `dotnet format --verify-no-changes`.

## Tablero por fases
| Fase | Paso | Estado | Commit | Pruebas | Notas |
|---|---|---|---|---|---|
| 0 | Scaffolding solucion y proyectos | DONE | pendiente: sin .git | verde | `ElTejido.sln`, `src/*`, `tests/*` |
| 0 | global.json / Directory.Build.props / .editorconfig | DONE | pendiente: sin .git | verde | SDK .NET 8, Nullable on, warnings-as-errors |
| 0 | Proyectos de prueba (Unit/Integration) | DONE | pendiente: sin .git | verde | xUnit + FluentAssertions + NSubstitute; WebApplicationFactory |
| 0 | Endpoint /health + prueba | DONE | pendiente: sin .git | verde | `GET /health` devuelve `{ "status": "ok" }` (`04` seccion 7) |
| 0 | Workflow CI | DONE | pendiente: sin .git | no ejecutado en GitHub | `.github/workflows/ci.yml` con backend + frontend |
| 1 | Normalizacion E.164 centralizada | DONE | pendiente: sin .git | verde | `NumeroWhatsApp`, `INormalizadorNumero`, `NormalizadorNumero`; REQ 10.2, 12.2.2 / ARQ 16 |
| 1 | Entidades Usuario y Tag | DONE | pendiente: sin .git | verde | `Usuario`, `Tag`, `RolUsuario`, `EstadoRegistro`; REQ 12, 13 |
| 1 | Puerto `users` para Usuario/Tag | DONE | pendiente | verde | `IRepositorioUsuarios`, filtros `FiltroUsuarios`/`FiltroTags`; REQ 12, 13, 26.3 / ARQ 8-9 |
| 1 | Entidad y puerto `campaigns` | TODO | - | - | `Campania` con mensajes/preguntas embebidos; `03` seccion 3.3 |
| 1 | Implementacion Cosmos inicial | TODO | - | - | `Infrastructure`; con emulador/mock en pruebas |
| 1 | Idempotencia WebhookDedupe/leases | TODO | - | - | `03` secciones 3.16 y 4 |
| 2 | Contratos API + seguridad transversal | TODO | - | - | 04, 10 |
| 3 | Identidad y Auth | TODO | - | - | 06 |
| 4 | Configuracion | TODO | - | - | 07 |
| 5 | WhatsApp Gateway + Orquestador | TODO | - | - | 05 |
| 6 | Evaluacion LLM | TODO | - | - | 08 |
| 7 | Markdown | TODO | - | - | 09 |
| 8 | Portal Angular | TODO | - | - | 11 |
| 9 | Integracion E2E + endurecimiento | TODO | - | - | 13 |

## Decisiones tomadas (con porque)
- 2026-06-12 - Arquitecto - Frontend en Angular 22 en vez de React (decision del cliente). Ref: `02` secciones 2-3. Sin impacto en backend.
- 2026-06-12 - Arquitecto - LLM configurable con Azure OpenAI como opcion por defecto; el codigo no asume un proveedor unico. Ref: `02` seccion 2, `08`.
- 2026-06-12 - SDET/Frontend - `npm run lint` usa Prettier check en Fase 0 porque Angular CLI 22 no genero target ESLint por defecto. Ref: `SUPUESTOS.md#fase0-frontend-lint`.
- 2026-06-12 - Backend/AppSec - Normalizacion E.164 centralizada como dominio puro; validacion plausible por longitud 8-15, solo digitos ASCII y primer digito distinto de 0. Ref: `SUPUESTOS.md#fase1-normalizacion-e164`.
- 2026-06-12 - Backend - `Usuario` y `Tag` se modelaron como dominio puro sin atributos Cosmos/API; el mapeo JSON queda para infraestructura para no acoplar persistencia al dominio. Ref: `03` secciones 3.1-3.2.
- 2026-06-12 - Arquitecto/Backend - Los puertos de persistencia se ubican en `ElTejido.Application` e `Infrastructure` los implementara; `Domain` permanece libre de I/O. Ref: `SUPUESTOS.md#fase1-puertos-persistencia-application`.

## Contratos: cambios respecto a las specs
- Ninguno.

## Como construir y probar (comandos verificados)
- Backend:
  - `dotnet build -c Release -warnaserror`
  - `dotnet test -c Release`
  - `dotnet format --verify-no-changes`
- Frontend:
  - Requisito: Node `22.22.3+`, `24.15.0+` o `26+` para Angular CLI 22. La maquina local tiene Node `22.17.0`, por eso se verifico con Node temporal en Fase 0.
  - `cd src/ElTejido.Web`
  - `npx -y -p node@24.15.0 npm run lint`
  - `npx -y -p node@24.15.0 npm run test -- --watch=false`
  - `npx -y -p node@24.15.0 node node_modules/@angular/cli/bin/ng build --configuration production`
- Local:
  - Cosmos emulator, Azurite, `dotnet user-secrets`, Key Vault y proxy Angular quedan pendientes para las fases que consuman infraestructura real.

## Deuda tecnica / pendientes conocidos
- Existen cambios no relacionados en el working tree (`.obsidian/` y `Propuesta_Comercial/`) que no pertenecen a Fase 1 y no se tocaron.
- El Node global local (`v22.17.0`) no cumple el minimo de Angular CLI 22 (`22.22.3+`). Impacto: usar Node temporal o actualizar Node antes de correr `npm run build` directamente.
- El lint frontend inicial es Prettier check; agregar ESLint cuando entren reglas/componentes reales del portal.

## Riesgos / bloqueos
- Los flujos E2E reales (Fase 9) requieren recursos Azure y plantillas WhatsApp aprobadas. El desarrollo y CI pueden avanzar con mocks/emuladores.

## Log cronologico (append-only)
- 2026-06-12 - (semilla) - Creados `AVANCES.md` y `SUPUESTOS.md`. Aun sin codigo. Proximo: Fase 0.
- 2026-06-12T18:07:16Z - Codex - Ejecutada Fase 0: solucion .NET 8, proyectos backend, proyectos de prueba, endpoint `/health`, scaffold Angular 22 y workflow CI. Build/test/format backend y lint/test/build frontend verificados. Commit pendiente por ausencia de `.git`.
- 2026-06-12T18:56:38Z - Codex - Iniciada Fase 1 con normalizacion E.164 centralizada en dominio (`NumeroWhatsApp`, `INormalizadorNumero`, `NormalizadorNumero`) y pruebas unitarias. Backend build/test/format verde. Commit omitido por ausencia de `.git` y decision del usuario.
- 2026-06-12T18:59:38Z - Codex - Agregadas entidades de dominio `Usuario` y `Tag` con validaciones, roles/estados y pruebas unitarias. Backend build/test/format verde. Commit omitido por ausencia de `.git` y decision del usuario.
- 2026-06-12T19:23:13Z - Codex - Agregado puerto `IRepositorioUsuarios` en Application para el contenedor `users`, filtros normalizados para Usuario/Tag y pruebas unitarias. Backend build/test/format verde. Commit pendiente.
