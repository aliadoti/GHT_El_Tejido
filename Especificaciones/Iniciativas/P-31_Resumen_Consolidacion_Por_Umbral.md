# P-31 — Resumen de la consolidación al alcanzar un umbral propio

**Estado:** **ESPECIFICADA 2026-08-06**; pendiente de implementación (3 cortes).
**Requerimiento de negocio:** `Client_partner/.../Nuevas iniciativas/REQ-052_Visibilidad_progreso_de_la_idea.md`.
**Fecha de decisión:** 2026-08-06 (solicitud de GHT: "los usuarios quieren mejor visibilidad del progreso de sus ideas").
**Áreas afectadas:** política de límites conversacionales, orquestador conversacional, redactor de turnos
(I-20), dominio/persistencia de `IdeaConsolidada`, configuración, observabilidad y pruebas.
**Contratos relacionados:** `03 §3.6/§3.8/§3.15`, `05 §4.4`, `08 §4`, `10 §6.2`, `13 §3`, `Reglas §2/§3`.
**Extiende:** I-19 (versión consolidada), I-17/P-13 (umbral y madurez), I-20 (acto conversacional).
**Se coordina con:** I-18 (coaching secuencial), P-25 (coaching directo), P-27 (salidas naturales), P-30
(reapertura).

---

## 1. Resumen ejecutivo

Hoy el participante trabaja su idea a ciegas: I-19 mantiene una **versión consolidada canónica** que se
reescribe en cada aporte, pero esa versión **solo se le muestra** al pedir confirmación (I-19 §4.1) o al
reabrir una idea (I-19 §4.7). En el flujo normal de coaching (P-25) recibe retroalimentación y una
pregunta de foco, nunca el texto acumulado. Y cuando la evaluación cruza el umbral base, la rama
`madura` de `ConfirmarOCorregirIdeaAsync` **cierra la idea y el hilo** enviando `retroalimentación +
mensajeCierre`: el participante nunca ve el resultado de su trabajo ni decide si quiere seguir.

P-31 cierra ese vacío con **una perilla propia**. Se introduce el **umbral de resumen**
(`umbralResumenConsolidacion`), **independiente** del umbral base de madurez: cuando la evaluación de una
idea **todavía abierta** alcanza ese umbral, el servidor envía en el mismo turno la **consolidación
vigente hasta el momento** y pregunta si quiere seguir madurándola. El umbral de madurez (I-17) y su
sellado **no cambian en absoluto**.

El resumen es **retroalimentación proactiva dentro del turno de coaching que ya existe**: no crea un
estado nuevo de la máquina conversacional, no consume repreguntas y no cierra la idea. Todas las
respuestas posibles ya tienen ruta (mejora, "así está bien" de 05 §4.4, alias de salida de P-27).

---

## 2. Decisiones confirmadas

| Decisión | Regla aprobada |
|---|---|
| Dos perillas independientes | `umbralCierreAnticipado` sigue sellando **madurez** (I-17, ya implementado, intacto). `umbralResumenConsolidacion` decide **cuándo se muestra** la consolidación vigente. No se derivan una de la otra. |
| Qué se muestra | El **texto de la versión vigente** de la idea (`VersionIdeaConsolidada.Texto`), insertado **server-side**. El LLM nunca lo redacta, resume ni edita. |
| Qué se pregunta | Si quiere **seguir madurando** la idea o dejarla como está. Una sola pregunta. |
| Estado conversacional | **Ninguno nuevo.** El hilo permanece en `esperandoRepregunta`; las rutas de respuesta vigentes resuelven todos los casos. |
| Efecto sobre la idea | Ninguno: no confirma, no cierra, no evalúa de nuevo, no cambia `nivelMadurez` ni `estadoResultado`. |
| Costo de repreguntas | El resumen **no** incrementa `repreguntasUsadas`; viaja dentro del turno de coaching que ya se iba a enviar. |
| Frecuencia | **Una vez por idea.** Idempotente y persistido; superar el umbral en turnos siguientes no lo repite. |
| Precedencia del umbral | pregunta → campaña → global, idéntica a I-17/P-13. |
| Relación de orden entre umbrales | Si `umbralResumen >= umbralBase`, el resumen **nunca dispara** en el flujo I-19 (la idea cierra por madurez primero). Se registra un **diagnóstico de arranque**, no un error. |
| Evaluación en fallback | No dispara: sin calificación válida no hay umbral alcanzado (coherente con I-17). |
| Interruptor | Kill-switch global `Conversacion:ResumenConsolidacionHabilitado` (default `false`) + opt-out por campaña. |
| Compatibilidad | Apagado, el comportamiento observable es exactamente el actual. |

