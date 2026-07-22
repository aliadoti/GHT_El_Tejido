# Iniciativas — Índice y plan de ejecución

> **Fuente:** hoja `Iniciativas` de `Presentacion/Plan_Trabajo_El_Tejido.xlsx` (28 iniciativas;
> la versión `_ACT` añade P-01 con billing Meta RESUELTO y P-12) + hojas `Cronograma` y
> `Priorizacion`. Complementa a `Especificaciones/plan_hito_1.md` (diseño extendido) y a
> `Presentacion/20260711_Plan_Desarrollo_Mitigacion_Riesgos.md` (riesgos RL/RO y decisiones D1–D9).
> **Hito inamovible:** 10-ago-2026, envío del mensaje de inicio de campaña.
> **Convención: ≈24-sep-2026 (confirmada por GHT).**
> Última revisión: 2026-07-21 — **I-03 DONE local** (pista de foco + filtro de fuga de rúbrica
> siempre-on, sin cambio de contratos; D5 real pendiente). Anterior 2026-07-20 — acuerdos GHT:
> alcance de convención confirmado (§1.3); rúbrica I-11 congelada (18-jul) → I-03 desbloqueada;
> P-01/P-02 COMPLETAS; seeds I-12 vencidos → escalar; **P-13 adelantada a Sprint 1b** (decisión del
> usuario).

## 1. Clasificación

### 1.1 Con especificación propia (implican código) — un archivo por iniciativa

| ID   | Spec                                                                                                                                                                                                                                                 | Ventana               | Estado                                                                                                                  |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| I-03 | [I-03_Followups_Eje_Debil.md](I-03_Followups_Eje_Debil.md)                                                                                                                                                                                           | Sprint 1b             | **DONE local 2026-07-21** (pista de foco + filtro de fuga de rúbrica siempre-on; sin cambio de contratos; D5 real contra staging pendiente) |
| I-05 | [I-05_Parafraseo_Transparencia.md](I-05_Parafraseo_Transparencia.md)                                                                                                                                                                                 | Sprint 1b             | **DONE local 2026-07-20** (flag por campaña + kill-switch, salida/persistencia aditivas, truncado determinista; D5 real pendiente) |
| I-06 | [I-06_Multi_Idea_N_Registros.md](I-06_Multi_Idea_N_Registros.md)                                                                                                                                                                                     | S1a diseño / S1b impl | **Código DONE local 2026-07-15**; flags apagados hasta D5/UAT/costo en staging (gran apuesta)                         |
| I-08 | [I-08_Carga_Masiva_Participantes.md](I-08_Carga_Masiva_Participantes.md)                                                                                                                                                                             | S1a backend / S1b UI  | **DONE** (backend 2026-07-15, UI 2026-07-20, ambos por Claude; carga real en el freeze)             |
| I-09 | [I-09_Tejido_Colectivo.md](I-09_Tejido_Colectivo.md)                                                                                                                                                                                                 | S1a diseño / S1b core | **Diseño DONE 2026-07-15**; **core DONE local 2026-07-17** (Opción A léxica, inyección delimitada/sanitizada, degradación autocontenida; flags apagados hasta medir costo/latencia bajo flag en staging; gran apuesta) |
| I-10 | [I-10_Flag_Base_Previa_vs_Blanco.md](I-10_Flag_Base_Previa_vs_Blanco.md)                                                                                                                                                                             | Sprint 2              | **← ACTUAL** (agente Codex; I-09 core ya implementó el flag/backend. Falta exponer el checkbox de activación en el portal y alinear la spec con el código actual.) |
| I-12 | [I-12_Seed_Thoughts.md](I-12_Seed_Thoughts.md)                                                                                                                                                                                                       | Sprint 2              | **BLOCKED — insumo vencido** (seeds de Felipe no recibidos al 2026-07-20; **escalar**)                                                                                        |
| I-16 | [I-16_Fix_Calificacion_Markdown.md](I-16_Fix_Calificacion_Markdown.md)                                                                                                                                                                               | Sprint 1a             | **DONE 2026-07-15** (Markdown usa la evaluación más reciente por `fecha`; regresión determinística verde)               |
| P-03 | [P-03_Reiniciar_Conversacion.md](P-03_Reiniciar_Conversacion.md) — **ampliada a sistema de reinicio de datos** (participante Y campaña completa: conserva campaña/config/usuarios, borra conversaciones/respuestas/Markdown y resetea participantes) | Sprint 1a             | **DONE 2026-07-13/14** (reinicio por participante y por campaña; backend verde y committeado; `Seguridad:PermitirReinicioDatos` se apaga en el freeze) |
| P-07 | [P-07_Consentimiento_Datos.md](P-07_Consentimiento_Datos.md)                                                                                                                                                                                         | Sprint 2              | Pendiente (copy GHT)                                                                                                    |
| P-09 | [P-09_Monitoreo_Dia_D.md](P-09_Monitoreo_Dia_D.md)                                                                                                                                                                                                   | Pruebas 4–8 ago       | Pendiente (workbook primero)                                                                                            |
| P-10 | [P-10_Guardrails_Cupos_Costo.md](P-10_Guardrails_Cupos_Costo.md)                                                                                                                                                                                     | S1a + S2              | **Backend HECHO 2026-07-14** (cupos + rate por número + costo LLM por campaña); portal pendiente por Node; conteo multi-idea diferido a I-06 |
| P-04 | [P-04_Dashboard_Resultados.md](P-04_Dashboard_Resultados.md)                                                                                                                                                                                         | Rama deseable / post  | Diferida (no bloquea Hito)                                                                                              |
| P-05 | [P-05_Capa_Insights.md](P-05_Capa_Insights.md)                                                                                                                                                                                                       | Post-convención       | Diferida                                                                                                                |
| P-06 | [P-06_Destilacion_Por_Lotes.md](P-06_Destilacion_Por_Lotes.md)                                                                                                                                                                                       | Post-convención       | Diferida                                                                                                                |
| P-08 | [P-08_Recordatorios_Nudges.md](P-08_Recordatorios_Nudges.md)                                                                                                                                                                                         | Rama deseable         | Diferida                                                                                                                |
| P-11 | [P-11_Informe_Consolidado.md](P-11_Informe_Consolidado.md)                                                                                                                                                                                           | Rama deseable / post  | Diferida                                                                                                                |
| P-13 | [P-13_Umbral_Cierre_Por_Campania.md](P-13_Umbral_Cierre_Por_Campania.md)                                                                                                                                                                             | **Sprint 1b (adelantada 2026-07-20)** | **DONE local 2026-07-21**: override nullable `configConversacional.umbralCierreAnticipado`, default numérico heredable, kill-switch booleano `Conversacion:CierreAnticipadoHabilitado`, API/Cosmos/portal/telemetría y regresiones; D5 real + calibración I-01 en staging pendientes. |
| D5   | [D5_Banco_Calibracion.md](D5_Banco_Calibracion.md)                                                                                                                                                                                                   | Sprint 1a             | **DONE 2026-07-14** (librería + golden set 24 + runner opt-in fuera de CI); **baseline real pendiente** (corrido pagado contra staging; árbitro de I-03/I-05 y del umbral I-01) |

