# I-19 — Consolidación progresiva y versión canónica de ideas

> **Origen:** problema identificado por el usuario el **27-jul-2026**: el hilo conversacional sí se
> conserva por participante/campaña/pregunta, pero cada respuesta inicial y cada respuesta a la
> retroalimentación se persisten y califican como versiones independientes. Esto impide que la
> calificación represente la idea completa construida durante la conversación.
> **Tipo:** Desarrollo + prompt + contratos aditivos · **Prioridad:** Crítica — corrige la unidad real
> de evaluación y de resultado · **Ventana:** siguiente iniciativa, solo especificación aprobada;
> implementación pendiente de autorización expresa.
> **Dependencias:** I-03 (coaching sobre el eje débil), I-05 (transparencia mediante paráfrasis), I-06
> (segmentación), I-12 (ideas semilla opcionales), I-17 (umbral y madurez), I-18 (cola por idea),
> P-15 (políticas separadas) y P-23 (Resultados maestro-detalle).
> Cubre REQ §9/§20/§21/§22/§25/§26/§27, ARQ §4.2/§6/§7/§8.3/§12/§13; afecta `03 §3.6/§3.8/§3.9/§3.10`,
> `04 §5.8`, `05 §4`, `08 §3–§6`, `09`, `10`, `11`, `13` y
> `Reglas_Conversacion_y_Participacion.md`.
> **Estado:** decisiones funcionales confirmadas con el usuario el **27-jul-2026**. Este documento y
> los contratos asociados son diseño para revisión; **no hay código de I-19 implementado**.

## 1. Resultado que se busca

La unidad de trabajo deja de ser “el último mensaje recibido” y pasa a ser una **idea consolidada**:
una versión canónica que acumula, corrige y depura lo que el participante ha expresado sobre una idea
de la pregunta inicial.

El comportamiento esperado es:

1. conservar cada mensaje significativo del participante como aporte original e inmutable;
2. construir una paráfrasis acumulada de lo entendido;
3. pedir al participante que confirme o corrija esa paráfrasis;
4. evaluar únicamente la versión consolidada confirmada, completa y vigente;
5. guiar la mejora sobre esa misma versión usando la rúbrica, el prompt aprobado y, cuando existan,
   las ideas semilla de la campaña;
6. repetir el ciclo sin perder ni sustituir silenciosamente lo dicho antes;
7. guardar una sola idea lógica como `madura`, `pendiente` o `rechazada`, conservando todo su historial;
8. permitir que el participante vuelva a una idea anterior para complementarla o corregirla mientras
   la campaña esté activa;
9. enviar toda idea madura a estado **pendiente de curaduría experta**; I-19 no publica, prioriza ni
   implementa automáticamente una idea.

Una frase complementaria como “la responsable sería Ana” no se califica aislada. Se integra con la
idea anterior, el participante confirma el texto completo y solo entonces se vuelve a evaluar.

## 2. Diagnóstico del comportamiento actual

El modelo actual ya cumple la regla “una conversación por participante, campaña y pregunta”. I-18
además conserva `ideaRaizId`, `respuestaAnteriorId`, `revisionIndice` y `respuestaVigenteId`. Sin
embargo:

- cada revisión sigue siendo una `Respuesta` independiente;
- el evaluador recibe el texto del último mensaje como `RespuestaTexto`;
- las versiones anteriores llegan únicamente como historial auxiliar acotado;
- la versión vigente de I-18 significa “última respuesta”, no “idea acumulada”;
- `nivelMadurez`, Resultados y Markdown se calculan/proyectan por respuesta;
- no existe una confirmación obligatoria de lo que el sistema entendió;
- una conversación cerrada no permite hoy volver a una respuesta anterior;
- las ideas semilla de I-12 aún no están implementadas.

Por tanto, el defecto no es la creación de varios hilos físicos. Es la ausencia de una **entidad lógica
canónica** que sea la única unidad de confirmación, evaluación, madurez, consulta y Markdown.

## 3. Conceptos y estados

### 3.1 Conceptos acordados

| Concepto | Significado |
| --- | --- |
| **Aporte** | Mensaje original del participante que agrega, corrige o amplía contenido. Es inmutable y auditable. |
| **Versión propuesta** | Paráfrasis acumulada generada por el sistema y todavía no confirmada. Nunca puede declararse madura. |
| **Idea consolidada** | Versión completa confirmada por el participante. Es la única que se evalúa y se muestra como resultado vigente. |
| **Idea madura** | Idea consolidada cuya evaluación válida alcanza el umbral efectivo. |
| **Idea pendiente** | Idea que no alcanza el umbral o cuyo proceso termina sin evaluación/confirmación suficiente. No se pierde. |
| **Idea rechazada** | Idea que el participante pide explícitamente no guardar. Se conserva para auditoría con acceso administrativo controlado. |
| **Idea validada** | Idea madura aprobada posteriormente por curaduría experta. La transición de curaduría queda fuera de I-19. |

