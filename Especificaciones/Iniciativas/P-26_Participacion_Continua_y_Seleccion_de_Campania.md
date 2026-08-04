# P-26 — Participación continua y selección de campaña/pregunta

**Estado:** **IMPLEMENTADA localmente (6/6, 2026-07-31); activación operativa pendiente.** El
interruptor por campaña nace apagado y requiere D5, UAT, revisión de costo y el cierre de P-27 antes
de activarse en UAT o producción.
**Fecha de decisión:** 2026-07-29.  
**Áreas afectadas:** dominio de campañas, resolución de participante, webhook, orquestador,
persistencia Cosmos, guardrails, API administrativa, portal y pruebas E2E simuladas.  
**Contratos relacionados:** `03 §2/§3.3/§3.6/§3.6.1`, `04 §5.3`, `05 §4.3/§4.4.3`,
`06 §3`, `07 §2`, `10 §2/§6`, `11 §6`, `13 §2–§6`, `Reglas §2.10/§3`.

---

## 1. Resumen ejecutivo

Hoy un participante completa las preguntas de una campaña una sola vez. Cuando todas sus
conversaciones están cerradas, un aporte posterior no puede convertirse en una idea nueva. Además,
si el mismo número está asociado a varias campañas activas, el sistema escoge una de ellas sin pedir
confirmación.

P-26 añade un interruptor por campaña, `participacionContinua`, para que un participante pueda
madurar una idea, terminarla y regresar después —incluso otro día— con una idea nueva. Cada nueva idea
conserva su propio hilo, versión consolidada, evaluación, estado de madurez, Markdown e ingreso a
curaduría. El historial anterior no se reabre ni se mezcla automáticamente.

Cuando hay más de una campaña posible, el sistema conserva el aporte original, presenta una lista
numerada de campañas y, después de la selección, procesa automáticamente ese aporte. Si la campaña
tiene varias preguntas, aplica el mismo criterio para seleccionar la pregunta. Mientras una idea está
en coaching, sus respuestas continúan en ese mismo contexto sin volver a preguntar la campaña.

`participacionContinua` **no reemplaza el estado de la campaña**: solo una campaña `activa` puede
recibir aportes. Una campaña `cerrada`, `archivada` o `borrador` nunca aparece en la lista ni acepta
nuevos mensajes.

---

## 2. Decisiones confirmadas

| Decisión | Regla aprobada |
|---|---|
| Campañas que reciben aportes | Únicamente campañas con estado `activa`. |
| Interruptor apagado | `participacionContinua=false` conserva exactamente el flujo actual de una sola participación. |
| Interruptor encendido | Después de cerrar una idea, un aporte sustantivo posterior puede abrir una idea lógica nueva. |
| Varias preguntas | Para cada ciclo nuevo se permite escoger la pregunta a la que pertenece la idea. |
| Coaching en curso | Las respuestas se enrutan automáticamente a la idea activa; no se repite el menú. |
| Varias campañas posibles | Se muestra una lista numerada; se acepta número o nombre exacto no ambiguo. |
| Aporte antes de elegir | Se conserva y se procesa automáticamente después de escoger campaña y pregunta. |
| Campaña elegible | Está activa, el participante está asociado y habilitado, y tiene trabajo pendiente o `participacionContinua=true`. |
| Idea posterior | Es una idea lógica nueva, salvo que el participante pida explícitamente complementar o revisitar una anterior. |
| Cambio del interruptor | Se puede editar al crear o configurar la campaña. Al apagarlo, una idea ya activa puede terminar; no se abren ideas nuevas. |
| Límites por participante | En campaña continua se calculan en una ventana móvil de 24 horas. |
| Presupuesto de campaña | El presupuesto total de tokens sigue siendo acumulado durante toda la campaña. |
| Vigencia de selección | La selección/afinidad dura mientras se trabaja la idea y como máximo 24 horas. |
| Auditoría | Si la selección vence, el aporte original no se procesa, pero permanece auditable. |
| Compatibilidad | Campo ausente en campañas históricas equivale a `false`. |

