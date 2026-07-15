# Iniciativas — Índice y plan de ejecución

> **Fuente:** hoja `Iniciativas` de `Presentacion/Plan_Trabajo_El_Tejido.xlsx` (28 iniciativas;
> la versión `_ACT` añade P-01 con billing Meta RESUELTO y P-12) + hojas `Cronograma` y
> `Priorizacion`. Complementa a `Especificaciones/plan_hito_1.md` (diseño extendido) y a
> `Presentacion/20260711_Plan_Desarrollo_Mitigacion_Riesgos.md` (riesgos RL/RO y decisiones D1–D9).
> **Hito inamovible:** 10-ago-2026, envío del mensaje de inicio de campaña.
> Última revisión: 2026-07-13.

## 1. Clasificación

### 1.1 Con especificación propia (implican código) — un archivo por iniciativa

| ID   | Spec                                                                                                                                                                                                                                                 | Ventana               | Estado                                                                                                                  |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| I-03 | [I-03_Followups_Eje_Debil.md](I-03_Followups_Eje_Debil.md)                                                                                                                                                                                           | Sprint 1a-1b          | Pendiente (depende I-11)                                                                                                |
| I-05 | [I-05_Parafraseo_Transparencia.md](I-05_Parafraseo_Transparencia.md)                                                                                                                                                                                 | Sprint 1b             | Pendiente                                                                                                               |
| I-06 | [I-06_Multi_Idea_N_Registros.md](I-06_Multi_Idea_N_Registros.md)                                                                                                                                                                                     | S1a diseño / S1b impl | **Diseño DONE 2026-07-15**; implementación pendiente Sprint 1b (gran apuesta)                                           |
| I-08 | [I-08_Carga_Masiva_Participantes.md](I-08_Carga_Masiva_Participantes.md)                                                                                                                                                                             | S1a backend / S1b UI  | Pendiente                                                                                                               |
| I-09 | [I-09_Tejido_Colectivo.md](I-09_Tejido_Colectivo.md)                                                                                                                                                                                                 | S1a diseño / S1b core | **Diseño DONE 2026-07-15**; core pendiente Sprint 1b (gran apuesta; `IBaseConocimientoCampania` + recuperación léxica A + inyección delimitada) |
| I-10 | [I-10_Flag_Base_Previa_vs_Blanco.md](I-10_Flag_Base_Previa_vs_Blanco.md)                                                                                                                                                                             | Sprint 2              | Pendiente (depende I-09)                                                                                                |
| I-12 | [I-12_Seed_Thoughts.md](I-12_Seed_Thoughts.md)                                                                                                                                                                                                       | Sprint 2              | Pendiente (insumo Felipe 18-jul)                                                                                        |
| I-16 | [I-16_Fix_Calificacion_Markdown.md](I-16_Fix_Calificacion_Markdown.md)                                                                                                                                                                               | Sprint 1a             | **DONE 2026-07-15** (Markdown usa la evaluación más reciente por `fecha`; regresión determinística verde)               |
| P-03 | [P-03_Reiniciar_Conversacion.md](P-03_Reiniciar_Conversacion.md) — **ampliada a sistema de reinicio de datos** (participante Y campaña completa: conserva campaña/config/usuarios, borra conversaciones/respuestas/Markdown y resetea participantes) | Sprint 1a             | **PRIMER PASO de la implementación** (decisión del usuario 2026-07-13: desbloquea las pruebas humanas de todo lo demás) |
| P-07 | [P-07_Consentimiento_Datos.md](P-07_Consentimiento_Datos.md)                                                                                                                                                                                         | Sprint 2              | Pendiente (copy GHT)                                                                                                    |
| P-09 | [P-09_Monitoreo_Dia_D.md](P-09_Monitoreo_Dia_D.md)                                                                                                                                                                                                   | Pruebas 4–8 ago       | Pendiente (workbook primero)                                                                                            |
| P-10 | [P-10_Guardrails_Cupos_Costo.md](P-10_Guardrails_Cupos_Costo.md)                                                                                                                                                                                     | S1a + S2              | **Backend HECHO 2026-07-14** (cupos + rate por número + costo LLM por campaña); portal pendiente por Node; conteo multi-idea diferido a I-06 |
| P-04 | [P-04_Dashboard_Resultados.md](P-04_Dashboard_Resultados.md)                                                                                                                                                                                         | Rama deseable / post  | Diferida (no bloquea Hito)                                                                                              |
| P-05 | [P-05_Capa_Insights.md](P-05_Capa_Insights.md)                                                                                                                                                                                                       | Post-convención       | Diferida                                                                                                                |
| P-06 | [P-06_Destilacion_Por_Lotes.md](P-06_Destilacion_Por_Lotes.md)                                                                                                                                                                                       | Post-convención       | Diferida                                                                                                                |
| P-08 | [P-08_Recordatorios_Nudges.md](P-08_Recordatorios_Nudges.md)                                                                                                                                                                                         | Rama deseable         | Diferida                                                                                                                |
| P-11 | [P-11_Informe_Consolidado.md](P-11_Informe_Consolidado.md)                                                                                                                                                                                           | Rama deseable / post  | Diferida                                                                                                                |
| P-13 | [P-13_Umbral_Cierre_Por_Campania.md](P-13_Umbral_Cierre_Por_Campania.md)                                                                                                                                                                             | post-Hito (adelantable S1b) | **Diseño DONE 2026-07-15**; impl pendiente (override `configConversacional.umbralCierreAnticipado` por campaña; global sigue como default/kill-switch) |

