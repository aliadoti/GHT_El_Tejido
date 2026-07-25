# P-23 — UX de la pantalla de Resultados (maestro-detalle claro, selección sin fricción, lectura fácil)

> **Origen:** solicitud del usuario (2026-07-25) como especialista en UX: mejorar la interacción del
> administrador en **Resultados** — instrucciones claras, navegación fácil, listas legibles, espacios
> bien definidos.
> **Tipo:** Desarrollo **frontend-only** (Angular 22, portal) · **Prioridad:** Media · **Ventana:** a
> coordinar (rama de mejoras de portal; fuera de la ruta crítica del Hito).
> **Dependencia:** construye sobre la pantalla de Resultados de **I-17** (filtro de madurez, badges,
> conteos) y sobre **P-18/P-19** (nombres accesibles y regiones vivas). **No** depende de insumos
> externos. · **Riesgo:** Bajo — **no cambia contratos `03`/`04`**, ni rutas, ni permisos; usa los
> mismos endpoints (`conversaciones`, `respuestas` con filtro de madurez, `respuesta`, `markdown`,
> `regenerarMarkdown`). Solo reorganiza layout, copia y navegación con los tokens/primitivas ya
> existentes. Cubre `REQ §27.3/§33.1.17-21`, `ARQ §3`; spec base `11 §6/§7`.
> **Estado:** **TODO — especificación lista, implementación pendiente.** Sin código.

## 1. Qué pide / por qué
Resultados es donde el administrador (o un `visor`) revisa lo que produjo una campaña: conversaciones,
respuestas, evaluaciones y el Markdown generado. Hoy la información se presenta en tres listas compactas
paralelas y detalles apilados debajo, lo que dificulta **saber qué estás viendo, cómo pasar de una
respuesta a su evaluación y a su Markdown, y qué significan las etiquetas**. El objetivo de UX es una
navegación **maestro-detalle** clara, con selección de campaña sin fricción, listas legibles con
leyenda, y espacios definidos que separen "elegir" de "leer el detalle".

## 2. Estado actual (qué existe y qué fricciona)
Base ya construida (a conservar): `ResultadosPage` con selector de campaña + filtro de nivel de madurez
(I-17), tres paneles en `three-column` (Conversaciones / Respuestas / Markdown) como `compact-list`,
panel de evaluación y panel de Markdown con **Regenerar** (solo admin) y **Descargar .md**. Mapea
`usuarioId → nombre (área)` para no mostrar ids técnicos. Badges de estado (`evaluada`/`sin evaluar`,
`madura`/`incubación`) con tooltip.

Fricciones de UX detectadas (a resolver, sin romper lo anterior):
1. **Arranque con fricción.** Hay que elegir campaña y pulsar "Consultar"; si se olvida, aparece un
   error ("Ingresa campaniaId…"). No hay preselección ni memoria de la última campaña.
2. **Tres listas paralelas difíciles de correlacionar.** Conversaciones, Respuestas y Markdown viven en
   columnas separadas; el usuario no ve fácilmente que "esta respuesta → esta evaluación → este
   Markdown" son la misma unidad. Los campos crudos (`estado / estadoMaquina`) no son legibles.
3. **Detalle apilado sin ancla.** La evaluación y el Markdown aparecen abajo, sin dejar claro **qué
   fila** está seleccionada ni cómo volver a la lista; con muchas respuestas hay que hacer scroll.
4. **Sin leyenda de etiquetas.** "madura/incubación" y "evaluada/sin evaluar" solo se explican por
   tooltip; falta una leyenda visible y un resumen (conteos) destacado.
5. **Densidad y jerarquía.** La lista de respuestas muestra el texto completo en línea, lo que
   satura; falta truncado con jerarquía (nombre + badges arriba, extracto debajo) y buen espaciado.

## 3. Diseño UX (maestro-detalle + selección sin fricción + lectura fácil)
Todo lo siguiente es **presentación y flujo**; reutiliza tokens (`--ght-*`) y primitivas (`page-grid`,
`panel`, `panel-heading`, `filters-grid`, `detail-grid`, `status-badge`, `compact-list`) y respeta las
regiones vivas (P-19) y los nombres accesibles (P-18).

### 3.1 Selección de campaña sin fricción + instrucción
- **Instrucción breve** bajo el título: *"Elige una campaña para revisar sus respuestas, evaluaciones y
  documentos."*
