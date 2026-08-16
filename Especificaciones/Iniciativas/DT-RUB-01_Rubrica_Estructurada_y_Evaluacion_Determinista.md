# DT-RUB-01 — Rúbrica estructurada como fuente única y evaluación determinista

> **Estado:** ESPECIFICADA — 0/4, siguiente prioridad de código.
> **Origen:** `QAS/resultados/Resultados_DT-I20-02_2026-08-16.md` §9.4.
> **Bloquea:** D5 confiable, el cierre funcional de DT-I20-02 y la congelación de un baseline de
> evaluación. No bloquea los ocho PASS funcionales ya obtenidos en QAS/21.
> **No es:** una reapertura del contrato visible de DT-I20-02 ni el refactor multidioma DT-P32-04.

## 1. Problema confirmado

La rúbrica `2` usada en la corrida Azure conserva dos representaciones contradictorias:

- el registro estructurado declara un único criterio, `Impacto`, con peso `1`;
- `contenidoMarkdown` enumera cinco ejes y el LLM devuelve calificaciones para esos cinco ejes.

La contradicción nace en el portal: `rubricas.page.ts` permite editar el Markdown, pero al guardar
siempre envía `escala={min:1,max:5}` y `criterios=[{nombre:"Impacto",peso:1}]`. El backend admite una
lista variable, pero actualmente no exige que la salida del modelo coincida con ella y acepta como
autoridad `calificacion_total` calculada por el propio modelo.

Esto produce cuatro riesgos:

1. el total puede no corresponder a los pesos configurados;
2. `CalculadorEjeDebil` puede no encontrar el criterio devuelto por el LLM;
3. `FiltroSalidaRubrica` solo conoce la lista estructurada y puede omitir nombres presentes únicamente
   en el Markdown;
4. D5 puede comparar prompts usando una evaluación cuya semántica interna no es reproducible.

## 2. Decisión funcional y arquitectónica

La **estructura de la versión de rúbrica** es la única fuente de verdad. La campaña y la pregunta solo
seleccionan una familia y una versión; nunca crean, editan, eliminan, reordenan ni sobrescriben
criterios.

```text
Editor de rúbrica estructurada
       |
       +-- escala + instrucciones + criterios ordenados + pesos
       |
       v
Compilador determinista de rúbrica
       |
       +-- contenidoMarkdown derivado (preview, LLM y auditoría)
       +-- contrato exacto de salida del LLM
       +-- cálculo total server-side
       +-- eje débil y filtro antifuga
       +-- snapshot de la evaluación

Campaña/pregunta --------------------> rubricaRef + versionRubrica
Prompt de evaluación ----------------> agnóstico de nombres de criterio
```

No hay que pedir al autor del prompt que copie los nombres de los criterios. Antes de cada llamada,
el servidor inyecta la versión efectiva completa y el contrato exacto que debe devolver el modelo.
Así una misma familia de prompt funciona con una rúbrica de uno, cinco u ocho criterios.

## 3. Modelo objetivo

### 3.1 Versión de rúbrica

Cada versión conserva:

```json
{
  "id": "r_general",
  "version": 3,
  "nombre": "Evaluación general",
  "descripcion": "...",
  "estado": "borrador",
  "escala": { "min": 1, "max": 5 },
  "instruccionesGenerales": "Evalúa la propuesta con evidencia del aporte.",
  "criterios": [
    {
      "id": "claridad",
      "nombre": "Claridad",
      "descripcion": "Qué tan comprensible y concreta es la propuesta.",
      "peso": 0.30,
      "orden": 1
    }
  ],
  "contenidoMarkdown": "# ...",
  "integridadEstructural": "valida"
}
```

Reglas:

- `criterios` es una lista ordenada de longitud variable; no existe un número funcional fijo;
- `id` es una clave estable, normalizada y única dentro de la versión; el nombre es la etiqueta
  visible y puede contener espacios o tildes;
- `peso` es decimal mayor que cero y la suma debe ser `1`; el portal lo presenta como porcentaje;
- `orden` es único, consecutivo y determina tanto el preview como el contrato enviado al LLM;
- la escala es global para la versión, con `min < max`;
- nombres e identificadores no pueden estar vacíos ni repetirse tras normalizar mayúsculas y tildes;
- se aplica un techo técnico compilado razonable para evitar payloads abusivos, sin convertirlo en un
  número de negocio. Valor inicial recomendado: 50 criterios y el límite general de tamaño del API;
