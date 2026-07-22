# P-14 — Vista de solo lectura de rúbricas y prompts en el portal

> **Origen:** solicitud del usuario (Aliado TI, 2026-07-21). **Estado:** **DONE local 2026-07-22** · **Tipo:** Desarrollo frontend (Angular) ·
> **Prioridad:** Alta — **EJECUTAR DE PRIMERA** (pequeña, sin riesgo, desbloquea que GHT/Felipe
> revisen la rúbrica y los prompts sin poder alterarlos) · **Ventana:** Sprint 1b · **Dependencia:** —
> · **Riesgo:** Muy bajo (frontend-only, sin cambio de contratos). Cubre REQ §17/§18/§33.1, ARQ §6;
> specs base `base/04_Contrato_API_REST.md §5.5-5.6`, `base/07_Backend_Campanas_y_Configuracion.md §3-4`,
> `base/11_Frontend_Portal_Angular.md`.

## 1. Qué pide / por qué
Hoy en el portal, para **ver** el contenido completo de una rúbrica o un prompt hay que entrar a
**modo edición**: las pantallas solo ofrecen acciones que **mutan** (crear/editar borrador, crear nueva
versión, archivar/aprobar/cambiar estado). Falta poder **solo leer** una rúbrica o un prompt (su
contenido, versión y estado) **sin riesgo de modificarlo** — útil para que GHT/Felipe y visores
revisen el material, y para consultar qué está activo antes de calibrar.

## 2. Estado actual del build (verificado 2026-07-21)
- `features/rubricas/rubricas.page.ts` y `features/prompts/prompts.page.ts` listan en una tabla las
  **versiones activas** (dedup por familia; columnas versión/estado/…). Acciones por fila:
  **Editar** (si `borrador`) / **Nueva versión** (si `activa`/aprobado), **Archivar** (rúbrica),
  **Aprobar** / cambiar estado (prompt). No hay acción de "Ver".
- El contenido completo (`contenidoMarkdown` de rúbrica; `contenido` de prompt) **solo se muestra
  dentro del formulario de edición** (textarea editable), al pulsar Editar/Nueva versión.
- El **dato ya está disponible**: `AdminApiService.rubricas()` / `.prompts()` (GET
  `/api/admin/rubricas|prompts`) ya devuelven el objeto completo con su contenido, escala/criterios
  (rúbrica), tipo y aprobación (prompt), versión, estado y fechas (modelos `Rubrica`/`PromptConfig`
  en `core/api-models.ts`). **No hace falta backend nuevo ni un GET adicional** para la versión listada.

## 3. Diseño técnico (frontend-only)
1. **Acción "Ver" por fila** (rúbricas y prompts): abre un **panel/modal de solo lectura** que muestra,
   sin ningún control editable:
   - Rúbrica: nombre, versión, estado, escala (`min`–`max`), tabla de criterios (nombre/peso) y el
     **`contenidoMarkdown`** completo (renderizado como Markdown de solo lectura, o en un bloque
     `<pre>`/textarea `readonly` si no hay renderer disponible; preferible render legible).
   - Prompt: nombre, tipo, versión, estado, aprobado por / fecha de aprobación y el **`contenido`**
     completo (solo lectura).
   - El panel **no expone** botones de guardar/editar/estado; solo "Cerrar". Reutiliza el objeto que
     ya está en el `signal` de la tabla (sin llamada extra); si se prefiere, puede refrescar con el GET
     por id existente (`/api/admin/rubricas|prompts/{id}`) — opcional.
2. **Separar lectura de edición:** el botón actual "Editar/Nueva versión" se mantiene; se **añade**
   "Ver" (no se elimina ni cambia ninguna mutación existente). Un **visor** (rol `visor`, no admin)
   debe poder usar "Ver" aunque no vea las acciones de mutación (respetar `auth.isAdmin()` en los
   botones que mutan; "Ver" disponible para admin y visor).
3. **Markdown seguro:** si se renderiza el Markdown, usar un render que **escape/sanitice** (no
   `innerHTML` crudo) para no introducir XSS desde el contenido almacenado. Alternativa segura y
   simple: mostrar el texto tal cual en `readonly` monoespaciado.

## 4. Contratos y configuración
**Ninguno.** Sin cambios en `03`/`04`/`08`. Solo `src/ElTejido.Web` (las dos páginas, y si aplica un
componente de "visor de contenido" reutilizable). No toca el backend.

## 5. Riesgos y mitigación
- *Que "Ver" permita mutar por error* → el panel de lectura no instancia ningún form/binding de
  escritura; se prueba que no hay llamadas `PUT/POST/PATCH` desde esa vista.
- *XSS desde `contenidoMarkdown`/`contenido`* → render sanitizado o `readonly` sin `innerHTML`.
- *Regresión de las acciones existentes* → no se modifica ninguna acción actual; solo se añade "Ver".

## 6. Criterios de aceptación / pruebas
- Existe un botón/acción **"Ver"** en cada fila de rúbricas y de prompts que abre un panel de **solo
  lectura** con el contenido completo, versión y estado; sin controles de edición.
- Desde ese panel **no** es posible mutar (0 llamadas `PUT/POST/PATCH`); cerrarlo vuelve a la lista.
- Las acciones actuales (Editar/Nueva versión/Archivar/Aprobar) siguen funcionando igual.
- Un usuario `visor` puede **Ver** pero no ve las acciones de mutación.
- Frontend verde: `npm run lint`, `ng test` (nuevo caso: "Ver" abre lectura, no dispara mutación),
  `ng build --configuration production`, con Node temporal `24.15.0`.

## 7. Cómo probarlo (resumen para una persona no técnica)
1. Entra al portal como administrador y abre la pantalla de **Rúbricas** (y luego **Prompts**).
2. En cualquier fila, pulsa **"Ver"**: se abre una ventana que muestra **todo el contenido** de esa
   rúbrica/prompt (su texto, versión y estado) **sin poder cambiar nada**.
3. Confirma que en esa ventana **no hay** botones de guardar/editar; solo **Cerrar**.
4. Cierra y verifica que los botones de siempre (**Editar / Nueva versión / Archivar / Aprobar**)
   siguen ahí y funcionan igual. Listo: ahora se puede **consultar** sin riesgo de modificar.

## 8. Alcance / futuro (no incluido)
Ver **versiones históricas** (no solo la vigente por familia) requeriría un endpoint de listado de
versiones; hoy el GET devuelve la última por familia. Queda como extensión posterior si GHT lo pide.
