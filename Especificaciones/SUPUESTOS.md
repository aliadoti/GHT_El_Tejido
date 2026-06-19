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
- Fecha: 2026-06-13 - Agente/Rol: Codex - Backend - Commit: 93cf008
- Contexto: `04` secciones 5.1-5.2 y `07` seccion 1 piden CRUD de usuarios/tags, pero no fijan estrategia de paginacion del repositorio, formato interno de ids nuevos ni si los servicios admin deben registrarse sin Cosmos. REQ 12-13.
- Decision: crear ids con prefijos `u_`/`t_` mas GUID, paginar en memoria (`page`, `pageSize`, maximo 100) sobre el resultado actual de `IRepositorioUsuarios`, mantener `DELETE` como baja logica (`EstadoRegistro.Inactivo`) y registrar `IServicioGestionUsuarios` solo cuando exista `Cosmos:AccountEndpoint`; las pruebas sin Cosmos registran el servicio con repositorio en memoria.
- Alternativa(s) descartada(s): ampliar ahora el puerto de repositorio con paginacion/total nativo (mayor cambio en Cosmos para el paso inicial); borrar documentos en `DELETE` (pierde trazabilidad y contradice estados); registrar el servicio siempre (rompe arranque validado sin Cosmos por DI).
- Impacto / reversibilidad: la paginacion puede migrar a Cosmos nativo sin cambiar el contrato HTTP; los ids siguen el patron de documentos existentes; el registro guardado conserva `/health` y pruebas locales sin emulador.

### fase5-gateway-alcance - Alcance y decisiones del WhatsApp Gateway
- Fecha: 2026-06-13 - Agente/Rol: Claude Code - Arquitecto/Backend/AppSec - Commit: pendiente (decision del usuario: sin commit)
- Contexto: la Fase 5 del plan agrupa **WhatsApp Gateway** y **Orquestador conversacional**, pero el orquestador (`05 §4`) depende de Evaluacion LLM (`08`, Fase 6) y Markdown (`09`, Fase 7), aun no construidos, y de los contenedores `conversations`/`responses` de `03`. El usuario pidio implementar **solo la mitad Gateway**. REQ §9, §15, §26 / ARQ §4.
- Decision:
  - **Alcance:** se implementa el Gateway completo (webhook entrante + envio masivo) y se difiere el orquestador. Para no dejar el flujo entrante colgando, se crea el seam `IOrquestadorConversacion` (`05 §4.1`, en `Application/Conversacion`) con impl provisional `OrquestadorConversacionPendiente` (solo registra el hito, sin texto ni PII). El `TrabajadorWebhook` ya le entrega el `MensajeEntrante` resuelto; la Fase 6/7 sustituye la impl sin tocar el Gateway.
  - **Procesamiento asincrono:** colas in-process con `System.Threading.Channels` + `BackgroundService` (02 §5). El ack del webhook es 200 inmediato; el parseo/idempotencia/resolucion ocurren en el trabajador.
  - **Idempotencia de envio saliente:** se decide por **estado del participante** (`estadoEnvio`), no por `ExisteEnvioAsync` (que existe por cualquier registro, incluido `error`, y bloquearia el reintento): `EncolarIniciales` omite `enviado`; `Reenviar` apunta a `sinRespuesta`; `Reintentar` apunta a `error`. `EnvioMensaje` se mantiene append-only para trazabilidad.
  - **Plantilla vs texto:** ACTUALIZACION 2026-06-18: el envio inicial de campania ya no cae a texto libre. Usa la plantilla global `WhatsApp:PlantillaEnvioInicial` y falla cerrado si falta el nombre. La logica fina de ventana de 24h vive con el orquestador (Fase 6).
  - **Almacen de jobs:** in-process en memoria (`AlmacenJobsMemoria`); volatil al reiniciar (02 §5). El portal redispara por estado de participante.
  - **Guardrails de entrada:** solo se acota la longitud al maximo de la campania; cupos por usuario/campania y rate por numero se completan con el orquestador.
  - **Registro guardado:** gateway, colas, almacen de jobs, orquestador provisional y trabajadores se registran siempre; los procesadores y `IServicioEnvios` se gatillan con `Cosmos:AccountEndpoint` (igual que el resto de orquestadores), para que la app arranque sin emulador.
- Alternativa(s) descartada(s): implementar el orquestador ahora con LLM/Markdown mockeados (cierra una frontera que las Fases 6/7 definen con detalle; mas trabajo sin valor inmediato); cola dedicada/Service Bus (excluido en MVP, `01 §11`); idempotencia por `ExisteEnvioAsync` (bloquea reintentos de errores).
- Impacto / reversibilidad: no cambia contratos `03`/`04`. El seam del orquestador y los puertos de cola quedan listos para Fase 6/7. Las colas/almacen in-process son reemplazables por infraestructura durable post-MVP sin tocar los puertos.

