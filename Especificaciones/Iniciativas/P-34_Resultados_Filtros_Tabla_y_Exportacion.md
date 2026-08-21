# P-34 — Resultados: identidad, filtros, tabla ordenable, exportación y resumen de campaña

> Estado: **COMPLETA — 6/6 DONE local (cortes 1 y 2 el 2026-08-20; cortes 3 a 6 el 2026-08-21)**
> Origen: solicitud del usuario (2026-08-20) como especialista en UX/UI sobre la pantalla de Resultados
> Tipo: Desarrollo **frontend + backend aditivo** · Prioridad: Alta · Ventana: previa a la convención
> Dependencias: **I-17**, **I-19** (idea consolidada), **P-18/P-19** (accesibilidad), **P-23** (maestro-detalle)
> Absorbe de **P-04**: filtros de servidor, ranking por calificación y exportación CSV
> Riesgo: Medio — cambia `04 §5.8` de forma **aditiva**; no toca `03`, ni rutas, ni permisos
> Handoff: **iniciativa completa, sin trabajo de código pendiente.** `04 §5.8` publica la identidad
> embebida, los filtros y `orden`/`dir` (`98b8bca`), la exportación (`c9181a6`) y el resumen
> (`267c0b7`). Lo que quedó deliberadamente fuera y la puerta operativa abierta (RU/latencia reales
> contra Cosmos) están en `SUPUESTOS.md#cierre-p-34`. Los commits siguen sin publicar.

---

## 1. Resumen ejecutivo

Resultados es la pantalla donde un `admin` o un `visor` revisa lo que produjo una campaña. P-23
(2026-07-25) resolvió su navegación —maestro-detalle, selección sin fricción, lectura fácil— y dejó
explícitamente fuera «los filtros de servidor más ricos», que quedaron «como una iniciativa aparte
con su cambio de `04`». **P-34 es esa iniciativa**, ampliada con lo que la operación real destapó:

1. **La identidad del participante no siempre aparece**: la pantalla muestra el `usuarioId` técnico
   (`u_58b9f811…`) en vez del nombre.
2. **No hay fechas ni metadata** en pantalla, aunque la API ya las devuelve.
3. **No se puede ordenar ni agrupar**: ver todas las ideas de una persona, o las mejor calificadas,
   exige recorrer la lista con la vista.
4. **No hay exportación** de datos ni resumen: solo se puede bajar un `.md` por idea.
5. **Faltan filtros**: solo hay campaña y estado de la idea.

Durante la auditoría del código aparecieron además **cuatro defectos en producción** y **un problema
de escala** que P-34 corrige antes de agregar nada nuevo.

---

## 2. Estado actual: qué existe y qué está roto

Base a conservar: `ResultadosPage` (P-23) con selector de campaña recordado en sesión, filtro por
`estadoResultado`, lista maestra de ideas con badges y extracto, panel de detalle con idea
consolidada, evaluación vigente, historial plegable y Markdown con **Regenerar** (solo admin) y
**Descargar .md**.

### 2.1 Defectos confirmados leyendo el código

| Id | Hallazgo | Evidencia |
|----|----------|-----------|
| **H-01** | El nombre del participante cae al id técnico y el fallo es invisible. `nombreUsuario()` devuelve `usuarioId` cuando el usuario no está en el mapa, y el error de la carga del maestro se descarta a propósito. Si `GET /usuarios` falla, **todas** las filas muestran el GUID y la pantalla se ve normal. | `resultados.page.ts` — `error: () => { /* el id técnico sigue siendo el fallback */ }` |
| **H-02** | El portal pide `pageSize: 500` y el servidor recorta a 100 sin avisar. Del participante 101 en adelante, H-01 es seguro. | `EndpointsAdminConfiguracion.TamanoPaginaMaximo = 100` vs. `api.usuarios({ pageSize: 500 })` |
| **H-03** | «Descargar .md» desaparece a partir de la idea 26. `api.markdown(campaniaId)` viaja sin `pageSize`, el servidor entrega 25, y el portal —que busca el artefacto por `ideaRef` dentro de esos 25— concluye «esta idea aún no tiene un documento Markdown disponible». El documento existe. | `admin-api.service.ts::markdown()` + `cargarMarkdownDeIdea()` |
| **H-04** | Los contadores cuentan la página cargada, no la campaña: `ideas().length` y `conteoIdeas()` ignoran el campo `total` que la API ya devuelve. Igual con conversaciones y respuestas históricas, que además llegan de 25 en 25. | `resultados.page.ts` — plantilla del resumen |
| **H-10** | **Escala.** `ListarIdeasAsync` resuelve la versión vigente idea por idea (`ObtenerVersionIdeaAsync`, hasta dos *point reads* cada una) **antes** de paginar. Con las 1.000 ideas previstas para la convención son ~2.000 lecturas puntuales por cada carga y por cada cambio de filtro. El detalle es peor: `ObtenerIdeaAsync` carga la partición completa de respuestas para quedarse con los aportes de una idea. | `EndpointsAdminResultados.cs` |

