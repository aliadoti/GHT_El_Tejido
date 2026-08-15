# Iniciativas — Índice y plan de ejecución

> **Fuente:** hoja `Iniciativas` de `Presentacion/Plan_Trabajo_El_Tejido.xlsx` (28 iniciativas;
> la versión `_ACT` añade P-01 con billing Meta RESUELTO y P-12) + hojas `Cronograma` y
> `Priorizacion`. Complementa a `Especificaciones/planes/plan_hito_1.md` (diseño extendido) y a
> `Presentacion/20260711_Plan_Desarrollo_Mitigacion_Riesgos.md` (riesgos RL/RO y decisiones D1–D9).
> **Hito inamovible:** 12-ago-2026, envío del mensaje de inicio de campaña.
> **Convención: ≈24-sep-2026 (confirmada por GHT).**
> **Última revisión: 2026-08-15 — `DT-P32-03-01` COMPLETA local (1/1), pendiente de despliegue.** El
> smoke de DT-P32-03 obtuvo PASS en 1–4 y 6: el cierre bilingüe está resuelto. La prueba 5 quedó
> BLOCKED porque borradores incompletos bloqueaban la señal global; el microajuste ya implementado
> mantiene sus pendientes visibles, calcula `listoParaGateOn` solo con campañas activas y valida los
> mapeos propios al activar con gate ON (`400 VALIDATION_ERROR` sin cambiar el estado). Backend 863 +
> 109, portal 62, todo verde. Falta desplegar con autorización y repetir QAS/23 4–6; con green sigue
> DT-I20-02 1/3. DT-P32-04 permanece como refactor posterior y no bloqueante. Sin configuración remota.
>
> **Revisión anterior: 2026-08-14 — `DT-P32-02` IMPLEMENTADA Y DESPLEGADA (3/3).** Semillas base,
> JSON masivo y readiness pasaron su validación; la corrida P-32 descubrió los bloqueos posteriores
> que ahora cubre DT-P32-03. Plan `planes/DT-P32-02_*`, QAS `22_*`.
>
> **Revisión anterior: 2026-08-13 — `DT-I20-02` ESPECIFICADA (0/3), en espera de P-32 green.**
> Corrige la exposición visible de encabezados Markdown/estado interno causada por el prompt runtime.
> La guarda opera por fragmento LLM y usa respaldos por campo, sin alterar puntajes, versión I-19,
> umbrales, estados, cierres, P-27, P-32, P-33 ni historial. Incluye selección runtime activa+aprobada
> y migración gradual por familia nueva; sin código ni cambio remoto. Spec `DT-I20-02_*`, QAS `21_*`.
>
> **Revisión 2026-08-13 — `P-33` DONE LOCAL 3/3.** Consulta/cierre visible, afinidad y reapertura
> implementados con gate OFF, opt-outs, versión exacta I-19, catálogo `es/en` y QAS `20_*`.
> Pendiente únicamente D5/UAT y acta de flags antes de activar.
>
> **Última revisión: 2026-08-13 — `DT-I20-01` DONE LOCAL 5/5.** Corrige la repetición
> mecánica de aperturas en coaching y la duplicación de un mismo reconocimiento entre puente y cuerpo.
> Aplica a toda salida nueva de todas las campañas, sin migrar ni editar conversaciones pasadas; no
> agrega flags, contratos ni configuración por campaña. Backend 785 unitarias (766 sin Calibración) +
> 88 de integración, build/format/diff verdes; falta D5 con ejemplos reales antes de desplegar.
> Spec: `DT-I20-01_*`; QAS: `QAS/19_*`.
>
> Revisión anterior: 2026-08-10 — `P-32` CORTE 1/4 DONE localmente.** Se verificó que
> `Usuario.Idioma` ya existe (`es|en`, default `es`) y se definió el catálogo versionado en Cosmos
> `config`, localizaciones de campaña, plantillas por participante, caché/rollback, API/portal,
> migración y QAS. Ya existen la base versionada, repositorios memoria/Cosmos, API administrativa,
> ETag, activación atómica, validación, caché/LKG, emergencia bilingüe, semillas administrativas y
> gate OFF. Faltan los cortes de runtime, campaña, envío y portal. Sin despliegue ni cambio remoto.
>
> Revisión anterior: 2026-08-08 — `DT-P27-01` DONE local 2/2. Las listas globales de finalización
> validan vacío/duplicado/límite tras normalizar y caen completas al default seguro. Dejan historial
> append-only de la versión aplicada/default/descartada, sin aliases, y el rollback restaura el origen
> de configuración o vacía ambas listas. No se activó P-27 ni se cambió configuración remota. La
> siguiente prioridad de código requiere decisión expresa del usuario.
>
> Revisión anterior: 2026-08-07 — `P-31` DONE 3/3 y desplegado.
> `P-31` (REQ-052, visibilidad del progreso de la idea) cerró con commits `6ba6ce0`/`32794fb`/`6d02492`:
> umbral de resumen propio (`umbralResumenConsolidacion`) **independiente** del umbral de madurez de
> I-17/P-13, que presenta la consolidación vigente y pregunta si se quiere seguir madurando. Sin estado
> conversacional nuevo, sin depender de los flags de P-27 y sin tocar el sellado de madurez.
> **Flags OFF**: encenderlos exige D5 real, UAT, costo y acta de flags, más elegir el umbral (rango
> útil **0.40–0.55** con base 0.6). Spec: [P-31_Resumen_Consolidacion_Por_Umbral.md](P-31_Resumen_Consolidacion_Por_Umbral.md);
> guía de prueba: `QAS/14_P31_Resumen_Consolidacion_Como_Probar.md`.
> *Decisión resuelta el 2026-08-13:* la consulta bajo demanda y la visibilidad al cierre se implementan
> separadamente en `P-33`; P-31 conserva únicamente su resumen proactivo por umbral.
>
> **▶ Siguiente: `I-08 v2`** (§1.1, TODO 22a) — carga masiva con la plantilla oficial de GHT. Spec y
> contratos listos, 0 código. Es lo único que bloquea el freeze (11 ago) e incluye **recrear el
> contenedor `users`** con unique key `/claveUnicidad`, paso **irreversible** que bloquea el resto;
> tras recrear, repetir la prueba de humo de P-31. **`DT-P27-01` corte 2** se retoma después.
> El soporte de **inglés** ya está especificado y su catálogo base está en implementación en P-32;
> permanece pendiente la conexión al runtime y las traducciones/plantillas Meta aprobadas.
> Última revisión: 2026-08-05 — **`DT-QA-01` DONE local.** Endpoint de diagnóstico
> `POST /diagnostico/simulacion/webhook-entrante` (protegido por `X-Diag-Key`) que inyecta un mensaje
> entrante y lo encola sin exigir firma, para correr las pruebas E2E conversacionales contra el desplegado
> sin exponer el App Secret de Meta. El webhook real sigue exigiendo firma. Pendiente: despliegue
> controlado; siguiente cambio de código: DT-P27-01 corte 2.
> Última revisión: 2026-08-05 — **`DT-P27-01` en curso (1/2).** El corte 1 ya lee las dos listas de
> alias de finalización desde configuración global, conserva el default compilado cuando están
> ausentes/vacías y reutiliza la normalización vigente. Siguiente: corte 2, validación con fallback y
> registro del motivo, más historial/rollback. Sin cambio de alias, flags ni configuración remota.
> Última revisión: 2026-08-04 — **P-28 y P-29 completas localmente; matriz canónica de cierres adoptada.** `P-26` ya entrega participación
> continua y ciclos nuevos; `P-28`, `P-29` y `P-30` cubrían vacíos acotados: entrada humana para
> saludo/inicio no sustantivo, mensaje humano posterior al cierre por inactividad ya existente y
> selección histórica de una idea, respectivamente. **Las tres están completas localmente** y viven
> detrás de kill-switch OFF y ninguna es prerrequisito técnico de P-26.
> Revisión: 2026-08-04 — **`P-27`, `P-28`, `P-29` y `P-30` completas localmente.** No hay otra
> implementación priorizada; siguen pendientes D5/UAT/costo y la decisión formal de flags.
> P-27 corrige las salidas naturales y añade, detrás de flags OFF, clasificación LLM de intención con
> ejecución siempre server-side y consumo persistente de sus llamadas/tokens.
> Revisión anterior: 2026-07-29 — **`P-26` especificada, pendiente de implementación.** Añade
> participación continua por campaña, selección determinista de campaña/pregunta, conservación del
> aporte raíz, ciclos independientes y cupos móviles de 24 h. Solo campañas activas reciben aportes.
> Revisión anterior: **`P-25` elimina la confirmación mecánica del flujo normal.** Cada
> aporte sustantivo se consolida y evalúa completo en el mismo turno; solo una ambigüedad real pide
> aclaración. I-19 conserva la versión canónica y P-24 queda como compatibilidad/rollback. I-20 redacta
> el coaching con un LLM controlado por el servidor y hace visible umbral/escala en Markdown. Revisión anterior:
> **`I-18` coaching secuencial DONE local.**
> Cola por idea, revisiones enlazadas y coach socrático; gates aún apagados y sin activación.
> "Priorización iniciativas MVP"): **I-09/I-10 (tejido colectivo) → DIFERIDAS a "Capa 3" post-convención;
> P-07 (consentimiento) → DIFERIDA (herramienta interna, IP de GHT); P-09 (panel en vivo) → DIFERIDO
> (basta health-check; métricas de tokens no prioritarias);** HITL fuera del MVP; **nueva iniciativa
> I-17 (BD de dos niveles: ideas maduras vs. incubación por umbral)**; paráfrasis (I-05) **solo tras
> umbral**; cierre por inactividad **~5 min**; carga masiva (I-08) debe incluir **variables
> demográficas** (Munir entrega lista); nombre confirmado **"Tejido de Red"** (no "Bright Idea").
> Anterior 2026-07-21 (mañana): I-03 DONE local; P-13 DONE local. Anterior 2026-07-20: rúbrica I-11
> congelada; P-01/P-02 COMPLETAS; seeds I-12 vencidos → escalar; P-13 adelantada a Sprint 1b.

