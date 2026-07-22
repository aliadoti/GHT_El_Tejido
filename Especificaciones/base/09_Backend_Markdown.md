# 09 — Backend: Generación de Markdown

**Módulo:** `Application/Markdown/` (+ `Infrastructure/Blob/`).
**Implementa:** `REQ §22, §23, §26.7`; `ARQ §7`.
**Depende de:** `03` (ArtefactoMarkdown, Respuesta, Evaluacion, Campania, Usuario), `05` (lo encola el orquestador al cerrar), Blob Storage.

---

## 1. Responsabilidad

Compilar la respuesta evaluada en un **artefacto Markdown** durable, atribuido y **regenerable**, guardarlo en Blob Storage y registrar sus metadatos en Cosmos para consulta rápida. El artefacto es **caché materializada**: la fuente de verdad son los datos operativos (`REQ §23.3`, `ARQ §7.4`).

---

## 2. Puerto

```csharp
public interface ICompiladorMarkdown
{
    Task<ArtefactoMarkdown> CompilarAsync(SolicitudCompilacion solicitud, CancellationToken ct);
}
```
`SolicitudCompilacion`: `{ string campaniaId; TipoArtefacto tipo; string? respuestaId; string? usuarioId; string? preguntaId; }`.

---

## 3. Disparo (`ARQ §7 paso 1`)
- Tras guardar la evaluación final (cierre del hilo), el orquestador **encola** un job de compilación para esa respuesta/participante (`05 §4.3`).
- El **tipo** de artefacto lo define la configuración de la campaña/pregunta (`configMarkdown.tipoArtefacto`) (`REQ §11.3.10, §22.2`). MVP: al menos `respuesta`.
- También se puede disparar manualmente vía `POST /api/admin/markdown/{id}/regenerar` (`04 §5.8`).

---

## 4. Ensamblaje (`ARQ §7 paso 2`)
1. Cargar datos operativos: respuesta original, **evaluación válida más reciente asociada a la respuesta** (ordenada por `fecha` descendente, con snapshots), metadatos de usuario/campaña/pregunta, rúbrica+versión, prompt+versión, calificación.
2. Renderizar la **plantilla Markdown estándar** (§5) de forma **determinística** desde los datos.
3. Opcional: un prompt de compilación (`tipoPrompt=compilar`, versionado) puede redactar **solo** la sección narrativa; el resto se arma siempre desde los datos (`ARQ §7 paso 2`).
4. **Regla dura:** el Markdown **NO** contiene secretos ni API keys (`REQ §22.4.9`, `ARQ §7`).

---

## 5. Plantilla estándar del artefacto (`REQ §22.3`, `ARQ Apéndice B`)

```markdown
# {{título del aporte}}

## Metadatos
- Campaña: {{campania.nombre}}
- Participante: {{usuario.nombre}}
- Área: {{usuario.area}}
- Empresa: {{usuario.empresa}}
- Fecha: {{respuesta.fecha}}
- Pregunta: {{pregunta.texto}}
- Tags: {{usuario.tags (snapshot)}}
- Idea índice: {{respuesta.ideaIndice (si aplica)}}
- Respuesta padre: {{respuesta.respuestaPadreId (si aplica)}}
- Rúbrica / Versión: {{rubricaRef}} / v{{versionRubrica}}
- Prompt / Versión: {{promptRef}} / v{{versionPrompt}}
- Calificación total: {{evaluacion.calificacionTotal}}

## Respuesta original
{{respuesta.texto}}

## Evaluación
### Calificación por criterio
| Criterio | Puntaje | Justificación |
|---|---:|---|
{{#each calificacionPorCriterio}}| {{criterio}} | {{puntaje}} | {{justificacion}} |{{/each}}

## Retroalimentación enviada
{{evaluacion.retroalimentacionEnviada}}

## Temas identificados
{{#each temas}}- {{.}}{{/each}}

## Entidades mencionadas
{{#each entidades}}- {{.}}{{/each}}

## Notas de trazabilidad
- ID de conversación: {{conversacionId}}
- ID de respuesta: {{respuesta.id}}
- ID de evaluación: {{evaluacion.id}}
```

Requisitos del artefacto (`REQ §22.4`): conserva autoría, pregunta y respuesta originales, evaluación, versiones de rúbrica/prompt; es regenerable; preparado para versionamiento e indexación futuros; legible por humanos; sin secretos. Para I-06, cada idea segmentada genera un artefacto `respuesta` independiente y estos campos opcionales permiten reconstruir qué ideas salieron del mismo mensaje original.

---

## 6. Persistencia (`ARQ §7 paso 3`)
- Guarda el `.md` en Blob Storage en la ruta:
  `campanias/{campaniaId}/{tipoArtefacto}/{entidadId}.md`
  (p. ej. `campanias/c_2026conv/respuesta/resp_xxx.md`).
- Guarda/actualiza el documento `ArtefactoMarkdown` (`03 §3.10`) con `contenidoMarkdown` embebido + `blobPath` + `version`, para que el portal consulte sin leer Blob.
- **Versiona** el artefacto (incrementa `version` al regenerar). Preparado para sincronización a Git en post-MVP (`REQ §22.4.7`, `§23.2`) — **no** implementar Git ahora.

---

## 7. Consulta y regeneración (`ARQ §7 paso 4`)
- El portal lista y muestra el Markdown (`04 §5.8`).
- **Regla de diseño:** el artefacto SIEMPRE puede regenerarse desde los datos operativos (`REQ §22.4.6`). `regenerar` recompila y sube una nueva versión; el contenido previo en Blob puede conservarse por `version` o sobreescribirse (MVP: sobreescribe la ruta canónica y aumenta `version` en Cosmos).

---

## 8. Preparación semántica (POST-MVP, no implementar) — `ARQ §7 paso 5`
Los metadatos (campaña, autor, tags, temas, entidades) y el contenido quedan estructurados para que una capa vectorial los indexe después sin reprocesar la conversación (`REQ §24.3`). Solo se **prepara** la estructura.

---

## 9. Criterios de aceptación del módulo (resumen; ver `13`)
- Al cerrar un hilo se genera un artefacto Markdown con todos los metadatos, evaluación y trazabilidad.
- El Markdown no contiene secretos.
- `regenerar` produce el mismo artefacto desde los datos operativos (idempotente en contenido salvo cambios de datos).
- El artefacto es consultable desde el portal y descargable como `.md`.

*Fin del documento.*
