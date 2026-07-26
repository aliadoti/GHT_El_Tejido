# I-18 — Coaching secuencial por idea

> **Origen:** interacción reportada por el usuario el **25-jul-2026**: un mensaje con dos o más ideas
> se segmenta correctamente, pero la respuesta agregada suena mecánica, ofrece cerrar aunque las ideas
> estén por debajo del umbral y no usa la evaluación de cada idea para ayudar a mejorarla.
> **Tipo:** Desarrollo + prompt (aditivo) · **Prioridad:** Alta — afecta la lógica central de la
> conversación · **Ventana:** Sprint 2, próximo ejecutable · **Dependencias:** I-03 (criterio más débil),
> I-06 (segmentación multi-idea), I-17 (madurez, rechazo e inactividad), P-15 (políticas separadas del
> orquestador) y D5 (calibración). Cubre REQ §9/§21/§22/§25/§26, ARQ §4.2/§6/§7/§12/§13; specs base
> `03 §3.3/§3.6/§3.8`, `04 §5.3/§5.8`, `05 §4`, `08`, `09`, `10` y
> `Reglas_Conversacion_y_Participacion.md`.
> **Estado:** especificación cerrada el 25-jul-2026; implementación **TODO**. Todos los campos y
> banderas nuevos nacen apagados o ausentes, por lo que el despliegue no cambia campañas existentes.

## 1. Resultado que se busca

Cuando una respuesta contiene varias ideas, el sistema debe acompañar al participante con **una idea a
la vez**, en el orden en que aparecieron:

1. segmenta y evalúa las ideas iniciales;
2. activa la primera que todavía no alcanza el umbral efectivo;
3. reconoce de forma natural lo que ya está claro y hace **una sola pregunta** sobre el criterio que
   más necesita trabajo;
4. toma la siguiente respuesta como una revisión de esa idea, la persiste y la vuelve a evaluar;
5. repite mientras haya margen y valor para seguir;
6. finaliza la idea cuando alcanza el umbral, el participante decide dejarla así, se agota el máximo
   de repreguntas, vence el tiempo de coaching o se activa un fallback;
7. continúa con la siguiente idea pendiente y, cuando termina la cola, abre la siguiente pregunta de
   la campaña usando el flujo existente.

El coach **no responde por el participante**: no redacta una versión propuesta, no da una respuesta de
ejemplo y no introduce responsables, datos, fechas o soluciones que la persona no haya expresado.

## 2. Problema observado y diagnóstico del build actual

Ejemplo reportado:

- idea 1: sustituir juntas semanales de estatus por un reporte asíncrono;
- idea 2: automatizar aprobaciones de bajo riesgo mediante preaprobación.

La segmentación produce correctamente dos registros. Sin embargo, el camino multi-idea actual:

- descarta para la conversación la retroalimentación y `repreguntaSugerida` de cada evaluación;
- construye una confirmación agregada fija del tipo **“Registramos N ideas”**;
- agrega una invitación genérica y la salida **“si ya te sientes conforme…”** aun cuando la
  calificación no supera el umbral;
- usa un solo `RepreguntasUsadas` para toda la pregunta, no uno por idea;
- interpreta “así está bien” como cierre de la pregunta completa;
- ante expiración/fallback termina el hilo o la pregunta, en lugar de avanzar de manera controlada a
  la siguiente idea.

Por tanto, el problema tiene tres capas:

| Capa | Cambio necesario | Lo que no resuelve por sí sola |
| --- | --- | --- |
| Prompt | voz natural, reconocimiento breve, foco en el criterio débil y una pregunta socrática | no puede conservar una cola ni imponer transiciones auditables |
| Código/orquestación | estado por idea, umbral, límites, avance y persistencia de revisiones | no garantiza por sí solo una formulación humana de buena calidad |
| Modelo/calibración | capacidad suficiente para analizar y formular la pregunta | cambiar de modelo no corrige el estado ni las reglas de cierre |

**Decisión:** implementar prompt + código. Mantener el modelo actual inicialmente y usar D5 para
determinar con evidencia si la calidad del modelo es insuficiente.

## 3. Comportamiento conversacional observable

### 3.1 Primer mensaje con varias ideas

Con I-18 efectivo, se conserva I-06 para segmentar y guardar las N ideas iniciales. Cada idea se evalúa
una vez y la cola mantiene el orden original.

