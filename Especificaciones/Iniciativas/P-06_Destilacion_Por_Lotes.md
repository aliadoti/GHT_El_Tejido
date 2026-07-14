# P-06 — Post-procesamiento / destilación colectiva por lotes (post-convención)

> **Origen:** hoja `Iniciativas` (idea de Munir). **Tipo:** Desarrollo · **Prioridad:** Media
> (post) · **Dependencia:** I-09 · **Riesgo:** Medio. NO construir antes del Hito.

## 1. Alcance
Pase LLM **por lotes** (batch, offline) sobre la base de conocimiento de una campaña para
destilar ideas más ricas entre todas las contribuciones — camino alterno/complementario al tejido
en línea (I-09): sin presión de latencia ni de ventana 24h.

## 2. Diseño (borrador)
- Job disparado por el admin (`POST /api/admin/campanias/{id}/destilar`, 202+jobId, patrón de
  envíos) que recorre las `Respuesta`s evaluadas de la campaña por lotes, llama al LLM con
  presupuesto de tokens por lote y produce artefactos de síntesis (Markdown tipo
  `campania`/`capitulo` — el enum `TipoArtefactoMarkdown` ya los soporta).
- Misma disciplina: contrato de salida validado, contenido de participantes como dato delimitado,
  fallback que no rompe el job, costo contado contra el presupuesto de campaña (P-10).

## 3. Nota de alcance
Especificar en detalle al retomar, junto con P-05 y P-11 (los tres consumen la misma base y
pueden compartir el pipeline por lotes).
