# P-33 — Consulta y cierre visible de la idea

> Estado: **ESPECIFICADA Y APROBADA — lista para implementación inmediata (2026-08-13)**  
> Origen: retroalimentación de usuario de la convención + REQ-054  
> Dependencias: I-19, I-20, P-26, P-29, P-30, P-31 y P-32  
> Alcance de esta entrega: especificación y contratos; **sin código, despliegue ni configuración remota**

---

## 1. Resumen ejecutivo

Un participante puede preguntar en cualquier momento «¿cómo va mi idea?», «muéstrame cómo quedó» o
«dime cómo va escrita mi iniciativa». Hoy esa frase no tiene una ruta propia: puede terminar tratada
como un aporte y el participante no ve la versión consolidada que I-19 ya guarda.

P-33 convierte la visibilidad de la idea en una capacidad conversacional de primer nivel:

1. **Por demanda**, muestra la idea que naturalmente está en contexto: primero la activa y, si no hay
   una, la última que la persona trabajó. No abre un menú por defecto.
2. **Al cerrar**, muestra cómo quedó la idea antes de despedirse o avanzar, aunque todavía esté en
   incubación.
3. Una consulta sobre una idea cerrada deja una afinidad temporal. Si la siguiente respuesta es una
   corrección o complemento sustantivo, el servidor reabre **esa misma idea** y la actualiza sin volver
   a preguntar cuál.

La consulta nunca es un aporte, no evalúa, no consume una repregunta y no cambia la madurez. El texto
visible es la versión I-19 elegida por el servidor e insertada íntegra; el LLM puede redactar un puente
breve, pero no elegir ni modificar la idea.

---

## 2. Decisiones confirmadas

| Decisión | Regla aprobada |
|---|---|
| Referencia por defecto | «Mi idea» significa la idea activa; sin una activa, la última idea trabajada por esa persona. |
| Experiencia | No mostrar menú si el participante no pidió otra idea. Un coach humano conserva el tema actual. |
| Otra idea | Solo «otra idea», «la anterior» o una referencia equivalente activa P-30 o una selección explícita. |
| Idea cerrada consultada | La consulta es de solo lectura, pero fija una afinidad temporal con esa idea. |
| Corrección posterior | Una corrección o complemento sustantivo reabre automáticamente la misma idea consultada. Decisión confirmada por el usuario el 2026-08-13. |
| Autoridad | Campaña, pregunta, usuario, idea, versión, reapertura y estados los decide el servidor. El modelo propone redacción, nunca transiciones. |
| Texto mostrado | La versión vigente de I-19, carácter a carácter; no se vuelve a resumir ni a traducir. |
| Madurez | Consultar o mostrar al cierre es independiente del umbral, de `nivelMadurez` y de P-31. |
| Cierre | En un cierre normal se muestra la idea antes del agradecimiento o de avanzar. Una idea rechazada explícitamente no se presenta como «así quedó». |
| Varias campañas | Se consideran únicamente campañas activas y asociaciones vigentes del usuario; entre ellas gana la idea con trabajo más reciente. No hay menú implícito. |
| Activación | Kill-switch global OFF y controles por campaña; sin activación remota en el cambio de código. |

---

## 3. Alcance

### 3.1 Incluido

- Intención determinista y localizada `consultarIdea` para consultas puras y breves.
- Resolución segura de la idea activa o de la última idea trabajada.
- Visualización de la versión vigente sin agregarla como aporte ni reevaluarla.
- Visualización al cerrar por madurez, decisión del participante, tope, fallback o inactividad.
- Afinidad temporal con una idea cerrada consultada y reapertura automática ante una corrección clara.
- Compatibilidad con hilo simple, I-18/multi-idea, P-26, P-29, P-30, P-31 y P-32.
- Textos `es/en`, respaldo determinista, telemetría sin contenido y E2E simulada.
- Interruptor global y opciones por campaña con rollback sin migración.

### 3.2 Fuera de alcance

- Mostrar porcentajes, puntajes, rúbricas, criterios o explicar por qué la idea es madura/incubación.
- Buscar ideas por similitud semántica o dejar que el LLM elija una idea.
- Mostrar ideas de campañas cerradas, asociaciones inactivas, usuarios anteriores del mismo teléfono o
  cualquier otro participante.
