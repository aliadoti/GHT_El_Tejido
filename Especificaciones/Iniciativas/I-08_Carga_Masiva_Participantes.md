# I-08 — Carga masiva de participantes vía Excel

> **Origen:** hoja `Iniciativas` (Action Item de la reunión 9-jul).
> **Tipo:** Desarrollo · **Prioridad:** Alta · **Ventana:** Sprint 1a backend / Sprint 1b UI ·
> **Dependencia:** lista final de GHT (insumo #5, límite 1-ago) · **Riesgo:** datos sucios.
> Cubre REQ §12/§26.3, ARQ §8; specs base `03 §3.1`, `04 §5.1/§5.3`, `07 §1`.
> **Revisión (7-ago-2026):** la plantilla oficial es
> `Información asistentes convención gerentes 2026 V1.xlsx` (entregada por GHT). Este documento se
> actualizó para reflejar sus columnas reales, el modo de actualización masiva por teléfono y los
> cambios al maestro de usuarios (§3.1): código secuencial, unicidad de teléfono por estado y
> reasignación de números.

## 1. Qué pide GHT / por qué
Subir la lista de participantes **en lote (Excel)** desde el portal en vez de crearlos uno a uno.
Necesario para cargar la lista real de la convención (freeze 8-ago).

**Alcance de esta entrega:** dejar el sistema **listo para cargar**, con la plantilla oficial
soportada de punta a punta (parser, validación, reporte, UI). **No se carga ningún dato todavía**: la
carga real de los participantes es un paso posterior del freeze, con la versión definitiva del archivo
(la V1 actual trae `Empresa`, `Idioma` y `Telefono` sin diligenciar). Ver §9.

## 2. Estado actual del build
Existe `ServicioCargaMasiva` (Application) con lector CSV y upsert por número normalizado, diseñado
para la plantilla anterior `Nombre | WhatsApp | Area | Empresa | Tags`. Esa plantilla **queda
reemplazada** por la de GHT; el servicio, el lector y el modelo `Usuario` deben ajustarse a §3 y §3.1.

## 3. Plantilla oficial y mapeo al modelo
Archivo de referencia: `Especificaciones/Iniciativas/plantillas/plantilla_participantes_v1.xlsx`
(hoja única, **fila 1 = cabecera obligatoria**, 9 columnas en este orden exacto).

| # | Columna Excel | Campo `Usuario` | Obligatorio | Notas |
|---|---|---|---|---|
| A | `Empresa` | `empresa` (nombre legible) | No | Si viene vacía se deja `null`; el código de `ID Empresa` es el que manda. |
| B | `ID Empresa` | `empresaId` | No | Código corto (`AL`, `GR`, `FF`, `GHT`…). Se usa para agrupar y para autogenerar la tag de empresa. |
| C | `Sede` | `sede` | No | Puede refinar la empresa (`FF - ADM`, `FF - AJ`, `FF - LN`). |
| D | `Nombre` | `nombre` | **Sí** | Se normaliza (trim, colapso de espacios). Llega en mayúsculas; **no** se re-capitaliza en la carga. |
| E | `Cargo` | `cargo` | No | Reemplaza el uso de `Area` en la plantilla anterior. |
| F | `Email` | `email` | No | Se normaliza a minúsculas y sin espacios (la V1 trae valores con espacio final). Si viene, debe ser único entre usuarios **activos**. |
| G | `Antigüedad en la empresa en años` | `antiguedadAnios` | No | **Decimal** (`decimal?`), tal cual viene del Excel (p. ej. `16.391666`). No se redondea. |
| H | `Idioma` | `idioma` | No | Default **`es`** si viene vacío. Valores aceptados: `es`, `en`. |
| I | `Telefono` | `whatsappNormalizado` | **Sí** | **Clave funcional y clave de upsert.** Sin teléfono no hay WhatsApp, así que la fila no entra. Se normaliza a E.164 con `NormalizadorNumero` (`06 §2`). |

**Obligatorios: `Nombre` y `Telefono`.** Todo lo demás es opcional; una fila con `Email`, `Cargo`,
`Sede`, `Empresa` o `Antigüedad` vacíos **se carga igual** y el hueco queda visible en el reporte.

**No hay columna `Tags`.** Las tags de área/empresa se derivan: si `ID Empresa` viene diligenciado se
asegura la tag `t_emp_<idEmpresa en minúscula>` (tipo `empresa`, creada si no existe) y se asigna al
usuario. La carga **no borra** tags puestas manualmente desde el portal.

**Fuera del archivo:** `codigoUsuario` y `usuarioWhatsapp` (§3.1) **nunca** se leen de la plantilla.
El primero lo asigna el sistema; el segundo se captura desde el portal.

### 3.1 Cambios al maestro de usuarios (`03 §3.1`)

**a) Campos de primer nivel nuevos** (hoy irían en `propiedadesDinamicas`):
`empresaId`, `sede`, `cargo`, `email`, `antiguedadAnios`, `idioma`.

