# 05 — Backend: WhatsApp Gateway y Orquestador Conversacional

**Módulos:** `Application/WhatsApp/` y `Application/Conversacion/`.
**Implementa:** `REQ §9, §15, §21, §26`; `ARQ §4, §6` (decisión conversacional).
**Depende de:** `03` (modelo), `04 §6` (webhook), `06` (identidad), `08` (evaluación), `09` (markdown), `10` (guardrails).

---

## 1. Responsabilidades

**WhatsApp Gateway** encapsula toda interacción con WhatsApp Cloud API (Meta): recepción (webhook), envío (plantilla vs texto libre según ventana de 24h), normalización, idempotencia, registro de `EnvioMensaje`.

**Orquestador de Conversación** gobierna la máquina de estados de un hilo: captura → evaluación → decisión (repregunta única o cierre) → compilación Markdown → cierre. No habla con Meta ni con el LLM directamente; usa los puertos de los otros módulos.

---

## 2. WhatsApp Gateway

### 2.1 Puertos (interfaces de dominio)

```csharp
public interface IWhatsAppGateway
{
    Task<EnvioResultado> EnviarPlantillaAsync(string numeroE164, PlantillaWhatsApp plantilla, IReadOnlyDictionary<string,string> variables, TipoEnvio tipo, CancellationToken ct);
    Task<EnvioResultado> EnviarTextoAsync(string numeroE164, string texto, TipoEnvio tipo, CancellationToken ct);
    MensajeEntrante? ParsearWebhook(WhatsAppWebhookPayload payload); // null si no es un mensaje procesable
    bool VerificarFirma(ReadOnlySpan<byte> cuerpoCrudo, string firmaHeader, string appSecret);
}
```

`EnvioResultado`: `{ bool exito; string? whatsappMessageId; string? error; }`.
`MensajeEntrante`: `{ string numeroE164; string texto; string whatsappMessageId; DateTime timestamp; }`.

### 2.2 Envío: plantilla vs texto libre (regla de negocio crítica)

`ARQ §4.1`: WhatsApp exige **plantillas HSM aprobadas** para iniciar conversación o enviar fuera de la ventana de servicio de 24h; dentro de la ventana se permite **texto libre**.

El Gateway decide así:
- **Mensaje inicial de campaña** y reenvíos/reintentos proactivos → `EnviarPlantillaAsync` usando la plantilla aprobada configurada en App Settings (`WhatsApp:PlantillaEnvioInicial`). Si falta `Nombre`, el backend rechaza el envío en vez de caer a texto libre.
- **Cualquier otro envío cuando no hay ventana abierta** → `EnviarPlantillaAsync` (plantilla aprobada + variables).
- **Retroalimentación y repregunta dentro de la ventana** (`now < conversacion.ventanaServicioVenceEn`) → `EnviarTextoAsync`.
- **Repregunta fuera de ventana** → plantilla de repregunta aprobada (`ARQ §16`).

La ventana se calcula desde el último mensaje **entrante** del usuario: `ventanaServicioVenceEn = ultimoEntrante.timestamp + 24h`. Se persiste en `Conversacion` (`03 §3.6`).

### 2.3 Llamada a Graph API
- POST a `{GraphApiBaseUrl}/{PhoneNumberId}/messages` con `Authorization: Bearer <access-token>` (token leído de Key Vault por `apiKeyRef`/secreto configurado, vía Managed Identity).
- Throttling configurable para envío masivo (respeta límites de Meta) (`ARQ §4.4`).
- Reintentos con backoff exponencial ante errores transitorios (5xx / rate de Meta); marca `EnvioMensaje.estado = error` con el detalle si agota reintentos.
- Cada envío persiste un `EnvioMensaje` (`03 §3.5`) con `tipo`, estado y timestamp.

### 2.4 Recepción (webhook)
Invocado desde el endpoint `POST /webhook/whatsapp` (`04 §6.2`). Flujo (`ARQ §4.2`):