### 3.2 Estados separados

Se separa el progreso conversacional del resultado de negocio:

- `estadoFlujo`:
  - `pendienteConfirmacion`;
  - `enMejora`;
  - `cerrada`;
  - `enRevision` cuando se reabre una idea previa.
- `estadoResultado`:
  - `madura`;
  - `pendiente`;
  - `rechazada`;
  - `null` mientras todavía no existe un resultado cerrado.
- `estadoCuraduria`:
  - `pendiente` únicamente cuando una idea queda madura;
  - `null` para pendientes/rechazadas y mientras el flujo sigue abierto.

I-19 solo asigna `estadoCuraduria=pendiente`. Aprobar, rechazar, editar o publicar desde curaduría será
una iniciativa posterior.

## 4. Flujo conversacional observable

### 4.1 Respuesta inicial

1. El participante responde la pregunta inicial.
2. Si el mensaje contiene varias ideas, I-06 conserva la separación por idea y el orden original.
3. Cada idea obtiene un `ideaId` estable y una cola independiente dentro de la misma pregunta.
4. El texto original se guarda como aporte inicial.
5. El sistema genera una versión propuesta fiel, sin añadir responsables, fechas, datos, beneficios ni
   soluciones no expresadas.
6. El sistema responde, por ejemplo: **“Entendí que propones… ¿Es correcto?”**
7. Todavía no se sella madurez: primero debe existir confirmación del participante.

La confirmación es obligatoria en todas las campañas y no depende del flag histórico de I-05.

### 4.2 Confirmación o corrección

Mientras `estadoFlujo=pendienteConfirmacion`:

- una confirmación inequívoca (“sí”, “correcto”, “eso es”, “así está bien”) confirma la versión
  propuesta;
- una corrección o complemento se guarda como nuevo aporte y genera otra versión propuesta que acumula
  lo confirmado anteriormente más el cambio;
- una frase como “no sería Ana, sería Carlos” **sustituye** el dato contradictorio en la nueva
  paráfrasis, sin borrar el aporte anterior del historial;
- “no lo guardes” tiene prioridad como rechazo explícito y no se interpreta como simple corrección;
- una respuesta ambigua no se usa para adivinar: el sistema pide una aclaración breve.

En `pendienteConfirmacion`, “así está bien” confirma la versión **y** expresa que no desea seguir
mejorándola: se evalúa una vez; si alcanza el umbral queda madura y, si no, queda pendiente por
decisión del participante.

Solo una versión confirmada puede pasar a evaluación. Las versiones propuestas descartadas permanecen
auditables, pero nunca se muestran como resultado vigente.

### 4.3 Evaluación y acompañamiento

Al confirmar:

1. la versión propuesta pasa a ser la versión consolidada vigente;
2. el evaluador recibe **todo el texto consolidado**, no el último aporte;
3. se aplican la rúbrica y el prompt efectivos de la pregunta/campaña;
4. si I-12 tiene ideas semilla configuradas, se incluyen como contexto orientador acotado;
5. se persiste una evaluación vinculada al `ideaId` y a la versión exacta evaluada;
6. el servidor compara la calificación válida con el umbral efectivo de I-17;
7. si alcanza el umbral, la idea queda `madura`, `estadoCuraduria=pendiente` y termina el coaching;
8. si no alcanza el umbral y todavía puede mejorar, I-03/I-18 formulan exactamente una pregunta
   socrática sobre el aspecto más débil, sin revelar rúbrica/puntaje ni proponer la respuesta;
9. el siguiente aporte vuelve al ciclo **consolidar → confirmar → evaluar**.

La retroalimentación y la siguiente pregunta también se basan en la versión consolidada completa.

### 4.4 Finalización por debajo del umbral

Una idea queda `pendiente` cuando no alcanza el umbral y ocurre cualquiera de estos casos:

- el participante confirma la versión pero decide no seguir mejorándola;
- se alcanza el máximo de repreguntas;
- vence el tiempo por idea o la inactividad de sesión;
- falla la consolidación/evaluación y no es seguro continuar;
- se agota un cupo o techo determinístico;
- la campaña deja de estar activa.

Se conserva la última versión confirmada. Si solo existe una propuesta no confirmada, se conserva
marcada como tal junto con los aportes originales y **nunca** se promueve a madura.

### 4.5 Rechazo explícito

“No lo guardes”, “borra esa idea” u otra frase configurada de rechazo:

- cierra solo la idea activa como `rechazada`;
- conserva aportes, versiones y evaluaciones para auditoría;
- retira `estadoCuraduria`;
- no la incluye en resultados maduros ni en salidas posteriores;
- avanza a la siguiente idea/pregunta con un acuse natural.

