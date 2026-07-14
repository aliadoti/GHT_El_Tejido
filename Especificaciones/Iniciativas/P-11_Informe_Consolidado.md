# P-11 — Informe consolidado de la base de conocimiento (rama deseable / post)

> **Origen:** hoja `Iniciativas`. **Tipo:** Desarrollo · **Prioridad:** Media-Alta ·
> **Ventana:** rama deseable / post-convención · **Dependencia:** I-06, I-09 · **Riesgo:** Bajo.

## 1. Alcance
Exportar los Markdown de una campaña en un **informe consolidado** — el entregable de valor de la
convención para GHT.

## 2. Diseño (borrador)
- Endpoint `GET /api/admin/campanias/{id}/informe` (guard admin) que compone un documento único a
  partir de los `ArtefactoMarkdown` de la campaña: portada (campaña, fechas, participación),
  índice por pregunta/tema/tag, y las ideas ordenadas por calificación (insumo de P-04).
- Primera entrega: **un solo `.md` consolidado** (determinista, regenerable — mismo principio de
  `09`); conversión a Word/PDF como paso posterior (herramienta externa o job aparte).
- Sin secretos ni datos personales más allá de lo acordado en P-07 (atribución según
  consentimiento; opción de informe anonimizado).

## 3. Nota de alcance
Especificar en detalle al retomar; comparte insumos con P-04 (ranking) y puede consumir las
síntesis de P-06 si existen.