## 1. Clasificación

### 1.1 Con especificación propia (implican código) — un archivo por iniciativa

| ID   | Spec                                                                                                                                                                                                                                                 | Ventana               | Estado                                                                                                                  |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| P-14 | [P-14_Lectura_Rubricas_Prompts.md](P-14_Lectura_Rubricas_Prompts.md)                                                                                                                                                                                 | Sprint 1b | **DONE local 2026-07-22.** Vista de **solo lectura** de rúbricas y prompts en el portal; frontend-only, sin cambio de contratos. |
| P-15 | [P-15_Refactor_Orquestador_Conversacional.md](P-15_Refactor_Orquestador_Conversacional.md) | Remediación auditoría | **DONE local 2026-07-24.** `CAL-001`: políticas, transición y efectos separados detrás de `IOrquestadorConversacion`. |
| P-16 | [P-16_Refactor_Pagina_Campanias.md](P-16_Refactor_Pagina_Campanias.md) | Remediación auditoría | **DONE local 2026-07-25.** `CAL-002`: paneles de campañas separados, preservando ruta/API/permisos. |
| P-17 | [P-17_Errores_API_Uniformes.md](P-17_Errores_API_Uniformes.md) | Remediación auditoría | **DONE local 2026-07-24.** `API-001`: `ErrorRespuesta` y correlación uniformes. |
| P-18 | [P-18_Controles_Con_Nombre_Accesible.md](P-18_Controles_Con_Nombre_Accesible.md) | Remediación auditoría | **DONE local 2026-07-25.** `UXA11Y-001`: nombres accesibles en selección, tags y CSV. |
| P-19 | [P-19_Estados_Dinamicos_Accesibles.md](P-19_Estados_Dinamicos_Accesibles.md) | Remediación auditoría | **DONE local 2026-07-25.** `UXA11Y-002`: errores y confirmaciones anunciados. |
| P-20 | [P-20_Pestanas_Accesibles_Campanias.md](P-20_Pestanas_Accesibles_Campanias.md) | Remediación auditoría | **DONE local 2026-07-25.** `UXA11Y-003`: patrón ARIA y teclado de pestañas. |
| P-21 | [P-21_Multi_Numero_WhatsApp.md](P-21_Multi_Numero_WhatsApp.md) | A coordinar (fuera de ruta crítica) | **DONE local 2026-07-25.** Multi-número de WhatsApp (misma WABA/App): captura `metadata.phone_number_id` para **responder por el número entrante**, `IWhatsAppGateway` con emisor opcional y número saliente **por campaña** (`configConversacional.numeroWhatsAppSaliente`). Sin secretos nuevos; configuración ausente conserva el único número legado/predeterminado. Backend 473/473 verde. |
| P-22 | [P-22_UX_Campanias.md](P-22_UX_Campanias.md) | A coordinar (mejoras de portal) | **DONE local 2026-07-25.** UX de Campañas frontend-only: creación bajo demanda, pasos numerados con completitud y nombre accesible, enlace a Envíos con id real, fieldsets con ayuda y estados vacíos. Preserva P-16/P-18/P-19/P-20; 21/21 pruebas Angular y build de producción verdes. |
| P-23 | [P-23_UX_Resultados.md](P-23_UX_Resultados.md) | A coordinar (mejoras de portal) | **DONE local 2026-07-25.** UX de Resultados frontend-only: precarga de campaña en sesión, patrón **maestro-detalle** (lista de respuestas → evaluación + Markdown), leyenda/conteos, lectura fácil y estados guiados. Sin contratos, rutas ni permisos nuevos; 24/24 pruebas Angular, Prettier y build verdes. |
| P-24 | [P-24_Evaluacion_Implicita_Al_Solicitar_Mejora.md](P-24_Evaluacion_Implicita_Al_Solicitar_Mejora.md) | Inmediata | **DONE local 2026-07-29.** Corrige el bug confirmado en hilo simple y cola multi-idea: una petición corta de mejorar confirma implícitamente la versión completa, la evalúa contra la rúbrica y abre coaching; no se persiste como corrección ni reduce `MaxRepreguntas`. Backend 579/579, formato y diff verdes. |
| P-25 | [P-25_Coaching_Directo_Sin_Confirmacion_Repetitiva.md](P-25_Coaching_Directo_Sin_Confirmacion_Repetitiva.md) | Inmediata | **DONE local 2026-07-29.** Cada aporte sustantivo consolida y evalúa la versión completa en el mismo turno; la confirmación explícita repetitiva sale del flujo normal y queda solo como rollback global. Backend 583/583, formato y diff verdes. |
| P-26 | [P-26_Participacion_Continua_y_Seleccion_de_Campania.md](P-26_Participacion_Continua_y_Seleccion_de_Campania.md) | Inmediata | **DONE local 2026-07-31 (6/6).** Flag `participacionContinua` por campaña, selección campaña/pregunta, aporte preservado, afinidad 24 h, ciclos independientes y cupos móviles. Default OFF; activación operativa pendiente. |
| P-27 | [P-27_Clasificacion_Flexible_Intenciones_Control.md](P-27_Clasificacion_Flexible_Intenciones_Control.md) | Alta, después de P-26 | **DONE local 2026-08-04 (5/5).** Alias, clasificador/política server-side, aclaración, portal y cupos/telemetría persistentes. Backend 698/698; flags global/campaña OFF y activación D5/UAT/costo pendiente. |
| P-28 | [P-28_Despertar_Proactivo_Coach.md](P-28_Despertar_Proactivo_Coach.md) | Completa local | **DONE local 2026-08-04 (3/3).** Saludo/inicio no sustantivo, selección P-26, redacción/fallback, telemetría sin texto y E2E. El saludo no crea idea; kill-switch `DespertarProactivoHabilitado` OFF. |
| P-29 | [P-29_Cierre_Conversacional_Por_Tiempo.md](P-29_Cierre_Conversacional_Por_Tiempo.md) | Completa local | **DONE local 2026-08-04 (2/2).** Aviso de pausa redactado por LLM con fallback determinista sobre el cierre por inactividad ya existente de I-17/I-19 (sin temporizador, umbral, estado ni motivo nuevos), telemetría `cierrePorInactividad` sin texto, E2E simulada y QAS. Kill-switch `CierrePorTiempoHabilitado` OFF; activación D5/UAT/costo pendiente. |
| P-30 | [P-30_Retomar_Ideas_Del_Pasado.md](P-30_Retomar_Ideas_Del_Pasado.md) | Reunión 31-jul (REQ-014) | **DONE local 2026-08-04 (3/3).** Lista histórica determinista por participante/campaña/pregunta, reapertura con el mismo `ideaId`, afinidad al ciclo histórico, telemetría y E2E; búsqueda vectorial fuera. Kill-switch `RetomarIdeasHabilitado` OFF. |
| P-31 | [P-31_Resumen_Consolidacion_Por_Umbral.md](P-31_Resumen_Consolidacion_Por_Umbral.md) | 2026-08-06/07 | **DONE 3/3 y DESPLEGADO (2026-08-07)** — commits `6ba6ce0`/`32794fb`/`6d02492`; build Release, 664 unitarias + 77 integración, formato y `git diff --check` verdes; E2E simulada y guía `QAS/14_*`. **Flags OFF**: encenderlos exige D5 real, UAT, costo y acta de flags, y elegir el umbral (rango útil 0.40–0.55 con base 0.6). La consulta bajo demanda se resolvió como P-33 y no cambia este alcance. Umbral de resumen propio (`umbralResumenConsolidacion`, global/campaña/pregunta) **independiente** del umbral de madurez: al cruzarlo con la idea **abierta**, el turno de coaching lleva la consolidación vigente insertada server-side y una pregunta de continuidad. Sin estado conversacional nuevo, sin consumir repreguntas, idempotente por idea y sin dependencia de los flags de P-27. Kill-switch `Conversacion:ResumenConsolidacionHabilitado` OFF. |
| P-32 | [P-32_Conversacion_Multidioma_y_Catalogo_Textos.md](P-32_Conversacion_Multidioma_y_Catalogo_Textos.md) | **DONE local 2026-08-11** | **4/4 cortes locales completos.** Catálogo versionado y portal, snapshots, textos globales, localizaciones de campaña, envío mixto y contextos LLM bilingües. Backend: 768 unitarias + 87 integración; Prettier verde. Gate OFF; no hubo despliegue, push ni cambio remoto. Activar exige D5/UAT, plantillas Meta inglesas aprobadas y revisión de costo. |
| P-33 | [P-33_Consulta_y_Cierre_Visible_de_la_Idea.md](P-33_Consulta_y_Cierre_Visible_de_la_Idea.md) | **DONE local 3/3 — 2026-08-13** | Consulta pura de la idea activa/última sin menú, aporte ni evaluación; versión exacta I-19 por demanda y en cierres normales; afinidad de 24 h y reapertura de la misma cerrada ante corrección sustantiva. Gate global OFF, opt-outs por campaña, catálogo `es/en`, telemetría sin contenido y QAS `20_*`. Build `-warnaserror`: 789 unitarias + 87 integración. Pendiente D5/UAT y acta de flags; sin activación remota. |
| **DT-I20-01** | [DT-I20-01_Variacion_y_No_Duplicacion_Redaccion_Conversacional.md](DT-I20-01_Variacion_y_No_Duplicacion_Redaccion_Conversacional.md) | **DONE local 5/5 — 2026-08-13** | I-20: permite ocasionalmente `Queda claro que...`, pero elimina su uso sistemático y descarta de forma determinista el puente duplicado frente al cuerpo insertado por el servidor. Todas las campañas reciben la corrección en mensajes nuevos; sin flag, migración, API, portal ni cambios históricos. QAS: `QAS/19_*`. |
| **DT-P32-02** | [DT-P32-02_Semillas_Edicion_Masiva_y_Readiness_Catalogo_Textos.md](DT-P32-02_Semillas_Edicion_Masiva_y_Readiness_Catalogo_Textos.md) | **DONE/DESPLEGADA — 3/3** | Base `es/en`, edición masiva JSON, límites, readiness editorial y bloqueo de campaña bilingüe implementados; QAS/22 pasó en Azure. La corrida posterior abrió DT-P32-03. |
| **DT-P32-03** | [DT-P32-03_Cierre_Localizado_y_Readiness_Plantillas_Meta.md](DT-P32-03_Cierre_Localizado_y_Readiness_Plantillas_Meta.md) | **DESPLEGADA — 2/2** | Cierre bilingüe demostrado; smoke 1–4 y 6 PASS. La semántica descubierta en prueba 5 se corrige aparte en DT-P32-03-01. |
| **DT-P32-03-01** | [DT-P32-03-01_Readiness_Gate_Solo_Campanias_Activas.md](DT-P32-03-01_Readiness_Gate_Solo_Campanias_Activas.md) | **COMPLETA local — 1/1 (2026-08-15)** | Implementada: `mapeosMeta[].bloqueaGateOn`, agregado limitado a pares de campañas activas y guarda de mapeos propios en `borrador → activa` con gate ON; Preparación separa bloqueo actual de pendiente de borrador. Sin Graph API ni nombres físicos. Falta desplegar, repetir QAS/23 4–6 y luego iniciar DT-I20-02. |
| **DT-P32-04** | [DT-P32-04_Nucleo_Transversal_Multidioma.md](DT-P32-04_Nucleo_Transversal_Multidioma.md) | **0/3 — backlog post-green** | Refactor incremental: idioma central, contenido efectivo, resolutores especializados y readiness compuesto, sin cambiar fuentes ni formas persistidas. No bloquea DT-I20-02. |
| **DT-I20-02** | [DT-I20-02_Contrato_Visible_Texto_Plano_y_Gobierno_de_Prompts.md](DT-I20-02_Contrato_Visible_Texto_Plano_y_Gobierno_de_Prompts.md) | **0/3 — espera micro-smoke green** | Iniciar después de DT-P32-03-01 y QAS/23 4–6 green; alcance ya aprobado. QAS `21_*`; runbook `planes/DT-I20-02_*`. |
| DT-P27-01 | [DT-P27-01_Config_Versionada_Frases_Finalizacion.md](DT-P27-01_Config_Versionada_Frases_Finalizacion.md) | **DONE local 2/2 — 2026-08-08** | Validación tras normalizar (vacío/duplicado/límite) con descarte completo y fallback; historial append-only seguro de versión aplicada/default/descartada y rollback desde el origen de configuración o al default. Sin alias nuevos, flags, edición por campaña ni configuración remota. |
| **DT-QA-02** | [DT-QA-02_Listado_Evaluaciones_Y_Huerfanas.md](DT-QA-02_Listado_Evaluaciones_Y_Huerfanas.md) | **DONE local 2026-08-08** | `GET /api/admin/evaluaciones` con puerto obligatorio, adaptadores Cosmos/memoria `fecha DESC`, filtros/paginación/resumen y diagnóstico derivado `enlazada`/`huerfana`/`superada`/`sin_version_idea`. El DTO no expone texto libre; `superada` no se cuenta como huérfana (I-16). Build y 814 pruebas no-Calibracion verdes; sin flags, despliegue ni configuración remota. No repara documentos ni agrega UI. **Siguiente prioridad actual: DT-P32-02 corte 1.** |
| DT-QA-01 | [DT-QA-01_Inyeccion_Webhook_Simulado_Diagnostico.md](DT-QA-01_Inyeccion_Webhook_Simulado_Diagnostico.md) | **DONE local 2026-08-05** | Herramienta de QA: endpoint `POST /diagnostico/simulacion/webhook-entrante` (gating `X-Diag-Key` + Development/`Simulacion:Habilitada`) que inyecta un mensaje entrante y lo encola por `IColaWebhook` **sin exigir firma**. Auditoría sin PII e id estable para el dedupe; 7 pruebas focalizadas verdes. **No** relaja la firma real. Pendiente: despliegue controlado. |
| I-03 | [I-03_Followups_Eje_Debil.md](I-03_Followups_Eje_Debil.md)                                                                                                                                                                                           | Sprint 1b             | **DONE local 2026-07-21** (pista de foco + filtro de fuga de rúbrica siempre-on; sin cambio de contratos; D5 real contra staging pendiente) |
| I-05 | [I-05_Parafraseo_Transparencia.md](I-05_Parafraseo_Transparencia.md)                                                                                                                                                                                 | Sprint 1b             | **DONE local 2026-07-20** (flag por campaña + kill-switch, salida/persistencia aditivas, truncado determinista; D5 real pendiente) |
| I-06 | [I-06_Multi_Idea_N_Registros.md](I-06_Multi_Idea_N_Registros.md)                                                                                                                                                                                     | S1a diseño / S1b impl | **Código DONE local 2026-07-15**; flags apagados hasta D5/UAT/costo en staging (gran apuesta)                         |
| I-08 | [I-08_Carga_Masiva_Participantes.md](I-08_Carga_Masiva_Participantes.md)                                                                                                                                                                             | S1a backend / S1b UI · **v2 antes del freeze**  | **REABIERTA 2026-08-07 (`I-08 v2`)** — v1 DONE (backend 15-jul, UI 20-jul) pero la plantilla oficial que entregó GHT tiene **otras 9 columnas**. v2: campos nuevos de primer nivel en `Usuario`, `codigoUsuario` secuencial, `usuarioWhatsapp`, **un solo activo por teléfono** con reasignación y conflicto de titular, modo `solo_actualizar`, lector `.xlsx`. **Spec y contratos listos, sin código.** Incluye **recrear `users` con unique key `/claveUnicidad`** (bloqueante). |
| I-09 | [I-09_Tejido_Colectivo.md](I-09_Tejido_Colectivo.md)                                                                                                                                                                                                 | ~~S1b core~~ → **DIFERIDA (Capa 3)** | **⚠️ DIFERIDA del MVP (reunión 20-jul).** Core DONE local 2026-07-17 pero **flag `tejidoColectivo` OFF y fuera de ruta crítica**; no se valida para el Hito. Código permanece; retomar en Capa 3. |
| I-10 | [I-10_Flag_Base_Previa_vs_Blanco.md](I-10_Flag_Base_Previa_vs_Blanco.md)                                                                                                                                                                             | ~~Sprint 2~~ → **DIFERIDA (Capa 3)** | **⚠️ DIFERIDA con I-09** (es su UI de activación). No se construye el checkbox de tejido para el MVP; el campo ya existe en el modelo y queda OFF. |
| I-17 | [I-17_BD_Dos_Niveles_Madurez.md](I-17_BD_Dos_Niveles_Madurez.md)                                                                                                                                                                                     | Sprint 1b–2           | **DONE local 2026-07-22 (6/6 slices).** Clasifica cada respuesta `maduro`/`incubacion` por umbral compartido; paráfrasis I-05 solo si madura, resultados/Markdown, rechazo explícito y cierre por inactividad por campaña. D5 real y acta de flags pendientes operativos. |
| I-18 | [I-18_Coaching_Secuencial_Por_Idea.md](I-18_Coaching_Secuencial_Por_Idea.md) | Sprint 2 | **DONE local 2026-07-25.** Extiende I-06 para afinar una idea a la vez con I-03, estado/contador por idea, revisiones enlazadas, umbral server-side, salida/tiempo/fallback y gates apagados por defecto. Pendiente D5/UAT/costo antes de activar. |
| I-19 | [I-19_Consolidacion_Progresiva_Ideas.md](I-19_Consolidacion_Progresiva_Ideas.md) | Completa local | **DONE local 2026-07-28.** Idea/versiones, confirmación y evaluación de versión completa, cola/reapertura, Resultados/Markdown, observabilidad y QA; quedan D5/UAT/costo operativos y seeds I-12 bloqueadas. |
| I-20 | [I-20_Redaccion_Conversacional_Fluida_y_Markdown_Ejecutivo.md](I-20_Redaccion_Conversacional_Fluida_y_Markdown_Ejecutivo.md) | Inmediata | **DONE local 2026-07-28.** Redactor LLM por acto, sin delegar decisiones; `promptRefs.conversacion`, guardrails/fallback/cupos y Markdown con umbral/origen/escala. Pendiente D5/UAT/costo. |
| I-12 | [I-12_Seed_Thoughts.md](I-12_Seed_Thoughts.md)                                                                                                                                                                                                       | Sprint 2              | **BLOCKED — insumo vencido** (seeds de Felipe no recibidos al 2026-07-20; **escalar**)                                                                                        |
| I-16 | [I-16_Fix_Calificacion_Markdown.md](I-16_Fix_Calificacion_Markdown.md)                                                                                                                                                                               | Sprint 1a             | **DONE 2026-07-15** (Markdown usa la evaluación más reciente por `fecha`; regresión determinística verde)               |
| P-03 | [P-03_Reiniciar_Conversacion.md](P-03_Reiniciar_Conversacion.md) — **ampliada a sistema de reinicio de datos** (participante Y campaña completa: conserva campaña/config/usuarios, borra conversaciones/respuestas/Markdown y resetea participantes) | Sprint 1a             | **DONE 2026-07-13/14** (reinicio por participante y por campaña; backend verde y committeado; `Seguridad:PermitirReinicioDatos` se apaga en el freeze) |
| P-07 | [P-07_Consentimiento_Datos.md](P-07_Consentimiento_Datos.md)                                                                                                                                                                                         | ~~Sprint 2~~ → **DIFERIDA** | **⚠️ DIFERIDA del MVP (reunión 20-jul):** consentimiento innecesario en herramienta interna (IP de GHT). El aviso puede ir en el `MensajeInicial` sin código si se pide. |
| P-09 | [P-09_Monitoreo_Dia_D.md](P-09_Monitoreo_Dia_D.md)                                                                                                                                                                                                   | Pruebas 4–8 ago       | **⚠️ PANEL DIFERIDO (reunión 20-jul):** basta health-check; métricas de tokens no prioritarias. **Se conservan** `/health(/ready)`, logs de entrega, **acta de flags** y **runbook de rollback** para el go-live. |
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
| I-14 | Segmentación por tags          | **BLOCKED — datos/config**: tags ya existen; falta que GHT entregue el catálogo consolidado (nombre, tipo, descripción opcional y estado) para aplicarlo en la carga masiva (I-08). No inventar ni hardcodear tags. |
| I-15 | Rebranding                     | Branding, post-convención.                                                                                                                                                                                                                                                                |
| P-01 | Validar entrega real E2E       | **COMPLETA (confirmado 2026-07-20):** flujo real validado envío→ventana→evaluación→Markdown con entregas monitoreadas. Ruta crítica Meta despejada. Sin código.                                                                                                                           |
| P-02 | Plantilla HSM de inicio        | **COMPLETA (confirmado 2026-07-20):** plantilla de inicio aprobada por Meta y configurada (`WhatsApp__PlantillaEnvioInicial__*`).                                                                                                                                                         |
| P-12 | ARMA como campaña/módulo       | **Diseño futuro** (reunión aparte). I-09/I-10 se diseñaron para habilitarlo sin reescritura.                                                                                                                                                                                              |

