# DT-I20-02 — Contrato visible en texto plano y gobierno seguro de prompts

> **Estado:** ESPECIFICADA — 2026-08-13 — 0/3, sin código ni cambios de configuración remota.  
> **Prioridad:** inmediata por defecto visible a participantes.  
> **Origen:** reporte real de WhatsApp con encabezados Markdown e instrucciones internas de presentación.  
> **Alcance:** salida visible del evaluador, fragmentos de I-20 y selección segura del prompt de evaluación en runtime.  
> **No cambia:** contratos API/Cosmos, puntajes, umbrales, estados, cierres, versión I-19, P-27, P-32, P-33 ni mensajes históricos.

---

## 1. Problema observado

Un participante recibió una respuesta con esta forma:

```text
Ya quedó claro que ...
### Lo que ya queda claro
...
### Lo que todavía falta
...
### Siguiente ajuste recomendado
...
¿Qué decisión o cambio concreto ...?
```

La inspección de solo lectura realizada el 2026-08-13 confirmó:

1. La evaluación persistida ya contenía los encabezados. El gateway de WhatsApp envió el cuerpo recibido y no leyó un archivo `.md` del repositorio.
2. El prompt de evaluación usado en runtime, familia `1`, versión `2`, pedía literalmente secciones Markdown como `### Lo que ya queda claro`, `### Pregunta clave` y `### Estado`.
3. Ese prompt también incluía conceptos internos como `ready_to_save` y decisiones de guardado que pertenecen al servidor.
4. I-20 añadió el puente inicial, pero no creó los encabezados; su filtro actual atiende duplicación semántica, no estructura Markdown dentro del cuerpo recibido.
5. Las campañas activas inspeccionadas compartían `promptRefs.evaluar = "1"`. Actualizar directamente esa familia tendría alcance simultáneo.
6. No se confirmó exposición de nombres de rúbrica, puntajes o umbrales en el ejemplo reportado. Ese control existente debe conservarse.

La causa, por tanto, es una contradicción entre el contrato estructurado del evaluador y las instrucciones de presentación almacenadas en el prompt de runtime, combinada con una validación insuficiente de los campos visibles.

---

## 2. Causa técnica

El contrato del evaluador espera JSON con campos separados, entre ellos:

- `retroalimentacion_usuario`;
- `repregunta_sugerida`;
- puntajes y recomendación para decisión server-side.

Sin embargo, el prompt vigente ordena producir dentro de la retroalimentación una mini-respuesta ejecutiva con títulos, estado y pregunta. Hoy el backend valida JSON, escala, campos obligatorios y algunas fugas de rúbrica, pero no rechaza:

- encabezados o listas Markdown en campos destinados a WhatsApp;
- etiquetas internas de proceso;
- una pregunta incrustada en `retroalimentacion_usuario` cuando ya existe `repregunta_sugerida`;
- bloques de código, tablas u otras estructuras editoriales;
- texto excesivo que luego pueda quedar cortado a mitad de palabra por un límite ciego.

Además, el repositorio obtiene la versión numéricamente más reciente del prompt y después comprueba su estado. Si la última se inactiva, no necesariamente vuelve a una versión anterior activa. Por eso «inactivar la última versión» no es un rollback confiable.

---

## 3. Reglas de negocio que esta deuda debe preservar

| Regla existente | Invariante obligatorio en DT-I20-02 |
|---|---|
| I-19, versión evaluada | Evaluar y mostrar siempre la misma `VersionIdeaConsolidada` confirmada. No modificar `ideaId`, `versionId` ni el texto consolidado. |
| Evaluación y madurez | Conservar puntajes válidos, recomendación, umbrales, clasificación y arbitraje server-side. Un defecto de presentación en un campo visible no invalida la evaluación completa. |
| I-18, repreguntas | Cuando el flujo requiere continuar, entregar exactamente una pregunta y mantener `MaxRepreguntas`, `repreguntasUsadas` y el estado conversacional actual. |
| I-20, redacción | El modelo solo propone fragmentos; el servidor compone y decide. Mantener el respaldo seguro y el filtro de duplicación `DT-I20-01`. |
| P-27, control | Las intenciones de continuar, terminar, salir o retomar siguen siendo decididas por el servidor. Ningún texto del prompt puede ordenar una transición. |
| P-32, idioma | Validar igual en `es` y `en`, sin mezclar idiomas y respetando el snapshot de idioma del hilo. |
| P-33, idea visible | El texto de la versión I-19 se inserta carácter por carácter. Nunca aplicar un saneamiento global al mensaje ni a la idea. |
| WhatsApp | El gateway permanece como transporte. No debe reinterpretar ni limpiar contenido de negocio. |
| Historial | No reescribir evaluaciones, respuestas o mensajes ya persistidos. La corrección aplica únicamente a mensajes nuevos. |
| Contenido válido | Si un fragmento cumple el contrato, conservarlo sin reformulación ni normalización semántica. |