- Las ideas que ya alcanzan el umbral se marcan finalizadas por `umbral`.
- La primera idea por debajo del umbral pasa a `activa`.
- La respuesta al participante se concentra en esa idea: reconocimiento breve + una pregunta.
- No se envía “Registramos N ideas”, un resumen técnico del procesamiento ni calificaciones/rúbrica.
- Puede usarse una transición natural como “Empecemos por la parte de las juntas”, siempre que se
  limite a lo dicho por el participante y no invente contenido.

Si todas las ideas iniciales alcanzan el umbral, se consideran finalizadas y se abre la siguiente
pregunta sin ofrecer una revisión innecesaria.

### 3.2 Coaching de la idea activa

La evaluación identifica el criterio con menor desempeño mediante I-03. La salida al participante:

1. reconoce en una frase corta qué ya se entiende;
2. hace **exactamente una pregunta abierta** que ayude a precisar el criterio más débil;
3. evita mencionar puntajes, nombres de criterios o instrucciones internas;
4. no propone la respuesta, no presenta alternativas cerradas y no incluye ejemplos que puedan
   sustituir la reflexión de la persona;
5. no ofrece cerrar por defecto mientras la idea esté bajo el umbral y todavía pueda mejorar.

La pregunta debe adaptarse a la respuesta y a la pregunta de campaña. No se define una plantilla
literal por criterio: el prompt aprobado de la campaña controla la voz y el LLM redacta la pregunta
dentro de las reglas anteriores.

### 3.3 Respuesta de revisión

Mientras existe una idea activa, el siguiente entrante que no sea una intención explícita de salida:

- se interpreta como revisión de **esa idea**, no se vuelve a segmentar;
- se persiste como una nueva `Respuesta`, enlazada con la raíz y la revisión anterior;
- se evalúa con la misma rúbrica/pregunta y el historial acotado de esa idea;
- sustituye a `respuestaVigenteId` para decidir madurez y Markdown vigentes;
- conserva las versiones anteriores para auditoría.

La revisión que responde a la última repregunta permitida **sí se evalúa**. El límite impide enviar
otra pregunta; no descarta la respuesta final.

### 3.4 Motivos de finalización y avance

| Motivo | Regla | Acción siguiente |
| --- | --- | --- |
| `umbral` | evaluación válida alcanza el umbral efectivo | finalizar y activar la siguiente idea |
| `participante` | “así está bien”, “pasemos a la otra” u otra salida de mejora | conservar la versión vigente y avanzar |
| `rechazo` | “no lo guardes”, “no es eso” u otro rechazo explícito I-17 | degradar solo la idea activa a incubación y avanzar |
| `maxRevisiones` | se evaluó la respuesta a la última pregunta permitida y sigue bajo umbral | finalizar en incubación y avanzar |
| `tiempo` | vence la ventana de coaching de la idea | finalizar en incubación y avanzar |
| `fallback` | no es posible evaluar/formular con seguridad tras los reintentos existentes | dejar trazabilidad segura y avanzar |
| `desactivacion` | se apaga un gate mientras la cola está activa | no enviar más coaching; conservar versiones y avanzar seguro |

“Así está bien” ya no cierra toda la pregunta si quedan ideas pendientes. Cuando se agota la cola, el
orquestador reutiliza el camino existente de cierre de pregunta y apertura de la siguiente.

## 4. Banderas, precedencia y compatibilidad

### 4.1 Activación efectiva

I-18 solo está activo cuando se cumplen las tres condiciones:

```text
Conversacion:CoachingSecuencialIdeas == true
AND campania.configConversacional.coachingSecuencialIdeas == true
AND SegmentacionIdeasEfectiva == true
```

- `Conversacion:CoachingSecuencialIdeas`: kill-switch global, default `true`. En `false` apaga I-18 en
  todas las campañas.
- `configConversacional.coachingSecuencialIdeas`: bandera por campaña, default `false`; ausente en
  documentos viejos equivale a `false`.
- I-06 sigue teniendo sus propios gates. Si I-06 no está efectivo, I-18 no inicia una cola.

Con I-18 inactivo se conserva exactamente el camino multi-idea actual. El despliegue y la migración de
datos no activan campañas.

