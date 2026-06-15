# Guía paso a paso — WhatsApp Cloud API en Meta (El Tejido MVP)

**Para:** la persona que configurará el canal de WhatsApp del MVP.
**Qué es esto:** instrucciones manuales para dar de alta la **app de WhatsApp Cloud API** en Meta, obtener el número, los tokens, configurar el webhook y crear las **plantillas de mensaje** que El Tejido necesita.
**Consola:** [https://developers.facebook.com](https://developers.facebook.com).

> **Contexto técnico:** El Tejido **inicia** la conversación (envía el mensaje inicial), por lo que Meta exige **plantillas (HSM) pre-aprobadas** para esos mensajes. Una vez el usuario responde, se abre una **ventana de servicio de 24 horas** durante la cual se puede enviar **texto libre** (la retroalimentación y la repregunta). Esta guía crea las plantillas necesarias y conecta el webhook al backend ya desplegado en Azure.

> **Importante:** desde finales de 2025, **Cloud API es el único camino** para integraciones nuevas (la antigua On-Premises API quedó descontinuada). La interfaz de Meta cambia con frecuencia; si un texto no coincide exactamente, busca la opción equivalente.

---

## §0. Prerrequisitos

1. Una **cuenta de Facebook/Meta** y acceso a **Meta Business Suite** (una cuenta de **Meta Business Portfolio / Business Manager**). Si no tienes una, créala en [business.facebook.com](https://business.facebook.com).
2. El backend de El Tejido **ya desplegado** en Azure con la URL pública del webhook disponible:
   `https://<webapp>.azurewebsites.net/webhook/whatsapp` (de la guía de Azure).
3. Haber definido tú mismo un **token de verificación** del webhook (cadena que inventas) y haberlo cargado en Key Vault como el secreto `wa-verify-token` (guía de Azure §5). Lo volverás a escribir aquí, deben coincidir.
4. Un **número de teléfono** para producción que **no** esté ya registrado en una cuenta de WhatsApp personal/Business app (o uno que puedas migrar). Para pruebas, Meta provee un número de prueba.

---

## §1. Crear la app de tipo Business y añadir WhatsApp

1. Entra a [developers.facebook.com](https://developers.facebook.com) → inicia sesión → **My Apps** → **Create App**.
2. **App type / Use case:** elige el caso de uso de **WhatsApp** (o tipo **Business**). Continúa.
3. **App name:** `El Tejido`. Asocia tu **Business portfolio** (Business Manager). **Create app** (puede pedir tu contraseña).
4. En el panel de la app, en **Add products**, localiza **WhatsApp** → **Set up**.
5. Se abrirá **WhatsApp → API Setup** (o *Quickstart*). Aquí verás un número de prueba, un token temporal y los identificadores. No cierres esta página: la usarás en §2 y §3.

---

## §2. Identificadores y número

En **WhatsApp → API Setup**:

1. **Phone number ID:** copia el valor del campo *Phone number ID* del número (de prueba o el de producción cuando lo agregues). → Es tu `WhatsApp:PhoneNumberId` (Azure §10).
2. **WhatsApp Business Account ID (WABA ID):** cópialo también (lo necesitarás para plantillas).
3. **Número de prueba:** Meta te da uno gratuito para desarrollo. Para **enviar** a destinatarios de prueba, en **API Setup → To → Manage phone number list**, agrega los números de los 5 usuarios de prueba; cada uno recibirá un **código de confirmación** por WhatsApp que debes ingresar para habilitarlo como destinatario de prueba.
4. **Producción:** cuando vayas a producción, en **WhatsApp → Phone numbers → Add phone number**, registra tu número real y verifícalo (SMS/llamada). Anota su **Phone number ID** y úsalo en Azure.

---

## §3. Tokens de acceso

Hay dos tipos:

### 3.1 Token temporal (solo para pruebas)
- En **API Setup** verás un **temporary access token** que **dura 24 horas**. Útil para una prueba rápida, **no** para producción.

### 3.2 Token permanente (producción) — recomendado
Se crea con un **System User**:
1. Ve a **Meta Business Suite → Business settings** ([business.facebook.com/settings](https://business.facebook.com/settings)).
2. **Users → System users → Add** → crea un system user (rol **Admin** o **Employee**), p. ej. `eltejido-system`.
3. **Assign assets:** asigna la **app** `El Tejido` y la **WhatsApp Business Account** al system user, con permisos completos (Manage).
4. Con el system user seleccionado → **Generate new token** → elige la app `El Tejido` → marca los permisos **`whatsapp_business_messaging`** y **`whatsapp_business_management`** → genera.
5. **Copia el token** (no se vuelve a mostrar) → cárgalo en Key Vault como el secreto **`wa-token`** (guía de Azure §5).

> Los tokens de system user pueden configurarse sin expiración (o de 60 días); para el MVP usa uno de larga duración y documenta cuándo rotarlo.

### 3.3 App Secret (para firmar/verificar el webhook)
1. En el panel de la app → **App settings → Basic**.
2. Junto a **App secret**, clic **Show** → copia el valor → cárgalo en Key Vault como **`wa-appsec`** (guía de Azure §5).
   *(El backend lo usa para validar la firma `X-Hub-Signature-256` de cada webhook entrante.)*

---

## §4. Configurar el Webhook (apuntar a Azure)

1. En el panel de la app → **WhatsApp → Configuration** (o **API Setup → Webhooks → Edit/Configure**).
2. **Callback URL:** `https://<webapp>.azurewebsites.net/webhook/whatsapp`.
3. **Verify token:** escribe **exactamente** la misma cadena que cargaste en Key Vault como `wa-verify-token`.
4. Clic **Verify and save**. Meta hará un `GET` a tu webhook con un `challenge`; el backend responde el challenge si el token coincide. Si falla:
   - Confirma que la app está desplegada y `/health` responde.
   - Confirma que el token coincide carácter a carácter.
   - Confirma que la URL es HTTPS y pública.
5. **Subscribe to fields:** en **Webhook fields**, suscríbete al menos a **`messages`** (mensajes entrantes y estados). Clic **Subscribe**.

> Para que los mensajes lleguen, el número de WhatsApp debe estar **suscrito** a la app (en producción, al registrar el número queda asociado a la WABA de la app).

---

## §5. Crear las plantillas de mensaje (HSM)

El Tejido necesita plantillas aprobadas para: **mensaje inicial**, **código de autenticación** y **repregunta fuera de ventana**. Las plantillas se crean en **WhatsApp Manager**.

1. Ve a **WhatsApp Manager** ([business.facebook.com/wa/manage/message-templates](https://business.facebook.com/wa/manage/message-templates)) → selecciona tu WABA → **Message templates → Create template**.

### 5.1 Plantilla: mensaje inicial
- **Category:** **Marketing** o **Utility** (Utility si es transaccional/de servicio; suele aprobar más rápido).
- **Name:** `el_tejido_saludo` (minúsculas y guiones bajos; **anótalo**, el backend lo referencia).
- **Language:** Spanish (es) — o el que uses.
- **Body:** texto con variables numeradas, p. ej.:
  ```
  Hola {{1}}, ayúdanos a contestar unas preguntas para {{2}}. Será breve.
  ```
  Donde `{{1}}` = nombre y `{{2}}` = nombre de campaña. En *Samples* da un ejemplo (`Ana`, `Convención 2026`).
- **Submit** → queda **In review**. La aprobación de Meta puede tardar de minutos a horas (a veces más).

### 5.2 Plantilla: código de autenticación
- **Category:** **Authentication** (Meta tiene un flujo específico para OTP).
- **Name:** `el_tejido_otp`.
- **Language:** es.
- Sigue el formato de autenticación de Meta (cuerpo con el código `{{1}}` y, si aplica, botón de copiar código). Ejemplo de cuerpo: `Tu código de acceso a El Tejido es {{1}}. Caduca en 5 minutos.`
- **Submit**.

### 5.3 Plantilla: repregunta (para fuera de la ventana de 24h)
- **Category:** **Utility**.
- **Name:** `el_tejido_repregunta`.
- **Body:** p. ej. `Hola {{1}}, ¿podrías ampliar tu respuesta anterior? {{2}}` (donde `{{2}}` es la repregunta sugerida).
- **Submit**.

> **Anota los nombres exactos** de las tres plantillas y el mapeo de variables; el equipo los configura en la campaña/Gateway (`Especificaciones/05` y `07`). Si Meta **rechaza** una plantilla, ajusta el texto (evita lenguaje promocional ambiguo en categorías Utility/Authentication) y reenvía.

---

## §6. Prueba de extremo a extremo

0. **Confirma que el backend lee los secretos de WhatsApp** antes de probar el envío real: llama a `GET /health/ready` con el header `X-Diag-Key` (ver guía de Azure §11.3). Los componentes `secreto:wa-token`, `secreto:wa-appsec` y `secreto:wa-verify-token` deben aparecer en `ok`. Si alguno sale `faltante`, cárgalo en Key Vault (§3.2/§3.3); si sale `error` (HTTP 403), revisa el rol *Key Vault Secrets User* de la identidad administrada. Así evitas depurar el webhook a ciegas.
1. Con el **token permanente** y el **Phone number ID** ya cargados en Azure (Key Vault + App settings), reinicia el App Service si hace falta.
2. Desde el portal de El Tejido, dispara el **envío del mensaje inicial** a un número de prueba ya habilitado (§2.3).
3. El usuario de prueba debe **recibir** el mensaje (plantilla `el_tejido_saludo`).
4. El usuario **responde** por WhatsApp → el webhook entrega el mensaje al backend → el sistema evalúa y responde **texto libre** (dentro de la ventana de 24h).
5. Verifica en Application Insights/logs que el webhook se recibió, la firma se validó y la evaluación ocurrió.

---

## §7. Notas de producción y límites

- **Verificación del negocio (Business Verification):** para levantar los límites de mensajería y salir del modo de prueba, Meta puede exigir **verificar tu negocio** en Business settings. Inícialo con tiempo.
- **Calidad y límites de mensajería:** los números nuevos empiezan con un límite diario de destinatarios que crece según la calidad. Para 5 usuarios del MVP no es problema; para los ~120 de la convención, monitorea el *messaging limit*.
- **Ventana de 24h:** fuera de ella solo se envían plantillas aprobadas (por eso existe `el_tejido_repregunta`).
- **Versión de Graph API:** el backend usa `WhatsApp:GraphApiBaseUrl` (p. ej. `https://graph.facebook.com/v21.0`). Usa una versión vigente y soportada; actualízala cuando Meta deprecie versiones.
- **Rotación de tokens:** si usaste un token con expiración, agéndalo. Rotar = generar nuevo token del system user y actualizar el secreto `wa-token` en Key Vault (nueva versión; el nombre no cambia).

---

## §8. Checklist

- [ ] App Business creada + producto WhatsApp añadido (§1).
- [ ] Phone number ID y WABA ID anotados (§2).
- [ ] Números de prueba habilitados (§2.3).
- [ ] Token permanente (system user) → secreto `wa-token` en Key Vault (§3.2).
- [ ] App Secret → secreto `wa-appsec` en Key Vault (§3.3).
- [ ] Webhook verificado contra `https://<webapp>.azurewebsites.net/webhook/whatsapp` con `wa-verify-token` (§4).
- [ ] Suscripción al campo `messages` (§4.5).
- [ ] Plantillas `el_tejido_saludo`, `el_tejido_otp`, `el_tejido_repregunta` creadas y **aprobadas** (§5).
- [ ] Prueba E2E exitosa (§6).

---

## Fuentes (documentación oficial consultada)
- WhatsApp Cloud API — Get Started: https://developers.facebook.com/docs/whatsapp/cloud-api/get-started/
- Business messaging / WhatsApp — Get started: https://developers.facebook.com/documentation/business-messaging/whatsapp/get-started
- Webhooks (overview): https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/overview/

*Fin de la guía.*
