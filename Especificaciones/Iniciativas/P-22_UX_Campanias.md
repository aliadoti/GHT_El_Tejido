# P-22 — UX de la pantalla de Campañas (navegación guiada, formularios claros, espacios definidos)

> **Origen:** solicitud del usuario (2026-07-25) como especialista en UX: mejorar la interacción del
> administrador en **Campañas** — instrucciones claras, navegación fácil entre los menús de la
> campaña, formularios bien maquetados y espacios bien definidos.
> **Tipo:** Desarrollo **frontend-only** (Angular 22, portal) · **Prioridad:** Media · **Ventana:** a
> coordinar (rama de mejoras de portal; fuera de la ruta crítica del Hito).
> **Dependencia:** construye sobre **P-16** (paneles standalone de campañas), **P-20** (pestañas
> accesibles del detalle) y **P-18/P-19** (nombres accesibles y regiones vivas de estado). **No
> depende** de insumos externos. · **Riesgo:** Bajo — **no cambia contratos `03`/`04`**, ni rutas, ni
> permisos, ni el comportamiento de las llamadas existentes; solo reorganiza layout, copia
> (microcopy) y flujo de navegación reutilizando los tokens de marca y las primitivas de layout ya
> definidas. Cubre `REQ §27/§32/§33.1.7-9`, `ARQ §3`; spec base `11 §6/§7`.
> **Estado:** **TODO — especificación lista, implementación pendiente.** Sin código.

## 1. Qué pide / por qué
La pantalla de Campañas concentra el ciclo de vida completo (crear → configurar → mensajes iniciales →
preguntas → participantes → activar → enviar), pero hoy la disposición no **guía** al administrador por
ese orden ni deja claro "qué sigue". El objetivo de UX es que un administrador —incluso con poca
familiaridad técnica— entienda de un vistazo **dónde está, qué puede hacer y cuál es el siguiente
paso**, con formularios legibles (campos agrupados, ayuda breve, buen espaciado) y una navegación fluida
entre los sub-menús de la campaña y hacia Envíos/Resultados.

## 2. Estado actual (qué existe y qué fricciona)
Base ya construida (a conservar): la página es un contenedor (`CampaniasPage`) con paneles standalone
(`CampaniasListaPanel`, `CampaniaCreacionPanel`, `CampaniaDetallePanel` con pestañas ARIA
`config|mensajes|preguntas|participantes` y sus paneles). Marca por tokens (`--ght-*`) y primitivas de
layout (`page-grid`, `two-column`, `panel`, `panel-heading`, `form-grid`, `detail-grid`, `actions-row`,
`status-badge`, `tab-*`). Confirmaciones fuertes para reinicios de datos.

Fricciones de UX detectadas (a resolver, sin romper lo anterior):
1. **Crear compite con explorar.** El formulario de creación (`CampaniaCreacionPanel`) está siempre
   visible al lado de la lista (`two-column`), saturando la vista y restando foco a la selección.
