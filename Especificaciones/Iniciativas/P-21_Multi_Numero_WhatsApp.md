# P-21 — Multi-número de WhatsApp (envío y respuesta por número, misma WABA/App)

> **Origen:** solicitud operativa del usuario (2026-07-25): se agrega un **segundo número de
> WhatsApp** (ya en estado *connected* en WhatsApp Manager) para que el sistema pueda usar **dos
> números indistintamente** (uno para pruebas/calidad y otro para producción). Decisión de alcance
> confirmada con el usuario: **no se separa por arquitectura** (una sola App/WABA, un solo backend);
> el sistema debe poder **enviar y responder por cualquiera de los dos números**.
> **Tipo:** Desarrollo (aditivo, tamaño medio; toca el WhatsApp Gateway de punta a punta) ·
> **Prioridad:** Media-Alta (habilita QA con número dedicado sin contaminar producción) ·
> **Ventana:** a coordinar con el usuario (no está en la ruta crítica del Hito del 10-ago, pero
> desbloquea pruebas E2E reales sin usar el número de producción).
> **Dependencia:** ninguna dura. Comparte el contenedor de config por campaña con P-13/I-06/I-17 (sin
> conflicto). Reutiliza el mismo `wa-token`, `wa-appsec` y `wa-verify-token` (misma WABA/App: **no se
> crean secretos nuevos**). · **Riesgo:** Medio (cambia la firma del puerto `IWhatsAppGateway` y toca
> sus 3 consumidores: orquestador, envíos de campaña y OTP; mitigado con parámetro **opcional con
> default = número predeterminado**, que preserva el comportamiento actual). Cubre REQ §9/§15/§26,
> ARQ §4.1/§4.2/§4.4; specs base `05 §2`, `03 §3.3`, `appsettings` sección `WhatsApp`.
> **Estado:** **DONE local 2026-07-25.** Implementación aditiva completa: configuración `Numeros[]` y
> `AliasPredeterminado` con fallback legacy, captura de `metadata.phone_number_id`, gateway con emisor
> opcional, respuestas por el número entrante y alias saliente por campaña. Sin secretos nuevos; sin
> configuración nueva se conserva el comportamiento de un solo número. Verificación: build Release,
> 473 pruebas backend (420 unitarias + 53 de integración) y formato limpio.

## 1. Qué pide / por qué
Hoy el backend está atado a **un solo número** de WhatsApp: la ruta de envío se arma con un único
`WhatsApp:PhoneNumberId` global (`05 §2`). El usuario necesita operar **dos números a la vez** bajo la
**misma WhatsApp Business Account (WABA) y la misma App de Meta** —típicamente uno para
**pruebas/calidad** y otro para **producción**— sin montar una segunda App ni un segundo despliegue.
El objetivo es que el sistema pueda **enviar el mensaje inicial desde el número que corresponda** y,
sobre todo, **responder cada conversación por el mismo número al que el participante escribió**
(invariante de la ventana de servicio de 24 h: contestar por otro número rompería el hilo y confundiría
al usuario).

## 2. Estado actual del build (qué ya funciona y qué falta)
Por ser **misma WABA / misma App**, buena parte ya está resuelta a nivel de transporte:
- **Recepción:** el webhook `POST /webhook/whatsapp` (`EndpointsWebhook`) **ya recibe** los mensajes
  entrantes de **ambos** números, porque los dos están suscritos a la misma App (una sola Callback
  URL). La **verificación de firma HMAC** (`X-Hub-Signature-256`) **ya funciona** para ambos, porque
  el `wa-appsec` es de la App (uno solo).
- **Secretos:** el `wa-token` (system user con la WABA asignada), el `wa-appsec` y el
  `wa-verify-token` **sirven para los dos números** sin cambios. **No hay secretos nuevos.**
- **Plantillas HSM:** aprobadas a nivel WABA → **compartidas** por ambos números; no se recrean.

