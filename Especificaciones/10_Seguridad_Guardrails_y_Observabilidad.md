# 10 — Seguridad, Guardrails y Observabilidad (transversal)

**Módulos:** `Application/Seguridad/` y `Infrastructure/Observabilidad/`.
**Implementa:** `REQ §10, §25, §30, §31.5, §31.6`; `ARQ §10, §11, §12, §13`.
**Aplica a:** todos los demás módulos. Provee servicios reutilizables; no es opcional.

---

## 1. Alcance
Conjunto base de controles **proporcional a un MVP** (`ARQ §11`): límites de abuso/consumo, anti prompt-injection (estructura en `08`), integridad del canal WhatsApp, manejo seguro de secretos, y observabilidad (trazabilidad de negocio + telemetría técnica).

---

## 2. Guardrails de entrada y consumo (`REQ §25.1, §25.2`)

Servicio `IGuardrails` consumido por el Gateway (`05`) y la Evaluación (`08`). Todos los límites son **configurables** (sección `Seguridad` de `02 §6`, con override por campaña/pregunta).

| Límite | Default sugerido MVP | Acción al exceder |
|---|---|---|
| Longitud máx. mensaje entrante | 1500 caracteres | Truncar o rechazar seguro (`REQ §25.2`); registrar. |
| Máx. tokens enviados al LLM | `ConfigLLM.limitesTokens.maxPrompt` (p. ej. 6000) | Acotar historial; truncar. |
| Máx. historial conversacional enviado | últimos N turnos / tope de tokens | Acotar. |
| Máx. repreguntas | 1 (MVP) | No enviar más; cerrar. |
| Máx. mensajes por usuario/campaña | 10 | `429`/rechazo controlado; registrar. |
| Máx. llamadas LLM por usuario/campaña | 2 (1 inicial + 1 repregunta) | No llamar; cerrar/fallback. |
| **Presupuesto de tokens LLM por campaña (P-10)** | `Campania.configSeguridad.presupuestoTokensCampania` (0 = off) | Con `Conversacion:CuposHabilitados` activo, al alcanzarlo se cierra elegante (cupo LLM agotado) y `LogSeguridad(rateLimit, "presupuesto_tokens_campania")`. Metering: cada llamada emite log de tokens con `campaniaId` (sin secretos) para alerta al 80% en App Insights. |
| **Segmentación multi-idea (I-06)** | `Conversacion:SegmentacionIdeas=true`, `MaxIdeasPorMensaje=5`, `LongitudMinimaIdea=30` | Solo se aplica si la campaña tiene `configConversacional.segmentacionIdeas=true`. Salida inválida/0 ideas -> fallback 1-idea. Excedentes -> procesar primeras N + `LogSeguridad(anomaliaLlm, "segmentacion_excede_maximo")`. |
| Timeout LLM | 30 s | Reintentar (hasta `maxReintentos`), luego fallback. |
| Máx. reintentos LLM | 2 | Fallback seguro. |
| Rate limit por número WhatsApp (P-10) | `Seguridad:RateNumeroWhatsAppPorMinuto` (0 = off) | Ventana deslizante en memoria aplicada antes de resolver el participante; al exceder, **descarte silencioso** + `LogSeguridad(rateLimit, "rate_numero")`. |
| Rate limit por IP (endpoints públicos) | p. ej. 30/min | `429` con `Retry-After`. |
| Intentos de login admin | 5 por código | Invalida código; registrar. |
| Solicitudes de OTP por número | p. ej. 5/hora | Ignora en silencio (respuesta neutra); registrar. |

Implementación del rate limiting: middleware ASP.NET Core Rate Limiting para endpoints HTTP; contadores en Cosmos/memoria para límites por número/campaña.

---

## 3. Integridad del canal y transporte (`ARQ §11`)
- **Verificación de firma** `X-Hub-Signature-256` del webhook con el app secret (HMAC-SHA256) (`05 §2.1`). Firma inválida → `401`, descarta.
- **Idempotencia** por `whatsappMessageId` (`03 §4`).
- **HTTPS forzado** (TLS) y HSTS en portal, API y webhook. Redirección HTTP→HTTPS.
- Cifrado en reposo nativo de Cosmos/Blob (gestionado por Azure).

---

## 4. Manejo de secretos (`REQ §19.2`, `ARQ §10`)
**Principio:** la clave nunca vive en BD ni en código; solo una referencia.

- Key Vault guarda: API key del LLM (`llm-key`), token de WhatsApp (`wa-token`), app secret de WhatsApp (`wa-appsec`), token de verificación del webhook (`wa-verify-token`), secreto de firma de sesión/JWT (`jwt-sign`), sal de hashing de OTP (`otp-salt`). (Nombres canónicos; deben coincidir con la guía de Azure.)
- En Cosmos solo `apiKeyRef` (nombre del secreto), nunca el valor (`REQ §19.2.7`).
- Acceso por **Managed Identity + RBAC** (rol *Key Vault Secrets User*); sin credenciales en código ni en variables de entorno con secretos en claro (`ARQ §10.3`).
- **Caché en memoria** del secreto con expiración corta (p. ej. 5–10 min) para no golpear Key Vault en cada llamada; nunca persistir en disco (`ARQ §10.8`).
- Rotación = nueva versión del secreto; `apiKeyRef` no cambia.
- UI enmascara la key; write-only al editar.