### 1.2 Omitidas (no se implementan en código) — con el porqué

| ID   | Iniciativa                     | Por qué se omite la spec                                                                                                                                                                                                                                                                  |
| ---- | ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| I-01 | Cierre por umbral de rúbrica   | **Ya existe** (`Conversacion:UmbralCierreAnticipado`, default off). Trabajo = calibración + activación tras el workshop de rúbrica. Regla D2: no retirar el tope determinístico hasta que los cupos (P-10) estén activos en producción. Umbral global para el Hito (decisión plan §12.2). |
| I-02 | Salvaguarda "no quiero seguir" | **Ya existe** (`DetectorIntencionContinuar`, `FrasesContinuar`). Solo calibrar frases.                                                                                                                                                                                                    |
| I-04 | Mensaje inicial estilo coach   | Solo **prompt + contenido de campaña** (el `MensajeInicial` ya sale de la BD, `Reglas §2.1`). Redacción con GHT; A/B en pruebas conjuntas.                                                                                                                                                |
| I-07 | Cierre conversacional natural  | **Ya existe** (`InvitacionContinuarVariantes`, acuses rotados). Solo afinar textos.                                                                                                                                                                                                       |
| I-11 | Recalibración de rúbrica       | **HECHA — workshop realizado y rúbrica congelada (18-jul; confirmado 2026-07-20).** Desbloquea I-03 y cumple la precondición de rúbrica de I-01. La rúbrica es parametrizable y versionada; recalibrar es cambio de datos por el portal. Regla: no producción con rúbrica en `borrador`.  |
| I-13 | Rúbrica agnóstica vs tailored  | **Decisión de diseño** (GHT+Aliado TI, 25-jul). Recomendación: agnóstica + relevancia por seed thoughts/tags. Registrar en `SUPUESTOS.md`.                                                                                                                                                |
| I-14 | Segmentación por tags          | **Datos/config**: tags ya existen; consolidar catálogo con GHT y aplicarlo en la carga masiva (I-08).                                                                                                                                                                                     |
| I-15 | Rebranding                     | Branding, post-convención.                                                                                                                                                                                                                                                                |
| P-01 | Validar entrega real E2E       | **COMPLETA (confirmado 2026-07-20):** flujo real validado envío→ventana→evaluación→Markdown con entregas monitoreadas. Ruta crítica Meta despejada. Sin código.                                                                                                                           |
| P-02 | Plantilla HSM de inicio        | **COMPLETA (confirmado 2026-07-20):** plantilla de inicio aprobada por Meta y configurada (`WhatsApp__PlantillaEnvioInicial__*`).                                                                                                                                                         |
| P-12 | ARMA como campaña/módulo       | **Diseño futuro** (reunión aparte). I-09/I-10 se diseñaron para habilitarlo sin reescritura.                                                                                                                                                                                              |