2. **Falta el hilo del ciclo de vida.** Las pestañas del detalle no comunican el **orden recomendado**
   ni el estado de completitud ("¿ya tiene mensaje inicial?, ¿ya tiene preguntas?, ¿ya hay
   participantes?"). El admin no sabe qué le falta para poder activar/enviar.
3. **Formulario de configuración denso.** Config mezcla, en un mismo bloque, campos de naturaleza
   distinta (rúbrica/prompt/LLM de **evaluación**; umbral/inactividad/paráfrasis/segmentación de
   **conversación**; presupuesto de tokens/límites de **seguridad y costo**) sin agrupación visual ni
   textos de ayuda; los valores numéricos (umbral 0–1, minutos, tokens) no explican su significado.
4. **Navegación a Envíos poco clara.** Se llega a Envíos por un ítem del menú lateral con ruta
   placeholder (`/campanias/_/envios`); desde la campaña seleccionada no hay un enlace contextual
   directo ("Ver envíos de esta campaña").
5. **Estados vacíos sin guía.** Sin campaña seleccionada, o con listas vacías (sin preguntas, sin
   participantes), no hay un mensaje que diga qué hacer a continuación.
6. **Acciones destructivas** (reinicios) usan diálogos del navegador; funcionan pero rompen el estilo
   y no anticipan consecuencias con claridad visual.

## 3. Diseño UX (navegación guiada + layout + microcopy)
Todo lo siguiente es **presentación y flujo**; reutiliza los tokens y primitivas existentes y respeta
el patrón ARIA de pestañas (P-20) y las regiones vivas (P-19).

### 3.1 Estructura de la pantalla (dos modos claros: *explorar* y *trabajar una campaña*)
- **Encabezado con instrucción breve.** Bajo el título "Campañas", una línea guía: *"Selecciona una
  campaña para configurarla, o crea una nueva."* (texto secundario `--ght-texto-secundario`).
- **Lista a ancho completo + "Nueva campaña" como acción, no como formulario permanente.** El
  formulario de creación deja de ocupar media pantalla: se abre desde un botón **"+ Nueva campaña"**
  en el encabezado de la lista, mostrando el `CampaniaCreacionPanel` en un contenedor enfocado
  (panel expandible o sección destacada). Al guardar, se cierra y selecciona la nueva campaña. Esto
  libera espacio y da foco a explorar/seleccionar.
- **Detalle de la campaña como área de trabajo.** Al seleccionar, el detalle ocupa el ancho con: (a)
  **cabecera de campaña** (nombre, estado con `status-badge`, y acciones primarias: cambiar estado,
  enlace "Ver envíos"); (b) **barra de pasos/pestañas**; (c) el panel del paso activo.

Boceto (desktop):

```
┌ Campañas ─────────────────────────────────── [Actualizar] ┐
│ Selecciona una campaña para configurarla, o crea una nueva.│
├────────────────────────────────────────────────────────────┤
│ Lista de campañas            [ buscar… ] [estado ▾] [+ Nueva]│
│  ○ Convención 2026     activa     12 participantes           │
│  ○ Piloto RRHH         borrador    0 participantes           │
├──────────── (al seleccionar una) ─────────────────────────── ┤
│ Convención 2026   ● activa      [Cambiar estado] [Ver envíos]│
│ ┌ Pasos ───────────────────────────────────────────────────┐│
│ │ 1·Configuración ✓  2·Mensajes ✓  3·Preguntas ⚠  4·Particip.││
│ └──────────────────────────────────────────────────────────┘│
│ [ panel del paso activo, formulario agrupado y espaciado ]   │
└──────────────────────────────────────────────────────────────┘
```

### 3.2 Navegación guiada por ciclo de vida
- **Pestañas numeradas con estado de completitud.** Reetiquetar las pestañas como pasos ordenados:
  **1 · Configuración · 2 · Mensajes · 3 · Preguntas · 4 · Participantes**. Junto a cada una, un
  indicador discreto de completitud derivado de datos ya disponibles en el objeto campaña
  (✓ completo / ⚠ pendiente): Mensajes = tiene ≥1 mensaje inicial activo; Preguntas = tiene ≥1
  pregunta activa; Participantes = tiene ≥1 asociado. **No** cambia el patrón ARIA de P-20 (siguen
  siendo `tab`/`tabpanel`); el indicador es texto/símbolo con nombre accesible ("Preguntas,
  pendiente").
- **"Qué sigue" contextual.** Al pie del panel activo, una línea de ayuda que sugiere el siguiente
  paso ("Ya configuraste la evaluación. Sigue con el mensaje inicial en el paso 2.").
- **Enlace contextual a Envíos.** En la cabecera de la campaña, botón **"Ver envíos"** que navega a
  `/campanias/:id/envios` con el id real seleccionado (elimina la fricción del placeholder `_`).
- **Regla de activación clara.** Si la campaña no cumple los mínimos para activarse/enviar (sin
  pregunta o sin participantes), el control de activar muestra el motivo en la región educada (P-19)
  en lugar de fallar sin explicación.

### 3.3 Formularios bien maquetados (agrupación, ayuda y espaciado)
- **Fieldsets con leyenda** en el paso Configuración, separando por intención con encabezados y aire
  entre grupos:
  - **Evaluación:** rúbrica, prompt de evaluar, configuración LLM.
  - **Conversación:** umbral de cierre anticipado, minutos de inactividad, paráfrasis, segmentación de
    ideas.
  - **Seguridad y costo:** presupuesto de tokens de la campaña (y, si se exponen, límites por usuario).
- **Texto de ayuda por campo** (`aria-describedby`, sin duplicar anuncios — P-19): p. ej. umbral →
  *"0 = desactivado. Valor entre 0 y 1: fracción de la rúbrica para cerrar antes (ej. 0.6)."*;
  inactividad → *"Minutos sin respuesta antes de cerrar el hilo. Vacío = usar el valor global."*;
  presupuesto de tokens → *"0 = sin límite."*
- **Rejilla de dos columnas** para campos cortos (`form-grid`), campos largos a ancho completo;
  **espaciado vertical consistente** entre grupos (usar el espaciado ya definido en `panel`/`form-grid`,
  sin inventar valores nuevos). Cada control con su `label` visible asociado (P-18).
- **Acciones del formulario ancladas y claras:** una `actions-row` al final con el botón primario
  ("Guardar cambios") y, si aplica, secundario ("Descartar"); estados de guardado anunciados (P-19).
- **Mismo patrón** para los formularios de Mensajes, Preguntas y creación: agrupar, dar ayuda breve,
  respetar el espaciado y mostrar validación inline.

### 3.4 Estados vacíos y de carga con instrucción
- **Sin campaña seleccionada:** tarjeta guía ("Selecciona una campaña de la lista para ver y editar su
  configuración, mensajes, preguntas y participantes.").
- **Listas vacías dentro de un paso:** mensaje + acción ("Esta campaña aún no tiene preguntas. Agrega
  la primera para poder evaluar respuestas.").
- **Skeletons/estados de carga** en la lista y el detalle (P-19 los anuncia).

### 3.5 Acciones destructivas presentadas con claridad
- Mantener la confirmación fuerte por nombre para el reinicio masivo, pero **presentar la consecuencia
  antes** (qué se borra y qué se conserva) en un bloque de advertencia con el color de acento sobrio
  (`--ght-rojo`) y el resultado anunciado por región (P-19). (Sustituir `window.confirm/prompt` por un
  diálogo del portal es deseable pero **opcional**; puede quedar como sub-paso.)

## 4. Contratos y configuración
- **Sin cambios de contrato.** No toca `03` (modelo) ni `04` (API); consume exactamente los endpoints
  y DTOs actuales. No cambia rutas ni guards ni permisos (`visor` sigue sin botones de mutación).
- **Marca y layout por tokens existentes** (`--ght-*`, `styles.scss`); **prohibido** hardcodear colores
  o inventar un sistema visual nuevo (`11 §5`, `01 §11`).
- **Documentar en `11 §6/§7`** (frontend) el flujo guiado y las reglas de maquetación de Campañas al
  implementar (actualización del doc base, sin contrato).

## 5. Riesgos y mitigación
- *Regresión de accesibilidad* (P-18/P-19/P-20) → mantener el patrón ARIA de pestañas y las regiones
  vivas; los indicadores de completitud llevan nombre accesible; pruebas de teclado/lector conservadas.
- *Cambio de comportamiento inadvertido* → es reorganización visual/navegación: las llamadas, payloads
  y validaciones no cambian; regresión de las pruebas de panel existentes (P-16) en verde.
- *Sobre-diseño* → sin librerías nuevas ni animaciones; solo tokens y primitivas ya presentes (`01 §11`,
  anti-patrones).

## 6. Criterios de aceptación / pruebas
- El formulario de creación **no** ocupa la vista por defecto; se abre desde "+ Nueva campaña" y, al
  guardar, selecciona la nueva campaña (prueba de componente).
- Las pestañas muestran **estado de completitud** correcto (✓/⚠) según mensajes/preguntas/participantes
  del objeto campaña, con nombre accesible; el recorrido por teclado (Flecha/Inicio/Fin) sigue verde
  (regresión P-20).
- "Ver envíos" navega a `/campanias/:id/envios` con el **id real** seleccionado.
- El paso Configuración presenta **fieldsets con leyenda** (Evaluación / Conversación / Seguridad y
  costo) y **texto de ayuda** asociado por `aria-describedby` sin doble anuncio (regresión P-19).
- Estados vacíos muestran el mensaje-guía correspondiente (sin campaña, sin preguntas, sin
  participantes).
- Frontend verde: `prettier --check`, `ng test` (casos nuevos + regresiones de paneles P-16/P-20) y
  `ng build` de producción, con Node 24.15.0 (`AVANCES.md` documenta el bloqueo esbuild/OneDrive: el CD
  reconstruye en Linux).

## 7. Degradación
Es puramente de presentación: si algo del flujo guiado no aplica (p. ej. datos incompletos para calcular
completitud), se muestra ⚠ sin bloquear. No hay estado persistente nuevo; revertir P-22 devuelve el
layout anterior sin afectar datos ni API.

## 8. Plan de implementación (pasos pequeños y verificables, frontend)
1. **Encabezado + instrucción + "Nueva campaña" como acción:** mover el formulario de creación a un
   contenedor enfocado; lista a ancho completo. Pruebas de apertura/cierre y selección tras crear.
2. **Cabecera de campaña + "Ver envíos" contextual:** nombre/estado/acciones; enlace a envíos con id
   real. Prueba de navegación.
3. **Pestañas como pasos numerados + completitud:** indicadores ✓/⚠ con nombre accesible, conservando
   ARIA de P-20. Prueba de estado y teclado.
4. **Config en fieldsets + ayuda + espaciado:** agrupar Evaluación/Conversación/Seguridad-costo, textos
   de ayuda `aria-describedby`, `actions-row`. Regresión P-19.
5. **Estados vacíos/carga con microcopy** en lista y pasos. Prueba de render.
6. **(Opcional) Diálogo del portal** para reinicios en vez de `window.confirm/prompt`, con bloque de
   consecuencias.
7. **Docs y verificación:** actualizar `11 §6/§7`, registrar en `AVANCES.md`/`TODO.md`; frontend en
   verde por paso (entorno local con Node 24.15.0).

> **Nota de entorno (para quien implemente):** `ng build`/`ng test` requieren Node temporal 24.15.0; la
> carpeta sincronizada por OneDrive puede bloquear esbuild — verificar en entorno local; el CD
> reconstruye `wwwroot` en Linux.
