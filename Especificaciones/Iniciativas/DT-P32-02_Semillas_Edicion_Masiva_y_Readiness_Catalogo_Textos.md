# DT-P32-02 — Semillas seguras, edición masiva y readiness del catálogo de textos

> **Estado:** ESPECIFICADA — 2026-08-14 — 0/3, sin código ni cambios de configuración remota.
> **Prioridad:** inmediata; debe implementarse y validarse antes de retomar `DT-I20-02`.
> **Origen:** `QAS/resultados/Resultados_P32_Multidioma_2026-08-13.md`, donde la semilla `es`
> fue rechazada porque `FrasesDespertarProactivo` heredó más de 30 elementos.
> **Alcance:** semilla base `es/en`, migración legacy prevalidada, edición masiva JSON, readiness y
> precondición de catálogo al activar campañas bilingües.
> **No cambia:** snapshot de idioma, contenido propio de campaña, plantillas Meta, prompts/rúbricas,
> reglas conversacionales, aportes históricos ni estado remoto de los gates.

---

## 1. Problema confirmado

P-32 ya tiene un catálogo global por idioma, versionado en Cosmos `config`, editable desde el portal,
con ETag, importación/exportación JSON, activación explícita, rollback, caché, última versión válida y
respaldo compilado del mismo idioma. No se crea un segundo catálogo.

La corrida del 2026-08-13 encontró cuatro brechas de preparación:

1. `POST /api/admin/catalogos-textos/semillas/es` mezcla una **semilla segura** con una **fotografía
   de la configuración legacy**. Una lista heredada con más de 30 entradas hizo inválido el catálogo
   completo y dejó el ambiente sin versión española persistida.
2. El portal ya permite exportar e importar JSON, pero el contrato no describe el flujo masivo como
   operación principal ni ofrece una prevalidación previa a persistir.
3. El límite uniforme de 30 frases por grupo está compilado; no distingue el límite operativo del
   techo de seguridad ni permite aumentar el vocabulario sin recompilar.
4. La activación de una campaña bilingüe valida sus localizaciones, pero no demuestra que exista una
   versión global activa y válida para cada idioma, aunque P-32 §10 lo exige.

El gate `Conversacion:CatalogoTextosHabilitado` permaneció OFF. Las pruebas conversacionales que lo
requieren siguen pendientes y no se sustituyen por esta deuda.

---

## 2. Decisiones de arquitectura

### 2.1 Un catálogo global, dos orígenes de borrador

Se conserva `CatalogoTextosConversacion` como única fuente editorial global. Todas las campañas lo
consumen por el idioma fijado en el hilo; no copian saludos, menús, ayudas ni vocabulario determinista.
El contenido propio de campaña permanece en `Campania.localizaciones`.

Se separan dos operaciones:

- **Semilla base:** contenido `es/en` curado y compilado, independiente de App Settings. Debe ser
  completo y válido en toda compilación. Crea un borrador; nunca activa.
- **Migración legacy:** fotografía de `Conversacion:Mensajes:*` y `Conversacion:Frases*` efectivos.
  Primero se previsualiza y valida; solo una fotografía válida puede importarse como borrador.

Una configuración legacy inválida nunca impide crear la semilla base. No se truncan listas, no se
descartan entradas y no se mezclan parcialmente valores base con valores legacy sin mostrarlo.

### 2.2 Qué es editable sin compilar

El administrador puede cambiar masivamente, sin build ni despliegue:

- el valor de cualquier clave existente en `mensajes`;
- agregar, editar, ordenar o retirar entradas dentro de los grupos existentes de `frases`; y
- importar el catálogo completo de un idioma mediante JSON.

Las claves y grupos siguen siendo un registro cerrado del servidor. Agregar una clave semántica nueva
requiere código porque el runtime debe saber dónde consumirla. El JSON no puede inventar claves.

### 2.3 Versionado y publicación

- Semilla, importación masiva y migración legacy crean siempre una **versión nueva en borrador**.
- Nunca sobrescriben el borrador seleccionado ni la versión activa.
- `version`, `estado`, `huella`, ETag y campos de auditoría de un archivo exportado son informativos;
  el servidor no los acepta como instrucciones de importación.
- La activación sigue siendo una acción posterior, explícita, confirmada por un administrador y con
  validación completa.
- La versión activa anterior permanece intacta si la carga o validación falla.

### 2.4 Límites

Los límites editoriales pasan a política operativa de `OpcionesCatalogoTextos`:

