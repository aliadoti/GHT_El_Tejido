# 04 — Contrato de API REST

**Propósito:** fuente de verdad de las interfaces HTTP entre frontend, WhatsApp/Meta y el backend. Implementa el Edge de `ARQ §1.1` y `§3`. El frontend (`11`) consume `/api/*` exactamente como aquí se define.

**Regla:** ningún cambio de forma de request/response se implementa sin actualizar primero este documento.

---

## 1. Convenciones generales

- Base URL: la del App Service. Prefijos: `/api/auth`, `/api/admin`, `/webhook/whatsapp`.
- **JSON** en request y response (`Content-Type: application/json; charset=utf-8`), salvo el webhook GET.
- Estilo: se admite Controllers o Minimal APIs (`02 §3`); el contrato es el mismo.
- **Fechas** ISO 8601 UTC. **IDs** como string.
- **Autenticación admin:** cookie de sesión `httpOnly`/`Secure`/`SameSite=Strict` emitida en login. Toda ruta `/api/admin/*` exige sesión válida + rol (`admin` o `visor` según permiso). El `visor` solo accede a endpoints de consulta (GET).
- **CSRF:** al usar cookie de sesión, las mutaciones (`POST/PUT/PATCH/DELETE`) exigen header anti-CSRF (`X-CSRF-Token`) emitido al iniciar sesión. (Alternativa: token en header `Authorization: Bearer` si se opta por JWT en almacenamiento en memoria; el MVP usa cookie + CSRF.)
- **Versionado de API:** prefijo implícito v1 en MVP; si se versiona, `/api/v1/...`. No requerido ahora.
- **Idempotencia de mutaciones sensibles** (envíos): aceptar header opcional `Idempotency-Key`.

---

## 2. Paginación, filtros y orden (endpoints de listado)

- Query params estándar: `?page=1&pageSize=25&sort=fecha:desc`.
- `pageSize` máximo 100 (default 25).
- Respuesta de listado:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 25,
  "total": 130,
  "continuationToken": "..."   // opcional, si se usa paginación nativa de Cosmos
}
```
- Filtros específicos por recurso se documentan en cada sección. Los filtros de consulta de resultados (`REQ §27.3`) son: `campaniaId, usuarioId, numero, area, empresa, tag, preguntaId, categoria, estado, calificacionMin, calificacionMax, fechaDesde, fechaHasta, estadoEnvio, estadoRespuesta, tema, entidad`.

---

## 3. Modelo de errores (uniforme)

Todos los errores devuelven este cuerpo, con el HTTP status adecuado (basado en RFC 7807 simplificado):

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "El número no tiene formato E.164.",
    "details": [ { "field": "numero", "issue": "formato" } ],
    "correlationId": "corr_..."
  }
}
```

| HTTP | `code` | Cuándo |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Payload inválido. |
| 401 | `UNAUTHENTICATED` | Sin sesión válida. |
| 403 | `FORBIDDEN` | Rol insuficiente. |
| 404 | `NOT_FOUND` | Recurso inexistente. |
| 409 | `CONFLICT` | Estado inválido (p. ej. enviar en campaña no activa) o duplicado (número ya existe). |
| 422 | `BUSINESS_RULE` | Regla de negocio violada. |
| 429 | `RATE_LIMITED` | Límite de abuso/consumo (incluye `Retry-After`). |
| 500 | `INTERNAL_ERROR` | Fallo no controlado (sin filtrar detalles). |
| 502/503 | `UPSTREAM_ERROR` | Fallo de WhatsApp/LLM aguas arriba. |

**Importante (seguridad):** los endpoints de auth **no** revelan existencia de números (`REQ §10.3.10`): responden de forma neutral aunque el número no exista (ver §4).

---

## 4. Endpoints de autenticación admin (`/api/auth/*`)

Implementa `REQ §10` / `ARQ §5`. Detalle de lógica en `06 §4`.

### 4.1 `POST /api/auth/request-code`
Solicita el envío de un OTP por WhatsApp.

Request:
```json
{ "numero": "573001119999" }
```
Response **siempre 200** (neutral, no revela existencia):
```json
{ "message": "Si el número está habilitado, recibirás un código por WhatsApp." }
```
- Aplica rate limit por número y por IP (`429` si excede; aún así, el mensaje no revela existencia — se puede responder 200 con mensaje neutral salvo abuso evidente).
- Si y solo si existe admin válido, genera y envía el OTP (ver `06 §4`).

### 4.2 `POST /api/auth/verify-code`
Verifica el OTP e inicia sesión.