### plantilla-envio-inicial-campania - Primer contacto proactivo de campania por plantilla global
- Fecha: 2026-06-18 - Agente/Rol: Codex - Backend/Integracion WhatsApp - Commit: pendiente.
- Contexto: `05 §2.2` exige plantilla HSM aprobada para iniciar conversacion fuera de la ventana de 24 h. El flujo de envio de campania podia caer a texto libre cuando `MensajeInicial.PlantillaWhatsApp` no estaba configurado, lo que Meta no entrega para el primer contacto. REQ §15, §26 / ARQ §4.1, §4.4.
- Decision:
  - El envio inicial de campania, reenvios a `sinRespuesta` y reintentos de errores usan siempre `EnviarPlantillaAsync` con una plantilla global no secreta cargada desde `WhatsApp:PlantillaEnvioInicial`.
  - Si `WhatsApp__PlantillaEnvioInicial__Nombre` falta o esta vacio, el backend rechaza el disparo con error de regla de negocio y no encola trabajos con texto libre.
  - Nombre sugerido para crear/aprobar en Meta y cargar en Azure: `el_tejido_inicio_campania`; idioma recomendado: `es_CO`; componentes sugeridos: `nombre`, `campania`.
  - `MensajeInicial.Texto` se conserva para trazabilidad/render local, pero ya no es el mecanismo de entrega proactiva fuera de ventana.
- Alternativa(s) descartada(s): depender de `MensajeInicial.PlantillaWhatsApp` por campania (deja el invariante critico en operacion manual); caer a texto libre si falta plantilla (Meta no lo entrega); duplicar la pregunta dentro de la plantilla (mezcla saludo/pregunta y contradice `#primer-contacto-pregunta`, que entrega la pregunta cuando el participante responde).
- Impacto / reversibilidad: no cambia contratos `03`/`04`; agrega configuracion no secreta y endurece el flujo de envio. Reversible cambiando la fuente de la plantilla, sin tocar el gateway ni el contrato REST.

### webhook-boton-template-como-entrante - Click del boton de plantilla abre el flujo conversacional
- Fecha: 2026-06-18 - Agente/Rol: Codex - Backend/Integracion WhatsApp - Commit: pendiente.
- Contexto: al recibir la plantilla inicial, si el participante respondia con texto el orquestador enviaba la pregunta vigente, pero si hacia click en el boton del template el webhook llegaba como `messages[].type=button` o `interactive.button_reply` y el parser lo descartaba como no-texto. REQ §9, §15, §26 / ARQ §4.2, §6.
- Decision:
  - `IWhatsAppGateway.ParsearWebhook` considera procesables los mensajes `text`, `button` y `interactive` con `type=button_reply`.
  - Para `button`, el texto conversacional se toma de `button.text` y, si falta, de `button.payload`.
  - Para `interactive.button_reply`, el texto se toma de `button_reply.title` y, si falta, de `button_reply.id`.
  - El orquestador no cambia: el click se trata como primer entrante del participante, crea la conversacion en `EsperandoRespuestaInicial` y envia la pregunta vigente como texto libre dentro de la ventana abierta por ese click.
- Alternativa(s) descartada(s): crear un flujo especial para botones (duplica la regla de primer entrante); ignorar el payload del boton y esperar texto manual (deja roto el CTA principal de la plantilla); evaluar el texto del boton como respuesta a la pregunta (contradice `#primer-contacto-pregunta`, porque el participante aun no recibio la pregunta).
- Impacto / reversibilidad: no cambia contratos `03`/`04`; amplia el parser del webhook y mantiene la idempotencia por `whatsappMessageId`.

### fase6-evaluacion-llm - Decisiones del modulo de Evaluacion LLM
- Fecha: 2026-06-13 - Agente/Rol: Claude Code - Arquitecto/Backend/AppSec - Commit: pendiente (sin commit)
- Contexto: `08` fija el puerto `IEvaluadorLlm`/`ILlmClient`, el contrato de salida (`08 §4`) y el fallback (`08 §6`), pero deja abiertos: la estrategia de seleccion de proveedor, el limite exacto de longitud de la retro, el tratamiento de la salida sobre-larga o de campos opcionales, donde se persiste la `Evaluacion`, y como se aplican los cupos de llamadas. REQ §19, §20, §25.3, §26.5 / ARQ §6, §12.
- Decision:
  - **Un solo cliente HTTP** (`LlmClientHttp`) que adapta la peticion a chat-completions de `AzureOpenAI` (URL `openai/deployments/{modelo}/chat/completions?api-version=2024-06-01`, header `api-key`) o `OpenAI`/`Otro` (URL `{endpoint}/chat/completions`, header `Authorization: Bearer`), seleccionando por `ConfigLLM.proveedor`. Mas simple que una impl por proveedor con factory; el modulo sigue agnostico. Reversible a factory si aparece un proveedor incompatible.
  - **Salida como dato no confiable:** se parsea y valida contra `08 §4`. Invalida (no parsea, recomendacion no valida, retro vacia, fuera de escala, repreguntar sin repregunta) -> fallback. La retro sobre-larga **no** invalida: se **acota** a 600 caracteres (brevedad, REQ §21) en vez de descartar.
  - **Explicacion vacia:** se rellena con un placeholder ("Sin explicacion.") para cumplir el dominio sin descartar una evaluacion por lo demas valida.
  - **Anomalia y fallback en LogSeguridad:** se registra `AnomaliaLlm` con `resultado=anomalia` cuando el modelo marca `anomalia_seguridad`, y con `resultado=fallback` (+motivo) en cada fallback, para revision/telemetria (10 §6.4). La deteccion por patrones de inyeccion mas alla del flag del modelo se difiere (la defensa principal es estructural, `08 §5`).
  - **Cupos de llamadas (`maxLlamadasLlm`):** los aplica el **orquestador** (que conoce `repreguntasUsadas`), no el evaluador. El evaluador solo acota longitud de la respuesta recibida en el contexto.
  - **Persistencia diferida:** la Fase 6 entrega el dominio `Evaluacion` (con snapshots de rubrica/prompt/configLLM y pesos) dentro de `ResultadoEvaluacion`; la persistencia en el contenedor `responses` (y la `Respuesta`/`evaluacionPendiente`) la realiza el orquestador (05 §4.3), aun no construido.
  - **Registro guardado por Cosmos:** `ILlmClient` se registra siempre; `IEvaluadorLlm` solo con `Cosmos:AccountEndpoint` (necesita `IRepositorioLogSeguridad`), como el resto de orquestadores.
