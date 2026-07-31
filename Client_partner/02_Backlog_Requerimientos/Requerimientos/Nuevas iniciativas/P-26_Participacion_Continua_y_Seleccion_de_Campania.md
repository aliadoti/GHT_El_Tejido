# P-26 · Participación continua y selección de campaña

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | Estimado |
| **Solicitado por / Fecha** | GHT · — |
| **Estimación** | Por estimar (T&M) |

## Qué se necesita
Permitir participación continua por campaña: crear ideas/ciclos nuevos, seleccionar campaña y pregunta de forma determinista, conservar el aporte raíz y aplicar cupos móviles de 24h.

## Alcance
- Incluye: flag `participacionContinua` por campaña; selección determinista de campaña/pregunta; ciclos independientes y cupos móviles 24h
- No incluye: Solo campañas activas reciben aportes. `false`/ausente conserva el recorrido único actual.

## Criterios de aceptación
- [ ] Con el flag on, el participante crea ideas/ciclos nuevos en campañas activas.
- [ ] La selección de campaña/pregunta no obliga a reescribir el aporte.

## Aprobación / Referencia
Estado de desarrollo: ESPECIFICADA 2026-07-29; implementación pendiente (6 cortes)
Spec detallada: `Especificaciones/Iniciativas/P-26_Participacion_Continua_y_Seleccion_de_Campania.md`
