# Runbook — DT-I20-02: migración segura del prompt de evaluación

> **Uso:** después de implementar y desplegar las guardias de `DT-I20-02`.  
> **Propietario de la activación:** humano autorizado.  
> **Prohibido en la corrida de desarrollo:** modificar Cosmos, asociar campañas reales, activar prompts, flags o despliegues.

## Riesgo que controla este runbook

Las campañas activas inspeccionadas el 2026-08-13 compartían la familia de prompt de evaluación `1`. Una nueva versión dentro de esa familia podría afectar varias campañas al mismo tiempo. Además, inactivar la versión numéricamente más reciente no es un rollback seguro mientras runtime no seleccione explícitamente la versión activa y aprobada más reciente.

## Prerrequisitos

- código de `DT-I20-02` desplegado en ambiente aislado;
- pruebas automáticas verdes;
- selección runtime de versión activa/aprobada verificada;
- acceso humano autorizado al portal/configuración;
- campaña de QA sin participantes reales;
- responsable, ventana, criterio de éxito y rollback acordados;
- credenciales del LLM entregadas por el canal humano autorizado, nunca registradas en documentos o logs.

## Preparar el prompt candidato

Crear una familia nueva para la prueba. No agregar la primera corrección como otra versión de la familia `1`.

El contenido debe conservar:

- objetivo de coaching;
- contexto y criterios de calidad necesarios;
- contrato JSON ya definido por el backend;
- idioma solicitado por el hilo.

Debe eliminar de los campos visibles:

- títulos y secciones Markdown;
- `Estado`, `Pregunta clave` y etiquetas semejantes;
- `ready_to_save`, `save now` y cualquier orden de persistencia;
- instrucciones para decidir transición, cierre o guardado;
- exposición de rúbrica, puntajes, escala o umbral.

Debe indicar que `retroalimentacion_usuario` y `repregunta_sugerida` son texto plano y que el servidor decide estados y transiciones.

## Secuencia de activación

1. Crear la familia nueva como borrador.
2. Revisar el contenido entre negocio, producto y desarrollo.
3. Aprobar/activar solo la versión candidata autorizada.
4. Asociarla únicamente a la campaña aislada de QA.
5. Ejecutar `QAS/21_DT-I20-02_Texto_Plano_y_Prompt_Seguro_Como_Probar.md`.
6. Ejecutar D5 contra el baseline autorizado y revisar calidad, costo y latencia.
7. Si todo pasa, migrar una sola campaña controlada.
8. Observar fallbacks, errores, latencia y calidad durante la ventana acordada.
9. Migrar las demás campañas una por una solo con aprobación.
10. Registrar familia/versiones antes y después, responsable, fecha y evidencia sin contenido sensible.

## Criterios para detener la migración

Detenerse y ejecutar rollback si ocurre cualquiera:

- reaparecen encabezados, listas o etiquetas internas;
- aparece más de una pregunta;
- cambia la versión de idea evaluada/mostrada;
- cambia el avance, cierre o presupuesto de repreguntas sin causa de negocio;
- aumenta de forma material el fallback, costo o latencia;
- hay mezcla de idiomas;
- no puede identificarse inequívocamente la familia/version usada.

## Rollback

1. Detener nuevas migraciones.
2. Restaurar en la campaña afectada el `promptRef` de la familia anterior aprobada.
3. Verificar con una conversación aislada que runtime cargó la versión activa/aprobada esperada.
4. Mantener las guardias de código activas: son el control de seguridad ante una salida visible inválida.
5. Registrar el motivo fijo y la versión, sin copiar contenido del participante o del prompt.
6. No intentar rollback limitándose a inactivar la versión más reciente.

## Evidencia de cierre

- pruebas QAS y D5 aprobadas;
- versión runtime efectiva verificada;
- métricas antes/después;
- lista de campañas migradas;
- prueba documentada de rollback;
- acta humana de aprobación;
- `TODO.md` y `AVANCES.md` actualizados con el estado real.
