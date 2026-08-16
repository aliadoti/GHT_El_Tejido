# QAS 24 — DT-RUB-01: rúbrica estructurada y evaluación determinista

> **Estado:** **implementada localmente (4/4) el 2026-08-16; pendiente de despliegue autorizado.**
> Las pruebas 1, 2, 3, 5, 6, 7, 8, 9 y 10 ya tienen cobertura automatizada verde en el repositorio
> (992 unitarias + 120 de integración + 1 de calibración; portal 70). Esta guía sirve para
> **revalidar en ambiente desplegado** y para las pruebas 4 y la preparación, que se hacen a mano.
> **Objetivo:** demostrar que la rúbrica seleccionada es la única fuente de criterios y que el total
> usado por negocio lo calcula el servidor.

## Lo que ya cambió en el producto (contexto para quien prueba)

- **Rúbricas** ya no tiene un cuadro de Markdown editable. Se edita la **estructura**: escala,
  instrucciones generales y una tabla de criterios con id, nombre, descripción y peso, con botones
  **Agregar**, **Quitar**, **Subir**, **Bajar**, la **suma de pesos** a la vista y
  **Revisar y previsualizar**, que trae el Markdown desde el servidor sin guardar nada.
- El botón **Editar** de una versión activa o archivada dice **Crear nueva versión**: la nueva nace
  en borrador y la anterior no se toca.
- Una rúbrica cargada antes de este cambio aparece con estructura **«sin verificar»** y su botón
  **Activar** está deshabilitado. **Esto es lo esperado**, no un defecto: hay que crear una versión
  estructurada nueva con los criterios que apruebe negocio.
- La campaña y la pregunta solo seleccionan familia y versión, y muestran la ayuda «Los criterios se
  administran en Rúbricas; aquí se selecciona una versión completa».

## Antes de empezar

- Usa un ambiente aislado y una campaña QA nueva; no edites campañas reales.
- No modifiques una rúbrica activa. Crea una familia QA o una versión nueva en borrador.
- Usa nombres y pesos sintéticos; no copies información sensible.
- Si habrá LLM real o tráfico, confirma antes costo, credencial y canal autorizado.
- Registra ids/versiones, nunca secretos, teléfonos completos, aportes ni justificaciones del modelo.

## Preparación puntuada

1. En **Rúbricas**, crea `qa_dt_rub_01_<fecha>` con escala 1–5 y tres criterios:
   `claridad` 30 %, `viabilidad` 50 % y `alcance` 20 %.
2. Comprueba que la suma es 100 %, el preview muestra los tres en ese orden y no aparece `Impacto`
   salvo que tú lo hayas escrito como criterio.
3. Guarda como borrador, vuelve a abrir y confirma que estructura y preview son idénticos.
4. Actívala y comprueba que ya no se puede editar en sitio.
5. Crea una nueva versión, agrega un cuarto criterio, cambia el orden y déjala inicialmente en
   borrador. La versión activa anterior debe permanecer intacta.
6. Crea una campaña aislada y selecciona explícitamente familia y versión. La campaña no debe ofrecer
   controles para editar criterios.

Si falla cualquiera de estos pasos, la preparación queda `FAIL` y no se ejecuta LLM real.

## Prueba 1 — cantidad variable

Crea o verifica mediante pruebas automatizadas rúbricas de 1, 3, 5 y 8 criterios. Todas deben poder
guardarse si cumplen reglas. El servidor y el portal no deben agregar, quitar ni renombrar criterios.

## Prueba 2 — validaciones completas

Intenta guardar, uno por uno: cero criterios, id vacío, id duplicado, nombre duplicado, peso cero,
pesos que no suman 100 %, órdenes repetidos y escala con mínimo mayor o igual al máximo. Cada caso
debe rechazarse completo, señalar el campo correcto y no crear una versión parcial.

> **Cómo se ve.** El servidor responde `400 VALIDATION_ERROR` con un detalle por campo:
> `criterios.{i}.id: requerido|duplicado|formato_invalido`, `criterios.{i}.nombre: requerido|duplicado`,
> `criterios.{i}.peso: fuera_de_rango`, `criterios.{i}.orden: duplicado|no_consecutivo`,
> `criterios.pesos: suma_invalida`, `escala: invalida`, `criterios: requerido|limite_excedido`. El
> portal los traduce a frases como «Criterio 2: ese valor ya lo usa otro criterio» o «Los pesos deben
> sumar 100%». Un id con espacios o mayúsculas se rechaza como `formato_invalido`: la clave admite
> solo minúsculas, números y guion bajo. Nota: **se reportan todos los motivos a la vez**, no solo el
> primero, para poder corregir la tabla completa de una pasada.

## Prueba 3 — fuente única y preview

Abre la versión guardada por API y portal. Criterios, pesos, escala y orden deben coincidir. El
Markdown/preview debe derivarse de esos mismos datos y no tener un criterio adicional. Recargar o
guardar dos veces la misma estructura debe producir el mismo Markdown y hash.

## Prueba 4 — campaña solo selecciona

Asocia la versión de tres criterios a campaña y, si se usa un override de pregunta, selecciona allí
otra versión completa. Verifica la precedencia pregunta → campaña. No debe existir una lista de
criterios editable dentro de campaña o pregunta.