**b) `codigoUsuario` — identificador secuencial legible.**
- Entero autonumérico global del maestro, formateado como `U-000123` para mostrar. Único e
  **inmutable**: acompaña al usuario toda su vida, incluso inactivo.
- El `id` técnico del documento **sigue siendo** `u_<guid>`: es el que referencian `Campania`,
  `Conversacion`, `EnvioMensaje`, `EnrutamientoAporte`, `Evaluacion` y `LogSeguridad` (`03`), y no se
  toca para no migrar esas referencias.
- Implementación: documento `Secuencia` en el contenedor `users` (`pk = "secuencia"`,
  `id = "seq_usuario"`, campo `ultimoValor`), incrementado con **concurrencia optimista por ETag**
  (reintento en `412`). En carga masiva se **reserva un bloque** de N valores en una sola operación
  (N = filas a crear) para no golpear el contador fila por fila.
- Se aplica también al alta individual. **No hay backfill**: la base se recrea desde cero (§3.2), el
  contador arranca en `1` y el usuario administrador de la semilla queda como `U-000001`.

**c) `usuarioWhatsapp` — identificador de WhatsApp por usuario.**
- `string?`, **opcional**, capturado solo desde el portal. **No se carga** desde CSV/Excel en esta
  entrega (la plantilla no lo trae). Se guarda tal cual, con trim; sin validación de formato por ahora.
- Reservado para la identificación por usuario (no solo por número) de WhatsApp. **No** participa aún
  en el enrutamiento ni en la resolución de participante: el canal sigue resolviendo por
  `whatsappNormalizado` (`05`, `06 §2`). Cuando se habilite, será una columna adicional de la plantilla
  y una revisión aparte de `05`.

**d) Unicidad de teléfono ligada al estado — reasignación de números.**
Un número de WhatsApp puede pasar de una persona a otra dentro de la organización (rotación de línea
corporativa). Regla:
- **A lo sumo un usuario `activo` por `whatsappNormalizado`.** Los usuarios `inactivo` conservan el
  número histórico y no bloquean la reasignación.
- Reasignar = poner el titular anterior en `estado = inactivo` y **crear un registro nuevo** (nuevo
  `id`, nuevo `codigoUsuario`) con el mismo teléfono. El histórico de campañas, conversaciones y
  evaluaciones queda colgado del `id` anterior, así que **la trazabilidad se conserva** y no se le
  atribuyen al nuevo titular.
**e) `claveUnicidad` — cómo se garantiza "un solo activo por teléfono" en Cosmos.**
La unique key sobre `/whatsappNormalizado` que exige hoy `03 §67/§735` **ya no sirve**: con la
reasignación habría varios documentos con el mismo número. Y no basta con quitarla, porque Cosmos
trata el path ausente como `null` y también lo hace único (solo un documento sin el campo por
partición). La solución es un **campo derivado que todo documento de `users` puebla**:

| Documento | `claveUnicidad` |
|---|---|
| `Usuario` con `estado = activo` | `wa\|<whatsappNormalizado>` |
| `Usuario` con `estado = inactivo` | `hist\|<id>` (único por construcción) |
| `Tag` | `tag\|<id>` |

- **Unique key policy** del contenedor `users`: **`/claveUnicidad`** (reemplaza a
  `/whatsappNormalizado`). Alcance = partición lógica, y como todos los `Usuario` comparten
  `pk = "usuario"`, la unicidad es efectivamente global para el maestro.