### 2.2 Capacidades desaprovechadas

| Id | Hallazgo |
|----|----------|
| **H-05** | `creadaEn`, `actualizadaEn`, `ideaIndice`, `motivoCierre`, `versionConfirmadaRef`, `fechaInicio`/`fechaCierre` de la conversación y `fecha`/`calificacionTotal` de la evaluación ya viajan en la respuesta HTTP y no se pintan. La pantalla no muestra **ni una sola fecha**. |
| **H-06** | `GET /admin/ideas` ya acepta `usuarioId`, `preguntaId`, `estadoFlujo` y `estadoCuraduria`; el portal solo expone campaña y `estadoResultado`. |
| **H-07** | No hay orden alternativo al del servidor (`preguntaId → ideaIndice → creadaEn`), ni agrupación, ni calificación en la lista. |
| **H-08** | La única exportación es un `.md` por idea, nombrado con el id del artefacto (`art_9f2c….md`). |
| **H-09** | Ni la campaña ni el filtro viven en la URL: no se puede compartir una vista, ni recargar sin perderla, ni usar el botón atrás. |

---

## 3. Decisiones cerradas (2026-08-20)

| # | Decisión | Consecuencia |
|---|----------|--------------|
| D1 | **El nombre real del participante puede salir en un archivo exportado.** | El export lleva nombre, código, área, empresa y sede. Se añade una casilla **«exportar anonimizado»** que sustituye el nombre por `codigoUsuarioLegible`. P-07 sigue gobernando el informe público (P-11), no esta consulta interna. |
| D2 | **Iniciativa nueva: P-34**, no continuación de P-04. | P-34 absorbe de P-04 los filtros de servidor, el ranking por calificación y la exportación CSV. A P-04 le queda la analítica dependiente de terceros: cobertura de *seed thoughts* contra I-12 y tarjetas por eje de rúbrica. |
| D3 | **Escala objetivo: 1.000 ideas por campaña.** | H-10 deja de ser una nota al pie y se convierte en un corte propio, **anterior** a los filtros: cada filtro nuevo multiplica el costo de un listado que hoy hace hasta dos lecturas puntuales por idea antes de paginar. |
| D4 | **La curaduría se mira, no se marca.** | Aprobar/descartar una idea toca el dominio y queda fuera de P-34. Sí se deja la **columna de selección múltiple** en la tabla, aunque no ejecute ninguna acción, para que la curaduría futura sea añadir un botón y no rehacer la tabla. |
| D5 | **El resumen de campaña entra al plan** (corte 6). | Necesita endpoint propio: con 1.024 ideas, calcular cuatro barras en el navegador obliga a descargarlas todas. |

### 3.1 Menudencias con opción por defecto

Si no hay indicación en contra, se implementa la opción marcada:

- **Botón «Exportar» sin desplegar** → **Excel (.xlsx) del listado visible**; el menú queda para los
  demás recursos y el CSV.
- **Vista por defecto** → **tabla**, recordando la última usada durante la sesión con el mismo
  mecanismo de `ResultadosSesionService` (sin `localStorage`, `01 §11`).
- **Resumen de campaña** → **plegado** por defecto, con las cinco cifras de la barra siempre visibles.

---

## 4. Diseño

Todo lo visual reutiliza los tokens (`--ght-*`) y primitivas (`page-grid`, `panel`, `panel-heading`,
`filters-grid`, `detail-grid`, `status-badge`, `compact-list`) existentes. **Sin sistema visual
nuevo, sin librerías nuevas** (`11 §5`, `01 §11`).

### 4.1 La decisión de fondo: la identidad la resuelve el servidor

Hoy el portal descarga el maestro de usuarios y hace el *join* contra un `Map` en el navegador. Ese
join es el origen de H-01 y H-02, y es también lo que impide filtrar u ordenar por área, empresa o
sede: el servidor pagina y ordena sin conocer esos campos, así que cualquier refinamiento posterior
en cliente miente sobre el total.

