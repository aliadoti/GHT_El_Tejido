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

Esta regla aplica también a retornos directos de endpoints, filtros y rutas de diagnóstico: no se devuelven cuerpos vacíos, texto plano ni objetos de error ad hoc cuando la aplicación rechaza una solicitud. El encabezado `X-Correlation-Id` y `error.correlationId` deben estar presentes y coincidir; si la solicitud no trae un identificador válido, la infraestructura común genera uno. Un error que deliberadamente oculte información (por ejemplo, readiness sin clave o firma de webhook inválida) conserva el estado HTTP y un mensaje neutro, pero usa el mismo sobre de error y nunca expone secretos, firmas, excepciones ni estado interno. La remediación de las rutas ya existentes está planificada en `P-17`.

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
| GET | `/api/admin/usuarios` | Lista/filtra (`rol, estado, area, empresa, empresaId, sede, tag, idioma, q`(nombre/número/email/`codigoUsuario`)). |
| POST | `/api/admin/usuarios` | Crea usuario (participante/admin/visor). Valida número E.164 **único entre activos** (`409` si ya hay un activo con ese número). |
| GET | `/api/admin/usuarios/{id}` | Detalle. |
| PUT | `/api/admin/usuarios/{id}` | Actualiza datos, empresa, sede, cargo, tags, propiedades. **No** cambia `codigoUsuario` ni el número. |
| PATCH | `/api/admin/usuarios/{id}/estado` | Activa/inactiva. Activar falla con `409` si ya hay otro activo con el mismo número. |
| GET | `/api/admin/usuarios/por-numero/{numero}` | Histórico del número: el activo (si lo hay) + los inactivos, por `creadoEn` (`I-08 §3.1.f`). |
| POST | `/api/admin/usuarios/{id}/reasignar-numero` | Reasignación manual: inactiva al titular y crea el nuevo (`I-08 §4.4`). |
| POST | `/api/admin/usuarios/carga-masiva` | Alta/actualización en lote desde archivo (`I-08`). Ver sub-sección. |
| GET | `/api/admin/usuarios/plantilla-carga` | Descarga la plantilla vacía (`.xlsx`) con la cabecera oficial. |

Request de creación (ejemplo):
```json
{
  "nombre": "Ana Pérez", "numero": "573001112233", "rol": "participante",
  "email": "ana.perez@ght.com", "empresa": "Flores El Aljibe", "empresaId": "AL", "sede": "AL",
  "cargo": "Coordinadora", "area": "Operaciones", "antiguedadAnios": 16.391666, "idioma": "es",
  "usuarioWhatsapp": null, "tags": ["t_area_oper"], "propiedadesDinamicas": {}
}
```
- Obligatorios: **`nombre`** y **`numero`** (`I-08 §3`). `area`, `empresa`, `empresaId`, `sede`,
  `cargo`, `email` y `antiguedadAnios` son opcionales. `idioma` ∈ `es | en`, default `es`.
- El backend **normaliza** el número (`06 §2`); si el formato es inválido → `400`.
- `codigoUsuario` (`number`, secuencial legible como `U-000042`) es **de solo lectura**: lo asigna el
  servidor al crear y no cambia nunca (`03 §3.1.1`). Se devuelve en todas las respuestas de usuario.
- `usuarioWhatsapp` (`string?`, opcional) se captura **solo por API/portal**; la carga masiva lo ignora
  (`03 §3.1`). No participa aún en el enrutamiento.
- `email`, si viene, debe ser único **entre activos** → `409` si ya lo tiene otro usuario activo.

**Response de usuario (DTO)** — se agregan de forma **aditiva** `codigoUsuario`, `email`, `empresaId`,
`sede`, `cargo`, `antiguedadAnios`, `idioma` y `usuarioWhatsapp` al DTO existente. Los clientes que
ignoren los campos nuevos siguen funcionando.

**Reasignación manual** `POST /api/admin/usuarios/{id}/reasignar-numero`:
```json
{ "nombre": "NUEVO TITULAR", "email": null, "empresaId": "AL", "sede": "AL", "cargo": "GERENTE" }
```
Inactiva al usuario `{id}` y **crea uno nuevo** con el mismo número (nuevo `id`, nuevo
`codigoUsuario`, `estado = activo`), sin heredar rol, tags ni historial. Respuesta `201` con el usuario
nuevo y el `codigoUsuario` del anterior. El histórico de campañas queda colgado del `id` anterior. Los
dos pasos van ordenados (primero inactivar) por la unique key `/claveUnicidad` de `03 §3.1`; si el alta
falla, se revierte la inactivación → `409`/`500` sin dejar el número sin titular.

