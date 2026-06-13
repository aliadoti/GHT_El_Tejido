# AVANCES - El Tejido MVP

> Documento de traspaso de contexto entre sistemas (Codex / Claude Code / opencode).
> Es la fuente del estado real del desarrollo y debe coincidir con el codigo.

## Estado global
- Fase actual: **Fase 1 - Dominio y persistencia (iniciada)**
- Ultima actualizacion: 2026-06-13T03:30:37Z por Codex
- Repo compilable y en verde: **si** (backend build/test/format verificados; frontend sin cambios desde Fase 0)
- Branch de trabajo: **main**

## Proximo paso (lo primero que debe hacer quien retome)
- [ ] Continuar Fase 1 con dominio y puerto del contenedor `participants` para `ParticipanteCampania` y `EnvioMensaje`, consumiendo `03_Modelo_de_Datos_Cosmos.md` secciones 3.4, 3.5, 4 y 5.
- Como continuar: leer `03` secciones 2, 3.4, 3.5, 4 y 5; revisar los patrones de `IRepositorioUsuarios`, `IRepositorioCampanias`, `RepositorioUsuariosCosmos` y `RepositorioCampaniasCosmos`. Crear entidades/puerto primero, cubrir reglas de estado/envio/respuesta e idempotencia de envio saliente. Ejecutar `dotnet build -c Release -warnaserror`, `dotnet test -c Release` y `dotnet format --verify-no-changes`.

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
| 1 | Puerto `users` para Usuario/Tag | DONE | a36bd2f | verde | `IRepositorioUsuarios`, filtros `FiltroUsuarios`/`FiltroTags`; REQ 12, 13, 26.3 / ARQ 8-9 |
| 1 | Entidad y puerto `campaigns` | DONE | 03c9277 | verde | `Campania`, `MensajeInicial`, `Pregunta`, configs embebidas y `IRepositorioCampanias`; REQ 11, 15, 16 / ARQ 8-9 |
| 1 | Implementacion Cosmos inicial | DONE | 93ac9b7 | verde | `RepositorioCampaniasCosmos` para `campaigns`, mapping JSON `Campania`, pruebas con fake container |
| 1 | Idempotencia WebhookDedupe/leases | DONE | 0556c8c | verde | `IRegistroWebhookDedupe`, `RepositorioWebhookDedupeCosmos`, TTL 604800 |
| 1 | Adaptador Cosmos `users` | DONE | 5ff1024 | verde | `RepositorioUsuariosCosmos` para `Usuario`/`Tag`, mapping JSON, particiones `usuario`/`tag`, busqueda por numero y filtros con fake container |
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
- 2026-06-13 - Arquitecto/Backend - `Campania` y sus embebidos se modelaron como dominio puro sin atributos Cosmos/API; el adaptador de `Infrastructure` mapeara nombres JSON y discriminador `type`. Ref: `03` seccion 3.3, `07` seccion 2.
- 2026-06-13 - Backend/Infrastructure - El adaptador Cosmos de `campaigns` usa DTOs internos con `Newtonsoft.Json` y `Microsoft.Azure.Cosmos`; el dominio sigue sin atributos de persistencia. Ref: `03` secciones 2, 3.3 y 5 / ARQ 8-9.
- 2026-06-13 - Arquitecto/Backend - La idempotencia de webhooks se expone como puerto booleano `IRegistroWebhookDedupe`: `true` permite procesar y `false` descarta reintentos por conflicto Cosmos. Ref: `03` secciones 3.16 y 4, `05` seccion 2.4 / ARQ 4.2.
- 2026-06-13 - Backend/Infrastructure - El adaptador Cosmos de `users` usa documentos internos separados para `Usuario` y `Tag`, con `pk` fija `usuario`/`tag`; busqueda por numero normalizado se resuelve por query contra `whatsappNormalizado`. Ref: `03` secciones 2, 3.1, 3.2 y 5 / ARQ 8-9.

## Contratos: cambios respecto a las specs
- Ninguno.

## Como construir y probar (comandos verificados)
- Backend:
  - `dotnet build -c Release -warnaserror`
  - `dotnet test -c Release --no-build`
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
- Existe un cambio no relacionado en el working tree (`.obsidian/workspace.json`) que no pertenece a Fase 1 y no se toco.
- El Node global local (`v22.17.0`) no cumple el minimo de Angular CLI 22 (`22.22.3+`). Impacto: usar Node temporal o actualizar Node antes de correr `npm run build` directamente.
- El lint frontend inicial es Prettier check; agregar ESLint cuando entren reglas/componentes reales del portal.

## Riesgos / bloqueos
- Los flujos E2E reales (Fase 9) requieren recursos Azure y plantillas WhatsApp aprobadas. El desarrollo y CI pueden avanzar con mocks/emuladores.

## Log cronologico (append-only)
- 2026-06-12 - (semilla) - Creados `AVANCES.md` y `SUPUESTOS.md`. Aun sin codigo. Proximo: Fase 0.
- 2026-06-12T18:07:16Z - Codex - Ejecutada Fase 0: solucion .NET 8, proyectos backend, proyectos de prueba, endpoint `/health`, scaffold Angular 22 y workflow CI. Build/test/format backend y lint/test/build frontend verificados. Commit pendiente por ausencia de `.git`.
- 2026-06-12T18:56:38Z - Codex - Iniciada Fase 1 con normalizacion E.164 centralizada en dominio (`NumeroWhatsApp`, `INormalizadorNumero`, `NormalizadorNumero`) y pruebas unitarias. Backend build/test/format verde. Commit omitido por ausencia de `.git` y decision del usuario.
- 2026-06-12T18:59:38Z - Codex - Agregadas entidades de dominio `Usuario` y `Tag` con validaciones, roles/estados y pruebas unitarias. Backend build/test/format verde. Commit omitido por ausencia de `.git` y decision del usuario.
- 2026-06-12T19:23:13Z - Codex - Agregado puerto `IRepositorioUsuarios` en Application para el contenedor `users`, filtros normalizados para Usuario/Tag y pruebas unitarias. Backend build/test/format verde. Commit a36bd2f.
- 2026-06-13T00:23:21Z - Codex - Agregada entidad/puerto inicial del contenedor `campaigns`: dominio puro de `Campania` con mensajes iniciales, preguntas y configs embebidas, `IRepositorioCampanias`, `FiltroCampanias` y pruebas unitarias. Backend build/test/format verde. Commit 03c9277.
- 2026-06-13T02:23:01Z - Codex - Implementado adaptador Cosmos inicial de `campaigns` en Infrastructure (`RepositorioCampaniasCosmos`) con mapping a contrato JSON de `Campania`, paquetes Cosmos/Newtonsoft y pruebas unitarias de repositorio/mapping con fake container. Backend build/test/format verde. Commit 93ac9b7.
- 2026-06-13T02:37:56Z - Codex - Implementada idempotencia `WebhookDedupe`/`leases`: puerto `IRegistroWebhookDedupe`, adaptador Cosmos create-if-not-exists con manejo de conflicto, documento con `ttl` 604800 y pruebas unitarias de nuevo/repetido/validacion. Backend build/test/format verde. Commit 0556c8c.
- 2026-06-13T03:30:37Z - Codex - Implementado adaptador Cosmos de `users`: documentos/mappers `Usuario` y `Tag`, wrapper de contenedor, repositorio `RepositorioUsuariosCosmos`, busqueda por numero normalizado y filtros de usuarios/tags; 7 pruebas unitarias nuevas con fake container. Backend build/test/format verde. Commit 5ff1024.
