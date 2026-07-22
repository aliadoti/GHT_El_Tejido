# 07 — Runbook de Rollback / Contingencia (Día-D)

> **Principio:** ante un síntoma en producción se **apaga el flag / kill-switch** correspondiente, **nunca** hotfix en caliente. *"El LLM propone, el sistema dispone."* Cada apagado es reversible y no requiere redeploy (son App Settings / config de campaña).
> **Base:** `Reglas_Conversacion_y_Participacion.md §3`, `10_Seguridad_Guardrails`, `Runbook_I-01_Umbral_Cierre_Anticipado.md`, P-09 monitoreo día-D.

---

## 1. Cómo se apaga cada palanca

| Palanca | Dónde | Efecto de apagar |
|---|---|---|
| `Conversacion:TejidoColectivo=false` | App Setting (env `Conversacion__TejidoColectivo`) | Apaga I-09 para **todas** las campañas → conversación autocontenida. |
| `configConversacional.tejidoColectivo=false` | Portal, por campaña | Apaga tejido solo en esa campaña. |
| `Conversacion:SegmentacionIdeas=false` | App Setting | Apaga multi-idea (I-06) global → 1 mensaje = 1 respuesta. |
| `configConversacional.segmentacionIdeas=false` | Portal, por campaña | Apaga multi-idea en esa campaña. |
| `Conversacion:Parafraseo=false` | App Setting | Apaga parafraseo (I-05) global → retro clásica. |
| `Conversacion:UmbralCierreAnticipado=0` | App Setting | Desactiva cierre por calificación alta (I-01) global. |
| `configConversacional.umbralCierreAnticipado=0`/null | Portal, por campaña (P-13) | Desactiva/hereda el umbral por campaña. |
| `Conversacion:CuposHabilitados` | App Setting | ON aplica cupos de campaña; OFF los desactiva (ver regla D2). |
| `Conversacion:MaxTurnosPorHilo` | App Setting | Techo duro de turnos; subir para aflojar, bajar para blindar. |
| `Seguridad:RateNumeroWhatsAppPorMinuto` | App Setting | Rate por número; subir/bajar la ventana. |
| `Seguridad:PermitirReinicioDatos=false` | App Setting | Bloquea reinicio masivo (P-03) → 409. |
| `Simulacion:Habilitada=false` | App Setting | Cierra la superficie de simulación en producción. |
| Pausar envíos | Portal `Envios` | Detener el envío escalonado de lotes. |

> Cambiar un App Setting en Azure **reinicia la app**. Confirmar `/health`=200 tras cada cambio. Los cambios por **portal** (config de campaña) son inmediatos, sin reinicio.

---

## 2. Matriz síntoma → acción (día-D)

