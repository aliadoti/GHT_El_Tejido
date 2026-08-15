# QAS 23 — DT-P32-03: cierre localizado y readiness Meta

## Precondiciones

- Ambiente de pruebas autorizado y gate inicialmente OFF.
- Catálogos `es/en` activos y válidos.
- Campaña de prueba completa en `es/en` con `plantillaRef` real.
- Plantillas Meta aprobadas y teléfonos de prueba si se ejecutará envío real.

## Prueba 1 — regresión gate OFF

Ejecuta las rutas de cierre normal, salida explícita, tope/cupo, fallback e inactividad con el gate
OFF. Todas deben conservar el cierre legacy exacto.

## Prueba 2 — matriz de cierres bilingües

Con gate ON, ejecuta cada ruta de cierre con un hilo `es` y otro `en`. El cierre debe coincidir con
`localizaciones.{idioma}.mensajeCierre`; ninguna salida inglesa puede contener el cierre español.

## Prueba 3 — localización inconsistente

En una prueba automatizada o fixture aislado, simula una campaña histórica activa sin cierre del
idioma del hilo. Debe aparecer el fallo tipificado y cero fallback a otro idioma; no debe quedar una
transición parcial ni duplicarse al reintentar.

## Prueba 4 — readiness sin mapeo

Usa una campaña borrador/activa que requiera `inicio_campania` para `es/en` y retira el mapeo solo en
el fixture/local autorizado. `listoParaGateOn` debe ser `false`, readiness debe señalar exactamente el
par faltante y listar la campaña afectada. No cambies una configuración compartida sin autorización.

## Prueba 5 — readiness estructural completo

Configura `Nombre`, `Idioma` y los `Componentes` exactos de las plantillas aprobadas. Tras reiniciar la
API, readiness debe mostrar ambos pares configurados y `listoParaGateOn=true` si los catálogos también
están listos.

## Prueba 6 — componentes y límite de la comprobación

- Un componente vacío o duplicado se reporta como inválido.
- `Componentes=[]` es válido únicamente si la plantilla aprobada no tiene variables de cuerpo.
- Verifica manualmente en Meta el número, orden y significado de variables. Readiness no puede afirmar
  que la plantilla está aprobada ni que coincide con Meta.

## Prueba 7 — lote mixto

Con autorización de tráfico real, envía a un participante `es` y uno `en`. Ambos deben usar nombre,
código Meta y valores de cuerpo de su mapeo. Un fallo selectivo no debe detener el otro envío.

## Evidencia y salida

Registrar por prueba `PASS|FAIL|BLOCKED`, IDs sin teléfonos completos, estado final del gate, pares
requeridos, cantidad/orden de componentes y referencia verificable a la plantilla aprobada. No
registrar tokens ni secretos.

Solo continuar con QAS/17 cuando las pruebas 1–6 estén en PASS y la 7 esté PASS o BLOCKED por una
razón externa explícita aceptada por el responsable humano.