Una idea rechazada puede reabrirse únicamente por una petición explícita posterior del mismo
participante mientras la campaña siga activa.

### 4.6 Varias ideas y nuevas ideas durante el coaching

Se mantiene una sola idea activa. Si un mensaje contiene contenido para la idea activa y además una
idea nueva:

1. la parte pertinente se incorpora como aporte a la idea activa;
2. la idea nueva obtiene otro `ideaId` y se añade al final de la cola;
3. el sistema no mezcla ambos contenidos en una misma consolidación;
4. trabaja primero la idea activa y luego la nueva.

La clasificación “complemento de la activa vs. idea nueva” es propuesta por el componente de
segmentación/consolidación, pero el servidor impone el máximo de ideas, el orden, la idempotencia y la
regla de una sola activa.

Esta detección de una idea nueva **explícita durante el coaching** es parte siempre activa de I-19,
aunque el flag I-06 de segmentación inicial esté apagado. I-06 continúa gobernando si el primer mensaje
se separa automáticamente en varias ideas.

### 4.7 Revisitar una idea o respuesta anterior

Mientras la campaña esté `activa`, el participante puede escribir “quiero complementar la anterior”,
“quiero volver a la pregunta de productividad” o una intención equivalente.

- Si existe una única candidata inequívoca, se reabre con el mismo `ideaId`.
- “La anterior” selecciona determinísticamente la idea cerrada más reciente.
- Si hay varias candidatas, el sistema presenta una lista breve y numerada de paráfrasis, sin
  calificaciones, y pregunta cuál desea revisar.
- Solo una idea puede estar activa; la actual se conserva en su estado y se devuelve a la cola antes de
  activar la seleccionada.
- La versión confirmada anterior sigue siendo oficial mientras se prepara/confirma la nueva.
- Si la idea estaba pendiente de curaduría, la reapertura suspende ese estado (`estadoCuraduria=null`)
  para impedir que se cure una versión que el participante está cambiando.
- La nueva versión confirmada se vuelve a evaluar completa y puede subir o bajar de `madura` a
  `pendiente` según el umbral vigente.
- El historial registra la reapertura y nunca sobrescribe las versiones anteriores.

Una campaña `cerrada` no acepta cambios del participante. La reapertura administrativa o de curaduría
queda fuera de I-19.

### 4.8 Inactividad, ventana de WhatsApp y fallback

- La inactividad finaliza la idea activa como pendiente usando la última versión confirmada.
- Una versión propuesta sin confirmar se conserva como `confirmacionPendiente`; no se evalúa ni madura.
- Fuera de la ventana de servicio de WhatsApp no se envía texto libre proactivo.
- Si falla el LLM de consolidación, no se inventa una unión: se conserva el aporte y la última versión
  confirmada, se marca `consolidacionPendiente` y se pide reformular cuando sea posible.
- Si falla la evaluación, la idea queda pendiente con trazabilidad; nunca se conserva una madurez
  calculada sobre un texto diferente.
- El fallback de una idea no elimina ni cierra indebidamente las demás.

## 5. Qué es determinístico y qué no

| Parte del flujo | Naturaleza | Regla |
| --- | --- | --- |
| Guardar aportes y enlazarlos con `ideaId` | Determinística | Ids, orden e idempotencia server-side. |
| Proponer la paráfrasis consolidada | No determinística | La redacta el LLM dentro de un esquema; nunca se acepta sin confirmación. |
| Confirmar/rechazar/intención corta | Determinística | Matcher normalizado y acotado según el estado actual. |
| Resolver una referencia inequívoca (“la anterior”, número elegido) | Determinística | Orden e ids persistidos. |
| Separar complemento y nueva idea en texto libre | No determinística | Clasificador/LLM; límites y transición los aplica el servidor. |
| Calificar la idea consolidada | No determinística | LLM con rúbrica/prompt/snapshots; salida estructurada validada. |
| Comparar con el umbral | Determinística | Fórmula I-17, precedencia pregunta → campaña → global. |
| Decidir estado, cola, límites, cierre y curaduría pendiente | Determinística | El servidor dispone; el LLM no cambia estados. |
| Formular retroalimentación/pregunta socrática | No determinística | LLM, filtrado por I-03 y guardrails. |
| Publicar/priorizar/convertir en conocimiento o acta | No aplica en I-19 | Requiere curaduría experta futura. |

## 6. Modelo de datos y contratos aditivos

### 6.1 Principio

`Respuesta` conserva el aporte original. `IdeaConsolidada` es la unidad lógica mutable por punteros y
estado. `VersionIdeaConsolidada` conserva cada paráfrasis como documento inmutable. Los tres tipos
viven en el contenedor `responses`, particionado por `campaniaId`; no se crea otro contenedor.

### 6.2 `IdeaConsolidada`