### 1.3 Alcance comprometido para la convención (re-priorizado con GHT, reunión 2026-07-20)

**Dentro del alcance — deben quedar listas/validadas para el Hito del 12-ago:**
I-01, I-02, I-03 ✓, I-04, I-05 ✓, I-06 ✓, I-07, I-08 ✓, I-11 ✓, I-12, I-13, I-14, I-16 ✓,
**I-17 (BD de dos niveles)**, **I-18 (coaching secuencial; DONE local)** y
**I-19 (consolidación progresiva; WIP local)**,
P-01 ✓, P-02 ✓, P-03 ✓, P-10 ✓ y P-13 ✓. Del MVP conversacional
se mantiene también el **cierre no determinista** (I-02 + inactividad ~5 min) y el **health-check +
acta de flags + runbook** (lo que se conserva de P-09).

**Pista de remediación autorizada el 2026-07-24:** `P-15` a `P-20` corrigen seis hallazgos confirmados de calidad, contrato y accesibilidad. Se ejecutan de forma secuencial antes de retomar un ítem de Sprint 2 bloqueado por terceros; no amplían el comportamiento de producto, no requieren nuevos datos GHT y no sustituyen los bloqueos de I-12/I-13/I-14.

**DIFERIDAS del MVP por la reunión del 20-jul (Capa 3 / post-convención):**
- **I-09 + I-10 — tejido colectivo:** ligado al HITL (aplazado) y requiere base curada → "Capa 3".
  El core ya existe detrás de flag OFF; **no se valida para el Hito**.