- Un segundo activo con el mismo número falla con **`409 Conflict`** aunque la validación de
  aplicación tenga un bug o haya una carrera. La aplicación **sigue validando primero** (para devolver
  un motivo tipificado en vez de un 409 crudo); la unique key es la red de seguridad.
- El campo se recalcula en cada escritura, siempre en el mapeo a documento de
  `RepositorioUsuariosCosmos` (nunca en el dominio ni a mano en un servicio), para que sea imposible
  guardar un `Usuario` con la clave desincronizada del `estado`.
- **Inactivar y reasignar debe hacerse en dos pasos ordenados** dentro de la misma fila: primero
  inactivar al titular (su clave pasa de `wa|…` a `hist|…`), y solo entonces crear al nuevo. Al revés,
  la unique key rechaza la operación. Si el segundo paso falla, se revierte el primero.
- ⚠️ Las unique keys de Cosmos son **inmutables**: se define al crear el contenedor. Corregir
  `03 §67`, `03 §735` y `Guia_Azure_Portal §2.1` (pasos 5 y checklist).

**f) Resolución por número: siempre filtrando por activo.**
Toda consulta que responda "el participante de este número" debe filtrar por `estado = activo`:
`ObtenerUsuarioPorNumeroAsync` pasa a devolver **solo el activo**. Los 7 puntos de uso actuales lo
requieren por igual —`ResolutorParticipante` (webhook entrante), `AuthAdminService` (login),
`ServicioGestionUsuarios` (alta y edición individual), `ServicioCargaMasiva` y
`EndpointsSimulacion`—, así que el filtro va **dentro del repositorio**, no en cada llamador.
- Se agrega `ListarUsuariosPorNumeroAsync` (devuelve activo + histórico, ordenado por `creadoEn`) para
  la ficha del portal y la auditoría de reasignaciones. Es el **único** camino para ver inactivos.
- Efecto buscado: un mensaje entrante desde un número cuyo único registro está inactivo **no resuelve
  participante** y cae en el flujo de rechazo existente (`rechazoParticipacion`, `06`), en vez de
  atribuirse al titular anterior.
- Aplica igual a `RepositoriosMemoria` (hoy hace `FirstOrDefault` sin filtrar), o las pruebas pasarán
  con un comportamiento distinto al de producción.

**g) `email` opcional y único entre activos.** Si viene, no puede repetirse en otro usuario activo
(se valida en aplicación; sin unique key por ser nullable). Vacío es válido.

**h) `area` y `empresa` dejan de ser obligatorios** en `Usuario.Crear` (la plantilla no trae `Area` y
`Empresa` puede venir vacía). Se relaja el `DomainGuards.Required` de ambos.

**Impacto:** el alta individual (`POST /api/admin/usuarios`), el DTO de usuario del portal y los
filtros por área/empresa deben aceptar los campos nuevos. Actualizar `03` y `04` en **commits aparte**.

### 3.2 Recreación de la base (no hay migración)
El entorno se puede **borrar y recrear**, así que todos los cambios de §3.1 se aplican como esquema
inicial en vez de como migración. Esto elimina el backfill de `codigoUsuario` y permite fijar la
unique key correcta desde el principio (es inmutable una vez creado el contenedor).

Procedimiento, antes de tocar código de carga:
1. Borrar y recrear el contenedor **`users`** con `pk = /pk` y **unique key `/claveUnicidad`**
   (ya **no** `/whatsappNormalizado`). Verificar que no quedó `/pk` como unique key —
   `Guia_Azure_Portal §2.1` documenta ese error.
2. Recrear también los contenedores que referencian `usuarioId` (`campaigns`, `conversations`,
   `security`, …) para no dejar huérfanos apuntando a ids que ya no existen.
3. Sembrar **solo el usuario administrador** (`Semillas/Semilla.md`) con `codigoUsuario = 1`,
   `estado = activo`, `claveUnicidad = "wa|<numero admin>"`, y el documento
   `Secuencia { id: "seq_usuario", pk: "secuencia", ultimoValor: 1 }`.
4. Verificar el guardarraíl: intentar crear un segundo usuario activo con el número del admin debe
   devolver **`409`**.