- `contenidoMarkdown` se genera en el servidor de forma determinista desde los campos anteriores. Es
  de solo lectura para clientes nuevos y puede persistirse como proyección/snapshot compatible;
- el hash de integridad se calcula sobre la representación canónica, no sobre el orden de propiedades
  del JSON recibido.

No se agregan escalas independientes por criterio en este corte. Si negocio las requiere, serán una
extensión versionada posterior; no se deben inferir desde texto libre.

### 3.2 Versionado e inmutabilidad

- `borrador`: editable en sitio y jamás usado por runtime;
- `activa` o `archivada`: inmutable;
- editar una versión comprometida crea una versión nueva en borrador, inicialmente clonada;
- activar la nueva versión no cambia silenciosamente campañas o preguntas que fijaron otra versión;
- documentos y evaluaciones históricas no se reescriben.

Una rúbrica legacy se puede leer. Si sus representaciones no son coherentes, se marca
`legacy_no_verificada` o `invalida`; no puede asignarse a una nueva campaña ni activarse como versión
nueva hasta crear una versión estructurada válida. Las campañas históricas no se migran
automáticamente.

## 4. Autoría en el portal

La pantalla **Rúbricas** reemplaza la edición libre del Markdown por:

- nombre, descripción e instrucciones generales;
- escala mínima y máxima;
- tabla ordenada de criterios con id, nombre, descripción y peso;
- acciones agregar, editar, quitar y mover, disponibles únicamente dentro de la versión borrador;
- suma de pesos visible y validación inmediata;
- preview Markdown obtenido del servidor mediante prevalidación sin escritura; el portal no mantiene
  un segundo compilador en TypeScript;
- acción **Crear nueva versión** para una rúbrica activa/archivada.

La campaña conserva únicamente sus selectores `rubricaRef` y `versionRubrica`, con la precedencia ya
existente pregunta → campaña. No muestra un editor de criterios. La ayuda debe decir: “Los criterios
se administran en Rúbricas; aquí se selecciona una versión completa”.

El hardcode de `Impacto`, la escala fija y el Markdown inicial que aparenta ser la fuente deben
desaparecer del portal y de sus pruebas.

## 5. Contrato API

Las operaciones de crear, editar borrador y crear versión reciben la estructura de §3.1. El servidor:

1. normaliza y valida toda la versión;
2. rechaza el cuerpo completo si existe un criterio inválido;
3. genera `contenidoMarkdown` canónico;
4. persiste estructura y proyección como una sola operación;
5. devuelve la versión resultante con su integridad.

`POST /api/admin/rubricas/prevalidar` recibe esa misma estructura, ejecuta el mismo validador y
compilador y devuelve `valido`, errores tipificados y `contenidoMarkdown` derivado sin escribir. El
portal lo usa para la revisión previa y el preview; no se acepta como prueba de activación.

Errores estables bajo `VALIDATION_ERROR`:

- `criterios: requerido`;
- `criterios.{i}.id: requerido|duplicado|formato_invalido`;
- `criterios.{i}.nombre: requerido|duplicado`;
- `criterios.{i}.peso: fuera_de_rango`;
- `criterios.{i}.orden: duplicado|no_consecutivo`;
- `criterios.pesos: suma_invalida`;
- `escala: invalida`;
- `rubrica: integridad_invalida`.

Durante una transición acotada, el API puede aceptar `contenidoMarkdown` legacy para lectura, pero no
puede dejar que contradiga una estructura nueva. Los contratos base `03`, `04`, `07`, `08` y `11`
deben actualizarse antes del primer cambio de código para reflejar esta fuente canónica.

## 6. Construcción del prompt

`ConstructorMensajesEvaluacion` deja de confiar en un Markdown libre. Debe inyectar un bloque
determinista con:

- id y versión de la rúbrica;
- escala exacta;
- criterios en orden con `id`, nombre, descripción y peso;
- instrucciones generales;
- esquema JSON que exige un resultado por cada `criterio_id`.

Ejemplo conceptual para una rúbrica de dos criterios:

```json
{
  "calificaciones": [
    { "criterio_id": "claridad", "puntaje": 4, "justificacion": "..." },
    { "criterio_id": "viabilidad", "puntaje": 3, "justificacion": "..." }
  ],
  "retroalimentacion_usuario": "...",
  "recomendacion": "repreguntar",
  "repregunta_sugerida": "..."
}
```