Request:
```json
{ "numero": "573001119999", "codigo": "482913" }
```
Response 200 (éxito): emite cookie de sesión + CSRF.
```json
{
  "usuario": { "id": "u_admin1", "nombre": "Admin", "rol": "admin" },
  "csrfToken": "...",
  "expiraEn": "2026-06-12T16:09:00Z"
}
```
Errores: `401 UNAUTHENTICATED` (código inválido/vencido/usado), `429 RATE_LIMITED` (intentos excedidos). Mensajes neutrales.

### 4.3 `POST /api/auth/logout`
Invalida la sesión. Response `204`.

### 4.4 `GET /api/auth/me`
Devuelve el usuario de la sesión actual (para que el SPA restaure estado). `200` con `{ usuario }` o `401`.

---

## 5. Endpoints administrativos (`/api/admin/*`)

> Todos exigen sesión. **Mutaciones**: rol `admin`. **Lectura (GET)**: rol `admin` o `visor`. Cada recurso lista, crea, lee, actualiza y cambia estado. Se muestran las firmas; los cuerpos siguen el modelo de datos de `03`.

### 5.1 Usuarios — `REQ §12`, `§8.2`
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/usuarios` | Lista/filtra (`rol, estado, area, empresa, tag, q`(nombre/número)). |
| POST | `/api/admin/usuarios` | Crea usuario (participante/admin/visor). Valida número E.164 único (`409` si duplicado). |
| GET | `/api/admin/usuarios/{id}` | Detalle. |
| PUT | `/api/admin/usuarios/{id}` | Actualiza datos, área, empresa, tags, propiedades. |
| PATCH | `/api/admin/usuarios/{id}/estado` | Activa/inactiva. |
| POST | `/api/admin/usuarios/carga-masiva` | Alta/actualización en lote desde archivo (`I-08`). Ver sub-sección. |

Request de creación (ejemplo):
```json
{ "nombre": "Ana Pérez", "numero": "573001112233", "rol": "participante", "area": "Operaciones", "empresa": "GHT", "tags": ["t_area_oper"], "propiedadesDinamicas": {} }
```
El backend **normaliza** el número (`06 §2`); si el formato es inválido → `400`.

#### Carga masiva de participantes — `I-08`, `REQ §12`, `§26.3`
> Cambio **aditivo** (una ruta nueva). No modifica `03`: usa las entidades existentes
> `Usuario`/`Tag`/`ParticipanteCampania`. El alta individual (`POST /api/admin/usuarios`) sigue
> disponible sin cambios. Sprint 1a entrega **solo CSV** (sin dependencia nueva); `.xlsx` queda como
> lector pluggable para una entrega posterior (`I-08 §7`).

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/admin/usuarios/carga-masiva` | Sube un archivo de participantes y hace **upsert por número normalizado** (`06 §2`). `multipart/form-data`, rol `admin` + CSRF. Una fila mala **no aborta** el lote. |

**Request** (`multipart/form-data`):
- Campo `archivo` (requerido): el archivo `.csv` (UTF-8). Tamaño máximo configurable
  (`Seguridad:CargaMasivaMaxBytes`, default **2 MB**); si se excede → `400`.
- Query o campo `campaniaId` (opcional): si se envía, los usuarios creados/actualizados se **asocian**
  a esa campaña al terminar el lote (reutiliza la asociación de `§5.3`; campaña inexistente → `404`).

**Plantilla CSV** (fila de cabecera obligatoria, columnas fijas; `Tags` separadas por `;`):
```csv
Nombre,WhatsApp,Area,Empresa,Tags
Ana Perez,573001112233,Operaciones,GHT,t_area_oper;t_lider
```

**Response `200`** — reporte por fila (sin PII: solo `usuarioId`, resultado y motivo):
```json
{
  "totalFilas": 3,
  "creados": 2,
  "actualizados": 0,
  "rechazados": 1,
  "asociados": 2,
  "filas": [
    { "fila": 2, "resultado": "creado",     "usuarioId": "u_8f3c...", "motivo": null },
    { "fila": 3, "resultado": "actualizado", "usuarioId": "u_1a2b...", "motivo": null },
    { "fila": 4, "resultado": "rechazado",   "usuarioId": null,        "motivo": "numero_invalido" }
  ]
}
```
- `resultado` ∈ `creado | actualizado | rechazado`. `motivo` (solo en `rechazado`) ∈
  `fila_incompleta` (falta `Nombre/WhatsApp/Area/Empresa`), `numero_invalido` (no normaliza a E.164),
  `duplicado_en_archivo` (número repetido en el archivo: **el primero gana**, el resto se rechaza).
