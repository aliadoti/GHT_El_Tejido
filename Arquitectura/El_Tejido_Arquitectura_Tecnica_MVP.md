# El Tejido — Arquitectura Técnica del MVP

**Proyecto:** El Tejido — Sistema conversacional de captura, evaluación y compilación de conocimiento institucional
**Documento:** Arquitectura técnica del primer MVP
**Versión:** 1.0
**Estado:** Propuesta de arquitectura
**Base:** `GHT_banco_de_ideas_req_inicial.md` v0.4
**Fecha:** Junio 2026

---

## 0. Resumen ejecutivo y criterio de diseño

Este documento define la arquitectura técnica del **MVP** de El Tejido: un sistema que captura ideas y conocimiento por **WhatsApp**, los evalúa con un **LLM configurable** usando una **rúbrica en Markdown**, devuelve retroalimentación conversacional breve (con **máximo una repregunta**), guarda **trazabilidad completa** y genera **artefactos Markdown** consultables desde un **portal administrativo**.

El criterio rector es **MVP, no enterprise**: una arquitectura **modular dentro de un monolito desplegable**, no microservicios. Se prioriza rapidez de construcción, bajo costo operativo (componentes serverless/consumo), seguridad razonable y facilidad de evolución. Cada módulo tiene fronteras claras para poder extraerse a un servicio independiente más adelante sin reescribir el sistema.

Decisiones de cabecera (justificadas en la sección 14):

- **Backend modular único** en **.NET 8 sobre Azure App Service (plan B1/Basic)**, organizado en módulos internos (no microservicios).
- **Cosmos DB for NoSQL en modo serverless** como repositorio documental operacional.
- **Azure Key Vault** para API keys y secretos.
- **WhatsApp Cloud API (Meta)** como canal, vía webhook HTTPS.
- **Frontend administrativo SPA** servido por el mismo App Service o como Static Web App.
- **Azure Blob Storage** para los artefactos Markdown (más una copia del contenido en Cosmos para consulta), preparados para versionar en Git en fases posteriores.
- Capa de búsqueda semántica **diseñada pero no implementada** (campos y estructura listos; índice vectorial diferido).

---

## 1. Arquitectura lógica

La arquitectura lógica se organiza en **cuatro capas** y un conjunto de **módulos funcionales** desacoplados por interfaces. Esto respeta el requerimiento de mantenibilidad (req. 31.8), que pide separar lógica conversacional, configuración, WhatsApp, LLM, persistencia, generación Markdown, seguridad y portal.

### 1.1 Capas

1. **Capa de canal e ingreso (Edge):** recibe el webhook de WhatsApp, sirve el portal y la API administrativa, valida firma/origen y aplica rate limiting de entrada.
2. **Capa de aplicación (dominio modular):** orquesta la conversación, las campañas, la evaluación, la generación de Markdown, la autenticación y la seguridad. Es el corazón del sistema y vive en un solo proceso desplegable.
3. **Capa de datos y artefactos:** Cosmos DB (operacional), Blob Storage (Markdown), Key Vault (secretos). Preparada para una futura capa vectorial.
4. **Capa de integraciones externas:** WhatsApp Cloud API y el proveedor LLM configurable (Azure OpenAI / OpenAI / otro).

### 1.2 Diagrama lógico (textual)

```
                          ┌──────────────────────────────────────────────┐
   Participante           │                EL TEJIDO (MVP)               │
   (WhatsApp)             │                                              │
        │                 │  ┌────────────────────────────────────────┐  │
        │  mensajes       │  │        CAPA DE CANAL / EDGE             │  │
        ▼                 │  │  • Webhook WhatsApp (inbound)          │  │
   ┌─────────┐  webhook   │  │  • API REST admin (/api/*)            │  │
   │WhatsApp │───────────▶│  │  • Hosting SPA portal                 │  │
   │Cloud API│◀───────────│  │  • Verificación firma + rate limit    │  │
   └─────────┘  outbound  │  └───────────────┬────────────────────────┘  │
        ▲                 │                  │                           │
        │                 │  ┌───────────────▼────────────────────────┐  │
   Administrador          │  │      CAPA DE APLICACIÓN (módulos)      │  │
   (Portal web)           │  │                                        │  │
        │                 │  │  [Auth]  [Conversación]  [Campañas]    │  │
        └────────────────▶│  │  [Evaluación LLM]  [Markdown]          │  │
            HTTPS/API      │  │  [Seguridad/Guardrails]  [Config]      │  │
                          │  └───┬─────────────┬───────────────┬──────┘  │
                          │      │             │               │         │
                          │  ┌───▼───┐   ┌─────▼─────┐   ┌─────▼──────┐  │
                          │  │Cosmos │   │   Blob    │   │ Key Vault  │  │
                          │  │  DB   │   │ (Markdown)│   │ (API keys) │  │
                          │  └───────┘   └───────────┘   └────────────┘  │
                          │                                              │
                          │   ····· (futuro) Azure AI Search vectorial ··│
                          └──────────────────────┬───────────────────────┘
                                                 │ HTTPS
                                          ┌──────▼───────┐
                                          │ Proveedor LLM │
                                          │ (configurable)│
                                          └───────────────┘
```

### 1.3 Módulos funcionales (dominio)

| Módulo | Responsabilidad | Equivale a (req.) |
|---|---|---|
| **WhatsApp Gateway** | Recepción/envío de mensajes, plantillas, normalización de número, idempotencia de webhook | §9, §15, §26.2 |
| **Conversación / Orquestador** | Máquina de estados de la conversación, control de repregunta única, cierre | §21, §26.4–26.8 |
| **Identidad y matrícula** | Resolver número → participante, validar activo y pertenencia a campaña | §12, §26.3 |
| **Autenticación admin** | Login por código OTP vía WhatsApp, sesiones, roles | §10 |
| **Campañas y configuración** | Campañas, mensajes iniciales, preguntas, tags, asociación participantes | §11–§16 |
| **Rúbricas y Prompts** | CRUD y versionado de rúbricas Markdown y prompts; aprobación humana | §17, §18 |
| **Evaluación LLM** | Construcción de contexto, llamada al proveedor, parsing/validación de salida estructurada | §19, §20 |
| **Generación Markdown** | Compilar respuesta + evaluación + metadatos en artefactos Markdown | §22 |
| **Seguridad / Guardrails** | Rate limiting, límites de longitud y consumo, anti prompt-injection, validación de salida | §25 |
| **Logging y trazabilidad** | Registro estructurado de eventos, conversaciones, evaluaciones, seguridad | §30 |
| **Portal admin (frontend)** | UI de configuración, envío y consulta de resultados | §27, §32 |

