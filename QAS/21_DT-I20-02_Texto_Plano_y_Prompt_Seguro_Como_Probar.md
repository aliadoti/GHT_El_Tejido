# QAS 21 — DT-I20-02: texto plano y prompt seguro

> **Estado:** `DT-I20-02` está **IMPLEMENTADA Y DESPLEGADA 3/3** (2026-08-15). Esta corrida incluye
> como preparación puntuada la creación de una **familia nueva**, su aprobación, la asociación a una
> campaña aislada y el rollback. Las guardias de código **no dependen de ningún flag**. En la prueba
> 8, «activa y aprobada» es la condición exacta: una versión activa sin aprobar tampoco se usa y el
> runtime cae a la anterior vigente. El contenido que debe copiarse está en
> `Especificaciones/planes/DT-I20-02_Prompt_Candidato_Evaluacion.md`.
> **Objetivo:** comprobar que WhatsApp responde de forma natural, sin mostrar formato editorial ni instrucciones internas, y sin cambiar las reglas de evaluación y conversación.

## Invocación corta para Claude o el agente de pruebas

```text
Lee y ejecuta estrictamente QAS/21_DT-I20-02_Texto_Plano_y_Prompt_Seguro_Como_Probar.md.
```

El agente debe leer también, antes de escribir datos:

- `Especificaciones/planes/DT-I20-02_Prompt_Candidato_Evaluacion.md`;
- `Especificaciones/planes/DT-I20-02_Runbook_Migracion_Prompt_Evaluacion.md`;
- `QAS/06_Criterios_Aceptacion_LLM.md` y `tests/Calibracion/README.md` si ejecutará D5.

## Autorización acotada para esta corrida

El usuario autoriza al agente de pruebas a crear exclusivamente:

- una familia de prompt nueva de tipo `evaluar`, con prefijo `qa_dt_i20_02_` y el identificador
  único de la corrida;
- la versión 1 candidata y, solo para la Prueba 8, una versión 2 con el mismo contenido;
- una copia en borrador de una campaña de QA, sin copiar participantes, con nombre
  `CAMP-QA-DT-I20-02-<identificador>`;
- usuarios y asociaciones de prueba estrictamente necesarios para los hilos `es` y `en`.

Esta autorización **no** permite modificar la familia `1`, campañas reales, rúbricas, configuraciones
LLM, secretos, App Settings, plantillas Meta, catálogos P-32, código, despliegues ni datos históricos.
No permite migrar ninguna campaña adicional. Si no puede distinguir inequívocamente una campaña de
QA de una real, debe crear una campaña aislada desde cero o marcar `BLOCKED`; nunca debe adivinar.

## Antes de empezar

Necesitas:

- un ambiente aislado, nunca una campaña productiva;
- una sesión administrativa autorizada;
- dos participantes nuevos de prueba, uno `es` y otro `en`, con teléfonos autorizados;
- una pregunta que permita aportar una idea y mejorarla;
- acceso de lectura al resultado para comparar idea, versión, evaluación y estado;
- autorización separada si la prueba usa un LLM real y genera costo.

Si se usará el webhook simulado contra Azure, un humano debe habilitar temporalmente
`Simulacion__Habilitada=true` y entregar `GHT_DIAG_KEY` como variable de entorno secreta. El agente
solo puede leerla del entorno y enviarla como `X-Diag-Key`: nunca debe mostrarla, buscarla, guardarla
ni incluirla en comandos o reportes. Al terminar, el humano debe volver la simulación a `false` y
retirar la variable. Sin ese acceso, las pruebas que dependan de simulación quedan `BLOCKED`.

No cambies prompts ni campañas activas de producción para ejecutar esta guía. Antes de cualquier
mensaje confirma que el canal saliente está aislado o que los teléfonos son de prueba autorizados.

## Preparación puntuada — familia nueva y campaña aislada

Registra un identificador único `I20-02-AAAAMMDD-HHMM` y ejecuta en este orden:

1. **Inventario de solo lectura.** Identifica la familia de evaluación y el `promptRef` efectivo de la
   campaña fuente. Registra familia y versión, sin copiar el contenido al reporte. No edites nada.