### Decisión de diseño para no romper esas reglas

Una infracción de formato visible se resuelve **por campo**, no descartando toda la evaluación:

- si `retroalimentacion_usuario` es inválida, sustituir solo ese campo por la retroalimentación neutral existente;
- si `repregunta_sugerida` es inválida y el flujo exige pregunta, sustituir solo ese campo por la repregunta neutral existente;
- mantener los puntajes válidos y dejar que el servidor aplique las mismas reglas de madurez, revisión y cierre;
- si falla el JSON o la evaluación de fondo, conservar el fallback completo que ya existe;
- si falla un fragmento de I-20, conservar el fallback completo de I-20 que ya existe.

No se autoriza eliminar Markdown con una expresión regular sobre el mensaje final: eso podría alterar una idea legítima, un texto configurado o contenido como `caja #3`.

---

## 4. Contrato visible objetivo

### 4.1 `retroalimentacion_usuario`

Debe ser texto conversacional breve y completo:

- una o dos frases completas;
- sin títulos, encabezados, listas, tablas, bloques de código ni separadores Markdown;
- sin etiquetas internas como `Estado`, `Pregunta clave`, `ready_to_save`, `save now` o equivalentes configurados;
- sin nombres de rúbrica, ejes, puntajes, escalas, umbrales o instrucciones internas;
- sin decidir guardar, cerrar, cambiar de estado o prometer acciones;
- sin pregunta si el turno ya llevará `repregunta_sugerida` por separado;
- dentro del límite vigente, sin truncar silenciosamente a mitad de palabra u oración.

Los saltos de línea por sí solos no hacen inválido el texto. El carácter `#` dentro de una frase tampoco: por ejemplo, `caja #3` es válido. Debe detectarse estructura, como un encabezado al inicio de línea (`^\s{0,3}#{1,6}\s`), no caracteres aislados.

### 4.2 `repregunta_sugerida`

Cuando el flujo la requiera:

- contiene exactamente una pregunta;
- está en el idioma del hilo;
- no contiene encabezado, lista, tabla, bloque de código ni etiqueta interna;
- no duplica una pregunta ya presente en otro fragmento;
- no altera el número de repreguntas permitido.

Cuando el flujo no la requiera, su contenido no debe forzar una pregunta ni una transición.

### 4.3 Fragmentos I-20

`puente` y `pregunta` deben pasar el mismo control estructural antes de la composición. Después sigue operando `FiltroDuplicacionTurno` de `DT-I20-01`; este nuevo control no lo reemplaza.

### 4.4 Contenido fuera de alcance del validador

No validar ni modificar con este componente:

- `VersionIdeaConsolidada.Texto`;
- respuestas originales del participante;
- textos del catálogo P-32;
- mensajes configurados de campaña;
- plantillas Meta;
- artefactos Markdown administrativos;
- el cuerpo final completo enviado por WhatsApp.

---

## 5. Diseño de implementación

### 5.1 Validador puro y reutilizable

Crear en Application un componente puro equivalente a `ValidadorFragmentoVisibleLlm`, con contexto tipado:

- tipo de fragmento: retroalimentación, repregunta, puente o pregunta;
- idioma esperado;
- si se exige pregunta;
- máximo aplicable;
- resultado válido/inválido y código fijo de motivo, sin devolver ni registrar el texto.

Debe detectar al menos:

- encabezados Markdown al inicio de línea;
- viñetas o listas numeradas usadas como estructura;
- cercas de código y tablas Markdown;
- etiquetas internas reservadas en español e inglés;
- cantidad de preguntas incompatible con el tipo de fragmento;
- exceso de longitud.

No debe incluir una lista de nombres de empresas ni bloquear palabras de negocio que el participante pudo usar legítimamente.

### 5.2 Integración con el evaluador

Después de deserializar y validar la evaluación de fondo, pero antes de persistirla:

1. validar `retroalimentacion_usuario`;
2. ante infracción estructural, usar `RetroNeutra` y registrar solo el código del motivo;
3. validar `repregunta_sugerida` cuando aplique;
4. ante infracción, usar `RepreguntaNeutra` y registrar solo el código del motivo;
5. mantener puntajes, recomendación y arbitraje server-side sin cambios;
6. conservar el filtro de fuga de rúbrica y sus respaldos actuales;
7. reemplazar el truncamiento ciego de campos visibles por validación y fallback, o por un recorte a frontera completa demostrado por pruebas. Nunca persistir una palabra cortada.

