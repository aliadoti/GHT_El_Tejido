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

Request de creación (ejemplo):
```json
{ "nombre": "Ana Pérez", "numero": "573001112233", "rol": "participante", "area": "Operaciones", "empresa": "GHT", "tags": ["t_area_oper"], "propiedadesDinamicas": {} }
```
El backend **normaliza** el número (`06 §2`); si el formato es inválido → `400`.

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
| POST | `/api/admin/rubricas` | Crea v1 (sube Markdown; parsea criterios/pesos/escala). |
| GET | `/api/admin/rubricas/{id}` | Última versión activa. |
| GET | `/api/admin/rubricas/{id}/versiones` | Lista versiones. |
| POST | `/api/admin/rubricas/{id}/versiones` | Nueva versión (no muta las previas). |
| PATCH | `/api/admin/rubricas/{id}/estado` | Activa/archiva. |

### 5.6 Prompts — `REQ §18`
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/prompts` | Lista (`tipoPrompt, estado`). |
| POST | `/api/admin/prompts` | Crea v1 (estado `borrador`, sin aprobar). |
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
