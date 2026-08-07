# I-08 · Carga masiva de participantes

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | **Reabierto (v2)** — v1 entregado; la plantilla oficial cambió el alcance |
| **Solicitado por / Fecha** | GHT · — · **plantilla oficial recibida 2026-08-07** |
| **Estimación** | Por estimar (T&M) — v2 requiere reestimación |

## Qué se necesita
Importar participantes de forma masiva desde archivo, con las variables demográficas que GHT usará
para el análisis posterior de la convención.

## Alcance
- **Incluye (v1, entregado):** carga masiva backend + UI; reporte por fila; idempotencia.
- **Incluye (v2, 2026-08-07):** soporte de la **plantilla oficial de GHT** (9 columnas:
  `Empresa | ID Empresa | Sede | Nombre | Cargo | Email | Antigüedad en la empresa en años | Idioma | Telefono`),
  lectura de `.xlsx`, **código de usuario** consecutivo en el maestro, **reasignación de números de
  WhatsApp** entre personas conservando la trazabilidad de quien participó, y un modo de
  **actualización masiva** que completa datos sin crear registros.
- **No incluye:** el catálogo de tags lo entrega GHT; no se inventan.

## Criterios de aceptación
- [x] El admin carga participantes desde archivo con validación.
- [x] Las columnas demográficas se importan sin romper cargas previas.
- [ ] **(v2)** La plantilla oficial de GHT se carga tal cual, sin edición manual previa.
- [ ] **(v2)** Solo `Nombre` y `Telefono` son obligatorios; las filas con `Cargo`, `Email`, `Sede` o
      `Antigüedad` vacíos se cargan igual y el hueco queda visible en el reporte.
- [ ] **(v2)** Cada participante tiene un código único consecutivo que no cambia nunca.
- [ ] **(v2)** Si un número de WhatsApp pasa de una persona a otra, el sistema **pregunta** antes de
      hacerlo, y los aportes de la persona anterior siguen siendo suyos.

## Novedades y bloqueos (2026-08-07)
- **Insumo de variables demográficas (Munir): ✓ cerrado.** Llegó como plantilla completa, no como
  extensión de columnas, por lo que reemplaza el formato de v1.
- **⚠️ Bloqueo para la carga real:** en el archivo recibido (`Información asistentes convención
  gerentes 2026 V1.xlsx`, 129 filas) las columnas **`Telefono`, `Idioma` y `Empresa` vienen vacías en
  todas las filas**. **Sin teléfono no hay canal de WhatsApp**, así que ninguna fila puede cargarse.
  **Se requiere la V2 del archivo con `Telefono` diligenciado.**
- Por decisión de proyecto, **no se carga ningún dato todavía**: el sistema queda listo y la carga
  real es un paso del freeze.

## Aprobación / Referencia
Estado de desarrollo: v1 Backend+UI DONE (15 y 20-jul); **v2 especificada 2026-08-07, sin código**.
Spec detallada: `Especificaciones/Iniciativas/I-08_Carga_Masiva_Participantes.md`
Plantilla vacía para GHT: `Especificaciones/Iniciativas/plantillas/plantilla_participantes_v1.xlsx`