El prompt administrable define método, tono y restricciones, pero no enumera criterios funcionales.
Si un prompt legacy los nombra manualmente, la prevalidación/preview debe advertir
`prompt_contiene_criterios_fijos`; no se intenta reconciliar una lista humana con la rúbrica en
runtime.

## 7. Validación y cálculo server-side

La salida válida contiene **exactamente** los ids de la versión efectiva:

- ninguno faltante;
- ninguno adicional;
- ninguno duplicado;
- un puntaje por criterio dentro de la escala;
- una justificación no vacía y acotada por criterio.

La comparación se hace por `criterio_id`, no por texto visible. Una salida inválida sigue la política
de fallback existente; no se inventan notas parciales ni se agrega un reintento LLM en esta deuda.

El total de negocio lo calcula el servidor:

```text
total = sum(puntaje * peso) / sum(peso)
```

Se usa `decimal` sin redondear antes de aplicar umbrales o clasificar madurez; el formato de portal o
reporte puede mostrar dos decimales sin cambiar el valor autoritativo. Con pesos válidos la suma es
`1`. `calificacion_total` deja de ser requerida al modelo; si se acepta temporalmente por
compatibilidad, se ignora para decisiones y persistencia y solo puede emitir una métrica de
diferencia sin texto ni PII.

Umbrales, madurez, cierres, Markdown ejecutivo y calibración consumen exclusivamente el total
calculado por el servidor.

## 8. Eje débil, antifuga y snapshots

- `CalculadorEjeDebil` empareja por id canónico y toma el menor puntaje. Desempate determinista:
  menor peso, luego `orden`, luego `id` ordinal;
- `FiltroSalidaRubrica` deriva los nombres y aliases únicamente de la misma lista canónica; revisa
  todos los criterios, cualquiera que sea su cantidad;
- la evaluación conserva snapshot de id/versión, escala, instrucciones/hash, criterios ordenados,
  nombres, descripciones, pesos, puntajes y total calculado;
- para documentos históricos sin `criterioId`, la lectura conserva el nombre snapshot y no muta el
  documento. La compatibilidad no habilita nuevas escrituras ambiguas.

## 9. Observabilidad y seguridad

Registrar solo ids, versiones, cantidades, hash y códigos estables:

- `criterio_faltante`;
- `criterio_extra`;
- `criterio_duplicado`;
- `puntaje_fuera_escala`;
- `rubrica_inconsistente`;
- `total_modelo_difiere`.

No registrar Markdown, descripciones, justificaciones, aportes ni respuestas visibles. La respuesta
del participante continúa delimitada como dato y no puede cambiar la rúbrica inyectada.

## 10. Compatibilidad y migración

1. Inventariar las rúbricas y campañas que las referencian, sin modificar datos.
2. Desplegar lectores compatibles y escritores estrictos.
3. Crear manualmente una **versión nueva** de la rúbrica `2` con los criterios, descripciones y pesos
   aprobados por negocio. No deducir pesos del Markdown existente.
4. Prevalidar y activar esa versión.
5. Migrar primero una campaña aislada; fijar la versión exacta en campaña y overrides de pregunta.
6. Ejecutar QAS/24.
7. Solo después ejecutar D5 con la misma rúbrica, versión, modelo, parámetros y golden set para ambos
   prompts; `n=3` por caso como exige la guía de calibración.
8. La migración de campañas reales requiere autorización humana separada.

Rollback: restaurar las referencias de campaña/pregunta a la versión previa. Nunca borrar versiones
ni evaluaciones. Si el nuevo runtime falla, revertir el corte de código manteniendo los documentos
aditivos; no convertir una versión activa en borrador.

## 11. Alcance de código por corte

### Corte 0 — contratos, primero y en commit separado

Actualizar `03`, `04`, `07`, `08` y `11` para declarar estructura canónica, Markdown derivado,
salida exacta y total server-side. No cambiar código en este corte.

### Corte 1/4 — dominio, compilador, persistencia y API

- extender `CriterioRubrica` y `Rubrica` sin romper lectura histórica;
- agregar validador y compilador deterministas;
- persistir estructura, proyección e integridad;
- actualizar DTOs/endpoints y pruebas de dominio/API/Cosmos.

### Corte 2/4 — evaluación autoritativa

