# DT-QA-01 — Endpoint de diagnóstico para inyectar un mensaje entrante simulado (sin exponer el App Secret)

**Tipo:** Herramienta de diagnóstico/QA (no es comportamiento de producto).
**Estado:** DONE local 2026-08-05 — pendiente únicamente de despliegue controlado para usarlo contra
Azure. Es el habilitador de las pruebas E2E conversacionales sin exponer el App Secret de Meta.
**Fecha de decisión:** 2026-08-05.
**Áreas afectadas:** endpoints de diagnóstico (`/diagnostico/simulacion`), cola de webhook, página de
simulación (opcional), observabilidad y pruebas.
**Contratos relacionados:** `04 §6.2`, `10 §3`, `13`, `Guias_Implementacion/Guia_Prueba_E2E_Simulada_WhatsApp.md §7`.
**Reutiliza:** `IColaWebhook.EncolarAsync`, `WhatsAppWebhookPayload`, `FiltroClaveDiagnostico`,
`INormalizadorNumero`. **No cambia** la verificación de firma del webhook real.

---

## 1. Problema

El webhook real `POST /webhook/whatsapp` **exige firma HMAC** válida (`X-Hub-Signature-256`) calculada
con el App Secret de Meta que vive en Key Vault (`EndpointsWebhook.RecibirAsync` → `VerificarFirma`).
No hay bypass: `Simulacion:Habilitada` solo mapea los endpoints de *crear admin* y *emitir OTP*.

Por eso, hoy, para **simular un mensaje entrante** hay que firmarlo del lado cliente, y eso obliga a
**exponer el App Secret real** (en el entorno desplegado, además, es el secreto que protege el webhook
de Meta en producción). Exponer ese secreto a un agente/sesión no es aceptable, y rotarlo cerca de la
convención es riesgoso.

## 2. Solución

Añadir un endpoint de **diagnóstico** que inyecta el mensaje **ya autenticado por la clave de
diagnóstico** (`X-Diag-Key`) y lo **encola por el mismo camino** que un webhook con firma válida, **sin
requerir la firma**. Así el sistema "usa lo que ya está configurado", el probador solo necesita la
**clave de diagnóstico** y el App Secret de Meta **nunca sale de Key Vault**.

```
POST /diagnostico/simulacion/webhook-entrante        (mismo grupo que admin-inicial / otp-admin)
Header: X-Diag-Key: <clave de diagnóstico>            (en Development el filtro no la exige)
Body:  { "numero": "573001112201", "texto": "…",
         "whatsappMessageId": "opcional",             (idempotencia; si falta se genera determinista)
         "phoneNumberIdDestino": "opcional" }
Respuesta: 200 (ack inmediato). El procesamiento es asíncrono, idéntico al del webhook real.
```

Internamente: normaliza el número, construye un `WhatsAppWebhookPayload` mínimo equivalente al de Meta
(o acepta el payload completo si se envía), y llama `IColaWebhook.EncolarAsync(...)` — exactamente el
paso que hace `RecibirAsync` **después** de validar la firma. El resto del flujo (dedupe, trabajador de
cola, `ProcesadorWebhookEntrante`, orquestador, evaluación, Markdown) **no cambia**.

---

## 3. Alcance

### 3.1 Incluido
- Endpoint `POST /diagnostico/simulacion/webhook-entrante` en el grupo `/diagnostico/simulacion`,
  **protegido por `FiltroClaveDiagnostico`** y **mapeado solo** en `Development` o con
  `Simulacion:Habilitada=true` (mismo gating que `admin-inicial`/`otp-admin`).
- DTO mínimo `{ numero, texto, whatsappMessageId?, phoneNumberIdDestino? }`; construcción del
  `WhatsAppWebhookPayload` y **encolado** vía `IColaWebhook`.
- **Idempotencia**: usa `whatsappMessageId`; si no viene, se deriva determinista de número+texto+fecha,
  respetando el dedupe existente.
- **Observabilidad**: registrar el uso con `origen=simulacionDiagnostico` (auditable), sin volcar el
  texto del participante a logs técnicos (`10 §6`).
- (Opcional) Ajustar la página `/simulacion-whatsapp` para usar este endpoint en vez de firmar con el
  App secret; el campo "App secret" deja de ser necesario para el camino entrante.
- Pruebas unit/integración: un mensaje inyectado recorre **el mismo** flujo que uno firmado y produce
  el mismo resultado.

### 3.2 Fuera de alcance
- Tocar la verificación de firma del **webhook real**: `/webhook/whatsapp` **sigue exigiendo firma**.
- Cualquier cambio de comportamiento de producto (orquestador, evaluación, contratos).
- Mapear el endpoint sin `X-Diag-Key`, o mapearlo en producción con `Simulacion:Habilitada=false`.
- Envío saliente / plantillas HSM (no aplica al camino entrante simulado).

---

## 4. Seguridad
- **Mismo gating** que los endpoints de diagnóstico ya existentes (crear admin / emitir OTP): clave de
  diagnóstico + solo Development/`Simulacion:Habilitada`. No abre ninguna superficie nueva sin
  autenticación.
- No inyecta nada que el webhook real no procese; el mensaje pasa por **toda** la validación de negocio
  posterior (participante activo, campaña activa, etc.).
- El App Secret de Meta **no participa** en este camino y **no se expone**.
- Recordatorio operativo: apagar `Simulacion:Habilitada` al terminar la corrida (igual que hoy).

---

## 5. Criterios de aceptación
1. Con `X-Diag-Key` válida y `Simulacion:Habilitada=true` (o en Development), `POST /diagnostico/simulacion/webhook-entrante`
   con `{numero, texto}` responde 200 y el mensaje se **procesa** como un webhook entrante normal.
2. El resultado (conversación, evaluación, Markdown, estados) es **idéntico** al de enviar el mismo
   mensaje firmado por el webhook real.
3. La **idempotencia** funciona: reenviar el mismo `whatsappMessageId` no crea dos hilos.
4. Sin `X-Diag-Key` (fuera de Development) el endpoint responde **404/401**, igual que los otros de
   diagnóstico.
5. El webhook real **sigue rechazando** mensajes sin firma válida (no se relajó nada).
6. El uso queda **auditado** (`origen=simulacionDiagnostico`) sin exponer el texto en logs técnicos.

---

## 6. Plan de implementación por cortes

| Corte | Entrega verificable | Pruebas mínimas |
|---|---|---|
| 1 (único) | **DONE local 2026-08-05:** endpoint + DTO + encolado por `IColaWebhook`, gating por `FiltroClaveDiagnostico` y mapeo condicional; auditoría sin PII e id determinista para el dedupe. La página de simulación no cambia: el nuevo camino está disponible para los E2E contra Azure. | Integración verde: 7 pruebas focalizadas de acceso/payload/auditoría/id estable/firma real y recorrido de la cola hacia la pregunta inicial. |

Cambio **aditivo**; requiere **desplegar** para poder usarlo contra el entorno de Azure. No modifica
configuración productiva ni el contrato de `/webhook/whatsapp`.

---

## 7. Rollback
1. No mapear el endpoint (quitar la línea de registro) o apagar `Simulacion:Habilitada`.
2. Nada persistido cambia; el webhook real y el resto del sistema quedan intactos.