Documento nuevo:

```json
{
  "id": "idea_resp_wamidabc_1",
  "type": "IdeaConsolidada",
  "campaniaId": "c_2026conv",
  "usuarioId": "u_8f3c",
  "preguntaId": "p_productividad",
  "conversacionId": "conv_...",
  "respuestaRaizId": "resp_wamidabc_1",
  "ideaIndice": 1,
  "versionConfirmadaRef": "idea_resp_wamidabc_1_v2",
  "versionPropuestaRef": null,
  "evaluacionVigenteRef": "eval_...",
  "estadoFlujo": "cerrada",
  "estadoResultado": "madura",
  "nivelMadurez": "maduro",
  "motivoCierre": "umbral",
  "estadoCuraduria": "pendiente",
  "reaperturas": 0,
  "creadaEn": "2026-07-27T14:00:00Z",
  "actualizadaEn": "2026-07-27T14:08:00Z",
  "cerradaEn": "2026-07-27T14:08:00Z"
}
```

Reglas:

- `id` es estable y determinístico a partir de la respuesta raíz;
- `versionConfirmadaRef` solo apunta a una versión confirmada;
- `versionPropuestaRef` puede coexistir temporalmente sin reemplazar la oficial;
- `estadoResultado` solo cambia después de una evaluación válida o de una salida determinística;
- `nivelMadurez` conserva compatibilidad I-17: `madura → maduro`;
  `pendiente|rechazada → incubacion`;
- `estadoCuraduria=pendiente` solo si `estadoResultado=madura`;
- una reapertura pone `estadoFlujo=enRevision` y suspende `estadoCuraduria` hasta reevaluar la nueva
  versión confirmada;
- el resultado puede cambiar al reabrir, pero el historial nunca se reescribe.

### 6.3 `VersionIdeaConsolidada`

Documento inmutable nuevo:

```json
{
  "id": "idea_resp_wamidabc_1_v2",
  "type": "VersionIdeaConsolidada",
  "campaniaId": "c_2026conv",
  "ideaId": "idea_resp_wamidabc_1",
  "numero": 2,
  "versionAnteriorId": "idea_resp_wamidabc_1_v1",
  "texto": "Crear una comunidad de mentores dirigida a empleados nuevos...",
  "aporteIdsAcumulados": ["resp_raiz", "resp_revision_1"],
  "aporteNuevoIds": ["resp_revision_1"],
  "origen": "complemento",
  "estadoConfirmacion": "confirmada",
  "evaluacionRef": "eval_...",
  "promptConsolidacionRef": "pr_consolidar",
  "versionPromptConsolidacion": 1,
  "configLLMSnapshot": { "proveedor": "AzureOpenAI", "modelo": "..." },
  "generadaEn": "2026-07-27T14:06:00Z",
  "confirmadaEn": "2026-07-27T14:07:00Z"
}
```

`estadoConfirmacion` ∈ `propuesta | confirmada | descartada | expirada`. Una versión confirmada no se
edita: cualquier corrección crea la siguiente.

### 6.4 Cambios en `Respuesta` y `Evaluacion`

Campos opcionales aditivos en `Respuesta`:

```json
{
  "ideaId": "idea_resp_wamidabc_1",
  "tipoAporte": "complemento"
}
```

`tipoAporte` ∈ `inicial | complemento | correccion | nuevaIdea`. Las confirmaciones/intenciones cortas
siguen auditadas como `Mensaje`; no se convierten en contenido de la idea.

Campos opcionales aditivos en `Evaluacion`:

```json
{
  "ideaId": "idea_resp_wamidabc_1",
  "versionIdeaId": "idea_resp_wamidabc_1_v2",
  "origenTextoEvaluado": "ideaConsolidada",
  "seedThoughtsSnapshot": {
    "usadas": false,
    "contenido": [],
    "truncadas": false
  }
}
```

Para I-19, `respuestaId` se conserva apuntando a la raíz por compatibilidad. Una evaluación que no
pueda demostrar qué `versionIdeaId` evaluó no puede sellar el estado maduro. El snapshot de semillas
permite reproducir el contexto; vacío confirma que la evaluación funcionó sin ese insumo.

### 6.5 Estado I-18

Cada elemento de `Conversacion.coachingIdeas` añade `ideaId` y sustituye conceptualmente
`respuestaVigenteId` por `versionIdeaVigenteId` para decisiones de I-19. El campo anterior permanece
para lectores legacy y apunta al último aporte, no a la unidad evaluada.

## 7. Contrato del LLM

### 7.1 Consolidación

Se añade un prompt versionado `tipoPrompt=consolidar`. Recibe como datos delimitados:

- pregunta original;
- versión confirmada anterior, si existe;
- nuevo aporte del participante;
- ideas ya separadas de la cola para evitar mezclarlas.