## Prueba 5 — contrato exacto del modelo

La evidencia automatizada debe cubrir cuatro respuestas simuladas:

- falta un criterio;
- aparece uno adicional;
- un id está duplicado;
- un puntaje está fuera de escala.

Todas deben rechazarse y seguir el fallback seguro existente. Una salida válida debe contener una vez
cada id de la versión efectiva, sin depender del nombre visible.

> **Ya cubierto en el repositorio.** El modelo devuelve `calificaciones` con `criterio_id` (la clave
> vieja `calificacion_por_criterio`/`criterio` ya no se usa). Los motivos que aparecen en el registro
> de fallback son `salida_invalida:criterio_faltante`, `:criterio_extra`, `:criterio_duplicado`,
> `:puntaje_fuera_escala` y `:justificacion_vacia`. En el fallback **no** quedan notas parciales: la
> evaluación se guarda sin calificaciones y con total 0.

## Prueba 6 — total ponderado del servidor

Con la rúbrica de preparación y puntajes `claridad=5`, `viabilidad=3`, `alcance=4`, el total esperado
es:

```text
(5 × 0.30) + (3 × 0.50) + (4 × 0.20) = 3.80
```

Haz que el LLM falso también envíe un total deliberadamente distinto, si el contrato de
compatibilidad aún lo acepta. La evaluación, madurez y umbral deben usar `3.80`, nunca el valor del
modelo.

> **Ya cubierto.** Hay un recorrido conversacional completo por el webhook en el que el modelo
> califica el criterio con 5 pero declara un total de 1: la idea queda **madura** y la evaluación
> persiste **5**, que es el ponderado del servidor. Cuando el total del modelo difiere, queda el
> registro `total_modelo_difiere` con rúbrica, versión y la magnitud de la diferencia — sin texto ni
> datos del participante.

## Prueba 7 — eje débil y antifuga

Con los puntajes anteriores, `viabilidad` es el eje débil. Confirma en prueba automatizada que:

- el cálculo lo identifica por id canónico;
- una retroalimentación que revele cualquiera de los tres nombres o un puntaje cae al respaldo;
- agregar/reordenar criterios en una versión nueva cambia ambas políticas sin editar código.

No expongas los textos usados por el test en logs de ambiente.

## Prueba 8 — snapshot e historia

Genera una evaluación con v1. Luego crea/activa v2 sin migrar la campaña. La evaluación histórica debe
seguir mostrando id, versión, escala, criterios, pesos, puntajes y total de v1. La campaña debe seguir
usando su versión fijada hasta cambiar la referencia explícitamente.

## Prueba 9 — mismo prompt, dos rúbricas

Asocia la misma familia/version de prompt de evaluación a dos campañas QA con rúbricas distintas.
Ejecuta una evaluación válida en cada una. Cada salida debe contener exactamente los criterios de su
rúbrica, sin editar ni duplicar el prompt.

## Prueba 10 — compatibilidad legacy

Verifica con fixtures que un documento histórico se puede leer y que una evaluación histórica sin
`criterioId` conserva su nombre snapshot. Una rúbrica legacy contradictoria debe aparecer como no
verificada/inválida y no debe poder usarse para una nueva activación hasta crear una versión válida.

> **Qué esperar en el ambiente desplegado.** **Todas** las rúbricas anteriores al despliegue van a
> aparecer como «sin verificar», incluida la rúbrica `2`. Es lo correcto: ninguna fue compilada por
> el servidor, así que no se puede afirmar que su estructura y su Markdown digan lo mismo. Siguen
> leyéndose y las campañas ya configuradas siguen evaluando igual que antes; lo único bloqueado es
> activarlas o asignarlas a algo nuevo. El procedimiento está en
> `Especificaciones/planes/DT-RUB-01_Inventario_y_Migracion_Rubricas.md`.

## Regresiones obligatorias

- DT-I20-02: texto plano, una sola pregunta y versión de prompt vigente.
- I-03: eje débil y antifuga.
- I-17/I-19/I-20: total, madurez, umbrales, consolidación y redacción.
- P-32/P-33: idioma y salida visible sin cambios.
- API/Cosmos y portal completos.

## D5 — únicamente después del green anterior

Compara el prompt anterior y el candidato con:

- exactamente la misma rúbrica y versión corregida;
- el mismo modelo, parámetros y golden set;
- `n=3` por caso;
- el mismo cálculo server-side.

Si falta credencial o autorización de costo, marca D5 `BLOCKED`; no rebajes `n`, no cambies la
rúbrica entre brazos y no congeles baseline.

## Rollback y estado final

- Restaura `rubricaRef/versionRubrica` de campaña y de cada pregunta modificada.
- Archiva la campaña QA mediante una transición soportada; no borres evidencia.
- No cambies familia de prompt, ConfigLLM, App Settings o campañas reales.
- Registra cada prueba como `PASS|FAIL|BLOCKED`, los conteos de suites y cualquier limitación.
- Guarda el reporte en `QAS/resultados/Resultados_DT-RUB-01_<fecha>.md`.

DT-RUB-01 queda green solo con pruebas 1–10 y regresiones en PASS. D5 puede quedar BLOCKED únicamente
por una dependencia externa explícita, pero en ese estado no se cierra el baseline ni se migra una
campaña real.

