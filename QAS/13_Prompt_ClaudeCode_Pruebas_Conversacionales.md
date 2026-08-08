# 13 — Prompt para Claude Code: pruebas E2E CONVERSACIONALES contra Azure (ventana limpia)

> Pega el bloque "INICIO/FIN DEL PROMPT" en una terminal nueva de Claude Code (`claude --model fable`,
> `/model fable`, opcional `/effort high`). Objetivo: validar que **todo el flujo conversacional y sus
> alternativas** funcionan en el entorno desplegado. Detalle completo: `QAS/10 §4` y `§8`.

## Antes de pegar (lo hace el HUMANO en Azure App Settings)
- `Simulacion__Habilitada=true` y (para probar sus casos) `Conversacion__DespertarProactivoHabilitado=true`,
  `Conversacion__RetomarIdeasHabilitado=true`, `Conversacion__CierrePorTiempoHabilitado=true`,
  `Conversacion__ClasificacionIntencionControl=true`, `Conversacion__MinutosInactividadSesion=2`,
  `Conversacion__IntervaloRevisionMinutos=1` (para que E10 cierre en ~1 min; default 15). Verifica que las
  listas `Conversacion__Frases…` estén en claves **indexadas** (`__0`, `__1`, …), no todo el listado en un value.
- ⚠️ **App Service Linux: usa doble guion bajo `__`, no dos puntos `:`.** Otros documentos de QAS aún
  escriben `Simulacion:Habilitada`; en Linux esa forma **no** mapea a la configuración anidada y el
  endpoint responde `404` como si no existiera. Si ya tienes la simulación funcionando, no la toques:
  reutiliza la notación que te está funcionando.
- Estos flags están **apagados por defecto a propósito** y encenderlos es una decisión consciente para
  la ventana de prueba. **Apágalos al terminar** (`07_Runbook_Rollback_Contingencia.md`).
- DT-QA-01 desplegado (con el fix del log a Cosmos). Verifica el portal en el navegador (debe funcionar).

> **Estado del entorno (2026-08-07).** La base se recreó por `I-08 v2` y se purgaron campañas,
> conversaciones, respuestas y participantes: el entorno ya está en la **ventana limpia** que este
> documento pide. `config` (rúbricas, prompts, ConfigLLM) **no** se tocó, así que los artefactos que
> nombra la preparación siguen existiendo — confírmalo antes de crear la campaña.
>
> **Dos cambios de `I-08 v2` que afectan a estas pruebas:**
> 1. **Un número cuyo único registro está `inactivo` ya NO resuelve participante** y cae en el rechazo
>    neutral. Es el comportamiento correcto, no un fallo: no lo reportes como defecto.
> 2. Crear participantes es más simple: solo `nombre` y `numero` son obligatorios (`area` y `empresa`
>    dejaron de serlo). Cada uno recibe un `codigoUsuario` (`U-000042`) que asigna el servidor.
>
> **P-31 no está en el catálogo de abajo** (es posterior a este documento) y sus flags siguen
> apagados. Su prueba va aparte: `14_P31_Resumen_Consolidacion_Como_Probar.md`.

## ▼ INICIO DEL PROMPT — copia desde aquí ▼

Eres Claude Code actuando como ingeniero de QA/SDET senior. Objetivo: **verificar de punta a punta que
el flujo conversacional del coach y todas sus alternativas funcionan** en el entorno DESPLEGADO. Lee y
sigue `QAS/10_Guia_E2E_Ejecutable_Agente_o_Humano.md` (en especial §2.1 respuestas de prueba, §4
catálogo, §5 reinicio y §8 modalidad Azure) y `QAS/04_Datos_de_Prueba_y_Reinicio.md`.

**Regla de oro:** si algo falta, no está claro o no puedes obtener un acceso, DETENTE y pregúntame. No
inventes secretos, URLs ni resultados; no marques PASS sin evidencia. No hagas push/despliegue/cambios
remotos. Declara en una línea qué vas a probar y desde qué rol decides; propón un plan corto antes de ejecutar.

### Acceso y transporte
- Base URL: `https://<tuapp>.azurewebsites.net` — confirma que responde cargando una PÁGINA del portal (no solo `/health`).
- Header `X-Diag-Key`: `<clave de diagnóstico>`. **No** uses ni pidas `wa-appsec` (App Secret real de Meta).
- **Transporte OBLIGATORIO: Playwright.** Haz TODAS las llamadas (portal, `/api/admin/*` y el endpoint de
  inyección) con Playwright (`page.request` / `fetch` en la página). NO uses PowerShell/Invoke-RestMethod
  (genera 400 intermitentes y corrompe UTF-8). Instala Chromium si falta (`npx playwright install chromium`).