Salida estructurada mínima:

```json
{
  "idea_consolidada_propuesta": "string",
  "tipo_cambio": "inicial | complemento | correccion",
  "nuevas_ideas": [
    { "texto": "string" }
  ],
  "requiere_aclaracion": false,
  "pregunta_aclaracion": null,
  "anomalia_seguridad": false
}
```

Validaciones:

- la propuesta no puede estar vacía ni exceder el límite configurado;
- debe conservar los hechos confirmados salvo corrección explícita;
- no puede añadir datos ausentes;
- `nuevas_ideas` respeta `MaxIdeasPorMensaje`;
- una salida inválida no se usa: se aplica fallback de §4.8;
- el servidor asigna ids, estados y orden; el LLM solo propone texto/clasificación.

### 7.2 Evaluación

`08 §3.2` cambia para I-19:

```text
CONTENIDO_A_EVALUAR = VersionIdeaConsolidada.texto confirmada
```

El historial puede incluir aportes/versiones para contexto, pero **no sustituye** el texto canónico.
La evaluación, retroalimentación, eje débil, paráfrasis visible y recomendación se generan sobre la
misma versión exacta.

### 7.3 Ideas semilla I-12

I-19 no depende de que GHT haya entregado semillas:

- vacío/ausente → flujo idéntico sin semillas;
- configuradas → bloque orientador separado, versionado y acotado;
- las semillas ayudan a interpretar relevancia y formular coaching, pero **no crean un criterio oculto
  de calificación** ni reemplazan la rúbrica;
- no se inventan seeds provisionales;
- el snapshot de evaluación registra si se usó el bloque y su versión, sin copiar contenido sensible a
  telemetría.

## 8. Orquestación y precedencias

### 8.1 Secuencia obligatoria

```text
aporte
  → persistir aporte
  → consolidar propuesta
  → pedir confirmación
  → confirmar/corregir
  → evaluar versión confirmada completa
  → comparar umbral
  → madura / pregunta socrática / pendiente / rechazada
```

No se permite evaluar primero el último mensaje y “arreglar” después el resultado.

### 8.2 Prioridad de intenciones

Con una versión propuesta:

1. rechazo explícito de guardado;
2. corrección/complemento con contenido;
3. confirmación inequívoca;
4. petición de revisitar otra idea;
5. aclaración por ambigüedad.

Con una idea en mejora:

1. rechazo;
2. petición de revisitar;
3. salida “así está bien”;
4. aporte de mejora/nueva idea.

Las intenciones determinísticas solo aplican en el estado que les da sentido; un “no” aislado fuera de
confirmación/rechazo no debe cerrar por accidente.

### 8.3 Umbral

Se conserva I-17:

```text
pregunta.umbralCierreAnticipado
?? campania.configConversacional.umbralCierreAnticipado
?? Conversacion:UmbralCierreAnticipado
```

El LLM no decide madurez. El servidor compara la calificación de la versión confirmada con la escala y
el umbral efectivo.

## 9. API y portal de Resultados

### 9.1 API

Rutas aditivas:

| Método | Ruta | Uso |
| --- | --- | --- |
| `GET` | `/api/admin/ideas` | Lista una fila por idea lógica; filtros por campaña, pregunta, participante, `estadoResultado`, `estadoFlujo` y `estadoCuraduria`. |
| `GET` | `/api/admin/ideas/{id}` | Devuelve versión confirmada vigente, propuesta pendiente si aplica, evaluación vigente, aportes y versiones. |

`GET /api/admin/respuestas` y sus detalles permanecen para auditoría/compatibilidad, pero dejan de ser
la fuente principal de la pantalla Resultados cuando la idea tiene `ideaId`.

La API no permite aprobar curaduría en I-19.

### 9.2 Resultados

La pantalla muestra:

- una fila por `IdeaConsolidada`, no una fila por aporte/revisión;
- texto consolidado vigente;
- `Madura`, `Pendiente` o `Rechazada`;
- marca `En revisión` o `Pendiente de confirmación` si corresponde;
- calificación de la versión exacta vigente;
- `Pendiente de curaduría` para maduras;
- detalle expandible con aportes originales, versiones propuestas/confirmadas, evaluaciones y motivos
  de cierre.

Las ideas legacy sin `ideaId` siguen visibles mediante el adaptador actual y se identifican como
“resultado histórico”, sin migración destructiva.

## 10. Markdown

Se genera un único artefacto canónico por `ideaId` para maduras, pendientes y rechazadas:

```text
campanias/{campaniaId}/idea/{ideaId}.md
```

Incluye:

- texto consolidado vigente y estado de confirmación;
- estado de resultado y motivo de cierre;
- estado de curaduría;
- pregunta, autoría/tags permitidos y fecha;
- calificación/evaluación de la versión vigente, si existe;
- referencias a `ideaId`, versión, aportes y evaluación;
- historial resumido de versiones sin ocultar que una propuesta no fue confirmada.

