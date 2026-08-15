# 07 — Backend: Campañas y Configuración (Tags, Rúbricas, Prompts, ConfigLLM)

**Módulos:** `Application/Campanas/` y `Application/Configuracion/`.
**Implementa:** `REQ §11–§19, §31.1`; `ARQ §8`.
**Depende de:** `03` (Campania, Tag, Rubrica, Prompt, ConfigLLM, Usuario, ParticipanteCampania), `04 §5` (endpoints), `10 §4` (Key Vault para API keys).

**Principio rector (`REQ §31.1`):** todo (campañas, mensajes, preguntas, tags, rúbricas, prompts, configuración) es **editable desde el portal sin tocar código**. Nada quemado.

---

## 1. Gestión de Usuarios y Tags

### 1.1 Usuarios
CRUD vía `/api/admin/usuarios` (`04 §5.1`). Reglas:
- Crear/editar valida y normaliza el número (`06 §2`); unicidad de `whatsappNormalizado` **entre
  usuarios activos** (`409` si ya hay un activo con ese número), validada en código además de la
  unique key `/claveUnicidad` de Cosmos (`03 §3.1`, `§5`). Un número **sí** puede repetirse en
  usuarios inactivos: es el histórico de una reasignación (`I-08 §3.1.d`).
- Asigna `area`, `empresa`, `empresaId`, `sede`, `cargo`, `email`, `antiguedadAnios`, `idioma`,
  `usuarioWhatsapp`, `tags[]`, `propiedadesDinamicas{}` y `rol`. **Obligatorios: `nombre` y el
  número**; el resto es opcional (`I-08 §3`).
- `codigoUsuario` (secuencial legible `U-000042`) lo asigna el servidor al crear y es **de solo
  lectura**: no cambia nunca, ni al editar ni al inactivar (`03 §3.1.1`).
- Activar/inactivar por `PATCH .../estado` (no se borran físicamente). **Activar falla con `409`** si
  ya hay otro activo con el mismo número.
- Reasignación de número (`POST .../reasignar-numero`, `04 §5.1`): inactiva al titular y crea un
  usuario nuevo con el mismo número, sin heredar rol, tags ni historial. Orden obligatorio: inactivar
  antes de crear (lo impone la unique key). `GET /api/admin/usuarios/por-numero/{numero}` devuelve el
  histórico.

### 1.2 Tags (`REQ §13`)
- CRUD vía `/api/admin/tags`. Parametrizables; iniciales `area` y `empresa` pero la lista **no** está quemada (`REQ §13.2.7`).
- Activar/desactivar. Se usan para filtrar participantes y resultados, y para clasificar Markdown.

---

## 2. Campañas (`REQ §11`)

### 2.1 CRUD y estados
- Estados: `borrador → activa → cerrada → archivada` (`REQ §11.2`). El servicio valida transiciones (p. ej. no se reactiva una `archivada`; documentar la matriz de transición permitida en el código).
- Una campaña embebe `mensajesIniciales[]` y `preguntas[]` (`03 §3.3`).
- Asocia por referencia: `rubricaRef` (+versión), `promptRefs`, `configLLMRef`, y configs de markdown/conversacional/seguridad.
- I-06 agrega `configConversacional.segmentacionIdeas` (aditivo, default `false`) como comportamiento por campaña; el kill-switch global `Conversacion:SegmentacionIdeas` queda fuera del CRUD de campaña.
- P-26 agrega `configConversacional.participacionContinua` (aditivo, default `false`) al crear,
  consultar, editar y duplicar. El servicio no lo confunde con el estado administrativo.
- P-27 agrega `configConversacional.clasificacionIntencionControl` (aditivo, default `false`) al
  crear, consultar, editar y duplicar. Solo habilita la clasificación flexible; el kill-switch global
  y la política server-side no son editables desde el CRUD de campaña.
- P-32 agrega `idiomasHabilitados` y `localizaciones` en campaña/mensajes/preguntas. Documento
  histórico equivale a español; duplicar copia todas las localizaciones y activar valida completitud.

### 2.2 Reglas de negocio (`REQ §11.3`)
- Solo `activa` permite envío de mensajes iniciales y recepción de respuestas (`§11.3.1–2`).
- `participacionContinua=true` solo permite ciclos nuevos mientras la campaña continúe `activa`.
  Apagarlo deja terminar ideas abiertas y bloquea las posteriores; cerrar la campaña prevalece.
- Una campaña requiere participantes asociados antes del envío (`§11.3.7`).
- `POST .../duplicar` clona la campaña como plantilla reutilizable (`§11.3.8`).
- La configuración usada en cada interacción se persiste vía snapshots en la Evaluación (`§11.3.9`, ver `08`).
- La campaña/pregunta define el tipo de artefacto Markdown (`§11.3.10`).