**`GET /api/admin/ideas` devuelve un objeto `participante` embebido** con `codigoUsuarioLegible`,
`nombre`, `area`, `empresa`, `sede` y `estado`. Es aditivo, no expone nada que el mismo rol no pueda
leer ya en `/usuarios`, y elimina la clase entera de bug.

Fallback visible (no silencioso): cuando el servidor no puede resolver al participante, la fila dice
**«Participante no identificado»** con el código corto, botón de copiar el id completo, y la pantalla
anuncia el problema en la región asertiva (P-19) con opción de reintentar.

### 4.2 Barra de filtros en dos niveles

- **Nivel 1, siempre visible:** Campaña · Buscar (nombre, código o texto de la idea) · Estado ·
  Rango de fechas · botón «Más filtros · N».
- **Nivel 2, panel desplegable:** participante, pregunta, área, empresa, sede, estado de flujo,
  curaduría, confirmada sí/no, calificación mín./máx., con/sin documento, con/sin evaluación.
- **Chips removibles** de todo lo aplicado, con «Limpiar todo». El usuario siempre puede ver por qué
  la lista muestra lo que muestra y desarmarlo de a un filtro.
- **Estado del filtro serializado en la URL** (query params), de modo que la vista se comparte
  pegando el enlace, sobrevive a un F5 y respeta el botón atrás (corrige H-09).

### 4.3 Vista tabla ordenable como principal; la lectura se conserva

El maestro-detalle sirve para leer una idea; no sirve para comparar mil. Entra una **vista tabla**
por defecto y el maestro-detalle de P-23 se conserva como **vista lectura**.

- Columnas: selección · participante (nombre + código) · área · pregunta · extracto de la idea ·
  estado · calificación · versiones · aportes · creada · actualizada · documento.
- **Orden por columna** resuelto en el servidor (`orden` + `dir`). Ordenar en el cliente con
  paginación es un orden falso.
- **Agrupar por participante** (colapsable), que es la forma natural de responder «¿qué aportó esta
  persona?».
- Selector de columnas y de densidad; tamaño de página configurable.
- **Paginación honesta:** «Mostrando 1–25 de 48 filtradas · 1.024 en la campaña», usando `total`
  (corrige H-04).
- Click en la fila abre el panel de detalle sin perder la posición en la tabla; `aria-current` se
  conserva (P-18/P-19).

### 4.4 Ficha de la idea con toda la metadata

El panel de detalle deja de ser solo texto y muestra: código y nombre del participante, área,
empresa y sede, campaña, pregunta, `ideaIndice`, creada y actualizada (fecha absoluta en
`America/Bogota` + relativa), confirmada y número de versión, curaduría, calificación, rúbrica y
versión, modelo LLM del snapshot, motivo de cierre e id técnico copiable (corrige H-05).

**Línea de tiempo:** los aportes y las versiones dejan de ser dos listas dentro de un `<details>` y
se intercalan en una sola secuencia cronológica —aporte, versión propuesta, evaluación, complemento,
versión confirmada— que es como realmente ocurrió la conversación.

### 4.5 Exportación con alcance explícito

«Descargar» no es una acción sino tres, y conviene decirlo antes de bajar el archivo:

| Recurso | Grano | Contenido |
|---------|-------|-----------|
| **Ideas** | una fila por idea | participante, código, área, empresa, sede, pregunta, texto vigente, estado, calificación, versiones, fechas |
| **Aportes** | una fila por mensaje | texto, tipo de aporte, versión asociada, fecha |
| **Evaluaciones** | una fila por evaluación | calificación por criterio, rúbrica y versión, recomendación, temas, modelo |
| **Documentos** | un archivo por idea | ZIP de `.md` nombrados `GHT-0142_Marta-Rueda_idea-2.md` |

- Formatos **.xlsx** (cabeceras congeladas, anchos ajustados) y **.csv** (UTF-8 con BOM, para que
  Excel no rompa los acentos).
- **Se resuelve en el servidor**, no en el navegador: mismo filtro, mismo orden, sin techo de página,
  una sola fuente de verdad.
- Nombre automático: `Convencion-GHT-2026_ideas_maduras_2026-08-20.xlsx`.
- Primera hoja **«Filtros aplicados»** con campaña, filtros, orden, total, fecha y quién exportó: sin
  eso, un CSV suelto en un correo no se puede auditar tres semanas después.
- Casilla **«exportar anonimizado»** (D1).

### 4.6 Resumen de campaña (corte 6)