- **Preseleccionar** la última campaña usada (recordada en memoria de sesión; sin `localStorage`,
  `01 §11`) o, en su defecto, la primera de la lista, y **cargar automáticamente**. El botón
  "Consultar" deja de ser obligatorio para el primer render; se conserva "Actualizar".
- **Deshabilitar** la consulta y mostrar guía en la región educada (P-19) en vez de un error rojo
  cuando no hay campaña, evitando el mensaje "Ingresa campaniaId".

### 3.2 Layout maestro-detalle (elegir arriba/izquierda, leer a la derecha)
Reemplazar las tres columnas paralelas por un patrón **lista maestra → panel de detalle**:
- **Barra de filtros y resumen** (arriba, ancho completo): campaña, nivel de madurez, y un **resumen
  destacado** con los conteos (total · X maduras · Y incubación) + **leyenda** de badges.
- **Lista maestra de respuestas** (columna izquierda, la unidad principal de trabajo): cada ítem con
  jerarquía clara — **nombre del participante** + **badges** (evaluada/sin evaluar, madura/incubación)
  en la línea superior y un **extracto truncado** de la respuesta debajo; seleccionar un ítem abre el
  detalle a la derecha y lo marca como activo (`aria-current`).
- **Panel de detalle** (columna derecha, ancho mayor): muestra, para la respuesta seleccionada, la
  **evaluación** (calificación, temas, retro enviada, explicación; o el aviso de *fallback* ya
  existente) y, debajo o en una sub-pestaña, el **Markdown** con "Regenerar" (admin) y "Descargar .md".
- **Conversaciones** pasan a ser un acceso secundario (contador + lista plegable o pestaña "Actividad"),
  no una tercera columna que compita con lo principal.

Boceto (desktop):

```
┌ Resultados ───────────────────────────────── [Actualizar] ┐
│ Elige una campaña para revisar respuestas, evaluaciones… │
├───────────────────────────────────────────────────────────┤
│ Campaña ▾   Madurez ▾    │ 24 respuestas · 15 maduras / 9 inc.│
│ Leyenda: ● madura  ○ incubación  ✓ evaluada  ⚠ sin evaluar   │
├──────────────── maestro ──────────┬──────── detalle ─────────┤
│ ○ Ana (RRHH)     ✓ ●              │ Evaluación de Ana (RRHH)  │
│   "Propongo un canal…"            │ Calificación  8.5         │
│ ○ Luis (Ventas)  ✓ ○   ◀ activo   │ Temas: canal, feedback    │
│   "Se podría mejorar…"            │ Retro enviada: …          │
│ ○ Marta (Ops)    ⚠                │ ── Markdown ──────────────│
│   "…"                             │ [Regenerar] [Descargar]   │
└───────────────────────────────────┴──────────────────────────┘
```

### 3.3 Lectura fácil (jerarquía, truncado, espaciado, leyenda)
- **Jerarquía por ítem:** nombre en negrita + badges arriba; extracto de la respuesta en texto
  secundario, **truncado** (p. ej. 140 caracteres con elipsis) para escanear rápido; el texto completo
  se ve en el detalle.
- **Leyenda visible** de las etiquetas (madura/incubación, evaluada/sin evaluar) junto al resumen, no
  solo en tooltip; conservar los `title`/nombres accesibles existentes.
- **Estados humanizados:** traducir campos crudos (`estado/estadoMaquina`, `evaluacionPendiente`) a
  lenguaje claro ("sin evaluar", "en incubación") — como ya se hace parcialmente — y evitar jerga en la
  UI visible.
- **Espaciado definido:** separación clara entre barra de filtros, lista maestra y detalle usando el
  espaciado de `panel`/`detail-grid` (sin inventar tokens); el detalle con aire entre "Evaluación" y
  "Markdown".