Cada módulo expone una interfaz interna (clase/servicio inyectado). El acoplamiento es por contrato, no por proceso: extraer "Evaluación LLM" a una Function aislada en el futuro es un cambio de hosting, no de diseño.

---

## 2. Arquitectura física sugerida en Azure

Se busca **mínima cantidad de recursos**, **costo bajo o por consumo**, y operación sencilla.

### 2.1 Diagrama físico (textual)

```
                         Internet
                            │
              ┌─────────────┴──────────────┐
              │                            │
       (Meta) WhatsApp              Administradores
        Cloud API                   (navegador)
              │                            │
              │ webhook HTTPS              │ HTTPS
              ▼                            ▼
   ┌──────────────────────────────────────────────────┐
   │   Azure App Service (Linux, plan B1 Basic)        │
   │   ┌────────────────────────────────────────────┐  │
   │   │  El Tejido — backend .NET 8 (modular)      │  │
   │   │  + portal SPA estático servido aquí mismo  │  │
   │   └────────────────────────────────────────────┘  │
   │   Managed Identity habilitada                      │
   └───────┬───────────────┬──────────────┬────────────┘
           │               │              │
   (Managed Identity)      │              │
           ▼               ▼              ▼
   ┌──────────────┐ ┌─────────────┐ ┌──────────────┐
   │ Cosmos DB    │ │ Blob Storage│ │ Key Vault    │
   │ NoSQL        │ │ (md + logs  │ │ (API keys,   │
   │ Serverless   │ │  fríos)     │ │  secretos)   │
   └──────────────┘ └─────────────┘ └──────────────┘
           │
           ▼
   ┌──────────────────────────────┐
   │ Application Insights / Logs   │  ← telemetría y trazas
   └──────────────────────────────┘

   Proveedor LLM (Azure OpenAI o externo) ── HTTPS saliente desde App Service
   (futuro, no MVP):  Azure AI Search (índice vectorial)
```

### 2.2 Recursos Azure y dimensionamiento MVP

| Recurso | SKU sugerido MVP | Por qué | Costo aprox. |
|---|---|---|---|
| **App Service Plan** | Linux **B1 Basic** (o S1 si se requiere slot) | Aloja backend + SPA, soporta Always On para webhook estable, Managed Identity | Bajo, fijo mensual |
| **Cosmos DB for NoSQL** | **Serverless** | Tráfico intermitente (5 usuarios → 120), se paga por RU consumida; ideal para prueba | Muy bajo (consumo) |
| **Azure Key Vault** | Standard | Secretos y API keys; integración nativa con Managed Identity | Casi nulo |
| **Blob Storage** | Standard LRS, Hot | Artefactos Markdown + logs fríos exportados | Casi nulo |
| **Application Insights** | Pago por ingesta, con sampling | Telemetría, trazas y alertas básicas | Bajo |
| **Azure OpenAI** *(si se usa como proveedor)* | Pago por token | LLM configurable; alternativa a OpenAI directo | Por consumo |

> **Nota sobre Always On:** el webhook de WhatsApp requiere que el endpoint responda con baja latencia. En App Service Basic+ se activa *Always On* para evitar cold starts. Esta es una razón clave para preferir App Service sobre Functions en plan Consumo para el endpoint de ingreso (ver alternativas, §15).

### 2.3 Entornos

Para el MVP basta **un entorno** (`mvp`/`staging`) más, opcionalmente, un *deployment slot* para pruebas. Infra como código con **Bicep** (o Terraform) recomendado pero no bloqueante; con un script de despliegue es suficiente para arrancar.

---

## 3. Componentes principales

Resumen de los componentes desplegables y su rol:

1. **Backend El Tejido (.NET 8 modular)** — único proceso que contiene todos los módulos de dominio de §1.3. Expone:
   - `POST /webhook/whatsapp` y `GET /webhook/whatsapp` (verificación) — ingreso de participantes.
   - `/api/admin/*` — API REST protegida para el portal.
   - Sirve los estáticos del SPA en la raíz.
2. **Portal administrativo (SPA)** — interfaz web (React/Vite recomendado por velocidad; ver §14) alineada a la marca GHT (§32). Consume `/api/admin/*`.
3. **Cosmos DB (operacional)** — todos los documentos del sistema (§9 contenedores).
4. **Blob Storage (artefactos)** — archivos `.md` generados, organizados por campaña; preparados para futura migración a Git.
5. **Key Vault (secretos)** — API keys de WhatsApp y LLM, secreto de firma de sesión/JWT, sal de hashing de OTP.
6. **Application Insights (observabilidad)** — telemetría, trazas distribuidas internas, alertas de error y de consumo.
7. **Integraciones externas** — WhatsApp Cloud API (Meta) y proveedor LLM configurable.

Worker en segundo plano: el envío masivo de mensajes iniciales y la generación de Markdown se ejecutan como **tareas en segundo plano dentro del mismo proceso** (cola en memoria + `IHostedService`, o Azure Storage Queue si se quiere durabilidad). Para 5–120 usuarios no se justifica una Function Queue dedicada en el MVP, pero la frontera queda lista para extraerla.

---

## 4. Flujo de WhatsApp inbound y outbound

### 4.1 Modelo de mensajería de WhatsApp (restricción de negocio)

WhatsApp Cloud API impone dos reglas que condicionan el diseño:

- **Conversaciones iniciadas por el negocio** (el caso de El Tejido, que dispara el mensaje inicial) **requieren plantillas (HSM) pre-aprobadas** por Meta. Los mensajes iniciales (§15) y la repregunta fuera de ventana deben gestionarse como **plantillas aprobadas con variables**.
- Una vez el usuario responde, se abre una **ventana de servicio de 24 horas** durante la cual se pueden enviar **mensajes de texto libres** (la retroalimentación y la repregunta, si llegan dentro de la ventana).

El módulo WhatsApp Gateway encapsula esta lógica: decide entre *plantilla* o *texto libre* según el estado de la ventana.

### 4.2 Flujo inbound (participante → sistema)

```
1. WhatsApp Cloud API hace POST a /webhook/whatsapp con el mensaje.
2. Edge valida:
   a. Firma X-Hub-Signature-256 (HMAC con app secret de Key Vault).
   b. Idempotencia: ¿ya procesamos este message_id? (dedupe en Cosmos).
3. Responde 200 OK INMEDIATAMENTE (ack a Meta) y encola el procesamiento.
   → Evita timeouts y reintentos de Meta; procesamiento asíncrono.
4. Worker toma el mensaje:
   a. Normaliza el número (E.164, sin símbolos).
   b. Resuelve participante por número (módulo Identidad).
   c. Valida: matriculado + activo + asociado a campaña activa.
      - Si NO autorizado → respuesta de rechazo controlado y neutral
        ("Este número no está habilitado para esta actividad"),
        sin revelar si el número existe (§10.3.10 / §26.3.6).
   d. Aplica guardrails de entrada: longitud máx, rate limit por número,
      conteo de mensajes/llamadas LLM por campaña (§25).
   e. Persiste el Mensaje y la Respuesta (asociada a usuario/campaña/pregunta).
   f. Avanza la máquina de estados de Conversación (§4 flujo evaluación).
```

### 4.3 Flujo outbound (sistema → participante)

```
1. El módulo que necesita enviar (Campañas, Conversación, Auth) llama a
   WhatsApp Gateway con: destino, tipo (plantilla|texto), contenido/variables.
2. Gateway decide:
   - Mensaje inicial / fuera de ventana 24h  → plantilla aprobada + variables.
   - Dentro de ventana 24h (retro/repregunta) → mensaje de texto libre.
3. Renderiza variables ({{nombre}}, {{campaña}}, {{empresa}}, {{area}}) (§15.3).
4. POST a Graph API de WhatsApp con el token (Key Vault, vía Managed Identity).
5. Registra un documento EnvioMensaje con estado, tipo y timestamp (§29.6):
   tipo ∈ {Inicial, Reenvío, Repregunta, Cierre, Autenticación}.
6. Maneja errores y reintentos con backoff; marca estado de envío por
   participante para consulta y reenvío (§15.4, §27.2).
```

### 4.4 Envío masivo de mensajes iniciales

El administrador dispara el envío desde el portal. El backend encola un job por participante seleccionado; el worker los procesa con control de ritmo (throttling) para respetar límites de la API y registra estado individual. El reenvío a quienes no respondieron reusa el mismo mecanismo filtrando por estado de respuesta.

---

## 5. Flujo de autenticación administrativa por código WhatsApp

Implementa un **OTP de un solo uso, con expiración, entregado por WhatsApp** (§10), sin contraseñas.

```
LOGIN (paso 1 — solicitud de código)
1. Admin abre el portal; la pantalla muestra instrucciones de normalización
   del número (formato internacional, sin símbolos) (§10.2).
2. Admin ingresa su número → POST /api/auth/request-code.
3. Backend normaliza el número y busca usuario con rol administrativo.
4. SIEMPRE responde igual ("Si el número está habilitado, recibirás un código"),
   sin revelar existencia del número (§10.3.10).
5. Si y solo si existe admin válido:
   a. Verifica límites: nº de solicitudes de código por número y ventana (§10.3.7).
   b. Genera código numérico aleatorio (p. ej. 6 dígitos, CSPRNG).
   c. Guarda SOLO el hash del código (bcrypt/Argon2 + sal) con:
      expiración (p. ej. 5 min), intentos restantes, usado=false (§10.3.4–8).
   d. Envía el código por WhatsApp (plantilla de autenticación, tipo=Autenticación).

LOGIN (paso 2 — verificación)
6. Admin ingresa el código → POST /api/auth/verify-code.
7. Backend:
   a. Verifica límite de intentos (§10.3.6); si excede, invalida el código.
   b. Compara hash; valida no-expirado y no-usado.
   c. Si válido: marca usado=true, emite sesión (cookie httpOnly + JWT corto
      o token de sesión en Cosmos), con rol y expiración.
   d. Si inválido/vencido: rechaza, decrementa intentos, registra evento (§10.3.9).
8. Todas las rutas /api/admin/* exigen sesión válida + rol; el participante
   nunca accede al portal (§8.1, §27.4).
```

Registros de seguridad: cada solicitud y verificación (éxito/fallo) genera un evento en el log de seguridad (§30), incluyendo número normalizado, resultado y timestamp, sin almacenar el código en claro.

---

## 6. Flujo de evaluación con LLM

El orquestador de Conversación invoca el módulo de Evaluación LLM tras capturar una respuesta. El principio central anti prompt-injection es **separar instrucciones del sistema de los datos del usuario** (§25.3).

