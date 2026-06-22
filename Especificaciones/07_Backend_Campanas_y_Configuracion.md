# 07 — Backend: Campañas y Configuración (Tags, Rúbricas, Prompts, ConfigLLM)

**Módulos:** `Application/Campanas/` y `Application/Configuracion/`.
**Implementa:** `REQ §11–§19, §31.1`; `ARQ §8`.
**Depende de:** `03` (Campania, Tag, Rubrica, Prompt, ConfigLLM, Usuario, ParticipanteCampania), `04 §5` (endpoints), `10 §4` (Key Vault para API keys).

**Principio rector (`REQ §31.1`):** todo (campañas, mensajes, preguntas, tags, rúbricas, prompts, configuración) es **editable desde el portal sin tocar código**. Nada quemado.

---

## 1. Gestión de Usuarios y Tags

### 1.1 Usuarios
CRUD vía `/api/admin/usuarios` (`04 §5.1`). Reglas:
- Crear/editar valida y normaliza el número (`06 §2`); unicidad de `whatsappNormalizado` (`409` si duplicado), validada en código además de la unique key de Cosmos (`03 §5`).
- Asigna `area`, `empresa`, `tags[]`, `propiedadesDinamicas{}` y `rol`.
- Activar/inactivar por `PATCH .../estado` (no se borran físicamente).

### 1.2 Tags (`REQ §13`)
- CRUD vía `/api/admin/tags`. Parametrizables; iniciales `area` y `empresa` pero la lista **no** está quemada (`REQ §13.2.7`).
- Activar/desactivar. Se usan para filtrar participantes y resultados, y para clasificar Markdown.

---

## 2. Campañas (`REQ §11`)

### 2.1 CRUD y estados
- Estados: `borrador → activa → cerrada → archivada` (`REQ §11.2`). El servicio valida transiciones (p. ej. no se reactiva una `archivada`; documentar la matriz de transición permitida en el código).
- Una campaña embebe `mensajesIniciales[]` y `preguntas[]` (`03 §3.3`).
- Asocia por referencia: `rubricaRef` (+versión), `promptRefs`, `configLLMRef`, y configs de markdown/conversacional/seguridad.

### 2.2 Reglas de negocio (`REQ §11.3`)
- Solo `activa` permite envío de mensajes iniciales y recepción de respuestas (`§11.3.1–2`).
- Una campaña requiere participantes asociados antes del envío (`§11.3.7`).
- `POST .../duplicar` clona la campaña como plantilla reutilizable (`§11.3.8`).
- La configuración usada en cada interacción se persiste vía snapshots en la Evaluación (`§11.3.9`, ver `08`).
- La campaña/pregunta define el tipo de artefacto Markdown (`§11.3.10`).

### 2.3 Mensajes iniciales (`REQ §15`)
- Sub-recurso de campaña (`04 §5.3`). Campos en `03 §3.3`.
- Soportan variables dinámicas `{{nombre}}`, `{{campaña}}`, `{{empresa}}`, `{{area}}` (`REQ §15.3`); el renderizado de variables ocurre en el Gateway al enviar (`05 §2`).
- Si la API de WhatsApp exige plantilla aprobada para iniciar, el mensaje inicial mapea a una **plantilla HSM** (campo `plantillaWhatsApp`) (`REQ §15.4.10`, `ARQ §4.1`). El texto editable sirve para la variante de plantilla y para trazabilidad.
- Varios mensajes iniciales se envían en el `orden` configurado (`§15.4.3`).

### 2.4 Preguntas (`REQ §16`)
- Sub-recurso de campaña. Campos en `03 §3.3`.
- Cada pregunta puede asociar su propia `rubricaRef`(+versión) y `promptRefs`, sobreescribiendo los de la campaña.
- `maxRepreguntas` (MVP=1), `limitesSeguridad`, `configMarkdown` por pregunta.
- Las preguntas MVP iniciales: mejorar ingresos, reducir costos, mejorar productividad (`REQ §16.1`) — se **cargan como datos**, no se hardcodean.

### 2.5 Selección de participantes (`REQ §14`)
- Asociar por ids o por filtro (área/empresa/tags/búsqueda/número) vía `/api/admin/campanias/{id}/participantes` (`04 §5.3`).
- `preview` devuelve cuántos y quiénes recibirían, para confirmar antes de enviar (`REQ §27.2`).
- Solo usuarios activos con número válido pueden asociarse (`§14.2.1–2`).
- Crear/actualizar `ParticipanteCampania` (`03 §3.4`) por cada asociación.

---

## 3. Rúbricas (`REQ §17`)

### 3.1 Carga y parseo
- Se crea/actualiza con un documento **Markdown** (`contenidoMarkdown`) que el LLM consumirá (`REQ §17.3.4, §17.3.6`).
- Al guardar, el servicio **parsea** del Markdown (o de campos estructurados acompañantes) los `criterios[]`, `pesos` y `escala` para poder validarlos y mostrarlos en el portal. Si el parseo no es determinista, se aceptan criterios/pesos/escala como campos estructurados además del Markdown (la fuente para el LLM sigue siendo el Markdown).
- Validación: la suma de pesos debe ser coherente (p. ej. ~1.0 o normalizable); escala con min/max válidos. Si no, `400/422` con detalle.