### 1.2 Omitidas (no se implementan en código) — con el porqué

| ID | Iniciativa | Por qué se omite la spec |
|---|---|---|
| I-01 | Cierre por umbral de rúbrica | **Ya existe** (`Conversacion:UmbralCierreAnticipado`, default off). Trabajo = calibración + activación tras el workshop de rúbrica. Regla D2: no retirar el tope determinístico hasta que los cupos (P-10) estén activos en producción. Umbral global para el Hito (decisión plan §12.2). |
| I-02 | Salvaguarda "no quiero seguir" | **Ya existe** (`DetectorIntencionContinuar`, `FrasesContinuar`). Solo calibrar frases. |
| I-04 | Mensaje inicial estilo coach | Solo **prompt + contenido de campaña** (el `MensajeInicial` ya sale de la BD, `Reglas §2.1`). Redacción con GHT; A/B en pruebas conjuntas. |
| I-07 | Cierre conversacional natural | **Ya existe** (`InvitacionContinuarVariantes`, acuses rotados). Solo afinar textos. |
| I-11 | Recalibración de rúbrica | **Contenido/workshop** GHT (18-jul). La rúbrica es parametrizable y versionada; recalibrar es cambio de datos por el portal. Regla: no producción con rúbrica en `borrador`. |
| I-13 | Rúbrica agnóstica vs tailored | **Decisión de diseño** (GHT+Aliado TI, 25-jul). Recomendación: agnóstica + relevancia por seed thoughts/tags. Registrar en `SUPUESTOS.md`. |
| I-14 | Segmentación por tags | **Datos/config**: tags ya existen; consolidar catálogo con GHT y aplicarlo en la carga masiva (I-08). |
| I-15 | Rebranding | Branding, post-convención. |
| P-01 | Validar entrega real E2E | **Operación/pruebas** (billing 131042 RESUELTO según `_ACT`): validar envío→ventana→evaluación→Markdown real, monitoreando entregas. Sin código. |
| P-02 | Plantilla HSM de inicio | **Gestión Meta + App Settings** (`WhatsApp__PlantillaEnvioInicial__*`, ya parametrizado). Radicar YA. |
| P-12 | ARMA como campaña/módulo | **Diseño futuro** (reunión aparte). I-09/I-10 se diseñaron para habilitarlo sin reescritura. |

## 2. Plan de ejecución (Cronograma de la hoja + decisiones D1–D9)

> **Regla transversal:** nada nuevo se considera hecho sin (1) flag apagado por defecto,
> (2) métrica en el dashboard, (3) banco de calibración o suite de regresión en verde,
> (4) línea en el runbook de rollback. **El LLM propone, el sistema dispone** (R-01).

