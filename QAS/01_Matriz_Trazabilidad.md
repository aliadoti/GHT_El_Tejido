# 01 — Matriz de Trazabilidad · Iniciativa/REQ → Caso(s) → Prioridad → Estado

> Traza cada iniciativa/requisito comprometido a los casos de `02_Casos_de_Prueba_E2E.md`.
> **Prioridad:** CORE = must-pass (bloquea go-live) · Ext = extendido.
> **Estado build:** de `AVANCES.md`/índice al 2026-07-21. **Estado prueba:** lo llena el tester (Pendiente/OK/Falla/Bloqueado).

## 1. Por iniciativa

| Iniciativa / REQ | Qué valida | Caso(s) | Prioridad | Estado build | Estado prueba |
|---|---|---|---|---|---|
| **Auth OTP** (REQ §10; spec 06) | Login admin por código, normalización, código inválido/vencido neutral, rate de intentos, neutralidad | AUT-01..05 | CORE | Listo | |
| **Identidad/matrícula** (REQ §12,§26.3) | Reconocer por número normalizado; rechazo neutral de no autorizado | CNV-01, SEC-13 | CORE | Listo | |
| **Cold-start / primer contacto** (Reglas §2.1) | Primer entrante → envía pregunta, no evalúa el saludo | CNV-01 | CORE | Listo | |
| **Evaluación LLM + snapshots** (REQ §20; spec 08) | Evalúa vs rúbrica/prompt/config; guarda snapshots reproducibles | CNV-02, CNV-03, ADM-07 | CORE | Listo | |
| **Retro + invitación a mejorar** (REQ §21; Reglas §2.3) | Retro breve útil + 1 invitación variada que enseña la salida | CNV-03 | CORE | Listo | |
| **Repregunta única / máquina estados** (REQ §21,§26.6) | ≤1 repregunta (≤2 evaluaciones); al agotar → solo cierre | CNV-04, CNV-05 | CORE | Listo | |
| **Salida natural "no quiero seguir"** (I-02; Reglas §2.3 salida 2) | Frase de conformidad → no evalúa, acuse + cierre | CNV-06 | CORE | Listo | |
| **Cierre + Markdown** (REQ §22; spec 09) | Cierre con agradecimiento; Markdown generado sin secretos; regenerable | CNV-07, ADM-07, SEC-14 | CORE | Listo | |
| **Cierre por umbral alto** (I-01; Reglas §2.3 salida 1) | `Total >= Min+Umbral·(Max−Min)` cierra sin insistir | FLG-05 | CORE* | Mecanismo listo; activación humana | |
| **I-03 follow-up eje débil** (REQ §21; spec 08 §3.2) | Repregunta profundiza el eje débil en lenguaje natural | SEC-01, CNV-03 | CORE | DONE local | |
| **I-03 `FiltroSalidaRubrica`** (spec 08 §3.4, §5.10) | NUNCA revela rúbrica/criterios/puntajes; registra `fuga_rubrica` | SEC-01..05 | CORE | DONE (siempre-on) | |
| **I-05 parafraseo** (I-05; Reglas §2.2) | Resumen fiel 2–3 frases bajo flag; off = retro clásica; kill-switch | FLG-01, FLG-02 | Ext | DONE local, off | |
| **I-06 multi-idea** (I-06; Reglas §2.4.1) | Segmenta N ideas → N respuestas/evaluaciones/Markdown; fallback 1-idea | FLG-03, FLG-04 | Ext | DONE, off | |
| **I-08 carga masiva** (I-08) | CSV/XLSX → reporte por fila; idempotente; sin PII en logs | ADM-08, ADM-09 | CORE | DONE | |
| **I-09 tejido colectivo** (I-09; Reglas §2.9; spec 10 §5) | **DIFERIDA (reunión 20-jul).** Función fuera de alcance; si el flag quedara ON, la **seguridad sigue CORE**: anonimización + anti-injection transitiva | SEC-06..08, SEC-12 | CORE (seguridad, solo si ON) / — (función diferida) | Core DONE pero **OFF/diferido** | |
| **I-17 BD dos niveles** (I-17; reunión 20-jul) | Clasificación `maduro`/`incubacion` por umbral (fórmula determinista, escala de la campaña); paráfrasis solo tras umbral; filtro en Resultados | (por definir — CNV-08/ADM-13 al implementar) | CORE | **Pendiente (diseño abierto §5)** | |
| **P-03 reinicio de datos** (P-03) | Reinicio participante/campaña; conserva config; cold-start real; 409 con flag off | ADM-10, ADM-11 | CORE | DONE | |
| **P-10 cupo mensajes/usuario** (P-10; Reglas §2.8.1) | Al exceder → descarte neutral silencioso | GRD-01 | CORE | Backend, gated off | |
| **P-10 cupo llamadas LLM/usuario** (P-10; Reglas §2.8.2) | Al exceder → no llama LLM, cierra elegante | GRD-02 | CORE | Backend, gated off | |
| **P-10 techo turnos/hilo** (Reglas §2.8.3) | `MaxTurnosPorHilo` → cierra elegante; independiente del flag cupos | GRD-03 | CORE | Backend | |
| **P-10 rate por número** (spec 10 §2) | `RateNumeroWhatsAppPorMinuto` → descarte silencioso | GRD-04 | CORE | Backend | |
| **P-10 presupuesto tokens campaña** (spec 10 §2) | Al alcanzar → cierre elegante + `LogSeguridad(presupuesto_tokens_campania)` | GRD-05 | Ext | Backend, gated | |
| **P-13 umbral por campaña** (P-13) | `campaña ?? global`; override activa/desactiva; herencia | FLG-05, GRD-06 | Ext | ← ACTUAL (verificar build) | |
| **Ventana 24h** (Reglas §2.5) | Respuesta tardía reabre ventana; sistema responde en texto libre | ROB-07 | CORE | Listo | REAL |
| **Expiración por inactividad** (Reglas §2.6) | Hilo abierto sin actividad → cierre silencioso por barrido | ROB-08 | Ext | Listo (config) | |
| **Multi-pregunta** (Reglas §2.1) | Avance por `orden`; abre siguiente al cerrar la actual | ROB-09, CNV-05 | CORE | Listo | |
| **Idempotencia/dedupe webhook** (spec 10 §3.2) | `whatsappMessageId` repetido → no reprocesa | ROB-04, ROB-05 | CORE | Listo | |
| **Firma webhook** (spec 10 §3) | `X-Hub-Signature-256` inválida → 401 descarta | ROB-06, SEC-15 | CORE | Listo | |
| **Fallback seguro LLM** (spec 08 §6) | Proveedor caído / salida inválida / config no activa → retro neutra, `evaluacionPendiente`, sin Markdown | ROB-01..03 | CORE | Listo | |
| **Guardrail longitud entrante** (spec 10 §2) | >1500 char → trunca/rechaza seguro, registra | GRD-07 | Ext | Listo | |
| **Secretos / no fuga** (REQ §19.2; spec 10 §4-5) | `apiKeyRef` enmascarada; sin secretos en logs/telemetría/Markdown | AUT-04, SEC-14 | CORE | Listo | |
| **Consulta de resultados/filtros** (REQ §33.1.8; spec 11) | Filtra por campaña/usuario/área/tag/pregunta/calificación | ADM-12 | Ext | Listo | |
| **CRUD campañas/usuarios/rúbricas/prompts/config** (REQ §33.1) | Alta/edición; aprobar prompt; versionado | ADM-01..07 | CORE/Ext | Listo | |
| **Envíos** (REQ §33.1.5) | Enviar inicial, reenviar a no respondió, reintentar fallidos | ADM-06 | CORE | Listo | REAL |