- **Idempotencia:** re-subir el mismo archivo produce `actualizado` (no duplica). Las `Tags` que no
  existan en el catálogo se **crean** (`tipoTag=importado`).
- La operación queda auditada en `LogSeguridad` (`AccionAdministrativa`, acción `carga_masiva`) con
  conteos y `correlationId`; **sin números ni nombres**.

### 5.2 Tags — `REQ §13`
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/tags` | Lista (`tipoTag, estado`). |
| POST | `/api/admin/tags` | Crea tag parametrizable. |
| PUT | `/api/admin/tags/{id}` | Edita. |
| PATCH | `/api/admin/tags/{id}/estado` | Activa/desactiva. |

### 5.3 Campañas — `REQ §11`
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/campanias` | Lista (`estado, q`). |
| POST | `/api/admin/campanias` | Crea (estado inicial `borrador`). |
| GET | `/api/admin/campanias/{id}` | Detalle completo (incluye mensajes y preguntas embebidos). |
| PUT | `/api/admin/campanias/{id}` | Actualiza datos y configs (rúbrica, prompts, LLM, markdown, seguridad). |
| PATCH | `/api/admin/campanias/{id}/estado` | Cambia estado (`borrador→activa→cerrada→archivada`). Valida transición. |
| POST | `/api/admin/campanias/{id}/duplicar` | Clona como plantilla (`REQ §11.3.8`). |

Campos de configuración conversacional (aditivos; documento viejo/campo ausente conserva comportamiento actual):
```json
{
  "configConversacional": {
    "maxRepreguntas": 1,
    "mensajeCierre": "Gracias. Tu aporte quedó registrado correctamente.",
    "segmentacionIdeas": false,
    "parafraseo": false
  }
}
```
- `segmentacionIdeas` (`I-06`, default `false`): si está en `true` y el kill-switch global
  `Conversacion:SegmentacionIdeas` no lo apaga, el backend puede separar un mensaje con varias ideas en
  N respuestas/evaluaciones/Markdown. El portal lo expone como checkbox en Configuración de campaña.
- `parafraseo` (`I-05`, default `false`): si está en `true` y `Conversacion:Parafraseo` no lo apaga,
  el evaluador solicita y el orquestador antepone un resumen fiel del aporte a la retroalimentación.
  Campo ausente = retro clásica; ambos flags permiten rollback sin redeploy.

#### Sub-recursos embebidos de campaña
| Método | Ruta | Descripción |
|---|---|---|
| GET/POST | `/api/admin/campanias/{id}/mensajes-iniciales` | Lista/crea mensaje inicial (`REQ §15`). |
| PUT/DELETE | `/api/admin/campanias/{id}/mensajes-iniciales/{miId}` | Edita/elimina. |
| GET/POST | `/api/admin/campanias/{id}/preguntas` | Lista/crea pregunta (`REQ §16`). |
| PUT/DELETE | `/api/admin/campanias/{id}/preguntas/{pId}` | Edita/elimina. |

#### Participantes de campaña — `REQ §14`
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/campanias/{id}/participantes` | Lista participantes asociados + estado de envío/respuesta. |
| POST | `/api/admin/campanias/{id}/participantes` | Asocia usuarios (por ids, o por filtro área/empresa/tag/búsqueda). |
| DELETE | `/api/admin/campanias/{id}/participantes/{usuarioId}` | Desasocia. |
| GET | `/api/admin/campanias/{id}/participantes/preview` | Dado un filtro, devuelve cuántos y quiénes recibirían (`REQ §27.2`). |

Asociar por filtro (ejemplo):
```json
{ "filtro": { "area": "Operaciones", "tags": ["t_emp_ght"], "estado": "activo" } }
```

#### Reinicio de datos de prueba — `P-03`, `REQ §26`
> Cambio **aditivo** (dos rutas nuevas). Borra físicamente lo producido por las interacciones
> (conversaciones, mensajes, respuestas, evaluaciones, artefactos Markdown y su blob) y resetea el
> estado de los participantes, **conservando** la campaña, su configuración y los usuarios. Habilita
> repetir las pruebas humanas del flujo sin recrear la campaña (cold-start real, `Reglas §2.1`).

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/admin/campanias/{id}/participantes/{usuarioId}/reiniciar` | Reinicia los datos de un participante. Cuerpo opcional `{ "reiniciarEnvios": bool }` (default `false`). |
| POST | `/api/admin/campanias/{id}/reiniciar-datos` | Reinicia los datos de toda la campaña. Cuerpo opcional `{ "usuarioIds": [..], "reiniciarEnvios": bool }` (`usuarioIds` acota a un subconjunto; vacío/ausente = todos). Gateado por `Seguridad:PermitirReinicioDatos` (default `true`): si está en `false` responde **409 CONFLICT**. |