```
1. PRE-PROCESO (guardrails de entrada)
   - Trunca/rechaza respuestas que exceden longitud máxima configurada.
   - Verifica cupos: máx. mensajes y máx. llamadas LLM por usuario/campaña.
   - Sanea: la respuesta del usuario se tratará como DATO, nunca como instrucción.

2. CONSTRUCCIÓN DEL CONTEXTO (mensajes separados por rol)
   - SYSTEM: prompt de evaluación (versionado) + reglas de comportamiento
     (no prometer implementar, no ejecutar acciones, responder corto) (§18.3, §20.3)
     + instrucción explícita de ignorar cualquier instrucción contenida en
       la respuesta del usuario.
   - SYSTEM/CONTEXT: rúbrica Markdown (versionada) con criterios, pesos y escala;
     contexto de campaña; tags relevantes; historial reciente acotado (§20.1).
   - USER (delimitado): la pregunta y la respuesta del usuario, encerradas en
     delimitadores claros y etiquetadas como "contenido a evaluar, no instrucciones".
   - NUNCA se incluyen secretos ni API keys en el contexto (§25.3.7).

3. LLAMADA AL PROVEEDOR (configurable)
   - Lee configuración LLM activa (proveedor, modelo, endpoint, parámetros)
     y la API key por referencia desde Key Vault (§19).
   - Aplica timeout y nº máximo de reintentos configurados (§19.1, §25.1).
   - Solicita salida en formato estructurado (JSON con esquema fijo).

4. POST-PROCESO (validación de salida)
   - Valida que la respuesta cumpla el esquema esperado (§20.3.1, §25.3.4).
   - Si es inválida o el proveedor falla → FALLBACK seguro: respuesta neutra
     al usuario y marca de evaluación pendiente; no rompe la conversación (§20.3.10).
   - Extrae: calificación por criterio + justificación, calificación total,
     explicación, retroalimentación al usuario, recomendación (cerrar|repregunta),
     repregunta sugerida, temas, entidades, indicador de anomalía (§20.2).

5. PERSISTENCIA Y DECISIÓN
   - Guarda Evaluación completa con: rúbrica+versión, prompt+versión,
     config LLM usada, pesos, calificaciones, textos (§20.3.3–6, §30).
   - El orquestador decide:
     · Primera evaluación y recomendación = repregunta y aún no se usó la
       repregunta → envía UNA repregunta (§26.6) y espera respuesta.
     · En caso contrario → cierra con mensaje de agradecimiento (§26.8).
   - Tope duro del MVP: 1 repregunta ⇒ máx. 2 evaluaciones por hilo (§25.2).

6. SALIDA AL USUARIO
   - Envía la retroalimentación breve/conversacional por WhatsApp (outbound §4.3),
     respetando los "no debe" de §21.3 (no prometer, no extenderse).
```

### 6.1 Esquema de salida estructurada esperado (contrato)

```json
{
  "calificacion_por_criterio": [
    { "criterio": "string", "puntaje": 0, "justificacion": "string" }
  ],
  "calificacion_total": 0,
  "explicacion": "string",
  "retroalimentacion_usuario": "string (breve)",
  "recomendacion": "cerrar | repreguntar",
  "repregunta_sugerida": "string | null",
  "temas": ["string"],
  "entidades": ["string"],
  "anomalia_seguridad": false
}
```

Este contrato fijo permite validar la salida, almacenarla normalizada y desacoplar el sistema del proveedor LLM concreto.

---

## 7. Flujo de generación de Markdown

La compilación a Markdown convierte la respuesta evaluada en un artefacto durable, atribuido y regenerable (§22).

```
1. DISPARO
   - Tras guardar la evaluación final (cierre del hilo), el orquestador encola
     un job de compilación Markdown para esa respuesta/participante.
   - El tipo de artefacto (por respuesta | participante | campaña | pregunta)
     lo define la configuración de la campaña (§11.3.10, §22.2).

2. ENSAMBLAJE
   - Toma datos operativos: respuesta original, evaluación, metadatos de
     usuario/campaña/pregunta, rúbrica+versión, prompt+versión, calificación.
   - Renderiza la plantilla Markdown estándar (§22.3): metadatos, respuesta
     original, tabla de evaluación por criterio, retroalimentación enviada,
     temas, entidades y notas de trazabilidad (IDs de conversación/respuesta/
     evaluación).
   - Opcional: un prompt de compilación (versionado) puede redactar la sección
     narrativa; el resto se arma determinísticamente desde los datos.
   - Regla dura: el Markdown NO contiene secretos ni API keys (§22.4.9).

3. PERSISTENCIA
   - Guarda el archivo .md en Blob Storage:
       /campañas/{campañaId}/{tipo}/{entidadId}.md
   - Guarda en Cosmos un documento ArtefactoMarkdown (§29.14) con el contenido
     y metadatos para consulta rápida desde el portal sin leer Blob.
   - Versiona el artefacto (campo versión); preparado para futura sincronización
     a Git (§22.4.7, §23.2).

4. CONSULTA Y REGENERACIÓN
   - El portal lista y muestra el Markdown generado (§27.1).
   - Regla de diseño (§23.3): el artefacto SIEMPRE puede regenerarse desde los
     datos operativos; el Blob/Cosmos es caché materializada, no fuente única.

5. PREPARACIÓN SEMÁNTICA (futuro, no MVP)
   - Los metadatos (campaña, autor, tags, temas, entidades) y el contenido
     quedan estructurados para que una capa vectorial los indexe después
     sin reprocesar la conversación (§24.3).
```

---

## 8. Modelo de datos conceptual

El modelo sigue las entidades del requerimiento (§28.3, §29). Al ser Cosmos (documental), se aplica **embebido** para datos que se leen juntos y rara vez cambian, y **referencias por ID** para entidades con ciclo de vida propio.

### 8.1 Diagrama de relaciones (textual)

```
Usuario (rol: participante|admin|visor)
   │  1───*  ParticipanteCampaña  *───1  Campaña
   │                                        │
   │                                        ├── embebe → MensajeInicial[]
   │                                        ├── embebe → Pregunta[]
   │                                        ├── ref → Rúbrica (por versión)
   │                                        ├── ref → Prompt[] (por versión)
   │                                        └── ref → ConfigLLM
   │
   │  1───*  Conversación  1───*  Mensaje
   │                          │
   │                          └──*  Respuesta  1───*  Evaluación
   │                                   │                 │
   │                                   └─────────────────┴──*  ArtefactoMarkdown
   │
   *───*  Tag

EnvioMensaje      ── ref → Usuario, Campaña, MensajeInicial
CodigoAuthAdmin   ── ref → Usuario (admin)
LogSeguridad      ── ref → Usuario/número (cuando aplica)
```

