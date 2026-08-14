# REQ-054 · Consulta y cierre visible de la idea

| | |
|---|---|
| **Origen** | Retroalimentación de participante, 13-ago-2026 |
| **Prioridad** | Alta — experiencia de la convención |
| **Estado** | Aprobado para implementación inmediata |

## Qué se necesita

El participante debe poder preguntar de forma natural cómo va o cómo quedó escrita su idea. El coach
responde con la idea que vienen trabajando —o con la última si ya cerró— sin abrir un menú. Al cerrar
una idea también debe mostrar cómo quedó antes de despedirse o avanzar.

Si se consultó una idea cerrada y la persona responde con una corrección clara, el sistema retoma esa
misma idea y la actualiza sin volver a preguntar cuál.

## Alcance

- Incluye: consulta bajo demanda, última idea por contexto, visualización al cierre, afinidad temporal
  y reapertura ante corrección, textos `es/en`, controles de activación y auditoría sin contenido.
- No incluye: puntajes/rúbrica, búsqueda semántica, ideas de campañas cerradas o de otras personas,
  ni menús históricos salvo petición explícita de otra idea.

## Criterios de aceptación

- [ ] «Dime cómo va escrita mi idea» muestra la versión vigente y no se guarda como aporte.
- [ ] Sin idea activa se muestra la última idea trabajada, sin menú.
- [ ] Una corrección posterior reabre la misma idea; «gracias» no la reabre.
- [ ] Al cerrar se muestra cómo quedó, salvo rechazo explícito o cierre administrativo.
- [ ] La consulta no cambia madurez, puntajes, repreguntas ni historial.
- [ ] Nunca se expone una idea no autorizada.

## Referencia

Spec técnica: `Especificaciones/Iniciativas/P-33_Consulta_y_Cierre_Visible_de_la_Idea.md`.
