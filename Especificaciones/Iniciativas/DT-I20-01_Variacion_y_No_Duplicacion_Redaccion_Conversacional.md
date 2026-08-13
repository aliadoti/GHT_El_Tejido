# DT-I20-01 — Variación natural y no duplicación en la redacción conversacional

| Campo | Decisión |
|---|---|
| Estado | **DONE LOCAL 2026-08-13 (5/5 pasos de §7).** Backend: build Release `-warnaserror`, 785 unitarias (766 sin Calibración) + 88 de integración, `dotnet format` y `git diff --check` verdes. Falta D5 con ejemplos reales y despliegue. |
| Alcance | I-20, evaluación/coaching y composición del mensaje saliente |
| Aplica | Todas las campañas y todo mensaje **nuevo** generado después del despliegue |
| No aplica | Mensajes ya enviados, historial, ideas, evaluaciones ni Markdown históricos |

## 1. Problema confirmado

En algunas conversaciones se repite la apertura `Queda claro que...` (o variantes como `Ya queda claro...`) de manera mecánica. Además, se confirmó una duplicación dentro de un mismo envío: el puente redactado y la retroalimentación validada pueden expresar el mismo acuse, por ejemplo `Ya queda claro...` dos veces.

La expresión no es incorrecta ni queda prohibida. El defecto es su uso sistemático y su repetición visible en el mismo turno.

## 2. Resultado esperado

Cada turno nuevo debe sonar natural y conservar el contenido seguro ya validado:

- `Queda claro que...`, `Se entiende que...`, `Es evidente que...` y equivalentes se pueden usar ocasionalmente, nunca como apertura obligatoria ni como única fórmula.
- Un mismo mensaje no repite la misma oración, apertura o reconocimiento sustancial entre puente, retroalimentación, versión insertada y pregunta.
- La retroalimentación validada conserva su significado; la corrección editorial no puede cambiar evaluación, estados, límites, preguntas aprobadas ni decisiones del servidor.
- Todas las campañas reciben la mejora automáticamente al desplegarla. No hay opt-in por campaña, migración de Cosmos ni edición de datos históricos.

Ejemplo válido: `Ya definiste responsables e indicadores para el seguimiento. ¿Cómo conectarías ese seguimiento con una acción que ayude a aumentar los ingresos?`

## 3. Causa y frontera de responsabilidad

`ConstructorMensajesEvaluacion` instruye al coaching a reconocer algo concreto que ya esté claro. `RedactorTurnoConversacional` pide, para el acto `Mejorar`, reconocer un avance real. Ambas indicaciones son útiles, pero no exigen variedad ni separan con suficiente precisión la función del puente de la retroalimentación que el servidor inserta.

La causa de la duplicación no se resuelve solo con un prompt: el servidor compone `puente -> cuerpo -> pregunta`. Por tanto:

1. El LLM propone el estilo y puede elegir una fórmula de reconocimiento.
2. El servidor conserva la propiedad del contenido validado y descarta de forma determinista un puente que lo duplique.
3. El LLM nunca decide qué se envía, qué se evalúa, cuándo se cierra ni qué contenido histórico se modifica.

## 4. Diseño

### 4.1 Variación editorial controlada

Reemplazar las instrucciones actuales que convierten el reconocimiento en una fórmula fija por estas reglas de salida para `retroalimentacion_usuario` y para el puente del acto `Mejorar`:

- Reconoce un elemento concreto del aporte solo cuando aporta valor al turno.
- Alterna de manera natural entre: reconocimiento concreto, conexión con lo ya dicho, pregunta directa de profundización o transición breve.
- Las fórmulas `queda claro`, `se entiende`, `es evidente` y equivalentes están permitidas, pero no se usan por defecto ni en turnos consecutivos cuando exista otra formulación natural.
- No repitas, parafrasees ni anticipes el texto que irá en otra parte del mismo mensaje.

El redactor recibe además una indicación estructural: cuando el cuerpo sea una `retroalimentacionValidada`, el puente no puede volver a reconocer el mismo avance. Si no aporta una función distinta, devuelve `puente: null` y deja que el mensaje inicie con el cuerpo validado.

No se agregan listas de frases, configuración por campaña ni un catálogo nuevo: es una regla de voz global de I-20 y debe quedar localizada en sus constructores de mensaje.

### 4.2 Guarda determinista al componer el envío

Antes de combinar `puente`, `cuerpo` y `pregunta`, el servidor normaliza para comparación (trim, minúsculas, espacios repetidos, puntuación y tildes equivalentes) y aplica:

1. Si el puente coincide con una oración del cuerpo, es prefijo del cuerpo o el cuerpo es prefijo del puente, omite el puente.
2. Si la primera oración del puente y la primera oración del cuerpo son equivalentes tras normalizar, omite el puente.
3. Si la pregunta duplica una oración ya visible, omite la pregunta solo si el acto admite quedar sin pregunta; si el acto exige una pregunta, descarta la salida del redactor completa y usa el respaldo seguro existente.

La guarda es deliberadamente conservadora: compara equivalencia y prefijos, no intenta inferir similitud semántica con otro LLM. Ante duda conserva el cuerpo validado y elimina solo el adorno redundante.

**Qué actos exigen pregunta (decisión de implementación, 2026-08-13):** `Confirmar`, `Mejorar`, `Aclarar` y `ResumirAvance` pierden su función sin pregunta, así que una pregunta duplicada en ellos descarta la redacción completa y el turno sale con su respaldo determinista. `Reabrir` y `Reactivar` admiten quedar como invitación afirmativa, así que allí la pregunta duplicada solo se omite. El resto de actos no lleva pregunta y la guarda del redactor ya la rechaza.

