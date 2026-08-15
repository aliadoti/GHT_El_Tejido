# Plan de implementación — DT-P32-04

## Precondición

DT-P32-03 y la nueva corrida P-32 están en green. Capturar antes una línea base de pruebas y de
comportamiento gate OFF/ON; este refactor no corrige resultados funcionales nuevos.
No bloquea DT-I20-02, que conserva la prioridad acordada inmediatamente después del green de P-32.

## Corte 1/3 — idioma central

1. Crear `IdiomaConversacion` con pruebas exhaustivas de normalización, soportados y default histórico.
2. Adaptar una frontera por vez: Usuario, Campania, Conversacion, EnrutamientoAporte y EnvioMensaje.
3. Mantener strings en DTO y Cosmos; agregar pruebas de round-trip histórico.
4. Sustituir listas `es/en` y normalizadores duplicados.

## Corte 2/3 — contenido efectivo de campaña

1. Crear el resultado inmutable y el puerto en Application.
2. Cubrir snapshot legacy y localización completa sin mezcla de fuentes.
3. Migrar primero `ServicioEnvios`, luego contexto LLM y finalmente salidas visibles del orquestador.
4. Eliminar helpers privados duplicados solo cuando no queden consumidores.

## Corte 3/3 — resolutores y readiness compuesto

1. Encapsular catálogo global existente tras la interfaz acordada, sin reescribir cache/LKG.
2. Encapsular mapeos Meta y directiva LLM.
3. Hacer que readiness consulte los mismos resolutores en modo diagnóstico sin efectos.
4. Añadir prueba arquitectónica contra nuevas resoluciones directas.
5. Actualizar la tabla multidioma y el checklist de tercer idioma.

## Verificación por corte

- unitarias focalizadas y round-trip de persistencia;
- integración de campaña/envío/orquestador;
- build Release con warnings como error;
- suite no-Calibración y formato;
- portal solo si cambia la proyección de readiness;
- `git diff --check`.

Después del corte 3, repetir QAS/23 y QAS/17 antes de considerar el refactor cerrado.