#### Carga masiva de participantes — `I-08`, `REQ §12`, `§26.3`
> **Revisión 2026-08-07 (I-08).** La plantilla anterior (`Nombre | WhatsApp | Area | Empresa | Tags`)
> **queda reemplazada** por la plantilla oficial de GHT, de 9 columnas. Se agregan los parámetros
> `modo` y `reasignaciones`, el resultado `reasignado` y motivos de rechazo nuevos. Esta revisión
> **sí** toca `03` (campos nuevos de `Usuario`, `codigoUsuario`, `claveUnicidad`, `Secuencia`) y
> soporta **`.xlsx`** además de `.csv`. El alta individual sigue disponible.

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/admin/usuarios/carga-masiva` | Sube un archivo de participantes y hace **upsert por número normalizado entre activos** (`06 §2`). `multipart/form-data`, rol `admin` + CSRF. Una fila mala **no aborta** el lote. |

**Request** (`multipart/form-data`):
- Campo `archivo` (requerido): `.xlsx` o `.csv` (UTF-8). Tamaño máximo configurable
  (`Seguridad:CargaMasivaMaxBytes`, default **2 MB**); si se excede → `400`. Otra extensión → `400`.
- Campo/query `campaniaId` (opcional): si se envía, los usuarios creados/actualizados se **asocian**
  a esa campaña al terminar el lote (reutiliza la asociación de `§5.3`; campaña inexistente → `404`).
- Campo/query `modo` (opcional) ∈ `upsert` (**default**) | `solo_actualizar`.
  En `solo_actualizar` **no se crea nada**: un teléfono sin usuario activo → `no_encontrado`.
- Campo `reasignaciones` (opcional, JSON): resolución de los conflictos de titular detectados en una
  llamada previa sobre **el mismo archivo** (ver más abajo).
  ```json
  [{ "fila": 7, "accion": "reasignar" }, { "fila": 9, "accion": "corregir_nombre" }]
  ```
  `accion` ∈ `reasignar` | `corregir_nombre` | `omitir`. Una fila no listada que vuelva a dar
  conflicto se reporta otra vez como `conflicto_titular` sin escribir nada.

**Plantilla oficial** (hoja única, **fila 1 = cabecera obligatoria**, 9 columnas en este orden exacto).
Descargable vacía desde `GET /api/admin/usuarios/plantilla-carga`. Equivalente CSV:
```csv
Empresa,ID Empresa,Sede,Nombre,Cargo,Email,Antigüedad en la empresa en años,Idioma,Telefono
Flores El Aljibe,AL,AL,CELY FARIAS EDGAR FELIPE,GERENTE 2 EAI,felipe.celyf@floreselaljibe.com,16.391666,es,573001112233
```
| Columna | Campo | Obligatorio |
|---|---|---|
| `Empresa` | `empresa` | No |
| `ID Empresa` | `empresaId` | No |
| `Sede` | `sede` | No |
| `Nombre` | `nombre` | **Sí** |
| `Cargo` | `cargo` | No |
| `Email` | `email` | No (único entre activos si viene) |
| `Antigüedad en la empresa en años` | `antiguedadAnios` (decimal) | No |
| `Idioma` | `idioma` (`es`\|`en`, default `es`) | No |
| `Telefono` | `whatsappNormalizado` — **clave de upsert** | **Sí** |

- Cabecera distinta o columnas fuera de orden → **`400`**, el lote no se procesa.
- **No hay columna `Tags`**: si `ID Empresa` viene, se asegura la tag `t_emp_<idEmpresa>`
  (`tipoTag=empresa`, creada si falta) sin borrar las tags puestas a mano.
- `codigoUsuario` y `usuarioWhatsapp` **nunca** se leen del archivo (`03 §3.1`).

**Response `200`** — reporte por fila (sin PII: solo ids, resultado y motivo):
```json
{
  "totalFilas": 4,
  "creados": 1,
  "actualizados": 1,
  "reasignados": 1,
  "rechazados": 1,
  "asociados": 3,
  "filas": [
    { "fila": 2, "resultado": "creado",       "usuarioId": "u_8f3c...", "codigoUsuario": 131, "motivo": null },
    { "fila": 3, "resultado": "actualizado",  "usuarioId": "u_1a2b...", "codigoUsuario": 42,  "motivo": null },
    { "fila": 4, "resultado": "reasignado",   "usuarioId": "u_9d4e...", "codigoUsuario": 132, "motivo": null,
      "usuarioIdAnterior": "u_5c7f...", "codigoUsuarioAnterior": 77 },
    { "fila": 5, "resultado": "rechazado",    "usuarioId": null,        "codigoUsuario": null, "motivo": "numero_invalido" }
  ]
}
```
- `resultado` ∈ `creado | actualizado | reasignado | rechazado`.
- `motivo` (solo en `rechazado`) ∈
  `fila_incompleta` (falta `Nombre` o `Telefono`) · `numero_invalido` (no normaliza a E.164) ·
  `email_invalido` · `duplicado_en_archivo` (teléfono repetido: **el primero gana**) ·
  `email_duplicado` (el email ya es de otro usuario **activo**) · `conflicto_titular` (ver abajo) ·
  `idioma_invalido` · `antiguedad_invalida` · `no_encontrado` (solo en `modo=solo_actualizar`) ·
  `reasignacion_incompleta` (falló la compensación; el número queda sin activo, recuperable a mano).

**Conflicto de titular** (`I-08 §4.4`) — el teléfono ya tiene un usuario activo y el `Nombre` del
archivo **no coincide**. La carga **no decide sola** si es un typo o un cambio de titular:
- Nombres muy similares (normalizados y con similitud ≥ 0,85) se tratan como **corrección** →
  `actualizado`, sin conflicto.
- Si no, la fila sale `rechazado(conflicto_titular)` y **no se escribe nada**. La respuesta incluye
  `usuarioIdAnterior`, `codigoUsuarioAnterior` y `nombreActual`/`nombrePropuesto` para que el portal
  muestre *actual vs. propuesto*. El admin reenvía el mismo archivo con `reasignaciones`.
- `reasignar` inactiva al titular y crea uno nuevo (nuevo `id` y `codigoUsuario`), sin heredar rol,
  tags ni historial → resultado `reasignado`.

**Otras reglas**
- **Idempotencia:** re-subir el mismo archivo produce `actualizado` (no duplica).
- **Qué se conserva al actualizar:** `codigoUsuario`, `usuarioWhatsapp`, `rol`, `estado`, `creadoEn`,
  tags manuales y `propiedadesDinamicas`. Un campo opcional **vacío** en el archivo **no borra** el
  valor existente.
- La operación queda auditada en `LogSeguridad` (`AccionAdministrativa`, acción `carga_masiva`; las
  reasignaciones además con acción propia) con conteos y `correlationId`; **sin números ni nombres**.

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
  "seedThoughts": [],
  "configConversacional": {
    "maxRepreguntas": 1,
    "mensajeCierre": "Gracias. Tu aporte quedó registrado correctamente.",
    "segmentacionIdeas": false,
    "coachingSecuencialIdeas": false,
    "minutosCoachingPorIdea": null,
    "parafraseo": false,
    "participacionContinua": false,
    "clasificacionIntencionControl": false,
    "umbralCierreAnticipado": null,
    "numeroWhatsAppSaliente": null
  }
}
```
- `seedThoughts` (`I-12/I-19`, default `[]`): lista opcional de contexto orientador. Vacía/ausente no
  cambia el flujo; la API nunca inventa valores. Se usa al evaluar la versión consolidada confirmada,
  acotada por `Conversacion:MaxTokensSeedThoughts`, sin agregar criterios fuera de la rúbrica.