---

## 3. Alcance

### 3.1 Incluido
- Umbral propio `umbralResumenConsolidacion` con precedencia pregunta → campaña → global y su
  resolución determinista en `PoliticaLimitesConversacion`.
- Envío de la consolidación vigente + pregunta de continuidad al cruzar ese umbral con la idea abierta.
- Nuevo acto conversacional para darle voz (I-20) con respaldo determinista.
- Idempotencia persistida por idea y telemetría sin texto.
- Interruptor global, opt-out por campaña y overrides por campaña/pregunta.
- Diagnóstico de arranque cuando la configuración deja el resumen inalcanzable.

### 3.2 Fuera de alcance
- Cambiar el umbral de madurez, su sellado, su telemetría o el cierre anticipado (I-17/P-13).
- Mostrar puntajes, criterios, porcentajes de rúbrica o "% de avance" al participante (prohibido por
  I-03/I-20; el participante no debe enterarse de que existe una rúbrica).
- Un resumen **periódico** o por número de turnos: el disparador es exclusivamente el umbral.
- Resumir varias ideas a la vez, o ideas de otra pregunta/campaña.
- Cambiar la reapertura (I-19 §4.7 / P-30) o el menú de salida (P-27).
- Traducir o localizar el resumen (queda para la iniciativa de idioma, en análisis).

---

## 4. Conceptos funcionales

| Concepto | Significado |
|---|---|
| Umbral base | Fracción de la escala que sella `nivelMadurez` y cierra la idea (I-17/P-13). **Sin cambios.** |
| Umbral de resumen | Fracción de la escala, propia y menor, a partir de la cual se muestra la consolidación vigente. |
| Consolidación vigente | Texto de la versión actual de la idea (I-19), lo que el sistema entiende "hasta el momento". |
| Turno de resumen | El turno de coaching normal, enriquecido con la consolidación y la pregunta de continuidad. |
| Idea abierta | `estadoFlujo` distinto de `cerrada`; solo una idea abierta puede recibir resumen. |

---

## 5. Flujo funcional

### 5.1 Orden determinista dentro del turno

En `OrquestadorConversacion.ConfirmarOCorregirIdeaAsync`, donde hoy se decide entre cerrar por madurez y
ofrecer coaching (rama `madura || conforme || fallback || MaxRepreguntas <= 0`):

1. Se evalúa la versión completa como siempre (I-19/P-25). Nada de esto cambia.
2. Se resuelve el **umbral base** y se sella `nivelMadurez` como siempre (I-17). **Sin cambios.**
3. **Si la idea cierra** (madura, conforme, fallback o sin repreguntas) → flujo actual intacto. No hay
   resumen: la madurez gana.
4. **Si la idea sigue abierta**, antes de componer el turno de coaching se evalúa el **umbral de
   resumen** con la misma calificación ya obtenida —sin llamadas ni evaluaciones extra— y se verifica
   que la idea no tenga resumen previo.
5. Si procede, el turno de coaching se compone con el acto `ResumirAvance`: puente redactado +
   **consolidación vigente insertada por el servidor** + pregunta de continuidad. Si no procede, se
   compone el turno `Mejorar` de siempre.
6. Se marca la idea como "resumen enviado" y se registra telemetría. `repreguntasUsadas` avanza
   exactamente igual que hoy (por el turno de coaching, no por el resumen).

### 5.2 Respuesta del participante

No se abre un estado nuevo: el hilo queda en `esperandoRepregunta` y las rutas vigentes cubren todo.

| Responde | Ruta que lo atiende | Resultado |
|---|---|---|
| Contenido sustantivo | I-19 / P-25 | Se consolida como complemento o corrección y se re-evalúa. |
| "así está bien", "listo", "sigamos" | `DetectorIntencionContinuar` (05 §4.4) | Cierra la idea con motivo `participante`. **No depende de P-27.** |
| "pasemos a otra idea" / "terminar por ahora" | P-27, si sus flags están activos | Finaliza idea o participación. |
| Petición de mejora corta | P-24 | Confirmación implícita y coaching. |
| Silencio | I-17 §7 / P-29 | Cierre por inactividad y aviso de pausa. |

**Consecuencia de diseño:** P-31 **no depende de la activación de P-27**. Su pregunta se responde con el
vocabulario de continuar que ya opera sin flags.

### 5.3 Alternativa considerada y descartada