Ambos responden **200** con el reporte de conteos:
```json
{ "conversaciones": 1, "mensajes": 3, "respuestas": 1, "evaluaciones": 1, "artefactos": 1, "blobsBorrados": 1, "blobsFallidos": 0, "participantesReseteados": 1 }
```
Reset de participante (`03 §3.4`, campos existentes): `estadoRespuesta=sinRespuesta`, `fechaUltimaRespuesta=null`; con `reiniciarEnvios=true` además `estadoEnvio=pendiente` y `fechaPrimerEnvio=null` (permite re-disparar el envío inicial desde Envíos). La acción queda auditada en `LogSeguridad` (`AccionAdministrativa`) con conteos y `correlationId`; sin PII.

### 5.4 Envíos — `REQ §15`, `§26.2`
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/admin/campanias/{id}/envios` | Dispara envío de mensajes iniciales a participantes seleccionados. Campaña DEBE estar `activa` (`409` si no). Encola jobs; responde `202 Accepted` con `jobId`. |
| POST | `/api/admin/campanias/{id}/envios/reenviar` | Reenvía a quienes no respondieron (`estadoRespuesta=sinRespuesta`). |
| GET | `/api/admin/campanias/{id}/envios` | Estado de envío por participante (`enviado/error/pendiente`), errores (`REQ §27.2`). |
| POST | `/api/admin/campanias/{id}/envios/reintentar` | Reintenta los `error`. |

Request de envío:
```json
{ "participantes": ["u_8f3c...", "u_1a2b..."], "mensajeInicialId": "mi_1" }
```
Response `202`:
```json
{ "jobId": "job_...", "encolados": 5, "estado": "enProceso" }
```

### 5.5 Rúbricas — `REQ §17`
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/rubricas` | Lista (`estado`). |
| POST | `/api/admin/rubricas` | Crea v1 (sube Markdown; parsea criterios/pesos/escala). Estado por defecto del portal: `borrador`. |
| GET | `/api/admin/rubricas/{id}` | Última versión activa. |
| PUT | `/api/admin/rubricas/{id}` | Edita **en sitio** la versión vigente. Solo si está en `borrador`; si no, responde `409 CONFLICT` (usar `/versiones`). No incrementa versión. |
| GET | `/api/admin/rubricas/{id}/versiones` | Lista versiones. |
| POST | `/api/admin/rubricas/{id}/versiones` | Nueva versión (no muta las previas). |
| PATCH | `/api/admin/rubricas/{id}/estado` | `borrador`/`activa`/`archivada`. |

### 5.6 Prompts — `REQ §18`
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/prompts` | Lista (`tipoPrompt, estado`). |
| POST | `/api/admin/prompts` | Crea v1 (estado `borrador`, sin aprobar). |
| PUT | `/api/admin/prompts/{id}` | Edita **en sitio** la versión vigente. Solo si está en `borrador` (sin aprobar); si no, responde `409 CONFLICT` (usar `/versiones`). No incrementa versión. |
| POST | `/api/admin/prompts/{id}/versiones` | Nueva versión. |
| POST | `/api/admin/prompts/{id}/aprobar` | Aprobación humana (`aprobadoPor`, `fechaAprobacion`). Sin esto no se usa en campaña (`REQ §18.3.6`). |
| PATCH | `/api/admin/prompts/{id}/estado` | Activa/inactiva. |

### 5.7 Configuración LLM — `REQ §19`
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/config-llm` | Lista configs. La API key **nunca** se devuelve; solo `apiKeyRef` + máscara. |
| POST | `/api/admin/config-llm` | Crea config. **No recibe la API key**: solo `apiKeyRef`, el nombre de un secreto que **ya debe existir** en Key Vault con la key real (`REQ §19.2`). |
| PUT | `/api/admin/config-llm/{id}` | Edita parámetros y/o cambia `apiKeyRef` (a otro secreto existente). Para rotar la key se actualiza el secreto en Key Vault fuera de la app. |
| PATCH | `/api/admin/config-llm/{id}/estado` | Activa/inactiva. |

