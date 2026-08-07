# Estado de requerimientos — El Tejido (corte 2026-08-04, addendum 2026-08-06)

> **Atendido / DONE local** = codificado y probado en local; interruptores apagados, pendiente de
> activación operativa (UAT/D5/costo). Nomenclatura unificada REQ; entre paréntesis el código de spec.

## ✅ Atendidos (entregados)

**MVP base (cerrado y facturado):**
- REQ-001 — Autenticación de administrador por OTP de WhatsApp. Atendido. Login sin contraseña.
- REQ-002 — Identidad y matrícula de participantes. Atendido. Reconoce por número y valida matrícula.
- REQ-003 — Gestión de campañas, usuarios y preguntas. Atendido. CRUD y asociación en el portal.
- REQ-004 — Envío de mensajes iniciales por WhatsApp. Atendido. Con reenvío y reintentos.
- REQ-005 — Captura conversacional de ideas y consolidación. Atendido. Paráfrasis y confirmación.
- REQ-006 — Rúbricas, prompts versionados y configuración LLM. Atendido. API key segura.
- REQ-007 — Evaluación de ideas con LLM y retroalimentación. Atendido. Con snapshots reproducibles.
- REQ-008 — Compilación de artefactos Markdown por idea. Atendido. Consultable y regenerable.
- REQ-009 — Portal administrativo Angular con marca GHT. Atendido.
- REQ-010 — Seguridad, guardrails y observabilidad. Atendido. Rate limit, firma webhook, telemetría.
- REQ-011 — Plataforma base (.NET 8, Cosmos, API REST, CI/CD). Atendido.

**Iniciativas posteriores (DONE local):**
- REQ-015 (P-03) — Reinicio de datos por participante/campaña. Atendido (13–14-jul).
- REQ-016 (D5) — Banco de calibración de la evaluación. Atendido (14-jul). Baseline real pendiente.
- REQ-017 (P-10) — Guardrails: cupos, rate por número y costo LLM. Backend atendido (14-jul); portal pendiente.
- REQ-018 (I-16) — Fix de calificación en Markdown (usa la evaluación más reciente). Atendido (15-jul).
- REQ-019 (I-06) — Multi-idea (varias ideas por participante). Atendido (15-jul); flag OFF hasta validación.
- REQ-021 (I-05) — Paráfrasis y transparencia (solo tras umbral). Atendido (20-jul).
- REQ-022 (I-08) — Carga masiva de participantes. Atendido (15–20-jul); extensión demográfica pendiente (Munir).
- REQ-023 (I-03) — Follow-ups al eje débil de la rúbrica. Atendido (21-jul).
- REQ-024 (P-13) — Umbral de cierre por campaña. Atendido (21-jul); calibración pendiente.
- REQ-025 (I-17) — BD de dos niveles de madurez + cierre por inactividad por campaña. Atendido (22-jul).
- REQ-026 (P-14) — Vista de solo lectura de rúbricas y prompts. Atendido (22-jul).
- REQ-027 (P-15) — Refactor del orquestador conversacional. Atendido (24-jul). Remediación CAL-001.
- REQ-028 (P-17) — Errores de API uniformes. Atendido (24-jul). Remediación API-001.
- REQ-029 (P-16) — Refactor de página de campañas. Atendido (25-jul). Remediación CAL-002.
- REQ-030 (P-18) — Controles con nombre accesible. Atendido (25-jul). Accesibilidad.
- REQ-031 (P-19) — Estados dinámicos accesibles (errores/confirmaciones anunciados). Atendido (25-jul).
- REQ-032 (P-20) — Pestañas accesibles de campañas (ARIA/teclado). Atendido (25-jul).
- REQ-033 (P-21) — Multi-número de WhatsApp (responde por el número entrante). Atendido (25-jul).
- REQ-034 (P-22) — Mejoras de UX en campañas. Atendido (25-jul). Frontend-only.
- REQ-035 (P-23) — Mejoras de UX en resultados (maestro-detalle). Atendido (25-jul). Frontend-only.
- REQ-036 (I-18) — Coaching secuencial por idea. Atendido (25-jul); pendiente D5/UAT.
- REQ-037 (I-19) — Consolidación progresiva de ideas (versión canónica). Atendido (28-jul).
- REQ-038 (I-20) — Redacción conversacional fluida + Markdown ejecutivo. Atendido (28-jul).
- REQ-039 (P-24) — Evaluación implícita al solicitar mejora (fix). Atendido (29-jul).
- REQ-040 (P-25) — Coaching directo sin confirmación repetitiva. Atendido (29-jul).