Lo que **falta** es que el backend distinga y use el número correcto:
1. **Envío atado a un solo id.** `WhatsAppGateway.EnviarAsync` construye la ruta
   `{GraphApiBaseUrl}/{PhoneNumberId}/messages` con el `PhoneNumberId` **único** de `OpcionesWhatsApp`.
   No hay forma de enviar desde el segundo número.
2. **El entrante no captura el número destino.** `WhatsAppWebhookPayload` **no modela** el bloque
   `value.metadata` (`phone_number_id` / `display_phone_number`) y `ParsearWebhook` no lo extrae; por
   eso `MensajeEntrante` (número, texto, id, timestamp) **no sabe** a cuál de los dos números llegó el
   mensaje, y el orquestador no puede responder por ese mismo número.
3. **La campaña no elige número de salida.** `ServicioEnvios`/`TrabajoEnvio`/`ProcesadorEnvio` no
   tienen el concepto de "desde qué número enviar" para los mensajes iniciales.

## 3. Diseño técnico (patrón "default global + número por campaña + responder por el número entrante")
Se reutiliza el mismo patrón aditivo de P-13 (default global + override por campaña) adaptado al
número saliente, más la captura del número entrante para las respuestas conversacionales.

### 3.1 Configuración multi-número (default global)
`OpcionesWhatsApp` pasa de un `PhoneNumberId` único a una **colección de números** con **alias**
lógico y su `PhoneNumberId` de Meta, más uno marcado como **predeterminado**:
- Nuevo `Numeros`: lista de `{ Alias, PhoneNumberId }` (p. ej. `produccion` → id A, `qas` → id B).
- Nuevo `AliasPredeterminado` (string): el alias por defecto cuando no hay override (equivale al
  comportamiento actual de un solo número).
- **Compatibilidad:** se conserva `PhoneNumberId` como forma legacy; si `Numeros` viene vacío, se
  deriva un único número `{ Alias = "predeterminado", PhoneNumberId }` (documento/appsettings viejo =
  comportamiento actual, sin cambios).
- **`appsettings.json`** (sección `WhatsApp`) documenta el formato; los ids reales viven en App
  Settings por entorno (no secretos, no versionados con valor sensible). Se resuelve `alias → id` en
  Infraestructura; **el dominio nunca guarda ids de Meta.**

### 3.2 Captura del número entrante (para responder por el mismo)
- **`WhatsAppWebhookPayload`:** agregar `WhatsAppMetadata { phone_number_id, display_phone_number }`
  en `WhatsAppChangeValue` (aditivo; Meta ya lo envía en `value.metadata`).
- **`WhatsAppGateway.ParsearWebhook`:** extraer `phone_number_id` del `metadata` del mismo `change`
  que contiene el mensaje procesable.
- **`MensajeEntrante`:** campo aditivo `PhoneNumberIdDestino` (string; el id de Meta al que llegó el
  mensaje). Si por algún motivo faltara (payloads atípicos), se degrada al número predeterminado.

### 3.3 Envío por el número correcto (Gateway)
- **`IWhatsAppGateway`:** los tres métodos de envío (`EnviarTextoAsync`, `EnviarPlantillaAsync`,
  `EnviarPlantillaAutenticacionAsync`) reciben **cuál número usar**. Para minimizar el ripple y
  preservar el comportamiento actual, se agrega un parámetro **opcional** `phoneNumberIdEmisor`
  (`string?`, default `null`); `null` ⇒ usa el `PhoneNumberId` del alias predeterminado.
- **`WhatsAppGateway.EnviarAsync`:** arma la ruta con el `phoneNumberIdEmisor` recibido (o el
  predeterminado si es `null`), en vez del `PhoneNumberId` fijo.

### 3.4 Respuestas conversacionales — SIEMPRE por el número entrante (invariante)
`OrquestadorConversacion` recibe el `MensajeEntrante` (con `PhoneNumberIdDestino`) y lo **propaga** a
todos sus envíos de respuesta (retro, repregunta, cierre, siguiente pregunta). Es un **invariante, no
un flag**: dentro de la ventana de 24 h se responde por el mismo número al que el usuario escribió.
El cold-start (primer contacto que envía la pregunta al recibir el saludo/click) también responde por
ese número entrante.