**Ventana:** la recreación debe ocurrir **antes del freeze** y antes de cualquier carga real (§9).
Coordinar con GHT si ya hay algo cargado en el entorno de pruebas.

## 4. Diseño técnico
1. **Endpoint** `POST /api/admin/usuarios/carga-masiva` (`multipart/form-data`, rol admin + CSRF,
   límite `Seguridad:CargaMasivaMaxBytes`, default 2 MB). Parámetros opcionales:
   - `campaniaId`: asocia los creados/actualizados a esa campaña al terminar el lote (`04 §5.3`).
   - `modo` ∈ `upsert` (default) | `solo_actualizar` — ver §4.3.
   - `reasignaciones`: lista de filas que el admin autorizó a reasignar — ver §4.4.
2. **Lectores** (`ILectorArchivoParticipantes`, ya existe el puerto):
   - `LectorXlsxParticipantes` (**nuevo**, Infrastructure, **ClosedXML**) — formato primario, es el
     que entrega GHT.
   - `LectorCsvParticipantes` (existente) — se **reajusta** a las 9 columnas nuevas; sigue disponible
     como fallback y para exportar la plantilla desde Excel.
   Ambos validan la cabecera exacta de §3 y descartan filas totalmente vacías (la V1 trae una).
3. **Por fila:** normaliza teléfono a E.164, normaliza email, aplica default de idioma, resuelve la
   tag de empresa, valida obligatorios y unicidad **contra usuarios activos**, y hace el upsert.
4. **Reporte por fila** (respuesta JSON): `creado | actualizado | rechazado` + motivo. Una fila mala
   **no aborta** el lote. Motivos tipificados (sin PII):
   `fila_incompleta` (falta `Nombre` o `Telefono`), `numero_invalido`, `email_invalido`,
   `duplicado_en_archivo` (mismo teléfono repetido; el primero gana), `email_duplicado` (el email ya
   pertenece a otro usuario **activo**), `conflicto_titular` (§4.4), `idioma_invalido`,
   `antiguedad_invalida`, `no_encontrado` (solo en `modo=solo_actualizar`).
   Cada fila resuelta devuelve además su `codigoUsuario`.
5. **Portal:** pantalla en Usuarios con upload (`.xlsx`/`.csv`) + selector de modo + preview del
   reporte + **resolución de conflictos de titular** (§4.4) + confirmación (toasts existentes). Botón
   para **descargar la plantilla vacía**. Sin PII en logs (solo conteos y motivos).

### 4.1 Clave de upsert
`whatsappNormalizado` **entre usuarios activos** (columna `Telefono`). Reprocesar el mismo archivo
**no duplica**: la segunda corrida devuelve `actualizado` para todas las filas.

### 4.2 Qué se conserva al actualizar
El archivo manda para `nombre`, `empresa`, `empresaId`, `sede`, `cargo`, `email`, `antiguedadAnios` e
`idioma`. Se **conservan** `codigoUsuario`, `usuarioWhatsapp`, `rol`, `estado`, `creadoEn`, tags
manuales y `propiedadesDinamicas`, para no degradar un admin ni reactivar un inactivo. Un campo
opcional que llega **vacío** en el archivo **no borra** el valor existente (se ignora); solo un valor
no vacío sobrescribe.

### 4.3 Modo `solo_actualizar` (actualización masiva por teléfono)
Pensado para completar datos de un roster ya cargado (p. ej. llenar `Idioma` o corregir `Cargo`) sin
crear registros nuevos por un teléfono mal digitado.
- Clave de búsqueda: `Telefono` normalizado, **filtrando por `estado = activo`**.
- Si el usuario activo **existe** → `actualizado` con las reglas de §4.2.
- Si **no existe** → `rechazado(no_encontrado)`; **no** se crea, ni siquiera si hay un usuario
  inactivo con ese número.
- Las columnas obligatorias siguen siendo `Nombre` y `Telefono` para poder validar la fila.
- En este modo **nunca** se reasigna: un nombre distinto sobre el mismo teléfono se trata como
  `conflicto_titular` (§4.4), igual que en `upsert`.

### 4.4 Conflicto de titular (teléfono existente, persona distinta)
La carga **no decide sola** si un nombre distinto sobre un teléfono existente es un typo o un cambio
de titular; inactivar a alguien por un error de digitación sería peor que fallar.