---

## 3. Alcance

### 3.1 Incluido

- Interruptor `configConversacional.participacionContinua` en creación y edición de campaña.
- Elegibilidad de múltiples campañas activas por participante.
- Menú determinista de campaña y, cuando corresponda, de pregunta.
- Conservación auditable del aporte recibido antes de la selección.
- Afinidad temporal con la campaña/pregunta/conversación seleccionada.
- Nuevo ciclo conversacional por idea posterior, sin modificar conversaciones cerradas.
- Reutilización de I-19/P-25 para consolidar y evaluar siempre la idea completa.
- Reutilización de I-18 para trabajar una idea a la vez cuando un mensaje contiene varias.
- Reapertura explícita de una idea previa, sin crear una idea distinta.
- Ventana móvil de 24 horas para cupos por participante en campañas continuas.
- Observabilidad, idempotencia, control de concurrencia y pruebas simuladas sin WhatsApp real.

### 3.2 Fuera de alcance

- Recibir aportes en campañas cerradas, archivadas o en borrador.
- Curaduría automática o publicación directa: toda idea madura continúa en curaduría experta.
- Integración final con repositorio de conocimiento, priorización/implementación o actas.
- Recordatorios proactivos fuera de la ventana de servicio de WhatsApp.
- Un LLM que elija la campaña, la pregunta o el estado del flujo.
- El saludo/elección humana cuando el participante vuelve sin un aporte sustantivo: es el vacío
  específico de P-28. P-26 ya procesa directamente un aporte sustantivo nuevo.
- El aviso humano al expirar una conversación: el cierre determinístico ya existe en I-17/I-19 y el
  mensaje de pausa es el único vacío de P-29.
- La lista de ideas de ciclos históricos para escoger cualquiera de ellas: P-26 conserva la
  reapertura explícita vigente; P-30 la amplía sin duplicarla.

### 3.3 Preparación para la visión de mediano plazo

Cada ciclo termina en una idea independiente y curable, lo que permite usar el mismo modelo en:

1. **Crowdsourcing de ideas:** las maduras pasan a priorización e implementación.
2. **Gestión del conocimiento:** las validadas pueden añadirse a un repositorio organizacional.
3. **Actas de reuniones “esteroides”:** las ideas capturadas se maduran y luego alimentan un resumen
   depurado.

P-26 solo deja referencias y estados preparados. En los tres casos la curaduría experta sigue siendo
obligatoria.

---

## 4. Conceptos funcionales

| Concepto | Significado |
|---|---|
| Campaña activa | Estado administrativo que permite interacción. Es la condición obligatoria. |
| Participación continua | Permiso para iniciar ideas nuevas después de haber completado el recorrido anterior. |
| Ciclo de participación | Hilo independiente creado para trabajar una idea nueva en una campaña y pregunta. |
| Idea activa | Idea que actualmente recibe coaching y complementos. |
| Afinidad | Selección temporal de campaña, pregunta y conversación a la que se enrutan las respuestas siguientes. |
| Aporte pendiente de enrutamiento | Mensaje original conservado mientras el participante elige campaña y/o pregunta. |
| Trabajo pendiente | Conversación abierta, idea en coaching o pregunta activa que el participante aún no ha completado. |
| Reapertura | Solicitud explícita de complementar una idea cerrada conservando su mismo `ideaId`. |

Una campaña puede estar `activa` y tener `participacionContinua=false`: admite terminar lo pendiente,
pero no repetir preguntas ya completadas. También puede estar `activa` y tener
`participacionContinua=true`: admite ciclos nuevos incluso cuando todo el recorrido anterior terminó.

---

## 5. Flujo funcional

### 5.1 Orden determinista de resolución

Ante cada mensaje entrante válido, el servidor aplica este orden:

1. Normaliza el número y valida usuario/rol/estado.
2. Si existe una afinidad vigente hacia una conversación abierta y el mensaje no pide cambiar de
   campaña, continúa esa idea automáticamente.
3. Si el participante pide explícitamente “otra campaña”, “cambiar de campaña” o equivalente, se
   suspende la afinidad actual sin cerrar la idea y se recalculan opciones.
4. Construye la lista de campañas elegibles en ese instante.
5. Si no hay opciones, responde el rechazo neutral vigente.
6. Si hay una sola opción, la selecciona sin menú.
7. Si hay varias, conserva el aporte y solicita seleccionar campaña.
8. Dentro de la campaña elegida:
   - si hay una sola pregunta elegible, la selecciona;
   - si hay varias, solicita seleccionar pregunta.
9. Revalida campaña, asociación y pregunta antes de crear/reabrir el hilo.
10. Procesa el aporte original exactamente una vez en la conversación resuelta.

Ninguna de estas decisiones se delega al LLM.

### 5.2 Campañas elegibles

Una campaña aparece en las opciones solo si cumple **todas** estas condiciones:

- `Campania.estado == activa`;
- `Usuario.estado == activo` y `Usuario.rol == participante`;
- existe `ParticipanteCampania` del usuario con `estado == activo`;
- existe al menos una pregunta activa; y
- el participante tiene trabajo pendiente **o**
  `Campania.configConversacional.participacionContinua == true`.

Campo ausente se interpreta como `false`. Los nombres de campañas no autorizadas nunca se revelan.

### 5.3 Selección de campaña

Mensaje de referencia, generado por el servidor:

```text
¿A cuál campaña corresponde tu aporte?
1. Innovación comercial
2. Convención de gerentes

Responde con el número o con el nombre de la campaña.
```

Reglas:

- acepta un número válido de la lista vigente;
- acepta el nombre completo, normalizado y no ambiguo;
- no acepta coincidencias parciales ambiguas;
- una opción inválida conserva el aporte y vuelve a pedir la selección;
- cada intento queda auditado sin copiar el texto del aporte a logs técnicos;
- al seleccionar se revalida la elegibilidad, porque el estado pudo cambiar desde que se ofreció.

### 5.4 Selección de pregunta

En una campaña con varias preguntas elegibles:

```text
¿Sobre cuál pregunta quieres aportar?
1. ¿Cómo mejoraríamos la experiencia del cliente?
2. ¿Cómo reduciríamos tiempos internos?
```

Se acepta el número o el texto completo no ambiguo. Para una campaña continua que ya completó todas
sus preguntas, todas las preguntas activas vuelven a estar disponibles. Para una campaña no continua,
solo aparecen preguntas pendientes.

### 5.5 Conservación y procesamiento del aporte original

- El primer mensaje sustantivo se persiste como `EnrutamientoAporte` antes de mostrar un menú.
- El documento conserva el `whatsappMessageId`, texto, fecha, número de destino y opciones ofrecidas.
- La respuesta de selección **no sustituye** el aporte.
- Al quedar resuelta campaña y pregunta, el sistema crea o identifica el ciclo y entrega internamente
  el aporte original al orquestador, sin simular un segundo webhook y sin volver a pasar por la marca
  de deduplicación.
- El estado pasa de `listo` a `enIdea` solo después de que el aporte se haya persistido en la
  conversación; `procesadoEn` marca ese instante.
- Reintentos de Meta o reintentos internos no pueden crear dos ciclos ni dos ideas para el mismo
  `whatsappMessageId`.
- Si transcurren 24 horas sin selección, el estado pasa a `expirado`. El texto permanece para
  auditoría, pero no se procesa automáticamente en una selección posterior.

Mientras hay una selección pendiente, un mensaje que no sea una opción válida se conserva como
intento de selección y se vuelve a mostrar ayuda; no se adivina campaña ni se pierde el aporte raíz.

### 5.6 Afinidad durante el coaching