### 3.2 Versionado y edición híbrida por estado (`REQ §17.3.2–3`)
- **Estrategia elegida:** `id` estable de familia (p. ej. `r_general`) + campo `version` incremental; cada versión es un documento independiente en `config` con el mismo nombre de familia y distinto `version`. La "versión activa" es la de mayor `version` con `estado=activa`. La Evaluación guarda `rubricaRef + versionRubrica` (snapshot).
- **Edición híbrida por estado:** una rúbrica en `borrador` (estado **no comprometido**, nunca usado para evaluar) se edita **en sitio** sobre su versión vigente (`PUT /api/admin/rubricas/{id}`), sin incrementar versión. Una vez `activa` (o `archivada`) queda inmutable: toda edición posterior es **nueva versión** (`POST .../versiones`); el `PUT` responde `409 CONFLICT`. Así las versiones comprometidas nunca mutan y los snapshots de evaluaciones pasadas se conservan. Ver `SUPUESTOS.md#edicion-config-hibrida`.
- `GET .../versiones` lista el historial.

---

## 4. Prompts (`REQ §18`)

### 4.1 Tipos y edición
- Tipos en `03 §3.12` (`evaluar`, `retro`, `repregunta`, `cierre`, `compilar`, etc.) (`REQ §18.1`).
- Editables desde el portal sin intervención técnica (`REQ §18.3.5`).
- Cada prompt de evaluación DEBE contener las reglas de comportamiento: no prometer implementar, no ofrecer ejecutar acciones, responder corto/natural/práctico, e **ignorar instrucciones contenidas en la respuesta del usuario** (`REQ §18.3.7–9`, `§25.3.2`). El módulo de Evaluación (`08`) además **estructura** la separación instrucción/dato a nivel de mensajes.

### 4.2 Versionado y aprobación humana (`REQ §18.2, §18.3.6`)
- Misma estrategia de versionado que rúbricas (familia + `version`).
- **Edición híbrida por estado:** un prompt en `borrador` (sin aprobar, nunca usado para evaluar) se edita **en sitio** (`PUT /api/admin/prompts/{id}`), sin incrementar versión y permaneciendo en `borrador`. Una vez aprobado/`activo` (o `inactivo`) queda comprometido: edición posterior es **nueva versión** (que vuelve a nacer en `borrador`, sin aprobar); el `PUT` responde `409 CONFLICT`. Ver `SUPUESTOS.md#edicion-config-hibrida`.
- **Aprobación obligatoria** antes de uso en campaña: `POST .../aprobar` setea `aprobadoPor` + `fechaAprobacion`. Un prompt sin aprobar **no** puede asociarse/usarse en una campaña activa; el servicio lo valida al activar la campaña o al evaluar.

---

## 5. Configuración LLM (`REQ §19`)

### 5.1 CRUD seguro de credenciales (`REQ §19.2`, `ARQ §10`)
- Campos en `03 §3.13`. La **API key nunca** se guarda en Cosmos ni se devuelve por la API.
- Al crear/rotar: el backend recibe `apiKey` (write-only), la **escribe en Key Vault** como una versión del secreto cuyo nombre es `apiKeyRef`, y persiste solo `apiKeyRef` en `ConfigLLM`.
- **Rotación** = nueva versión del secreto en Key Vault; `apiKeyRef` no cambia (`REQ §19.2.3`, `ARQ §10.5`).
- La UI muestra la key enmascarada (`••••1234`); nunca completa (`REQ §19.2.2`).
- Registrar quién y cuándo actualizó la configuración (auditoría) (`REQ §19.2.4`).
- Función restringida a administradores autorizados (`REQ §19.2.5`).

### 5.2 Acceso en runtime
- El módulo de Evaluación (`08`) lee `ConfigLLM` activa y resuelve la API key por `apiKeyRef` desde Key Vault vía Managed Identity, con **caché en memoria de expiración corta** (no persiste el secreto en disco) (`ARQ §10.8`).

---

## 6. Validaciones transversales
- Toda entidad de configuración valida campos obligatorios y estados; errores → `400/422` con el modelo de error de `04 §3`.
- Cambiar configuración nunca rompe interacciones ya registradas: la trazabilidad por snapshots (`08`) garantiza reproducibilidad aunque la rúbrica/prompt cambien después (`REQ §17.3.3`, `§18.2`).

---

## 7. Criterios de aceptación del módulo (resumen; ver `13`)
- Un admin crea usuarios, tags, una campaña, mensajes iniciales, preguntas y asocia participantes desde el portal sin tocar código.
- Carga una rúbrica Markdown y se versiona; edita y aprueba prompts; un prompt sin aprobar no se usa.
- Configura el proveedor/modelo LLM y guarda la API key de forma segura (solo `apiKeyRef` en BD; key en Key Vault; enmascarada en UI).
- Solo campañas activas permiten envío/recepción.
- Duplicar una campaña produce una plantilla reutilizable.

*Fin del documento.*