- **Detección:** el teléfono ya tiene un usuario activo y el `Nombre` del archivo **no coincide** con
  el registrado. La comparación es tolerante: se normaliza (trim, colapso de espacios, mayúsculas, sin
  tildes) y se aplica un umbral de similitud (p. ej. Levenshtein normalizado ≥ 0,85 ⇒ se considera el
  mismo nombre y es una **corrección**, se actualiza sin conflicto).
- **Resultado de la primera pasada:** `rechazado(conflicto_titular)`, con `usuarioId` y
  `codigoUsuario` del titular actual y el nombre nuevo, para que la UI muestre *actual vs. propuesto*.
  Nada se escribe para esa fila.
- **Resolución (segunda llamada, mismo archivo):** el admin marca cada conflicto y reenvía con
  `reasignaciones = [{ fila, accion }]`, `accion` ∈
  - `corregir_nombre` → actualiza el registro existente (§4.2), conserva `id` y `codigoUsuario`.
  - `reasignar` → transacción por fila: el titular actual pasa a `estado = inactivo` y se **crea** un
    usuario nuevo (nuevo `id`, nuevo `codigoUsuario`, `estado = activo`) con los datos del archivo. El
    nuevo **no hereda** tags, rol ni historial. Resultado reportado: `reasignado`.
  - `omitir` → la fila queda `rechazado(conflicto_titular)`.
- **Auditoría:** cada reasignación genera un `LogSeguridad` de tipo `accionAdministrativa` con
  `usuarioId` anterior y nuevo `codigoUsuario` (sin PII).
- La reasignación también debe estar disponible **manualmente** desde la ficha de usuario del portal,
  no solo por carga.

## 5. Contratos y configuración
- `04 §5.1`: actualizar plantilla, parámetros `modo` y `reasignaciones`, nuevos motivos de rechazo,
  resultado `reasignado` y campos del DTO de usuario (`codigoUsuario`, `usuarioWhatsapp`, …) —
  **commit aparte**.
- `03 §3.1`: campos nuevos de primer nivel, `codigoUsuario`, `usuarioWhatsapp`, `claveUnicidad`,
  documento `Secuencia`, `area`/`empresa` opcionales — **commit aparte**.
- `03 §67` y `03 §735`: **reemplazar la unique key `/whatsappNormalizado` por `/claveUnicidad`** y
  reescribir la afirmación "`whatsappNormalizado` es único" → "único **entre usuarios activos**"
  (§3.1.d/e).
- `Guia_Azure_Portal §2.1` (pasos 5 y checklist final): la unique key de `users` pasa a
  `/claveUnicidad` y deja de ser opcional; el `Tag` también debe poblar el campo.
- `06 §2` / `05`: dejar explícito que la resolución de participante por número filtra por
  `estado = activo`, y que un número cuyo único registro está inactivo cae en `rechazoParticipacion`.
- `07 §1.1`: unicidad del número **entre activos**, campos nuevos, `codigoUsuario` de solo lectura,
  reasignación y `409` al activar.
- `11 §Usuarios/Tags`: `.xlsx`, selector de modo, resolución de conflictos, descarga de plantilla,
  código de usuario en la tabla e histórico del número en la ficha.
- `QAS`: `00 §1.2`, `01` (matriz), `02` (ADM-08/08a/08b/08c/09/09a), `03` (checklist día-D),
  `04 §5/§5.1/§5.2` (datos de prueba), `08` (guía en lenguaje simple), `10` (E15/E15b).
- Semilla del entorno: admin con `codigoUsuario = 1` + documento `Secuencia` (§3.2).
- Paquete nuevo: **ClosedXML** (Infrastructure). Registrar en `AVANCES.md`.

> **Estado documental (2026-08-07):** todos los documentos de esta lista **ya están actualizados**.
> Lo único pendiente es el código y la recreación de la base.

## 6. Riesgos y mitigación
- *Datos sucios (teléfonos mal formados, emails con espacios, duplicados)* → normalización + validación
  por fila con reporte; el lote no falla completo.
- *Plantilla V1 incompleta (`Empresa`, `Idioma`, `Telefono` sin diligenciar)* → **no se carga nada aún**
  (§9); se pide a GHT la versión completa. `Telefono` es obligatorio: sin él ninguna fila entra.
