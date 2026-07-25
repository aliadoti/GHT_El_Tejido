# P-18 — Dar nombre accesible a controles de selección y formularios

**Estado:** DONE local — 2026-07-25
**Origen:** `UXA11Y-001` de la auditoría técnica del 2026-07-24.
**Dependencias:** ninguna externa.

## 1. Propósito

Permitir que una persona que usa lector de pantalla identifique la finalidad de todos los controles antes de activarlos. Los textos visibles, los placeholders y la posición en una tabla no sustituyen un nombre accesible programático.

## 2. Alcance confirmado

Se corregirán como mínimo:

- las casillas de selección total y por fila de `EnviosPage`;
- los tres campos de etiquetas en línea de `UsuariosPage`;
- la carga CSV de esa misma pantalla;
- cualquier control análogo descubierto al revisar las pantallas modificadas.

La iniciativa no rediseña formularios, no cambia los datos enviados ni sustituye la validación actual. P-19 se ocupa de anunciar sus resultados dinámicos.

## 3. Diseño de implementación

- Usar `label` visible asociado por `for`/`id` cuando el control tenga una etiqueta visible propia.
- Cuando una casilla de fila no pueda mostrar texto sin empeorar la tabla, asignar un `aria-label` específico que incluya el contexto necesario, por ejemplo el nombre de la persona o campaña. La selección total tendrá un nombre como “Seleccionar todos los envíos visibles”.
- Sustituir placeholders usados como único nombre por etiquetas visibles o visualmente ocultas accesibles, sin usar `title` como alternativa.
- Asociar la entrada de archivo con una etiqueta clara que indique el tipo de archivo esperado; las instrucciones adicionales se conectarán con `aria-describedby` cuando corresponda.
- Revisar que no se generen `id` repetidos en filas, formularios o componentes reutilizados.

## 4. Criterios de aceptación y pruebas

- Cada `input`, `select`, `textarea`, botón de selección y control de archivo intervenido expone un nombre accesible no vacío.
- La selección por fila identifica de forma inequívoca qué elemento cambia; la selección total explica su alcance.
- Los placeholders quedan como ayuda, no como única etiqueta.
- Las pruebas de componentes verifican etiquetas, atributos ARIA condicionales e identificadores únicos cuando haya listas.
- Una revisión manual con el árbol de accesibilidad del navegador permite leer nombre y rol correctos para cada control corregido.

## 5. Cómo probarlo

1. Abrir **Envíos** y recorrer con Tab la selección total y dos filas distintas.
2. Abrir **Usuarios**, recorrer los campos de etiquetas y el selector de CSV.
3. Con lector de pantalla o el panel de accesibilidad, confirmar que cada control anuncia qué hace y sobre qué elemento actúa.
4. Es un fallo si se oye solo “casilla”, “editar” o “elegir archivo” sin contexto suficiente.

**Verificación realizada:** pruebas de componentes para los nombres de las casillas, los campos de etiquetas y las instrucciones del CSV; `prettier --check`, `ng test --watch=false` (15 pruebas) y `ng build --configuration production` verdes con Node temporal 24.15.0.

## 6. Riesgo y reversión

Es un cambio aditivo de marcado y no altera datos ni permisos. Se revisará el comportamiento visual de las etiquetas ocultas y la tabla para evitar regresiones de diseño; la reversión es localizada por control.