- Reescribir, traducir o «mejorar» la versión al mostrarla.
- Exponer un menú histórico si no se pidió otra idea; P-30 conserva esa función explícita.
- Crear una nueva pantalla o endpoint administrativo.
- Modificar ideas históricas por el solo hecho de consultarlas.

---

## 4. Conceptos y selección determinista

### 4.1 Consulta pura

Una consulta pura pide leer la idea y no aporta información nueva. El detector normaliza mayúsculas,
acentos, puntuación y espacios, aplica `MaxCaracteresConsultaIdea`, y reconoce las frases del catálogo
`consultarIdea` y patrones inequívocos equivalentes.

Ejemplos incluidos:

- «dime cómo va escrita mi idea hasta ahora»;
- «muéstrame mi idea»;
- «¿cómo quedó mi iniciativa?»;
- «recuérdame cómo va»;
- `show me how my idea looks so far`.

La coincidencia por contención solo se acepta si fuera de la frase hay palabras de cortesía o tiempo
(`por favor`, `hasta ahora`, `en este momento`). Un mensaje mixto como «muéstrame mi idea y agrega que
aplica en Colombia» **no se consume como consulta**: conserva su contenido y sigue la ruta normal de
aporte. Así nunca se pierde una corrección por una coincidencia parcial.

### 4.2 Orden para elegir «mi idea»

Después de revalidar usuario, asociación y campaña activa:

1. Si existe una única idea activa en el hilo/cola vigente, se elige esa.
2. Si por un dato inconsistente aparecen varias activas, se elige la de `ActualizadaEn` más reciente y
   se registra la anomalía sin texto.
3. Sin idea activa, se consideran las ideas propias no rechazadas de las campañas activas autorizadas.
4. Gana la de `ActualizadaEn` más reciente; desempates: conversación de inicio más reciente,
   `IdeaIndice` descendente e `IdeaId` ordinal.
5. Nunca se mezcla el resultado con una campaña/pregunta distinta ni se consulta un documento solo
   porque el teléfono coincide.

Una idea `rechazada` se excluye de la selección implícita. Sigue siendo auditable y P-30 puede
retomarla únicamente mediante intención explícita.

### 4.3 Versión visible

- Idea abierta: `VersionPropuestaRef ?? VersionConfirmadaRef`.
- Idea cerrada: `VersionConfirmadaRef ?? VersionPropuestaRef`.
- Si el puntero no resuelve o todavía no existe versión, no se reconstruye texto desde mensajes ni se
  invoca al LLM: se envía `sinIdeaDisponible` y se conserva todo el estado.

La versión se inserta server-side y no se modifica. El límite vigente
`MaxCaracteresIdeaConsolidada` mantiene el bloque dentro del máximo del canal; encabezado/idea e
invitación/cierre pueden salir como dos mensajes consecutivos del mismo turno si hace falta para no
recortar la versión.

---

## 5. Flujos visibles

### 5.1 Consulta bajo demanda

1. P-33 reconoce la consulta **antes** de selecciones pendientes P-26/P-30, afinidad, P-27 y aporte.
2. Cancela de forma auditable un menú pendiente que ya no corresponde, sin procesar la consulta como
   elección ni como contenido.
3. Resuelve la idea y la versión con §4.
4. Compone: puente natural breve → versión íntegra → invitación opcional, sin pregunta obligatoria.
5. Envía y registra solo ids internos, estado y resultado.
6. Si la idea estaba cerrada, persiste afinidad `consultarIdea` durante un máximo de 24 horas y hasta
   el primer mensaje significativo.

Ejemplo de respaldo:

> Claro. Así va tu idea hasta ahora:  
> _[versión vigente]_  
> Si quieres seguir afinándola, cuéntame qué cambiarías; si prefieres dejarla así, también está bien.

Una consulta repetida vuelve a mostrar la versión: no comparte la idempotencia «una vez por idea» de
P-31. El dedupe del webhook sí evita responder dos veces al mismo `whatsappMessageId`.

### 5.2 Respuesta después de consultar una idea cerrada

La afinidad es contextual, no una reapertura anticipada:

| Respuesta siguiente | Resultado |
|---|---|
| Corrección o complemento sustantivo | Revalidar y reabrir la misma idea; conservar `ideaId`, versiones e historial; procesar el texto como corrección. |
| «Gracias», «ok», saludo u otro acuse sin contenido | Agradecer o degradar al flujo neutral; completar la afinidad; no reabrir. |
| «Otra idea», «la anterior», «cambiar de campaña» | Completar/suspender afinidad y entregar a P-30/P-26. |
| Otra consulta pura | Mostrar de nuevo la versión actual sin reabrir. |
| Finalizar participación o rechazo | Aplicar la intención de control; no reabrir. |
| Afinidad vencida, campaña/asociación inactiva o versión inexistente | No reabrir; recalcular el flujo autorizado vigente. |

Tras descartar intenciones explícitas de consulta, acuse, saludo, cambio, nueva idea y control, el
primer mensaje sustantivo se interpreta como respuesta al contexto mostrado y reabre la idea. La
afinidad se consume una sola vez. La nueva versión usa origen `reapertura`/`correccion` de I-19 y se
evalúa completa como siempre; una idea madura reabierta suspende su curaduría hasta reevaluar.

### 5.3 Idea visible al cerrar

| Causa de cierre | Qué se muestra |
|---|---|
| Umbral de madurez | Versión que se acaba de evaluar, luego cierre/transición. |
| «Así está bien» o finalizar idea | Versión vigente, luego acuse y siguiente idea/pregunta si existe. |
| Tope de revisiones, turnos, cupo o fallback | Última versión disponible, identificada como «hasta aquí», sin afirmar madurez. |
| Finalizar participación con varias ideas | Última idea activa/trabajada y una nota breve de que las demás quedaron guardadas; no enumerarlas. |
| Inactividad | Última idea activa/trabajada y aviso de pausa, solo dentro de la ventana de servicio. Las demás quedan guardadas. |
| Rechazo explícito | No mostrar «así quedó»; conservar el acuse de rechazo. |
| Cierre administrativo | No enviar ni exponer historial. |
| Sin versión resoluble | Cierre vigente sin inventar ni reconstruir texto. |

En un cierre individual de una cola, cada idea se muestra al cerrar su turno antes de activar la
siguiente. En cierres masivos (participación/inactividad) se muestra solo la última trabajada para no
convertir la despedida en un reporte robótico.

Fuera de la ventana de 24 horas no se envía texto libre ni se fuerza una plantilla HSM. El estado se
cierra igual y la siguiente consulta entrante, si vuelve a abrir una ventana válida, puede mostrar la
última idea autorizada.

---

## 6. Precedencia con capacidades existentes

1. Dedupe, identidad, usuario activo y autorización.
2. **P-33 consulta pura**, incluso si había un menú pendiente.
3. Afinidad P-33 y sus salidas explícitas.
4. Cambio de campaña P-26 y retomar otra idea P-30.
5. Rechazo/continuar/P-27 y demás controles del estado actual.
6. Aporte/consolidación/evaluación normal.

P-33 no depende de `ResumenConsolidacionHabilitado`, del umbral de P-31 ni de los flags de P-27. P-31
sigue siendo proactivo y una vez por idea; P-33 es reactivo y repetible. P-30 sigue siendo la ruta
explícita para otra idea y puede mostrar menú.

---

## 7. Contratos de dominio, persistencia y configuración

### 7.1 Configuración

- `Conversacion:VisibilidadIdeaParticipanteHabilitada` (`bool`, default `false`) — kill-switch global.
- `Conversacion:MaxCaracteresConsultaIdea` (`int`, default recomendado `220`).
- `configConversacional.consultaIdea` (`bool`, default `true`) — opt-out por campaña.
- `configConversacional.mostrarIdeaAlCerrar` (`bool`, default `true`) — opt-out por campaña.
- `FrasesConsultarIdea` como respaldo legacy; en P-32 la clave canónica es la lista
  `frases.consultarIdea` por idioma.

El gate global manda sobre ambos campos de campaña. Campos ausentes conservan los defaults indicados;
con el gate OFF el comportamiento observable es el actual.

### 7.2 Textos P-32

Claves nuevas obligatorias del registro cerrado, con semilla `es/en` y respaldo compilado del mismo
idioma:

- mensajes: `encabezadoConsultaIdea`, `invitacionConsultaIdea`, `encabezadoCierreIdea`,
  `otrasIdeasGuardadas`, `sinIdeaDisponible`;
- frases: `consultarIdea`, `acuseConsultaIdea`, `nuevaIdea`.

Se amplía el registro de 24 a **29 mensajes** y de 13 a **16 listas**. Una versión de catálogo nueva
debe contenerlas; las versiones históricas activas se leen con respaldo legacy durante la migración y
no se mutan.

### 7.3 Redacción I-20

Agregar `ActoConversacional.ConsultarIdea` al final del enum. El redactor produce solo un puente breve
sin repetir ni parafrasear la versión; el servidor inserta el cuerpo exacto y la invitación. Si falta
cupo, configuración o salida válida, se usa `encabezadoConsultaIdea` sin bloquear la consulta.

El cierre conserva `ActoConversacional.Cerrar`; el servidor antepone encabezado + versión y mantiene
el mensaje de cierre vigente. No se agrega una segunda llamada LLM al cierre.

### 7.4 Afinidad sin entidad nueva

Reutilizar `EnrutamientoAporte`:

- agregar `modo=consultarIdea` al final del enum;
- conservar `ideaSeleccionadaId`, `conversacionId`, idioma y versión de catálogo;
- usar `estado=enIdea` mientras la afinidad está vigente y `completado|expirado|cancelado` al terminar;
- `venceEn` es 24 horas, pero el primer mensaje significativo la consume.

No se agregan campos a `IdeaConsolidada`, `VersionIdeaConsolidada` ni `Conversacion`, ni contenedores,
índices o migraciones. Documentos previos conservan su interpretación vigente.

### 7.5 Telemetría

`TipoEventoSeguridad.VisibilidadIdeaParticipante`, aditivo al final del enum:

- `accion=consultaEnviada|consultaSinIdea|cierreEnviado|cierreOmitido|afinidadCreada|reaperturaAplicada|afinidadCompletada|anomaliaMultiplesActivas`;
- ids internos de campaña/conversación/pregunta/idea, estado, origen y resultado de envío;
- nunca el texto consultado, la versión, el nombre, el número, el puente ni la corrección.

La llamada opcional de redacción conserva además la contabilidad I-20/P-10 existente.

---

## 8. Seguridad y privacidad

- Revalidar usuario activo, rol participante, asociación activa y campaña activa en cada consulta y
  antes de reabrir.
- Filtrar siempre por `UsuarioId`, `CampaniaId`, `PreguntaId`, `ConversacionId` e `IdeaId`; no confiar
  únicamente en ids recibidos de una afinidad persistida.
- El cambio de titular de un teléfono no transfiere historial: la consulta usa el `Usuario.Id` activo
  resuelto por I-08 v2.
- Una campaña cerrada/borrador/archivada nunca responde ni entrega texto histórico.
- La versión es dato no confiable para el redactor y se inserta después de validar su salida; no se
  convierte en instrucción.
- Logs y métricas no contienen contenido de participantes ni textos de catálogo.

---

## 9. Condiciones especiales

| Condición | Comportamiento seguro |
|---|---|
| Consulta durante menú de campaña/pregunta/idea | Cancelar el menú y responder por contexto; la consulta no cuenta como selección. |
| Consulta con idea abierta y otras en cola | Mostrar solo la activa. |
| Última idea rechazada | Saltarla; elegir la no rechazada más reciente o informar que no hay idea visible. |
| P-31 ya mostró la idea | P-33 puede mostrarla de nuevo por demanda. |
| Consulta + información nueva en el mismo mensaje | No interceptar como consulta pura; procesar el aporte para no perder contenido. |
| Dos ideas con la misma fecha | Aplicar desempates estables de §4.2. |
| Versión o conversación desapareció entre resolución y envío | Revalidar, registrar `consultaSinIdea` y no inventar contenido. |
| Reintento del mismo webhook | Dedupe existente; no duplicar envío ni afinidad. |
| Fallo del redactor | Respaldo determinista; la versión se sigue mostrando. |
| Fallo de envío | Reintento normal del gateway; no cambiar madurez ni reabrir. |
| Catálogo incompleto | Respaldo del mismo idioma; nunca mezclar `en` con `es`. |
| Ventana de servicio cerrada | No enviar texto libre; esperar el siguiente entrante autorizado. |