### 1.3 Alcance comprometido para la convención (confirmado con GHT, 2026-07-20)

**Dentro del alcance — deben quedar listas/validadas para el Hito del 10-ago:**
I-01, I-02, I-03, I-04, I-05, I-06, I-07, I-08, I-09, I-10, I-11 ✓, I-12, I-13, I-14, I-16 ✓,
P-01 ✓, P-02 ✓, P-03 ✓, P-07, P-09, P-10 y **P-13 (adelantada 2026-07-20)**.

**Fuera del alcance (rama de deseables, post-convención):** I-15, P-04, P-05, P-06, P-08, P-11, P-12.

**Insumos y actividades externas — seguimiento (estado al 2026-07-20):**

| Insumo / actividad | Responsable | Fecha | Estado |
|---|---|---|---|
| Priorización de iniciativas | Felipe / Munir | 14-jul | ✓ Confirmada (reunión + correo) |
| Fecha de la convención | Felipe / Munir | 14-jul | ✓ Confirmada: ≈24-sep-2026 |
| Rúbrica recalibrada — workshop I-11 | Felipe / Munir | 18-jul | ✓ **Congelada** (desbloquea I-03; precondición de I-01 cumplida) |
| Pensamientos semilla (I-12) | Felipe | 18-jul | ✗ **VENCIDO — ESCALAR** (bloquea I-12 y el afinado de I-04/I-13) |
| Decisión rúbrica agnóstica vs tailored (I-13) | Felipe / Munir | 25-jul | Pendiente |
| Lista final de participantes | GHT | 1-ago | Pendiente (insumo de la carga real I-08 en el freeze) |
| Plantilla HSM de inicio (P-02) | Aliado TI | Semana 0 | ✓ Aprobada por Meta y configurada |
| Validación E2E real (P-01) | Aliado TI | Semana 0 | ✓ Validada con entregas monitoreadas |
| Baseline D5 real (corrido pagado contra staging) | Aliado TI (op humana) | antes de Pruebas 4-ago | Pendiente (árbitro de I-03/I-05 y del umbral I-01) |

## 2. Plan de ejecución (Cronograma de la hoja + decisiones D1–D9)

> **Regla transversal:** nada nuevo se considera hecho sin (1) flag apagado por defecto,
> (2) métrica en el dashboard, (3) banco de calibración o suite de regresión en verde,
> (4) línea en el runbook de rollback. **El LLM propone, el sistema dispone** (R-01).

- **Semana 0 (9–13 jul) — CERRADA:** P-02 radicada **y aprobada**; P-01 E2E real **validado**
  (ambas confirmadas 2026-07-20); staging (D8); workshop I-11 **realizado (rúbrica congelada
  18-jul)**; seed thoughts I-12 **NO entregados (vencido — escalar a Felipe)**. Cupos de P-10
  implementados (2026-07-13).
- **Sprint 1a (14–18 jul) — CERRADO:** P-03 ✓ (reinicio de datos), P-10 ✓ (cupos + rate por número
  + costo LLM), D5 ✓ (baseline real pendiente), I-16 ✓, I-08 backend ✓, diseños I-06/I-09 ✓, y las
  implementaciones locales de **I-06 (15-jul)** e **I-09 core (17-jul)** llegaron adelantadas con
  flags apagados. I-01 quedó preparada (runbook + observabilidad + regresión) y **BLOCKED** para el
  flip humano (falta baseline D5 real; la rúbrica ya está ✓).