Cada regeneración incrementa `version`. Los Markdown históricos por `respuestaId` se conservan; para
ideas I-19, el artefacto canónico por `ideaId` es el que muestra Resultados.

## 11. Activación, compatibilidad y rollback

### 11.1 Activación acordada

- No existe flag por campaña.
- I-19 se activa para **todas las campañas** al desplegar la implementación.
- `Conversacion:ConsolidacionProgresivaHabilitada` es únicamente un kill-switch global de emergencia y
  nace en `true`; no se expone en el portal.
- I-06/I-18 conservan sus gates para la segmentación automática inicial, pero toda idea que procese el
  sistema usa consolidación y confirmación. Excepción acordada: una idea nueva explícita detectada
  durante el coaching se encola aunque I-06 esté apagado.

### 11.2 Datos existentes

- No hay migración masiva ni reescritura de resultados históricos.
- Una conversación nueva crea `IdeaConsolidada` desde el primer aporte.
- Si llega un nuevo mensaje a una conversación I-18 abierta, el sistema reconstruye una propuesta desde
  sus aportes enlazados y pide confirmación antes de volver a evaluar.
- Una conversación histórica cerrada solo se convierte cuando el participante solicita revisitarla
  mientras la campaña está activa.

### 11.3 Rollback

En una emergencia, poner `Conversacion:ConsolidacionProgresivaHabilitada=false`:

- impide crear nuevas versiones consolidadas;
- conserva aportes, ideas, versiones y evaluaciones ya creadas;
- no borra ni degrada resultados maduros;
- los nuevos aportes se guardan como pendientes y el sistema usa un mensaje neutro, evitando volver a
  calificarlos aisladamente como si fueran la idea completa;
- permite corregir/desplegar sin migración destructiva.

El rollback no restaura silenciosamente el defecto anterior.

## 12. Seguridad, observabilidad y costo

### 12.1 Seguridad

- aportes, versiones previas y semillas se delimitan como datos/instrucciones según `08`/I-12;
- no se mezclan aportes de ideas distintas ni de participantes distintos;
- se conservan los filtros I-03 contra fuga de rúbrica;
- Resultados respeta roles actuales; versiones rechazadas no aparecen en vistas maduras;
- logs técnicos no contienen texto, paráfrasis, nombres ni PII;
- el texto consolidado es dato no confiable y nunca ejecuta acciones.

### 12.2 Observabilidad

Nuevo evento `LogSeguridad.tipoEvento=consolidacionProgresivaIdeas` con detalles permitidos:

```text
accion:<propuesta|confirmada|corregida|evaluada|reabierta|cerrada|fallback>;
ideaIndice:<n>;version:<n>;estado:<enum>;motivo:<enum>
```

Métricas por campaña:

- propuestas por idea;
- correcciones antes de confirmar;
- tasa de confirmación;
- revisiones y reaperturas;
- cambio de calificación por versión;
- maduras/pendientes/rechazadas;
- fallos de consolidación/evaluación;
- tokens y latencia separados entre consolidación y evaluación.

### 12.3 Costo

Cada mejora puede consumir:

```text
1 llamada de consolidación
+ 1 llamada de evaluación después de confirmar
+ segmentación cuando haya varias ideas
```

Una corrección antes de confirmar repite consolidación, pero no evaluación. P-10 debe contar ambas
clases de llamadas y aplicar cupos sin saltarse la conservación del aporte.

## 13. Criterios de aceptación

### 13.1 Consolidación y confirmación

- [ ] La respuesta inicial crea una idea estable y una propuesta parafraseada.
- [ ] Ninguna propuesta se evalúa o declara madura antes de confirmación.
- [ ] Una corrección reemplaza el dato contradictorio en la nueva versión sin borrar historial.
- [ ] Cada evaluación referencia el `ideaId` y la versión consolidada exacta.
- [ ] La calificación, retroalimentación y siguiente pregunta usan el texto consolidado completo.
- [ ] El LLM no inventa contenido; una paráfrasis incorrecta puede corregirse.

### 13.2 Estados

- [ ] Superar el umbral produce `madura` + `estadoCuraduria=pendiente`.
- [ ] No superar el umbral al terminar produce `pendiente`.
- [ ] “No lo guardes” produce `rechazada`, conserva auditoría y no entra a curaduría.
- [ ] Fallback/inactividad con propuesta sin confirmar nunca produce madurez.
- [ ] No existe publicación, priorización o incorporación automática a conocimiento/acta.

### 13.3 Multi-idea y reapertura

