# Plan de Hito 1 — El Tejido de la Red (Bright Insights)

> **⚠️ DOCUMENTO HISTÓRICO (corte 11-jul-2026) — PARCIALMENTE SUPERADO por la reunión GHT del 20-jul.**
> La fuente viva del plan y la priorización es `Iniciativas/00_Indice_y_Plan_de_Ejecucion.md`. Cambios
> del 20-jul que este documento **NO** refleja (léelos allí): **I-06 sigue en el MVP, pero I-09 (tejido
> colectivo) e I-10 se DIFIRIERON a "Capa 3" post-convención** (aquí aún aparecen como "grandes apuestas
> de la ruta crítica", §2.2/§5/§7 — ya no lo son); **P-07 (consentimiento) y el panel de P-09 se
> difirieron**; **HITL fuera del MVP**; **nueva iniciativa I-17 (BD de dos niveles: maduras vs.
> incubación)**; nombre confirmado **"Tejido de Red"** (no "Bright Insights/Idea"). Se conserva como
> referencia de diseño de fondo (mitigación R-01, frentes A–D), no como estado vigente.
>
> **Ruta crítica hacia el go-live del 10-ago-2026.** Documento técnico de ingeniería,
> escrito desde el rol de **Arquitecto de la solución + equipo de desarrollo** que construyó el MVP.
> Deriva de: `Plan_Trabajo_El_Tejido.xlsx` (hojas `Timeline`, `Iniciativas`, `Priorizacion`,
> `Insumos_de_GHT`), la reunión de demo del 9-jul-2026, el estado real del build en
> `AVANCES.md` (Fase 11) y las especificaciones `03/04/05/08/10`.
>
> **Fecha de corte del plan:** 11-jul-2026 · **Hito:** envío del mensaje de inicio de campaña a los
> participantes reales de la convención el **10-ago-2026** · **Convención (supuesto):** ≈ 24-sep-2026.
>
> Alcance de este documento: **Línea de Tiempo 1 (lo indispensable para el 10-ago)**. La rama de
> deseables (P-04, P-05, P-06, P-08, P-11, P-12) queda **referenciada** en §10, no detallada.

---

## 0. Cómo leer este plan

Cada iniciativa se especifica con la misma plantilla accionable:

- **Qué pide GHT / por qué** — el requerimiento de negocio surgido de la reunión.
- **Estado real hoy** — qué existe en el código (según `AVANCES.md` Fase 11), para no reconstruir lo hecho.
- **Diseño técnico** — cambios de código, contratos, config y archivos afectados.
- **Riesgo (foco LLM) y mitigación** — el riesgo declarado en el Timeline y cómo lo neutralizamos desde ingeniería.
- **Criterios de aceptación / pruebas** — cómo sabemos que quedó listo (build `-warnaserror` + test + `format` verde).

**Disciplina de trabajo (heredada de `jdocs/TODO.txt` y `01_Convenciones_para_Agentes.md`):**
leer antes de escribir; pasos pequeños y verificables; declarar rol y `REQ/ARQ §` cubiertos; commits
atómicos (Conventional Commits); **no cambiar contratos `03` (datos) / `04` (API) sin actualizar la spec
en commit aparte**; cero secretos en el repo; **push a `main` solo cuando el usuario lo pida** (un push
despliega a producción por CD OIDC).

---

## 1. Contexto de arquitectura (lo que ya está construido)

Stack: **.NET 8 (Clean Architecture: Domain / Application / Infrastructure / Api) + Angular 22**,
desplegado en **Azure App Service Linux**, persistencia **Cosmos DB** (contenedores `users`, `campaigns`,
`participants`, `conversations`, `responses`, `config`, `security`, `leases`), **Blob Storage** para
Markdown, **Key Vault** para secretos (Managed Identity), **Application Insights**. CD por push a `main`
(`deploy.yml`, OIDC).

El **flujo conversacional** (spec `05`) es una máquina de estados por hilo:
`esperandoRespuestaInicial → evaluando → (esperandoRepregunta →) evaluando → cerrada`. El primer entrante
de una conversación nueva **envía la pregunta vigente y no evalúa** (cold-start ya resuelto, `AVANCES` Fase 11);
el LLM entra a partir del segundo turno. El evaluador (spec `08`) es **agnóstico de proveedor** vía
`ConfigLlm` (Azure OpenAI / OpenAI-compatibles / Anthropic nativo), exige **salida JSON con esquema fijo**
(`08 §4`) y aplica **fallback seguro** ante fallo o salida inválida. La defensa anti prompt-injection es
**arquitectónica** (separación `system`/`user`, respuesta del usuario tratada como dato).

**Piezas clave que este plan toca** (rutas reales):

- `src/ElTejido.Application/Conversacion/OrquestadorConversacion.cs` — orquestador (máquina de estados).
- `src/ElTejido.Application/Evaluacion/` + `src/ElTejido.Infrastructure/Llm/LlmClientHttp.cs` — evaluación LLM.
- `src/ElTejido.Application/WhatsApp/` (`ServicioEnvios`, `ProcesadorEnvio`, `TrabajadorWebhook`) — gateway/envíos.
- `src/ElTejido.Domain/Respuestas/Respuesta.cs` — hoy **1 idea = 1 registro** (clave para I-06).
- `src/ElTejido.Application/Markdown/` — compilación determinista del `.md`.
- `src/ElTejido.Api/appsettings.json` — sección `Conversacion:*`, `WhatsApp:*`, `Seguridad:*`.

**Hechos que condicionan el plan (verificados en el repo):**

1. **No existe infraestructura de recuperación semántica** (sin embeddings/vector store). I-09 (tejido
   colectivo) y P-05 (insights) requieren construirla desde cero → decisión de diseño en §5.
2. **`Respuesta` es un único texto por registro.** I-06 (multi-idea → N registros) exige un paso de
   segmentación previo a la evaluación y un cambio aditivo de modelo.
3. **La entrega real por WhatsApp está bloqueada** por `code=131042` (billing de Meta) → P-01/P-02 son la
   ruta crítica externa y no dependen de nosotros en tiempos, solo en gestión.
4. **Guardrails de cupo por usuario/campaña y de costo LLM están pendientes** (`AVANCES` deuda técnica) →
   P-10 es requisito de seguridad para abrir a producción.

---

## 2. Estrategia del Hito 1 (backtracking desde el 10-ago)

El enfoque acordado con GHT es **backtracking**: fijar la ruta crítica ("lo básico"), congelar alcance,
probar y sacar un branch para lo deseable. Traducido a ingeniería, el Hito 1 se organiza en **cuatro
frentes** que avanzan en paralelo con dependencias explícitas:

| Frente | Objetivo | Iniciativas | Dueño técnico |
|---|---|---|---|
| **A. Desbloqueo de entrega** | Que el mensaje salga el 10-ago | P-01, P-02 | Aliado TI + GHT (gestión Meta) |
| **B. Calidad conversacional del coach** | Prompts, rúbrica, salida natural | I-01, I-03, I-04, I-05, I-11, I-12, I-13 | Aliado TI (backend/prompts) + GHT (contenido) |
| **C. Núcleo "tejido"** | Multi-idea + base común | I-06, I-09, I-10 | Aliado TI (backend) |
| **D. Operación y seguridad de producción** | Carga, segmentación, guardrails, monitoreo, consentimiento | I-08, I-14, I-16, P-03, P-07, P-09, P-10 | Aliado TI |

**Principio de control de riesgo LLM (transversal a los frentes B y C):** cada comportamiento no
determinístico se acota con **tres capas** — (1) *contrato de salida estructurado* que el sistema valida,
(2) *fallback determinista* que nunca rompe la conversación, y (3) *observabilidad* (métricas de fallback,
tokens y costo). Ningún ítem del frente B/C se considera "listo" sin las tres capas.

### 2.1 Secuencia (Sprints)

```
Sem 0    (9–13 jul)  · Priorización con GHT · Arrancar P-01/P-02 (Meta) · Workshop rúbrica I-11 · Recolectar seed thoughts I-12
Sprint 1a (14–18 jul) · I-01, I-05, I-06(inicio), P-03 · Fix I-16 · esqueleto I-09
Sprint 1b (21–25 jul) · I-03, I-04, I-08, I-06(cierre), I-09(core)
Sprint 2  (28 jul–1 ago) · I-10, I-12(embed), I-13(decisión), I-14, P-07, P-10
Pruebas   (4–8 ago)  · Pruebas robustas conjuntas · P-09 monitoreo · endurecimiento
Freeze    (8–9 ago)  · Congelación de alcance + carga real + dry-run E2E
HITO      (10 ago)   · Envío en vivo
```

### 2.2 Ruta crítica (lo que, si se atrasa, mueve el 10-ago)

`P-01/P-02 (Meta)` → `I-11 (rúbrica)` → `I-03/I-04 (prompts que dependen de rúbrica y seed thoughts)` →
`I-06 + I-09 (núcleo tejido)` → `Pruebas conjuntas` → `Freeze + dry-run` → **Hito**. Todo lo demás es
paralelizable o degradable. Las **grandes apuestas de la ruta crítica** son **I-06 e I-09** (alto esfuerzo,
alto riesgo de no determinismo): se blindan con el plan de §4.3 y §5.

---

## 3. Frente A — Desbloqueo de entrega (P-01, P-02)

Sin esto **no hay Hito**. Es la única dependencia con tiempos fuera de nuestro control (aprobaciones de Meta).

### P-01 · Resolver billing de Meta (code 131042)
- **Qué / por qué:** la WhatsApp Business Account acepta OTP y campaña con HTTP 200 pero **no entrega**
  (`code=131042` "Business eligibility payment issue"). Falta configurar moneda + método de pago en el Billing Hub.
- **Estado hoy:** bloqueador activo. Diagnóstico ya confirmado; el `TrabajadorWebhook` loguea estados de
  entrega `failed` con `code/detalle`.
- **Acción de ingeniería:** ninguna en código; es **gestión con GHT/Meta**. Nuestro rol: (1) dejar el
  App Service listo para conmutar de simulación a real con un solo cambio de config; (2) instrumentar un
  chequeo en `/health/ready` que reporte `wa-token`/`PhoneNumberId` presentes; (3) documentar el runbook
  de verificación de entrega.
- **Mitigación de riesgo de cronograma:** desacoplar todo el desarrollo de Meta usando el **camino de
  simulación** (`Simulacion__Habilitada=true` + `X-Diag-Key`, página `/simulacion-whatsapp`). Los frentes
  B, C y D se construyen y prueban íntegros **sin billing**. Meta solo es imprescindible para el dry-run
  real y el Hito.
- **Aceptación:** un envío real de prueba al participante `573182527390` se marca `sent`/`delivered`
  (no `failed:131042`). Escalar **hoy** (Sem 0); es el riesgo #1 del hito.

### P-02 · Plantilla HSM de inicio aprobada por Meta
- **Qué / por qué:** WhatsApp exige **plantilla HSM aprobada** para iniciar conversación fuera de la ventana
  de 24 h. La aprobación puede tardar días → enviar a revisión con antelación.
- **Estado hoy:** parametrizable; `ServicioEnvios` resuelve la plantilla global `WhatsApp:PlantillaEnvioInicial`
  y **rechaza** el disparo si falta el nombre (no cae a texto libre). Falta la aprobación de Meta.
- **Diseño técnico:** configurar en App Service `WhatsApp__PlantillaEnvioInicial__Nombre=el_tejido_inicio_campania`,
  `__Idioma=es_CO`, `__Componentes__0=nombre`, `__Componentes__1=campania`. El idioma debe **coincidir
  exactamente** con el aprobado por Meta.
- **Riesgo y mitigación:** externo (tiempos de Meta). Mitigación: enviar la plantilla a aprobación en **Sem 0**,
  con copy alineado al rebranding **Bright Insights** (ver I-15, opcional). Tener una **plantilla de repregunta**
  aprobada de respaldo por si alguna repregunta cae fuera de ventana (`05 §2.2`).
- **Aceptación:** plantilla en estado *Approved* en Meta; envío inicial desde el portal (Envíos) produce
  `EnvioMensaje.estado=enviado`; `/health/ready` = `ok`.

---

## 4. Frente B — Calidad conversacional del coach

Este frente concentra las **tres palancas** definidas por José/Felipe: pulir prompts, pulir rúbrica,
segmentar. Es donde vive la mayor parte del **riesgo de no determinismo** del LLM.

### I-11 · Recalibración de la rúbrica (6 variables SP/IE/AC/TR/CO/DU)
- **Qué / por qué:** revisar pesos y variables, debatir "Inside Edge", enriquecer más allá del sesgo
  personal en workshop conjunto. Es **dependencia dura** de I-03 (follow-ups sobre eje débil) e I-09.
- **Estado hoy:** la rúbrica es **parametrizable y versionada** (dominio `Rubrica`, edición híbrida por
  estado: in-place en `borrador`, nueva versión si activa). No requiere código nuevo; requiere **contenido**.
- **Diseño técnico:** el workshop produce una **nueva versión** de rúbrica (Markdown + pesos) cargada por
  el portal. Cada `Evaluacion` guarda `rubricaRef+versionRubrica` como snapshot (reproducibilidad ya
  implementada). La escala y las claves de criterio de la rúbrica activa se **incrustan en el system prompt**
  del evaluador (`ConstructorMensajesEvaluacion`), por lo que recalibrar es cambio de datos, no de código.
- **Riesgo y mitigación:** *depende de agenda de GHT* (riesgo de cronograma, no técnico). Mitigación:
  agendar el workshop en Sem 0; congelar la versión de rúbrica **antes de Sprint 1b** para no bloquear
  I-03/I-04. Regla dura: **no se abre a producción con rúbrica en `borrador`** (el orquestador ya cae a
  fallback si la rúbrica no está activa — `AVANCES` 10.2).
- **Aceptación:** existe una `Rubrica` versión N en estado *activa*; una evaluación de prueba devuelve
  `calificacion_por_criterio` con exactamente las 6 variables acordadas y `calificacion_total` en escala.

### I-12 · Pensamientos semilla (seed thoughts) embebidos en el prompt
- **Qué / por qué:** incorporar material de las charlas de la convención y pendientes del año pasado para
  orientar al coach. **Felipe entrega el contenido** (Insumo GHT #2, límite 18-jul).
- **Estado hoy:** contenido nuevo; el mensaje inicial ya sale de la BD de campaña. Falta el mecanismo de
  *embeber* los seed thoughts en el contexto de evaluación/coach por campaña.
- **Diseño técnico:** añadir a la configuración de campaña un bloque **`SeedThoughts`** (texto/lista,
  aditivo, sin romper `03`) que el `ConstructorMensajesEvaluacion` inyecta como **mensaje `system`
  adicional** ("contexto orientador de la campaña"), claramente separado de la rúbrica y de la respuesta
  del usuario. Los seed thoughts son **instrucción de contexto**, nunca se mezclan con el bloque de dato
  del usuario. Límite de longitud/tokens configurable para no inflar el prompt (`10 §2`).
- **Riesgo y mitigación:** (1) *depende de la entrega de Felipe* → mitigación: plantilla de captura de seed
  thoughts entregada en Sem 0 para que GHT la llene; si no llegan a tiempo, la campaña arranca con
  `SeedThoughts` vacío (degradación limpia). (2) *Prompt-injection vía contenido de campaña* → los seed
  thoughts los carga un admin de confianza por el portal; aun así van en rol `system` y no habilitan
  acciones. (3) *Coste de tokens* → acotar y medir (P-10).
- **Aceptación:** una campaña con seed thoughts produce respuestas del coach alineadas al material; el
  contexto enviado al LLM contiene el bloque `system` de seed thoughts y **nunca** secretos; con seed
  thoughts vacío el flujo sigue funcionando.

### I-13 · Decisión: rúbrica agnóstica vs. "tailored" a la coyuntura
- **Qué / por qué:** definir si la rúbrica queda amplia/agnóstica (y la relevancia entra por seed thoughts)
  o se ajusta a los pilares del momento. Depende de I-11, I-12.
- **Estado hoy:** decisión pendiente (GHT + Aliado TI). Es **diseño**, no build.
- **Recomendación de arquitectura:** **rúbrica agnóstica + relevancia por seed thoughts.** Razones: (a)
  desacopla contenido de estructura → reutilizable para ARMA y futuras campañas (P-12); (b) evita
  recalibrar la rúbrica cada convención; (c) menor superficie de cambio antes del freeze. La "coyuntura"
  entra por `SeedThoughts` (I-12) y por `tags` (I-14), no por la rúbrica.
- **Aceptación:** decisión registrada en `SUPUESTOS.md`; la rúbrica activa refleja la opción elegida.

### I-04 · Mensaje inicial abierto / estilo coach-terapeuta
- **Qué / por qué:** arranque abierto ("soy todo oídos") en vez de pregunta rígida; el coach guía sin
  "ferrocarrilear". Depende de I-12 (seed thoughts).
- **Estado hoy:** el mensaje inicial ya sale del `MensajeInicial` activo de la campaña (BD, variables por
  `RenderizadorMensaje`). Falta afinar **tono coach** (hoy es más neutro).
- **Diseño técnico:** trabajo de **prompt + contenido de campaña** (no código): (1) redactar el
  `MensajeInicial` de la campaña de convención con tono abierto; (2) reforzar en el system prompt del
  coach las reglas de comportamiento ("guía sin dirigir, refleja, invita a profundizar"). El
  comportamiento ya soporta historial reciente (memoria) para no repetir.
- **Riesgo y mitigación:** *no determinismo (medio)* — un arranque demasiado abierto puede diluir la
  captura. Mitigación: el `MensajeInicial` incluye una pregunta-ancla suave; A/B en las pruebas conjuntas
  con Felipe/Munir.
- **Aceptación:** el primer contacto usa el `MensajeInicial` de tono coach; en pruebas humanas el arranque
  se percibe abierto pero encauzado; no se evalúa el saludo (cold-start intacto).

### I-05 · Parafraseo y transparencia del proceso
- **Qué / por qué:** el LLM devuelve "esto es lo que entendí", organiza el texto y muestra la elaboración;
  evita el seco "gracias, registrado".
- **Estado hoy:** comportamiento **nuevo** (hoy no parafrasea; la retro es breve).
- **Diseño técnico:** extender el **contrato de salida** (`08 §4`) con un campo **aditivo**
  `parafraseo_devuelto` (string, breve) y mapearlo en `Evaluacion`. El orquestador antepone el parafraseo a
  la retro/invitación existente. Cambio de contrato de salida → actualizar la spec `08 §4` en commit aparte
  y las validaciones (`parafraseo_devuelto` no vacío, dentro de límite de longitud). No cambia el contrato
  de datos `03` de forma incompatible (campo opcional).
- **Riesgo y mitigación:** *no determinismo (medio)* — el parafraseo puede alucinar o alargarse.
  Mitigación: (1) límite duro de longitud validado en post-proceso → si excede, se trunca o se cae al texto
  operativo neutro; (2) el parafraseo es **dato mostrado**, nunca acción; (3) fallback: si el campo falta,
  se usa la retro breve actual (comportamiento previo intacto).
- **Aceptación:** una respuesta válida produce un parafraseo breve y fiel; salida sin el campo → retro
  clásica; parafraseo sobre-largo se acota; `format`/test verdes.

### I-03 · Follow-ups sobre el eje más débil, sin revelar la rúbrica
- **Qué / por qué:** el coach repregunta enfocándose en la dimensión con menor puntaje, **sin mostrar la
  rúbrica ni cuál eje está bajo**. Depende de I-11.
- **Estado hoy:** **parcial** — ya hay evaluación por rúbrica + `RepreguntaSugerida`; falta orientar
  explícitamente la repregunta al **criterio de menor puntaje** manteniendo la rúbrica oculta.
- **Diseño técnico:** en `EvaluadorLlm`, tras validar `calificacion_por_criterio`, calcular
  determinísticamente el **criterio mínimo** (server-side, no lo decide el LLM) y pasarlo al LLM como
  *pista de foco* en la generación de `repregunta_sugerida` **sin nombrar la rúbrica** ("profundiza en X
  aspecto"). La rúbrica y los puntajes **nunca** viajan al usuario (regla ya vigente: la retro es breve y
  no expone internals). La invitación a mejorar sigue siendo natural y variada (Opción B ya implementada).
- **Riesgo y mitigación:** *no determinismo (alto)* — el LLM podría filtrar la rúbrica o el eje. Mitigación:
  (1) el system prompt prohíbe explícitamente revelar rúbrica/ejes/puntajes; (2) **filtro de salida
  determinista** que rechaza/enmascara la retro si contiene nombres de criterios o cifras de puntaje
  (lista negra derivada de la rúbrica activa); (3) el cálculo del eje débil es server-side (auditable).
- **Aceptación:** ante una respuesta floja en un criterio, la repregunta se enfoca en ese aspecto sin
  nombrarlo; ninguna salida al usuario contiene nombres de criterio ni puntajes; test unit con respuesta
  que fuerza un mínimo conocido.

### I-01 · Parada no determinística + cierre por umbral de rúbrica
- **Qué / por qué:** que el LLM decida cuándo termina (no N reintentos fijos) y **cerrar cuando la
  calificación supere un umbral** (~0.70). ¿Umbral parametrizable por campaña?
- **Estado hoy:** **ya existe** — `Conversacion:UmbralCierreAnticipado` (fracción de la escala, default 0=off)
  cierra y felicita si una evaluación válida supera el umbral; y `DetectorIntencionContinuar` permite salir
  por intención del participante. La decisión de cierre/repregunta ya la modula el LLM dentro del tope de
  revisiones (`MaxRepreguntas`).
- **Diseño técnico:** trabajo de **calibración + parametrización**, no de build. (1) Fijar el default de
  `UmbralCierreAnticipado` (p. ej. `0.70`) tras el workshop de rúbrica. (2) **Decisión de producto abierta
  en el Timeline:** ¿umbral por campaña? Hoy es global (`Conversacion:*`). **Recomendación:** mantener
  global para el Hito 1 (menos superficie antes del freeze; no toca contratos `03`/`04`); dejar la
  granularidad por campaña/pregunta como mejora post-go-live. Registrar la decisión en `SUPUESTOS.md`.
- **Riesgo y mitigación:** *no determinismo: requiere pruebas* — el umbral y la binaria del LLM pueden
  cerrar demasiado pronto o tarde. Mitigación: el umbral es **determinista** (fracción de escala, auditable);
  cap duro de turnos (`MaxRepreguntas`, `maxLlamadasLlmPorUsuario` de P-10) garantiza terminación; batería
  de pruebas con respuestas de distinta calidad.
- **Aceptación:** con `UmbralCierreAnticipado=0.70`, una respuesta que supera el umbral cierra felicitando
  sin ofrecer más mejoras; una floja ofrece una mejora y cierra al agotar el cupo; nunca hay bucle infinito.

---

## 5. Frente C — Núcleo "tejido" (las dos grandes apuestas)

I-06 e I-09 son el corazón del producto y **el mayor riesgo técnico del Hito**. Se detallan con más
profundidad y con un *plan de degradación* explícito por si el tiempo aprieta antes del freeze.

### I-06 · Detección de múltiples ideas → N registros
- **Qué / por qué:** detectar varias ideas en una misma respuesta, separarlas en **registros distintos**
  (BD + Markdown) y recorrer la rúbrica **por idea**.
- **Estado hoy:** **nuevo** — hoy `Respuesta` guarda **1 idea por registro** (verificado en
  `Domain/Respuestas/Respuesta.cs`). El orquestador crea una `Respuesta` por mensaje entrante.
- **Riesgo declarado (Timeline):** **sobre-fragmentar** (partir una idea coherente en piezas triviales →
  inflar el conteo) o **sub-fragmentar** (fusionar dos ideas distintas → perder granularidad en la rúbrica).
  Es el riesgo LLM más delicado del hito.
- **Diseño técnico (paso de segmentación previo a la evaluación):**
  1. **Nuevo puerto** `ISegmentadorIdeas` en `Application` con un `SalidaSegmentacion` de contrato fijo:
     `{ "ideas": [ { "texto": "string", "resumen": "string" } ] }` (1..N). Implementación LLM
     (`SegmentadorIdeasLlm`) que reutiliza `ILlmClient` y la misma disciplina instrucción/dato.
  2. El orquestador, tras persistir el mensaje entrante, llama al segmentador **antes** de evaluar. Por
     **cada idea** crea una `Respuesta` (campo aditivo `ideaIndice`/`ideaDe` y `respuestaPadreId` para
     trazar el mensaje origen) y ejecuta el ciclo de evaluación por idea. El Markdown se compila **por idea**
     (un artefacto por `Respuesta`).
  3. **Guardas anti-fragmentación (deterministas):** (a) longitud mínima por idea (descarta fragmentos
     triviales); (b) tope máximo de ideas por mensaje (`Conversacion:MaxIdeasPorMensaje`, p. ej. 5) →
     si el LLM devuelve más, se registran las N primeras y se loguea `anomaliaLlm`; (c) si el segmentador
     falla o devuelve salida inválida → **fallback: 1 idea = el mensaje completo** (comportamiento actual,
     cero regresión).
- **Contrato/config:** cambios **aditivos** en `03` (campos `ideaIndice`, `respuestaPadreId` en `Respuesta`)
  → actualizar spec `03 §3.8` en commit aparte. Nueva config `Conversacion:MaxIdeasPorMensaje`,
  `Conversacion:LongitudMinimaIdea`.
- **Mitigación del no determinismo:** el segmentador **no evalúa ni califica**, solo separa; la calificación
  sigue el flujo validado. Las guardas de fragmentación son deterministas y auditables. Métrica de
  observabilidad: *ideas por mensaje* (distribución) para detectar sobre/sub-fragmentación en las pruebas.
- **Plan de degradación (si el tiempo aprieta):** si el segmentador no alcanza calidad antes del freeze, se
  **desactiva por flag** (`Conversacion:SegmentacionIdeas=false`) y el sistema opera en modo 1-idea, sin
  bloquear el Hito. El resto del núcleo (I-09) no depende de I-06.
- **Aceptación:** un mensaje con dos ideas claras produce **2 `Respuesta` + 2 Markdown + 2 evaluaciones**;
  un mensaje con una idea produce 1; un mensaje "Hola" no genera fragmentos triviales; con el flag off,
  comportamiento 1-idea idéntico al actual; test unit para 1/2/N ideas y para fallback.

### I-09 · Tejido colectivo: LLM con acceso a la base común
- **Qué / por qué:** cada conversación se enriquece con la **base de conocimiento común** de la campaña
  (no autocontenida): el coach usa aportes de otros expertos. **Es el corazón del "tejido".** Depende de I-11.
- **Estado hoy:** **nuevo** — hoy el LLM usa **solo el historial del propio hilo** (`HistorialReciente`).
  No existe recuperación sobre la base común (sin embeddings/vector store).
- **Diseño técnico (recuperación acotada, MVP-proporcional):**
  1. **Nuevo puerto** `IBaseConocimientoCampania` en `Application`:
     `Task<IReadOnlyList<AporteRelevante>> RecuperarAsync(campaniaId, textoConsulta, tags, topK, ct)`.
  2. **Implementación MVP sin vector store nativo:** dado el volumen esperado (participantes de una
     convención, no millones), **recuperación por similitud liviana** sobre los Markdown/resúmenes de
     `responses` de la campaña: filtrado por `campaniaId` (partición Cosmos) + `tags` (I-14), ranking por
     solapamiento léxico/keywords y recencia. **Opción B (si hay tiempo/calidad):** embeddings vía el
     proveedor LLM configurado, guardados como campo aditivo en `responses`, con similitud coseno en memoria.
     Elegir **A por defecto** (cero dependencia nueva, determinista, auditable); B queda como mejora
     activable por flag `Conversacion:RecuperacionSemantica`.
  3. El orquestador inyecta los **top-K aportes relevantes** (resúmenes, anonimizados o atribuidos según
     P-07) como un **mensaje `system` "aportes de la comunidad"**, separado de rúbrica, seed thoughts y de
     la respuesta del usuario. Acotado por tokens (P-10).
  4. **Parametrización por campaña (habilita I-10):** un flag decide si el coach parte de la base común
     (tejido) o de página en blanco.
- **Contrato/config:** aditivo. Nueva config `Conversacion:TejidoColectivo` (on/off por campaña vía I-10),
  `Conversacion:TopKAportes`, `Conversacion:RecuperacionSemantica`. Si se usa embeddings, campo aditivo
  `embedding` en el doc de `responses` (spec `03 §3.8`, commit aparte).
- **Riesgo declarado (alto, "corazón del tejido") y mitigación:** (a) *fuga/PII entre participantes* → los
  aportes inyectados son **resúmenes** sin datos personales; se respeta el consentimiento (P-07); regla dura
  de no incluir números/nombres. (b) *prompt-injection cruzado* (una respuesta maliciosa contamina a otros)
  → los aportes recuperados son **dato no confiable** en `system` con la instrucción anti-inyección; la
  salida se valida igual. (c) *coste/latencia* → top-K pequeño (p. ej. 3–5), recuperación local, acotado por
  tokens. (d) *ruido/irrelevancia* → filtro por tags + umbral mínimo de similitud; si no hay aportes
  relevantes, se cae a conversación autocontenida (degradación limpia).
- **Plan de degradación:** si la recuperación no rinde antes del freeze, `TejidoColectivo=false` deja el
  sistema en modo autocontenido (comportamiento actual, probado). El Hito no depende de I-09 para entregar,
  pero I-09 es **lo que da valor diferencial**; por eso está en ruta crítica con prioridad.
- **Aceptación:** en una campaña con base poblada, una conversación nueva recibe aportes relevantes de otros
  participantes en el contexto y el coach los teje sin exponer PII; con `TejidoColectivo=false` la
  conversación es autocontenida; ninguna salida filtra datos personales; test unit del puerto de
  recuperación (filtrado por campaña/tags, top-K, fallback vacío).

### I-10 · Parametrizar campaña: base previa vs. papel en blanco
- **Qué / por qué:** elegir por campaña si el coach parte del conocimiento ya construido o de página en
  blanco; habilita otros casos de uso (ARMA). Depende de I-09.
- **Estado hoy:** la config de campaña existe; **falta el flag**.
- **Diseño técnico:** exponer `TejidoColectivo` (definido en I-09) como **campo de configuración de campaña**
  editable en el portal (pestaña Configuración del detalle de campaña, que ya usa tabs reales). Aditivo a
  `03`/`04` (campo opcional con default seguro). El orquestador lo lee para decidir si invoca
  `IBaseConocimientoCampania`.
- **Riesgo y mitigación:** *medio* — bajo; es un flag. Mitigación: default = página en blanco (seguro);
  activar tejido es decisión explícita del admin.
- **Aceptación:** una campaña con el flag on usa la base común; con off, no; el cambio se refleja sin
  redeploy (config de campaña).

---

## 6. Frente D — Operación y seguridad de producción

Lo que convierte un MVP demostrable en algo **operable el día de la convención**.

### I-08 · Carga masiva de participantes vía Excel
- **Qué / por qué:** subir la lista de participantes en lote (Excel) desde el portal, en vez de uno a uno.
  Depende de la **lista final de GHT** (Insumo #5, límite 1-ago).
- **Estado hoy:** **nuevo** (Action Item de la reunión). Hoy el alta es individual.
- **Diseño técnico:** (1) endpoint `POST /api/admin/participantes/carga-masiva` (multipart) que parsea el
  Excel server-side, **normaliza E.164** (reutiliza `NormalizadorNumero`), valida unicidad por
  `whatsappNormalizado`, aplica tags, y devuelve un **reporte por fila** (creado/actualizado/rechazado +
  motivo). (2) Pantalla en el portal (subir archivo + preview + confirmación con toast, patrón ya usado).
  (3) Idempotencia: reejecutar la carga no duplica (upsert por número). Actualizar contrato `04 §5` (commit
  aparte).
- **Riesgo y mitigación:** *depende de lista final* → aceptar formato de plantilla Excel definido por
  nosotros y entregado a GHT en Sprint 1; datos sucios (números mal formados, duplicados) → validación por
  fila con reporte, no falla todo el lote. Sin PII en logs.
- **Aceptación:** cargar un Excel de N filas crea N participantes con tags; filas inválidas se reportan sin
  abortar el lote; recarga idempotente; test unit del parser/normalización.

### I-14 · Refinar segmentación de participantes por tags
- **Qué / por qué:** refinar tags/áreas (analítica, transformación digital, innovación…) para clasificar y
  filtrar aportes. Alimenta la recuperación de I-09 y el dashboard.
- **Estado hoy:** **ya existe** (tags); refinar para la convención.
- **Diseño técnico:** trabajo de **datos/config** + pequeño refinamiento: consolidar el catálogo de tags con
  GHT; asegurar que la carga masiva (I-08) los aplique; que `tagsSnapshot` de `Respuesta` los capture al
  responder (ya implementado). Sin cambio de contrato.
- **Riesgo y mitigación:** *bajo*. Alinear taxonomía con GHT en Sprint 2.
- **Aceptación:** los participantes cargados tienen tags del catálogo acordado; los aportes se pueden filtrar
  por tag en Resultados.

### I-16 · Fix: calificación no reflejada en resumen/Markdown
- **Qué / por qué:** en la demo, al reusar una campaña vieja no se guardó la nueva calificación en el
  resumen/Markdown. Verificar y corregir.
- **Estado hoy:** **por verificar** — posible efecto de campaña determinística previa.
- **Diseño técnico:** reproducir con simulación (limpiar `conversations`/`responses` del participante),
  confirmar si la regeneración de Markdown toma la **última evaluación válida**. El compilador ya
  incrementa versión y guarda contenido canónico; revisar que el orquestador dispare la compilación tras la
  evaluación correcta y que no se lea un artefacto cacheado de la campaña previa. Añadir test de regresión.
- **Riesgo y mitigación:** *bajo* pero **visible en demo** → priorizar en Sprint 1a. Mitigación: test unit
  que verifique `calificacion_total` del Markdown == última `Evaluacion` válida.
- **Aceptación:** el Markdown refleja la calificación de la última evaluación; test de regresión verde.

### P-03 · Endpoint admin "reabrir/reiniciar conversación"
- **Qué / por qué:** reabrir el hilo de un participante sin tocar Cosmos a mano; **reduce fricción de
  pruebas** (hoy hay que borrar docs manualmente en cada re-prueba — deuda declarada en `AVANCES`).
- **Estado hoy:** **nuevo** (ya sugerido). Necesario para las pruebas conjuntas del 4–8 ago.
- **Diseño técnico:** endpoint `POST /api/admin/campanias/{id}/participantes/{uid}/reiniciar` (rol admin +
  CSRF) que cierra/elimina lógicamente la `Conversacion` abierta del participante y sus `responses` de la
  campaña, permitiendo un nuevo cold-start. Registrar en `LogSeguridad`. Botón en el portal (Envíos/Resultados).
- **Riesgo y mitigación:** *bajo* — destructivo por diseño; gated admin, con confirmación en UI y log.
- **Aceptación:** reiniciar un participante permite volver a enviarle la pregunta y rehacer el flujo sin
  tocar Cosmos; queda `LogSeguridad`; test integration.

### P-07 · Bienvenida/consentimiento y manejo de datos
- **Qué / por qué:** mensaje de bienvenida que explique qué es y **cómo se usan los datos** (privacidad)
  antes de abrir a participantes reales. Relevante porque I-09 comparte aportes entre participantes.
- **Estado hoy:** **nuevo**.
- **Diseño técnico:** (1) el `MensajeInicial` de campaña incluye el aviso de privacidad/consentimiento
  (texto configurable, sin código). (2) Registrar el **consentimiento** al primer entrante como campo
  aditivo en la conversación/participante (`consentimientoAceptadoEn`). (3) Regla para I-09: solo se tejen
  aportes de participantes cuya campaña declara el uso colectivo (default anonimizado). Coordinar el copy
  legal con GHT.
- **Riesgo y mitigación:** *bajo técnico, medio de cumplimiento* — al compartir aportes debe haber base de
  consentimiento. Mitigación: anonimización por defecto en I-09; consentimiento explícito en el arranque;
  decisión de privacidad registrada en `SUPUESTOS.md`.
- **Aceptación:** el primer mensaje explica el uso de datos; el consentimiento queda registrado; los aportes
  tejidos respetan la política acordada.

### P-10 · Guardrails y cupos para producción abierta
- **Qué / por qué:** límites/cupos de LLM y **control de costos** antes de abrir a todos. Deuda técnica
  declarada: faltan `maxMensajesPorUsuario`/`maxLlamadasLlmPorUsuario` y rate por número WhatsApp.
- **Estado hoy:** **parcial** — hay rate limiting HTTP, longitud máx. de mensaje y tope de repreguntas;
  **falta** el cupo explícito por usuario/campaña y el **cupo de costo LLM**.
- **Diseño técnico:** completar `IGuardrails` (`10 §2`): (1) contadores por `usuario+campaña` en Cosmos para
  `maxMensajesPorUsuario` (default 10) y `maxLlamadasLlmPorUsuario` (default 2); al exceder → cierre/fallback
  controlado + `LogSeguridad(rateLimit)`. (2) **Cupo de costo LLM** por campaña: acumular tokens/costo
  aproximado (ya se miden en observabilidad `08 §7`) y **alerta + corte configurable** al superar umbral.
  (3) rate por número WhatsApp. Con I-06 (multi-idea) el conteo de llamadas LLM sube (1 segmentación + N
  evaluaciones) → el cupo debe contemplarlo (contar segmentación aparte y cap por mensaje).
- **Riesgo y mitigación:** *costo LLM* — abrir a todos sin cupo puede disparar gasto, agravado por I-06/I-09.
  Mitigación: cupos duros + alertas de gasto en App Insights + `MaxIdeasPorMensaje`/`TopKAportes` acotados.
- **Aceptación:** un usuario que excede el cupo recibe cierre controlado y queda `LogSeguridad`; al superar
  el umbral de costo de campaña se dispara alerta (y corte si configurado); test unit de los contadores.

### P-09 · Panel de monitoreo en vivo (día-D)
- **Qué / por qué:** salud de envíos/entregas/errores en tiempo real para operar el 10-ago.
- **Estado hoy:** **parcial** — existen logs de entrega y el `TrabajadorWebhook` loguea estados
  (`sent/read/failed` con code).
- **Diseño técnico:** (1) para el Hito basta un **dashboard de App Insights** (workbook) con: envíos por
  estado, tasa de entrega, errores por code (131042 y otros), latencia y tasa de fallback LLM, tokens/costo.
  (2) Opcional (si hay tiempo): pantalla mínima en el portal que consuma `GET .../envios` + métricas. Preferir
  el workbook para no meter código nuevo antes del freeze.
- **Riesgo y mitigación:** *bajo* — es observabilidad; ya hay señales. Mitigación: preparar el workbook en la
  semana de pruebas para validarlo antes del día-D.
- **Aceptación:** el día del envío se puede ver en tiempo casi real cuántos mensajes salieron, cuántos se
  entregaron y qué errores hubo, sin entrar a Cosmos.

---

## 7. Riesgo R-01 — Delegación de control del flujo al LLM

> **Este es el riesgo estructural del Hito 1.** Con las nuevas iniciativas, el LLM deja de ser un
> *productor de datos* (calificar, sugerir) y pasa a ser *árbitro de la lógica de negocio*: decide dónde
> hay ideas distintas (I-06), si una respuesta cumple la rúbrica y cuándo cerrar (I-01), y qué aportes de
> otros tejer en la conversación (I-09).

### 7.1 Registro de riesgo

| Campo | Contenido |
|---|---|
| **ID** | R-01 |
| **Categoría** | Integridad del sistema / gobernanza del flujo (no es "calidad de output") |
| **Descripción** | Se delega en un componente **no determinista y no auditable** el control de decisiones de negocio: segmentación de ideas, cierre por umbral de rúbrica y tejido colectivo. |
| **Advertencia (una frase)** | *"Al delegar en el LLM decisiones de flujo (segmentación de ideas, cierre por rúbrica, tejido colectivo), el comportamiento del sistema deja de ser determinista y reproducible: el mismo insumo puede producir recorridos, puntajes y cierres distintos, comprometiendo la auditabilidad, la uniformidad de trato y las garantías operativas."* |
| **Iniciativas afectadas** | I-01, I-03, I-06 (alto), I-09 (alto) |
| **Impacto** | Alto — no reproducibilidad, pérdida de garantías (terminación, cupos, trato uniforme), superficie de falla acoplada a la estructura de datos. |
| **Probabilidad** | Alta si no se acota (inherente a los LLM). |
| **Estrategia** | **Mitigar y acotar** (no eliminar: el no determinismo es *lo que da valor al coach*). |

**Dos sub-riesgos, se mitigan distinto:**

- **R-01a · No determinismo de control** — el LLM decide transiciones de la máquina de estados (dónde
  hay N ideas, cuándo cerrar). Consecuencia: recorridos no reproducibles, terminación no garantizada.
- **R-01b · No determinismo evaluativo** — la misma respuesta puede recibir distinta calificación en
  corridas distintas. Consecuencia: trato no uniforme entre participantes, difícil de auditar.

**Consecuencias concretas si no se controla:** no se puede depurar ni defender ante GHT por qué a un
participante se le cerró el hilo y a otro no; no se puede *garantizar* cupos ni costo; un mal turno del
modelo se propaga a la estructura de datos (N registros inflados, hilos que no cierran), no solo al texto.

### 7.2 Principio rector de la mitigación

**El LLM propone, el sistema dispone.** El modelo genera *evidencia* (una calificación, una lista de ideas
candidatas, una repregunta sugerida); una **máquina de estados determinista sigue siendo la dueña de las
transiciones**. Nunca se deja que el LLM *ejecute* la decisión: produce el dato y el código decide sobre ese
dato. El no determinismo se **acota dentro de límites deterministas y medibles**, no se elimina.

### 7.3 Salvaguardas (mapeadas a iniciativas y sub-riesgo)

1. **Decisiones binarias en código, no en el modelo** — *(R-01a; I-01, I-03, I-06, I-09)*. El LLM no
   "decide cerrar": devuelve `calificacion_total` y el código compara contra `UmbralCierreAnticipado`. El
   eje débil lo calcula el servidor sobre `calificacion_por_criterio`. Las ideas del segmentador se
   confirman con guardas deterministas (longitud mínima, tope). Regla dura, sin excepción.
2. **Caps duros e invariantes que el modelo no puede violar** — *(R-01a; P-10)*: tope de turnos
   (`MaxRepreguntas`), de ideas por mensaje (`MaxIdeasPorMensaje`), de llamadas LLM por usuario/campaña.
   Garantizan **terminación y costo acotado** aunque el modelo se comporte mal.
3. **Contrato de salida estructurado + validación + fallback determinista** — *(R-01a/b; `08 §4/§6`)*: JSON
   con esquema fijo (claves + escala **incrustadas en el system prompt**); si no valida, se cae a un camino
   conocido (1 idea = mensaje completo, retro neutra, cierre limpio). Cero regresión.
4. **Reproducibilidad por diseño** — *(R-01b; `08 §7`)*: snapshot de `rubrica+versión`, `prompt+versión`,
   `modelo` y `parámetros` en cada `Evaluacion` (ya existe); además **temperatura baja/fija** y, donde el
   proveedor lo permita, **`seed`**, para reducir la varianza evaluativa.
5. **El no determinismo se valida por muestreo y evals, no por igualdad exacta** — *(R-01b; pruebas 4–8 ago)*:
   correr un set fijo de respuestas por N iteraciones y medir **dispersión** de puntaje y de decisión de
   cierre; definir una **tolerancia aceptable** antes del freeze (p. ej. "la decisión de cierre coincide en
   ≥90% de las corridas"; "el puntaje varía ≤1 punto de escala"). Convierte R-01 en algo medible.
6. **Anti prompt-injection arquitectónico** — *(I-09; `08 §5`, `ARQ §12`)*: separación `system`/`user`;
   respuesta del usuario, seed thoughts y aportes recuperados etiquetados como **dato no confiable**; la
   salida también es dato (el sistema no ejecuta acciones que el modelo "pida"; nunca promete implementar).
   Crítico en el tejido (I-09), donde una respuesta maliciosa podría contaminar a otros.
7. **Flags con degradación a modo determinista** — *(todas; freeze)*: `SegmentacionIdeas`, `TejidoColectivo`,
   `RecuperacionSemantica`, `UmbralCierreAnticipado` apagables. Si en pruebas la varianza es inaceptable, se
   apaga la pieza no determinista y el sistema opera predecible. **El Hito nunca depende de que el modelo
   "salga perfecto".**
8. **Human-in-the-loop donde importa** — *(I-11, I-12, I-13; P-07)*: la config crítica (rúbrica, seed
   thoughts, umbral) la fija y aprueba un humano por el portal; la base de conocimiento se revisa post-hoc,
   no se auto-publica.
9. **Observabilidad de las decisiones, no solo de los tokens** — *(P-09; `10 §6`)*: distribución de *ideas
   por mensaje* (detecta sobre/sub-fragmentación), tasa de cierre por umbral vs. por cupo, tasa de fallback,
   costo/latencia; `correlationId` por conversación en toda la cadena. Son las alarmas tempranas en pruebas
   y el día-D.

### 7.4 Cómo se presenta a GHT

No se elimina el no determinismo —es inherente y es *lo que da valor al coach*—; se **acota dentro de
límites deterministas y medibles**. El modelo tiene libertad conversacional; **no** tiene libertad para
violar cupos, saltarse el cierre, ni tomar decisiones que no queden auditadas. El riesgo residual aceptado
se cuantifica con las tolerancias de §7.3.5 antes del freeze.

---

## 8. Plan de pruebas y aceptación del Hito (semana 4–8 ago)

Alineado con `13_Plan_de_Pruebas_y_Aceptacion.md`. Tres niveles:

- **Unit + integration (continuo, en CI):** cada iniciativa entrega sus pruebas; se mantienen verdes las
  ~264 existentes. Backend: `dotnet build -c Release -warnaserror` + `dotnet test -c Release` +
  `dotnet format --verify-no-changes`. Frontend: `lint` + `ng test` + `ng build --configuration production`
  (Node temporal 24.15.0).
- **E2E simulado (sin Meta):** por `/simulacion-whatsapp` con webhook firmado, recorrer:
  cold-start → pregunta → respuesta multi-idea → evaluación por idea → tejido colectivo → parafraseo →
  invitación a mejorar → cierre por umbral → Markdown por idea. Verificar en Resultados.
- **Pruebas robustas conjuntas (Felipe/Munir/Jason):** validación humana de tono, relevancia de seed
  thoughts, calidad de la segmentación (sobre/sub-fragmentación) y del tejido. Aquí se calibran umbral,
  top-K y `MaxIdeasPorMensaje`.
- **Dry-run E2E real (8–9 ago):** con billing Meta resuelto y plantilla aprobada; envío a un grupo reducido;
  limpieza de transacciones de prueba en BD; revisión de infraestructura; `/health/ready = ok`.

**Definition of Done del Hito:** todos los ítems de la ruta crítica en verde (o degradados por flag con
decisión registrada), P-01/P-02 resueltos, dry-run exitoso, alcance congelado y lista real cargada.

---

## 9. Congelación de alcance y go-live (8–10 ago)

1. **Freeze (8 ago):** se cierra el set de flags de producción (`SegmentacionIdeas`, `TejidoColectivo`,
   `UmbralCierreAnticipado`, cupos P-10), se congela la versión de rúbrica y de prompts, se cargan los
   seed thoughts finales.
2. **Carga real + dry-run (8–9 ago):** carga masiva de la lista final (I-08), reinicio de datos de prueba
   (P-03), ensayo end-to-end, revisión de infraestructura.
3. **Hito (10 ago):** envío del mensaje de inicio desde el portal; monitoreo en vivo (P-09); runbook de
   incidentes (si 131042 reaparece, si sube el fallback LLM, si el costo se dispara → apagar tejido/seg.).

---

## 10. Fuera de alcance del Hito 1 (rama deseable — solo referencia)

Post-go-live, sobre la misma estructura, sin bloquear el 10-ago (Línea de Tiempo 2 del plan):
**P-04** dashboard/analítica de resultados · **P-11** informe consolidado de la base de conocimiento ·
**P-08** recordatorios/nudges · **P-05** capa de Insights (yuxtaposición thought→insight→meaning, se apoya
en la recuperación de I-09) · **P-06** post-procesamiento/destilación por lotes (idea de Munir) ·
**I-15** rebranding a Bright Insights · **P-12** ARMA como campaña/módulo (reunión aparte). El diseño de
I-09/I-10 (recuperación + parametrización por campaña) se hizo **pensando en habilitar** P-05 y P-12 sin
reescritura.

---

## 11. Insumos requeridos de GHT (bloquean la ruta crítica)

| # | Insumo | Responsable | Bloquea | Límite |
|---|---|---|---|---|
| 1 | Priorización de iniciativas ("lo básico") | Felipe/Munir | Congelar alcance | 14 jul |
| 2 | Seed thoughts (productividad, ingresos, otros ejes) | Felipe | I-12, I-04 | 18 jul |
| 3 | Feedback/validación de la rúbrica (workshop) | Felipe/Munir/equipo | I-11 → I-03/I-09 | 18 jul |
| 4 | Decisión rúbrica agnóstica vs. tailored | Felipe/Munir | I-13 | 25 jul |
| 5 | Lista final de participantes | GHT | I-08, I-14 | 1 ago |
| 6 | Gestión del billing Meta (131042) | GHT + Aliado TI | **Todo el Hito** | **YA (Sem 0)** |
| 7 | Fecha exacta de la convención | GHT | Backtracking | 14 jul |

---

## 12. Decisiones de arquitectura que este plan propone (para registrar en `SUPUESTOS.md`)

1. **Rúbrica agnóstica + relevancia por seed thoughts/tags** (I-13), no tailored por coyuntura.
2. **Umbral de cierre y cupos globales en `Conversacion:*`/`Seguridad:*`** para el Hito 1 (no por
   campaña/pregunta), para no tocar contratos `03`/`04` antes del freeze; granularidad = post.
3. **Recuperación del tejido (I-09) por similitud liviana local por defecto**, embeddings detrás de flag;
   top-K pequeño; aportes anonimizados.
4. **Cada comportamiento LLM nuevo detrás de flag con degradación a modo autocontenido/1-idea**, de modo que
   el Hito nunca dependa de que un comportamiento no determinístico "salga perfecto".
5. **Cambios de contrato solo aditivos** (`parafraseo_devuelto` en `08 §4`; `ideaIndice`/`respuestaPadreId`,
   `embedding`, `consentimientoAceptadoEn` en `03`), cada uno en commit aparte que actualiza la spec.

---

*Fin del documento — Plan de Hito 1. Preparado por Aliado TI (Arquitectura + equipo de desarrollo El Tejido).*