- **P-07 — consentimiento de privacidad:** innecesario en herramienta interna (IP de GHT).
- **P-09 — panel de monitoreo en vivo:** basta un health-check; métricas de tokens no prioritarias.
- **HITL (humano en el ciclo):** nunca tuvo spec; el riesgo de ideas "locas" se gestiona en análisis
  post-evento, no en vivo.

**Fuera del alcance (rama de deseables, sin cambio):** I-15, P-04, P-05, P-06, P-08, P-11, P-12.

**Decisiones adicionales de la reunión (registradas):**
- **Paráfrasis (I-05) solo tras el umbral** de la rúbrica (confirma que la idea está lista para
  guardarse como madura; se guarda salvo "no" explícito). Refina el disparo de I-05 → detalle en I-17 §3.3.
- **Carga masiva (I-08) con variables demográficas** (departamento, antigüedad…) para el análisis
  post-evento; **Munir entrega la lista**. ✓ **Recibida el 2026-08-07** — llegó como plantilla oficial
  completa, no como extensión aditiva: **reemplaza** las columnas de v1 y obliga a reabrir I-08
  (`I-08 v2`, ver §1.1 y TODO 22a). No fue un cambio menor: arrastró `codigoUsuario`, reasignación de
  números y la unique key de `users`.
- **Nombre confirmado: "Tejido de Red"** ("Bright Idea" ya está en uso por otro equipo GHT). Finalizar
  el nombre visible del contacto de WhatsApp antes del lanzamiento (I-15 sigue post).