- Alternativa(s) descartada(s): una impl `ILlmClient` por proveedor + factory (mas piezas sin valor MVP); descartar la evaluacion por retro larga o explicacion vacia (pierde una evaluacion util por un detalle de formato); aplicar cupos en el evaluador (no tiene el historial de turnos).
- Impacto / reversibilidad: no cambia contratos `03`/`04`. El contrato de salida (`08 §4`) y los puertos quedan estables para el orquestador (Fase 7+). El cliente unico es reemplazable por una factory por proveedor sin tocar el evaluador.

### fase7-markdown - Decisiones del modulo de Markdown
- Fecha: 2026-06-14 - Agente/Rol: Claude Code - Arquitecto/Backend - Commit: pendiente (sin commit)
- Contexto: `09` define el puerto `ICompiladorMarkdown`, la plantilla (`09 §5`) y la persistencia (Blob + `ArtefactoMarkdown`), pero deja abiertos: el titulo del aporte, la estrategia de versionado/id del artefacto, donde se persiste el contenedor `responses` y como manejar Blob sin recurso configurado. REQ §22, §23, §26.7 / ARQ §7.
- Decision:
  - **Contenedor `responses` ahora:** se crea el puerto `IRepositorioRespuestas` y el adaptador Cosmos (Respuesta, Evaluacion, ArtefactoMarkdown; pk `campaniaId`) en Fase 7 porque el compilador los lee/escribe. La **persistencia** de `Respuesta`/`Evaluacion` (al recibir/evaluar) la hara el orquestador (05 §4.3); Fase 7 solo escribe `ArtefactoMarkdown` y lee Respuesta+Evaluacion.
  - **Id de artefacto estable** `md_<respuestaId>` (un artefacto de tipo `respuesta` por respuesta): la regeneracion sobreescribe la ruta canonica de Blob e **incrementa `version`** conservando `creadoEn` (`09 §7`, MVP sobreescribe).
  - **Titulo del aporte:** determinista `# Aporte de {usuario.Nombre}` (la plantilla `09 §5` deja el titulo abierto). Se puede enriquecer luego (p. ej. con `temas`) sin cambiar el contrato.
  - **Contenido canonico unico:** el dominio `ArtefactoMarkdown` recorta (`Trim`) el contenido; se guarda en Blob exactamente `artefacto.ContenidoMarkdown` para que Blob y el documento embebido coincidan byte a byte.
  - **Evaluacion requerida para compilar:** si falta la evaluacion de la respuesta se lanza `NOT_FOUND` (el orquestador compila tras evaluar, incluso en fallback persiste una evaluacion parcial).
  - **Blob configurable con fallback:** `AlmacenBlobAzure` (Azure.Storage.Blobs, Managed Identity) si hay `Blob:AccountUrl`; en su defecto `AlmacenBlobMemoria` in-process (local/CI). El artefacto siempre es regenerable desde datos (REQ §22.4.6), asi que la volatilidad del fallback es aceptable.
  - **Tipos de artefacto:** MVP implementa `respuesta` (REQ §22.2); el enum soporta los demas (`participante`/`campania`/`entidad`/`capitulo`) para el futuro sin cerrarlos.
- Alternativa(s) descartada(s): versionar el Blob por version (mas almacenamiento; MVP sobreescribe la ruta canonica, `09 §7`); generar el titulo con un prompt de compilacion (`09 §4 paso 3` lo permite pero agrega dependencia LLM no necesaria para el MVP); requerir Azurite en CI (se evita con el fallback in-process).
- Impacto / reversibilidad: no cambia contratos `03`/`04`. El renderizado es determinista y regenerable; el versionado por Blob y el prompt de compilacion se pueden anadir despues. El puerto `IRepositorioRespuestas` queda listo para que el orquestador persista Respuesta/Evaluacion.