Abrir un estado propio (`esperandoDecisionResumen`) o reutilizar `esperandoConfirmacionSalida` de P-27.
Se descarta: el primero duplica una máquina de decisión que ya existe y agrega modos de falla (respuesta
inesperada, expiración, reintento) sin beneficio funcional; el segundo acopla P-31 a los flags de P-27,
que están apagados, y degradaría la pregunta a un menú de salida cuando P-27 está OFF.

---

## 6. Parte determinista y parte LLM

| Parte del flujo | Tipo | Responsable |
|---|---|---|
| Resolver el umbral de resumen y su precedencia | Determinista | Servidor (`PoliticaLimitesConversacion`) |
| Decidir si dispara (idea abierta, sin resumen previo, evaluación válida) | Determinista | Servidor |
| Elegir el texto que se muestra | Determinista | Servidor (versión vigente I-19, sin edición) |
| Redactar el puente y la pregunta de continuidad | No determinista, validado | LLM vía I-20 con respaldo determinista |
| Interpretar la respuesta | Determinista | Servidor (05 §4.4 / P-24 / P-27) |

El LLM **no** decide si se envía el resumen, **no** reescribe la consolidación y **no** puede omitirla:
igual que en el acto `Confirmar`, el servidor la inserta entre el puente y la pregunta, y la guarda
anti-fuga de I-03/I-20 rechaza cualquier redacción que nombre rúbrica, criterios o puntajes.

---

## 7. Contratos de datos y configuración

Sin contenedores nuevos. Todo es **aditivo** y con default que preserva el comportamiento actual.

### 7.1 Configuración global (`Conversacion`)
- `ResumenConsolidacionHabilitado` (`bool`, default `false`) — kill-switch.
- `UmbralResumenConsolidacion` (`double`, default `0`) — fracción de la escala en [0,1]; **0 o negativo
  desactiva**, igual convención que `UmbralCierreAnticipado`.

### 7.2 Override por campaña (`configConversacional`)
- `umbralResumenConsolidacion` (`double?`, ausente ⇒ global).
- `resumenConsolidacion` (`bool`, default `true`) — opt-out por campaña; el gate real sigue siendo el
  kill-switch global.

### 7.3 Override por pregunta
- `umbralResumenConsolidacion` (`double?`, ausente ⇒ campaña ⇒ global), simétrico a
  `Pregunta.UmbralCierreAnticipado`.

### 7.4 Política
- `PoliticaLimitesConversacion.ResolverUmbralResumen(campania, pregunta)` y
  `OrigenUmbralResumen(...)` → `pregunta|campania|global`, calcados de `ResolverUmbralBase`/
  `OrigenUmbral`. Reutiliza `UmbralAlcanzado`/`ValorUmbral`: no se introduce una segunda aritmética
  de umbrales.

### 7.5 Dominio y persistencia (idempotencia)
- `IdeaConsolidada` gana dos campos de solo lectura, ambos anulables: `ResumenEnviadoEn`
  (`DateTimeOffset?`) y `ResumenEnviadoEnVersion` (`int?`), más una transición `ConResumenEnviado(numeroVersion, ahora)`
  que **solo** los fija. Se propagan por `Crear`/`Restaurar`/`CrearEstado` y por las transiciones
  existentes (`ConPropuesta`, `ConfirmarVersion`, `Cerrar`, `Reabrir`) sin alterar sus invariantes.
- `IdeaConsolidadaCosmosDocument` mapea `resumenEnviadoEn` / `resumenEnviadoEnVersion`; **ausente ⇒
  `null`**, de modo que las ideas históricas se comportan como "sin resumen previo" sin migración.
- Una idea **reabierta conserva** el marcador: recibe a lo sumo un resumen en toda su vida. Queda como
  supuesto revisable en `SUPUESTOS.md` si la calibración muestra que conviene reenviarlo por ronda.

### 7.6 Redacción (I-20)
- `ActoConversacional.ResumirAvance`, **aditivo al final** del enum.
- `PoliticaRedaccionConversacional.AdmitePregunta` lo incluye; `TipoPromptDelActo` devuelve `null`
  (comparte la voz general del hilo).
- `DescribirActo`: *"mostrar el avance acumulado y preguntar si quiere seguir puliéndolo. El sistema
  mostrará la idea completa entre tu puente y tu pregunta."* La regla vigente *"no repitas la idea
  completa"* del redactor aplica sin cambios.

### 7.7 Mensajes deterministas (`OpcionesMensajesConversacion`)
- `EncabezadoResumenAvanceDefault` — p. ej. *"Así va tu idea hasta ahora:"*
- `PreguntaContinuarMadurandoDefault` — p. ej. *"¿Quieres seguir puliéndola o prefieres dejarla así?"*