- *Inactivar a la persona equivocada por un typo en el nombre* → nunca se reasigna automáticamente;
  requiere confirmación explícita del admin por fila (§4.4).
- *Dos usuarios activos con el mismo teléfono por una condición de carrera* → doble barrera: chequeo
  previo en aplicación (motivo tipificado) + unique key `/claveUnicidad` (`409`, §3.1.e). El lote
  serializa además con el set de números vistos.
- *`claveUnicidad` desincronizada del `estado`* (el riesgo propio de un campo derivado) → se calcula en
  un único lugar, el mapeo a documento del repositorio Cosmos, nunca en el dominio ni en un servicio.
  Prueba dedicada en §7.
- *Reasignación a medias: se inactivó al titular pero falló el alta del nuevo* → los dos pasos van
  ordenados y con compensación (§3.1.e). Si no se puede revertir, la fila se reporta como
  `rechazado(reasignacion_incompleta)` y el número queda **sin activo** (recuperable a mano desde la
  ficha), nunca con dos activos.
- *Contador de secuencia como cuello de botella* → reserva de bloques por lote y reintento en `412`.
- *Recreación de la base mal ejecutada* (unique key equivocada, contenedores a medias) → la unique key
  es inmutable, así que el paso 4 de §3.2 es un chequeo obligatorio antes de habilitar la carga.
- *Archivo malicioso/enorme* → límite de tamaño, solo `.xlsx`/`.csv`, parseo en streaming, rate limit
  `publico`.

## 7. Criterios de aceptación / pruebas
**Lectura y validación**
- Unit: archivo `.xlsx` con la cabecera de §3 y N filas válidas → N `creado`; recarga → N
  `actualizado` (sin duplicar).
- Unit: cabecera distinta o columnas fuera de orden → `ErrorValidacion` (el lote no se procesa).
- Unit: fila sin `Telefono` o sin `Nombre` → `rechazado(fila_incompleta)`; el resto se procesa.
- Unit: fila **sin `Email`** → se crea igual (`creado`).
- Unit: teléfono no normalizable → `rechazado(numero_invalido)`.
- Unit: teléfonos duplicados dentro del archivo → primero gana, resto `rechazado(duplicado_en_archivo)`.
- Unit: email ya asociado a otro usuario **activo** → `rechazado(email_duplicado)`; el mismo email en
  un usuario **inactivo** → no bloquea.
- Unit: `Cargo`, `Sede`, `Empresa` y `Antigüedad` vacíos → la fila se crea igual.
- Unit: `Idioma` vacío → `es`; fuera de `{es,en}` → `rechazado(idioma_invalido)`.
- Unit: `Antigüedad` decimal (`16.391666`) → se guarda sin redondear.

**Identidad y estado**
- Unit: cada usuario creado recibe un `codigoUsuario` único y consecutivo; un lote de N altas consume
  exactamente N valores del contador.
- Unit: al actualizar, `codigoUsuario` y `usuarioWhatsapp` **no** cambian.
- Unit: `usuarioWhatsapp` presente en el modelo y en el DTO, pero **ignorado** por el lector aunque el
  archivo traiga una columna extra con ese nombre.
- Unit: teléfono de un usuario **inactivo** → se crea un usuario **nuevo** activo (`creado`), sin
  reactivar el anterior.
- Unit: `claveUnicidad` se calcula en el mapeo del repositorio — `wa|<numero>` si activo,
  `hist|<id>` si inactivo, `tag|<id>` para `Tag`; al inactivar un usuario la clave cambia sola.
- Integration (Cosmos): crear un segundo usuario **activo** con un número ya usado por otro activo →
  **`409`** desde la base, incluso saltándose la validación de aplicación.
- Integration (Cosmos): N usuarios inactivos con el mismo número conviven sin conflicto; varias `Tag`
  conviven sin conflicto (regresión del `null` de la unique key).
- Integration: `ObtenerUsuarioPorNumeroAsync` devuelve solo el activo cuando hay histórico inactivo
  con el mismo número; `ListarUsuariosPorNumeroAsync` los devuelve todos por `creadoEn`.
