# 00 — Plan de Pruebas E2E · El Tejido

> **Objetivo:** validar El Tejido de punta a punta antes de producción (**Hito 10-ago-2026**), ejecutable por **1 tester manual**, con enfoque **risk-based** y foco en go-live sin fallas.
> **Base:** `Especificaciones/base/13_Plan_de_Pruebas_y_Aceptacion.md`, `Iniciativas/00_Indice_y_Plan_de_Ejecucion.md`, `AVANCES.md`, `Reglas_Conversacion_y_Participacion.md`, `Guia_Prueba_E2E_Simulada_WhatsApp.md`.
> **Última revisión del estado real:** 2026-07-21.

---

## 1. Alcance

### 1.1 Qué se valida (in-scope para el Hito) — re-priorizado reunión GHT 20-jul
Flujo conversacional completo del coach (cold-start → evaluación → revisión determinista → **clasificación de madurez I-17** → cierre → Markdown), portal admin (login OTP, CRUD de campañas/usuarios/rúbricas/prompts/config LLM, envíos, resultados) y las salvaguardas transversales. Iniciativas comprometidas para la convención (índice §1.3): **I-01, I-02, I-03 ✓, I-04, I-05 ✓, I-06 ✓, I-07, I-08 ✓, I-11 ✓, I-12, I-13, I-14, I-16 ✓, I-17 (NUEVA), P-01 ✓, P-02 ✓, P-03 ✓, P-10 ✓, P-13 ✓**. Se valida también el **cierre no determinista** (intención "no más" I-02 + inactividad ~5 min) y el **health-check + acta de flags + runbook** (lo conservado de P-09).

### 1.2 Fuera de alcance (diferidas / deseables, post-convención)
- **Diferidas por la reunión del 20-jul (Capa 3):** **I-09 + I-10 (tejido colectivo)** — su código existe pero el flag `tejidoColectivo` queda **OFF** y **no se valida** para el Hito (si acaso, smoke de que off = comportamiento autocontenido); **P-07 (consentimiento)**; **panel en vivo de P-09** (basta health-check; métricas de tokens no prioritarias); **HITL**.
- **Rama de deseables (sin cambio):** I-15 (rebranding), P-04 (dashboard), P-05 (insights), P-06 (destilación), P-08 (nudges), P-11 (informe consolidado), P-12 (ARMA). No se prueban salvo smoke visual si ya están desplegados.

> **Nota de seguridad sobre el tejido diferido:** aunque I-09 sale del alcance funcional, si por error quedara con flag ON, sus casos de seguridad (SEC-06..08 anonimización, SEC-12 injection transitiva) siguen siendo **CORE**. Verificar en el smoke que `tejidoColectivo` está OFF en todas las campañas del Hito.

### 1.3 Estado real relevante (de `AVANCES.md` / índice, al 2026-07-21)
| Función | Estado | Implicación para QA |
|---|---|---|
| I-03 follow-up eje débil + `FiltroSalidaRubrica` | **DONE local**; filtro **siempre-on** | Probar sí o sí (seguridad). D5 real vs staging pendiente. |
| I-05 parafraseo | **DONE local**; flag campaña + kill-switch, **default off** | Probar encendido y apagado. |
| I-06 multi-idea | **Código DONE**; `segmentacionIdeas` **default off** (kill-switch global `true`) | Probar bajo flag encendido en sim. |
| I-08 carga masiva | **DONE** (backend + UI) | Probar CSV/XLSX + reporte por fila. |
| I-09 tejido colectivo | **DIFERIDA (reunión 20-jul, Capa 3)**; `tejidoColectivo` **default off** | **Fuera del alcance funcional del Hito.** Solo smoke: verificar que el flag está OFF; si por error queda ON, SEC-06..08/SEC-12 siguen CORE. |
| I-17 BD dos niveles (maduro/incubación) | **NUEVA (reunión 20-jul); pendiente de implementar** — puntos de diseño abiertos (I-17 §5) | Cuando esté en el build: clasificación determinista por umbral, paráfrasis solo tras umbral, filtro en Resultados. Hasta entonces marcar **BLOCKED**. |
| P-03 reinicio de datos | **DONE** (participante y campaña); `Seguridad:PermitirReinicioDatos` **se apaga en el freeze** | Herramienta base para re-correr casos. |
| P-10 cupos/rate/costo | **Backend HECHO**; `Conversacion:CuposHabilitados` **default off**; techo turnos y rate por número independientes | Probar encendiendo cupos en sim. |
| P-13 override umbral por campaña | **← ACTUAL** (impl en curso); `configConversacional.umbralCierreAnticipado` (`double?`) | Confirmar herencia global vs override. Si aún no está en el build de prueba, marcar **BLOCKED** y usar el global. |
| I-01 cierre por umbral | Mecanismo existe, **`UmbralCierreAnticipado` default 0 = off**; activación humana en Pruebas | Probar activando por override P-13 (o global). |
| I-12 seed thoughts | **BLOCKED** (insumo vencido) | No bloquea el Hito conversacional; validar solo "campaña sin seeds = comportamiento base". |

