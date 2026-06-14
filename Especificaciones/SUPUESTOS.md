# SUPUESTOS - El Tejido MVP

> Registro de decisiones tomadas ante ambiguedad de las specs (ver `01_Convenciones_para_Agentes.md` seccion 9).
> Cada vez que una spec no resuelva un caso y debas decidir, anade una entrada aqui en lugar de dejar la decision oculta en el codigo.

## Formato de cada entrada

```markdown
### <id corto> - <titulo>
- Fecha: <YYYY-MM-DD> - Agente/Rol: <Codex/Claude Code/opencode - rol> - Commit: <hash o PR>
- Contexto: <que spec/caso no estaba definido> - REQ/ARQ: <referencias>
- Decision: <que se eligio>
- Alternativa(s) descartada(s): <que y por que>
- Impacto / reversibilidad: <a que afecta, si cierra o no fronteras post-MVP>
```

---

## Supuestos registrados

### fase0-frontend-lint - Lint inicial del portal Angular
- Fecha: 2026-06-12 - Agente/Rol: Codex - SDET/Frontend - Commit: n/a (sin repositorio Git)
- Contexto: Angular CLI 22 no genero un target `lint`/ESLint por defecto, pero `12_CICD_GitHub_Actions.md` seccion 3.1 exige `npm run lint`.
- Decision: definir `npm run lint` como `prettier --check "src/**/*.{ts,html,scss}"` durante Fase 0.
- Alternativa(s) descartada(s): agregar ESLint en Fase 0 sin reglas de portal reales; aumenta dependencias y no aporta validacion funcional todavia.
- Impacto / reversibilidad: afecta solo el scaffold frontend; es reversible agregando ESLint y cambiando el script sin romper contratos API o de datos.

### fase1-normalizacion-e164 - Validacion plausible de prefijo E.164
- Fecha: 2026-06-12 - Agente/Rol: Codex - Backend/AppSec - Commit: n/a (sin repositorio Git)
- Contexto: `06_Backend_Identidad_y_Autenticacion.md` seccion 2 exige rechazar si el resultado no es E.164 plausible por longitud y prefijo de pais valido, pero las specs no incluyen un catalogo de prefijos permitido. REQ 10.2, REQ 12.2.2 / ARQ 16.
- Decision: validar formato E.164 plausible sin simbolos: solo digitos ASCII, longitud 8-15 y primer digito distinto de 0. Se eliminan simbolos comunes antes de validar.
- Alternativa(s) descartada(s): incluir un catalogo completo de prefijos de pais en dominio; aumenta mantenimiento y puede bloquear numeros validos si queda incompleto.
- Impacto / reversibilidad: afecta solo el value object de dominio; se puede endurecer luego agregando un catalogo/configuracion de prefijos sin cambiar contratos de API o Cosmos.

### fase1-alcance-contenedores - Contenedores construidos para cerrar Fase 1
- Fecha: 2026-06-13 - Agente/Rol: Claude Code - Arquitecto/Backend - Commit: (Fase 1 cierre)
- Contexto: la Fase 1 del plan pide "interfaces de repositorio por contenedor" e "implementacion Cosmos", pero `03` define 8 contenedores y varios pertenecen a modulos de fases posteriores. REQ 14, 29.4, 29.6, 10.3, 30 / ARQ 8-9, 13.
- Decision: cerrar Fase 1 con los contenedores `participants` (ParticipanteCampania, EnvioMensaje) y `security` (CodigoAuthAdmin, LogSeguridad), ademas de los ya hechos (`users`, `campaigns`, `leases`). `conversations`, `responses` y `config` se implementaran junto a sus modulos duenos (Fases 5, 6 y 4).
- Alternativa(s) descartada(s): construir los 8 contenedores ahora; duplica trabajo con las fases que definen la forma exacta de uso y agranda Fase 1 sin valor inmediato.
- Impacto / reversibilidad: no cierra fronteras; los contenedores faltantes se agregan despues sin tocar lo ya hecho. `security` se adelanta porque la Fase 3 (auth) lo consume.

### fase3-otp-bcrypt-jwt - Hashing OTP y mecanismo de sesion admin
- Fecha: 2026-06-13 - Agente/Rol: Claude Code - AppSec - Commit: (Fase 3)
- Contexto: `06 §4.2c` y `10 §5` permiten Argon2id **o** bcrypt para el OTP, y `06 §4.3b` permite JWT corto firmado **o** registro de sesion server-side. Decision del usuario.
- Decision: hashing OTP con `BCrypt.Net-Next`; la sal `otp-salt` de Key Vault se aplica como pepper (se concatena al codigo antes del hash bcrypt, que ya genera su propia sal por hash). Sesion admin como JWT corto firmado con `jwt-sign` (HS256), en cookie `httpOnly/Secure/SameSite=Strict` + token CSRF; expiracion default 60 min. Sin contenedor de sesion.
- Alternativa(s) descartada(s): Argon2id (mas dependencia/config para el MVP); sesion server-side en Cosmos (contenedor extra no definido en `03`, mas piezas).
- Impacto / reversibilidad: reversible. El puerto `IHasherOtp` permite cambiar a Argon2id sin tocar el servicio de auth; el logout depende de expiracion corta (no hay revocacion server-side hasta introducir un contenedor de sesion).