### 8.2 Entidades y campos clave

- **Usuario** — `id`, `nombre`, `whatsappNormalizado` (E.164), `rol` (participante/admin/visor), `estado`, `area`, `empresa`, `tags[]`, `propiedadesDinamicas{}`, timestamps. (§29.1)
- **Tag** — `id`, `nombre`, `tipo` (area/empresa/…), `descripcion`, `estado`. Parametrizable, no quemado en código. (§13, §29.2)
- **Campaña** — `id`, `nombre`, `descripcion`, `objetivo`, `estado` (borrador/activa/cerrada/archivada), `mensajesIniciales[]` (embebidos), `preguntas[]` (embebidas), `rubricaRef`, `promptRefs`, `configLLMRef`, `configMarkdown`, `configConversacional`, `usuariosHabilitados`, timestamps. (§11, §29.3)
- **ParticipanteCampaña** — `id`, `usuarioId`, `campañaId`, `estado`, `fechaInclusion`, `estadoEnvio`, `estadoRespuesta`, `fechaPrimerEnvio`, `fechaUltimaRespuesta`. (§29.4)
- **MensajeInicial** — `id`, `campañaId`, `nombreInterno`, `texto`, `orden`, `variablesDinamicas[]`, `estado`, timestamps. (§29.5)
- **EnvioMensaje** — `id`, `campañaId`, `usuarioId`, `mensajeInicialId`, `numero`, `estadoEnvio`, `fechaEnvio`, `tipo` (Inicial/Reenvío/Repregunta/Cierre/Autenticación), `error?`. (§29.6)
- **Pregunta** — `id`, `campañaId`, `texto`, `instruccion`, `categoria`, `orden`, `estado`, `rubricaRef`, `promptRef`, `maxRepreguntas`, `limitesSeguridad`, `configMarkdown`. (§16.2, §29.7)
- **Rúbrica** — `id`, `nombre`, `descripcion`, `contenidoMarkdown`, `escala`, `criterios[]`, `pesos`, `version`, `estado`, timestamps. **Versionada.** (§17, §29.8)
- **Prompt** — `id`, `nombre`, `tipo` (evaluar/retro/repregunta/cierre/compilar/…), `contenido`, `version`, `estado`, `aprobadoPor`, `fechaAprobacion`, timestamps. **Versionado y con aprobación humana.** (§18, §29.9)
- **ConfigLLM** — `id`, `nombre`, `proveedor`, `modelo`, `endpoint`, `apiKeyRef` (referencia a Key Vault, **nunca la clave**), `parametros`, `limitesTokens`, `timeout`, `maxReintentos`, `estado`, timestamps. (§19, §29.10)
- **Conversación** — `id`, `usuarioId`, `campañaId`, `canal`, `estado`, `mensajes[]` (o referencia), `fechaInicio`, `fechaCierre`, `estadoMaquina` (control de repregunta). (§29.11)
- **Mensaje** — `id`, `conversacionId`, `direccion` (in/out), `texto`, `whatsappMessageId` (idempotencia), `timestamp`. (§28.3)
- **Respuesta** — `id`, `usuarioId`, `campañaId`, `preguntaId`, `texto`, `canal`, `fecha`, `estado`. (§29.12)
- **Evaluación** — `id`, `respuestaId`, `rubricaRef`+`versionRubrica`, `promptRef`+`versionPrompt`, `configLLMRef`, `calificacionPorCriterio[]`, `calificacionTotal`, `explicacion`, `retroalimentacionEnviada`, `temas[]`, `entidades[]`, `recomendacion`, `pesosUsados`, `fecha`. (§20, §29.13)
- **ArtefactoMarkdown** — `id`, `tipo` (respuesta/participante/campaña/entidad/capítulo), `campañaId`, `usuarioId`, `preguntaId`, `respuestaRef`, `evaluacionRef`, `contenidoMarkdown`, `blobPath`, `estado`, `version`, timestamps. (§22, §29.14)
- **CodigoAuthAdmin** — `id`, `usuarioId`, `hashCodigo` (Argon2/bcrypt + sal), `expiracion`, `intentosRestantes`, `usado`, `creadoEn`. **Nunca en texto plano.** (§10.3, §28.3)
- **LogSeguridad** — `id`, `tipoEvento`, `usuarioId?`, `numero?`, `resultado`, `detalle`, `timestamp`. (§30)

### 8.3 Criterio de embebido vs. referencia

- **Embebido** en Campaña: mensajes iniciales y preguntas (se editan y leen junto con la campaña, baja cardinalidad).
- **Referencia + snapshot de versión**: rúbrica, prompt y config LLM. La Evaluación guarda el **ID + versión usada** (no solo el ID), garantizando trazabilidad reproducible aunque la rúbrica/prompt cambie después (§17.3.3, §18.2, §20.3.4–6).
- **Documentos independientes** de alto volumen: Mensaje, Respuesta, Evaluación, EnvioMensaje, LogSeguridad.

---

## 9. Colecciones / contenedores sugeridos para la base documental

Cosmos DB for NoSQL serverless. Se agrupan entidades afines para limitar el número de contenedores (menor complejidad y costo) y se elige la **partition key** buscando distribución y queries eficientes.

| Contenedor | Documentos (type) | Partition key | Justificación |
|---|---|---|---|
| **users** | Usuario, Tag | `/tipo` o `/id` | Catálogo pequeño; lectura por número e id |
| **campaigns** | Campaña (con mensajes y preguntas embebidos) | `/id` (campañaId) | Unidad de configuración; se lee completa |
| **participants** | ParticipanteCampaña, EnvioMensaje | `/campañaId` | Consultas y envíos siempre por campaña |
| **conversations** | Conversación, Mensaje | `/campañaId` (o `/usuarioId`) | Hilo conversacional agrupado por campaña |
| **responses** | Respuesta, Evaluación, ArtefactoMarkdown (metadatos) | `/campañaId` | Consulta administrativa filtra por campaña, área, tag, calificación |
| **config** | Rúbrica, Prompt, ConfigLLM (todas las versiones) | `/tipo` | Catálogo versionado de baja escritura |
| **security** | CodigoAuthAdmin, LogSeguridad | `/tipo` (o `/usuarioId`) | OTP con TTL; logs append-only |
| *(opcional)* **leases** | dedupe de webhook / locks | `/id` | Idempotencia de mensajes WhatsApp |