> **Regla de ambigüedad:** ante duda sobre el estado real, asumir el **comportamiento documentado** en `Reglas_Conversacion_y_Participacion.md` y `AVANCES.md`, y **marcar el caso como "Verificar build"**.

---

## 2. Estrategia risk-based

Priorización por riesgo × impacto de go-live. Cada área tiene un peso que determina si sus casos son **CORE (must-pass, bloquean el go)** o **Extendido (Should/Could)**.

| # | Riesgo | Por qué duele en producción | Casos |
|---|---|---|---|
| **R1** | **No determinismo del LLM** | Falsos negativos en QA; comportamiento inestable el día-D | Transversal: todos los criterios son **cualitativos/tolerantes** (§6). |
| **R2** | **Fuga de rúbrica** (I-03 / `FiltroSalidaRubrica`) | Revela mecánica de evaluación; rompe la premisa del producto | CORE — SEC-01..05 |
| **R3** | **Fuga de PII entre participantes** (I-09 tejido) | Incidente de privacidad grave | CORE — SEC-06..08 |
| **R4** | **Prompt-injection** (directa y transitiva I-09) | El modelo ejecuta/revela lo que no debe | CORE — SEC-09..12 |
| **R5** | **Guardrails deterministas fallan** (cupos, techo turnos, rate, presupuesto) | Costo/loop sin control; el día-D se dispara el gasto | CORE — GRD-01..07 |
| **R6** | **Fallback del LLM inseguro** | Error técnico visible al participante; conversación rota | CORE — ROB-01..03 |
| **R7** | **Duplicados de webhook / firma** | Evaluaciones duplicadas, doble cobro de tokens | CORE — ROB-04..06 |
| **R8** | **Auth OTP / secretos** | Acceso indebido al portal; secreto en logs/Markdown | CORE — AUT-01..05 |
| **R9** | **Repregunta única / máquina de estados** | Loop infinito de revisiones | CORE — CNV-04..06 |
| **R10** | **Ventana 24h / expiración / multi-pregunta** | Hilos colgados, mensajes perdidos | Ext/CORE — ROB-07..10 |
| **R11** | Multi-idea (I-06), tejido (I-09), parafraseo (I-05) bajo flag | Costo/latencia y regresión al encender | Ext — FLG-* |
| **R12** | Carga masiva sucia (I-08) | Lista real del freeze mal cargada | CORE — ADM-08 |

**Definición de CORE (go/no-go):** conjunto mínimo que **debe pasar** para autorizar el envío del 10-ago. Está marcado caso por caso en `02_Casos_de_Prueba_E2E.md` y consolidado en `03_Smoke_y_Checklist_Dia_D.md`. Todo lo demás es suite extendida (mejora la confianza; su falla se gestiona por severidad, no bloquea automáticamente).

---

## 3. Ambientes

| Ambiente | Uso | Cómo | Costo |
|---|---|---|---|
| **SIM** — simulación firmada | Grueso de los casos (funcionales, borde, negativos, seguridad, guardrails) | `/simulacion-whatsapp` con header `X-Diag-Key`. Local (Development, sin clave) o Azure desplegado (`Simulacion:Habilitada=true` + `X-Diag-Key`). Ver `Guia_Prueba_E2E_Simulada_WhatsApp.md`. | **Sin costo de WhatsApp**; costo LLM solo si `llm-key` real está cargada. |
| **REAL** — WhatsApp Cloud API | Confirmación de los **críticos** (recepción real, ventana 24h, plantilla HSM de inicio, OTP real cuando aplique) | Teléfonos reales de prueba; envío inicial desde `Envios`. | Costo de mensajes reales (acotar a los casos marcados `REAL`). |