```
1. El endpoint verifica firma (VerificarFirma). Si falla → 401, descarta.
2. Responde 200 OK inmediato y encola { payload } a la cola in-process.
3. El worker:
   - Nota: el parser considera entrantes procesables los textos, clicks de boton de plantilla (`type=button`) y respuestas `interactive.button_reply`.
   a. ParsearWebhook → MensajeEntrante (ignora payloads de estado/no-mensaje).
   b. Idempotencia: intenta crear WebhookDedupe{ id = whatsappMessageId } en `leases`.
      - Si ya existía → descarta (mensaje repetido por reintento de Meta).
   c. Normaliza número (06 §2) y resuelve participante (06 §3).
   d. Si NO autorizado → respuesta de rechazo neutral y fin (06 §3.3).
   e. Guardrails de entrada (10 §2): longitud, rate limit, cupos por campaña.
   f. Persiste Mensaje (direccion=in) y actualiza ventana de servicio.
   g. Entrega el control al Orquestador (Conversacion).
```

### 2.5 Envío masivo de mensajes iniciales
Disparado por `POST /api/admin/.../envios` (`04 §5.4`). El backend encola un job por participante; el worker los procesa con throttling y registra estado individual (`EnvioMensaje`). El reenvío reusa el mecanismo filtrando por `estadoRespuesta = sinRespuesta` (`ARQ §4.4`). El estado por participante alimenta `GET .../envios`.

### 2.6 Configuración consumida (sección `WhatsApp` de `02 §6`)
`GraphApiBaseUrl`, `PhoneNumberId`, `VerifyTokenSecretName`, `AppSecretSecretName`, `AccessTokenSecretName`, y el catálogo de plantillas aprobadas (nombre, idioma, mapeo de variables). Los nombres de secretos coinciden con la guía de Azure.

Para el envío inicial de campañas, configurar en el App Service:
- `WhatsApp__PlantillaEnvioInicial__Nombre` = `el_tejido_inicio_campania` (nombre sugerido para la plantilla aprobada en Meta).
- `WhatsApp__PlantillaEnvioInicial__Idioma` = `es_CO` (debe coincidir exactamente con el idioma aprobado).
- `WhatsApp__PlantillaEnvioInicial__Componentes__0` = `nombre` y `WhatsApp__PlantillaEnvioInicial__Componentes__1` = `campania` si la plantilla usa dos variables de cuerpo.

---

## 3. Endpoint del webhook (recordatorio de contrato)
Ver `04 §6`. Puntos no negociables: verificación de firma, ack 200 inmediato, procesamiento asíncrono, idempotencia por `whatsappMessageId`.

---

## 4. Orquestador conversacional

### 4.1 Puerto

```csharp
public interface IOrquestadorConversacion
{
    Task ProcesarMensajeEntranteAsync(ParticipanteResuelto participante, MensajeEntrante mensaje, CancellationToken ct);
}
```

### 4.2 Máquina de estados

Estados de `Conversacion.estadoMaquina` (`03 §3.6`):

```
                 mensaje inicial enviado
   (no existe) ───────────────────────────▶  esperandoRespuestaInicial
                                                     │ usuario responde
                                                     ▼
                                                 evaluando  (llama 08)
                                          ┌──────────┴───────────┐
                       recomendacion=cerrar │                    │ recomendacion=repreguntar
                       o repreguntas agotadas│                    │ y repreguntasUsadas < maxRepreguntas
                                             ▼                    ▼
                                        (compila 09)        esperandoRepregunta
                                             │                    │ usuario responde
                                             ▼                    ▼
                                          cerrada  ◀────────── evaluando (2ª y última)
                                                              (compila 09) → cerrada
```

### 4.3 Algoritmo (`ARQ §6 paso 5`)

