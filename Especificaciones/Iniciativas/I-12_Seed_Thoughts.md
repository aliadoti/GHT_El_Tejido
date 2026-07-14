# I-12 — Pensamientos semilla (seed thoughts) embebidos en el prompt

> **Origen:** hoja `Iniciativas` (Action Item; el contenido lo entrega Felipe/GHT, límite 18-jul).
> **Tipo:** Config/Prompt + Desarrollo aditivo · **Prioridad:** Alta · **Ventana:** Sprint 2 ·
> **Dependencia:** entrega de GHT · **Riesgo:** insumo tardío; costo de tokens.
> Cubre REQ §15/§21, ARQ §4.2/§12; specs base `03 §3.3`, `08 §3`.

## 1. Qué pide GHT / por qué
Incorporar material de las charlas de la convención y pendientes del año pasado ("seed thoughts":
productividad, ingresos y otros ejes) para **orientar al coach** por campaña.

## 2. Estado actual del build
Contenido nuevo. El mensaje inicial ya sale de la BD de campaña; no existe mecanismo para embeber
contexto orientador en la evaluación/conversación.

## 3. Diseño técnico
1. **Dominio:** campo aditivo `SeedThoughts` (texto largo o lista de strings) en la configuración
   de campaña (`03 §3.3` — **spec en commit aparte**). Editable desde el portal (pestaña
   Configuración o Mensajes).
2. **Inyección:** `ConstructorMensajesEvaluacion` agrega un bloque `system` separado
   ("CONTEXTO ORIENTADOR DE LA CAMPAÑA"), claramente delimitado y **nunca mezclado con el bloque
   de dato del usuario** ni con los aportes del tejido (I-09, que van como dato). Los seed
   thoughts los carga un **admin de confianza** por el portal: por eso pueden ir con rol de
   instrucción, a diferencia del contenido de participantes.
3. **Acotación:** límite de longitud/tokens configurable (`Conversacion:MaxTokensSeedThoughts`)
   para no inflar el prompt (`10 §2`); truncado determinista si excede.
4. **Degradación limpia:** campaña sin seed thoughts (o campo vacío) → prompt idéntico al actual.
5. **Plantilla de captura** (doc para GHT) se entrega en Semana 0/Sprint 1 para que Felipe la llene.

## 4. Contratos y configuración
- `03 §3.3` (+ `04 §5.3` request/response de campaña): campo aditivo — commit aparte.
- Config nueva: `Conversacion:MaxTokensSeedThoughts`.

## 5. Riesgos y mitigación
- *Entrega tardía de Felipe (RL-5)* → la campaña arranca con el campo vacío (degradación limpia);
  defaults provisionales para calibrar.
- *Prompt-injection vía contenido de campaña* → lo carga un admin por el portal; aun así no
  habilita acciones (la salida se valida igual).
- *Costo de tokens* → límite configurable + medición (P-10).

## 6. Criterios de aceptación / pruebas
- Unit: campaña con seed thoughts → el contexto al LLM contiene el bloque orientador, separado del
  dato del usuario y sin secretos.
- Unit: campo vacío → contexto idéntico al actual.
- Unit: contenido sobre el límite → truncado.
- Pruebas conjuntas: respuestas del coach alineadas al material.