Notas de diseño Cosmos:

- Cada documento lleva un campo discriminador `type` para coexistir varios tipos por contenedor.
- **TTL nativo** en `security` para que los códigos OTP expiren automáticamente.
- La **partition key por `campañaId`** es la decisión central: casi todas las consultas administrativas y el flujo conversacional operan dentro de una campaña, lo que da queries de una sola partición y bajo RU. Riesgo de *hot partition* es nulo a escala MVP (5–120 usuarios).
- Índices secundarios automáticos de Cosmos cubren los filtros del portal (área, empresa, tag, pregunta, estado, calificación) sin diseño adicional. Se puede afinar la política de indexado para reducir RU si fuese necesario.

---

## 10. Estrategia de almacenamiento seguro de API keys

Cumple §19.2 y §25.3.7. Principio: **la clave nunca vive en la base de datos ni en el código; solo una referencia.**

```
┌─────────────┐   apiKeyRef (nombre del secreto)   ┌──────────────────┐
│  Cosmos DB  │ ─────────────────────────────────▶ │   Azure Key Vault │
│ ConfigLLM   │                                    │  secret: llm-key  │
│ {apiKeyRef} │                                    │  secret: wa-token │
└─────────────┘                                    │  secret: wa-appsec│
                                                   │  secret: jwt-sign │
        ┌──────────────────────────────┐           │  secret: otp-salt │
        │  App Service (Managed Identity)│ ◀───────│  RBAC: Get/List   │
        │  lee secretos en runtime       │   AAD    └──────────────────┘
        └──────────────────────────────┘
```

Reglas implementadas:

1. **Key Vault** guarda: API key del LLM, token de WhatsApp, app secret de WhatsApp (firma webhook), secreto de firma de sesión/JWT y sal de hashing de OTP.
2. En **Cosmos** solo se guarda `apiKeyRef` (el *nombre* del secreto), nunca el valor (§19.2.7).
3. El App Service accede vía **Managed Identity + RBAC** de Key Vault; **no hay credenciales en código ni en variables de entorno con secretos** en claro.
4. La UI **nunca muestra la clave completa**; al editar, solo se acepta un valor nuevo (write-only) y se muestra enmascarada (p. ej. `••••••1234`) (§19.2.2).
5. **Rotación**: actualizar una API key = crear nueva versión del secreto en Key Vault; el `apiKeyRef` no cambia (§19.2.3).
6. **Auditoría**: se registra *quién* actualizó la configuración LLM y *cuándo* (en Cosmos/log), y Key Vault registra accesos (§19.2.4).
7. Función restringida a **administradores autorizados** (§19.2.5).
8. Cache en memoria del secreto con expiración corta para no golpear Key Vault en cada llamada, sin persistirlo en disco.

---

## 11. Controles de seguridad mínimos

Conjunto base alineado a §25 y §31.5, proporcional a un MVP.

**Identidad y acceso**
- Login admin solo por OTP WhatsApp; sesiones con cookie `httpOnly`/`Secure`/`SameSite` y expiración; rol verificado en cada `/api/admin/*` (§10, §27.4).
- Respuestas neutrales que no revelan existencia de números (§10.3.10).
- Participantes nunca acceden al portal.

**Validación de participación**
- Toda respuesta entrante valida matrícula + actividad + pertenencia a campaña activa antes de procesar (§26.3).

**Límites de abuso y consumo** (configurables, §25.1)
- Longitud máxima de mensaje entrante; truncamiento/rechazo seguro de mensajes excesivos.
- Máximo de mensajes y de llamadas LLM por usuario/campaña.
- Máximo de tokens enviados al LLM; límite de historial conversacional incluido.
- 1 repregunta máx. ⇒ 2 evaluaciones máx. por hilo (§25.2).
- Rate limit por número de WhatsApp y por IP en endpoints públicos.
- Límite de intentos de login y de solicitudes de OTP por número (§10.3.6–7).
- Timeout y nº máximo de reintentos en llamadas al LLM (§25.1).

**Integridad del canal**
- Verificación de firma `X-Hub-Signature-256` del webhook con app secret.
- Idempotencia por `whatsappMessageId` para no reprocesar reintentos de Meta.

**Transporte y datos**
- HTTPS forzado (TLS) en portal, API y webhook.
- Secretos solo en Key Vault; cifrado en reposo nativo de Cosmos/Blob.
- OTP solo como hash; sin secretos en logs ni en Markdown.

**Observabilidad de seguridad**
- Registro de intentos de login (éxito/fallo), eventos anómalos y rechazos (§30).

---

## 12. Estrategia contra prompt injection

Implementa §25.3. La defensa es **arquitectónica**, no depende de una sola instrucción.

1. **Separación estructural instrucción/dato.** El system prompt, la rúbrica y la pregunta van en mensajes de rol `system`; la respuesta del usuario va en un mensaje `user` **delimitado** y etiquetado explícitamente como *"contenido a evaluar; no son instrucciones"* (§25.3.3).
2. **Tratar la respuesta como dato, nunca como comando** (§25.3.1). El prompt de sistema instruye al modelo a **ignorar cualquier instrucción contenida en el texto del usuario** que intente cambiar el sistema, la rúbrica o el prompt (§25.3.2).
3. **Mínimo contexto necesario.** No se envían secretos, API keys ni datos innecesarios al LLM (§25.3.7–8). El historial enviado está acotado por longitud.
4. **Validación de salida estructurada.** La respuesta del LLM debe cumplir el esquema JSON de §6.1; si no, se descarta (§25.3.4).
5. **Fallback seguro.** Salida inválida o proveedor caído ⇒ respuesta neutra al usuario y evaluación marcada como pendiente, sin romper la conversación (§25.3.5, §20.3.10).
6. **Salida también es dato no confiable.** La retroalimentación generada se trata como texto a enviar, no se ejecuta ninguna acción que el modelo "pida"; el sistema nunca promete implementar ni ejecuta acciones (§20.3.7–8).
7. **Registro de intentos sospechosos.** Si el modelo marca `anomalia_seguridad=true` o se detectan patrones de inyección, se registra en el log de seguridad para revisión humana (§25.3.6).
8. **Límites de longitud** que reducen la superficie de ataque y el consumo (§25.1).