Panel plegable sobre la tabla, que respeta el filtro activo:

- **Participación:** participantes con al menos una idea sobre convocados, y promedio de ideas por
  participante activo.
- **Embudo:** iniciadas → confirmadas → con evaluación vigente → maduras.
- **Distribución de calificaciones:** histograma con la mediana y el umbral de madurez marcados.
- **Cobertura por pregunta:** barras apiladas maduras/pendientes/rechazadas, con leyenda.
- **Temas más frecuentes:** los `temas` de las evaluaciones, con conteo.

Cada gráfico lleva tooltip con el valor exacto y una **vista de tabla equivalente**; el color nunca
va solo: cada segmento tiene etiqueta y leyenda (P-18/P-19).

### 4.7 Estados vacíos, de carga y error

Con diez filtros combinables, «no hay resultados» es inútil. El vacío **nombra el filtro más
restrictivo y ofrece quitarlo**: «Hay 546 ideas maduras en esta campaña, pero ninguna del área
Operaciones con calificación ≥ 7,0 en ese rango» + botones «Quitar calificación ≥ 7,0», «Ampliar a
todo agosto», «Limpiar todo». Los *skeletons* toman la forma de la tabla. Los errores van a la región
asertiva y las confirmaciones a la educada (P-19), sin doble anuncio.

---

## 5. Contratos y configuración

Todo aditivo. `03` no cambia. `04 §5.8` se actualiza en commit aparte, con su entrada en
`SUPUESTOS.md` reemplazando lo diferido en `#fase8-consultas-resultados`.

| Endpoint | Cambio | Tipo |
|----------|--------|------|
| `GET /admin/ideas` | Objeto `participante` embebido (código legible, nombre, área, empresa, sede, estado, `resuelto`). **Implementado (corte 3)** | aditivo |
| `GET /admin/ideas` | `calificacionTotal` y `evaluadaEn` de la evaluación vigente. **Implementado (corte 3)** | aditivo |
| `GET /admin/ideas` | Filtros `q`, `area`, `empresa`, `sede`, `desde`, `hasta`, `calificacionMin`, `calificacionMax`, `confirmada`. **Implementado (corte 3)** | aditivo |
| `GET /admin/ideas` | `orden` = `participante\|calificacion\|creada\|actualizada\|pregunta` + `dir` = `asc\|desc`. **Implementado en el servidor (corte 3); la UI llega con la tabla del corte 4** | aditivo |
| `GET /admin/ideas/{id}` | Traer los aportes por `ideaId` en vez de cargar la partición de respuestas | interno |
| `GET /admin/markdown` | Sin cambio de contrato: el portal debe pasar `pageSize` y paginar (corrige H-03) | solo portal |
| `GET /admin/campanias/{id}/exportar` | **Nuevo.** `recurso` = `ideas\|aportes\|evaluaciones`, `formato` = `xlsx\|csv`, `anonimizado` = `true\|false`, más los filtros del listado. **Implementado (corte 5)** | nuevo |
| `GET /admin/campanias/{id}/documentos.zip` | **Nuevo.** ZIP de los `.md` con nombres legibles. **Implementado (corte 5)** | nuevo |
| `GET /admin/campanias/{id}/resumen` | **Nuevo.** Participación, embudo, histograma, cobertura y temas; acepta los mismos filtros. **Implementado (corte 6)** | nuevo |
| `GET /admin/usuarios` | Subir el tope de `pageSize` o exponer `continuationToken` (red de seguridad para H-02). **Ya no hace falta para Resultados**: el corte 1 pagina y el corte 3 resuelve la identidad en el servidor | aditivo |
| `IRepositorioUsuarios` | **Nuevo** `ListarUsuariosPorIdsAsync`: identidad por bloques de ids dentro de la partición de usuarios. **Implementado (corte 3)** | interno |
| `IRepositorioRespuestas` | **Nuevo** `ListarEvaluacionesPorIdsAsync`: calificación vigente en una consulta por ids. **Implementado (corte 3)** | interno |
| `IRepositorioRespuestas` | **Nuevo** `ListarVersionesDeCampaniaAsync(campaniaId, versionIds, ct)`: una query por partición (`ARRAY_CONTAINS`) en vez de N lecturas puntuales. Recibe los ids de la página —no toda la campaña— para no traer el texto de ~2.000 documentos; degrada por defecto a lecturas puntuales. **Implementado (corte 2)** | interno |
| `IRepositorioRespuestas` | **Nuevo** `ListarRespuestasPorIdeaAsync`: aportes por `ideaId`; degrada por defecto al filtro en memoria. **Implementado (corte 2)** | interno |