```
ProcesarMensajeEntranteAsync:
1. Cargar/crear Conversacion (usuario, campaña, pregunta vigente).
2. Persistir Respuesta (esRepregunta según estadoMaquina) con tagsSnapshot.
3. estadoMaquina = evaluando.
4. Llamar IEvaluadorLlm.EvaluarAsync(...) (08).  // guardrails de pre/post dentro de 08
5. Persistir Evaluacion (03 §3.9).
6. Decisión:
   - Si Evaluacion.recomendacion == repreguntar
        AND conversacion.repreguntasUsadas < campaña/pregunta.maxRepreguntas (MVP=1):
        → enviar UNA repregunta (Gateway: texto libre si ventana abierta, si no plantilla repregunta)
        → registrar EnvioMensaje(tipo=Repregunta); repreguntasUsadas++
        → estadoMaquina = esperandoRepregunta; FIN (espera respuesta).
   - En caso contrario (cerrar, o repreguntas agotadas):
        → enviar retroalimentación (si no se envió ya como parte del flujo)
        → enviar mensaje de cierre (Gateway, tipo=Cierre) (REQ §26.8)
        → encolar compilación Markdown (09) para la(s) respuesta(s) del hilo
        → estadoMaquina = cerrada; cerrar Conversacion (fechaCierre).
7. Enviar la retroalimentación breve al usuario por WhatsApp (outbound).
```

### 4.4 Tope duro del MVP
**Revisiones como oportunidades:** `MaxRepreguntas` controla cuantas invitaciones a mejorar se ofrecen. Cuando el hilo esta en `esperandoRepregunta` y `repreguntasUsadas >= maxRepreguntas`, el siguiente entrante se registra como respuesta `recibida`, no se evalua, no genera retro/Markdown y se cierra con solo el mensaje de cierre (`REQ §25.2`, `§26.6`).

### 4.5 Reglas de la retroalimentación (`REQ §21`)
La retroalimentacion que se envia es la `retroalimentacionEnviada` que produjo el LLM (`08`), validada para ser breve. El orquestador **no** reescribe el contenido; solo decide cuando enviarla, si ademas envia cierre, y que textos operativos de sistema agregar desde `Conversacion:Mensajes:*`. Prohibido (lo garantiza el prompt en `08`, pero el orquestador no lo viola): prometer implementar, ofrecer ejecutar acciones, textos largos, mas de una repregunta (`REQ §21.3`).


#### Textos operativos configurables
Los textos no generados por el LLM se leen de la seccion `Conversacion:Mensajes` y pueden cambiarse por variables de entorno: `Conversacion__Mensajes__SaludoPrimerContacto`, `Conversacion__Mensajes__SaludoSiguientePregunta`, `Conversacion__Mensajes__InvitacionMejora` y `Conversacion__Mensajes__MensajeConfiguracionNoDisponible`. Si el valor falta o esta vacio, se usa el default compilado. `ConfigConversacional.MensajeCierre` sigue viniendo de la campania/portal.
### 4.6 Manejo de errores
- Si la evaluación cae en **fallback** (`08 §6`): el orquestador envía una retroalimentación neutra ("Gracias, registramos tu aporte") y cierra sin romper el hilo; la `Respuesta` queda `evaluacionPendiente` (`REQ §20.3.10`).
- Si el envío saliente falla: se reintenta (Gateway) y se registra; la conversación no se pierde (el estado persiste en Cosmos).

### 4.7 Correlación
Toda la cadena webhook → orquestador → LLM → Markdown comparte el `correlationId` de la `Conversacion` (`ARQ §13`). Propagarlo en logs y telemetría (`10 §6`).

---

## 5. Criterios de aceptación del módulo (resumen; ver `13`)
- Un mensaje entrante de un participante autorizado genera Respuesta + Evaluación + retroalimentación enviada.
- Como máximo se envía **una** repregunta; el segundo turno siempre cierra.
- El cierre envía mensaje de agradecimiento y dispara compilación Markdown.
- Mensajes repetidos por reintento de Meta no duplican Respuestas ni Evaluaciones (idempotencia).
- Fuera de ventana de 24h, la repregunta se envía por plantilla aprobada.
- Participante no autorizado recibe rechazo neutral; no se procesa.

*Fin del documento.*