### fase2-seguridad-transversal - Detalles de implementacion de la Fase 2
- Fecha: 2026-06-13 - Agente/Rol: Claude Code - Arquitecto/Backend/AppSec - Commit: (Fase 2)
- Contexto: `04 §3, §8` y `10 §2-§4, §6` fijan el modelo de errores, correlationId, rate limiting y manejo de secretos, pero dejan abiertos nombres de seccion de configuracion, particion del rate limiter y como exponer endpoints aun inexistentes (`/api/auth/*`, webhook son Fases 3/5).
- Decision:
  - Secretos locales bajo la seccion `Secretos:<nombre-canonico>` (user-secrets); cache corta bajo `Seguridad:CacheSecretos:DuracionMinutos` (default 5, rango 5-10). Seleccion de proveedor por presencia de `KeyVault:Uri`.
  - Rate limiter con politicas nombradas `publico`/`webhook` (FixedWindow por IP, 1 min, limites desde `Seguridad`), aplicadas por endpoint (nunca global) para no afectar `/health`. Politica `demo` (1/min) solo para diagnostico.
  - Endpoints de diagnostico `/diagnostico/{error,validacion,limitado}` mapeados solo en `Development` para ejercitar el modelo de errores y el 429; no se exponen en produccion.
  - HTTPS/HSTS solo fuera de `Development` para no romper `/health` sobre http en `WebApplicationFactory`.
  - Registro Cosmos guardado por presencia de `Cosmos:AccountEndpoint`; nombres de contenedor desde `Cosmos:Containers:*` con defaults `users/campaigns/participants/leases/security`; credencial por `Cosmos:AccountKey` o `DefaultAzureCredential`.
- Alternativa(s) descartada(s): rate limiting global (afectaria `/health`); exponer endpoints de diagnostico siempre (superficie innecesaria en produccion); registrar Cosmos siempre (rompe arranque sin emulador).
- Impacto / reversibilidad: no cierra fronteras. Las politicas y el puerto `ISecretProvider` quedan listos para que Fase 3 (auth) y Fase 5 (webhook) los consuman. Los endpoints de diagnostico se pueden quitar sin afectar contratos.

### fase3-identidad-auth-impl - Detalles de implementacion de la Fase 3
- Fecha: 2026-06-13 - Agente/Rol: Claude Code - Arquitecto/Backend/AppSec - Commit: (Fase 3)
- Contexto: `06 §3.2` deja abierta la "pregunta vigente del hilo" (no hay aun contenedor `conversations`, Fase 5), el envio del OTP depende del Gateway (Fase 5) y el registro de los servicios debe convivir con el registro guardado de Cosmos (Fase 2). REQ §10, §26.3 / ARQ §5.
- Decision:
  - **Pregunta vigente (MVP):** sin conversaciones todavia, se elige la primera pregunta `activa` por `orden` ascendente. Cuando exista el hilo conversacional (Fase 5/6) se cambiara a "pregunta de la conversacion abierta o primera pendiente".
  - **Campania activa por participante:** se asume una; si hubiera varias se elige la asociacion mas reciente por `fechaUltimaRespuesta` y, en su defecto, `fechaInclusion`.
  - **Envio del OTP:** puerto `INotificadorOtp` con impl provisional `NotificadorOtpLog` que NO registra el codigo ni el numero (10 §5); el cliente real de WhatsApp llega en Fase 5.
  - **Limite de solicitudes OTP por numero:** `ILimitadorOtp` con `LimitadorOtpMemoria` (ventana fija en `IMemoryCache`), suficiente para el MVP en un solo proceso (02 §5); complementa el rate limit HTTP por IP.
  - **Registro de servicios:** los servicios hoja (hasher, generador, sesion, limitador, notificador, normalizador) se registran siempre; los orquestadores `IAuthAdminService`/`IResolutorParticipante` se gatillan con la presencia de `Cosmos:AccountEndpoint` (necesitan los repos). Los endpoints `request-code`/`verify-code` resuelven `IAuthAdminService` desde `RequestServices` para no condicionar la inferencia de parametros de minimal API cuando el servicio no esta registrado.
  - **Cookie de sesion:** `httpOnly`, `SameSite=Strict`, `Secure` fuera de Development (en pruebas http sobre TestServer viaja igual), nombre `eltejido_sesion`. La emision del JWT incluye `csrf` como claim; el enforcement de `X-CSRF-Token` y de `/api/admin/*` se implementa en Fase 4 (aun no hay endpoints admin).