### 3.5 Envíos iniciales de campaña — número **por campaña** (decisión del usuario)
- **Campo aditivo por campaña** `configConversacional.numeroWhatsAppSaliente` (`string?`, **default
  ausente/null** = hereda el alias predeterminado global). Guarda el **alias** (no el id de Meta).
- **`ServicioEnvios`** resuelve el alias de la campaña, lo lleva en `TrabajoEnvio` (campo aditivo
  `AliasNumeroSaliente` o el id ya resuelto) y **`ProcesadorEnvio`** lo pasa al Gateway al enviar la
  plantilla/texto inicial.
- Ausencia/null ⇒ la campaña envía desde el número predeterminado (comportamiento idéntico al actual).

### 3.6 OTP de login admin
`NotificadorOtpWhatsApp` envía la plantilla de autenticación desde el **número predeterminado** (o un
alias configurable en `Auth:OtpWhatsApp`, opcional). Es un mensaje de plataforma, no atado a campaña.

## 4. Contratos y configuración (todo aditivo, commit aparte)
- **`03 §3.3` (aditivo):** `configConversacional.numeroWhatsAppSaliente` (`string?`, default
  ausente/null = hereda el predeterminado global). Documento viejo sin el campo = comportamiento
  actual. El valor es un **alias lógico**, no un id de Meta.
- **`05 §2` (gateway):** documentar la firma extendida de `IWhatsAppGateway` (parámetro opcional de
  número emisor) y el nuevo modelado de `metadata.phone_number_id` en el webhook; el envío arma la
  ruta con el id resuelto.
- **Config `appsettings`/App Settings:** sección `WhatsApp` con `Numeros[]` (`Alias`/`PhoneNumberId`)
  y `AliasPredeterminado`; se conserva `PhoneNumberId` legacy como número predeterminado si `Numeros`
  está vacío. **Sin secretos nuevos** (mismo `wa-token`/`wa-appsec`/`wa-verify-token`).
- **`04` (portal):** un selector opcional de "número de envío" en la edición de campaña (lista de
  alias configurados; vacío = "usar el predeterminado"). Aditivo; el DTO acepta el campo opcional.
- **Reglas de flujo:** actualizar `Reglas_Conversacion_y_Participacion.md` para reflejar que la
  respuesta sale **por el número entrante** y que el inicial sale por el número de la campaña
  (`campaña ?? predeterminado`).

## 5. Riesgos y mitigación
- *Ripple por cambio de firma del puerto* (`IWhatsAppGateway` lo consumen orquestador, `ProcesadorEnvio`
  y `NotificadorOtpWhatsApp`) → parámetro **opcional con default = número predeterminado**: los
  llamadores no migrados compilan y se comportan igual que hoy; se migran uno a uno con pruebas.
- *Responder por número equivocado* (rompe la ventana de 24 h) → invariante §3.4 cubierto por prueba:
  la respuesta usa el `PhoneNumberIdDestino` del entrante, no el predeterminado.
- *Una sola Callback URL para dos números* (misma App) → **no es un problema para este alcance**: el
  backend es único; recibe ambos y responde por el número correcto gracias a `metadata.phone_number_id`.
  (Si en el futuro se quisiera QA en un backend separado, requeriría **App/webhook aparte** — fuera de
  alcance, anotado en `SUPUESTOS.md`.)
- *Ids de Meta filtrándose al dominio* → el dominio guarda un **alias**; la resolución `alias → id`
  vive en Infraestructura/config. Sin acoplar la campaña a Meta.
- *Config incompleta* (alias de campaña que no existe en `Numeros`) → degradar al predeterminado y
  registrar advertencia; nunca bloquear el envío por un alias mal escrito.