- `segmentacionIdeas` (`I-06`, default `false`): si está en `true` y el kill-switch global
  `Conversacion:SegmentacionIdeas` no lo apaga, el backend puede separar un mensaje con varias ideas en
  N respuestas/evaluaciones/Markdown. El portal lo expone como checkbox en Configuración de campaña.
- `coachingSecuencialIdeas` (`I-18`, default `false`): con segmentación efectiva, activa el coaching
  de una idea a la vez. Requiere además `Conversacion:CoachingSecuencialIdeas=true`; campo ausente
  conserva la confirmación multi-idea anterior.
- `minutosCoachingPorIdea` (`I-18`, `int?`, default `null`): ventana opcional por idea. `null` hereda
  `Conversacion:MinutosCoachingPorIdea`; `<=0` la desactiva para la campaña. No sustituye la
  inactividad de sesión I-17.
- `parafraseo` (`I-05`, default `false`): si está en `true` y `Conversacion:Parafraseo` no lo apaga,
  el evaluador solicita y el orquestador antepone un resumen fiel del aporte a la retroalimentación.
  Campo ausente = retro clásica; ambos flags permiten rollback sin redeploy.
- `participacionContinua` (`P-26`, default `false`): mientras la campaña esté `activa`, permite
  iniciar ideas/ciclos nuevos después de completar el recorrido anterior. Campo ausente conserva el
  flujo de una sola participación. Se admite en `POST`, se devuelve en `GET`, se actualiza en `PUT` y
  se copia de forma explícita al duplicar. Apagarlo permite terminar ideas abiertas, pero impide abrir
  otra; el estado `cerrada` prevalece y detiene toda interacción.