## 🆕 Nuevas solicitudes (2026-08-06)

- **REQ-052 (P-31) — Visibilidad del progreso de la idea.** Al alcanzar un umbral configurable, el
  sistema presenta de forma proactiva la consolidación de la idea en la que se está trabajando y
  pregunta si quiere seguir madurándola. **Especificada el 06-ago y priorizada como la próxima
  iniciativa a implementar** (3 cortes, kill-switch OFF). El umbral que dispara el mensaje es
  **independiente** del que clasifica una idea como madura, y ambos se ajustan por campaña y por
  pregunta. Spec: `Especificaciones/Iniciativas/P-31_Resumen_Consolidacion_Por_Umbral.md`.
  - *Decisión abierta:* que el participante pueda pedir el consolidado **bajo demanda** ("muéstrame
    cómo va mi idea"). Hoy no existe esa ruta y la petición se guardaría como parte de la idea.
    Fuera del alcance de P-31 hasta decidirlo.
- **REQ-053 — Soporte de inglés en el chatbot.** Campo de idioma en el participante, mensaje de
  bienvenida bilingüe y respuestas del asistente en el idioma de la persona. **Pendiente de
  especificar:** el alcance real toca la plantilla aprobada de WhatsApp, los prompts, los
  vocabularios deterministas de reconocimiento de intención y el contenido de campaña (preguntas y
  mensajes de cierre). En análisis.

## 🔄 En curso

**En pruebas:**
- REQ-041 (P-26) — Campañas continuas y selección de campaña/pregunta. Código completo local (31-jul, 6/6); default OFF, en validación/UAT.

**Mejora del proceso de cierre:**
- REQ-042 (P-27) — Clasificación de intenciones de control (salidas naturales: "parar aquí", "pasar a otra idea"). Código completo local (04-ago, 5/5; backend 698/698); flags OFF, activación pendiente.

**En desarrollo:**
- REQ-012 (P-28) — Reactivación de conversación / despertar proactivo (el participante inicia/retoma). Código completo local (04-ago, 3/3); kill-switch OFF.
- REQ-013 (P-29) — Cierre conversacional por tiempo (mensaje de pausa humano). En desarrollo: corte 1 de 2 hecho (04-ago); pendiente redacción LLM + E2E.
- REQ-014 (P-30) — Reanudación de ideas anteriores (retomar cualquier idea previa). Especificada; próxima a iniciar (3 cortes); kill-switch OFF.

## ⏳ Pendiente

- REQ-043 (I-12) — Seed thoughts por campaña. Bloqueado: insumo de seeds vencido (escalar a Felipe).
- REQ-020 (I-09) — Tejido colectivo. Diferida a Capa 3 (core hecho, flag OFF).
- REQ-044 (I-10) — Flag base previa vs. en blanco (UI del tejido). Diferida a Capa 3.
- REQ-045 (P-04) — Dashboard de resultados. Diferida (deseable).
- REQ-046 (P-05) — Capa de insights. Diferida post-convención.
- REQ-047 (P-06) — Destilación por lotes. Diferida post-convención.
- REQ-048 (P-07) — Consentimiento de datos. Diferida (herramienta interna, IP de GHT).
- REQ-049 (P-08) — Recordatorios / nudges. Diferida; requiere plantilla HSM aprobada.
- REQ-050 (P-09) — Monitoreo día D (panel en vivo). Panel diferido; se conservan health-check + runbook.
- REQ-051 (P-11) — Informe consolidado. Diferida (deseable).