- `MaxFrasesPorGrupo`, default `100`, rango permitido `1..500`;
- `MaxBytesImportacionJson`, default `262144` bytes (256 KiB), techo `1048576` bytes;
- longitud máxima de cada frase: `200` caracteres, sin cambio;
- longitud máxima de cada mensaje: `1000` caracteres, sin cambio.

El techo de 500 y el máximo absoluto de 1 MiB son guardas compiladas. Cambiar el límite operativo
dentro de esos techos no requiere recompilar. Un exceso devuelve error tipificado; nunca se recorta.

---

## 3. Contrato JSON para edición masiva

### 3.1 Flujo de usuario obligatorio

1. En **Textos de conversación**, elegir idioma y versión de origen.
2. Pulsar **Descargar JSON para edición masiva**.
3. Editar localmente solo los valores de `mensajes` y las listas de `frases`.
4. Seleccionar **Cargar JSON editado**.
5. El portal previsualiza idioma, conteos y todos los errores sin persistir.
6. Si es válido, el administrador confirma **Importar como nuevo borrador**.
7. El portal selecciona la versión creada y permite comparar contra la activa.
8. Activar exige una acción separada; cargar el archivo nunca publica.

### 3.2 Forma canónica editable

```json
{
  "formato": "catalogo-textos/v1",
  "familiaId": "catalogo_conversacion",
  "idioma": "es",
  "mensajes": {
    "saludoPrimerContacto": "Hola. Gracias por escribirnos."
  },
  "frases": {
    "despertarProactivo": [
      "hola",
      "quiero participar"
    ]
  }
}
```

La descarga contiene todas las claves obligatorias, no solo las del ejemplo. Se genera como UTF-8,
JSON indentado y nombre `catalogo-{familiaId}-{idioma}-v{version}-editable.json`.

Para mantener compatibilidad, el importador acepta exportaciones P-32 anteriores que incluyan
`version`, `estado`, auditoría, `huella` o `etag`; ignora esos metadatos. `formato` ausente equivale a
`catalogo-textos/v1`. Un `formato` desconocido se rechaza.

### 3.3 Reglas de prevalidación

La prevalidación y la importación ejecutan exactamente el mismo validador y devuelven todos los
errores detectables en una sola respuesta:

- JSON bien formado, UTF-8 y dentro del límite de tamaño;
- `familiaId` exacto e idioma `es|en`;
- idioma del archivo igual al seleccionado en portal; no se corrige silenciosamente;
- todas las claves obligatorias y ninguna desconocida;
- mensajes no vacíos, longitudes, texto plano y placeholders permitidos;
- entre 1 y `MaxFrasesPorGrupo` entradas por grupo;
- frases no vacías, dentro de longitud y sin duplicados después de normalizar;
- conteos totales de mensajes, grupos y frases para revisión humana.

Respuesta de prevalidación válida:

```json
{
  "valido": true,
  "familiaId": "catalogo_conversacion",
  "idioma": "es",
  "conteos": { "mensajes": 29, "gruposFrases": 16, "frases": 74 },
  "errores": []
}
```

La respuesta inválida usa `valido:false` y `errores[]` con `field`/`issue`; no devuelve ni registra
los textos. Para contenido JSON legible, prevalidar responde `200` aunque `valido=false`; JSON
malformado o por encima del tamaño permitido responde `400`. La importación real inválida siempre
responde `400` y no crea versión.

---

## 4. API aditiva

Se preservan las rutas P-32 y se agregan:

| Método | Ruta | Regla |
|---|---|---|
| POST | `/api/admin/catalogos-textos/semillas/{idioma}/base` | Crea nueva versión borrador desde la base curada, independiente del ambiente. |
| GET | `/api/admin/catalogos-textos/semillas/{idioma}/legacy/preview` | Prevalida la configuración efectiva sin persistir ni devolver secretos. |
| GET | `/api/admin/catalogos-textos/semillas/{idioma}/legacy/exportar` | Descarga la fotografía legacy completa, aun inválida, sin truncarla ni persistirla. |
| POST | `/api/admin/catalogos-textos/semillas/{idioma}/legacy` | Crea borrador solo si la fotografía legacy completa es válida. |
| POST | `/api/admin/catalogos-textos/importar/prevalidar` | Valida el mismo body de importación y no escribe. |
| GET | `/api/admin/catalogos-textos/readiness` | Resume gate efectivo, catálogos por idioma y bloqueos; admite `idioma`. |

`POST /api/admin/catalogos-textos/semillas/{idioma}` conserva su semántica P-32 por compatibilidad:
fotografía legacy para `es` y base curada para `en`. El portal nuevo deja de usar esa ambigüedad y
llama las rutas explícitas.