### orquestador-conversacional - Decisiones del orquestador (cierre de Fase 5)
- Fecha: 2026-06-14 - Agente/Rol: Claude Code - Arquitecto/Backend - Commit: pendiente (sin commit)
- Contexto: `05 §4` define la maquina de estados y el algoritmo, pero deja abiertos: la identidad de la conversacion, como se combinan retro/repregunta/cierre en mensajes, como se encola la compilacion Markdown, que pasa con configuracion incompleta y la resolucion de snapshots. REQ §9, §21, §25.2, §26 / ARQ §4, §6.
- Decision:
  - **Identidad de conversacion determinista** `conv_<campaniaId>_<usuarioId>_<preguntaId>` (una por terna, MVP `03 §3.6`): permite upsert/lectura directa. Una conversacion **cerrada ignora** mensajes posteriores (se descartan en silencio).
  - **Pregunta vigente:** la que entrega la resolucion de participante (`06 §3`, primera activa por orden en el MVP); el orquestador no implementa aun seleccion por hilo abierto.
  - **Mensajes combinados:** para reducir mensajes de WhatsApp, el turno de repregunta envia `retro + "\n\n" + repreguntaSugerida` (tipo `Repregunta`) y el cierre envia `retro + "\n\n" + mensajeCierre` (tipo `Cierre`). Asi cada salida lleva la retro breve (REQ §21) y queda un unico `EnvioMensaje` por turno saliente.
  - **Ventana de servicio:** tras un entrante la ventana de 24h esta abierta, asi que el MVP envia **texto libre** siempre (`05 §2.2`). La repregunta por plantilla fuera de ventana se difiere (no hay plantilla de repregunta configurada); si la ventana estuviera cerrada se sigue usando texto libre (limitacion documentada).
  - **Compilacion Markdown en linea:** el orquestador llama `ICompiladorMarkdown.CompilarAsync` directamente (ya corre en el `TrabajadorWebhook` asincrono) en vez de una cola dedicada (02 §5). Un fallo de compilacion se traga (el artefacto es regenerable, REQ §22.4.6) y no rompe el cierre del hilo. Solo se compila en exito; en fallback la respuesta queda `evaluacionPendiente` y no se compila.
  - **Configuracion incompleta = cierre neutro:** si faltan rubrica/prompt(evaluar)/configLLM no se puede evaluar; se envia retro neutra + cierre, la respuesta queda `evaluacionPendiente` y la conversacion se cierra (no se llama al LLM). Mismo trato visible que el fallback del evaluador (08 §6).
  - **Resolucion de snapshots:** `rubricaRef`/`promptRefs[evaluar]` se toman de la pregunta y, si no, de la campania; las versiones se resuelven como la **ultima** por familia (`ObtenerUltimaRubricaAsync`/`ObtenerUltimoPromptAsync`). La aprobacion del prompt (`18 §3.6`) no se vuelve a verificar aqui (se asume gestionada en configuracion).
  - **Persistencia:** `Mensaje(in/out)` y `Conversacion` en `conversations`; `Respuesta` (con `tagsSnapshot` del usuario) y `Evaluacion` en `responses`. La `Respuesta` se guarda una vez con su estado final (`evaluada`/`evaluacionPendiente`). El participante pasa a `estadoRespuesta=respondio` con `fechaUltimaRespuesta`.
- Alternativa(s) descartada(s): cola dedicada de compilacion (excluido en MVP, `01 §11`); dos mensajes separados de retro y cierre (mas mensajes a Meta sin valor); id de conversacion aleatorio con query (lectura mas cara); fallar duro ante configuracion incompleta (rompe el hilo del usuario, contradice REQ §20.3.10).
- Impacto / reversibilidad: no cambia contratos `03`/`04`. La compilacion en linea y los mensajes combinados son reversibles (cola/mensajes separados) sin tocar el dominio. La seleccion de pregunta vigente y la ventana por plantilla se endurecen al crecer el MVP.

### primer-contacto-pregunta - Primer entrante de un hilo nuevo -> responder con la pregunta vigente
- Fecha: 2026-06-16 - Agente/Rol: Claude Code - Arquitecto/Backend - Commit: pendiente. Actualizado 2026-06-17 - Agente/Rol: Codex - Arquitecto/Backend - Commit: pendiente. Actualizado 2026-06-19 - Agente/Rol: Codex - Backend/Orquestador conversacional - Commit: pendiente.
- Contexto: `05 §4` asume flujo business-initiated (la campania envia la pregunta y el participante responde), pero el envio masivo real entrega el `MensajeInicial`/saludo y marca `EstadoEnvio=Enviado` sin enviar `Pregunta.Texto`. Si el participante responde "Hola", evaluar ese saludo como respuesta genera fallback/cierre sin que haya recibido la pregunta. Decision final 2026-06-17: el primer entrante de una conversacion nueva debe recibir la pregunta vigente; recien el siguiente mensaje se evalua. Esto cubre tanto el cold-start sin envio como el flujo business-initiated cuyo envio inicial fue solo saludo.
- Decision:
  - **Discriminador:** en `OrquestadorConversacion.ProcesarMensajeEntranteAsync`, si `conversacion is null` se envia la pregunta vigente y NO se evalua el mensaje entrante, sin consultar `EstadoEnvio`. El estado de envio solo describe el saludo/plantilla de campania, no prueba que la pregunta evaluable haya sido vista.
  - **Como se evita el bucle:** el primer entrante **crea y persiste** la `Conversacion` en `EsperandoRespuestaInicial` (no avanza a `Evaluando`). El SIGUIENTE entrante ya halla esa conversacion (`conversacion is not null`) y cae al flujo normal de evaluacion. Es decir, el discriminador del "segundo turno" es la existencia de la conversacion, no el estado de envio.
  - **Mensaje enviado:** saludo fijo + `pregunta.Texto` combinados (`SaludoPrimerContacto = "¡Hola! Gracias por escribirnos. Para participar, responde a esta pregunta:"` + `"\n\n"` + texto de la pregunta), como **texto libre** dentro de la ventana (05 §2.2), registrado como `EnvioMensaje` tipo `Inicial` (es, en efecto, la entrega de la pregunta inicial). Se guarda tambien el `Mensaje(in)` del saludo para el hilo, pero NO se llama al LLM ni se persiste `Respuesta`/`Evaluacion`.
  - **Campanias multipregunta:** la pregunta de trabajo se resuelve por preguntas activas en `orden` ascendente y por el estado de sus conversaciones del usuario. Una conversacion abierta conserva el ciclo existente (respuesta -> evaluacion -> un reintento -> cierre). Una conversacion cerrada habilita avanzar a la siguiente pregunta activa; al cierre valido de una pregunta, el orquestador crea el hilo de la siguiente y envia su texto como `Inicial` dentro de la misma ventana. Si no quedan preguntas activas pendientes, los entrantes posteriores se ignoran.