- **Infraestructura:** el MVP se queda en la infra de **Aliado**; migración a GHT es post-evento.
- **Reabrir conversación:** solo herramienta **interna de pruebas** (ya cubierto por P-03), no de cara
  al usuario.

**Insumos y actividades externas — seguimiento (estado al 2026-07-20):**

| Insumo / actividad | Responsable | Fecha | Estado |
|---|---|---|---|
| Priorización de iniciativas | Felipe / Munir | 14-jul | ✓ Confirmada (reunión + correo) |
| Fecha de la convención | Felipe / Munir | 14-jul | ✓ Confirmada: ≈24-sep-2026 |
| Rúbrica recalibrada — workshop I-11 | Felipe / Munir | 18-jul | ✓ **Congelada** (desbloquea I-03; precondición de I-01 cumplida) |
| Pensamientos semilla (I-12) | Felipe | 18-jul | ✗ **VENCIDO — ESCALAR** (bloquea I-12 y el afinado de I-04/I-13) |
| Decisión rúbrica agnóstica vs tailored (I-13) | Felipe / Munir | 25-jul | Pendiente |
| Lista final de participantes | GHT | 1-ago | **Parcial (2026-08-07).** Llegó `Información asistentes convención gerentes 2026 V1.xlsx` (129 filas), pero **`Telefono`, `Idioma` y `Empresa` vienen vacías en todas las filas**. Sin teléfono no hay WhatsApp y ninguna fila entra → **pedir la V2 diligenciada**. Bloquea la carga real (TODO 22). |
| **Variables demográficas para I-08** (departamento, antigüedad…) | **Munir** | reunión 20-jul → **✓ recibido 2026-08-07** | ✓ **Cerrado.** Son las columnas de la plantilla oficial: `Empresa`, `ID Empresa`, `Sede`, `Cargo`, `Antigüedad en la empresa en años`, `Idioma`, `Email`. Ya incorporadas a `I-08 v2` como campos de primer nivel de `Usuario`. |
| Rúbrica reescrita (Felipe, tras 20-jul) + curar seeds I-12 | Felipe | "para mañana" (≈21-jul) | Pendiente de confirmar recepción |
| Plantilla HSM de inicio (P-02) | Aliado TI | Semana 0 | ✓ Aprobada por Meta y configurada |
| Validación E2E real (P-01) | Aliado TI | Semana 0 | ✓ Validada con entregas monitoreadas |
| Baseline D5 real (corrido pagado contra staging) | Aliado TI (op humana) | antes de Pruebas 4-ago | Pendiente (árbitro de I-03/I-05 y del umbral I-01) |