- Alternativa(s) descartada(s): contenedor de sesion server-side (mas piezas, ya descartado en fase3-otp-bcrypt-jwt); registrar Cosmos siempre (rompe el arranque sin emulador); inferencia de `IAuthAdminService` por parametro (falla el build de endpoints si el servicio esta guardado).
- Impacto / reversibilidad: no cierra fronteras. La eleccion de pregunta vigente y el notificador se sustituyen en Fase 5 sin tocar contratos; el enforcement admin se agrega en Fase 4 reutilizando `IServicioSesion.ValidarAsync`.

### fase1-puertos-persistencia-application - Ubicacion de puertos de repositorio
- Fecha: 2026-06-12 - Agente/Rol: Codex - Arquitecto/Backend - Commit: a36bd2f
- Contexto: `01_Convenciones_para_Agentes.md` seccion 2 menciona interfaces en Domain para algunos modulos, mientras `02_Arquitectura_y_Stack.md` seccion 3 define que Application expone puertos e Infrastructure los implementa. REQ 31.8 / ARQ 1.1, 8, 9.
- Decision: ubicar los puertos de persistencia en `ElTejido.Application`, manteniendo `ElTejido.Domain` como capa pura de entidades/value objects sin contratos de I/O.
- Alternativa(s) descartada(s): poner repositorios en `ElTejido.Domain`; acopla el dominio a necesidades de aplicacion/persistencia y complica mantenerlo como nucleo puro.
- Impacto / reversibilidad: afecta solo la organizacion de interfaces internas; no cambia contratos API ni Cosmos y permite mover interfaces despues si el equipo estandariza otra frontera.

### fase4-admin-enforcement - Guard comun para endpoints administrativos
- Fecha: 2026-06-13 - Agente/Rol: Codex - Backend/AppSec - Commit: be861d4
- Contexto: `04 §1/§5` y `06 §4.4` exigen sesion valida, roles por metodo y CSRF en mutaciones, pero no fijan el mecanismo ASP.NET ni el status exacto para CSRF faltante/invalido.
- Decision: implementar el enforcement como `IEndpointFilter` reutilizable (`AutorizacionAdminEndpointFilter`) para grupos `/api/admin/*`: GET permite `admin` o `visor`; cualquier otro metodo exige `admin` y `X-CSRF-Token` igual al claim `csrf`; CSRF faltante/invalido responde `403 FORBIDDEN`. Para probarlo antes del CRUD real se agregan endpoints minimos bajo `/api/admin/diagnostico/*` solo en `Development`.
- Alternativa(s) descartada(s): middleware global por path (menos explicito para grupos y pruebas); atributo/controller auth (el proyecto usa Minimal APIs); responder `400` por CSRF faltante (filtra menos intencion de seguridad que `403`).
- Impacto / reversibilidad: no cambia contratos de request/response de los CRUD; los endpoints reales de Fase 4 pueden reutilizar el filtro y los diagnosticos se pueden retirar sin afectar produccion.

### fase4-crud-usuarios-tags - Alcance inicial del CRUD admin
- Fecha: 2026-06-13 - Agente/Rol: Codex - Backend - Commit: c689c8f
- Contexto: `04` secciones 5.1-5.2 y `07` seccion 1 piden CRUD de usuarios/tags, pero no fijan estrategia de paginacion del repositorio, formato interno de ids nuevos ni si los servicios admin deben registrarse sin Cosmos. REQ 12-13.
- Decision: crear ids con prefijos `u_`/`t_` mas GUID, paginar en memoria (`page`, `pageSize`, maximo 100) sobre el resultado actual de `IRepositorioUsuarios`, mantener `DELETE` como baja logica (`EstadoRegistro.Inactivo`) y registrar `IServicioGestionUsuarios` solo cuando exista `Cosmos:AccountEndpoint`; las pruebas sin Cosmos registran el servicio con repositorio en memoria.
- Alternativa(s) descartada(s): ampliar ahora el puerto de repositorio con paginacion/total nativo (mayor cambio en Cosmos para el paso inicial); borrar documentos en `DELETE` (pierde trazabilidad y contradice estados); registrar el servicio siempre (rompe arranque validado sin Cosmos por DI).
- Impacto / reversibilidad: la paginacion puede migrar a Cosmos nativo sin cambiar el contrato HTTP; los ids siguen el patron de documentos existentes; el registro guardado conserva `/health` y pruebas locales sin emulador.