Ambos con propiedad configurable, mismo patrón que el resto de la clase.

### 7.8 Telemetría
`LogSeguridad(TipoEventoSeguridad.ResumenConsolidacion)` — **aditivo al final** del enum (03 §3.15) —
con `accion=enviado|omitidoYaEnviado|omitidoPorCierre|omitidoFallback|fallbackRedaccion`, `ideaId`,
`numeroVersion`, `umbral`, `origen`, `score`, `escala` y `correlationId`. **Nunca** el texto
consolidado, el puente, la pregunta ni el aporte del participante (10 §6).

No cambia el contrato de API administrativa salvo la exposición de los dos campos nuevos de
configuración en el detalle de campaña/pregunta (aditiva, opcional).

---

## 8. Seguridad, privacidad y observabilidad
- El resumen solo puede contener el texto que el propio participante aportó y que I-19 ya consolidó y
  auditó; el redactor no puede agregar contenido (guardas de I-20 §4.1).
- Prohibido exponer puntaje, criterio, umbral o porcentaje: se reutiliza `FiltroSalidaRubrica` y las
  guardas de longitud/pregunta única sin excepciones para este acto.
- El envío consume una llamada de redacción: cuenta contra los cupos y el costo de P-10 y contra la
  ventana móvil de P-26. Sin cupo, se usa el respaldo determinista **sin llamada al LLM**; el resumen
  se envía igual.
- La idempotencia se persiste antes de considerar el resumen entregado, de modo que un reintento del
  worker de envíos no genere un segundo resumen.

---

## 9. Manejo de condiciones especiales

| Caso | Comportamiento |
|---|---|
| Kill-switch global apagado | Comportamiento actual idéntico; ni siquiera se resuelve el umbral. |
| `umbralResumen <= 0` o ausente | Desactivado para ese alcance. |
| `umbralResumen >= umbralBase` | El resumen es inalcanzable en el flujo I-19; se registra un **diagnóstico de arranque** (`ServicioPreparacion`) y el sistema opera normal. |
| Evaluación en fallback | No dispara; se registra `omitidoFallback`. |
| La idea cierra en el mismo turno (madura/conforme/sin repreguntas) | Gana el cierre; se registra `omitidoPorCierre`. |
| Resumen ya enviado para esa idea | No se repite; se registra `omitidoYaEnviado`. |
| Idea reabierta (I-19 §4.7 / P-30) | Conserva el marcador: no se reenvía. |
| Cola multi-idea (I-06/I-18) | El resumen aplica a la **idea activa** y a ninguna otra. |
| Redactor falla, degrada o no hay cupo | Respaldo determinista (§7.7); **la consolidación se envía siempre**. |
| Texto consolidado muy largo | Se aplica el acotamiento vigente de I-19 (`MaxCaracteresIdeaConsolidada`); no se resume ni se recorta con LLM. |
| Envío a WhatsApp falla | Lo maneja el reintento vigente de `ProcesadorEnvio`; el marcador ya persistido evita duplicar el resumen. |
| Campaña con `resumenConsolidacion=false` | No dispara para esa campaña. |

---

## 10. Criterios de aceptación
1. Con el kill-switch **apagado**, el comportamiento observable es **idéntico** al actual (regresión
   completa verde sin cambios de expectativas).
2. `umbralResumenConsolidacion` se resuelve con precedencia **pregunta → campaña → global** y es
   **independiente** de `umbralCierreAnticipado`: cambiar uno no altera el otro.
3. El sellado de `nivelMadurez`, el cierre por umbral y sus telemetrías (`ClasificacionMadurez`,
   `CierreUmbralAnticipado`) son **bit a bit los mismos** antes y después de P-31.
4. Al cruzar el umbral de resumen con la idea abierta, el participante recibe el **texto íntegro de la
   versión vigente** más una pregunta de continuidad, en un solo turno.
5. El texto mostrado coincide **carácter a carácter** con `VersionIdeaConsolidada.Texto`; el LLM no
   puede alterarlo, omitirlo ni truncarlo.
6. El resumen **no** cierra la idea, **no** la confirma, **no** dispara una evaluación adicional y
   **no** incrementa `repreguntasUsadas`.
7. El resumen se envía **una sola vez por idea**, aunque los turnos siguientes sigan superando el umbral.
8. Responder "así está bien" tras el resumen cierra la idea por `participante` **con los flags de P-27
   apagados**.
9. Responder con contenido sustantivo tras el resumen produce una nueva versión consolidada y su
   re-evaluación, como cualquier aporte.