Permisos sin cambios: lectura para `admin`/`visor`; el export es `GET` y por tanto también lectura,
bajo el mismo guard admin, sin PII fuera de él.

---

## 6. Rendimiento a 1.000 ideas

D3 convierte esto en un corte propio y **anterior** a los filtros:

1. `ListarVersionesDeCampaniaAsync` + join en memoria: de ~2.000 *point reads* a 1 query.
2. Resolver la versión vigente **después** de paginar cuando el orden no dependa del texto.
3. `ObtenerIdeaAsync` deja de leer la partición completa de respuestas.
4. **Medición obligatoria:** RU consumidas y latencia del listado con 1.000 ideas sembradas, antes y
   después, registradas en el commit del corte.

Si tras esto el listado sigue caro, la salida siguiente —no incluida en P-34— es denormalizar
`textoVigente` y `calificacionVigente` en el documento de `IdeaConsolidada`, lo que sí tocaría `03`.

---

## 7. Riesgos y mitigación

- *Regresión de accesibilidad (P-18/P-19)* → conservar nombres accesibles y `aria-current`; la tabla
  necesita `scope`, encabezados ordenables anunciados con `aria-sort` y navegación por teclado;
  pruebas de lector y teclado.
- *Filtros en memoria que crecen sin control* → todos se aplican dentro de la partición `campaniaId`
  y después del corte 2; se mide RU en cada corte que añada filtros.
- *Export pesado* → respuesta en streaming y tope explícito documentado; nunca construir el archivo
  completo en memoria.
- *Fuga de PII* → D1 autoriza el nombre en el archivo, pero el endpoint sigue tras el guard admin y
  la casilla de anonimizado queda disponible desde el primer día.
- *Sobre-alcance* → la curaduría accionable (D4) y la analítica de P-04 quedan explícitamente fuera.

---

## 8. Criterios de aceptación

1. Con el maestro de usuarios caído, la pantalla **lo dice** y muestra «Participante no identificado
   · código», nunca un GUID pelado (prueba de componente con la llamada en error).
2. Los contadores coinciden con `total` del servidor, no con el largo del arreglo (regresión H-04).
3. Una campaña con más de 25 artefactos muestra «Descargar .md» en todas las ideas que lo tengan
   (regresión H-03).
4. El listado de 1.000 ideas se sirve sin lecturas puntuales por idea; la medición queda en el commit
   (regresión H-10).
5. `GET /admin/ideas` devuelve `participante` y `calificacionTotal`; filtrar por área y ordenar por
   calificación devuelve el conjunto correcto **y el total correcto** (pruebas de integración).
6. La URL refleja el filtro: pegarla en otra pestaña reproduce la misma vista.
7. La tabla ordena por cualquier columna, agrupa por participante y anuncia el orden con `aria-sort`.
8. El export de las tres formas produce archivo con la hoja de filtros, y el anonimizado no contiene
   ningún nombre.
9. El resumen coincide con lo que muestra la tabla para el mismo filtro.
10. Verde en cada corte: `prettier --check`, `ng test`, `ng build --configuration production` con Node
    24.15.0, y backend con `-warnaserror` + unitarias e integración.

---

## 9. Degradación

Puramente aditivo. Si `participante` no viaja (servidor viejo), el portal cae al comportamiento
actual con el fallback legible. Si un filtro nuevo no está soportado, no se ofrece. Si el endpoint de
export o el de resumen no existen, sus controles no se muestran. Revertir P-34 devuelve la vista de
P-23 sin afectar datos ni contratos.

---

## 10. Plan de implementación