- Unit: `RepositoriosMemoria` filtra por activo igual que Cosmos (mismo test contra ambas
  implementaciones).
- Integration: mensaje entrante desde un número cuyo único registro está inactivo → no resuelve
  participante, cae en `rechazoParticipacion`; no se atribuye al titular anterior.
- Integration: login de admin con un usuario inactivo → rechazado.

**Conflicto de titular**
- Unit: teléfono existente + nombre muy similar (typo) → `actualizado`, sin conflicto.
- Unit: teléfono existente + nombre claramente distinto → `rechazado(conflicto_titular)` con el
  `codigoUsuario` del titular actual; **nada se escribe**.
- Unit: reenvío con `accion=reasignar` → anterior `inactivo`, nuevo usuario `activo` con nuevo `id` y
  nuevo `codigoUsuario`, sin heredar tags ni rol; resultado `reasignado`.
- Unit: si el alta del nuevo titular falla durante una reasignación, el anterior vuelve a `activo`
  (compensación); nunca quedan dos activos ni cero por un fallo silencioso.
- Unit: reenvío con `accion=corregir_nombre` → mismo `id` y `codigoUsuario`, nombre actualizado.
- Integration: tras reasignar, las conversaciones y evaluaciones previas siguen apuntando al `id`
  anterior (trazabilidad intacta) y no aparecen bajo el nuevo usuario.
- Integration: la reasignación queda auditada en `LogSeguridad` sin PII.

**Modos y endpoint**
- Unit: `modo=solo_actualizar` con teléfono inexistente → `rechazado(no_encontrado)`, sin alta.
- Unit: al actualizar, un campo opcional vacío **no** borra el valor previo; `rol`, `estado` y tags
  manuales se conservan.
- Unit: se asegura/crea la tag `t_emp_<idEmpresa>` y se asigna sin borrar las tags existentes.
- Integration: endpoint exige admin + CSRF; `.xlsx` y `.csv` funcionan; archivo > 2 MB → `400`;
  `campaniaId` inexistente → `404`; reporte completo sin fuga de PII en logs.
- Frontend lint/test/build verdes; la plantilla vacía se descarga desde el portal.

## 8. Orden de implementación sugerido
1. `03`/dominio: `codigoUsuario` + `Secuencia`, `usuarioWhatsapp`, campos nuevos, `area`/`empresa`
   opcionales, unicidad por estado. Sin backfill.
2. Repositorio: cálculo de `claveUnicidad` en el mapeo a documento, filtro por `estado = activo` en
   `ObtenerUsuarioPorNumeroAsync`, nuevo `ListarUsuariosPorNumeroAsync`; mismo comportamiento en
   `RepositoriosMemoria`.
3. **Recreación de la base y semilla (§3.2)**, con el chequeo de `409`. Bloquea todo lo que sigue: la
   unique key es inmutable, así que conviene hacerlo apenas el esquema esté cerrado.
4. Lectores `.xlsx`/`.csv` con las 9 columnas.
5. `ServicioCargaMasiva`: modos, conflicto de titular, reasignación, reporte.
6. Endpoint + contrato `04`.
7. Portal: upload, resolución de conflictos, descarga de plantilla, ficha de usuario con reasignación
   manual e histórico del número.

## 9. Estado de la carga real
| Paso | Estado |
|---|---|
| Plantilla oficial definida (9 columnas) | ✅ 7-ago-2026 |
| Base recreada con unique key `/claveUnicidad` + semilla admin (§3.2) | ⏳ pendiente |
| Backend listo (lector xlsx + validación + modos + reasignación) | ⏳ Sprint 1a |
| UI de carga en el portal | ⏳ Sprint 1b |
| Archivo V1 completo de GHT (`Telefono` diligenciado) | ⏳ pendiente GHT |
| **Ejecución de la carga real** | ⛔ **no ejecutar todavía** — paso del freeze |

## 10. Degradación
No aplica flag: el alta individual sigue disponible. Si ClosedXML se complica, la primera entrega
acepta solo `.csv` (la plantilla se exporta desde Excel como CSV con las mismas 9 columnas). Si la
resolución de conflictos en la UI no alcanza para Sprint 1b, la primera entrega deja los conflictos
como `rechazado` y la reasignación se hace manualmente desde la ficha de usuario.