- *Cero cambio por defecto* → sin `Numeros` configurados y sin campo por campaña, el sistema se comporta
  **exactamente como hoy** (un número, el legacy `PhoneNumberId`). Respeta D1.

## 6. Criterios de aceptación / pruebas
- Unit: `ParsearWebhook` extrae `phone_number_id` de `value.metadata` y lo pone en
  `MensajeEntrante.PhoneNumberIdDestino`; ausencia de `metadata` ⇒ degrada al predeterminado.
- Unit: una respuesta conversacional (retro/repregunta/cierre) se envía por el **mismo**
  `phone_number_id` del entrante, aunque el predeterminado sea otro.
- Unit: `EnviarAsync` arma la ruta con el `phoneNumberIdEmisor` recibido; con `null` usa el
  predeterminado (regresión: comportamiento idéntico al actual).
- Unit: campaña **sin** `numeroWhatsAppSaliente` ⇒ el inicial sale por el predeterminado; campaña
  **con** alias válido ⇒ sale por ese número; alias inexistente ⇒ degrada al predeterminado + log.
- Unit: resolución `alias → PhoneNumberId` desde `OpcionesWhatsApp.Numeros`; `Numeros` vacío ⇒ deriva
  el legacy `PhoneNumberId` como predeterminado.
- Contrato: documento `03` viejo sin `numeroWhatsAppSaliente` se deserializa a null y hereda
  (compatibilidad aditiva).
- Integración: firma HMAC sigue validándose igual (mismo `wa-appsec`) para mensajes de ambos números.

## 7. Degradación
Sin `WhatsApp:Numeros` y sin campo por campaña ⇒ sistema **idéntico al actual** (un solo número, el
legacy `PhoneNumberId`). Quitar el campo de una campaña vuelve al predeterminado sin migración. La
respuesta por número entrante es transparente: si el `metadata` faltara, cae al predeterminado. P-21
añade capacidad multi-número sin cerrar fronteras (una App/WABA hoy; App separada para QA queda como
opción futura documentada).

## 8. Plan de implementación (pasos pequeños y verificables)
1. **Config multi-número:** extender `OpcionesWhatsApp` (`Numeros[]` + `AliasPredeterminado`, con
   derivación legacy) + registro DI (`ServiciosWhatsApp`) + `appsettings.json` + `ComprobacionWhatsApp`
   (health de secretos sin cambio). Pruebas de resolución `alias → id`.
2. **Captura del entrante:** `WhatsAppMetadata` en `WhatsAppWebhookPayload`, extracción en
   `ParsearWebhook`, campo `PhoneNumberIdDestino` en `MensajeEntrante`. Pruebas de parseo.
3. **Gateway emisor:** parámetro opcional `phoneNumberIdEmisor` en `IWhatsAppGateway` (3 métodos) y
   uso en `EnviarAsync`. Regresión: default = predeterminado.
4. **Respuesta por número entrante:** propagar `PhoneNumberIdDestino` en `OrquestadorConversacion`.
   Prueba del invariante.
5. **Número por campaña:** campo `configConversacional.numeroWhatsAppSaliente` (dominio + Cosmos,
   aditivo), `TrabajoEnvio`/`ServicioEnvios`/`ProcesadorEnvio` y portal. Pruebas de selección/degradado.
6. **OTP:** enviar por el predeterminado (o alias configurable). Regresión.
7. **Docs y verificación:** `03 §3.3`, `05 §2`, `04`, `Reglas`, `SUPUESTOS.md`; build `-warnaserror`
   + test + `dotnet format` verdes (y frontend si toca el portal). Registrar en `AVANCES.md`/`TODO.md`.

> **Nota de entorno (para quien implemente):** la verificación de build/test debe correrse en el
> entorno local del desarrollador; la carpeta sincronizada por OneDrive no permite compilar de forma
> fiable desde herramientas del agente. Seguir el bucle de `TODO.md §3` (build/test/format en verde por
> paso).