| # | Síntoma observado | Causa probable | Acción inmediata (apagar) | Verificación | Escalar |
|---|---|---|---|---|---|
| C1 | **Aparece un criterio/puntaje/"rúbrica" en un mensaje al participante** | Falla de `FiltroSalidaRubrica` (I-03) | El filtro es siempre-on y no tiene flag: **pausar envíos** de la campaña afectada y **apagar tejido** (reduce superficie). Revisar `LogSeguridad(fuga_rubrica)`. | Ningún mensaje nuevo con fuga | Ing. inmediato (Crítico) |
| C2 | **PII de un participante aparece en la conversación de otro** | Falla de anonimización del tejido (I-09) | `Conversacion:TejidoColectivo=false` (global) de inmediato | Nuevas conversaciones autocontenidas; sin PII | Ing. + privacidad (Crítico) |
| C3 | **El modelo obedece una instrucción inyectada** (revela sistema, cambia nota) | Injection directa/transitiva | Apagar **tejido** (corta injection transitiva); si persiste, pausar envíos | Salidas vuelven a esquema; sin obediencia | Ing. inmediato |
| C4 | **Gasto LLM se dispara / loop de evaluaciones** | Cupos no activos o mal dimensionados | `Conversacion:CuposHabilitados=true` + revisar `maxLlamadasLlmPorUsuario`; bajar `MaxTurnosPorHilo`; revisar `presupuestoTokensCampania` | Alerta de costo se estabiliza; `LogSeguridad(rateLimit)` corta | Ops + Ing. |
| C5 | **Un número inunda el webhook** | Rate por número apagado/alto | Subir `Seguridad:RateNumeroWhatsAppPorMinuto` (activar) | Excedentes se descartan (`rate_numero`) | Ops |
| C6 | **Multi-idea genera respuestas raras / costo N×** | I-06 mal calibrado | `Conversacion:SegmentacionIdeas=false` | Vuelve a 1 mensaje = 1 respuesta | Ing. |
| C7 | **Parafraseo infiel / agrega datos** | I-05 | `Conversacion:Parafraseo=false` | Retro clásica | Ing. |
| C8 | **Cierra demasiado pronto por calificación alta** | Umbral I-01 muy agresivo | Bajar/`0` el umbral (global o override P-13 de la campaña) | Vuelven las revisiones normales | Árbitro calibración |
| C9 | **No cierra nunca / participantes atrapados** | Umbral off + revisiones + sin techo | Verificar `MaxRepreguntas`; activar `MaxTurnosPorHilo`; opcional subir umbral | Hilos terminan | Ing. |
| C10 | **Error técnico visible al participante** | Fallback no cubrió un caso | Revisar `LogSeguridad`/App Insights por `correlationId`; si es una función bajo flag, apagarla | Nuevos casos caen a fallback neutro | Ing. |
| C11 | **Webhook procesa duplicados** | Dedupe/idempotencia | No hay flag: **pausar envíos**, revisar `leases`/`WebhookDedupe` | Sin duplicados nuevos | Ing. inmediato |
| C12 | **Firma inválida se estaría procesando** | Config de `wa-appsec` | Verificar secreto en Key Vault; **pausar webhook/envíos** | 401 a firmas inválidas | Ing. + seguridad |
| C13 | **Envío inicial falla masivamente** | `wa-token`/PhoneNumberId/HSM | Pausar envíos; verificar plantilla HSM aprobada y token | Reintento controlado desde `Envios` | Ops + Meta |
| C14 | **Reinicio de datos disparado por error en prod** | `PermitirReinicioDatos` quedó `true` | `Seguridad:PermitirReinicioDatos=false` (masivo → 409) | Masivo bloqueado | Ops |

---

## 3. Secuencia general de contingencia

1. **Detectar** (reunión 20-jul: el **panel en vivo de P-09 quedó diferido** → usar `/health(/ready)`, los **logs de entrega** estructurados del `TrabajadorWebhook`, alertas básicas de la plataforma y revisión de muestras de conversación).
2. **Contener** con la acción de la matriz §2 (apagar el flag más específico primero: campaña antes que global; si urge, global).
3. **Verificar** que las **nuevas** conversaciones ya no presentan el síntoma (las en curso pueden terminar en modo degradado seguro).
4. **Registrar** el evento, hora, flag tocado y `correlationId` afectados.
5. **No** hacer hotfix en caliente. Si requiere código, esperar a una ventana controlada post-lote.
6. **Reanudar** envíos solo tras confirmar contención.

---

## 4. Orden de apagado por severidad (si hay que decidir rápido)

1. **Fuga de seguridad/PII (C1, C2, C3):** apagar tejido + pausar envíos de la campaña. Máxima prioridad.
2. **Costo/loop (C4, C5):** activar cupos + rate + techo de turnos.
3. **Calidad conversacional (C6, C7, C8):** apagar la función bajo flag correspondiente.
4. **Terminación (C9):** activar techo de turnos.

> **Regla D2:** si en el día-D se decidió activar el umbral I-01 (cierre por calificación alta), los cupos P-10 **deben** estar activos en producción. Si hay que apagar cupos por emergencia, revisar que el umbral no deje hilos sin tope determinístico.

---

## 5. Estado seguro por defecto (fallback de todo el sistema)

Si la duda es grande, dejar el sistema en su **configuración probada**:
- Tejido **off**, multi-idea **off**, parafraseo **off**, umbral **0/off**.
- Cupos y techo de turnos **on** (protección de costo/terminación).
- Rate por número **on**.
- Reinicio masivo **off** (`PermitirReinicioDatos=false`), simulación **off**.

Esta es exactamente la línea base validada en QA; ninguna función nueva queda activa por defecto (regla D1).

---

## 6. Contactos y referencias
- Monitoreo día-D: `Especificaciones/Iniciativas/P-09_Monitoreo_Dia_D.md`.
- Umbral I-01: `Especificaciones/planes/Runbook_I-01_Umbral_Cierre_Anticipado.md`.
- Guardrails y eventos de seguridad: `Especificaciones/base/10_Seguridad_Guardrails_y_Observabilidad.md §6.4`.
- Reglas de flujo y parámetros: `Especificaciones/Reglas_Conversacion_y_Participacion.md §3`.

*Fin del documento.*
