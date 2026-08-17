
# DEF-P32-04-01 — Una campaña activa acepta quedar bilingüe incompleta

Caso relacionado: QAS/16 prueba 0.4; QAS/23 pruebas 5 y 6  
Iniciativa: DT-P32-04  
Severidad: Alta  
Prioridad: P1  
Ambiente: Azure de pruebas · build 28c3cb1 · corrida P32-20260816-1955  
Flags: CatalogoTextosHabilitado=true durante la observación

## Precondición

Existe una campaña bilingüe activa con localizaciones completas en `es` y `en`.

## Pasos para reproducir

1. Editar la localización `en` de una campaña bilingüe activa.
2. Vaciar un campo obligatorio, por ejemplo nombre, cierre, mensaje inicial o pregunta.
3. Guardar la edición.
4. Consultar readiness.

## Resultado esperado

El sistema rechaza la edición antes de guardarla, informa los campos faltantes y conserva íntegra la campaña activa.

## Resultado obtenido

La edición se guarda. El readiness detecta `localizacion_campania_incompleta` y baja la señal de listo, pero la campaña ya quedó activa e incompleta.

## Impacto

No mezcla inglés con español, pero puede afectar la continuidad: una conversación que necesite ese contenido queda sin configuración resoluble y puede cerrarse con respuesta neutra.

## Evidencia

Reporte `Resultados_P32_Multidioma_2026-08-16_corrida-P32-20260816-1955.md`, sección 14, observación 4.

## Estado

Abierto