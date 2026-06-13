# AVANCES - El Tejido MVP

> Documento de traspaso de contexto entre sistemas (Codex / Claude Code / opencode).
> Es la fuente del estado real del desarrollo y debe coincidir con el codigo.

## Estado global
- Fase actual: **Fase 3 COMPLETA - siguiente: Fase 4 (Configuracion, sin iniciar)**
- Ultima actualizacion: 2026-06-13T00:00:00Z por Claude Code
- Repo compilable y en verde: **si** (backend build/test/format verificados; 121 pruebas en verde: 111 unit + 10 integration; frontend sin cambios desde Fase 0)
- Branch de trabajo: **main**

## Proximo paso (lo primero que debe hacer quien retome)
- [ ] Implementar Fase 4 - Configuracion (`07`): CRUD de usuarios, tags, campanias (+ mensajes y preguntas embebidos), rubricas (versionadas), prompts (versionados + aprobacion), ConfigLLM (API key write-only a Key Vault via `ISecretProvider`). Endpoints `/api/admin/*` (04 §5) con sesion + rol.
- Como continuar: implementar el **enforcement de `/api/admin/*`** (sesion valida + rol `admin` para mutaciones / `admin`|`visor` para GET + `X-CSRF-Token`) reutilizando `IServicioSesion.ValidarAsync` (Fase 3) y el claim `csrf`; consumir los repos `users`/`campaigns` (Fase 1) y `ISecretProvider` para escribir la API key del LLM. Leer `07`, `04 §5`. Ejecutar `dotnet build -c Release -warnaserror`, `dotnet test -c Release --no-build` y `dotnet format --verify-no-changes`.

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
| 1 | Adaptador Cosmos `users` | DONE | 19d4761 | verde | `RepositorioUsuariosCosmos` para `Usuario`/`Tag`, mapping JSON, particiones `usuario`/`tag`, busqueda por numero y filtros con fake container |
| 1 | Dominio + puerto + Cosmos `participants` | DONE | pendiente | verde | `ParticipanteCampania`, `EnvioMensaje`, enums; `IRepositorioParticipantes`, `RepositorioParticipantesCosmos` (pk `campaniaId`); idempotencia de envio saliente (03 §4) |
| 1 | Dominio + puertos + Cosmos `security` | DONE | pendiente | verde | `CodigoAuthAdmin` (TTL), `LogSeguridad` (append-only), `TipoEventoSeguridad`; `IRepositorioCodigosAuth`, `IRepositorioLogSeguridad`, repos Cosmos (pk = `pk`) |
| 2 | Modelo de errores uniforme (04 §3) | DONE | pendiente | verde | `ExcepcionAplicacion`+tipadas en `Application/Common`; `MiddlewareManejoErrores`, `MapeadorErrores`, `EscritorRespuestaError`; mapea `DomainValidationException`->400 y no controladas->500 sin filtrar |
| 2 | CorrelationId (04 §8, 10 §6.2) | DONE | pendiente | verde | `MiddlewareCorrelationId` lee/genera `corr_<guid>`, scope de logging, header de respuesta y cuerpo de error |
| 2 | HTTPS/HSTS (10 §3) | DONE | pendiente | verde | `UseHsts`+`UseHttpsRedirection` solo fuera de Development; `/health` sigue verde en pruebas |
| 2 | Rate limiting (10 §2, §3) | DONE | pendiente | verde | `AddRateLimiter` con politicas `publico`/`webhook`/`demo` por IP, por endpoint; 429 + `Retry-After` con modelo uniforme |
| 2 | Logging estructurado (10 §6.3) | DONE | pendiente | verde | `ILogger` con propiedades, niveles Information/Warning/Error, correlationId en scope; sin secretos/PII |
| 2 | Acceso a secretos (10 §4) | DONE | pendiente | verde | Puerto `ISecretProvider`+`NombresSecretos`; `KeyVaultSecretProvider` (DefaultAzureCredential), `ConfiguracionSecretProvider` (user-secrets), decorador `SecretProviderConCache` (IMemoryCache, 5 min); seleccion por `KeyVault:Uri` |
| 2 | Composition root + Cosmos guardado | DONE | pendiente | verde | `AgregarSeguridad`/`AgregarInfraestructura` en Infrastructure; Cosmos solo si hay `Cosmos:AccountEndpoint`; `Program.cs` cablea middleware/servicios |
| 3 | Identidad/matricula (`IResolutorParticipante`) | DONE | pendiente | verde | `ResolutorParticipante` (numero->participante, rechazo neutral + LogSeguridad); pregunta vigente = primera activa por orden (MVP). 06 §3 |
| 3 | OTP request/verify (bcrypt + sesion JWT) | DONE | pendiente | verde | `AuthAdminService`; `HasherOtpBcrypt` (pepper `otp-salt`), `GeneradorCodigoOtpCsprng`, `LimitadorOtpMemoria`, `ServicioSesionJwt` (HS256 `jwt-sign`), `NotificadorOtpLog` (seam Fase 5). 06 §4, 10 §5 |
| 3 | Endpoints `/api/auth/*` + cookie sesion | DONE | pendiente | verde | `request-code`/`verify-code`/`logout`/`me`; respuestas neutrales; cookie `httpOnly/Secure/SameSite=Strict`; rate limit `publico`. 04 §4 |
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
- 2026-06-13 - Arquitecto/Backend (Claude Code) - Cerrada Fase 1 implementando solo los contenedores `participants` y `security` (decision del usuario); `conversations`, `responses` y `config` se construiran con sus modulos duenos (Fases 5/6/4). `EnvioMensaje` y `LogSeguridad` se modelan append-only via `CreateItemAsync`; `ParticipanteCampania` y `CodigoAuthAdmin` via upsert. Ref: `03` secciones 3.4, 3.5, 3.14, 3.15, 4 / ARQ 8-9, 13.
- 2026-06-13 - Arquitecto/Backend (Claude Code) - Fase 2: excepciones de aplicacion tipadas en `Application/Common` (`ExcepcionAplicacion` base + `ErrorValidacion/.../ErrorUpstream`) que transportan codigo+estado HTTP; el Edge las traduce con `MapeadorErrores` al modelo `04 §3`. `INTERNAL_ERROR` (500) no tiene tipo: es el fallback de no controladas, sin filtrar mensaje. Ref: `04 §3`, `10 §6.3`.
- 2026-06-13 - Backend/Edge (Claude Code) - Fase 2: correlationId via `MiddlewareCorrelationId` (lee `X-Correlation-Id` o genera `corr_<guid>`), guardado en `HttpContext.Items`, scope de logging y header de respuesta; un unico `EscritorRespuestaError` serializa el cuerpo de error tanto desde el middleware como desde el rechazo del rate limiter. Ref: `04 §8`, `10 §6.2`, `SUPUESTOS.md#fase2-seguridad-transversal`.
- 2026-06-13 - AppSec (Claude Code) - Fase 2: rate limiting por endpoint (no global) con politicas `publico`/`webhook` configurables (seccion `Seguridad`) y `demo` para pruebas; HTTPS/HSTS solo fuera de Development para no romper `/health`. Ref: `10 §2, §3`, `04 §8`.
- 2026-06-13 - AppSec (Claude Code) - Fase 2: `ISecretProvider` (puerto en Application) con `KeyVaultSecretProvider` (Managed Identity via `DefaultAzureCredential`), `ConfiguracionSecretProvider` (user-secrets local) y decorador `SecretProviderConCache` (IMemoryCache, expiracion 5 min, nunca a disco); seleccion por presencia de `KeyVault:Uri`. Nombres canonicos en `NombresSecretos` (`llm-key`, `wa-token`, `wa-appsec`, `wa-verify-token`, `jwt-sign`, `otp-salt`). Ref: `10 §4`, `ARQ §10.3`, `SUPUESTOS.md#fase2-seguridad-transversal`.
- 2026-06-13 - Arquitecto/Backend (Claude Code) - Fase 2: registro de Cosmos guardado por `Cosmos:AccountEndpoint` (la app arranca en verde sin emulador; `/health` intacto). `AgregarSeguridad`/`AgregarInfraestructura` en `Infrastructure/Configuracion`. Ref: `02 §6`, `04 §7`.
- 2026-06-13 - AppSec (Claude Code) - OTP se hashea con bcrypt (`BCrypt.Net-Next`) usando `otp-salt` de Key Vault como pepper (decision del usuario); sesion admin emitida como JWT corto firmado con `jwt-sign` (sin contenedor de sesion). Ref: `06 §4.3`, `10 §5`, `SUPUESTOS.md#fase3-otp-bcrypt-jwt`.
- 2026-06-13 - Backend (Claude Code) - Fase 3: `IResolutorParticipante`/`ResolutorParticipante` resuelven numero->participante validando matricula/estado/rol/campania activa; rechazos neutrales registrados en `LogSeguridad` (motivo solo interno). Pregunta vigente = primera activa por orden (MVP, sin conversaciones aun). Ref: `06 §3`, REQ §26.3, `SUPUESTOS.md#fase3-identidad-auth-impl`.
- 2026-06-13 - AppSec (Claude Code) - Fase 3: `AuthAdminService` orquesta OTP con respuestas neutrales (solo admin activo recibe codigo, un solo uso, expiracion/intentos, sin codigo en claro ni en logs). Puertos `IHasherOtp`/`IGeneradorCodigoOtp`/`INotificadorOtp`/`ILimitadorOtp`/`IServicioSesion` con impls en Infrastructure (bcrypt, CSPRNG, stub WhatsApp, ventana en memoria, JWT HS256). Ref: `06 §4`, `10 §2/§5`.
- 2026-06-13 - Backend/Edge (Claude Code) - Fase 3: endpoints `/api/auth/{request-code,verify-code,logout,me}` (04 §4) con cookie de sesion `httpOnly/Secure/SameSite=Strict` y `IProveedorCorrelacion` (sobre `IHttpContextAccessor`) para propagar correlationId a `LogSeguridad`. Los orquestadores estan guardados tras `Cosmos:AccountEndpoint`; `request-code`/`verify-code` resuelven `IAuthAdminService` desde `RequestServices` para no romper la inferencia de minimal API. Ref: `04 §4`, `10 §6.2`, `SUPUESTOS.md#fase3-identidad-auth-impl`.

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
- 2026-06-13T03:30:37Z - Codex - Implementado adaptador Cosmos de `users`: documentos/mappers `Usuario` y `Tag`, wrapper de contenedor, repositorio `RepositorioUsuariosCosmos`, busqueda por numero normalizado y filtros de usuarios/tags; 7 pruebas unitarias nuevas con fake container. Backend build/test/format verde. Commit 19d4761.
- 2026-06-13 - Claude Code - Cerrada Fase 1: contenedor `participants` (dominio `ParticipanteCampania`/`EnvioMensaje` + enums, puerto `IRepositorioParticipantes`, `RepositorioParticipantesCosmos` con idempotencia de envio saliente) y contenedor `security` (dominio `CodigoAuthAdmin`/`LogSeguridad` + `TipoEventoSeguridad`, puertos `IRepositorioCodigosAuth`/`IRepositorioLogSeguridad`, repos Cosmos). 13 pruebas unitarias nuevas (66 unit + 1 integration en verde). Backend build/test/format verde.
- 2026-06-13 - Claude Code - Completada Fase 3 (Identidad y Auth, `06`): identidad/matricula (`IResolutorParticipante`/`ResolutorParticipante`) y autenticacion admin por OTP (`IAuthAdminService`/`AuthAdminService`). Puertos en `Application/Auth` e `Application/Identidad`; impls en `Infrastructure` (`HasherOtpBcrypt`, `GeneradorCodigoOtpCsprng`, `LimitadorOtpMemoria`, `ServicioSesionJwt`, `NotificadorOtpLog`); endpoints `/api/auth/*` con cookie de sesion y `IProveedorCorrelacion`. Paquetes nuevos en Infrastructure (BCrypt.Net-Next, Microsoft.IdentityModel.JsonWebTokens). 31 pruebas nuevas (111 unit + 10 integration = 121 en verde). Backend build `-warnaserror`/test/format verde. Commit pendiente.
- 2026-06-13 - Claude Code - Completada Fase 2 (Contratos de API y seguridad transversal, `04`/`10`): modelo de errores uniforme (excepciones tipadas en `Application/Common`, `MiddlewareManejoErrores`+`MapeadorErrores`+`EscritorRespuestaError`), `MiddlewareCorrelationId`, HTTPS/HSTS guardado a no-Development, rate limiting por endpoint (politicas `publico`/`webhook`/`demo`, 429+`Retry-After`), logging estructurado y acceso a secretos (`ISecretProvider`+`NombresSecretos`; `KeyVaultSecretProvider`/`ConfiguracionSecretProvider`/`SecretProviderConCache`; seleccion por `KeyVault:Uri`). Composition root en `Program.cs` con `AgregarSeguridad`/`AgregarInfraestructura` (Cosmos guardado). Paquetes nuevos en Infrastructure (Azure.Security.KeyVault.Secrets, Azure.Identity, Caching.Memory, Options.ConfigurationExtensions). 24 pruebas nuevas (84 unit + 6 integration = 90 en verde). Backend build `-warnaserror`/test/format verde. Commit pendiente.