- **Sprint 1b (21–25 jul) — EN CURSO:** I-06 ✓ e I-09 core ✓ vienen adelantadas de S1a (flags
  apagados); I-05 parafraseo ✓ DONE local (2026-07-20, Codex); I-08 UI ✓ DONE (2026-07-20, Claude);
  I-03 ✓ DONE local (2026-07-21, Claude — pista de foco + filtro de fuga de rúbrica siempre-on) y
  P-13 ✓ DONE local (override por campaña + kill-switch global independiente).
  Siguiente: **I-10 UI de activación del tejido (← ACTUAL, agente Codex)**.
  Criterio de salida: I-06/I-09 funcionales en staging bajo flag, costo por conversación medido.
- **Sprint 2 (28 jul–1 ago) — parametrización + robustez:** prueba de carga el 28 (D7, decide
  cola/jobs/RU); I-10 flag por campaña; I-12 seed thoughts embebidos (**BLOCKED hasta recibir el
  insumo de Felipe — escalar**); I-13 decisión (25-jul); I-14 tags; P-07 consentimiento; P-10
  restante **ya hecho en S1a** (verificar y saltar); resiliencia LLM (D6).
- **Pruebas (4–8 ago):** UAT conjunta Felipe/Munir/Jason; calibración con el banco como árbitro;
  P-09 workbook + runbook; **acta de flags del día-D (6-ago)**: multi-idea, tejido **y el umbral de
  cierre I-01** solo quedan ON si pasaron carga + UAT + costo (checklist en `P-09 §3.4`).
- **Activar + calibrar umbral I-01 en staging (op humana, ventana Pruebas):** precondición: rúbrica
  I-11 congelada **✓ (18-jul)** + **corrido D5 real** contra staging (pendiente). Pasos: elegir el
  valor sobre la distribución de scores del banco (P85–P90 conservador), activarlo — **con P-13
  implementada, como override en la campaña de prueba** (reversible por campaña) en vez del flip del
  App Setting global `Conversacion__UmbralCierreAnticipado` —, verificar vía
  `LogSeguridad(CierreUmbralAnticipado)` en App Insights, y llevar la decisión on/off al **acta de
  flags del día-D (6-ago)**. Responsable: humano/ops.
  Ver `Especificaciones/Runbook_I-01_Umbral_Cierre_Anticipado.md` y `SUPUESTOS.md#activacion-umbral-i01`.
- **Freeze (8–9 ago):** code freeze; carga real (I-08); dry-run E2E; congelar rúbrica/prompts/seeds.
- **HITO (10-ago):** envío escalonado por lotes con monitoreo; ante síntoma se apaga el flag según
  runbook, nunca hotfix en caliente.
- **Post:** P-04, P-11, P-08, P-06, P-05, I-15, P-12 en rama de deseables. (**P-13 salió de esta
  lista: adelantada a Sprint 1b** por decisión del usuario 2026-07-20.)

## 3. Dependencias duras (ruta crítica)

`P-01/P-02 (Meta)` **✓** → `I-11 (rúbrica)` **✓ 18-jul** → `I-03` **✓ DONE local 2026-07-21** ·
`I-12 (seeds)` **BLOCKED (insumo vencido — escalar)** → `I-04/I-13` · `P-10 cupos` **✓** →
`I-01 (activar)` ← simplificada por `P-13 (override umbral por campaña, adelantada a S1b)` ·
`I-09` **✓ core** → `I-10` → (post: `P-05/P-06/P-11`) · `I-08` **✓ backend + UI** → carga real del
freeze · `P-07` → apertura a participantes reales. **Único insumo externo en rojo: seeds de Felipe.**

## 4. Parametrización por campaña (análisis 2026-07-13, decisión del usuario: no perder flexibilidad)

> **Principio rector:** todo lo que define el **comportamiento del coach o el contenido** de una
> campaña es **parametrizable por campaña** (una campaña sin seed thoughts simplemente no los
> tiene; ARMA/P-12 podrá configurar lo suyo sin tocar código). Las **salvaguardas de seguridad y
> costo** quedan **globales** como kill-switch de operación (freeze/día-D), aunque sus *valores*
> vivan en la campaña. Regla técnica: cada campo nuevo de campaña es **aditivo con default
> seguro** (`03 §3.3` en commit aparte); documento viejo sin el campo = comportamiento actual.

