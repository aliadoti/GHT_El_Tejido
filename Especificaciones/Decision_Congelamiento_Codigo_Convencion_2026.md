# Decisión de congelamiento de código — Convención 2026

> ⚠️ **Este documento fue modificado por una adenda.** Ver
> [`Adenda_Congelamiento_Convencion_2026.md`](./Adenda_Congelamiento_Convencion_2026.md) (2026-08-20).
> El artefacto en producción **ya no es `28c3cb1`** sino `v1.0.3-convencion`, y cuatro de las
> condiciones de este acta fueron modificadas durante el montaje del ambiente y el piloto controlado.
> No leer este documento de forma aislada.

> **Decisión:** APROBADO CONDICIONADO — congelar el código en `28c3cb1` para la convención.
> **Fecha:** 2026-08-16.
> **Alcance:** una sola campaña en un ambiente nuevo y exclusivo, con base de datos y configuración
> creadas desde cero.

## Razón de la decisión

El cierre local del artefacto aprobó build Release sin advertencias, 1030 pruebas unitarias y 120 de
integración sin Calibración, formato y `git diff --check`. La convención no depende de los dos cambios
de código que quedaron identificados después de esa validación:

- `DT-P32-05` no bloquea porque la campaña se parametriza completa antes de activarla y queda
  operativamente prohibido editarla después del primer envío;
- `DT-QA-03` no bloquea porque la convención usa los servicios reales de WhatsApp, no la simulación.

Las plantillas Meta ya están aprobadas. Su asociación y verificación efectiva forman parte de la
parametrización del ambiente nuevo.

## Condiciones obligatorias del alcance congelado

1. El ambiente es exclusivo para la convención y comienza sin documentos legacy.
2. Existe una sola campaña; se crea y completa en borrador antes de activarla.
3. Después de activar o realizar el primer envío no se editan campaña, localizaciones, mensajes,
   preguntas, rúbrica, prompts ni catálogos. Un cambio exige pausar y reevaluar el congelamiento.
4. Se mantiene una sola versión operativa por familia de rúbrica y prompt; no se crea una versión o
   borrador posterior mientras la campaña esté en uso.
5. `Simulacion__Habilitada=false` y no se usa `GHT_DIAG_KEY`. Entrada y salida usan WhatsApp real.
6. Los secretos se inyectan por las referencias previstas y nunca se guardan en archivos o reportes.

## Puertas operativas antes del primer envío

Estas tareas no son desarrollo pendiente, pero sí condicionan la salida:

1. desplegar exactamente el artefacto congelado y verificar `/health/ready`;
2. parametrizar desde cero catálogos `es/en`, campaña, mensajes, preguntas, rúbrica, prompts,
   ConfigLLM, mapeos Meta y flags aprobados;
3. cargar y validar los usuarios reales cuando GHT entregue el archivo definitivo;
4. ejecutar D5 con la configuración definitiva y autorización de costo;
5. realizar smoke/UAT bilingüe con teléfonos autorizados usando WhatsApp real;
6. revisar costo, latencia, trazabilidad y rollback, y firmar el acta de flags;
7. verificar justo antes del envío que readiness está verde y que no hay borradores o configuraciones
   posteriores a las versiones seleccionadas.

Si cualquiera de estas puertas falla, no se corrige en caliente: se detiene el envío, se conserva la
evidencia y se decide explícitamente si se parametriza de nuevo o se descongela el código.

## Deuda aceptada fuera del alcance

- `DT-P32-05`: corregir post-convención la edición inválida de una campaña activa.
- `DT-QA-03`: implementar post-convención una salida simulada observable para QA.

La aceptación de estas deudas solo aplica al alcance anterior; no constituye aceptación general para
operación continua, múltiples campañas o edición posterior a la activación.