## 2. Plan de ejecución (Cronograma de la hoja + decisiones D1–D9)

> **Regla transversal:** nada nuevo se considera hecho sin (1) flag apagado por defecto,
> (2) métrica en el dashboard, (3) banco de calibración o suite de regresión en verde,
> (4) línea en el runbook de rollback. **El LLM propone, el sistema dispone** (R-01).

- **Remediación técnica (24–25 jul) — CERRADA local:** P-15 (cohesión del orquestador) → P-16
  (descomposición de campañas) → P-17 (errores API uniformes) → P-18 (nombres accesibles) → P-19
  (estados dinámicos anunciados) → P-20 (pestañas accesibles). Se preservaron contratos/permisos y
  el comportamiento salvo las correcciones explícitas.
- **I-18 coaching secuencial (25-jul) — DONE local:** extiende I-06/I-03/I-17 con cola y revisiones
  por idea, detrás de flag por campaña y kill-switch. Backend 484/484 y portal 24/24 verdes; exige
  D5/UAT/costo antes de activarse.
- **I-19 consolidación progresiva (27–28 jul) — DONE local:** la idea canónica confirmada ya gobierna
  evaluación, reapertura y resultados. **I-20/P-24/P-25 (28–29 jul) — DONE local:** redacción fluida,
  Markdown ejecutivo y coaching directo sin confirmación repetitiva, sin delegar decisiones.
- **P-26 (31-jul), P-27 y P-28 (04-ago) — DONE local:** P-26 entrega participación continua y
  enrutamiento; P-27 corrige los alias y contabiliza sus clasificaciones LLM; P-28 responde saludos
  sin convertirlos en ideas. Sus flags permanecen apagados hasta D5/UAT/costo; el siguiente corte es P-29.

- **Semana 0 (9–13 jul) — CERRADA:** P-02 radicada **y aprobada**; P-01 E2E real **validado**
  (ambas confirmadas 2026-07-20); staging (D8); workshop I-11 **realizado (rúbrica congelada
  18-jul)**; seed thoughts I-12 **NO entregados (vencido — escalar a Felipe)**. Cupos de P-10
  implementados (2026-07-13).
- **Sprint 1a (14–18 jul) — CERRADO:** P-03 ✓ (reinicio de datos), P-10 ✓ (cupos + rate por número
  + costo LLM), D5 ✓ (baseline real pendiente), I-16 ✓, I-08 backend ✓, diseños I-06/I-09 ✓, y las
  implementaciones locales de **I-06 (15-jul)** e **I-09 core (17-jul)** llegaron adelantadas con
  flags apagados. I-01 quedó preparada (runbook + observabilidad + regresión) y **BLOCKED** para el
  flip humano (falta baseline D5 real; la rúbrica ya está ✓).