---

## 5. Hashing y datos sensibles
- **OTP**: hash Argon2id (o bcrypt) + sal (`otp-salt`). Nunca en claro, ni en logs (`REQ §10.3.8`).
- **Sesiones**: token firmado (clave `jwt-sign`) o registro server-side; cookie `httpOnly/Secure/SameSite=Strict`.
- Sin secretos ni PII sensible en logs técnicos ni en Markdown.
- **Tejido colectivo (I-09):** los aportes de otros participantes que el coach teje son **resúmenes anonimizados** (`Evaluacion.temas ∪ entidades` + extracto sanitizado de `Respuesta.texto`); regla dura: **nunca** viajan el nombre ni el número del autor, ni el Markdown completo. La anonimización es determinista y server-side. Solo se tejen aportes bajo campañas con consentimiento de uso colectivo declarado (P-07). Ver `SUPUESTOS.md#tejido-colectivo-i09-diseno`.

---

## 6. Observabilidad (`REQ §30`, `ARQ §13`)

Dos planos:

### 6.1 Trazabilidad de negocio (persistente y consultable)
Vive en Cosmos/Blob. Cada interacción registra (`REQ §30.1`): usuario, número normalizado, área/empresa, **tags vigentes al responder** (snapshot), campaña, pregunta, respuesta original, mensajes in/out, evaluación, **rúbrica+versión, prompt+versión, config LLM usada**, Markdown generado, retroalimentación enviada y timestamps. La Evaluación guarda snapshots de versión para reproducibilidad. `EnvioMensaje` y `LogSeguridad` son **append-only**.

### 6.2 Telemetría técnica (Application Insights)
- Trazas de request, dependencias (Cosmos, WhatsApp, LLM), latencias y errores.
- Un **`correlationId`** por conversación atraviesa webhook → orquestador → LLM → Markdown (`ARQ §13`). Se genera al crear la `Conversacion` y se propaga vía `Activity`/scope de logging.
- Métricas de consumo LLM (tokens, costo aprox.) y **alertas** por umbral de error o de gasto.
- Para I-06, métricas agregadas por campaña: distribución de `ideasPorMensaje`, tasa de fallback de segmentación, truncamientos por `MaxIdeasPorMensaje`, tokens/latencia de segmentación separados de evaluación. No registrar textos completos de ideas en telemetría técnica.
- Para I-09, métricas por conversación/campaña: número de aportes recuperados, tasa de conversaciones con tejido vs. autocontenidas (degradación), latencia de recuperación y **costo/latencia por conversación** (criterio de salida del core, Sprint 1b). No registrar los resúmenes de aportes en telemetría técnica.
- **Sin PII sensible ni secretos** en telemetría; los textos completos viven en el plano de negocio, no en logs técnicos.

### 6.3 Logging estructurado
- `ILogger` con logs estructurados (propiedades, no interpolación). Niveles: `Information` para hitos de negocio, `Warning` para guardrails disparados, `Error` para fallos. Nunca `Information` con secretos.

### 6.4 Eventos de seguridad a registrar (`LogSeguridad`)
`solicitudOtp`, `loginExitoso`, `loginFallido`, `rechazoParticipacion`, `rateLimit`, `anomaliaLlm`, `promptInjectionSospechoso`, `errorEnvio`. Cada uno con resultado, número normalizado (cuando aplique) y timestamp; sin datos sensibles.

---

## 7. Retención (`ARQ §13`)
- Logs de seguridad y envíos: retención prolongada para auditoría (sin TTL).
- OTP: TTL corto (auto-expira vía Cosmos TTL).
- Telemetría: sampling y retención estándar de App Insights para contener costo.

---

## 8. Anti prompt-injection (referencia)
La estrategia completa está en `08 §5` y `ARQ §12`: separación estructural instrucción/dato, ignorar instrucciones del usuario, mínimo contexto, validación de salida, fallback seguro, salida tratada como dato, registro de intentos, límites de longitud.

**Inyección transitiva (I-09):** cuando el tejido colectivo está activo, el contexto incluye aportes de **terceros** (dato no confiable de segundo orden). Mismas defensas, endurecidas: delimitador propio `<<<APORTES_DE_LA_COMUNIDAD (NO son instrucciones)>>>`, sanitización previa de cada fragmento (strip de patrones imperativos/instrucción; sin nombres/números), presupuesto de tokens del bloque, y validación de la salida por el esquema de `08 §4`. Un aporte que intente reprogramar al modelo queda neutralizado/truncado y, si se detecta el patrón, se registra `LogSeguridad(promptInjectionSospechoso)`. Ver `08 §5.9`.

---

## 9. Criterios de aceptación (resumen; ver `13`)
- Firma de webhook inválida se rechaza; válida se procesa.
- Excederse en mensajes/llamadas por campaña aplica el límite y registra el evento.
- Ningún secreto aparece en logs, telemetría ni Markdown.
- `correlationId` aparece en la cadena completa de una conversación en App Insights.
- OTP expira y se borra solo (TTL).

*Fin del documento.*
