# Plan de implementación — DT-RUB-01

> **Estado:** **COMPLETA LOCAL — corte 0 + 4/4 (2026-08-16).** Contratos `03`, `04`, `07`, `08` y
> `11` declaran estructura canónica, Markdown derivado, salida exacta por `criterio_id` y total
> server-side. Progresión del gate focalizado: baseline **925 unitarias + 111 de integración
> (`Category!=Calibracion`) + 1 de calibración** → corte 1 **959 + 119 + 1** → corte 2
> **985 + 119 + 1** → corte 4 **992 + 120 + 1**. Portal: **70 pruebas en 9 archivos**. Build Release
> `-warnaserror`, `dotnet format`, `git diff --check`, `ng build` y Prettier verdes en cada corte.
> **Sin push, despliegue, Cosmos, Azure, D5 ni migración real.**
>
> **Nota de entorno:** `npx ng test` es intermitente en esta máquina —el pool de vitest a veces no
> arranca algún worker y reporta menos archivos de los que existen, nunca una aserción fallida—.
> Repetir hasta ver los 9 archivos; `VITEST_MAX_FORKS=1` lo hace más probable y más rápido.
> **Spec:** `../Iniciativas/DT-RUB-01_Rubrica_Estructurada_y_Evaluacion_Determinista.md`.
> **QAS:** `../../QAS/24_DT-RUB-01_Rubrica_Estructurada_y_Evaluacion_Determinista_Como_Probar.md`.

## Resultado buscado

Una versión de rúbrica estructurada gobierna criterios, pesos, escala, prompt efectivo, validación,
total, eje débil, antifuga y snapshot. La campaña solo selecciona esa versión y el prompt queda
reutilizable con cualquier rúbrica.

## Antes de tocar código

1. Leer `PROMPT_Inicio_Desarrollo_Codex.md`, `AVANCES.md`, `SUPUESTOS.md`, la spec, este plan y
   `TODO.md`.
2. Confirmar rama, `git status` y cambios ajenos; preservar `.obsidian/workspace.json` y el reporte
   QAS no versionado si aún aparecen.
3. Inspeccionar contratos y código reales; los archivos de la spec son orientación.
4. Ejecutar el gate focalizado vigente y registrar su baseline.
5. Actualizar primero `03`, `04`, `07`, `08` y `11` en un cambio documental separado.

## Corte 1/4 — estructura canónica

1. Escribir pruebas rojas del modelo para 1, 3 y 8 criterios, duplicados, orden, escala y pesos.
2. Extender `CriterioRubrica` con id, descripción y orden; preservar lectura de documentos históricos.
3. Crear un `ValidadorRubricaEstructurada` puro y un `CompiladorRubricaMarkdown` determinista.
4. Hacer que `Rubrica` solo acepte/active una estructura válida y exponga Markdown derivado.
5. Actualizar `ConfigCosmosDocument` con campos aditivos e integridad; probar round-trip antiguo/nuevo.
6. Actualizar DTOs/endpoints y agregar prevalidación/preview sin escritura sobre el mismo servicio.
   Rechazar el cuerpo entero ante cualquier error.
7. Verificar build, pruebas focalizadas, no-Calibración, formato y diff.

## Corte 2/4 — evaluación autoritativa

1. Escribir pruebas rojas para criterio faltante, extra, duplicado y puntaje fuera de escala.
2. Inyectar ids/criterios/escala/pesos ordenados y un esquema de salida exacto.
3. Emparejar `calificaciones` por `criterio_id`; no por el nombre visible.
4. Calcular el total ponderado con `decimal`, sin redondear decisiones; ignorar el total del modelo.
5. Hacer que umbrales y madurez consuman el total server-side sin cambiar su semántica.
6. Migrar `CalculadorEjeDebil` y `FiltroSalidaRubrica` a la lista canónica.
7. Persistir snapshot completo de rúbrica y criterios; conservar lectura histórica.
8. Ejecutar regresiones I-03, I-17, I-19, I-20, DT-I20-02, P-32 y P-33.

## Corte 3/4 — portal estructurado

1. Escribir pruebas que fallen con el hardcode `Impacto`/escala fija.
2. Crear el editor de escala, instrucciones y criterios ordenables.
3. Mostrar suma de pesos, errores por fila y preview derivado.
4. Bloquear la edición de versiones activas/archivadas y ofrecer nueva versión clonada.
5. Mantener campaña/pregunta como selectores de referencia y versión; añadir ayuda contextual.
6. Verificar que el portal no envíe `contenidoMarkdown` como autoridad.
7. Ejecutar Prettier, pruebas y build Angular.

## Corte 4/4 — integración y cierre local

1. Probar el mismo prompt con rúbricas de 1, 3, 5 y 8 criterios.
2. Probar persistencia de snapshot y total tras crear una versión posterior.
3. Probar fallback ante las cuatro formas de salida LLM inválida.
4. Actualizar contratos efectivos, QAS/24, TODO, AVANCES, índice y handoff.
5. Ejecutar toda la validación proporcional y `git diff --check`.
6. Entregar inventario de rúbricas/campañas a migrar; no modificar datos remotos.

## Verificación mínima

```text
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build --filter "Category!=Calibracion"
dotnet test -c Release --no-build --filter "Category=Calibracion"
dotnet format --verify-no-changes --no-restore
cd src/ElTejido.Web
npx ng test --watch=false
npx ng build
npx prettier . --check
git diff --check
```

Si la categoría de calibración no existe o requiere insumos externos, registrar el bloqueo exacto y
ejecutar las pruebas unitarias del cálculo ponderado. Un build Angular no sustituye `ng test`.

## Pruebas mínimas que no se pueden omitir

- modelo/validador/compilador con cantidades variables;
- API create/update/version/estado e inmutabilidad;
- round-trip Cosmos legacy y nuevo;
- constructor del prompt con ids exactos;
- evaluador con conjunto exacto y cada anomalía;
- total ponderado y formato de presentación, incluidos pesos no uniformes y bordes de umbral;
- eje débil con empate y filtro antifuga con todos los nombres;
- snapshot de evaluación y lectura histórica;
- portal: alta, edición, quitar, ordenar, pesos, escala, preview y nueva versión;
- E2E conversacional que demuestre que la decisión usa el total server-side.

## Handoff y operación posterior

- No inventar los criterios/pesos correctos de la rúbrica `2`.
- No tocar Azure, Cosmos, ConfigLLM, prompts, campañas reales ni secretos durante implementación.
- Tras despliegue autorizado, un administrador crea una versión corregida, la prueba en campaña
  aislada y ejecuta QAS/24.
- D5 se ejecuta después, con `n=3`, la misma versión de rúbrica, modelo, parámetros y golden set para
  ambos prompts.
- Solo un D5 comparable habilita congelar baseline y decidir migración real.

## Rollback

Revertir el corte de código correspondiente y conservar documentos aditivos. En operación, restaurar
`rubricaRef/versionRubrica` tanto en campaña como en overrides de pregunta. No borrar ni reescribir
versiones o evaluaciones.
