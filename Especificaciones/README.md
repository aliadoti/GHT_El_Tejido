# Especificaciones — mapa maestro

> Punto de entrada único a la documentación de **El Tejido de la Red**. Explica **qué hay en cada
> carpeta**, el **orden de lectura** y **dónde vive cada cosa**. Última organización: 2026-07-21.

## Estructura

```
Especificaciones/
├── README.md            ← este mapa
├── AVANCES.md           ← estado real + log de traspaso entre agentes (append-only)
├── SUPUESTOS.md         ← decisiones de ambigüedad, con anclas #slug citadas en todo el repo
├── Reglas_Conversacion_y_Participacion.md   ← reglas de negocio vivas del flujo del coach
├── base/                ← documentación base numerada 00–13 (contratos + guía + arquitectura)
├── planes/              ← planes, runbooks y prompts de arranque/meta
└── Iniciativas/         ← backlog: índice, plan de sprints, TODO/handoff y una spec por iniciativa
```
Documentación relacionada **fuera** de esta carpeta: `../QAS/` (plan y casos de prueba) y
`../Guias_Implementacion/` (guías Azure/WhatsApp paso a paso).

## Qué es cada cosa

### Raíz (los 3 documentos que se abren a diario)
- **`AVANCES.md`** — la **fuente del estado real**: qué está DONE/WIP/pendiente, y el log cronológico
  append-only con el que un agente retoma el trabajo. Se lee SIEMPRE primero.
- **`SUPUESTOS.md`** — registro de decisiones tomadas ante ambigüedad, con anclas `#slug`
  (`#orquestador-conversacional`, `#primer-contacto-pregunta`, …) referenciadas desde specs y código.
- **`Reglas_Conversacion_y_Participacion.md`** — el resumen vivo de las reglas del flujo por WhatsApp
  (cold-start, evaluación, revisión, cierre, ventana 24 h, expiración, guardrails, tejido diferido).

### `base/` — documentación base numerada (00–13)
El conjunto formal y **canónico**. Los números son identificadores estables: el resto del repo y los
comentarios de código citan estos documentos **por número** (`03 §3.3`, `08 §4`, `10 §2`) — **no se
renumeran ni renombran**. Orden de lectura recomendado para entender el sistema:
`00` guía → `01` convenciones para agentes → `02` arquitectura → `03` modelo de datos (Cosmos) →
`04` contrato API REST → `05` WhatsApp y conversación → `06` identidad/auth → `07` campañas/config →
`08` evaluación LLM → `09` Markdown → `10` seguridad/guardrails → `11` portal Angular →
`12` CI/CD → `13` plan de pruebas y aceptación. **`03`, `04` y `08 §4` mandan** como contratos: sus
cambios son aditivos y en commit aparte.

### `planes/` — planes, runbooks y prompts de trabajo
- `plan_hito_1.md` — plan extendido del Hito (histórico, 11-jul; parcialmente superado por la reunión
  del 20-jul — la fuente viva es `Iniciativas/00_Indice…`).
- `Runbook_I-01_Umbral_Cierre_Anticipado.md` — runbook de activación/rollback del umbral de cierre.
- `PROMPT_Inicio_Desarrollo_Codex.md` — prompt de arranque para retomar el desarrollo.
- `PROMPT_Reorganizar_Documentacion.md` — prompt meta (esta reorganización).

### `Iniciativas/` — backlog del Hito y forma de trabajar
- `00_Indice_y_Plan_de_Ejecucion.md` — **clasificación de iniciativas** (con código vs. omitidas vs.
  diferidas), **plan por sprints**, **ruta crítica** y **parametrización por campaña**. Fuente viva del plan.
- `TODO.md` — handoff vivo: objetivo actual, tabla de estado por iniciativa y primer paso ejecutable.
- `PROMPT_Inicio_Iniciativas.md` — prompt estándar para implementar una iniciativa.
- `D5_Banco_Calibracion.md` y una spec por iniciativa (`I-03, I-05, I-06, I-08, I-09, I-10, I-12,
  I-16, I-17, P-03…P-13`): qué pide GHT, estado del build, diseño, contratos, riesgos, aceptación.

## Orden de lectura para un agente que retoma
1. `AVANCES.md` (Próximo paso + Tablero) → 2. `Iniciativas/00_Indice…` y `Iniciativas/TODO.md`
(objetivo actual) → 3. la spec de la iniciativa objetivo en `Iniciativas/` → 4. `Reglas_Conversacion…`
y `SUPUESTOS.md` (anclas citadas) → 5. las secciones de `base/` que la iniciativa toque
(`03`/`04`/`08`/… por número) → 6. como fondo: `planes/plan_hito_1.md` y `../QAS/`.

## Reglas de la documentación (para no romperla)
- **No renumerar/renombrar `base/00–13`**: son referencias lógicas por número.
- **`AVANCES.md` es append-only**; **no cambiar los `#slug` de `SUPUESTOS.md`**.
- Cambios de contrato (`03`/`04`/`08 §4`): **aditivos** y en **commit aparte** que actualiza la spec.
- Al mover o crear documentos, actualizar este README y verificar que no queden enlaces rotos.