### 4.2 Umbral

El servidor, no el LLM, decide si la idea puede finalizar por calidad. Se reutiliza el umbral único de
I-17 con precedencia:

```text
pregunta.umbralCierreAnticipado
?? campania.configConversacional.umbralCierreAnticipado
?? Conversacion:UmbralCierreAnticipado
```

El valor persistido/configurado es una fracción `[0,1]` proyectada sobre la escala de la rúbrica. Si la
escala es 0–10, un umbral de 7 se configura como `0.7`. No se codifica el número 7 en el prompt.

En I-18 la comparación con este umbral siempre controla el avance por calidad. El kill-switch
`Conversacion:CierreAnticipadoHabilitado` conserva su significado para el flujo legado; no puede hacer
que el LLM cierre una idea bajo umbral dentro de una cola I-18.

### 4.3 Máximo de repreguntas

`configConversacional.maxRepreguntas` se interpreta **por idea** cuando I-18 está activo. El contador
sube al enviar una pregunta de coaching. Si vale 0, las evaluaciones iniciales se guardan y la cola
avanza sin coaching.

### 4.4 Tiempo por idea

El “tiempo cumplido” se especifica como una ventana propia, distinta de la expiración completa de
sesión de I-17:

- `Conversacion:MinutosCoachingPorIdea`: entero, default `0` (desactivado).
- `configConversacional.minutosCoachingPorIdea`: entero nullable; ausente hereda el global, `<=0`
  desactiva el tiempo para esa campaña.
- El reloj empieza al activar la idea y se reinicia al activar la siguiente; no se reinicia con cada
  mensaje de revisión.

El trabajador de expiración reutiliza el patrón de barrido por campaña de I-17, pero su transición es
por idea. Dentro de la ventana de servicio de WhatsApp envía el turno de la siguiente idea. Fuera de
la ventana no envía texto libre: deja el estado trazable y aplica el cierre seguro definido por el
gateway/política vigente.

## 5. Estado y contratos aditivos

### 5.1 `Campania.configConversacional`

```json
{
  "coachingSecuencialIdeas": false,
  "minutosCoachingPorIdea": null
}
```

Los campos se aceptan y devuelven en el CRUD existente de campañas. No se crea un endpoint nuevo.

### 5.2 `Conversacion.coachingIdeas`

Bloque opcional, ausente en conversaciones históricas y en campañas legacy:

```json
{
  "estado": "activo",
  "respuestaPadreId": "wamid.entrada-inicial",
  "ideaActivaIndice": 1,
  "ideas": [
    {
      "ideaIndice": 1,
      "respuestaRaizId": "resp_idea_1",
      "respuestaVigenteId": "resp_idea_1_rev_1",
      "estado": "activa",
      "motivoFinalizacion": null,
      "repreguntasUsadas": 1,
      "iniciadaEn": "2026-07-25T15:00:00Z",
      "finalizadaEn": null
    }
  ]
}
```

Reglas:

- `ideaActivaIndice` es null cuando no hay una idea activa.
- solo puede existir una idea `activa`;
- estados: `pendiente | activa | finalizada`;
- motivos: `umbral | participante | rechazo | maxRevisiones | tiempo | fallback | desactivacion`;
- el `RepreguntasUsadas` superior de la conversación sigue siendo legado/single-idea; I-18 usa el
  contador de cada elemento;
- las escrituras deben ser idempotentes frente a reintentos del webhook.

### 5.3 Linaje de `Respuesta`

Campos opcionales nuevos:

```json
{
  "ideaRaizId": "resp_idea_1",
  "respuestaAnteriorId": "resp_idea_1",
  "revisionIndice": 1
}
```

- La respuesta inicial usa su propio id como `ideaRaizId`, `respuestaAnteriorId=null` y
  `revisionIndice=0`.
- Cada revisión apunta a la respuesta inmediatamente anterior y conserva la misma raíz.
- `ideaIndice` y `respuestaPadreId` de I-06 conservan su significado; no se reutilizan.
- El DTO de resultado y el detalle de conversación exponen estos campos/bloque de forma opcional.
- El Markdown vigente se compila desde `respuestaVigenteId` e incluye raíz/revisión como metadatos
  auditables; no elimina artefactos históricos.

## 6. Prompt y arbitraje del servidor