### 5.3 Integración con I-20

Validar los fragmentos generados antes de componer el turno:

1. si un fragmento viola el contrato, activar el fallback de I-20 ya definido;
2. si pasa, aplicar después el filtro de no duplicación de `DT-I20-01`;
3. insertar los cuerpos server-side exactamente como hoy;
4. mantener la regla de máximo una pregunta visible.

### 5.4 Resolución segura de prompts en runtime

Agregar una operación de repositorio/servicio específica para runtime que obtenga la versión más nueva que sea simultáneamente:

- de la familia pedida;
- activa;
- aprobada.

La consulta administrativa de «última versión» conserva su semántica actual. El cambio de runtime debe probar, como mínimo:

- versión 1 activa/aprobada + versión 2 inactiva ⇒ runtime usa versión 1;
- versión 1 activa/aprobada + versión 2 borrador ⇒ runtime usa versión 1;
- versión 2 activa/aprobada ⇒ runtime usa versión 2;
- ninguna activa/aprobada ⇒ comportamiento seguro actual de configuración no disponible.

### 5.5 Prompt corregido

El prompt candidato debe:

- mantener el objetivo de coaching, contexto de negocio y criterios de calidad;
- pedir únicamente los campos del JSON contractual;
- indicar que `retroalimentacion_usuario` y `repregunta_sugerida` son texto plano conversacional;
- prohibir títulos, listas, estado interno e instrucciones de guardado en los campos visibles;
- dejar explícito que el servidor decide persistencia, estados, cierres y siguiente paso;
- mantener la rúbrica como razonamiento interno, nunca como contenido visible.

No crear ni activar el prompt remoto durante la corrida de desarrollo. Su migración se rige por el runbook de esta deuda.

---

## 6. Cortes de ejecución

### Corte 1/3 — Guardia visible sin alterar decisiones

- crear el validador puro;
- agregar regresión con la estructura exacta reportada;
- integrar reemplazos por campo en el evaluador;
- integrar la guardia en I-20 antes de `DT-I20-01`;
- eliminar el truncamiento a mitad de palabra en los campos visibles;
- mantener contratos y persistencia compatibles.

### Corte 2/3 — Gobierno de versión runtime

- implementar selección de la versión activa/aprobada más nueva;
- conservar la consulta administrativa actual;
- probar avance, ausencia y rollback de versiones;
- documentar el comportamiento efectivo en la especificación base correspondiente.

### Corte 3/3 — Prompt candidato, calibración y continuidad

- preparar el contenido candidato sin modificar Cosmos;
- ejecutar pruebas unitarias, integración y regresiones de I-18/I-19/I-20/P-27/P-32/P-33;
- ejecutar D5 real y QAS humana en ambiente aislado cuando existan credenciales y autorización;
- actualizar `05`, `08`, reglas, `SUPUESTOS`, `TODO`, `AVANCES` y el prompt de arranque con el estado real;
- dejar la activación remota como acción humana separada y aprobada.

---

## 7. Pruebas obligatorias

### 7.1 Unitarias

1. Rechaza `### Lo que ya queda claro` al inicio de línea.
2. Acepta `La diferencia está en la caja #3.`.
3. Acepta texto plano con un salto de línea sin estructura editorial.
4. Rechaza lista, tabla y bloque de código.
5. Rechaza etiquetas internas en español e inglés.
6. Retroalimentación inválida usa respaldo sin cambiar puntajes ni recomendación.
7. Repregunta inválida usa respaldo cuando es obligatoria.
8. Una retroalimentación válida permanece carácter por carácter.
9. Una salida excesiva nunca queda persistida con palabra u oración cortada.
10. Los fragmentos I-20 inválidos toman el fallback existente.
11. `DT-I20-01` sigue omitiendo el puente duplicado después de esta validación.

### 7.2 Integración

1. El caso reportado produce un mensaje sin encabezados, sin etiquetas internas y con máximo una pregunta.
2. La evaluación mantiene la misma idea, versión, puntajes, madurez y decisión que tendría con campos visibles válidos.
3. I-18 conserva exactamente una repregunta y su contador.
4. P-27 conserva todas las salidas y continuaciones deterministas.
5. P-32 genera salida coherente en `es` y en `en`.
6. P-33 conserva carácter por carácter una idea que contenga `#`, listas u otro contenido legítimo.
7. El gateway recibe el cuerpo ya compuesto y lo transporta sin sanitización propia.
8. La resolución runtime ignora una versión más nueva que no esté activa y aprobada.