---

## 10. Criterios de aceptación

1. «Dime cómo va escrita mi idea hasta ahora» muestra la activa sin consolidar esa frase como aporte.
2. Sin idea activa, muestra automáticamente la no rechazada más reciente; no ofrece menú.
3. «Otra idea» o «la anterior» conserva P-30 y puede pedir una selección.
4. La versión visible coincide carácter a carácter con la referencia elegida por §4.3.
5. Consultar no crea `Respuesta`, versión, evaluación o Markdown, no consume repreguntas y no cambia
   madurez, curaduría ni estado de la idea.
6. Una consulta repetida es válida y el mismo webhook duplicado es idempotente.
7. Tras consultar una cerrada, «cambia la parte de Estados Unidos; también aplica en Colombia» reabre
   el mismo `ideaId`, crea una nueva versión y evalúa la versión completa.
8. «Gracias» después de consultar no reabre ni crea una idea.
9. Un mensaje mixto de consulta y corrección no pierde la corrección.
10. Los cierres normales muestran la versión; rechazo y cierre administrativo no la muestran.
11. Finalizar participación/inactividad con varias ideas muestra solo la última y reconoce las demás.
12. Fuera de la ventana no se fuerza texto libre ni plantilla.
13. Usuario/asociación/campaña inactivos y teléfono reasignado no reciben historial.
14. P-31/P-27 OFF no impiden P-33 cuando su propio gate está ON.
15. Gate OFF o campaña opt-out conserva exactamente el comportamiento previo.
16. Funciona en `es/en` sin mezcla y sin usar texto del participante en telemetría.
17. Build, pruebas no-Calibración, formato y `git diff --check` quedan verdes.

---

## 11. Plan de implementación inmediata

| Corte | Entrega verificable | Pruebas mínimas |
|---|---|---|
| 1 | **Contratos y resolución pura.** Opciones/gates, campos de campaña, registro P-32, detectores, selector determinista y extensión `EnrutamientoAporte`; sin salida visible. | Defaults OFF, round-trip Cosmos/API, catálogo histórico, consultas puras/mensajes mixtos, orden/seguridad y versiones ausentes. |
| 2 | **Consulta y continuidad.** Enganche previo al routing, acto I-20/fallback, envío exacto, afinidad y reapertura automática de la idea consultada. | Abierta/cerrada/múltiples, menú pendiente, repetición/dedupe, gracias/otra idea/corrección, revalidación y no creación de artefactos por consulta. |
| 3 | **Cierres y cierre documental.** Integrar todos los cierres normales e inactividad, telemetría, E2E simulada `es/en`, QAS y sincronización final. | Matriz de §5.3, multi-idea, fuera de ventana, rechazo/admin, fallo redactor/envío, gate/opt-outs y regresión P-29/P-30/P-31. |

### 11.1 Archivos/puntos de entrada previstos

- `ServicioEnrutamientoParticipacion` y `ProcesadorWebhookEntrante`: precedencia, resultado tipado y
  afinidad.
- `OrquestadorConversacion`: consulta, reapertura y composición en cierres.
- `DetectorIntencionContinuar` o detector puro dedicado: listas `consultarIdea`, `acuseConsultaIdea` y
  `nuevaIdea` sin ampliar P-27.
- `EnrutamientoAporte` + mapeo Cosmos: modo nuevo compatible.
- `RegistroCatalogoTextosConversacion`, semillas y portal P-32: claves nuevas.
- `TipoEventoSeguridad` y QAS: observabilidad/E2E sin contenido.

---

## 12. Rollback y activación

1. `VisibilidadIdeaParticipanteHabilitada=false` devuelve consulta y cierres al comportamiento anterior.
2. Por campaña se puede desactivar solo `consultaIdea` o solo `mostrarIdeaAlCerrar`.
3. Afinidades `consultarIdea` persistidas quedan inertes y se completan/expiran; no se borran.
4. No hay migración ni reparación de ideas/versiones.
5. Antes de activar: D5 de frases `es/en`, UAT con casos §10, costo/latencia del puente I-20, prueba de
   ventana WhatsApp y acta de flags. No activar remotamente durante la implementación local.