Crear/editar (la app **referencia** un secreto, no lo recibe ni lo escribe):
```json
{ "nombre": "LLM", "proveedor": "openrouter.ai", "modelo": "deepseek/deepseek-chat", "endpoint": "https://openrouter.ai/api/v1", "apiKeyRef": "llm-key", "parametros": { "temperature": 0.2 }, "timeoutSegundos": 30, "maxReintentos": 2 }
```
> **Cambio de contrato (2026-06-15, modelo de mínimo privilegio):** se eliminó el campo `apiKey` del request. El backend **no escribe** secretos: valida que `apiKeyRef` exista y sea legible (si no, responde `400 VALIDATION_ERROR` con detalle `apiKeyRef`), y persiste solo `apiKeyRef`. La API key real la carga un humano/operación en Key Vault. La identidad del App Service solo necesita **Key Vault Secrets User** (lectura). La respuesta nunca incluye la key (`REQ §19.2.2`). Ver `AVANCES.md` → Contratos y `SUPUESTOS.md#configllm-apikeyref-solo-lectura`.

### 5.8 Consultas de resultados — `REQ §27.3`
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/conversaciones` | Lista/filtra conversaciones. |
| GET | `/api/admin/conversaciones/{id}` | Detalle con mensajes in/out. |
| GET | `/api/admin/respuestas` | Lista/filtra (todos los filtros de `§2`). |
| GET | `/api/admin/respuestas/{id}` | Respuesta + evaluación asociada. |
| GET | `/api/admin/evaluaciones/{id}` | Evaluación completa (calificación, explicación, versiones). |
| GET | `/api/admin/markdown` | Lista artefactos Markdown (`campaniaId, tipoArtefacto, usuarioId, preguntaId`). |
| GET | `/api/admin/markdown/{id}` | Contenido Markdown + metadatos. |
| POST | `/api/admin/markdown/{id}/regenerar` | Regenera el artefacto desde datos operativos (`REQ §22.4.6`). |
| GET | `/api/admin/markdown/{id}/raw` | Descarga el `.md` (text/markdown). |

I-05 añade `parafraseoDevuelto` opcional al detalle de evaluación que devuelven
`/respuestas/{id}` y `/evaluaciones/{id}`. `null`/ausente significa que la campaña no lo tenía
activo o que la salida del LLM no produjo un resumen utilizable; conserva compatibilidad de lectura.

Campos aditivos de respuesta para I-06:
```json
{
  "id": "resp_wamidabc_1",
  "texto": "Idea segmentada...",
  "ideaIndice": 1,
  "respuestaPadreId": "wamid.abc"
}
```
- `ideaIndice`/`respuestaPadreId` solo aparecen poblados en respuestas segmentadas; clientes existentes
  pueden ignorarlos.
- Los endpoints de Markdown no cambian: cada idea segmentada produce un artefacto `tipoArtefacto=respuesta`
  con `respuestaRef` propio.

### 5.9 Catálogos auxiliares
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/jobs/{jobId}` | Estado de un job de envío/compilación (para el portal). |

---

## 6. Webhook de WhatsApp (`/webhook/whatsapp`)

Implementa `ARQ §4.2`. Detalle en `05 §3`.

### 6.1 `GET /webhook/whatsapp` (verificación de Meta)
Query: `hub.mode=subscribe&hub.verify_token=<token>&hub.challenge=<n>`.
- Si `hub.verify_token` coincide con el secreto configurado → responde **200** con el cuerpo `hub.challenge` (texto plano).
- Si no coincide → **403**.

### 6.2 `POST /webhook/whatsapp` (mensajes entrantes)
- **Verifica** la firma `X-Hub-Signature-256` (HMAC-SHA256 con el app secret de Key Vault). Si falla → **401** y se descarta.
- Responde **200 OK inmediatamente** (ack a Meta) y **encola** el procesamiento (`ARQ §4.2`). Nunca procesa síncrono dentro del request.
- Cuerpo: el payload estándar de WhatsApp Cloud API (objeto `entry[].changes[].value.messages[]`). El Gateway (`05 §2`) lo parsea.

---

## 7. `GET /health`
Devuelve `200` con `{ "status": "ok" }` si el proceso está vivo. Puede incluir checks ligeros (Cosmos reachable) sin exponer detalles sensibles. Usado por App Service y CI smoke test.

---

## 8. Notas de seguridad transversales (recordatorio)
- HTTPS forzado; HSTS.
- Rate limiting en `/api/auth/*` y webhook (`10 §3`).
- Respuestas de auth neutrales (`REQ §10.3.10`).
- `correlationId` en toda respuesta de error y propagado en logs (`10 §6`).
- La API **nunca** devuelve secretos ni la API key del LLM.

*Fin del documento.*
