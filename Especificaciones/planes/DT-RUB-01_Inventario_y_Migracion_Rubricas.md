# DT-RUB-01 — Inventario y migración de rúbricas

> **Estado:** CONSERVADO COMO REFERENCIA, **NO EJECUTAR EN EL ARRANQUE LIMPIO**. El 2026-08-16 el
> usuario decidió inicializar el sistema desde cero y no importar datos legacy; por tanto no existe
> migración operativa en el alcance vigente. Antes de reutilizar este procedimiento se deben cerrar
> las deudas de DT-RUB-01 §16 y obtener una autorización nueva.
> **Spec:** `../Iniciativas/DT-RUB-01_Rubrica_Estructurada_y_Evaluacion_Determinista.md` §10.
> **QAS:** `../../QAS/24_DT-RUB-01_Rubrica_Estructurada_y_Evaluacion_Determinista_Como_Probar.md`.

## 1. Qué cambia para los datos que ya existen

El código nuevo puede leer documentos actuales sin mutarlos: deriva el `id` desde `nombre`, el orden
desde la posición y conserva `contenidoMarkdown`. Esto **no garantiza** que una campaña legacy evalúe
igual, porque el nuevo contrato exacto también consume la estructura. Por esa razón el arranque limpio
no restaura esos documentos y este procedimiento queda suspendido.

Lo que sí cambia es la **integridad estructural** derivada en lectura (`03 §3.11`):

| Situación del documento | `integridadEstructural` | Efecto |
|---|---|---|
| Estructura válida y Markdown igual al compilado por el servidor | `valida` | Sin restricciones. |
| Estructura válida pero Markdown escrito a mano | `legacy_no_verificada` | Se lee y se sigue evaluando donde ya estaba configurada; **no** se puede activar ni asignar a una campaña nueva. |
| Estructura que rompe las reglas canónicas | `invalida` | Igual que la anterior. |

> **Consecuencia esperada:** **todas** las rúbricas anteriores a este cambio quedan
> `legacy_no_verificada`, porque ninguna fue compilada por el servidor. Es el resultado buscado — es
> exactamente la condición de la rúbrica `2` — y obliga a crear una versión estructurada antes de
> volver a comprometerla. Ver `SUPUESTOS.md#integridad-estructural-rubrica-dt-rub-01`.

## 2. Inventario futuro (suspendido; requiere cerrar DT-RUB-01 §16)

Ejecutar con una identidad de solo lectura y guardar la salida junto al reporte de QAS/24. Registrar
**ids, versiones, estados y cantidades**; nunca aportes, justificaciones ni secretos.

1. **Rúbricas y su integridad.** `GET /api/admin/rubricas?pageSize=200` y, por cada familia,
   `GET /api/admin/rubricas/{id}/versiones`. Anotar `id`, `version`, `estado`,
   `integridadEstructural`, cantidad de criterios y suma de pesos.
2. **Campañas que las referencian.** `GET /api/admin/campanias?pageSize=200` y, por campaña, sus
   preguntas. Anotar `campaniaId`, `estado`, `rubricaRef` y, por pregunta, `rubricaRef` +
   `versionRubrica` cuando existan (precedencia pregunta → campaña).
3. **Contradicciones a resolver primero.** Marcar toda familia cuya versión activa quede
   `legacy_no_verificada` **y** esté referenciada por una campaña `activa`. Esa es la lista de trabajo
   real.

Plantilla de la tabla a llenar:

| rubricaRef | versión | estado | integridad | nº criterios | suma pesos | campañas que la usan | preguntas con override |
|---|---|---|---|---|---|---|---|
| | | | | | | | |

## 3. Migración futura (no autorizada; requiere fixes y autorización separada)

No se deduce nada de un Markdown existente. **Los criterios, descripciones y pesos correctos los
define negocio**, no el agente ni una inferencia sobre el texto.

1. Negocio aprueba, por escrito, los criterios y pesos de cada familia a corregir.
2. En **Rúbricas**, usar **Crear nueva versión** sobre la familia; llenar escala, instrucciones y la
   tabla de criterios con los valores aprobados.
3. **Revisar y previsualizar** (`POST /api/admin/rubricas/prevalidar`): la suma debe dar 100 %, no
   debe haber motivos de error y el preview debe listar exactamente los criterios aprobados.
4. Guardar como borrador, reabrir y confirmar que estructura y preview son idénticos.
5. Activar la versión. La versión anterior queda intacta y **ninguna campaña se reapunta sola**.
6. Fijar la versión exacta en **una campaña QA aislada** (y en los overrides de pregunta si aplica).
7. Ejecutar `QAS/24` completo sobre esa campaña.
8. Solo con QAS/24 en green, y con autorización expresa, migrar campañas reales una por una.
9. D5 se ejecuta **después**, con `n=3`, la misma versión de rúbrica, modelo, parámetros y golden set
   para ambos brazos.

## 4. Rollback

- Restaurar `rubricaRef` / `versionRubrica` en la campaña **y** en cada pregunta modificada.
- **Nunca** borrar versiones ni evaluaciones, ni convertir una versión activa en borrador.
- Si el runtime nuevo falla, revertir el corte de código: los campos agregados son aditivos y un
  lector anterior los ignora.

## 5. Fuera de alcance de este documento

- Decidir cuáles son los criterios y pesos correctos (es de negocio).
- Ejecutar el inventario o la migración contra datos reales sin autorización expresa.
- Tocar ConfigLLM, App Settings, secretos, plantillas Meta o el gate de P-32.