2. **Familia nueva.** En **Prompts versionados**, crea como borrador la familia
   `qa_dt_i20_02_<identificador normalizado>`, nombre
   `QA DT-I20-02 texto plano <identificador>`, tipo `evaluar` y versión 1. Copia **exactamente** el
   bloque de «Contenido candidato» del documento candidato; no copies el prompt antiguo ni agregues
   el esquema JSON que ya inyecta el backend.
3. **Prevalidación del borrador.** Comprueba por lectura que el ID es nuevo, versión `1`, estado
   `borrador`, sin aprobación, tipo `evaluar` y contenido exacto. Es `FAIL` si se creó bajo la familia
   `1` o si nació activo/aprobado.
4. **Aprobación.** Aprueba la versión 1 con el usuario administrador autorizado. Confirma por API o
   portal que quedó simultáneamente `activa` y aprobada; no basta con que aparezca en un selector.
5. **Campaña aislada.** Elige como fuente una campaña inequívocamente de QA con pregunta activa,
   rúbrica y ConfigLLM utilizables. Duplícala: la copia debe nacer `borrador`, con ID nuevo y **sin
   participantes**. Renómbrala `CAMP-QA-DT-I20-02-<identificador>`. No modifiques la fuente. Si no
   existe una fuente segura, crea una campaña nueva con recursos activos ya existentes, sin editarlos.
6. **Asociación efectiva.** Guarda el `promptRef` anterior para rollback. Asocia la familia nueva en
   `promptRefs.evaluar` tanto en la campaña como en la pregunta activa usada por la prueba: el valor
   de la pregunta tiene precedencia. Confirma por lectura que ambos apuntan a la familia nueva.
7. **Participantes y activación.** Crea/asocia solo los dos usuarios `es/en` de esta corrida, completa
   las localizaciones exigidas si la copia las usa y activa únicamente la campaña aislada. No envíes
   lote proactivo real; usa el transporte de prueba autorizado.

La preparación es `PASS` solo si familia, versión, campaña, pregunta y participantes pueden trazarse
inequívocamente y ninguna entidad preexistente fue modificada.

## Prueba 1 — Reproducir el caso reportado

1. Inicia un hilo aislado.
2. Responde con una idea equivalente a: comparar diferencias en almacenamiento de racks en el primer punto de arribo en Estados Unidos.
3. Continúa hasta recibir la retroalimentación y la siguiente pregunta.

Debe aparecer:

- un mensaje breve y natural;
- como máximo una pregunta;
- una relación clara con la idea aportada.

Es falla si aparece cualquiera de estos elementos:

- `###`;
- `Lo que ya queda claro`;
- `Lo que todavía falta`;
- `Siguiente ajuste recomendado`;
- `Pregunta clave`;
- `Estado`;
- `ready_to_save`, `save now` o una orden de guardar/cerrar;
- dos preguntas que pidan prácticamente lo mismo.

## Prueba 2 — Confirmar que no cambió la decisión de negocio

Para el mismo turno, consulta el resultado con las herramientas autorizadas y compara:

- identificador de idea;
- versión evaluada;
- puntajes;
- clasificación o madurez;
- estado del hilo;
- número de repreguntas usadas.

La corrección solo puede cambiar el fragmento visible inválido por un texto seguro. Es falla si cambia la idea o versión evaluada, se pierde un puntaje válido, se consume una repregunta adicional o el prompt decide cerrar/guardar por sí mismo.

## Prueba 3 — Una sola pregunta

1. Usa una idea que todavía necesite precisión.
2. Revisa el mensaje completo recibido.

Debe contener exactamente una pregunta útil. Es falla si la retroalimentación incluye una pregunta y después aparece otra repregunta separada, o si no aparece ninguna cuando el flujo exige continuar.

## Prueba 4 — Contenido legítimo con `#` y formato del participante

1. Aporta una idea que incluya literalmente `caja #3`.
2. Si P-33 está habilitada en el ambiente aislado, solicita ver la idea.

Debe conservarse `caja #3` exactamente. Es falla si desaparece el carácter, se cambia el texto de la idea o se aplica una limpieza general al mensaje.