Todas las mutaciones exigen `admin` + CSRF. Preview/readiness permiten `admin|visor`. La importación
conserva el request actual (`familiaId`, `idioma`, `mensajes`, `frases`) y acepta `formato` de manera
aditiva. `Content-Type` debe ser `application/json`.

### 4.1 Readiness

Por cada idioma, la respuesta informa:

- si existe una versión activa;
- versión, huella y estado de validez, sin contenido;
- si existe al menos un borrador;
- si la semilla base vigente puede generarse;
- problemas tipificados de la fotografía legacy;
- `gateHabilitado` real del proceso; y
- campañas activas/borrador que quedarían bloqueadas por ausencia de catálogo.

`GET .../efectivo` sigue siendo preview y no prueba por sí solo el estado del gate.

---

## 5. Reglas de campaña

Al pasar una campaña bilingüe a `activa`, independientemente del gate:

1. validar localizaciones completas como hoy;
2. exigir una versión global activa y válida para cada idioma habilitado; y
3. devolver `400 VALIDATION_ERROR` con `catalogosTextos.{idioma}: activo_requerido` si falta.

Una campaña histórica inconsistente sigue excluida del enrutamiento. Con gate OFF, una campaña
monolingüe española legacy conserva compatibilidad. Con gate ON, readiness es rojo si cualquier
campaña activa carece de catálogo válido para alguno de sus idiomas.

No se validan plantillas Meta mediante secretos. Readiness solo puede informar la existencia del
mapeo operativo no secreto; la aprobación real de Meta permanece como prerequisito humano.

---

## 6. Portal

La pantalla **Textos de conversación** debe ofrecer, con lenguaje simple:

- **Crear semilla base** y **Revisar/importar configuración anterior** como acciones distintas;
- **Descargar configuración anterior como JSON** aunque la prevalidación legacy encuentre errores,
  para corregirla y cargarla por el flujo masivo sin perder entradas;
- **Descargar JSON para edición masiva** en cualquier versión;
- selector de archivo `.json`, máximo configurado y ayuda de qué campos editar;
- prevalidación con idioma, conteos y lista completa de errores;
- confirmación separada para importar como nuevo borrador;
- rechazo visible si el idioma del archivo difiere del selector;
- comparación del borrador nuevo contra la versión activa;
- readiness `es/en` y explicación de por qué una campaña está bloqueada; y
- las acciones existentes de edición individual, guardar, activar, reactivar y rollback.

El selector de archivo se limpia después de cada intento para permitir volver a escoger el mismo
archivo corregido. El portal no intenta reparar JSON, truncar listas ni activar automáticamente.

---

## 7. Seguridad, auditoría y observabilidad

- El JSON es contenido editorial de negocio; no contiene secretos ni ids físicos de Meta.
- Validar tamaño antes de deserializar y limitar profundidad a la forma contractual.
- No aceptar claves adicionales como configuración dinámica del servidor.
- Escapar preview; no usar `innerHTML`.
- `LogSeguridad(catalogoTextosConversacion)` registra acción, actor, idioma, tamaño, conteos, versión,
  resultado, motivo, huella y `correlationId`; nunca JSON, mensajes, frases ni diferencias.
- Acciones nuevas: `crearSemillaBase`, `prevalidarLegacy`, `importarLegacy`,
  `prevalidarImportacion` e `importarMasivo`.
- Una prevalidación no crea documentos ni invalida caché.

---

## 8. Cortes de implementación

### Corte 1/3 — Semilla base y validación reutilizable

- separar semilla base de fotografía legacy en Application;
- parametrizar límites operativos con techo seguro;
- crear prevalidación pura compartida por seed/import;
- agregar rutas de semilla base y preview/import legacy;
- mantener compatible la ruta P-32 existente;
- pruebas unitarias de semilla `es/en`, legacy con más de 30 frases y ausencia de truncamiento.

### Corte 2/3 — JSON masivo, readiness y campañas

- formalizar `formato:catalogo-textos/v1` y descarga editable;
- agregar prevalidación sin escritura e importación como nueva versión borrador;
- agregar readiness administrativo;
- exigir catálogos activos al activar campaña bilingüe;
- conservar asociación/enrutamiento defensivos;
- pruebas API, ETag, auditoría sin contenido y regresión gate OFF.

### Corte 3/3 — Portal, QAS y handoff