### 3.4 Estados vacíos, de carga y error con instrucción
- **Sin respuestas** para la campaña/filtro: mensaje-guía ("Esta campaña aún no tiene respuestas con
  ese filtro. Cambia el nivel de madurez o revisa que la campaña haya recibido mensajes.").
- **Nada seleccionado en el detalle:** placeholder ("Selecciona una respuesta de la izquierda para ver
  su evaluación y su documento.").
- **Carga:** skeletons en lista y detalle; **errores** en región asertiva y **confirmaciones**
  (regenerado, descarga) en región educada (P-19), sin duplicar anuncios.

### 3.5 Descarga y regenerado claros
- Botones con etiqueta explícita y, tras la acción, confirmación anunciada ("Documento regenerado",
  "Descarga iniciada"). "Regenerar" sigue oculto/inhabilitado para `visor`.

## 4. Contratos y configuración
- **Sin cambios de contrato.** No toca `03` ni `04`; usa los endpoints actuales
  (`conversaciones`, `respuestas` con `nivelMadurez`, `respuesta`, `markdown`, `markdownDetalle`,
  `regenerarMarkdown`). Los **filtros adicionales** de `11 §6` (área, empresa, tag, pregunta, categoría,
  calificación, fecha…) que **no** estén ya soportados por la API **no** se implementan aquí para no
  cambiar contrato; el refinamiento se hace **en cliente** sobre lo ya traído, y los filtros de
  servidor más ricos quedan como una iniciativa aparte (con su cambio de `04`).
- **Marca y layout por tokens existentes**; prohibido hardcodear colores o crear un sistema visual
  nuevo (`11 §5`, `01 §11`).
- **Documentar en `11 §6/§7`** el patrón maestro-detalle de Resultados al implementar (doc base, sin
  contrato).

## 5. Riesgos y mitigación
- *Regresión de accesibilidad* (P-18/P-19) → conservar nombres accesibles, `aria-current` en la
  selección, regiones vivas para error/confirmación; pruebas de lector/teclado.
- *Cambio de comportamiento inadvertido* → es reorganización visual/navegación; las llamadas y el
  filtro de madurez (I-17) no cambian; regresión de las pruebas existentes en verde.
- *Alcance de filtros* → no introducir filtros que requieran cambios de `04`; refinamiento en cliente y
  nota explícita de que los filtros de servidor son otra iniciativa.
- *Sobre-diseño* → sin librerías nuevas ni animaciones; solo tokens/primitivas (`01 §11`).

## 6. Criterios de aceptación / pruebas
- Al entrar, se **precarga** la campaña recordada/primera y se muestran resultados **sin** pulsar
  "Consultar"; sin campaña, aparece guía educada (no error rojo) (prueba de componente).
- La vista es **maestro-detalle**: seleccionar una respuesta en la lista abre su evaluación y su
  Markdown en el panel de detalle y la marca activa (`aria-current`).
- La lista muestra **jerarquía + extracto truncado + badges** y hay **leyenda + resumen de conteos**
  visibles (regresión de conteos I-17 en verde).
- Estados vacíos/carga/error muestran el mensaje correcto en la región adecuada (P-19), sin doble
  anuncio.
- "Regenerar" sigue restringido a admin; "Descargar .md" produce el archivo (regresión).
- Frontend verde: `prettier --check`, `ng test` (nuevos + regresiones) y `ng build` de producción con
  Node 24.15.0.

## 7. Degradación
Puramente de presentación: si el detalle no tiene datos (respuesta sin evaluación, o Markdown
inexistente) se muestran los avisos ya existentes. No hay estado persistente nuevo (la "última campaña"
vive en memoria de sesión). Revertir P-23 devuelve la vista de tres columnas sin afectar datos ni API.

## 8. Plan de implementación (pasos pequeños y verificables, frontend)
1. **Selección sin fricción:** preseleccionar última/primera campaña, autocargar, sustituir el error
   por guía educada; conservar "Actualizar". Prueba de arranque.
2. **Layout maestro-detalle:** convertir Respuestas en lista maestra y montar el panel de detalle
   (evaluación + Markdown) a la derecha; Conversaciones como acceso secundario. Prueba de selección y
   `aria-current`.
3. **Lectura fácil:** jerarquía por ítem, extracto truncado, leyenda + resumen de conteos, estados
   humanizados. Prueba de render/leyenda.
4. **Estados vacíos/carga/error** con microcopy en la región correcta (P-19). Prueba.
5. **Descarga/regenerado** con etiquetas y confirmación anunciada; `visor` sin "Regenerar". Regresión.
6. **Docs y verificación:** actualizar `11 §6/§7`, registrar en `AVANCES.md`/`TODO.md`; frontend en
   verde por paso (entorno local con Node 24.15.0).

> **Nota de entorno (para quien implemente):** `ng build`/`ng test` requieren Node temporal 24.15.0; la
> carpeta sincronizada por OneDrive puede bloquear esbuild — verificar en entorno local; el CD
> reconstruye `wwwroot` en Linux.
