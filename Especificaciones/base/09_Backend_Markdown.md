# 09 — Backend: Generación de Markdown

**Módulo:** `Application/Markdown/` (+ `Infrastructure/Blob/`).
**Implementa:** `REQ §22, §23, §26.7`; `ARQ §7`.
**Depende de:** `03` (ArtefactoMarkdown, Respuesta, Evaluacion, Campania, Usuario), `05` (lo encola el orquestador al cerrar), Blob Storage.

---

## 1. Responsabilidad

Compilar la respuesta evaluada —o, con I-19, la **idea consolidada vigente**— en un artefacto
Markdown durable, atribuido y **regenerable**, guardarlo en Blob Storage y registrar sus metadatos en
Cosmos para consulta rápida. El artefacto es **caché materializada**: la fuente de verdad son los
datos operativos (`REQ §23.3`, `ARQ §7.4`).

---

## 2. Puerto

```csharp
public interface ICompiladorMarkdown
{
    Task<ArtefactoMarkdown> CompilarAsync(SolicitudCompilacion solicitud, CancellationToken ct);
}
```
`SolicitudCompilacion`: `{ string campaniaId; TipoArtefacto tipo; string? respuestaId; string? ideaId; string? usuarioId; string? preguntaId; }`.

---

## 3. Disparo (`ARQ §7 paso 1`)
- Tras guardar la evaluación final (cierre del hilo), el orquestador **encola** un job de compilación para esa respuesta/participante (`05 §4.3`).
- El **tipo** de artefacto lo define la configuración de la campaña/pregunta (`configMarkdown.tipoArtefacto`) (`REQ §11.3.10, §22.2`). MVP: al menos `respuesta`.
- También se puede disparar manualmente vía `POST /api/admin/markdown/{id}/regenerar` (`04 §5.8`).
- I-19 actualiza un artefacto canónico por `ideaId` cada vez que cambia la versión confirmada o el
  estado final; una propuesta no confirmada puede materializarse marcada como tal al cerrar por
  inactividad/fallback, pero nunca aparece como madura.
  - **Implementado (2026-07-27):** el orquestador regenera el artefacto de la idea **al evaluar** una
    versión confirmada y **al cerrarla** (umbral, salida, techo determinista, fallback o rechazo). No se
    regenera en cada propuesta sin confirmar: esas versiones ya quedan en el historial del artefacto.
    Mientras la idea sigue abierta, `evaluacionVigenteRef` aún no está sellado y el artefacto usa la
    evaluación **de la versión vigente exacta**; si no coincide, omite la sección de evaluación en vez
    de mostrar la calificación de una versión anterior. El fallo de compilación nunca rompe el hilo
    (`REQ §22.4.6`).

---

## 4. Ensamblaje (`ARQ §7 paso 2`)
1. Cargar datos operativos. Legacy: respuesta original y **evaluación válida más reciente asociada a
   la respuesta**. I-19: `IdeaConsolidada`, versión confirmada/propuesta vigente, evaluación referida
   explícitamente por `evaluacionVigenteRef`, aportes/versiones auditables y metadatos de
   usuario/campaña/pregunta. No buscar “la última evaluación” de otro aporte como sustituto.
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
- Idea raíz: {{respuesta.ideaRaizId (si aplica)}}
- Revisión: {{respuesta.revisionIndice (si aplica; 0 = raíz)}}
- Rúbrica / Versión: {{rubricaRef}} / v{{versionRubrica}}
- Prompt / Versión: {{promptRef}} / v{{versionPrompt}}
- Calificación total: {{evaluacion.calificacionTotal}}
- Nivel de madurez: {{respuesta.nivelMadurez}}  <!-- I-17: maduro|incubacion; ausente en datos históricos = incubacion. Determinista, sin secretos, regenerable. -->
- Estado de la idea: {{idea.estadoResultado (si aplica)}}  <!-- I-19: madura|pendiente|rechazada -->
- Estado de confirmación: {{versionIdea.estadoConfirmacion (si aplica)}}
- Estado de curaduría: {{idea.estadoCuraduria (si aplica; I-19 solo escribe pendiente)}}

## Idea consolidada
{{versionIdea.texto (I-19) o respuesta.texto (legacy)}}

## Aportes originales
{{#each aportes (I-19)}}- v{{revisionIndice}}: {{texto}}{{/each}}

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
- ID de idea: {{idea.id (si aplica)}}
- Versión de idea: {{versionIdea.id (si aplica)}}
- ID de evaluación: {{evaluacion.id}}
```

Requisitos del artefacto (`REQ §22.4`): conserva autoría, pregunta y aportes originales, evaluación,
versiones de rúbrica/prompt; es regenerable; preparado para versionamiento e indexación futuros;
legible por humanos; sin secretos. Para I-06, cada idea segmentada conserva un artefacto independiente.
I-18 mantiene el linaje de aportes. I-19 compila el vigente desde `ideaId` +
`versionConfirmadaRef`/`evaluacionVigenteRef`, por lo que las revisiones dejan de aparecer como
resultados separados.

---

## 6. Persistencia (`ARQ §7 paso 3`)
- Guarda el `.md` en Blob Storage en la ruta:
  `campanias/{campaniaId}/{tipoArtefacto}/{entidadId}.md`
  (p. ej. legacy `campanias/c_2026conv/respuesta/resp_xxx.md`; I-19
  `campanias/c_2026conv/idea/idea_xxx.md`).
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
- Con I-19, existe un solo artefacto canónico por idea, incluso si tiene varios aportes/versiones; su
  estado indica madura, pendiente o rechazada y si la versión quedó sin confirmar.
- El Markdown no contiene secretos.
- `regenerar` produce el mismo artefacto desde los datos operativos (idempotente en contenido salvo cambios de datos).
- El artefacto es consultable desde el portal y descargable como `.md`.

*Fin del documento.*