- Alternativa(s) descartada(s): mantener `EstadoEnvio` como condicion (reproduce el bug business-initiated: saludo enviado, pregunta nunca enviada, primer "Hola" evaluado); agregar la pregunta al envio masivo (cambia semantica de `MensajeInicial` y mezcla saludo+pregunta en la plantilla de campania); exigir que `MensajeInicial` contenga la pregunta (traslada un invariante critico a operacion manual); marcar `EstadoEnvio=Enviado` tras el primer entrante (confunde el estado del envio masivo con el contacto entrante); evaluar el saludo y responder con la pregunta a la vez (gasta un turno de evaluacion sobre "Hola", mala señal para la rubrica).
- Impacto / reversibilidad: no cambia contratos `03`/`04`. El texto del saludo es una constante facilmente ajustable; el discriminador es reversible (volver a evaluar todo entrante) sin tocar el dominio.

### fase8-consultas-resultados - Alcance de los endpoints de consulta de resultados
- Fecha: 2026-06-14 - Agente/Rol: Claude Code - Backend - Commit: (Fase 8 backend)
- Contexto: `04 §5.8` define los GET de conversaciones/respuestas/evaluaciones/markdown y `§2` lista un conjunto amplio de filtros; los contenedores `responses`/`conversations` estan particionados por `campaniaId`, por lo que una consulta cross-campania seria cross-particion. REQ §27.3.
- Decision:
  - Las listas (`/conversaciones`, `/respuestas`, `/markdown`) exigen `campaniaId` (query) para consultar dentro de una sola particion (bajo RU). Los detalles por id tambien reciben `campaniaId` (necesario como partition key del point read).
  - Filtros soportados en el MVP: `usuarioId`, `preguntaId`, `estado` (respuestas), `tipoArtefacto` (markdown), aplicados en memoria sobre el resultado de la particion + paginacion `§2`. Los demas filtros de `§2` (area, empresa, tag, calificacionMin/Max, fechaDesde/Hasta, tema, entidad, numero) y la busqueda cross-campania quedan pendientes.
  - `GET /markdown/{id}/raw` devuelve el contenido embebido (`text/markdown`); `POST /markdown/{id}/regenerar` recompila por `ICompiladorMarkdown` desde datos operativos (REQ §22.4.6) y devuelve el artefacto con la version incrementada.
- Alternativa(s) descartada(s): consultas cross-particion sin `campaniaId` (mayor RU y complejidad, innecesario a escala MVP); indexado/consulta avanzada por todos los filtros (se difiere; el indexado automatico de Cosmos lo soporta cuando se exponga).
- Impacto / reversibilidad: no cambia contratos `03`/`04`. Anadir mas filtros es aditivo (se amplian las consultas de los repos sin tocar el contrato HTTP).

### fase4-config-versionado-cosmos - Id fisico para versiones de config
- Fecha: 2026-06-14 - Agente/Rol: Codex - Backend/AppSec - Commit: pendiente
- Contexto: `07` secciones 3-4 define versionado por familia estable + version para rubricas/prompts, y `03` usa el contenedor `config` con `id` y `pk`; Cosmos exige `id` unico dentro de la particion, por lo que varias versiones no pueden compartir el mismo `id` fisico.
- Decision: persistir rubricas/prompts con `id` fisico versionado (`<familiaId>_v<version>`) y campo adicional `familiaId` como id logico expuesto por la API. `GET /rubricas/{id}` y `GET /prompts/{id}` resuelven la ultima version de esa familia.
- Alternativa(s) descartada(s): sobrescribir siempre el mismo documento (rompe historial/versiones); usar contenedores separados por tipo (cambia la arquitectura de `03`); cambiar el contrato REST para requerir id fisico por version (filtra detalle Cosmos al frontend).
- Impacto / reversibilidad: no cambia el contrato HTTP ni los snapshots `rubricaRef/versionRubrica` y `promptRef/versionPrompt`. Si luego se ajusta la spec de datos, el mapeo queda encapsulado en `RepositorioConfiguracionCosmos`.