- `clasificacionIntencionControl` (`P-27`, default `false`): permite que, además de los alias
  deterministas, un clasificador LLM proponga si un mensaje corto significa aportar, dejar la idea,
  terminar la participación o pedir aclaración. Requiere el kill-switch global
  `Conversacion:ClasificacionIntencionControl=true`; el servidor conserva la decisión. Se admite en
  POST, se devuelve en GET, se actualiza en PUT y se copia explícitamente al duplicar.
- `umbralCierreAnticipado` (`P-13`, `double?`, default `null`): override opcional de la fracción de
  cierre anticipado para esta campaña. `null` hereda `Conversacion:UmbralCierreAnticipado`; `<= 0`
  lo desactiva para la campaña. `Conversacion:CierreAnticipadoHabilitado=false` es el kill-switch
  global que prevalece sobre el default y cualquier override. Campo ausente = herencia segura.
- `numeroWhatsAppSaliente` (`P-21`, `string?`, default `null`): alias lógico opcional del número que
  inicia los envíos de la campaña. Vacío o ausente usa el predeterminado global; un alias no configurado
  también degrada a ese predeterminado y no expone ids de Meta.

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
{ "conversaciones": 1, "mensajes": 3, "respuestas": 1, "ideas": 1, "versionesIdeas": 2, "evaluaciones": 1, "artefactos": 1, "blobsBorrados": 1, "blobsFallidos": 0, "participantesReseteados": 1 }
```
`ideas` y `versionesIdeas` son conteos aditivos I-19; clientes anteriores pueden ignorarlos. El
reinicio elimina esos documentos dentro del mismo participante/campaña para no dejar resultados
canónicos huérfanos.
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
| GET | `/api/admin/ideas` | **I-19:** lista una fila por idea lógica (`usuarioId, preguntaId, estadoResultado, estadoFlujo, estadoCuraduria`). |
| GET | `/api/admin/ideas/{id}` | **I-19:** idea vigente + evaluación + aportes/versiones auditables. |
| GET | `/api/admin/respuestas` | Lista/filtra aportes originales (`usuarioId, preguntaId, estado` y, para legacy, `nivelMadurez`). |
| GET | `/api/admin/respuestas/{id}` | Respuesta + evaluación asociada. |
| GET | `/api/admin/evaluaciones` | **`DT-QA-02`:** lista/filtra evaluaciones de una campaña con **diagnóstico de enlace** (`enlazada`/`huerfana`/`superada`/`sin_version_idea`). Ver sub-sección. |
| GET | `/api/admin/evaluaciones/{id}` | Evaluación completa (calificación, explicación, versiones). |
| GET | `/api/admin/markdown` | Lista artefactos Markdown (`campaniaId, tipoArtefacto, usuarioId, preguntaId`). |
| GET | `/api/admin/markdown/{id}` | Contenido Markdown + metadatos. |
| POST | `/api/admin/markdown/{id}/regenerar` | Regenera el artefacto desde datos operativos (`REQ §22.4.6`). |
| GET | `/api/admin/markdown/{id}/raw` | Descarga el `.md` (text/markdown). |

I-05 añade `parafraseoDevuelto` opcional al detalle de evaluación que devuelven
`/respuestas/{id}` y `/evaluaciones/{id}`. `null`/ausente significa que la campaña no lo tenía
activo o que la salida del LLM no produjo un resumen utilizable; conserva compatibilidad de lectura.

#### Listado de evaluaciones — `DT-QA-02`
> Cambio **aditivo** (una ruta nueva, solo lectura). Cubre el hueco de que `/evaluaciones` devolvía
> `404`: sin colección, una evaluación persistida **sin quedar enlazada** era invisible desde la API.
> No modifica `03` ni `/evaluaciones/{id}`. Detalle en
> `Iniciativas/DT-QA-02_Listado_Evaluaciones_Y_Huerfanas.md`.

**Query:** `campaniaId` (**requerido**; ausente → `400`) y, opcionales, `usuarioId`, `preguntaId`,
`respuestaId`, `ideaId`, `recomendacion=cerrar|repreguntar`, `anomaliaSeguridad=true|false`,
`enlace=enlazada|huerfana|superada|sin_version_idea`, `desde`/`hasta` (ISO UTC sobre `fecha`), más
`page`/`pageSize`. Orden: **`fecha` DESC** (mismo criterio de "evaluación vigente" que I-16, `09 §5`).

```json
{
  "resumen": { "total": 128, "enlazadas": 124, "huerfanas": 1, "superadas": 2, "sinVersionIdea": 1 },
  "items": [
    {
      "id": "eval_...", "campaniaId": "c_2026conv", "respuestaId": "resp_...",
      "ideaId": "idea_resp_...", "versionIdeaId": "idea_resp_..._v2",
      "origenTextoEvaluado": "ideaConsolidada",
      "usuarioId": "u_8f3c...", "preguntaId": "p_ingresos",
      "calificacionTotal": 4.1, "recomendacion": "repreguntar", "anomaliaSeguridad": false,
      "fecha": "2026-06-11T14:05:10Z",
      "enlace": "enlazada", "motivoDesenlace": null
    }
  ],
  "page": 1, "pageSize": 25, "total": 1
}
```
- `enlace` y `motivoDesenlace` son **derivados en tiempo de consulta**, no campos persistidos:
  `huerfana` (`respuesta_inexistente` \| `respuesta_id_vacio`), `superada`
  (`evaluacion_mas_reciente_existe`, situación **normal** contemplada por I-16), `sin_version_idea`
  (no puede promover una idea a madura, `03 §3.9`).
- `resumen` se calcula sobre el conjunto filtrado **antes** de paginar.
- El DTO de lista **no** incluye `explicacion`, `retroalimentacionEnviada`, `parafraseoDevuelto`,
  `repreguntaSugerida`, `calificacionPorCriterio` ni los snapshots: son texto largo y parte contiene
  el aporte del participante. Para eso está `/evaluaciones/{id}`.

**I-17 (aditivo):** el DTO de respuesta expone `nivelMadurez` (`maduro`/`incubacion`); ausente en
documentos históricos se interpreta como `incubacion`. `GET /api/admin/respuestas` acepta el filtro
opcional `nivelMadurez=maduro|incubacion` (vacío = todas), aplicado en memoria como el resto de los
filtros de `§2`. Permite a la pantalla de Resultados separar "Maduras" e "Incubación".

**I-19 (aditivo):** cuando existe `ideaId`, la unidad principal de Resultados es
`IdeaConsolidada`, no cada `Respuesta`. `GET /api/admin/ideas` exige `campaniaId` como el resto de los
resultados y acepta:

```text
usuarioId, preguntaId,
estadoResultado=madura|pendiente|rechazada,
estadoFlujo=pendienteConfirmacion|enMejora|enRevision|cerrada,
estadoCuraduria=pendiente
```

El DTO de lista devuelve `id`, `usuarioId`, `preguntaId`, `ideaIndice`, extracto de la versión
confirmada (o propuesta marcada si todavía no hay confirmación), estados, `nivelMadurez`,
`evaluacionVigenteRef`, `versionConfirmadaRef`, fechas y motivo de cierre. El detalle devuelve además
las versiones ordenadas, aportes originales, evaluación vigente y propuesta pendiente. Las versiones
rechazadas requieren los mismos roles administrativos vigentes y nunca aparecen al filtrar maduras.

Campos aditivos de respuesta para I-06/I-18:
```json
{
  "id": "resp_wamidabc_1",
  "texto": "Idea segmentada...",
  "ideaId": "idea_resp_wamidabc_1",
  "tipoAporte": "inicial",
  "ideaIndice": 1,
  "respuestaPadreId": "wamid.abc",
  "ideaRaizId": "resp_wamidabc_1",
  "respuestaAnteriorId": null,
  "revisionIndice": 0
}
```
- `ideaIndice`/`respuestaPadreId` solo aparecen poblados en respuestas segmentadas; clientes existentes
  pueden ignorarlos.
- `ideaRaizId`/`respuestaAnteriorId`/`revisionIndice` (I-18) permiten recorrer las revisiones sin
  cambiar el significado de los campos I-06. Son opcionales y ausentes en datos legacy.
- `ideaId`/`tipoAporte` (I-19) enlazan el aporte con la idea lógica. `/respuestas` se conserva para
  auditoría/compatibilidad y deja de alimentar una fila independiente de Resultados cuando existe
  `ideaId`.
- `GET /api/admin/conversaciones/{id}` expone opcionalmente `coachingIdeas` (`03 §3.6`) con la idea
  activa, estados, contadores y referencias vigentes; no incluye nuevos textos ni secretos.
- Los endpoints de Markdown conservan sus rutas. Para I-19, una idea produce un artefacto canónico
  `tipoArtefacto=idea`, con `ideaRef`/`versionIdeaRef`; los artefactos históricos
  `tipoArtefacto=respuesta` no se eliminan.

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
