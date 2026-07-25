# P-17 — Uniformar errores y correlación de la API

**Estado:** DONE local — 2026-07-24 (Claude Opus 4.8)  
**Origen:** `API-001` de la auditoría técnica del 2026-07-24.  
**Dependencias:** ninguna externa. No cambia la versión pública de la API. (Se ejecutó antes que P-16 por decisión del usuario: P-16/P-18/P-19/P-20 son frontend y `ng build`/`ng test` están bloqueados en este entorno; P-17 es backend y sí se verifica aquí.)

> **Avance 2026-07-24 (Claude Opus 4.8, Backend/AppSec/SDET) — DONE local.** Camino único reutilizable
> `ResultadoError : IResult` (`src/ElTejido.Api/Errores/`) que reutiliza `EscritorRespuestaError` +
> `AccesorCorrelationId` y escribe el cuerpo `ErrorRespuesta` (04 §3) con `correlationId` (que coincide con
> el encabezado `X-Correlation-Id` que ya fija `MiddlewareCorrelationId`). Se sustituyeron los resultados
> directos por este camino: `EndpointsAdminEnvios` (objeto anónimo → `NOT_FOUND`), `EndpointsPreparacion` y
> `FiltroClaveDiagnostico` (`Results.NotFound()` vacío → `NOT_FOUND` genérico, sin revelar la postura de la
> clave) y `EndpointsWebhook` (403 → `FORBIDDEN`, 401 → `UNAUTHENTICATED`, con mensaje genérico idéntico para
> ambos rechazos de cada estado, sin filtrar token, firma ni si el secreto estaba configurado). No cambian
> códigos HTTP, auth, autorización ni las respuestas exitosas; el 503 de readiness (reporte autorizado) se
> conserva. Pruebas: nueva `Jobs_Inexistente_Responde404ConModeloUniforme` + enriquecidas webhook 403/401,
> readiness 404 y filtro de diagnóstico 404 (verifican estado, código, `correlationId` del cuerpo, encabezado
> y no-fuga de secretos). **Verificado local (SDK 8.0.412):** build `-warnaserror` 0/0; test **469 verde (416
> unit + 53 integration; +1)**; format limpio.

## 1. Propósito

Hacer que todo error HTTP producido por la aplicación use el cuerpo `ErrorRespuesta` y conserve o genere `X-Correlation-Id`. Hoy algunas rutas devuelven resultados directos sin ese cuerpo, lo que impide a portal, integraciones y soporte asociar el error con el contrato documentado.

## 2. Alcance confirmado

Se revisarán los resultados directos de `EndpointsPreparacion`, `EndpointsWebhook`, `EndpointsAdminEnvios`, `FiltroClaveDiagnostico` y cualquier ruta equivalente encontrada mediante búsqueda. Se reutilizarán `EscritorRespuestaError`, `MapeadorErrores` y la infraestructura de correlación existentes.

Se normalizarán tanto los errores de negocio como los errores deliberadamente discretos. Por ejemplo, la ruta de preparación que no deba revelar estado devolverá `404` con un `ErrorRespuesta` seguro y genérico; los rechazos de credenciales o firma del webhook no expondrán detalles sensibles, pero sí el código, mensaje seguro y correlación del contrato.

No se altera la semántica de códigos HTTP, autenticación, autorización ni la política de qué información debe ocultarse.

## 3. Diseño de implementación

- Definir un único camino reutilizable para convertir un estado y un código de error en `ErrorRespuesta` y escribir el encabezado de correlación actual.
- Sustituir `Results.NotFound()`, `StatusCode(...)` y objetos anónimos de error de rutas de aplicación por ese camino. Los casos de excepción seguirán pasando por el manejador global ya establecido.
- Usar códigos estables y documentados: por ejemplo `NOT_FOUND` para recursos no revelados, `UNAUTHENTICATED` para credenciales inválidas y `FORBIDDEN` para una operación no permitida. La elección final debe corresponder a la semántica que ya aplica cada endpoint.
- No incluir excepción, secreto, firma, token ni estado interno en `mensaje` o `detalles` de respuestas públicas.
- Conservar el encabezado de correlación recibido cuando sea válido y generar uno cuando no exista, según el comportamiento común actual.

## 4. Contrato de salida

Toda respuesta de error de la aplicación tendrá el contenido definido en `Especificaciones/base/04_Contrato_API_REST.md`:

```json
{
  "error": {
    "code": "CODIGO_ESTABLE",
    "message": "Mensaje seguro para quien consume la API.",
    "details": [],
    "correlationId": "..."
  }
}
```

El estado HTTP sigue expresando la categoría del fallo. El cuerpo no cambia la información que la ruta puede revelar; solo la presenta en forma uniforme.

## 5. Criterios de aceptación y pruebas

- Las rutas identificadas no devuelven cuerpos vacíos, objetos anónimos ni texto plano para errores propios de la aplicación.
- Cada prueba de error comprueba estado HTTP, `codigo`, `correlationId` del cuerpo y encabezado `X-Correlation-Id`.
- Un fallo de webhook no revela el valor esperado de la firma, secretos ni configuración; un fallo de preparación tampoco revela la causa interna.
- Las respuestas exitosas y sus contratos permanecen iguales.
- Se actualizan o agregan pruebas de integración de las rutas intervenidas y pasa la puerta backend acordada.

## 6. Cómo probarlo

1. Solicitar una ruta protegida sin la credencial requerida y una ruta de administración con un identificador inexistente.
2. Verificar que el estado HTTP corresponde al caso, pero que el cuerpo siempre contiene código, mensaje seguro y `correlationId`.
3. Comparar `correlationId` con el encabezado `X-Correlation-Id`: deben coincidir.
4. Es un fallo si la respuesta está vacía, cambia el código HTTP sin motivo o muestra claves, firmas, rutas internas o trazas.

## 7. Riesgo y reversión

El riesgo es que un consumidor informal dependiera de un cuerpo no documentado. El contrato oficial ya exige `ErrorRespuesta`, por lo que esta iniciativa corrige la desviación. La reversión se limita a las rutas afectadas, aunque no debe utilizarse para restaurar cuerpos fuera de contrato.