Al resolver campaña/pregunta, el `EnrutamientoAporte` queda temporalmente en estado `enIdea` y apunta a
la conversación. Mientras esa conversación siga abierta y no hayan pasado 24 horas:

- toda respuesta a retroalimentación se procesa allí;
- no se vuelve a listar campañas ni preguntas;
- la afinidad más reciente prevalece si el participante cambió explícitamente de campaña;
- cambiar de campaña no cierra ni rechaza la idea suspendida;
- al volver explícitamente, se revalida que la campaña siga activa.

Cuando la idea termina, el enrutamiento se marca `completado`. El siguiente aporte sustantivo vuelve a
resolver campaña/pregunta y, si la campaña es continua, crea un ciclo nuevo.

### 5.7 Nuevo ciclo e independencia del historial

Cada nueva participación posterior:

- crea una `Conversacion` distinta para la misma combinación usuario/campaña/pregunta;
- recibe un `cicloParticipacion` mayor a 1;
- conserva la conversación anterior cerrada e inmutable;
- crea un `ideaId` diferente;
- consolida solo los aportes del ciclo nuevo;
- evalúa la versión consolidada completa del ciclo;
- genera Markdown y curaduría propios.

Los documentos históricos sin `cicloParticipacion` equivalen al ciclo `1`. El identificador de un
ciclo nuevo se deriva de forma determinista de campaña, usuario, pregunta y mensaje raíz para que un
reintento no lo duplique. `cicloParticipacion` sirve para ordenar/mostrar, no como única garantía de
idempotencia.

Si un mensaje raíz contiene varias ideas y están activos I-06/I-18, esas ideas forman la cola del
mismo ciclo y se trabajan una por una. El siguiente aporte recibido después de cerrar la cola crea un
ciclo nuevo.

### 5.8 Reapertura vigente de una idea anterior

Frases explícitas como “quiero complementar la anterior” no crean una idea nueva:

1. se resuelve primero el alcance vigente de campaña y pregunta;
2. I-19 reabre la idea candidata conservando su `ideaId`;
3. si hay varias candidatas, se usa la lista numerada de ideas de I-19;
4. la nueva versión completa se vuelve a evaluar;
5. una idea madura reabierta suspende su curaduría hasta cerrar la nueva evaluación.

Si no existe afinidad o la idea pertenece a otra campaña/pregunta, el sistema solicita primero el
alcance y después aplica la selección de idea. Nunca mezcla ideas de campañas o preguntas distintas.
Esta capacidad cubre la idea cerrada reciente en el alcance resuelto; **no** promete todavía listar
cualquier idea de ciclos históricos o de cualquier estado. Esa ampliación es P-30.

### 5.9 Cambio del interruptor

- `false → true`: permite nuevos ciclos desde la siguiente resolución, siempre que la campaña siga
  `activa`.
- `true → false`: las conversaciones abiertas y sus ideas activas pueden terminar; al cerrarse ya no
  se crea otro ciclo. Las preguntas no completadas del recorrido original siguen disponibles.
- `activa → cerrada`: prevalece sobre la regla anterior y detiene la interacción de inmediato, como
  establece el ciclo de vida vigente. La gracia para terminar aplica al interruptor, no al estado.

---

## 6. Parte determinista y parte LLM

| Parte del flujo | Tipo | Responsable |
|---|---|---|
| Validar campaña activa, asociación y participante | Determinista | Servidor |
| Calcular campañas/preguntas elegibles | Determinista | Servidor |
| Construir/validar listas numeradas | Determinista | Servidor |
| Conservar el aporte y controlar vencimiento | Determinista | Servidor/Cosmos |
| Elegir afinidad y crear ciclo/IDs | Determinista | Servidor |
| Detectar número o nombre exacto | Determinista | Servidor |
| Aplicar flags, cupos, umbral, estados y curaduría | Determinista | Servidor |
| Detectar intención explícita de cambiar/revisitar | Determinista con vocabulario configurable | Servidor |
| Separar varias ideas, cuando I-06 está activo | No determinista, validado | LLM propone; servidor limita y valida |
| Consolidar la idea completa | No determinista, validado | LLM propone; servidor conserva versiones |
| Evaluar contra rúbrica y redactar coaching | No determinista, validado | LLM; servidor valida salida y decide transición |