- **Sprint 1b (21–25 jul) — EN CURSO:** I-06 ✓ (flag off); I-05 parafraseo ✓ (2026-07-20, Codex);
  I-08 UI ✓ (2026-07-20, Claude); I-03 ✓ (2026-07-21) y P-13 ✓ (2026-07-21). **I-09 core** quedó
  hecho pero **DIFERIDO** por la reunión del 20-jul (flag OFF, fuera de ruta crítica). **I-10 ya NO es
   el ítem actual** (se difirió con I-09). **I-17 está DONE local (2026-07-22)** y la remediación
   P-15→P-20 e I-18 cerraron localmente. El siguiente paso es operativo (D5/UAT/costo) o depende de
   insumos externos de I-12/I-13/I-14.
- **Sprint 2 (28 jul–1 ago) — parametrización + robustez:** prueba de carga el 28 (D7, decide
  cola/jobs/RU); **I-20 redacción fluida y Markdown ejecutivo** sobre I-19; **extender I-08 con
  variables demográficas** (insumo de Munir); I-12 seed thoughts (**BLOCKED hasta insumo de Felipe —
  escalar**); I-13 decisión; I-14 tags; **cierre por inactividad ~5 min** (granularidad sub-hora,
  I-17 §7); P-10 restante **ya hecho en S1a** (verificar y saltar); resiliencia LLM (D6).
  ~~I-10 / P-07~~ salen del sprint (diferidas).
- **Pruebas (4–8 ago):** UAT conjunta Felipe/Munir/Jason; calibración con el banco como árbitro;
  health-check + runbook (lo conservado de P-09); **acta de flags del día-D (6-ago)**: multi-idea (I-06)
  **y el umbral de cierre/madurez** solo quedan ON si pasaron carga + UAT + costo (checklist en
  `P-09 §3.4`). **El tejido (I-09/I-10) NO entra al acta: queda OFF, diferido.**
- **Activar + calibrar umbral I-01 en staging (op humana, ventana Pruebas):** precondición: rúbrica
  I-11 congelada **✓ (18-jul)** + **corrido D5 real** contra staging (pendiente). Pasos: elegir el
  valor sobre la distribución de scores del banco (P85–P90 conservador), activarlo — **con P-13
  implementada, como override en la campaña de prueba** (reversible por campaña) en vez del flip del
  App Setting global `Conversacion__UmbralCierreAnticipado` —, verificar vía
  `LogSeguridad(CierreUmbralAnticipado)` en App Insights, y llevar la decisión on/off al **acta de
  flags del día-D (6-ago)**. Responsable: humano/ops.
  Ver `Especificaciones/planes/Runbook_I-01_Umbral_Cierre_Anticipado.md` y `SUPUESTOS.md#activacion-umbral-i01`.
- **Freeze (11 ago):** code freeze; carga real (I-08); dry-run E2E; congelar rúbrica/prompts/seeds.
- **HITO (12-ago):** envío escalonado por lotes con monitoreo; ante síntoma se apaga el flag según
  runbook, nunca hotfix en caliente.
- **Post (rama de deseables / Capa 3):** P-04, P-11, P-08, P-06, P-05, I-15, P-12 y — **movidas aquí
  por la reunión del 20-jul** — **I-09/I-10 (tejido colectivo), P-07 (consentimiento) y el panel de
  P-09**. La Capa 3 (base curada + HITL + tejido + insights) es una fase de desarrollo posterior.
  (P-13 salió de esta lista y entró al MVP, adelantada a Sprint 1b, 2026-07-20.)

## 3. Dependencias duras (ruta crítica)

`P-01/P-02 (Meta)` **✓** → `I-11 (rúbrica)` **✓ 18-jul** → `I-03` **✓ 2026-07-21** ·
`I-12 (seeds)` **BLOCKED (insumo vencido — escalar)** → `I-04/I-13` · `P-10 cupos` **✓** →
`I-01/umbral (activar)` ← simplificada por `P-13` **✓** → habilita `I-17 (BD dos niveles)` **✓** ·
`I-06 + I-03 + I-17 + P-15` → `I-18 (coaching secuencial)` **DONE local** ·
`I-18 + I-05 + P-23` → `I-19 (idea consolidada)` **DONE local** →
`I-20/P-24/P-25` **DONE local** → `P-26` **DONE local** → `P-27` **DONE local (5/5)** →
`P-28` **DONE local (3/3)** → `P-29` **objetivo vigente (corte 1/2)** ·
`I-17/I-19` → `P-29` **ESPECIFICADA** (solo pausa humana tras cierre existente) ·
`I-19/P-26` → `P-30` **DONE local** (selector histórico que amplía la reapertura reciente). ·
`I-19/I-20/P-26/P-29/P-30/P-31/P-32` → `P-33` **DONE local 3/3** (consulta y cierre
visible; D5/UAT pendientes) → `DT-P32-02` **ESPECIFICADA 0/3** (semillas/JSON/readiness; siguiente
código) → corrida P-32 green → `DT-I20-02` **ESPECIFICADA 0/3** (contrato visible). ·
`I-08` **✓ backend + UI** → (extensión demográfica, insumo Munir) → carga
real del freeze. **Fuera de la ruta crítica del MVP (diferidas a Capa 3):** `I-09 → I-10`, `P-07`,
panel `P-09`. **Insumos externos en rojo: seeds de Felipe (I-12) y variables demográficas de Munir (I-08).**

## 4. Parametrización por campaña (análisis 2026-07-13, decisión del usuario: no perder flexibilidad)

