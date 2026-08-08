# DT-QA-02 — `GET /api/admin/evaluaciones`: listar evaluaciones y detectar huérfanas

> **Origen:** hueco encontrado en diagnóstico E2E (2026-08-07): al investigar si P3 tenía una
> evaluación persistida pero **no enlazada**, no hay forma de comprobarlo desde la API.
> **Tipo:** Deuda técnica · **Prioridad:** Media-alta (habilitador de QA del freeze) ·
> **Ventana:** antes del dry-run E2E · **Riesgo:** bajo (solo lectura, aditivo).
> Cubre `REQ §27.3`; specs base `03 §3.8/§3.9`, `04 §5.8`, `09 §5`.
> **Estado:** **DONE local 2026-08-08.** Endpoint, puerto sin default, Cosmos/memoria, filtros,
> diagnóstico y pruebas verdes. Pendiente solamente la comprobación ADM-09b/09c/09d en el despliegue.

## 1. El problema concreto
`GET /api/admin/evaluaciones` devuelve **404**: la ruta no existe. Verificado en
`src/ElTejido.Api/Admin/EndpointsAdminResultados.cs` — se mapean `/respuestas`, `/respuestas/{id}`,
`/ideas`, `/ideas/{id}`, `/conversaciones`, `/markdown` y **`/evaluaciones/{id}`**, pero **no** la
colección `/evaluaciones`. Es el único recurso de `04 §5.8` con detalle y sin listado.

La consecuencia práctica: **solo se puede leer una evaluación si ya se conoce su `id`**, y hoy el `id`
solo se obtiene navegando desde una `Respuesta` o una `IdeaConsolidada`. Si una `Evaluacion` se
persistió pero **no quedó enlazada** —o quedó enlazada a un `respuestaId` que no existe— es
**invisible desde la API**: no aparece en `/respuestas`, no aparece en `/ideas`, y no hay ruta para
enumerarla. La única vía es abrir el Data Explorer de Cosmos, que no sirve para una prueba
reproducible ni para un agente de QA.

Esto importa porque el enlace evaluación↔respuesta **ya falló una vez**: `I-16` corrigió que el
Markdown tomara una evaluación vieja cuando había más de una para el mismo `respuestaId`. Ese defecto
se detectó por el Markdown, no por una consulta directa — no había forma de mirar.

### 1.1 Detrás del 404 hay un hueco en el puerto
`IRepositorioRespuestas` **no tiene** un método para listar evaluaciones. Tiene
`ObtenerEvaluacionPorIdAsync`, `ObtenerEvaluacionPorRespuestaAsync` (ambos exigen conocer un id),
`ContarEvaluacionesUsuarioAsync` y `SumarTokensCampaniaAsync` (devuelven agregados, no documentos).
Ninguno permite recorrer las evaluaciones de una campaña. Por eso el endpoint no es un simple mapeo:
hay que abrir el puerto primero.

## 2. Estado actual del build
- Endpoint de detalle: existe y funciona (`ObtenerEvaluacionAsync`, exige `campaniaId` en query).
- Endpoint de colección: **no existe**.
- Puerto: falta `ListarEvaluacionesAsync`.
- Adaptadores: `RepositorioRespuestasCosmos` y `RepositoriosMemoria` (ambos deben implementarlo).

## 3. Diseño técnico

### 3.1 Puerto
```csharp
/// <summary>
/// Lista las evaluaciones de una campaña (04 §5.8). Consulta acotada a la partición campaniaId.
/// Orden: fecha DESC (mismo criterio que ObtenerEvaluacionPorRespuestaAsync, I-16).
/// </summary>
Task<IReadOnlyCollection<DominioEvaluacion>> ListarEvaluacionesAsync(
    string campaniaId,
    CancellationToken cancellationToken);
```
- **Sin implementación por defecto.** A diferencia de los métodos I-19/P-26 del puerto, aquí un
  `=> Task.FromResult(vacío)` sería peligroso: un adaptador que no lo implemente reportaría "no hay
  evaluaciones huérfanas" en vez de fallar, y este endpoint existe precisamente para no confiar en
  esa clase de silencio. Que rompa la compilación es lo correcto.
- `RepositorioRespuestasCosmos`: `SELECT * FROM c WHERE c.type = "Evaluacion" ORDER BY c.fecha DESC`
  con `PartitionKey = campaniaId`. Una sola partición, RU bajo.
- `RepositoriosMemoria`: filtrar y ordenar en memoria, mismo orden.