- [ ] Varias ideas conservan `ideaId` y consolidación separados y se trabajan una por una.
- [ ] Un mensaje con complemento + idea nueva actualiza la activa y encola la nueva.
- [ ] “La anterior” reabre la idea cerrada más reciente; si hay ambigüedad se pide elegir.
- [ ] Una idea madura reabierta se vuelve a evaluar y puede quedar pendiente.
- [ ] Una campaña cerrada no admite cambios del participante.

### 13.4 Datos, API, portal y Markdown

- [ ] Aportes y versiones son auditables e inmutables.
- [ ] Resultados muestra una fila por idea lógica y no duplica revisiones.
- [ ] El detalle permite reconstruir aportes → versiones → confirmaciones → evaluaciones.
- [ ] El Markdown canónico usa `ideaId`, la versión vigente y el estado correcto.
- [ ] Lectores legacy y resultados históricos siguen funcionando.
- [ ] Reinicio P-03 elimina también ideas/versiones/Markdown I-19 dentro del mismo alcance autorizado.

### 13.5 Calidad, seguridad y regresión

- [ ] Idempotencia evita duplicar aportes, versiones o evaluaciones ante reintentos.
- [ ] Logs no contienen texto ni PII.
- [ ] Seeds vacías no cambian el flujo; configuradas se usan sin crear criterios ocultos.
- [ ] Cupos, timeout, ventana WhatsApp y máximo de ideas/turnos permanecen efectivos.
- [ ] La suite prueba explícitamente que el último mensaje aislado no es el texto evaluado.
- [ ] D5/UAT comparan exactitud de consolidación, calidad del coaching y costo antes del despliegue.

## 14. Cómo probarlo en lenguaje simple

1. Responde una pregunta con una idea incompleta.
2. El sistema debe devolverte, en sus propias palabras, lo que entendió y pedirte confirmación.
3. Corrige un dato. Debe mostrarte otra versión completa con la corrección, sin olvidar lo anterior.
4. Confírmala. Solo entonces debe evaluarla y hacer una pregunta de mejora si sigue incompleta.
5. Responde únicamente con el dato faltante; la siguiente paráfrasis debe contener la idea anterior más
   ese dato, no solo tu último mensaje.
6. Continúa hasta superar el nivel esperado o escribe “así está bien”. En Resultados debe existir una
   sola idea, madura o pendiente, con todo el historial dentro.
7. Pide “quiero complementar la anterior”, corrígela y confirma. Debe mantener el mismo resultado
   lógico, crear una nueva versión y volver a calificarla.

**Indica fallo:** el sistema califica “la responsable sería Ana” como idea independiente; olvida una
parte ya confirmada; inventa datos; no pide confirmación; muestra cada revisión como otra idea; conserva
una calificación de una versión anterior; o publica una idea sin curaduría.

## 15. Estado de implementación y plan de continuación

**Estado al 2026-07-27:** implementación autorizada y WIP local. Los pasos 1–6 están cubiertos: el hilo
de una idea y la cola I-18/multi-idea recorren el mismo ciclo canónico con una sola idea activa a la
vez, una idea nueva explícita se encola aparte sin mezclarse con la activa y una idea cerrada puede
reabrirse conservando su historial. La última validación fue: `dotnet build -c Release -warnaserror`,
`dotnet test -c Release --no-build --filter "Category!=Calibracion"` (**512: 459 unitarias + 53
integración**) y `dotnet format --verify-no-changes --no-restore`, todas verdes (commits `748870f`,
`4e31f94`, `62240b9` y `401d9dd`, sin push).

**Próximo corte ejecutable (paso 9 — observabilidad y cupos, §12.2/§12.3):** el paso 8 (seeds I-12)
sigue **BLOCKED** por el insumo externo y el flujo ya degrada limpio con `seedThoughts` vacío, así que
el siguiente ítem ejecutable es el 9. Falta (1) el evento
`LogSeguridad.tipoEvento=consolidacionProgresivaIdeas` —aditivo al final del enum— con el detalle
permitido de §12.2, sin texto ni PII, emitido en cada transición de la idea; y (2) **contar las llamadas
de consolidación** en los cupos de P-10: hoy el contador deriva solo de las evaluaciones, así que una
corrección repetida consume LLM sin tocar el cupo. Al hacerlo hay que **decidir y registrar** si se
persiste el uso de tokens de la consolidación (la versión ya guarda snapshot de config) o si basta un
contador derivado. No eliminar lectores legacy y no activar/desplegar el cambio.

**Pendientes conocidos de los pasos 5/5b/6** (registrados, no bloquean el corte):

- la reapertura opera sobre ideas del **hilo actual**; volver a la idea de **otra pregunta** (“la
  pregunta de productividad”, §4.7) exige reabrir una `Conversacion` cerrada y queda fuera del corte;

- el cierre por inactividad (`ServicioExpiracionConversaciones`) finaliza el turno en la cola pero no
  cierra todavía el documento `IdeaConsolidada` como `pendiente` (§4.8);