- inyectar el contrato exacto;
- validar conjunto de ids, duplicados y escala;
- calcular total ponderado server-side;
- migrar eje débil, antifuga y snapshots a la lista canónica;
- cubrir fallback y regresiones de umbrales/madurez.

### Corte 3/4 — portal

- editor estructurado, reordenamiento, pesos y preview;
- inmutabilidad/nueva versión;
- campaña como selector, sin editor de criterios;
- pruebas de componente y E2E administrativa.

### Corte 4/4 — integración, documentación y preparación operativa

- pruebas cruzadas con rúbricas de 1, 3, 5 y 8 criterios;
- actualizar QAS/24, TODO, AVANCES y handoff;
- preparar inventario/migración sin tocar Cosmos/Azure;
- dejar D5 y campañas reales como operaciones autorizadas posteriores al despliegue.

## 12. Archivos/fronteras probables

- `src/ElTejido.Domain/Configuracion/{Rubrica,CriterioRubrica}.cs`
- `src/ElTejido.Application/Evaluacion/{ConstructorMensajesEvaluacion,EvaluadorLlm}.cs`
- `src/ElTejido.Application/Evaluacion/{CalculadorEjeDebil,FiltroSalidaRubrica}.cs`
- nuevo validador/compilador puro en Domain o Application, sin dependencia de Cosmos/API;
- `src/ElTejido.Infrastructure/Configuracion/ConfigCosmosDocument.cs`
- `src/ElTejido.Infrastructure/Respuestas/EvaluacionCosmosDocument.cs`
- `src/ElTejido.Api/Admin/EndpointsAdminFase4.cs`
- `src/ElTejido.Web/src/app/features/rubricas/rubricas.page.{ts,spec.ts}`
- modelos y servicio tipado del portal;
- pruebas unitarias, de integración API/Cosmos y E2E conversacional.

Los nombres son orientación. Antes de editar, localizar las fronteras reales y evitar duplicar la
compilación o la validación en portal, API y evaluador.

## 13. Criterios de aceptación

1. No existe `Impacto` ni una escala `1..5` hardcodeada en el flujo de guardado del portal.
2. Se puede crear y versionar una rúbrica válida con cualquier cantidad admitida de criterios.
3. Cero criterios, ids/nombres repetidos, pesos inválidos, orden ambiguo o escala inválida se rechazan
   sin escritura parcial.
4. El Markdown derivado es determinista y no puede contradecir la estructura.
5. Campaña y pregunta solo seleccionan una versión completa; sus formularios no editan criterios.
6. El mismo prompt vigente evalúa correctamente dos campañas con rúbricas distintas sin nombrar sus
   criterios en el texto administrable.
7. Falta, sobra o se duplica un criterio en la respuesta LLM y la evaluación cae al fallback seguro.
8. El total persistido coincide con el cálculo ponderado del servidor y no con un total suministrado
   por el modelo.
9. Eje débil y filtro antifuga usan todos y solo los criterios de la versión efectiva.
10. La evaluación persiste un snapshot suficiente para explicar el resultado aunque luego exista una
    versión nueva.
11. Una versión activa no se sobrescribe; una corrección crea borrador nuevo y no altera evaluaciones
    históricas.
12. Lecturas legacy siguen funcionando; nuevas escrituras inconsistentes no.
13. Las suites DT-I20-02, I-03, I-17/I-19/I-20, P-32/P-33 y calibración permanecen verdes.
14. Ninguna prueba o telemetría expone aportes, justificaciones, prompts completos, secretos o PII.

## 14. Fuera de alcance

- definir por negocio cuáles son los cinco criterios correctos o sus pesos;
- migrar o activar campañas reales;
- ejecutar D5 pagado, cambiar ConfigLLM o credenciales;
- editar rúbricas activas en sitio;
- escalas diferentes por criterio, fórmulas no ponderadas o traducción automática de criterios;
- importar/exportar rúbricas masivamente en JSON; puede especificarse después sin cambiar esta fuente
  canónica;
- DT-P32-04 y cambios de idioma.

## 15. Definition of Done

- Corte 0 separado antes del código.
- Cuatro cortes pequeños, compilables y reversibles.
- Backend: build Release con warnings como errores, pruebas no calibración y calibración aplicable,
  formato y `git diff --check` verdes.
- Portal: Prettier, pruebas y build de producción verdes.
- QAS/24 actualizado en lenguaje simple.
- Sin push, despliegue, Cosmos, Azure, D5 ni migración real salvo nueva autorización expresa.