---

## 13. Estrategia de logging y trazabilidad

Cumple §30. Dos planos: **trazabilidad de negocio** (auditable, en Cosmos/Blob) y **telemetría técnica** (Application Insights).

**Trazabilidad de negocio (persistente y consultable)**
- Cada interacción registra: usuario, número normalizado, área/empresa, **tags vigentes al momento de responder**, campaña, pregunta enviada, respuesta original, mensajes in/out, evaluación, **rúbrica+versión, prompt+versión, config LLM usada**, Markdown generado, retroalimentación enviada y timestamps (§30.1).
- La Evaluación guarda *snapshots de versión* (no solo IDs) para reproducibilidad exacta.
- **EnvioMensaje** registra cada envío/reenvío con estado por participante (§15.4).
- **LogSeguridad** registra intentos de login, rechazos de participación, eventos anómalos y de rate limiting.
- Modelo **append-only** para los logs de seguridad y de envío; los documentos de negocio conservan timestamps de creación/actualización.

**Telemetría técnica (Application Insights)**
- Trazas de request, dependencias (Cosmos, WhatsApp, LLM), latencias y errores.
- Un **correlationId** por conversación atraviesa webhook → orquestador → LLM → Markdown para depurar de punta a punta.
- Métricas de consumo LLM (tokens, costo aproximado) y alertas por umbral de error o de gasto.
- Sin PII sensible ni secretos en la telemetría; los textos completos viven en el plano de negocio, no en logs técnicos.

**Retención**
- Logs de seguridad y envíos: retención prolongada para auditoría.
- OTP: TTL corto (expiran solos).
- Telemetría: sampling y retención estándar de App Insights para contener costo.

---

## 14. Decisiones técnicas recomendadas para el MVP