| # | Corte | Qué entrega |
|---|-------|-------------|
| **1** ✅ | **Los cuatro bugs** (frontend) — **DONE local 2026-08-20** | Recorrido de páginas hasta agotar `total` en `/usuarios`, `/markdown`, `/respuestas` y `/conversaciones` con `pageSize` en el tope real (100), degradando a una sola página si el servidor no informa `total`; contadores tomados de `total`, con aviso «(sobre las N primeras)» mientras el listado de ideas siga trayendo una página (se retira con el corte 2); error de `/usuarios` visible en la región asertiva y reintentable; «Participante no identificado · código» en vez del id técnico. Portal 76 pruebas en 10 archivos, `ng build` producción y Prettier verdes; backend sin cambios. |
| **2** ✅ | **Que aguante 1.000 ideas** (backend) — **DONE local 2026-08-20** | Se filtra, ordena y **pagina antes** de resolver versiones; las de la página se piden en una sola consulta por ids dentro de la partición (`ListarVersionesDeCampaniaAsync`); el detalle trae los aportes por `ideaId` (`ListarRespuestasPorIdeaAsync`); ambos con degradación por defecto en el puerto. El portal recorre además el listado completo de ideas, lo que vuelve exacto el desglose por estado de H-04. Medición con 1.000 ideas y 5.000 aportes: listado de **1.000 lecturas puntuales a 0** (403 → 332 ms) y detalle de **5.000 documentos de respuesta a 5** (701 → 496 ms), en operaciones e in-process; **RU/latencia reales contra Cosmos siguen pendientes como puerta operativa**. |
| **3** ✅ | **Identidad y filtros en el servidor** — **DONE local 2026-08-21** | `participante` embebido (con `resuelto`) y `calificacionTotal`/`evaluadaEn` en `/ideas`; filtros `q`, área, empresa, sede, fechas, calificación y confirmada, más `orden`/`dir`, con `400` y todos los motivos ante una consulta inválida; identidad y evaluaciones por consultas acotadas a ids; barra de dos niveles con chips removibles, estado del filtro en la URL y vacío que nombra el filtro. Quedan fuera «con/sin documento» y «con/sin evaluación», y la **UI** de ordenamiento pasa al corte 4. |
| **4** ✅ | **Tabla, orden y metadata** (solo portal) — **DONE local 2026-08-21** | Vista tabla como principal con orden resuelto en el servidor (`orden`/`dir`), anunciado con `aria-sort` y con tercer clic al orden natural; agrupación por participante colapsable, selector de columnas, densidad, tamaño de página y paginación honesta («Mostrando 1–25 de 30 filtradas · 120 en la campaña»); vista lectura de P-23 conservada y recordada en sesión; ficha con toda la metadata de H-05 —fechas absolutas en `America/Bogota` más relativas, rúbrica/modelo, id copiable— y línea de tiempo única de aportes y versiones; selección múltiple prevista sin acción (D4). **No se implementaron las columnas «versiones» y «aportes»**: sus conteos no están en el DTO de lista y exigirían otro cambio aditivo de `04 §5.8`. |
| **5** ✅ | **Exportación** — **DONE local 2026-08-21** | `/exportar` con los tres recursos en xlsx y csv y `documentos.zip` con nombres legibles, todos con **los filtros de la pantalla** e ignorando `page`/`pageSize`; hoja «Filtros aplicados» (o líneas `#` en csv) con campaña, filtros, orden, total, fecha y quién exportó; casilla de anonimizado (D1) que alcanza también a los nombres del ZIP; csv en UTF-8 con BOM; tope de 10.000 filas con `400`; `xlsx` y ZIP escritos en archivo temporal y enviados asíncronos, nunca enteros en memoria. Listado y exportación comparten `ConsultaResultadosCompartida`. |
| **6** ✅ | **Resumen de campaña** — **DONE local 2026-08-21** | `/resumen` con los mismos filtros y el mismo alcance que la tabla (`totalIdeas` = `total` del listado, §8.9): participación sobre la convocatoria completa, embudo acumulativo, histograma sobre la escala de la rúbrica, cobertura por pregunta y temas estables. Panel plegado con las cinco cifras en el encabezado, una tabla con barra por gráfico —sin librerías nuevas— y marca de umbral solo cuando aplica a todas las barras. |

Cada corte deja el repositorio verde y es desplegable sin esperar al siguiente.

---

## 11. Relación con otras iniciativas

- **P-23** — construye sobre su maestro-detalle, que se conserva como «vista lectura».
- **P-04** — absorbido en su parte de filtros, ranking y export CSV (D2); le queda la analítica que
  depende de I-12.
- **P-07** — D1 autoriza el nombre en la consulta interna; el informe público conserva su decisión de
  consentimiento.
- **P-11** — el ZIP de documentos del corte 5 es insumo directo del informe consolidado.
- **DT-QA-02** — `GET /admin/evaluaciones` ya expone `ideaId` y `calificacionTotal`; P-34 lo usa como
  camino de transición mientras el corte 3 embebe la calificación en `/ideas`.

> **Nota de entorno:** `ng build`/`ng test` requieren Node 24.15.0; la carpeta sincronizada por
> OneDrive puede bloquear esbuild — verificar en entorno local; el CD reconstruye `wwwroot` en Linux.
