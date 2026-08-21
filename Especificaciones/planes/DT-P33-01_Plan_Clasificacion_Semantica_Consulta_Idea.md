# Plan de implementación — DT-P33-01

> **Estado:** hotfix desplegado en `85b78f8` / `v1.0.3-convencion`; workflow/readiness verdes, gates
> semántico/visibilidad ON y catálogo inglés v3 activo. Validación integral al terminar el fix completo.

## Corte 1 — clasificador único y candidato transportado — DONE

1. Crear pruebas rojas para `consultarIdea|confirmarIdea`, aportes mixtos, contrato JSON estricto,
   inyección y fallback.
2. Agregar `ConsultarIdea` y `ConfirmarIdea` al final de `IntencionControl` y ampliar el prompt sin
   cambiar ids, estados ni autoridad server-side.
3. Transportar una clasificación ya realizada en `ResultadoEnrutamiento.ContinuarConversacion` y
   demostrar que el orquestador no hace una segunda llamada.
4. Compartir la guarda de cupos/tokens de clasificación; un mensaje genera a lo sumo un evento y un uso.
5. Mantener el nuevo gate OFF y ejecutar las regresiones focales P-27/P-10.

## Corte 2 — routing P-33 y cierre — DONE

1. Integrar la clasificación semántica antes de menús/afinidad solo bajo los gates de la spec.
2. Consumir `consultarIdea` con el selector P-33 existente; el modelo nunca recibe la versión.
3. Persistir la afinidad P-33 después de mostrar una idea abierta o cerrada y consumir
   `confirmarIdea` solo contra esa referencia server-side vigente.
4. Aplicar la transición existente según estado: confirmar/evaluar pendiente, cerrar/avanzar abierta ya
   confirmada, o solo acusar cerrada; nunca emitir otra repregunta de coaching.
5. Eliminar el patrón puntual hardcodeado y conservar el catálogo como fast path determinista.
6. Probar `es/en`, conformidad pura/mixta, menú pendiente, idea abierta/cerrada, P-27 OFF, múltiples
   campañas, expiración, fallback y dedupe.
7. Ejecutar build, suites completas no-Calibración, formato y `git diff --check`.
8. Sincronizar spec/TODO/AVANCES/SUPUESTOS/QAS con conteos reales y dejar ambos gates OFF.

## Límites

Sin REST, DTO, Cosmos, portal, Azure, despliegue, push, secretos, catálogo remoto, ConfigLLM remoto ni
activación de flags. No crear un segundo clasificador ni permitir dos llamadas por mensaje.

## Evidencia

Build Release `-warnaserror`, 1043 unitarias y 121 de integración sin Calibración, formato y diff
verdes. El E2E reproduce la consulta inglesa no catalogada y la conformidad posterior, vinculadas a
la misma idea, sin repregunta, reevaluación ni repetición de la versión.

## Hotfix determinista posterior al despliegue — DONE LOCAL

1. Reproducir `How is my idea going?` → `No is all right for me` y localizar la dependencia del
   clasificador para una conformidad no catalogada.
2. Resolver afinidad P-33 + coincidencia exacta de `frases.confirmar` antes del LLM, sin ejecutar la
   transición fuera de las validaciones server-side existentes.
3. Agregar siete alias ingleses a semilla y catálogo editable; conservar español sin cambios.
4. Cubrir idea abierta, cerrada y mensaje mixto. Resultado local: 1053 unitarias + 121 integración,
   build Release `-warnaserror`, formato y diff verdes.
5. Handoff operativo cerrado: commit `85b78f8`, tag `v1.0.3-convencion`, deploy/readiness verdes e
   inglés v3 activo. Siguiente: terminar el fix completo y ejecutar QAS/25 abierto/cerrado/mixto,
   D5, costo/latencia y acta en una corrida integral.