### 2.3 Mensajes iniciales (`REQ §15`)
- Sub-recurso de campaña (`04 §5.3`). Campos en `03 §3.3`.
- Soportan variables dinámicas `{{nombre}}`, `{{campaña}}`, `{{empresa}}`, `{{area}}` (`REQ §15.3`); el renderizado de variables ocurre en el Gateway al enviar (`05 §2`).
- Si la API de WhatsApp exige plantilla aprobada para iniciar, el mensaje inicial mapea a una **plantilla HSM** (campo `plantillaWhatsApp`) (`REQ §15.4.10`, `ARQ §4.1`). El texto editable sirve para la variante de plantilla y para trazabilidad.
- Varios mensajes iniciales se envían en el `orden` configurado (`§15.4.3`).
- **P-32:** el mismo mensaje tiene `localizaciones[es|en]` con `texto` y `plantillaRef`. El alias
  lógico se mapea a la plantilla Meta del ambiente; no se persiste el id físico del proveedor como
  contenido editable. Para `es`, el escalar `texto` es fallback temporal; `en` nunca hereda español.

### 2.4 Preguntas (`REQ §16`)
- Sub-recurso de campaña. Campos en `03 §3.3`.
- Cada pregunta puede asociar su propia `rubricaRef`(+versión) y `promptRefs`, sobreescribiendo los de la campaña.
- `maxRepreguntas` (MVP=1), `limitesSeguridad`, `configMarkdown` por pregunta.
- Las preguntas MVP iniciales: mejorar ingresos, reducir costos, mejorar productividad (`REQ §16.1`) — se **cargan como datos**, no se hardcodean.
- **P-32:** `localizaciones[idioma].texto/instruccion` comparte el mismo `Pregunta.id`, rúbrica,
  límites y estados. Una campaña bilingüe no se activa si una pregunta activa está incompleta.

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

### 5.3 Catálogo de textos conversacionales (P-32)

- Se persiste en el contenedor existente `config` (`03 §3.13.1`) y se administra por `04 §5.7.1`.
- Versionado híbrido: borrador editable en sitio; activo/inactivo inmutable; una edición posterior
  crea nueva versión. Exactamente una versión activa por idioma.
- Activar valida el catálogo completo y cambia versiones en un lote transaccional de la misma
  partición con ETag. No existe activación parcial ni importación que publique automáticamente.
- El proveedor runtime expone lecturas por clave e idioma, caché corta y última versión válida. Un
  fallo de Cosmos no reemplaza una versión válida por contenido incompleto.
- El registro de claves, tipos, placeholders y límites es contrato del servidor. El portal modifica
  valores/listas, no inventa claves.
- App Settings conserva solo configuración operacional (gate, cache TTL, flags/límites y mapeos Meta).
  Los textos y frases editoriales legacy se migran y después quedan deprecados.
- **DT-P32-02:** la base curada `es/en` no lee App Settings; la fotografía legacy es una operación
  separada y prevalidada. Descargar/reimportar JSON permite edición masiva de valores y listas, pero
  siempre crea una versión nueva en borrador. `MaxFrasesPorGrupo` y `MaxBytesImportacionJson` son
  límites operativos con techo compilado, no contenido editorial.
- Activar una campaña bilingüe exige una versión global activa y válida por cada idioma además de
  localizaciones completas. Readiness expone el bloqueo antes de intentar la transición.
- **DT-P32-03-01:** readiness conserva visibles los mapeos que necesitan campañas activas y borrador,
  pero solo los requeridos por al menos una campaña activa bloquean `listoParaGateOn`. Si el gate está
  ON, activar un borrador valida los mapeos estructurales de esa campaña con la misma política del
  envío; falla sin cambiar estado si falta alguno. Con gate OFF no cambia la activación vigente.

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
- P-32: un admin puede editar/versionar textos `es/en` y revertirlos sin build; una campaña bilingüe
  incompleta no se activa y una campaña legacy española sigue funcionando.
- DT-P32-02: un admin descarga, edita, prevalida e importa el catálogo completo como nuevo borrador;
  una configuración legacy inválida no impide crear las semillas base `es/en`.
- DT-P32-03-01: un borrador incompleto permanece visible en Preparación sin bloquear campañas activas,
  y no puede pasar a activa con el gate ON hasta completar sus propios mapeos Meta.
- Crear/editar/duplicar preserva `participacionContinua`; un documento histórico ausente se devuelve
  como `false`.
- Crear/editar/duplicar preserva `clasificacionIntencionControl`; ausente se devuelve como `false` y
  no habilita llamadas LLM de control.

*Fin del documento.*