\* CORE condicional: es CORE **si** el acta de flags decide activar el umbral I-01 para el día-D; si queda OFF, pasa a Ext (verificar solo que off = comportamiento base).

## 2. Cobertura obligatoria (regla de calidad)

| Categoría obligatoria | Casos que la cubren | ¿Cubierta? |
|---|---|---|
| **Seguridad — no fuga de rúbrica** | SEC-01, SEC-02, SEC-03, SEC-04, SEC-05 | ✅ |
| **Seguridad — no fuga de PII (tejido)** | SEC-06, SEC-07, SEC-08 | ✅ |
| **Seguridad — prompt-injection (directa + transitiva)** | SEC-09, SEC-10, SEC-11, SEC-12 | ✅ |
| **Seguridad — auth OTP y secretos** | AUT-01..05, SEC-14, SEC-15 | ✅ |
| **Guardrails deterministas** | GRD-01..07 | ✅ |
| **Robustez (24h, dedupe, expiración, fallback, multi-pregunta/idea)** | ROB-01..10 | ✅ |

## 3. Convención de estado prueba (para el tester)
`Pendiente` · `OK` · `OK con obs.` · `Falla (DEF-###)` · `Bloqueado (motivo)` · `N/A (flag off este ciclo)`.
Registrar cada resultado en la **bitácora** (`05_Plantillas_Defecto_y_Bitacora.md`).

*Fin del documento.*