### 7.3 Regresión funcional humana

Usar el escenario anonimizado de almacenamiento en racks y verificar:

- no aparecen `###`, `Lo que ya queda claro`, `Estado`, `Pregunta clave` ni instrucciones internas;
- el mensaje es natural y no repite la misma idea en el puente y el cuerpo;
- aparece una sola pregunta cuando corresponde;
- la versión de la idea evaluada no cambia;
- el avance o cierre coincide con las reglas previas, no con una orden del prompt.

---

## 8. Observabilidad y seguridad

Registrar solo metadatos de baja cardinalidad, reutilizando la telemetría existente cuando sea posible:

- componente (`evaluador` o `redactor`);
- campo (`retroalimentacion`, `repregunta`, `puente`, `pregunta`);
- resultado (`aceptado` o `respaldo`);
- motivo fijo (`markdown_estructural`, `etiqueta_interna`, `cantidad_preguntas`, `longitud`, etc.);
- familia y versión del prompt cuando ya sean metadatos admitidos.

No registrar el texto generado, respuesta del participante, idea consolidada, nombres propios ni contenido del prompt.

---

## 9. Migración y rollback

1. Desplegar primero las guardias de código con configuración remota sin cambios.
2. Crear después una **familia nueva** de prompt; no publicar una versión adicional bajo la familia `1` para la prueba inicial.
3. Asociar la familia nueva solo a una campaña aislada de QA.
4. Ejecutar D5 y el QAS de esta deuda.
5. Migrar campañas de manera controlada tras aprobación humana.
6. Para rollback, restaurar el `promptRef` anterior de la campaña. No confiar en inactivar la última versión como mecanismo de retorno.
7. Si el defecto aparece antes de migrar prompts, las guardias deben seguir evitando exposición visible mediante respaldos seguros.

El detalle operativo está en `Especificaciones/planes/DT-I20-02_Runbook_Migracion_Prompt_Evaluacion.md`.

---

## 10. Criterios de aceptación

- [ ] El caso reportado tiene una prueba de regresión que falla antes y pasa después.
- [ ] No se envía estructura Markdown ni etiquetas internas desde campos generados por LLM.
- [ ] Un defecto de presentación sustituye solo el campo visible afectado.
- [ ] Puntajes, umbrales, madurez, estados y cierre conservan sus reglas actuales.
- [ ] La versión I-19 evaluada y mostrada permanece exacta.
- [ ] Se conserva máximo una pregunta y el presupuesto I-18.
- [ ] P-32 pasa en español e inglés.
- [ ] P-27, P-33 y `DT-I20-01` tienen regresiones verdes.
- [ ] Runtime selecciona la versión activa/aprobada más nueva y permite rollback verificable.
- [ ] No hay cambios históricos, de API, portal, Cosmos ni configuración remota durante el desarrollo.
- [ ] Build, pruebas no calibración, formato y `git diff --check` están verdes.
- [ ] D5/QAS real queda ejecutado o explícitamente pendiente por costo, credenciales y autorización.

---

## 11. Fuera de alcance

- reescribir mensajes o evaluaciones históricas;
- cambiar la rúbrica, escala, umbral o lógica de madurez;
- agregar estados conversacionales;
- cambiar contratos REST o documentos Cosmos;
- sanitizar globalmente todos los mensajes de WhatsApp;
- editar plantillas Meta o textos P-32;
- activar flags, desplegar o modificar Cosmos sin autorización humana;
- resolver contenido de negocio concreto dentro del prompt.

---

## 12. Instrucción para la próxima corrida

Implementar estrictamente los cortes en orden. Antes de editar código, releer:

1. este documento;
2. `I-19_Consolidacion_Progresiva_Ideas.md`;
3. `I-20_Redaccion_Conversacional_Fluida_y_Markdown_Ejecutivo.md`;
4. `DT-I20-01_Variacion_y_No_Duplicacion_Redaccion_Conversacional.md`;
5. `../base/05_Backend_WhatsApp_y_Conversacion.md`;
6. `../base/08_Backend_Evaluacion_LLM.md`;
7. `../Reglas_Conversacion_y_Participacion.md`;
8. el QAS y runbook asociados.

No modificar configuración remota. Detenerse y documentar si una implementación requiere cambiar puntajes, estados, versión de idea, presupuesto de repreguntas o contratos persistidos.