- implementar el flujo descargar → editar → prevalidar → confirmar → nuevo borrador;
- mostrar conteos, errores, diferencia y readiness;
- pruebas Angular de accesibilidad, mismo archivo corregido y no activación;
- actualizar `QAS/16`, `QAS/17`, `QAS/22`, AVANCES y TODO;
- ejecutar validación local completa secuencial.

Después de desplegar controladamente, se ejecuta `QAS/22` y luego la corrida completa P-32 de
`QAS/17`. Solo si ambas quedan verdes, incluidos los pasos con gate ON, D5/UAT y los prerequisitos
externos aplicables, se retoma `DT-I20-02` corte 1/3.

---

## 9. Pruebas mínimas

### Backend unitarias

- semilla base `es/en` contiene todas las claves y siempre valida;
- opciones legacy con 31 o más frases no afectan la semilla base;
- preview legacy reporta el grupo y conteo exactos sin persistir;
- límite operativo mayor a 30 funciona sin recompilar y respeta el techo duro;
- duplicados normalizados, HTML, placeholders y claves desconocidas siguen rechazados;
- metadatos exportados no controlan versión/estado al reimportar;
- no hay truncamiento ni mezcla parcial base/legacy.

### Integración API

- descargar una versión y reimportarla editada crea `v+1` borrador;
- prevalidar no crea documentos ni cambia ETag/caché;
- archivo inválido devuelve todos los errores y deja intacta la activa;
- idioma distinto al seleccionado se rechaza;
- visor descarga/prevalida y no importa; admin+CSRF sí;
- campaña bilingüe no activa sin catálogo `es` o `en` activo;
- campaña activa cuando catálogos y localizaciones están completos;
- gate OFF conserva el comportamiento legacy.

### Portal

- botones y selector tienen nombre accesible;
- descarga usa nombre y UTF-8 esperados;
- prevalidación muestra conteos/errores y no activa;
- confirmar crea y selecciona un borrador nuevo;
- cancelar no escribe;
- puede elegirse nuevamente el mismo archivo corregido;
- el readiness explica el idioma faltante.

---

## 10. Criterios de aceptación

1. En un ambiente sin catálogos, un admin crea borradores base válidos `es/en` aunque App Settings
   legacy contenga una lista inválida.
2. Descargar, editar y cargar nuevamente el JSON completo permite cambiar en masa mensajes y frases
   sin compilar ni desplegar.
3. La carga se previsualiza antes de escribir y, al confirmar, crea una nueva versión borrador.
4. Importar nunca activa, sobrescribe ni modifica la versión activa.
5. Se pueden administrar hasta el límite operativo de frases por grupo sin recompilar; el techo de
   seguridad no puede excederse ni produce truncamiento.
6. Readiness distingue gate, catálogo activo, borrador, emergencia y bloqueos de campaña.
7. Una campaña bilingüe no se activa sin catálogo global activo y localizaciones completas por idioma.
8. Logs y errores no contienen JSON ni textos del catálogo.
9. Gate OFF conserva la regresión legacy; gate ON nunca cae de inglés a español.
10. Build, pruebas no-Calibración, formato, frontend y `git diff --check` quedan verdes.
11. La nueva corrida P-32 verifica que el aporte y la idea consolidada en inglés permanezcan en inglés.
12. `DT-I20-02` no comienza hasta registrar este cierre y la corrida P-32 verde.

---

## 11. Fuera de alcance

- Traducir automáticamente catálogos, campañas o aportes.
- Permitir claves semánticas nuevas sin cambio de contrato/código.
- Guardar el JSON como archivo fuente de runtime; Cosmos sigue siendo la fuente de verdad.
- Activar automáticamente una semilla o importación.
- Modificar plantillas Meta, secretos, App Settings o gates remotos durante desarrollo.
- Corregir `DT-I20-02`; queda explícitamente después del cierre verde de esta deuda.
- Resolver dentro de esta deuda el emisor real observado durante simulación. La nueva corrida exige
  ambiente aislado o autorización humana y números de prueba controlados; si no existen, queda
  `BLOCKED`, no se envía a participantes reales.

---

## 12. Rollback

1. Mantener `Conversacion:CatalogoTextosHabilitado=false` durante desarrollo y despliegue inicial.
2. Si falla la UI nueva, conservar API y flujo individual P-32 existentes.
3. Si una importación editorial es incorrecta, no activarla o reactivar la versión anterior.
4. No borrar versiones ni editar documentos Cosmos manualmente.
5. Revertir los commits de la deuda si la regresión gate OFF cambia; no compensar con configuración
   remota.