### release-readiness-diagnostico - Endpoint de verificacion de despliegue (/health/ready)
- Fecha: 2026-06-14 - Agente/Rol: Claude Code - Arquitecto/AppSec - Commit: pendiente
- Contexto: la fase de release (`13 §5/§7`, guia de Azure §11) necesita confirmar, tras crear la infra y cargar los secretos de WhatsApp, que la identidad administrada lee Key Vault y que Cosmos/Blob son accesibles. `/health` es solo liveness (no prueba dependencias) y no existia un mecanismo para "saber si esta parte ya esta bien" sin recorrer Log Stream a mano. REQ §22.4.9 (sin secretos en artefactos), 10 §4/§6.
- Decision:
  - Nuevo endpoint `GET /health/ready` (readiness) que ejecuta comprobaciones por dependencia (secretos canonicos, Cosmos, Blob, config no secreta de WhatsApp) y devuelve un reporte agregado (`ok`/`faltante`/`error`/`no_aplica`) **sin exponer valores de secretos** (solo presencia/alcanzabilidad).
  - Se expone tambien en produccion pero **protegido por una clave de diagnostico** (header `X-Diag-Key`) resuelta por `Diagnostico:ClaveSecretName` (Key Vault, preferido) o `Diagnostico:Clave` (app settings). Si no hay clave configurada, responde **404** (deshabilitado por defecto), para no filtrar la postura de infraestructura en un App Service publico. Comparacion en tiempo constante; rate limit `publico`.
  - HTTP: `200` si agregado `ok`; `503` si `faltante`/`error` (para monitoreo). Comprobaciones `no_aplica` (p. ej. Cosmos en modo memoria, Blob sin `Blob:AccountUrl`) no bloquean.
  - Abstraccion `IComprobacionPreparacion` (Application) con impls en Infrastructure; agregador `ServicioPreparacion`. Registrado siempre (`AgregarDiagnostico`).
- Alternativa(s) descartada(s): gating por sesion admin (huevo-gallina: requiere Cosmos y un admin sembrado, justo lo que se esta verificando en el bring-up); agregado publico 200/503 sin desglose (no dice cual secreto falta); solo Development (no sirve para verificar el App Service productivo); escribir un blob de prueba para verificar Blob (efecto secundario innecesario: se usa `ExistsAsync` del contenedor).
- Impacto / reversibilidad: no cambia contratos `03`/`04` (es un endpoint de operaciones, no del API funcional). Aditivo y reversible. Hallazgo colateral documentado: las filas `Llm__Provider`/`Llm__Endpoint`/`Llm__ApiKeySecretName` de la guia de Azure §10 **no las lee el codigo** (el proveedor/endpoint/keyRef del LLM viven en la entidad `ConfigLLM` en Cosmos, gestionada por el portal); se anoto la aclaracion en la guia.

### simulacion-azure-gating - Habilitar la simulacion WhatsApp en el despliegue real
- Fecha: 2026-06-14 - Agente/Rol: Claude Code - Arquitecto/AppSec - Commit: pendiente
- Contexto: el usuario quiere probar el sistema desplegado en Azure simulando WhatsApp (igual que en local) **sin** conectar Meta todavia. La pagina `/simulacion-whatsapp` y los endpoints `/diagnostico/simulacion/*` (crean admin, emiten OTP) estaban gateados solo a `Development`; en Azure la app corre en Production y no se mapeaban.
- Decision:
  - Nuevo flag `Simulacion:Habilitada` (default false). Los endpoints de simulacion se mapean si `Development` **o** `Simulacion:Habilitada=true`.
  - Fuera de Development quedan protegidos por la **clave de diagnostico** (header `X-Diag-Key`, `FiltroClaveDiagnostico` reutilizando `IProveedorClaveDiagnostico`): sin clave configurada o sin coincidencia -> 404 (indistinguible de no-mapeado). En Development el filtro no exige clave (DX local intacto).
  - **No** se reutiliza `ASPNETCORE_ENVIRONMENT=Development` en Azure porque eso ademas desactiva HTTPS/HSTS, cambia la persistencia a memoria (`appsettings.Development.json`) y expone otros endpoints de diagnostico. El flag aisla solo la simulacion.
  - El webhook `/webhook/whatsapp` no cambia: se firma con el valor de `wa-appsec` (temporal durante la simulacion) y se verifica como siempre. La pagina agrega un campo *Clave de diagnostico* que envia `X-Diag-Key` en los dos endpoints de simulacion (no en el webhook).
- Alternativa(s) descartada(s): flipear a Development en Azure (debilita seguridad y persistencia); dejar la simulacion abierta tras solo el flag (crea admins/OTP sin proteccion); endpoint de simulacion bajo el guard de sesion admin (huevo-gallina: necesitas un admin para crear el primer admin).
- Impacto / reversibilidad: no cambia contratos `03`/`04`. Aditivo y reversible (flag off por defecto). La guia `Guia_Prueba_E2E_Simulada_WhatsApp.md §7` documenta activar/cerrar la simulacion en Azure.