- **Semana 0 (9–13 jul, cierra HOY):** P-02 radicada en Meta; P-01 validación E2E real;
  staging (D8); workshop I-11 y seed thoughts I-12 según agenda GHT. **Cupos de P-10 implementados
  (2026-07-13, pendiente verificación local + commit).**
- **Sprint 1a (14–18 jul) — reinicio de datos primero, luego guardrails y prompts:**
  **1.º P-03 sistema de reinicio de datos** (decisión del usuario 2026-07-13: sin él, cada prueba
  humana de las demás iniciativas exige limpiar Cosmos a mano — es el multiplicador de velocidad
  de todo el Hito); 2.º cerrar P-10 cupos (verificar/commit); luego banco de calibración (D5);
  I-16 (fix, visible en demo); I-08 backend; diseño I-06 **DONE 2026-07-15**; diseño I-09 **DONE
  2026-07-15**; sigue activar I-01 en staging.
- **Sprint 1b (21–25 jul) — desarrollo mayor tras flags:** I-06 implementación + pruebas de no
  determinismo; I-09 recuperación top-k + inyección delimitada; I-05 parafraseo; I-08 UI;
  I-03 prompts sobre rúbrica congelada. Criterio de salida: I-06/I-09 funcionales en staging bajo
  flag, costo por conversación medido.
- **Sprint 2 (28 jul–1 ago) — parametrización + robustez:** prueba de carga el 28 (D7, decide
  cola/jobs/RU); I-10 flag por campaña; I-12 seed thoughts embebidos; I-13 decisión; I-14 tags;
  P-07 consentimiento; P-10 restante (costo LLM + rate por número); resiliencia LLM (D6).
- **Pruebas (4–8 ago):** UAT conjunta Felipe/Munir/Jason; calibración con el banco como árbitro;
  P-09 workbook + runbook; **acta de flags del día-D (6-ago)**: multi-idea y tejido solo quedan ON
  si pasaron carga + UAT + costo.
- **Freeze (8–9 ago):** code freeze; carga real (I-08); dry-run E2E; congelar rúbrica/prompts/seeds.
- **HITO (10-ago):** envío escalonado por lotes con monitoreo; ante síntoma se apaga el flag según
  runbook, nunca hotfix en caliente.
- **Post:** P-04, P-11, P-08, P-06, P-05, I-15, P-12, **P-13** (override umbral por campaña; diseño
  DONE, adelantable a S1b) en rama de deseables.

## 3. Dependencias duras (ruta crítica)

`P-01/P-02 (Meta)` → `I-11 (rúbrica)` → `I-03` · `I-12 (seeds)` → `I-04/I-13` ·
`P-10 cupos` → `I-01 (activar)` → `P-13 (override umbral por campaña)` · `I-09` → `I-10` →
(post: `P-05/P-06/P-11`) · `I-08` → carga real del freeze · `P-07` → apertura a participantes reales.

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
| I-06 multi-idea | `segmentacionIdeas` (bool, default `false`) — **por campaña** (spec I-06 ajustada 2026-07-13; antes era solo flag global) | `false` = modo 1-idea |

### 4.3 Candidatas a por-campaña (decidir al implementar; post-Hito si aprieta el freeze)

| Iniciativa | Propuesta | Nota |
|---|---|---|
| I-05 parafraseo | `parafraseo` (bool) en `ConfigConversacional` | Una campaña formal/corta puede no quererlo; si no se agrega el campo, queda global-on con degradación por campo LLM ausente |
| I-01 umbral de cierre | `umbralCierreAnticipado` por campaña — **formalizado como spec [P-13](P-13_Umbral_Cierre_Por_Campania.md)** (diseño DONE 2026-07-15) | Decisión vigente del plan (§12.2): **global para el Hito 1**, granularidad post-go-live (P-13, adelantable a S1b para simplificar la calibración de I-01). Patrón: default global + override por campaña (`campaña ?? global`) |
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