### 3.2 Endpoint
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/admin/evaluaciones` | Lista/filtra evaluaciones de una campaña. Rol `admin` o `visor`, como el resto de `§5.8`. |

**Query params**
- `campaniaId` (**requerido**, como todos los de `§5.8`; ausente → `400`).
- Opcionales, aplicados en memoria igual que el resto de la sección:
  `usuarioId`, `preguntaId`, `respuestaId`, `ideaId`,
  `recomendacion` ∈ `cerrar|repreguntar`,
  `anomaliaSeguridad` ∈ `true|false`,
  `enlace` ∈ `enlazada|huerfana|sin_version_idea` (§3.4),
  `desde`/`hasta` (ISO UTC, sobre `fecha`).
- `page`/`pageSize` con el helper `Paginar` existente (default 25, tope 100).

**Orden:** `fecha` DESC. Es el mismo criterio que ya usa `ObtenerEvaluacionPorRespuestaAsync` (I-16,
`09 §5`), y aquí importa: al diagnosticar un enlace roto lo primero que se busca es la última
evaluación escrita.

### 3.3 DTO de lista
Resumen, **no** el documento completo: el detalle ya existe en `/evaluaciones/{id}`. Reutiliza los
nombres del mapeo actual (`MapearEvaluacion`) para no introducir un vocabulario nuevo.

```json
{
  "items": [
    {
      "id": "eval_...",
      "campaniaId": "c_2026conv",
      "respuestaId": "resp_...",
      "ideaId": "idea_resp_...",
      "versionIdeaId": "idea_resp_..._v2",
      "origenTextoEvaluado": "ideaConsolidada",
      "usuarioId": "u_8f3c...",
      "preguntaId": "p_ingresos",
      "calificacionTotal": 4.1,
      "recomendacion": "repreguntar",
      "anomaliaSeguridad": false,
      "fecha": "2026-06-11T14:05:10Z",
      "enlace": "enlazada",
      "motivoDesenlace": null
    }
  ],
  "page": 1, "pageSize": 25, "total": 1
}
```
- **No incluye** `explicacion`, `retroalimentacionEnviada`, `parafraseoDevuelto`,
  `repreguntaSugerida`, `calificacionPorCriterio` ni los snapshots: son texto largo y algunos
  contienen el aporte del participante. Para eso está el detalle. La lista debe poder pegarse en un
  reporte de QA sin filtrar PII a mano.
- `enlace` y `motivoDesenlace` son **derivados**, no persistidos (§3.4).

### 3.4 Diagnóstico de enlace — el corazón de esta deuda
Por cada evaluación se resuelve un estado de enlace comparando contra las `Respuesta` e
`IdeaConsolidada` de la misma campaña (ya cargables con `ListarRespuestasAsync` y
`ListarIdeasConsolidadasAsync`, ambas en la misma partición):

| `enlace` | Condición | `motivoDesenlace` |
|---|---|---|
| `enlazada` | Existe la `Respuesta` de `respuestaId` **y** esa respuesta resuelve a esta evaluación como la vigente | `null` |
| `huerfana` | `respuestaId` vacío, o apunta a una `Respuesta` que **no existe** en la campaña | `respuesta_inexistente` \| `respuesta_id_vacio` |
| `superada` | La `Respuesta` existe pero su evaluación vigente es **otra más reciente** | `evaluacion_mas_reciente_existe` |
| `sin_version_idea` | Tiene `ideaId` pero **no** `versionIdeaId` | `sin_version_idea` |

- La fila `superada` no es un error: I-16 la contempla explícitamente (campañas reutilizadas o datos
  legacy con más de una evaluación por respuesta). Se distingue de `huerfana` justamente para no
  disparar una falsa alarma en el dry-run.
- `sin_version_idea` importa porque, según `03 §3.9`, **una evaluación sin `versionIdeaId` no puede
  promover una `IdeaConsolidada` a madura**. Es la causa típica de "la idea no salió como madura y no
  se entiende por qué".
- El filtro `enlace=huerfana` responde de forma directa la pregunta que originó esta deuda: *"¿se
  persistió una evaluación para P3 sin quedar enlazada?"*.

**Coste:** el diagnóstico obliga a leer respuestas e ideas además de evaluaciones. Son tres consultas
de una sola partición sobre volúmenes de MVP (decenas a cientos de documentos), así que es aceptable.
Si molestara, el parámetro `enlace` puede omitirse y el campo devolverse como `no_evaluado`, pero
**no** se recomienda: sin el diagnóstico este endpoint no resuelve el problema que lo motiva.

### 3.5 Contadores de cabecera
La respuesta agrega un bloque `resumen` para no obligar a paginar el listado completo solo para saber
si hay algo roto:
```json
"resumen": { "total": 128, "enlazadas": 124, "huerfanas": 1, "superadas": 2, "sinVersionIdea": 1 }
```
Se calcula sobre el conjunto **filtrado antes de paginar**.

## 4. Contratos y configuración
- `04 §5.8`: agregar la fila `GET /api/admin/evaluaciones` con sus filtros y el DTO de lista —
  **commit aparte**. Cambio **aditivo**: no toca `/evaluaciones/{id}` ni ningún otro endpoint.
- `03`: **sin cambios**. `enlace`/`motivoDesenlace` son derivados en tiempo de consulta y **no** se
  persisten; agregarlos al documento crearía un campo que puede quedar desactualizado.
- `09 §5`: referenciar que el criterio de "evaluación vigente" del listado es el mismo que usa el
  compilador de Markdown (I-16, `fecha DESC`). Si los dos criterios divergen, el endpoint mentiría.
- Sin configuración ni flags nuevos. Sin dependencias nuevas.

## 5. Seguridad y PII
- Mismo filtro de autorización que el resto de `§5.8` (`AutorizacionAdminEndpointFilter`): `admin` o
  `visor`; nunca anónimo.
- El DTO de lista **no expone texto del participante** (§3.3). El detalle sigue siendo el único punto
  donde aparece, y ya está sujeto a los mismos roles.
- Sin PII en logs: el endpoint es de lectura y no registra `LogSeguridad` (no es una acción
  administrativa que mute estado).

## 6. Riesgos y mitigación
- *Falsa alarma de "huérfana" cuando en realidad es una evaluación superada* → son estados distintos
  y separados a propósito (§3.4).
- *El listado se convierte en una vía cómoda de exfiltrar aportes* → el DTO de lista excluye todo el
  texto libre; quien quiera el contenido debe pedir el detalle uno por uno.
- *Divergencia con el criterio de "vigente" de I-16* → una prueba cruzada lo fija (§7).
- *Adaptador que no implemente el método y reporte cero huérfanas* → el puerto **no** lleva
  implementación por defecto (§3.1).

## 7. Criterios de aceptación / pruebas
- Integration: `GET /api/admin/evaluaciones?campaniaId=X` con sesión admin → `200` y las evaluaciones
  de la campaña, en `fecha` DESC. **Hoy este caso devuelve 404** — es la prueba que fija la deuda.
- Integration: sin `campaniaId` → `400`; sin sesión → `401`; con rol `visor` → `200`.
- Unit: evaluación cuyo `respuestaId` no existe en la campaña → `enlace=huerfana`,
  `motivoDesenlace=respuesta_inexistente`, y aparece con el filtro `enlace=huerfana`.
- Unit: evaluación con `respuestaId` vacío → `huerfana(respuesta_id_vacio)`.
- Unit: dos evaluaciones para la misma respuesta → la más reciente `enlazada`, la anterior
  `superada`; **ninguna** marcada como `huerfana`. Regresión directa del defecto de I-16.
- Unit: evaluación con `ideaId` y sin `versionIdeaId` → `sin_version_idea`, y se comprueba que esa
  idea **no** figura como madura.
- Unit: el bloque `resumen` cuadra con el conteo del listado filtrado sin paginar.
- Unit: el DTO de lista **no** contiene `explicacion`, `retroalimentacionEnviada`,
  `parafraseoDevuelto`, `repreguntaSugerida` ni `calificacionPorCriterio` (prueba de no-fuga de PII).
- Unit: el criterio de "vigente" del listado coincide con
  `ObtenerEvaluacionPorRespuestaAsync` para el mismo conjunto de datos (prueba cruzada contra I-16).
- Unit: `RepositoriosMemoria` y el adaptador Cosmos devuelven el mismo orden para el mismo conjunto.
- Filtros `usuarioId`/`preguntaId`/`recomendacion`/`desde`/`hasta` y paginación con el helper existente.

## 8. Orden de implementación
1. `ListarEvaluacionesAsync` en `IRepositorioRespuestas` (sin default) + los dos adaptadores.
2. Endpoint `/evaluaciones` con filtros y paginación, DTO de lista sin texto libre.
3. Diagnóstico de enlace + bloque `resumen`.
4. `04 §5.8` y la referencia en `09 §5`.
5. Caso de QA en `QAS/02` y fila en la matriz (`01`).

## 9. Fuera de alcance
- **No** se corrige ninguna evaluación huérfana que aparezca: este endpoint **diagnostica**, no repara.
  Si el dry-run encuentra huérfanas, eso abre un hallazgo aparte.
- **No** se agrega UI en el portal. Es una herramienta de API para QA y soporte; si Resultados la
  necesita después, se decide entonces.
- **No** se toca el criterio de evaluación vigente de I-16: el listado lo **refleja**, no lo redefine.