### spa-hosting-y-cd - Servir el portal desde la API y despliegue OIDC
- Fecha: 2026-06-15 - Agente/Rol: Claude Code - Arquitecto/DevOps - Commit: pendiente
- Contexto: CI verde no despliega; faltaba el workflow CD (`12 §3.2`) y, mas critico, la API **no servia** el SPA Angular. En local el portal corre con `ng serve`, asi que el hueco solo aparece en el despliegue real: en Azure no hay `ng serve` y `/simulacion-whatsapp` (y todo el portal) daria 404. Ademas `@angular/build:application` emite a `wwwroot/browser/`, no a la raiz que sirve ASP.NET.
- Decision:
  - El SPA se sirve desde la propia API: `UseDefaultFiles` + `UseStaticFiles` + un `MapFallback` que devuelve `index.html` para deep-links del cliente y **404 propio** para prefijos de API (`/api`, `/webhook`, `/health`, `/diagnostico`). Mismo origen que la API: evita CORS y conserva las cookies de sesion (`Secure/SameSite=Strict`).
  - El fallback se implemento como delegado (no `MapFallbackToFile` con regex de ruta): las restricciones regex en plantillas de ruta son fragiles (el `:` de `(?:` rompe el parseo) y devolvian 405. El delegado verifica el prefijo en codigo, deterministico.
  - `angular.json`: `outputPath` pasa a objeto `{ base: ../ElTejido.Api/wwwroot, browser: "" }` para aplanar la salida en la raiz de `wwwroot` (sin subcarpeta `browser/`).
  - CD: `.github/workflows/deploy.yml` (push a `main` + `workflow_dispatch`) construye SPA -> publica API (incluye wwwroot) -> `azure/login` OIDC -> `azure/webapps-deploy` -> smoke test `/health`. Sin secretos de larga vida (`12 §2/§6`). Requiere Variables de Actions `AZURE_CLIENT_ID/TENANT_ID/SUBSCRIPTION_ID/WEBAPP_NAME` (guia Azure §9.4).
- Alternativa(s) descartada(s): correr `ng serve` apuntando a la API de Azure (CORS + cookies cross-site Secure/SameSite=Strict se complican; no es la topologia de produccion); `MapFallbackToFile` global (devuelve HTML para POST/GET de API desconocidos y da 405); dejar `browser/` y apuntar el WebRoot ahi (mas fragil que aplanar); Publish Profile en vez de OIDC (descartado: el usuario eligio OIDC, mas seguro `12 §2`).
- Impacto / reversibilidad: no cambia contratos `03`/`04`. El `dotnet test` sigue verde sin `wwwroot` (el fallback maneja `index.html` ausente -> 404). Verificado E2E local: `/`, deep-link y `/health` 200; `/api/*` desconocido 404.

### configllm-apikeyref-solo-lectura - ConfigLLM referencia un secreto existente (no escribe)
- Fecha: 2026-06-15 - Agente/Rol: Claude Code - AppSec/Backend - Commit: pendiente
- Contexto: el flujo original de Config LLM ESCRIBIA la API key en Key Vault (`ISecretWriter` -> `llm-key-<id>`), lo que obligaba a dar rol de ESCRITURA (Key Vault Secrets Officer) a la identidad del App Service. En el despliegue real eso fallo con 403 (la guia solo daba Secrets User/lectura). Decision del usuario: no permitir agregar secretos/llaves desde el front; solo referenciar un secreto que ya exista. REQ §19.2, 10 §4, 04 §3.
- Decision:
  - El request `POST/PUT /api/admin/config-llm` ya NO acepta `apiKey`; solo `apiKeyRef` (obligatorio en POST), el nombre de un secreto que un humano cargo previamente en Key Vault con la API key real.
  - `ServicioGestionConfiguracion` usa `ISecretProvider` (lectura) en vez de `ISecretWriter`: valida que `apiKeyRef` exista y sea legible (lo lee y descarta el valor, sin loguearlo). Si no existe / no es legible / esta vacio -> `ErrorValidacion` (400) con detalle `apiKeyRef` y mensaje accionable (cubre 404 y 403 sin acoplar Application a Azure).
  - La identidad del App Service solo necesita **Key Vault Secrets User** (lectura). Se elimina la necesidad de Secrets Officer. `ISecretWriter`/`KeyVaultSecretWriter` quedan en el repo pero sin uso por la app.
  - Contrato `04 §5.7` actualizado en el mismo cambio (regla de oro: contrato compartido se actualiza con la spec). Front `config-llm.page.ts` reemplaza el input de API key por `apiKeyRef`.
  - Mejora transversal de diagnostico: `MapeadorErrores` agrega el TIPO de excepcion en `INTERNAL_ERROR` (`details.exceptionType`), sin filtrar el mensaje (puede traer datos), como pista segura junto al correlationId (peticion del usuario: errores menos genericos).
- Alternativa(s) descartada(s): dar rol de escritura a la identidad (mas privilegio del necesario, lo que el usuario pidio evitar); mantener `apiKey` opcional y escribir solo si viene (sigue requiriendo escritura y confunde el contrato); nombre de secreto fijo `llm-key` impuesto por la app (menos flexible que dejar elegir el `apiKeyRef`).
- Impacto / reversibilidad: cambia el contrato `04 §5.7` (request sin `apiKey`) -> documentado en AVANCES "Contratos". La respuesta no cambia (sigue exponiendo `apiKeyRef`+mascara `********`). Para rotar la key, se actualiza el secreto en Key Vault fuera de la app (el `apiKeyRef` no cambia).