## Prueba 5 — Español e inglés

Ejecuta el mismo flujo una vez en `es` y otra en `en`.

Debe cumplirse en ambos casos:

- texto natural en el idioma del hilo;
- sin encabezados, listas ni etiquetas internas;
- máximo una pregunta;
- sin mezcla de idiomas.

## Prueba 6 — Salidas y continuidad P-27

Desde un turno que espere mejora, usa las frases de continuar y terminar ya aceptadas por el sistema.

Debe ocurrir exactamente el comportamiento previo. Es falla si una instrucción del prompt ignora la intención del participante, fuerza otra pregunta o cambia el cierre decidido por el servidor.

## Prueba 7 — No duplicación I-20

Usa una idea cuya retroalimentación pueda parafrasear el cuerpo que el servidor inserta.

Debe verse una sola formulación del mismo contenido. Es falla si reaparece un puente como `Ya quedó claro que...` seguido de una segunda frase prácticamente idéntica.

## Prueba 8 — Rollback de prompt en ambiente aislado

Ejecuta esta prueba **después** de las Pruebas 1 a 7. La selección segura de versiones ya está
implementada (corte 2/3):

1. deja la versión 1 candidata activa y aprobada;
2. crea dentro de la familia QA una versión 2 en borrador con el mismo contenido candidato;
3. ejecuta el flujo y confirma en telemetría autorizada que runtime sigue usando la versión 1;
4. aprueba la versión 2 y confirma que queda activa/aprobada;
5. repite el flujo y confirma que runtime usa la versión 2;
6. restaura en la campaña **y en la pregunta** los `promptRefs.evaluar` anteriores anotados en la
   preparación;
7. confirma por lectura y por un recorrido aislado que el rollback usa la familia anterior.

Es falla si runtime intenta usar el borrador, no avanza a la versión 2 aprobada o el rollback no
restaura la familia anterior en el recorrido efectivo.

## Calibración D5 — calidad, costo y latencia

Con autorización expresa de costo, ejecuta el baseline D5 indicado en
`QAS/06_Criterios_Aceptacion_LLM.md` y `tests/Calibracion/README.md`, comparando la familia anterior
contra la familia candidata en casos equivalentes `es/en`: idea fuerte, idea débil, inyección y
salida. Registra calidad, tokens, costo y latencia sin contenido sensible. Sin presupuesto o
credenciales autorizadas, no ejecutes llamadas reales y marca D5 como `BLOCKED`.

## Evidencia mínima

Guardar, sin datos personales:

- fecha y ambiente;
- campaña/hilo técnico anonimizado;
- idioma;
- familia y versión de prompt;
- captura o transcripción anonimizada del mensaje;
- idea/versionId y resultado funcional, sin texto sensible;
- aprobado o fallido por cada prueba;
- motivo fijo de cualquier fallback observado.

Crear `QAS/resultados/Resultados_DT-I20-02_<AAAA-MM-DD>.md` con:

- ambiente, ejecutor, autorización y alcance realmente ejecutado;
- identificador de corrida;
- IDs de la familia, versiones, campaña, pregunta y usuarios, sin teléfonos completos;
- familia/versiones y `promptRefs` antes, durante y después del rollback;
- tabla `Preparación/Prueba | es | en | Estado | Evidencia | Observación`;
- resultado D5, costo/tokens/latencia si estaba autorizado, o `BLOCKED` concreto;
- confirmación de que no se modificaron la familia `1` ni campañas reales;
- estado final de la campaña QA y confirmación de simulación/secretos por parte del operador.

No borres la familia ni la campaña: la evidencia debe quedar auditable. No dejes la campaña QA
activa después de la corrida; ciérrala por la transición soportada si fue activada. No intentes una
transición inexistente ni elimines datos para forzar limpieza.

## Resultado final

La deuda puede cerrarse funcionalmente solo si:

- las ocho pruebas pasan;
- las regresiones automáticas están verdes;
- no se modificaron reglas de negocio ni contenido histórico;
- D5 no muestra degradación relevante de calidad;
- cualquier activación remota tiene aprobación humana y plan de rollback.
