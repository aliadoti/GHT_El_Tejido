# P-35 — Nombre de saludo de participantes

## 1. Necesidad

La fuente de participantes entrega el nombre completo en el orden `APELLIDOS NOMBRES`, por ejemplo
`ARENAS CHAVES JUAN PABLO`. Ese valor debe conservarse como identidad administrativa, pero el chat
debe saludar de forma cercana: `Juan Pablo`.

## 2. Decisión de datos

- `Usuario.nombre` conserva el nombre completo recibido. Sigue siendo la fuente para portal,
  búsquedas, resultados, exportaciones, Markdown, auditoría y comparación de titulares de I-08.
- Se agrega `Usuario.nombreSaludo` (`string`, requerido en la forma canónica de Cosmos). Es el único
  nombre que resuelve la variable conversacional `{{nombre}}`.
- Un documento histórico sin `nombreSaludo` continúa siendo legible: al materializarlo, el servidor
  calcula el valor con la misma regla usada en altas nuevas. La siguiente escritura del usuario lo
  persiste. El llenado general de documentos existentes se ejecuta como backfill explícito,
  idempotente y con conteo de resultado; una lectura nunca escribe en silencio.
- `nombreSaludo` es editable en el alta/edición individual y en el portal. Una corrección manual se
  conserva aunque una carga masiva posterior actualice `nombre`.

## 3. Regla automática

1. Colapsar espacios y separar el nombre completo en palabras.
2. Cuando existan tres o más palabras, asumir la convención vigente `APELLIDO1 APELLIDO2 NOMBRES` y
   tomar desde la tercera palabra.
3. Cuando existan una o dos palabras, usar el valor completo.
4. Aplicar capitalización legible con cultura española, sin perder tildes.

| `nombre` | `nombreSaludo` calculado |
|---|---|
| `ARENAS CHAVES JUAN PABLO` | `Juan Pablo` |
| `PEREZ GOMEZ ANA MARIA` | `Ana Maria` |
| `ANA` | `Ana` |

La regla es determinista y no intenta adivinar apellidos compuestos. Casos como
`DE LA CRUZ PEREZ ANA` pueden requerir corrección manual; no se usa un LLM ni un catálogo externo.

## 4. Contratos

- Los DTO de usuario agregan `nombreSaludo` de forma aditiva.
- `POST /api/admin/usuarios`, `PUT /api/admin/usuarios/{id}` y la reasignación manual aceptan
  `nombreSaludo` opcional. Ausente en un alta significa “calcular”; ausente en una edición significa
  “conservar”; una cadena vacía en edición significa “recalcular”.
- `POST /api/admin/usuarios/nombres-saludo/completar` completa únicamente documentos históricos sin
  la propiedad y devuelve el número actualizado.
- `GET /api/admin/usuarios/nombres-saludo/pendientes` permite previsualizar el conteo sin exponer PII.
- El portal muestra y permite editar ambos campos con etiquetas inequívocas: **Nombre completo** y
  **Nombre para saludo**.
- `RenderizadorMensaje.ConstruirVariables` resuelve `{{nombre}}` con `usuario.NombreSaludo`; las
  demás variables no cambian.

## 5. Persistencia y backfill

- `UsuarioCosmosDocument` agrega la propiedad JSON `nombreSaludo`.
- `FromDomain` siempre la escribe.
- `ToDomain` acepta temporalmente el campo ausente y calcula el fallback.
- El backfill selecciona únicamente documentos `type = "Usuario"` sin `nombreSaludo`, calcula el
  valor y actualiza esa propiedad sin alterar `nombre`, ids, estado, número, tags ni fechas de
  creación. Puede repetirse sin cambiar registros ya llenados o corregidos manualmente.
- No se ejecuta una mutación contra Cosmos remoto como parte del build ni del despliegue. La corrida
  requiere ambiente identificado, respaldo verificado, previsualización y autorización operativa.

## 6. Fuera de alcance y deuda explícita

- Agregar `Nombre para saludo` a la plantilla CSV/XLSX y al lector de carga masiva.
- Inferencia avanzada de partículas, apellidos compuestos o convenciones distintas por empresa.
- Cambiar `nombre` en búsquedas, resultados, exportaciones, Markdown, JWT o comparación de titular.

Hasta que se amplíe I-08, la carga masiva crea `nombreSaludo` con la regla automática y conserva el
valor existente al actualizar. Las excepciones se corrigen desde la edición individual del portal.

## 7. Criterios de aceptación

1. `ARENAS CHAVES JUAN PABLO` se persiste como `nombre` y `Juan Pablo` como `nombreSaludo`.
2. El mensaje `Hola {{nombre}}` se renderiza `Hola Juan Pablo`.
3. Portal, resultados y documentos siguen mostrando el nombre completo donde ya lo hacían.
4. Una edición manual de `nombreSaludo` sobrevive a una carga masiva posterior.
5. Un documento Cosmos histórico sin el campo se lee y saluda sin error.
6. El DTO y el formulario administrativo permiten corregir el valor.
7. El backfill no sobrescribe valores existentes.

## 8. Validación

- Pruebas unitarias del cálculo, dominio, renderizador, gestión de usuarios, carga masiva y mapping
  Cosmos histórico/canónico.
- Pruebas de integración del contrato administrativo y pruebas Angular del alta y la edición.
- Gate secuencial: build Release con warnings como error, pruebas backend, formato, build/pruebas del
  portal, Prettier y `git diff --check`.