### otp-whatsapp-plantilla - Envio real del OTP de login por WhatsApp
- Fecha: 2026-06-15 - Agente/Rol: Claude Code - Backend/AppSec - Commit: pendiente
- Contexto: `06 §4.2e` exige enviar el OTP por WhatsApp y `INotificadorOtp` quedo con la impl provisional `NotificadorOtpLog` (ver [[fase3-identidad-auth-impl]]). El login admin es un mensaje **iniciado por el negocio** (no hay ventana de servicio de 24h abierta), por lo que Meta exige una **plantilla HSM aprobada** (`05 §2.2`); las specs no fijan el nombre de la plantilla ni el comportamiento ante un fallo de envio. Decision del usuario: conectar el OTP real por WhatsApp. REQ §10.3, `05 §2.2`.
- Decision:
  - Nuevo adaptador `NotificadorOtpWhatsApp` (Infrastructure) que envia el codigo via `IWhatsAppGateway.EnviarPlantillaAutenticacionAsync` (tipo `Autenticacion`). El codigo nunca se registra (`10 §5`).
  - **Plantilla de categoria Authentication (con boton).** ACTUALIZACION 2026-06-16: el primer intento envio solo el componente `body` (`EnviarPlantillaAsync`), pensando reutilizar una plantilla de cuerpo simple `{{1}}`. La plantilla real del cliente es de **categoria Authentication con boton copy-code**, y Meta exige que el codigo viaje **tambien en el componente `button`** (`sub_type=url`, `index=0`). Por eso se agrego `EnviarPlantillaAutenticacionAsync` al puerto/gateway, que arma `body` + `button` con el mismo codigo. Es el formato documentado por Meta para plantillas de autenticacion.
  - **Gating por configuracion:** seccion `Auth:OtpWhatsApp` con `Habilitado` (default `false`), `PlantillaNombre` y `PlantillaIdioma` (default `es`, debe coincidir EXACTO con el idioma aprobado en Meta). Solo si `Habilitado=true` **y** hay `PlantillaNombre`, el registro elige `NotificadorOtpWhatsApp`; en caso contrario sigue `NotificadorOtpLog`. Asi dev y la simulacion (que no conectan Meta) no cambian de comportamiento.
  - **Fallo de envio = se traga y se registra (sin el codigo):** un `EnvioResultado` fallido o una excepcion del gateway NO se propagan, para preservar la respuesta neutral del login (`REQ §10.3.10`): el cliente no debe poder distinguir un admin valido de uno invalido por un error de envio. El `WhatsAppGateway` ahora **registra el cuerpo de error de Meta** (`code`/`error_subcode`/`message`/`fbtrace_id`) para diagnosticar; el cuerpo de error de Graph API no contiene secretos.
- Alternativa(s) descartada(s): mandar solo el componente `body` (Meta rechaza la plantilla Authentication con boton por parametros faltantes); enviar texto libre (Meta lo rechaza fuera de ventana, justo el caso del login); propagar el fallo al endpoint (rompe la neutralidad y filtra que el numero es un admin valido); registrar siempre el notificador WhatsApp (romperia dev/simulacion sin Meta).
- Impacto / reversibilidad: no cambia contratos `03`/`04` ni el endpoint `/api/auth/request-code`. Aditivo y reversible (flag off por defecto). El puerto `INotificadorOtp` no cambia. Nota de diagnostico: el **404** observado en el primer despliegue NO es por el boton (eso da 400) ni por falta de metodo de pago (da 400/403 con codigo); apunta a la **version de Graph API** del default `v20.0` (posible deprecacion ~mediados de 2026) o a un `PhoneNumberId`/token de WABA que no resuelve. La version se ajusta por `WhatsApp__GraphApiBaseUrl` (app setting, sin redeploy).

### fase10-configuracion-llm-usable - Estados requeridos para evaluar con LLM
- Fecha: 2026-06-15 - Agente/Rol: Codex - Arquitecto/Backend/AppSec - Commit: pendiente
- Contexto: Fase 10 pide que una `ConfigLLM` inactiva sea un interruptor real y que el orquestador evalue tambien prompt no aprobado/inactivo y rubrica inactiva. Las specs no definian si debia bloquear o solo advertir al administrador durante la ejecucion conversacional. REQ §19, §20.3.10, §25.1, §31.6 / ARQ §6, §10, §12.
- Decision: el orquestador trata `ConfigLLM` inactiva, rubrica no activa y prompt de evaluacion no activo/no aprobado como configuracion no disponible: no llama al proveedor LLM, guarda la respuesta como `evaluacionPendiente`, envia cierre neutro y registra `LogSeguridad` con resultado `fallback` y motivo interno.
- Alternativa(s) descartada(s): llamar de todos modos y solo advertir en logs (consume tokens y contradice el interruptor); fallar duro la conversacion (mala experiencia y contradice fallback seguro).
- Impacto / reversibilidad: no cambia contratos HTTP ni Cosmos; endurece runtime y reduce consumo. Si luego se requiere flujo de revision administrativa, se puede agregar una notificacion/estado visible sin cambiar la decision de no llamar al LLM.