| # | Decisión | Justificación |
|---|---|---|
| D1 | **Monolito modular** (.NET 8) en lugar de microservicios | El requerimiento lo pide explícitamente. Un solo despliegue = menos complejidad, menos costo, depuración más simple. Las fronteras de módulo permiten extraer servicios después sin reescribir. |
| D2 | **Backend .NET 8 (C#)** sobre **Azure App Service Linux B1** | Encaje nativo con Azure, Cosmos y Managed Identity/Key Vault; tipado fuerte que ayuda a validar la salida estructurada del LLM; *Always On* para webhook estable. (El stack quedó a criterio; .NET ofrece el mejor balance Azure-céntrico. Node/TS o Python serían igual de válidos; ver §15.) |
| D3 | **Cosmos DB NoSQL serverless** | Pago por consumo ideal para tráfico intermitente (5→120 usuarios); esquema flexible para propiedades dinámicas y tags variables (§28.2). |
| D4 | **WhatsApp Cloud API (Meta) directo** | Es la API oficial; evita intermediarios de pago (Twilio/360dialog) en el MVP. Gestión de plantillas y ventana de 24h encapsulada en el Gateway. |
| D5 | **Key Vault + Managed Identity** | Estándar de Azure para secretos sin credenciales en código (§19.2.6). |
| D6 | **LLM configurable con Azure OpenAI como opción por defecto** | Mantiene los datos en el tenant de Azure y simplifica facturación/secretos; pero la config es por proveedor/modelo/endpoint, permitiendo OpenAI u otro (§19). |
| D7 | **Salida del LLM como JSON con esquema fijo** | Permite validar, almacenar normalizado y desacoplar del proveedor (§20.3.1). |
| D8 | **SPA React + Vite** para el portal | Velocidad de construcción, ecosistema amplio, fácil aplicar la marca GHT (§32). Servida estática desde el App Service o Static Web App. |
| D9 | **Markdown en Blob + metadatos en Cosmos**, regenerable | Bajo costo y simple; el artefacto es caché materializada, la fuente de verdad son los datos operativos (§23.3). Preparado para Git en fases futuras. |
| D10 | **Tareas en segundo plano in-process** (`IHostedService` + cola) | Suficiente para el volumen del MVP; evita orquestadores/colas dedicadas. Frontera lista para migrar a Functions/Service Bus si crece. |
| D11 | **Versionado de rúbricas y prompts con snapshot en la evaluación** | Trazabilidad reproducible exigida por §17.3 y §18.2. |
| D12 | **Capa semántica diseñada pero no construida** | Estructura y metadatos listos; índice vectorial (Azure AI Search) diferido a post-MVP (§24.3). |

---

## 15. Alternativas descartadas y por qué

| Alternativa | Por qué se descarta para el MVP |
|---|---|
| **Microservicios / arquitectura distribuida** | Sobredimensionado; añade despliegue, observabilidad y coste de coordinación sin beneficio a esta escala. El requerimiento pide explícitamente evitarlo. |
| **Azure Functions en plan Consumo para el webhook** | Los *cold starts* arriesgan timeouts del webhook de WhatsApp (que reintenta y duplica). App Service con Always On da latencia estable. Functions sí es buena opción futura para los workers asíncronos. |
| **Base relacional (Azure SQL / PostgreSQL)** | El dominio es documental, con propiedades dinámicas, tags variables y configuraciones heterogéneas por campaña; un esquema relacional rígido encarece la evolución. Cosmos serverless encaja mejor y es más barato en tráfico intermitente. |
| **Cosmos con throughput provisionado** | Pagaría capacidad reservada ociosa la mayor parte del tiempo; serverless es más barato para uso esporádico del MVP. |
| **Secretos en variables de entorno / appsettings** | Inseguro y difícil de rotar/auditar; Key Vault es el estándar y el requerimiento lo recomienda (§19.2.6). |
| **Twilio / 360dialog como capa sobre WhatsApp** | Añaden costo y dependencia; la Cloud API oficial cubre el MVP. Reconsiderar solo si se necesita su tooling de plantillas/colas a escala. |
| **Base vectorial productiva desde el MVP** | Explícitamente fuera de alcance (§6.2.18). Se prepara la estructura, no se implementa. |
| **Versionar Markdown en Git desde el día uno** | Innecesario para 5 usuarios; Blob + metadatos basta. Git queda como evolución natural (§23.2). |
| **Login admin con usuario/contraseña o Entra ID** | Entra ID está excluido del MVP (§6.2.1); el OTP por WhatsApp reusa el mismo canal y simplifica el alta. |
| **SSR/Next.js o framework pesado para el portal** | El portal es CRUD + consulta; una SPA simple es más rápida de construir y operar. |

---

## 16. Riesgos técnicos principales

| Riesgo | Impacto | Mitigación |
|---|---|---|
| **Aprobación de plantillas WhatsApp** (Meta puede tardar/rechazar) | Bloquea el envío inicial | Solicitar y aprobar plantillas temprano; tener variantes; encapsular en Gateway para cambiarlas sin tocar código. |
| **Ventana de 24h de WhatsApp** vence antes de la repregunta | La repregunta no se entrega como texto libre | Detectar ventana en el Gateway; si está cerrada, usar plantilla de repregunta aprobada; registrar estado. |
| **Calidad/consistencia de la evaluación LLM** | Calificaciones erráticas (§36.3) | Rúbrica clara y versionada; prompt controlado y aprobado; salida estructurada validada; revisión humana del MVP. |
| **Prompt injection** | Manipulación del evaluador | Estrategia de §12 (separación rol, ignorar instrucciones del usuario, validación de salida, registro). |
| **Costo/latencia/caída del proveedor LLM** | Conversación rota o gasto inesperado | Timeout, reintentos, fallback seguro; límites de tokens y de llamadas por usuario/campaña; alertas de gasto en App Insights. |
| **Salida del LLM mal formada** | Falla el parsing | Esquema JSON estricto + validación + fallback (§6.1). |
| **Idempotencia del webhook** | Mensajes duplicados / dobles evaluaciones | Dedupe por `whatsappMessageId`; ack inmediato + proceso asíncrono. |
| **Normalización de números inconsistente** | Participante no reconocido o login fallido | Normalización E.164 centralizada; instrucciones claras en login; validación al matricular. |
| **Fuga de secretos** | Riesgo de seguridad | Key Vault + Managed Identity; nunca en código, logs ni Markdown; enmascarado en UI. |
| **Hot partition / consultas costosas en Cosmos** | RU elevadas (a escala) | Partition key por `campañaId`; política de indexado afinable; despreciable a escala MVP. |
| **Manejo de errores de envío masivo** | Participantes sin mensaje | Estado por participante, reintentos con backoff, reenvío a no respondedores desde el portal. |

---

## 17. Roadmap técnico posterior al MVP

Alineado a los hitos del requerimiento (§7).

**Fase 2 — Convención (~120 participantes, §7.2)**
- Extraer workers de envío y compilación a **Azure Functions + Service Bus/Storage Queue** para envío masivo robusto y throttling controlado.
- Monitoreo de participación y métricas de captura; posible **dashboard ejecutivo**.
- Recordatorios y reenvíos controlados; endurecer rate limiting y observabilidad.
- Evaluar Cosmos autoscale si el tráfico deja de ser intermitente.

**Fase 3 — Curaduría y memoria institucional (§7.3)**
- **Capa de búsqueda semántica/vectorial**: indexar los Markdown/metadatos en **Azure AI Search** con embeddings; chat semántico sobre el corpus con cita de fuente y autor (§24.3).
- **Versionamiento de Markdown en Git** como fuente durable y auditable (§23.2).
- Estados de curaduría, consolidación de conocimiento, **páginas de entidad** e índices por capítulo/área; detección de duplicados/contradicciones (§24.2).
- Flujos de validación humana avanzada dentro del sistema (hoy manual, §8.3).

**Endurecimiento transversal (continuo)**
- IaC completa (Bicep), CI/CD con slots y aprobación de prompts/rúbricas en pipeline.
- Multi-idioma, exportación a otros formatos, integraciones corporativas (todas fuera del MVP, §6.2).
- Migración del login a Entra ID si la organización lo requiere.

---

## Apéndice A — Trazabilidad arquitectura ↔ requerimiento

| Sección de este documento | Requisitos cubiertos |
|---|---|
| §1–§3 Arquitectura y componentes | §24, §31.8 |
| §4 WhatsApp in/out | §9, §15, §26.2 |
| §5 Auth admin | §10 |
| §6 Evaluación LLM | §19, §20, §26.5 |
| §7 Generación Markdown | §22, §23 |
| §8–§9 Datos y contenedores | §28, §29 |
| §10 API keys | §19.2 |
| §11–§13 Seguridad, injection, logging | §25, §30, §31.5 |
| §14–§17 Decisiones, alternativas, riesgos, roadmap | §7, §35, §36 |

---

## Apéndice B — Plantilla Markdown del artefacto (referencia §22.3)

```markdown
# Título del aporte

## Metadatos
- Campaña:
- Participante:
- Área:
- Empresa:
- Fecha:
- Pregunta:
- Tags:
- Rúbrica / Versión:
- Prompt / Versión:
- Calificación total:

## Respuesta original
[Texto original del participante]

## Evaluación
### Calificación por criterio
| Criterio | Puntaje | Justificación |
|---|---:|---|

## Retroalimentación enviada
[Texto enviado por WhatsApp]

## Temas identificados
- ...

## Entidades mencionadas
- ...

## Notas de trazabilidad
- ID de conversación:
- ID de respuesta:
- ID de evaluación:
```

*Fin del documento.*