### 4.1 Ya parametrizables por campaña HOY (sin cambios)

| Iniciativa | Palanca existente |
|---|---|
| I-04 mensaje inicial coach | `MensajeInicial` activo de la campaña (BD, editable en portal) |
| I-03 follow-ups / I-11 rúbrica / I-13 agnóstica-vs-tailored | `rubricaRef` + `promptRefs` + `configLlmRef` por campaña (override por pregunta): cada campaña elige SU rúbrica, SU prompt y SU LLM versionados |
| Revisiones (base de I-01) | `MaxRepreguntas` por pregunta/campaña |
| Cierre (I-07 parcial) | `MensajeCierre` en `ConfigConversacional` de campaña |
| P-10 (valores de cupo) | `ConfigSeguridad.maxMensajesPorUsuario`/`maxLlamadasLlmPorUsuario` por campaña |
| P-07 (aviso de datos) | El texto del consentimiento viaja en el `MensajeInicial` de la campaña |
| P-06 / P-11 (post) | Operan por campaña por naturaleza (job/informe reciben `campaniaId`) |

### 4.2 Diseñadas por campaña en estas specs (campo aditivo nuevo)

| Iniciativa | Campo de campaña | "Apagado" natural |
|---|---|---|
| I-09/I-10 tejido colectivo | `tejidoColectivo` (bool, default `false`) — **declarado por I-09** en `configConversacional` (diseño 2026-07-15); I-10 añade base-previa-vs-blanco + UI | `false` = conversación autocontenida |
| I-12 seed thoughts | `seedThoughts` (texto/lista, default vacío) | **vacío = la campaña no los tiene** (el ejemplo del usuario) |
| I-06 multi-idea | `segmentacionIdeas` (bool, default `false`) — **por campaña** (implementado 2026-07-15; flag apagado hasta validación de staging) | `false` = modo 1-idea |
| I-05 parafraseo | `parafraseo` (bool, default `false`) — **implementado 2026-07-20** | `false` = retro clásica |

### 4.3 Candidatas a por-campaña (decidir al implementar; post-Hito si aprieta el freeze)

| Iniciativa | Propuesta | Nota |
|---|---|---|
| I-01 umbral de cierre | `umbralCierreAnticipado` por campaña — **formalizado como spec [P-13](P-13_Umbral_Cierre_Por_Campania.md)** | Patrón: default numérico global + override por campaña (`campaña ?? global`) y kill-switch booleano global `Conversacion:CierreAnticipadoHabilitado` (decisión confirmada 2026-07-21). |
| P-08 nudges | `nudgesHabilitados` + plantilla por campaña | Post; requiere plantilla HSM aprobada |
| P-02 plantilla de inicio | `MensajeInicial.PlantillaWhatsApp` ya existe en el dominio | Alternativa descartada en su momento (invariante crítico en operación manual); retomar solo si ARMA exige plantillas distintas |
| Textos conversacionales (I-07) | `Conversacion:Mensajes:*` por campaña | Bajo valor hoy (un solo idioma/tono); post |

### 4.4 Deliberadamente GLOBALES (no por campaña)

| Palanca | Por qué global |
|---|---|
| `Conversacion:CuposHabilitados`, `MaxTurnosPorHilo` | Salvaguardas de terminación/costo (D2): kill-switch de operación; los *valores* sí son por campaña |
| `Seguridad:PermitirReinicioDatos` (P-03) | Protección de datos en producción; se apaga en el acta del freeze |
| Rate por número / presupuesto-alerta de costo (P-10 restante) | Protección transversal de la plataforma |
| `Conversacion:RecuperacionSemantica` (I-09 opción B) | Capacidad de infraestructura (embeddings), no comportamiento de campaña |
| I-16 (fix), I-08, P-03, P-09 | Correcciones y herramientas admin: aplican a todas las campañas |

## 5. Disciplina de cambios

Cada iniciativa se implementa con el prompt estándar del repo: leer AVANCES/SUPUESTOS/spec de la
iniciativa → declarar rol y REQ/ARQ → pasos pequeños con build `-warnaserror`/test/format verdes →
cambios de contrato `03`/`04`/`08` **siempre aditivos y en commit aparte** que actualiza la spec →
commit atómico (Conventional Commits, "ATI JPC") → push solo cuando el usuario lo pida.