### Simular mensajes entrantes (sin App Secret)
`POST {URL}/diagnostico/simulacion/webhook-entrante` con header `X-Diag-Key` y body
`{ "numero": "...", "texto": "..." }` → 200; se procesa como un webhook real. Si responde 404 o 500,
detente y avísame.

> ⚠️ **Nunca repitas el mismo texto desde el mismo número.** Sin `whatsappMessageId`, el id del mensaje
> se **deriva del payload**, así que un texto idéntico se trata como reentrega de WhatsApp y **se
> descarta en silencio**: responde `200`, no pasa nada y parece que el sistema está roto. Verificado el
> 2026-08-08 (costó dos turnos diagnosticarlo). Cambia el texto en cada envío, o manda
> `whatsappMessageId` explícito.

> ⚠️ **El primer entrante de un participante activa el cold-start**: recibe la pregunta y **no** se
> registra como aporte. El aporte real va en el **segundo** mensaje.

### Preparación (una vez; no es prueba puntuada)
1. Con el diagnóstico crea admin (`/diagnostico/simulacion/admin-inicial`, número `573001119999`), emite
   OTP (`/otp-admin`, `123456`) y entra por `/login` → `/api/auth/me`.
2. Crea 5 participantes activos: `573001112301`..`573001112305`. (Cuida el UTF-8 de las tildes; con
   Playwright no debería haber problema.)
   ⚠️ **No uses el rango `5730011122xx`:** es el de los archivos de carga masiva (`QAS/datos/`). Si esas
   pruebas ya corrieron, esos números tienen titular y el alta responde `409`. Rangos separados para
   que el orden de ejecución no importe.
3. ⚠️ **Crea DOS campañas, no una** (verificado 2026-08-08): con `segmentacionIdeas=true` incluso la
   «idea fuerte» de `QAS/10 §2.1` se parte en dos ideas, así que **E5/E6/E9 no se pueden evaluar** en una
   campaña segmentada.
   - **Campaña A — sin segmentación** (`segmentacionIdeas=false`, `coachingSecuencialIdeas=false`): E5,
     E6, E9, E14, E18.
   - **Campaña B — con segmentación** (ambos `true`): E7, E8, E13.
   **Cada participante debe estar en UNA sola campaña.** Si comparte dos campañas activas, P-26 abre el
   menú de selección en cada entrante y contamina todos los casos. Usa rangos de números disjuntos.
   ⚠️ **`minutosInactividadSesion` de la campaña gana sobre el App Setting global.** Ponlo en ~20 en
   **ambas** campañas mientras pruebas E11/E13/E14, o la conversación se cierra entre turnos (cada turno
   con LLM tarda ~40 s) y esos casos dan falso FAIL.

   Crea cada campaña **reutilizando** lo existente: rúbrica
   `rúbrica OpenBrain v3.4`, prompt `Evaluación con rubrica OpenBrain Thought-Scoring`, config LLM
   `OpenRouter-Terra`. Preguntas: 1) «¿Cómo aumentarías los ingresos de tu área?» 2) «¿Dónde ves
   oportunidades de reducir costos?» 3) «¿Qué mejoraría la productividad del equipo?». Mensaje inicial
   `Hola {{nombre}}, comparte tu idea.` Asocia los 5 participantes y pon la campaña **activa**.
4. Flags POR CAMPAÑA en esa campaña: `participacionContinua=true`, `segmentacionIdeas=true`,
   `coachingSecuencialIdeas=true`, `minutosInactividadSesion=2`, `clasificacionIntencionControl=true`
   (opt-in de P-27; el clasificador LLM necesita además el flag global), y un umbral de madurez. (Los
   flags globales los encendió el humano; si alguno falta, detente y avísame.)

### Qué validar — el flujo conversacional y sus alternativas
Ejecuta y valida (por `/api/admin/*`) estos casos del catálogo de `QAS/10 §4`, usando las respuestas de
`QAS/10 §2.1`. Reinicia por participante (`QAS/10 §5`) solo si un caso necesita cold-start:

- **E5 — Camino feliz:** idea fuerte → consolidación, evaluación con rúbrica, retro breve, idea **madura**, Markdown sin secretos.
- **E6 — Rama débil:** idea floja en un eje → **una** repregunta enfocada; no revela rúbrica.
- **E9 — Madurez:** idea fuerte vs floja → clasifica **maduro/incubación** por umbral.
- **E7 — Multi-idea:** mensaje con 3 ideas → registros independientes.
- **E8 — Coaching secuencial:** con multi-idea, trabaja **una idea a la vez**.
- **E14 — Intención de control:** requiere el flag global `ClasificacionIntencionControl` **y** el opt-in
  por campaña `clasificacionIntencionControl=true`. Prueba con las frases exactas de alias
  («quiero parar aquí», «quiero pasar a otra idea», «stop now») que funcionan sin flag, y también con una
  variante («paremos aquí») que solo el clasificador LLM debería captar. Se tratan como **control**, no como contenido.
- **E10 — Cierre por tiempo:** aportar y no responder ~2 min → cierra por inactividad con **mensaje de pausa** humano; idea reanudable.
- **E12 — Despertar:** requiere estado **DORMIDO** (participante sin trabajo pendiente ni idea activa).
  Secuencia: primero completa/cierra su idea; luego envía «hola» (o «buenas») → el coach saluda y ofrece
  continuar/crear. Ojo: un participante con preguntas pendientes que dice «hola» recibe la primera
  pregunta (flujo base) — eso es correcto, no un fallo.
- **E11 — Participación continua:** cerrar una idea y aportar otra → **ciclo nuevo** independiente (ideaId distinto).
- **E13 — Retomar:** requiere una **idea previa CERRADA** en esa campaña/pregunta. Secuencia: crea y
  cierra una idea; luego envía la frase de alias exacta **«quiero volver a la anterior»** → reabre la
  **misma** idea (mismo ideaId). Sin idea previa no hay qué retomar (lo trataría como contenido).
- **E2 — No matriculado:** mensaje desde `573009990000` → rechazo neutral; no revela campañas.
- **E18 — Seguridad:** injection directa + pedir puntaje → no revela rúbrica/puntaje ni secretos; ignora la injection.
- **E19 — DT-P27-01:** **no hay que configurar nada**; las dos listas ya están en App Settings desde el
  2026-08-05 (23 frases para finalizar idea, 21 para participación), frente a 6 y 6 compiladas. Para que
  el caso pruebe algo, usa una frase que exista **solo en la configuración** —«cerremos esta idea»,
  «paremos aqui», «terminemos esta idea»— sobre una idea **activa**. Si usas una que también es default
  (p. ej. «quiero pasar a otra idea»), el caso no distingue si la lista vino de config o del código.

### Resultados
Crea `QAS/resultados/Resultados_E2E_Conversacional_<fecha>.md` con el formato de `QAS/10 §6`: cabecera de
entorno (ejecutor "Claude Code / Fable 5", URL, flags), tabla `# | Caso | Estado (PASS/FAIL/BLOCKED) |
Evidencia (ideaId/conversacionId, Markdown, captura) | Observaciones`, y un resumen. **NO borres datos al
terminar** (los reviso). Cierra con una revisión breve (≤8 líneas) y la lista de dudas/pendientes.

### Precondiciones de estado (E12/E13/E14) — obligatorio antes de puntuar
Estos tres dependen del **estado** de la conversación, no solo de la frase. Probar fuera de estado da
falso FAIL. Detalle en `QAS/10 §2.2`.
- **E14:** la frase de control solo se honra con **idea activa** y estado `EsperandoRepregunta` /
  `EsperandoConfirmacionSalida` (justo tras aportar y recibir la repregunta o la propuesta de mejora).
  Enviada antes = aporte (correcto). Aplica a alias y clasificador LLM.
- **E12:** estado **dormido** (sin pregunta pendiente) **y** frase **exacta** del diccionario (`hola`, no
  «Hola, ¿cómo sigo?»). Verifica que `Conversacion__FrasesDespertarProactivo` esté en claves indexadas
  (`__0`, `__1`, …) y no todo el listado en un value.
- **E13:** requiere **idea consolidada cerrada** previa (no una simple conversación cerrada) y consolidación
  activa; recién ahí el alias exacto reabre el mismo `ideaId`.

### Importante
- No marques PASS sin evidencia. Un FAIL describe en una línea qué se esperaba y qué pasó.
- Antes de reportar «feature ausente»: confirma que E12/E13/E14 se probaron **en el estado de §2.2** y que
  las listas de frases están **indexadas** en App Settings. Si aun así se comportan como apagado, repórtalo
  con evidencia (busca el evento `despertarProactivo` / `clasificacionIntencionControl` en el log; si no
  aparece, la condición no se cumplió).

## ▲ FIN DEL PROMPT ▲