- las campañas con I-06 activo y coaching I-18 apagado conservan su ruta histórica de evaluación por
  idea segmentada, sin confirmación previa;
- `requiereAclaracion` del consolidador aún no genera la pregunta breve de aclaración (§4.2).

Antes de cambiar código, quien retome debe leer `AVANCES.md`, `TODO.md`, `SUPUESTOS.md`, esta sección,
`I-18_Coaching_Secuencial_Por_Idea.md` y `Reglas_Conversacion_y_Participacion.md`. Mantener
`.obsidian/workspace.json` fuera del alcance: es un cambio ajeno ya presente en el árbol de trabajo.

1. **[Hecho local] Dominio y persistencia:** `IdeaConsolidada`, versiones, estados, repositorios e idempotencia.
2. **[Hecho local] Contratos primero:** cambios aditivos en `03`/`04`/`08`/`09`; DTOs legacy preservados.
3. **[Hecho local] Consolidador:** puerto, prompt versionado, esquema de salida, validación y fallback.
4. **[Hecho local] Orquestador de una idea:** confirmación/corrección, evaluación de versión completa y prioridades de intención.
5. **[Hecho local] I-18/multi-idea:** propuesta, confirmación y evaluación por cada idea, con una sola activa y confirmación de la siguiente al cerrar la anterior.
5b. **[Hecho local] Complemento + idea nueva (§4.6):** la idea nueva obtiene su propio `ideaId`, aporte y propuesta, se encola al final (tope/orden/idempotencia server-side) y espera turno sin mezclarse con la activa; sin cola I-18, el hilo la atiende por orden de llegada al cerrar la activa.
6. **[Hecho local] Reapertura:** “la anterior” determinista, lista numerada al desambiguar, mismo `ideaId`, curaduría suspendida y ninguna versión sobrescrita; solo dentro del hilo actual y con la campaña activa.
7. **[Hecho local] Markdown/API/Resultados:** artefacto canónico por idea (`tipoArtefacto=idea`, ruta `campanias/{campaniaId}/idea/{ideaId}.md`, regenerado al evaluar y al cerrar), rutas `GET /api/admin/ideas` y `/ideas/{id}`, y pantalla de Resultados con **una fila por idea** —estado, marcas de flujo y curaduría, historial de aportes y versiones, Markdown por `ideaRef`— conservando los aportes sin `ideaId` como “resultado histórico”.
8. **[Pendiente] Seeds:** consumir I-12 cuando estén configuradas; degradación vacía.
9. **[Pendiente] Observabilidad/cupos:** métricas y conteo de llamadas de consolidación.
10. **[Pendiente] QA final:** unitarias, integración, regresión, E2E simulado, D5 y UAT.

No se activa ni despliega código como parte de la aprobación de esta especificación.

## 16. Visión de mediano plazo y fuera de alcance

El modelo estable `ideaId + versiones + evaluación + procedencia + estadoCuraduria` prepara tres usos:

| Caso de uso | Salida posterior a I-19 |
| --- | --- |
| Crowdsourcing de ideas | Curaduría prioriza ideas maduras y decide cuáles pasan a implementación. |
| Gestión del conocimiento | Curaduría valida qué ideas se incorporan al repositorio organizacional. |
| Actas de reuniones “esteroides” | Curaduría revisa el resumen depurado/evaluado antes de publicarlo como acta. |

Fuera de I-19:

- pantalla, roles y transiciones de curaduría;
- publicación o sincronización con repositorios de conocimiento;
- priorización/portafolio de implementación;
- generación completa de actas;
- parametrizar el tipo de campaña por caso de uso;
- edición experta de la idea consolidada;
- migración masiva de datos históricos.

## 17. Decisiones confirmadas con el usuario

El **27-jul-2026** se confirmó:

1. reformular el defecto como ausencia de una idea consolidada;
2. conservar aportes originales y crear versión canónica acumulada;
3. parafrasear y pedir confirmación antes de aceptar lo entendido;
4. evaluar y retroalimentar siempre la versión completa;
5. conservar versiones anteriores como historial auditable;
6. madurez solo al superar el umbral;
7. cierre bajo umbral = pendiente;
8. rechazo explícito = rechazada, no borrada;
9. permitir corrección de la paráfrasis;
10. consolidación separada por idea y coaching una por una;
11. complemento de activa + idea nueva en cola;
12. funcionar sin seeds y consumirlas cuando existan;
13. preparar, no implementar, curaduría;
14. ninguna idea madura pasa automáticamente a un destino: siempre queda pendiente de curaduría;
15. no parametrizar aún los tres casos de uso;
16. una fila de Resultados por idea con historial;
17. un Markdown canónico por idea;
18. activación inmediata para todas las campañas, sin opt-in por campaña.