El LLM nunca recibe la lista de campañas para decidir por el participante y nunca puede convertir una
campaña cerrada en elegible.

---

## 7. Contratos de datos

### 7.1 Campaña

Campo aditivo:

```json
{
  "configConversacional": {
    "participacionContinua": false
  }
}
```

- tipo `boolean`;
- default `false`;
- campo ausente = `false`;
- editable en creación y configuración;
- no cambia `Campania.estado` ni `ParticipanteCampania.estadoRespuesta`.

### 7.2 Conversación

Campos aditivos:

```json
{
  "cicloParticipacion": 2,
  "origenAporteMessageId": "wamid.HBgM...",
  "enrutamientoAporteId": "route_u_..._wamid..."
}
```

- `cicloParticipacion` ausente = `1`;
- `origenAporteMessageId` hace idempotente el ciclo posterior;
- `enrutamientoAporteId` permite auditar la selección que lo originó;
- deja de aplicar la restricción “una conversación por usuario/campaña/pregunta”; pasa a ser una
  conversación por usuario/campaña/pregunta/**ciclo**.

### 7.3 `EnrutamientoAporte`

Nuevo tipo aditivo en el contenedor existente `conversations`, partición interna determinista
`campaniaId="routing:<usuarioId>"`; no crea infraestructura Azure ni atribuye el aporte a una campaña
antes de que el participante la elija:

```json
{
  "id": "route_u_8f3c_wamidabc",
  "type": "EnrutamientoAporte",
  "campaniaId": "routing:u_8f3c...",
  "usuarioId": "u_8f3c...",
  "whatsappMessageId": "wamid.abc",
  "phoneNumberIdDestino": "123456789",
  "textoOriginal": "Se me ocurrió crear...",
  "estado": "seleccionCampania",
  "campaniasOfrecidas": [
    { "campaniaId": "c_1", "nombreSnapshot": "Innovación comercial", "orden": 1 }
  ],
  "campaniaSeleccionadaId": null,
  "preguntasOfrecidas": [],
  "preguntaSeleccionadaId": null,
  "conversacionId": null,
  "intentosSeleccion": [
    { "whatsappMessageId": "wamid.sel1", "tipo": "campania", "resultado": "invalido", "fecha": "2026-07-29T15:05:00Z" }
  ],
  "creadoEn": "2026-07-29T15:00:00Z",
  "actualizadoEn": "2026-07-29T15:05:00Z",
  "venceEn": "2026-07-30T15:00:00Z",
  "procesadoEn": null
}
```

- `estado` ∈
  `seleccionCampania|seleccionPregunta|listo|enIdea|completado|expirado|cancelado`;
- id determinista por usuario + `whatsappMessageId`;
- la partición reservada `routing:<usuarioId>` permite consulta directa por usuario; no se expone como
  id de campaña y las consultas normales de conversaciones filtran por `type`;
- texto de negocio sujeto a los mismos controles de acceso/retención que `Mensaje`;
- las opciones guardan snapshots solo para auditoría; la autorización se vuelve a consultar;
- `intentosSeleccion` guarda ids, tipo, resultado y fecha, no el texto libre recibido;
- no tiene TTL físico: el vencimiento es lógico para conservar auditoría.

---

## 8. Contrato API y portal

### 8.1 API administrativa

`POST /api/admin/campanias`, `GET /api/admin/campanias/{id}` y
`PUT /api/admin/campanias/{id}` incluyen:

```json
{
  "configConversacional": {
    "participacionContinua": true
  }
}
```

No se crea un endpoint adicional. La validación es booleana y el backend aplica `false` si el campo
no viene. Duplicar una campaña copia la elección explícita; campañas históricas siguen en `false`.

### 8.2 Portal

En Campañas → Configuración → Conversación:

- checkbox: **“Permitir nuevas ideas después de finalizar”**;
- ayuda: “Mientras la campaña esté activa, cada participante podrá volver y comenzar ideas nuevas.
  Sus ideas anteriores no se mezclarán.”;
- texto separado del selector de estado para evitar confundir “continua” con “activa”;
- disponible al crear y editar para admin; solo lectura para visor;
- al apagarlo se informa: “Las ideas que ya están en conversación podrán terminar; no se abrirán
  ideas nuevas.”

---

## 9. Cupos y costo

En una campaña con `participacionContinua=true`:

- `maxMensajesPorUsuario` cuenta los mensajes aplicables de ese usuario/campaña con
  `timestamp > ahoraUtc - 24h`;
- `maxLlamadasLlmPorUsuario` conserva las clases de llamada ya contabilizadas por P-10/I-06/I-19,
  pero solo suma las ocurridas en `ahoraUtc - 24h`;
- la ventana es móvil, no se reinicia a medianoche;
- los ciclos y preguntas de la campaña comparten la misma ventana;
- el techo `MaxTurnosPorHilo` continúa siendo por conversación/ciclo;
- `presupuestoTokensCampania` continúa sumando toda la vida de la campaña y no se reinicia cada 24h,
  pregunta, idea ni participante.

Con `participacionContinua=false`, los cupos por participante conservan su semántica acumulada actual.
P-26 cambia la ventana únicamente donde la continuidad haría inviables los límites acumulados.

---

## 10. Seguridad, privacidad y observabilidad

- Revalidar elegibilidad al mostrar y al aceptar una selección evita carreras con cambios
  administrativos.
- No registrar `textoOriginal`, nombres, números ni preguntas completas en telemetría técnica.
- El texto original solo vive en el plano de negocio y hereda sus permisos y retención.
- El menú no filtra campañas, preguntas ni ideas ajenas.
- La deduplicación del webhook ocurre antes de crear `EnrutamientoAporte`.
- El procesamiento interno del aporte conservado no vuelve a registrar el mismo webhook.
- Usar ETag/operación condicional al cambiar estado de enrutamiento; solo una ejecución puede pasar
  de `listo` a `enIdea` y fijar `procesadoEn`.
- Registrar `LogSeguridad(enrutamientoParticipacion)` con:
  `accion=ofrecido|seleccionado|invalido|expirado|procesado|cambioCampania`,
  conteo de opciones, ids internos, resultado y `correlationId`; nunca texto del participante.
- Métricas agregadas: participantes con continuidad, ciclos nuevos, menús ofrecidos, tasa de
  selección, expiraciones, ambigüedades y latencia hasta procesar.

---

## 11. Manejo de condiciones especiales

| Caso | Comportamiento |
|---|---|
| Campaña cerrada después de mostrar la lista | Se rechaza la selección, se recalculan opciones y no se procesa el aporte allí. |
| Participante deshabilitado después de mostrar la lista | Rechazo neutral; aporte queda cancelado/auditable. |
| Pregunta desactivada antes de elegir | Se ofrece de nuevo la lista de preguntas vigentes. |
| Dos campañas con el mismo nombre | El nombre es ambiguo; debe responder con número. |
| Llega el mismo aporte por reintento de Meta | Se reutiliza el mismo `EnrutamientoAporte`; no crea otro ciclo. |
| Dos aportes llegan casi simultáneamente sin afinidad | Se serializa por participante; uno abre/resuelve el ciclo y el otro se procesa como aporte de la idea activa o queda en espera, nunca crea dos afinidades activas por accidente. |
| Selección vence | Se marca `expirado`; el siguiente aporte empieza una resolución nueva. |
| Flag se apaga durante coaching | La idea actual termina; no se crea un ciclo posterior. |
| Estado de campaña cambia a cerrada | Se detiene la interacción inmediatamente. |
| El participante dice “otra campaña” | Se ofrece el menú sin cerrar la idea actual. |
| El participante dice “complementar la anterior” | Se resuelve alcance y se reaplica la reapertura I-19 con el mismo `ideaId`. |
| Falla Cosmos al conservar el aporte | No se muestra un menú que pueda perder el mensaje; se aplica el manejo técnico/reintento y no se procesa parcialmente. |
| Falla LLM después de enrutar | Se conserva el comportamiento de fallback I-19/P-25; el enrutamiento ya resuelto permanece auditable. |

---

## 12. Criterios de aceptación

1. Una campaña histórica sin el campo se comporta igual que antes.
2. Con el flag apagado, completar todas las preguntas impide crear ideas nuevas.
3. Con el flag encendido y la campaña activa, un participante puede cerrar una idea y crear otra
   distinta en la misma pregunta.
4. Cada idea nueva tiene conversación, `ideaId`, versión consolidada, evaluación, Markdown y
   curaduría independientes.
5. Una respuesta al coaching continúa en la campaña/idea activa sin volver a mostrar opciones.
6. Con dos campañas elegibles se presenta una lista y no se elige silenciosamente la más reciente.
7. El aporte enviado antes de elegir se procesa exactamente una vez después de una selección válida.
8. Con varias preguntas se solicita la pregunta; con una sola se avanza automáticamente.
9. Número, nombre exacto no ambiguo y errores de selección se comportan como define §5.
10. A las 24 horas, una selección pendiente vence sin borrar su evidencia.
11. Apagar el flag permite terminar la idea abierta y bloquea la siguiente.
12. Cerrar la campaña detiene toda interacción, incluso si el flag estaba encendido.
13. “Complementar la anterior” conserva el `ideaId`; un aporte normal posterior crea otro.
14. Los cupos de campaña continua miran las últimas 24 horas y el presupuesto de tokens sigue
    acumulado.
15. Ninguna campaña/pregunta no autorizada aparece en el menú o en logs visibles.
16. Una prueba E2E simulada cubre webhook → selección campaña → selección pregunta → coaching →
    madurez → aporte posterior → nueva idea, sin usar WhatsApp real.

---

## 13. Registro de implementación local

| Corte | Entrega verificable | Pruebas mínimas |
|---|---|---|
| 1 | Dominio, contratos y `EnrutamientoAporte`. | Histórico/default, CRUD/duplicado y Cosmos. |
| 2 | Resolución multi-campaña y aporte conservado. | 0/1/N, selección, revalidación, expiración e idempotencia. |
| 3 | Pregunta, afinidad y ciclos nuevos. | 1/N preguntas, coaching sin menú, segundo ciclo y cambio explícito. |
| 4 | Reapertura vigente y cupos móviles de 24 horas. | Idea nueva vs. reapertura, bordes de ventana y presupuesto acumulado. |
| 5 | Portal accesible. | Admin/visor, ayuda, round-trip y regresión de campañas. |
| 6 | Observabilidad, E2E simulada, QA y cierre documental. | Flujo completo, concurrencia/reintento, seguridad, backend, portal y diff. |

Los seis cortes están en `main` (`0e07527` a `da899ce`). No desplegar ni cambiar configuración remota
sin una instrucción posterior del usuario. Los vacíos P-28/P-29/P-30 se implementarán como extensiones
puntuales de esta base, no como otra participación continua.

---

## 14. Rollback

1. Desactivar `participacionContinua` en las campañas afectadas.
2. Las ideas/ciclos ya persistidos no se borran.
3. Las ideas activas pueden terminar mientras la campaña continúe `activa`.
4. No se abren ciclos nuevos.
5. Los documentos históricos/aditivos siguen siendo legibles por los flujos anteriores.
