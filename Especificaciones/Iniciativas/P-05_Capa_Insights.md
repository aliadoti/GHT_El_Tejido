# P-05 — Capa de Insights: yuxtaposición de pensamientos (post-convención)

> **Origen:** hoja `Iniciativas` (roadmap thought→insight→meaning). **Tipo:** Desarrollo ·
> **Prioridad:** Baja (futuro) · **Dependencia:** I-09 (recuperación) · **Riesgo:** Alto.
> Diseñado en concepto; NO construir antes del Hito.

## 1. Alcance
Nuevo objeto **`Insight`** que emerge de la yuxtaposición de 2+ pensamientos (Respuestas) de la
base de conocimiento. Base de la plataforma de gestión del conocimiento post-convención.

## 2. Diseño (borrador conceptual)
- Dominio nuevo `Insight { id, campaniaId, respuestaRefs[2..N], sintesis, temas, estado }` en un
  contenedor nuevo o en `responses` con `type=insight` (decidir al retomar; contrato `03` nuevo →
  spec en commit aparte).
- Generación: pase LLM sobre pares/grupos candidatos que la recuperación de I-09 detecte como
  relacionados (misma infraestructura `IBaseConocimientoCampania`), con validación de esquema y
  human-in-the-loop: el insight nace `borrador` y un admin lo aprueba en el portal (no se
  auto-publica).
- El diseño de I-09/I-10 se hizo pensando en habilitar esta capa sin reescritura.

## 3. Nota de alcance
Se especifica al cerrar el Hito 1, con los aprendizajes de I-09 (calidad de la recuperación) y la
decisión de producto sobre thought→insight→meaning.