**Regla de doble pasada:** cada caso CORE de flujo se ejecuta primero en **SIM** (barato, repetible) y se **confirma en REAL** solo el subconjunto marcado `Ambiente: real` (recepción física, ventana, plantilla, envío desde portal). El resto queda en SIM.

**Precondición de ambiente para evaluación LLM real:** cargar `llm-key` real y una `Config LLM` válida y activa. Sin ella, el orquestador cierra con **fallback neutro** y deja `evaluacionPendiente` (esto **no es fallo** del flujo; es el caso ROB-01). Para casos que exigen evaluación real, verificar primero que la Config LLM está activa.

> **Nota conocida (esperada):** en la prueba simulada contra Azure, el **envío inicial** desde `Envios` llama a Graph API y **falla sin `wa-token`/`PhoneNumberId` reales** — es esperado. El camino entrante simulado (webhook firmado) evalúa igual.

---

## 4. Roles y responsabilidades (1 persona)

| Rol | Persona | Responsabilidad |
|---|---|---|
| **Tester manual (único)** | QA asignado | Ejecuta todos los casos, registra bitácora y defectos, corre P-03 entre corridas. |
| **Apoyo ops (puntual)** | Ing./Ops | Flips de flags en Azure (App Settings), carga de secretos en Key Vault, activación/cierre de `Simulacion:Habilitada`. Solo cuando el caso lo pide. |
| **Árbitro de calibración** | Felipe/Munir + Jason | Deciden en UAT si una salida cualitativa del LLM es aceptable cuando el criterio deja margen (banco D5 como referencia). |

El tester **no** necesita tocar Cosmos a mano: **P-03** reinicia datos entre corridas (§ `04_Datos_de_Prueba_y_Reinicio.md`).

---

## 5. Cronograma (alineado a freeze/día-D)

| Fase | Fechas | Actividad de QA |
|---|---|---|
| **Preparación** | 28 jul – 1 ago | Cargar datos de prueba (`04_*`), verificar ambientes SIM/REAL, humo post-deploy inicial. Confirmar que P-13 está en el build (si no, marcar y usar umbral global). |
| **Ejecución SIM (CORE + Ext)** | 4 – 6 ago | Correr toda la suite en SIM; abrir defectos; re-correr con P-03. |
| **UAT conjunta + calibración** | 4 – 8 ago | Con Felipe/Munir/Jason; banco D5 como árbitro; calibrar umbral I-01 (por override P-13). |
| **Confirmación REAL (críticos)** | 6 – 8 ago | Subconjunto `Ambiente: real`; ventana 24h, HSM inicio, recepción física, OTP real. |
| **Acta de flags día-D** | **6 ago** | Decidir ON/OFF de: multi-idea (I-06), tejido (I-09), umbral I-01, cupos. Solo ON si pasaron carga + UAT + costo. |
| **Freeze** | 8 – 9 ago | Code freeze; **carga real (I-08)**; **dry-run E2E completo**; congelar rúbrica/prompts; apagar `Seguridad:PermitirReinicioDatos`; cerrar `Simulacion:Habilitada`. |
| **HITO** | **10 ago** | Envío escalonado por lotes + monitoreo (P-09). Ante síntoma → runbook de rollback (`07_*`), **nunca hotfix en caliente**. |

**Gate de salida a producción:** todos los CORE en verde + checklist de release (`13 §7`) completo + acta de flags firmada. Ver `03_Smoke_y_Checklist_Dia_D.md`.

---

## 6. Gestión del no determinismo del LLM (Riesgo #1)

El LLM **no** produce texto idéntico entre corridas. **Ningún caso valida texto exacto de salidas del modelo.** Reglas para todo el paquete:

1. **Criterios cualitativos, no literales.** Se aprueba por propiedades verificables (p. ej. "la retro es breve, en 2ª persona, accionable y **no** contiene nombres de criterio ni cifras N/M"), no por una cadena esperada. Detalle en `06_Criterios_Aceptacion_LLM.md`.
2. **Lo determinista sí se valida al pie de la letra.** Cupos, techo de turnos, rate por número, dedupe de webhook, firma, umbral (fórmula `Total >= Min + Umbral·(Max−Min)`), estados de la máquina, snapshots, cierre por agotar revisiones. Estos **no** dependen del modelo → resultado esperado exacto.
3. **Regla de oro de El Tejido:** *"el LLM propone, el sistema dispone"*. Cuando un caso mezcla LLM + salvaguarda, el criterio de aprobación recae en la **salvaguarda determinista**, no en la redacción del modelo.
4. **Repetición para inestabilidad.** Un caso cualitativo que "a veces pasa" se corre **3 veces** (reiniciando con P-03). Si falla ≥1 de 3 en una propiedad de **seguridad** (fuga rúbrica/PII, injection) → **defecto Crítico** (la seguridad no admite variación). Si falla en una propiedad de **calidad** (tono/utilidad) → se escala al árbitro de calibración (banco D5), no es bloqueante automático.
5. **Banco de calibración D5** como árbitro de calidad cuando el criterio deja margen. La **baseline D5 real contra staging está pendiente**: hasta tenerla, la calidad conversacional se juzga en UAT; la **seguridad no espera al banco** (se valida por filtro determinista).
6. **Fallback como resultado válido.** Si la Config LLM no está activa o el proveedor falla, el resultado correcto es fallback neutro + `evaluacionPendiente` + sin Markdown. No confundir con defecto (ver ROB-01..03).

---

## 7. Criterios de entrada y salida

### 7.1 Entrada (para empezar a ejecutar)
- Build desplegado en el ambiente de prueba con `/health` = 200 y `/health/ready` OK (incluye `X-Diag-Key`).
- Simulación habilitada (`Simulacion:Habilitada=true` + clave de diagnóstico) en el ambiente elegido.
- Datos de prueba cargados (`04_*`): admin, 5 participantes, campaña, 3 preguntas, rúbrica activa, prompt `evaluar` aprobado, Config LLM activa.
- Secretos mínimos en Key Vault: `jwt-sign`, `otp-salt`, `wa-appsec` (valor temporal para firmar el webhook simulado); `llm-key` real si se prueba evaluación real.
- P-03 disponible (`Seguridad:PermitirReinicioDatos=true`) para re-correr.

### 7.2 Salida (para autorizar go-live)
- **100 % de los casos CORE en verde** (o con defecto aceptado formalmente por severidad Baja + workaround documentado).
- **0 defectos abiertos de severidad Crítica o Alta** en seguridad/privacidad y guardrails.
- Checklist de release (`13 §7`) completo: CI verde, deploy OK, HSM aprobadas por Meta, secretos solo en Key Vault, E2E real con 5 usuarios ejecutado, `SUPUESTOS.md` revisado.
- **Acta de flags día-D** firmada (qué queda ON/OFF).
- Runbook de rollback (`07_*`) revisado y a mano para el día-D.

### 7.3 Criterios de suspensión / reanudación
- **Suspender** si: `/health` cae, se detecta fuga de rúbrica/PII en cualquier corrida, o el gasto LLM se dispara sin cupo. Escalar y apagar el flag según runbook.
- **Reanudar** cuando el defecto bloqueante esté cerrado y re-verificado en SIM con P-03.

---

## 8. Riesgos del propio testing y mitigación
- *Un solo tester → cuello de botella.* Mitiga: SIM barato + P-03 para re-correr rápido; CORE priorizado sobre Ext.
- *Baseline D5 real pendiente.* Mitiga: seguridad por filtro determinista (no espera banco); calidad a UAT.
- *P-13 puede no estar en el build.* Mitiga: si falta, calibrar umbral I-01 por el **global** `Conversacion:UmbralCierreAnticipado` y marcar el caso.
- *Costo LLM en pruebas.* Mitiga: grueso en SIM; evaluación real solo en casos que la exigen; presupuesto de tokens (P-10) encendido en la campaña de prueba real.

*Fin del documento.*