10. Ninguna salida menciona rúbrica, criterios, puntaje, umbral ni escala (I-03/I-20).
11. La telemetría registra el disparo y las omisiones **sin** texto del participante ni del resumen.
12. Una configuración con `umbralResumen >= umbralBase` produce un diagnóstico de arranque y no rompe
    el flujo.
13. Una prueba E2E simulada cubre: aporte → evaluación bajo umbral base y sobre umbral de resumen →
    resumen → respuesta de mejora → segunda evaluación **sin** segundo resumen.

---

## 11. Plan de implementación por cortes

| Corte | Entrega verificable | Pruebas mínimas |
|---|---|---|
| 1 | **Perilla y dominio, sin efecto visible.** `UmbralResumenConsolidacion` global/campaña/pregunta, `ResolverUmbralResumen` + `OrigenUmbralResumen`, kill-switch, campos de idempotencia en `IdeaConsolidada` + documento Cosmos y diagnóstico de arranque. | Precedencia de los tres niveles; independencia del umbral base; default OFF; documento histórico sin los campos ⇒ `null`; regresión completa sin cambios de expectativas. |
| 2 | **Turno visible.** Acto `ResumirAvance` en I-20, inserción server-side del texto consolidado, respaldo determinista, enganche en la rama de coaching de `ConfirmarYEvaluarAsync`, marcado idempotente y telemetría. | Dispara/no dispara según umbral, idea abierta, fallback y resumen previo; texto idéntico a la versión vigente; sin cambio de `repreguntasUsadas`; fuga de rúbrica rechazada; respaldo sin cupo. |
| 3 | **Cierre.** E2E simulada por `POST /diagnostico/simulacion/webhook-entrante` (DT-QA-01), casos QAS, `TODO.md`/`AVANCES.md`/`SUPUESTOS.md` y fila en el índice de iniciativas. | Flujo completo sin WhatsApp real; respuesta "así está bien" con P-27 OFF; build Release, `dotnet format` y `git diff --check` verdes. |

Cada corte deja `TODO.md` y `AVANCES.md` actualizados. No desplegar sin instrucción posterior.

### 11.1 Punto de enganche

`OrquestadorConversacion.ConfirmarOCorregirIdeaAsync`, en la rama que hoy compone el turno de coaching
(`ComponerTurnoAsync(..., ActoConversacional.Mejorar, ...)`) después de descartar el cierre. Es el único
punto donde se conoce simultáneamente: la evaluación válida, la escala, la versión vigente, la idea
abierta y el turno que ya se iba a enviar. No se toca la rama de cierre.

### 11.2 Calibración (operación, no código)

Con `umbralCierreAnticipado = 0.6` (default vigente), un `umbralResumenConsolidacion` **entre 0.40 y
0.55** deja espacio real para que el resumen aparezca antes del cierre por madurez. Si GHT quiere el
resumen literalmente al **70%**, entonces el umbral base debe subir por encima de ese valor (p. ej.
`0.8`) — **decisión de negocio, no técnica**, y afecta la distribución maduro/incubación que D5 está
calibrando. Ambos valores se fijan en el acta de flags del día D.

---

## 12. Rollback
1. Apagar `Conversacion:ResumenConsolidacionHabilitado`.
2. El turno de coaching vuelve al acto `Mejorar` de siempre; nada persistido se borra ni se corrige.
3. Los campos `resumenEnviadoEn` / `resumenEnviadoEnVersion` quedan como dato histórico inerte; si el
   interruptor se vuelve a encender, las ideas ya resumidas no repiten el resumen.
4. Los umbrales de campaña/pregunta son opcionales: borrarlos devuelve el alcance al default global.

---

## 13. Avance local

> Actualizacion 2026-08-07: **P-31 esta DONE local (3/3).** La E2E simulada cubre inicio, aporte
> sobre umbral, resumen y mejora posterior sin repeticion; la guia humana esta en
> `QAS/14_P31_Resumen_Consolidacion_Como_Probar.md`. Los flags permanecen apagados: D5/UAT/costo
> siguen siendo pasos operativos fuera de este cambio local.

- **2026-08-07 — corte 2 en curso:** el acto `ResumirAvance` compone el texto de la versión vigente
  en el servidor, persiste su marca de idempotencia y registra `ResumenConsolidacion` sin texto de la
  idea ni del participante. La preparación advierte cuando el umbral global de resumen es igual o
  superior al de cierre. Queda el corte 3: E2E simulada, QAS y cierre documental. Los flags siguen
  apagados por defecto.