Se reutiliza el contrato de evaluación de `08`; no se añade una segunda llamada LLM solo para
conversar. En contexto I-18:

- el system prompt indica rol de coach socrático, una pregunta, foco en el criterio más débil,
  reconocimiento de progreso y prohibición de redactar la solución;
- `repregunta_sugerida` debe ser no vacía cuando la idea está bajo umbral y todavía hay margen;
- `retroalimentacion_enviada` debe poder enviarse directamente, ser breve y no revelar la rúbrica;
- el historial contiene únicamente versiones acotadas de la idea activa y la pregunta original;
- la instrucción no muestra una salida JSON de ejemplo con `recomendacion="cerrar"` como constante.

`recomendacion` del LLM continúa siendo informativa. La transición se decide con estado, umbral,
intención, límites y fallback server-side. `CalculadorEjeDebil` y `FiltroSalidaRubrica` de I-03 siguen
siendo obligatorios y siempre activos.

El prompt aprobado por campaña puede ajustar tono y vocabulario, pero no puede cambiar las reglas de
estado, el umbral, el número de preguntas ni los guardrails.

## 7. Orquestación

### 7.1 Inicio

1. Ejecutar segmentación I-06.
2. Persistir y evaluar cada idea inicial con su `ideaIndice`.
3. Crear el bloque `coachingIdeas` en el mismo guardado lógico/idempotente.
4. Finalizar automáticamente las que ya alcanzan umbral.
5. Activar la primera pendiente bajo umbral y producir su turno de coaching.
6. Si no queda ninguna, continuar a la siguiente pregunta.

### 7.2 Turno de revisión

1. Resolver la conversación y la idea activa.
2. Aplicar idempotencia, cupos, techo de turnos e intenciones.
3. Si es salida/rechazo explícito, finalizar la idea sin una llamada LLM innecesaria.
4. Si es contenido, persistir la revisión enlazada y evaluarla.
5. Actualizar `respuestaVigenteId`, madurez y Markdown.
6. Si alcanza umbral, finalizar y avanzar.
7. Si no alcanza y quedan repreguntas, enviar un nuevo turno de coaching.
8. Si no quedan, finalizar por `maxRevisiones` y avanzar.

El mensaje al avanzar puede enlazar brevemente la conversación con la siguiente idea, pero solo hace
una pregunta y nunca envía N mensajes técnicos.

### 7.3 Fallback y consistencia parcial

Un fallo en una evaluación no debe perder las demás ideas ni cerrar toda la pregunta. Se registra la
idea afectada como `fallback`/incubación, se conserva su respuesta y se avanza. Si falla la
persistencia del estado, no se envía un turno que el servidor no pueda reconstruir.

## 8. Observabilidad, seguridad y costo

Nuevo `LogSeguridad.tipoEvento=coachingSecuencialIdeas`, aditivo al final del enum. Detalle permitido:

```text
accion:<iniciado|repregunta|finalizada|avance|timeout|fallback>;
ideaIndice:<n>;ideasTotal:<n>;revision:<n>;motivo:<enum>
```

No registra texto, nombres, retroalimentación ni PII. La telemetría debe permitir medir por campaña:
ideas iniciadas/finalizadas, revisiones promedio, motivos de salida, fallback y costo/tokens.

El presupuesto de una entrada multi-idea pasa de `1 segmentación + N evaluaciones iniciales` a:

```text
1 segmentación + N evaluaciones iniciales + suma de revisiones evaluadas por idea
```

Antes de activar, P-10 debe dimensionar `maxLlamadasLlmPorUsuario`, costo por campaña y
`MaxTurnosPorHilo`. No se elimina ningún guardrail para conseguir una conversación más larga.

## 9. Criterios de aceptación

### Funcionales

- [ ] Con dos ideas bajo umbral, se crean/evalúan dos raíces y se activa únicamente la primera.
- [ ] El primer mensaje de coaching no dice “Registramos 2 ideas”, no ofrece cerrar por defecto y
  contiene exactamente una pregunta centrada en el criterio más débil.