> **Principio rector:** todo lo que define el **comportamiento del coach o el contenido** de una
> campaña es **parametrizable por campaña** (una campaña sin seed thoughts simplemente no los
> tiene; ARMA/P-12 podrá configurar lo suyo sin tocar código). Las **salvaguardas de seguridad y
> costo** quedan **globales** como kill-switch de operación (freeze/día-D), aunque sus *valores*
> vivan en la campaña. Regla técnica: cada campo nuevo de campaña es **aditivo con default
> seguro** (`03 §3.3` en commit aparte); documento viejo sin el campo = comportamiento actual.
>
> **Excepción confirmada 2026-07-27:** I-19 corrige la unidad de evaluación y no es una preferencia
> de campaña. Se activa transversalmente, sin campo por campaña; solo tiene kill-switch global de
> emergencia con default `true`.

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
| I-12 seed thoughts | `seedThoughts` (texto/lista, default vacío) | **vacío = la campaña no los tiene** (el ejemplo del usuario) |
| I-06 multi-idea | `segmentacionIdeas` (bool, default `false`) — **por campaña** (implementado 2026-07-15; flag apagado hasta validación de staging) | `false` = modo 1-idea |
| I-05 parafraseo | `parafraseo` (bool, default `false`) — **implementado 2026-07-20**; reunión 20-jul: además **solo dispara tras el umbral** (I-17) | `false` = retro clásica |
| I-20 redacción conversacional | `promptRefs.conversacion` opcional por campaña/pregunta; pregunta prevalece — **TODO** | ausente = `retro` efectivo guía el tono; no cambia la lógica |
| I-17 madurez (dos niveles) | Reutiliza `umbralCierreAnticipado` (`double?`, precedencia pregunta→campaña→global) — **DONE local** | la clasificación siempre se calcula; el cierre anticipado queda apagado mientras su kill-switch global esté OFF |
| I-18 coaching secuencial | `coachingSecuencialIdeas` (bool, default `false`) + `minutosCoachingPorIdea` (`int?`, null = hereda global) — **DONE local** | `false` = confirmación multi-idea anterior; minutos `<=0` = sin timeout por idea |
| P-21 número saliente de WhatsApp | `configConversacional.numeroWhatsAppSaliente` (alias `string?`, default null) — **DONE local 2026-07-25** | **null = usa el número predeterminado global** (`WhatsApp:AliasPredeterminado`); la respuesta conversacional NO usa este campo: sale siempre por el número entrante |
| P-26 participación continua | `configConversacional.participacionContinua` (bool, default `false`) — **DONE local 2026-07-31** | `false`/ausente = recorrido único actual; `true` permite ideas/ciclos nuevos solo con campaña `activa` |
| P-27 clasificación flexible de control | `configConversacional.clasificacionIntencionControl` (bool, default `false`) — **DONE local 2026-08-04** | `false`/ausente = sin llamada flexible; los alias deterministas corregidos siguen disponibles |
| P-29 cierre por tiempo | Reutiliza el temporizador existente `ConfigConversacional.MinutosInactividadSesion` (I-17); solo añade `promptRefs.cierre` opcional y telemetría del mensaje de pausa — **DONE local 2026-08-04** | kill-switch global `CierrePorTiempoHabilitado` OFF conserva íntegro el cierre por inactividad existente, sin mensaje humano |
| P-28 despertar / P-30 retomar | Sin campo por campaña: capacidades transversales con kill-switch global (`promptRefs.reactivacion` y vocabulario configurable son opcionales) — **ambas DONE local 2026-08-04** | OFF: P-28 no responde a saludo/inicio no sustantivo sin flujo; P-30 no muestra selector histórico y sigue la reapertura reciente I-19/P-26 |
| ~~I-09/I-10 tejido colectivo~~ | ~~`tejidoColectivo`~~ | **DIFERIDA (Capa 3)** — el campo existe en el modelo pero queda OFF; su UI no se construye para el MVP |

### 4.3 Candidatas a por-campaña (decidir al implementar; post-Hito si aprieta el freeze)

| Iniciativa | Propuesta | Nota |
|---|---|---|
| I-01 umbral de cierre | `umbralCierreAnticipado` por campaña — **formalizado como spec [P-13](P-13_Umbral_Cierre_Por_Campania.md)** | Patrón: default numérico global + override por campaña (`campaña ?? global`) y kill-switch booleano global `Conversacion:CierreAnticipadoHabilitado` (decisión confirmada 2026-07-21). |
| P-08 nudges | `nudgesHabilitados` + plantilla por campaña | Post; requiere plantilla HSM aprobada |
| P-02 plantilla de inicio | `MensajeInicial.PlantillaWhatsApp` ya existe en el dominio | Alternativa descartada en su momento (invariante crítico en operación manual); retomar solo si ARMA exige plantillas distintas |
| Textos conversacionales (P-32) | Catálogo global versionado por idioma + `Campania.localizaciones` | **Corte 1/4 DONE local 2026-08-10.** Catálogo/API/caché/semillas listos con gate OFF; runtime y campañas pendientes. |
| Visibilidad de idea (P-33) | `consultaIdea` + `mostrarIdeaAlCerrar` | Defaults `true`, gobernados por kill-switch global OFF; permiten opt-out independiente sin ampliar autorización. |

### 4.4 Deliberadamente GLOBALES (no por campaña)

| Palanca | Por qué global |
|---|---|
| `Conversacion:CuposHabilitados`, `MaxTurnosPorHilo` | Salvaguardas de terminación/costo (D2): kill-switch de operación; los *valores* sí son por campaña |
| `Conversacion:CoachingSecuencialIdeas` (I-18) | Kill-switch global de operación; la activación funcional y el tiempo efectivo siguen siendo por campaña |
| `Conversacion:ConsolidacionProgresivaHabilitada` (I-19) | Capacidad transversal acordada para todas las campañas; default `true`, sin flag por campaña. Solo kill-switch global de emergencia; apagado conserva aportes pendientes y no vuelve a evaluarlos aislados. |
| `Conversacion:RedaccionConversacionalFluidaHabilitada` (I-20) | Kill-switch transversal para la voz dinámica; default `true`. Apagado usa respaldo seguro sin tocar estado, evaluación ni consolidación. |
| `Conversacion:ClasificacionIntencionControl` (P-27) | Kill-switch global de una capacidad que puede proponer cierres; default `false`. La campaña también debe optar y el servidor conserva la decisión. |
| `Conversacion:DespertarProactivoHabilitado` (P-28) | Kill-switch global; default `false`. Apagado, el sistema no responde a saludo/inicio no sustantivo sin flujo; P-26 conserva aportes sustantivos elegibles. |
| `Conversacion:CierrePorTiempoHabilitado` (P-29) | Kill-switch global; default `false`. Gobierna **solo el mensaje de pausa humano**; apagado, el cierre por inactividad de I-17 sigue operando. El umbral es el existente `MinutosInactividadSesion` (por campaña ?? global), no uno nuevo. |
| `Conversacion:RetomarIdeasHabilitado` (P-30) | Kill-switch global; default `false`. Apagado, no hay selector histórico; se conserva la reapertura reciente vigente de I-19/P-26. |
| `Seguridad:PermitirReinicioDatos` (P-03) | Protección de datos en producción; se apaga en el acta del freeze |
| Rate por número / presupuesto-alerta de costo (P-10 restante) | Protección transversal de la plataforma |
| `Conversacion:RecuperacionSemantica` (I-09 opción B) | Capacidad de infraestructura (embeddings), no comportamiento de campaña |
| I-16 (fix), I-08, P-03, P-09 | Correcciones y herramientas admin: aplican a todas las campañas |

## 5. Disciplina de cambios

Cada iniciativa se implementa con el prompt estándar del repo: leer AVANCES/SUPUESTOS/spec de la
iniciativa → declarar rol y REQ/ARQ → pasos pequeños con build `-warnaserror`/test/format verdes →
cambios de contrato `03`/`04`/`08` **siempre aditivos y en commit aparte** que actualiza la spec →
commit atómico (Conventional Commits, "ATI JPC") → push solo cuando el usuario lo pida.