### 4.3 Alcance por tipo de turno

La variación aplica a toda salida LLM visible de evaluación y redacción. La guarda de composición aplica a cualquier acto I-20 que tenga puente y cuerpo (`Confirmar`, `Mejorar`, `Reabrir`, `ResumirAvance` y los que se agreguen). Los respaldos deterministas permanecen compatibles; solo pasan por la misma prevención de duplicación cuando se ensamblen con otro fragmento.

No se reescriben `MensajeInicial`, plantillas Meta, preguntas configuradas por campaña ni mensajes históricos.

## 5. Contratos, seguridad y observabilidad

- No cambia `03_Modelo_de_Datos_Cosmos.md`, `04_Contrato_API_REST.md`, endpoints, DTOs, permisos ni el portal.
- No hay flag: la mejora corrige un defecto de calidad transversal y debe aplicar a todas las campañas futuras tras despliegue. Rollback operacional: revertir el cambio de aplicación; no hay datos que deshacer.
- La auditoría existente de redacción conserva acto, resultado técnico y tokens, nunca texto del participante ni texto descartado. Puede añadir el motivo no sensible `puente_duplicado_omitido` al detalle técnico sin registrar la frase.
- El filtrado de rúbrica, promesas y preguntas conserva precedencia; esta guarda no lo sustituye.

## 6. Pruebas y criterios de aceptación

1. La instrucción de evaluación y la del redactor permiten `Queda claro que...`, pero no la presentan como fórmula obligatoria.
2. Un conjunto de salidas simuladas cubre aperturas variadas; al menos una conserva `Queda claro que...` como caso válido y otra inicia directamente con un detalle concreto.
3. Con puente `Ya queda claro el avance.` y cuerpo con la misma oración, se envía una sola vez.
4. Con puente que es prefijo, superconjunto o primera oración equivalente del cuerpo, se conserva el cuerpo y se omite el puente.
5. Con puente distinto, cuerpo y una pregunta válida, se preserva el orden `puente -> cuerpo -> pregunta`.
6. Una pregunta duplicada en un acto que la exige provoca el respaldo; una en un acto opcional se omite sin alterar el estado.
7. Las pruebas actuales de guardrails, fallback, idioma, cupos, I-18, I-20 y P-31 continúan verdes.
8. Una campaña existente y una campaña creada después del despliegue reciben la misma regla en sus próximos turnos; no se modifican mensajes ya enviados.

## 7. Orden de implementación

1. ✅ Añadir las reglas editoriales a `ConstructorMensajesEvaluacion` y `RedactorTurnoConversacional`, con pruebas de los mensajes construidos.
2. ✅ Extraer una ayuda pura y testeable de comparación/filtrado de segmentos en la capa Conversación.
3. ✅ Aplicarla en el único punto de composición de I-20 y en los ensamblajes de respaldo que combinen fragmentos visibles.
4. ✅ Registrar el motivo técnico no sensible cuando se omita un puente.
5. ⏳ Ejecutar pruebas focalizadas y la compuerta local completa (**hecho**); realizar D5 con un banco de ejemplos de campañas reales antes de desplegar (**pendiente**).

### 7.1 Qué quedó implementado (2026-08-13)

- `ConstructorMensajesEvaluacion`: regla `VARIACION DE REDACCION` para `retroalimentacion_usuario` y coaching secuencial sin fórmula fija de apertura.
- `RedactorTurnoConversacional`: regla de variedad en las instrucciones duras del sistema y, cuando el turno lleva retroalimentación validada, indicación estructural de devolver `puente: null` si no aporta una función distinta.
- `FiltroDuplicacionTurno` (nuevo, puro): normaliza (minúsculas, sin tildes, sin puntuación, espacios colapsados), compara por oraciones y por prefijos de palabras, y compone `puente → cuerpo → pregunta` omitiendo lo redundante.
- `PoliticaRedaccionConversacional.ExigePregunta`: define los actos que no pueden quedarse sin pregunta.
- `OrquestadorConversacion`: la guarda corre en el único punto de composición I-20 y en los ensamblajes de respaldo que unen retro + invitación/repregunta; la auditoría de redacción añade `ajuste:<motivo>` (`puente_duplicado_omitido`, `pregunta_duplicada_omitida`, `duplicacion_sin_salida_valida` o `ninguno`), sin texto.

## 8. Cómo probarlo en lenguaje simple

1. Abra una campaña existente y otra creada recientemente, ambas con una pregunta activa.
2. Responda con una idea y luego agregue un detalle para que el sistema haga una pregunta de mejora.
3. Verifique que el mensaje reconoce algo específico o pregunta directamente, sin arrancar siempre de la misma forma.
4. Repita el flujo varias veces: `Queda claro que...` puede aparecer alguna vez, pero no debe dominar todos los mensajes.
5. Revise que nunca aparezca dos veces el mismo `Ya queda claro...` ni la misma idea repetida dentro de un solo mensaje.
6. Si aparece una duplicación, si falta la pregunta de mejora o si el sistema cambia de pregunta/estado sin corresponder, la prueba falla.

## 9. Fuera de alcance

- Modificar mensajes ya enviados o sus documentos históricos.
- Configuración editorial diferente por campaña.
- Usar un segundo LLM para medir similitud semántica.
- Alterar la evaluación, consolidación, rubrica, puntajes, estados o transiciones conversacionales.