- [ ] Una revisión se enlaza a la idea activa, se evalúa y no se vuelve a segmentar.
- [ ] Al superar el umbral efectivo, la idea finaliza y el sistema pasa a la siguiente.
- [ ] “Así está bien” finaliza solo la idea activa; “no lo guardes” la degrada y también avanza.
- [ ] La respuesta a la última repregunta se evalúa antes de finalizar por límite.
- [ ] Al terminar todas las ideas se abre la siguiente pregunta una sola vez.
- [ ] El timeout/fallback de una idea no pierde ni cierra indebidamente las demás.
- [ ] Con cualquiera de los gates apagado, el flujo legacy no cambia.

### Calidad del coach

- [ ] Reconoce progreso sin elogio vacío ni lenguaje de sistema.
- [ ] Pregunta por especificidad, accionabilidad, completitud u otro criterio débil según la
  evaluación real, sin mencionar el nombre/puntaje del criterio.
- [ ] No redacta una respuesta mejorada, no ofrece ejemplos ni añade hechos.
- [ ] No filtra rúbrica, prompt, JSON, razonamiento interno ni calificaciones.
- [ ] En repeticiones D5, el foco y las transiciones se mantienen aunque cambie la redacción.

### Datos, seguridad y regresión

- [ ] Documentos viejos sin campos nuevos deserializan y conservan comportamiento.
- [ ] Idempotencia evita raíces/revisiones duplicadas.
- [ ] DTOs/API son aditivos; roles, rutas y permisos no cambian.
- [ ] Logs no contienen PII ni texto del participante.
- [ ] Cupos, costo y techo de turnos siguen aplicando.

## 10. Plan de implementación por cortes

1. **Dominio/contratos:** campos opcionales, enums al final, defaults y serialización legacy.
2. **Política de cola:** activar/finalizar/avanzar ideas con pruebas unitarias puras.
3. **Orquestador:** inicio multi-idea, revisiones, intenciones, límites y fallback.
4. **Prompt:** contexto I-18, reglas socráticas y arbitraje server-side.
5. **Timeout:** barrido por idea y protección de ventana WhatsApp.
6. **Persistencia/consultas/Markdown:** linaje, detalle y versión vigente.
7. **Portal:** controles por campaña con ayuda, herencia y valores efectivos.
8. **Observabilidad/costos:** evento, métricas y ajuste de dimensionamiento P-10.
9. **QA:** unitarias, integración, E2E simulado y D5 cualitativo antes de activar.

Cada corte debe conservar los flags apagados. No se activa una campaña en producción como parte del
merge.

## 11. Cómo probarlo en lenguaje simple

1. En una campaña de pruebas, activa segmentación y coaching secuencial; configura un umbral conocido
   (por ejemplo `0.7` para 7/10), dos repreguntas y un tiempo corto controlado.
2. Responde una pregunta con dos ideas incompletas.
3. Debe aparecer una conversación natural sobre la primera idea, con una sola pregunta y sin decir
   cuántas ideas “registró”.
4. Mejora solo la primera idea. El sistema debe volver a evaluarla y, al alcanzar el umbral o escribir
   “así está bien”, pasar a la segunda.
5. Repite con la segunda. Solo después debe aparecer la siguiente pregunta de la campaña.
6. Comprueba en Resultados que cada idea conserva su respuesta inicial, sus revisiones y la versión
   vigente.

**Indica fallo:** ofrece cerrar una idea bajo umbral sin que la persona lo pida, mezcla las dos ideas,
contesta por la persona, hace más de una pregunta, pierde una revisión o salta la siguiente idea.

## 12. Rollback y degradación

1. Poner `configConversacional.coachingSecuencialIdeas=false` en la campaña afectada.
2. Si el problema es transversal, poner `Conversacion:CoachingSecuencialIdeas=false`.
3. El gate apagado impide crear colas y enviar nuevas preguntas de coaching. Si ya había una activa,
   el siguiente entrante se conserva como revisión `recibida` sin llamada LLM; la cola se finaliza por
   `desactivacion` y avanza de forma segura.
4. No borrar `coachingIdeas` ni revisiones: son trazabilidad válida y los lectores legacy ignoran los
   campos opcionales.
5. El flujo vuelve al comportamiento I-06 anterior sin migración destructiva.

Alternativas descartadas: cambio solo de prompt; cambio solo de modelo; una respuesta agregada para
todas las ideas; una conversación/contenedor físico por idea; reutilizar `respuestaPadreId` para las
revisiones; coaching sin límites deterministas.
