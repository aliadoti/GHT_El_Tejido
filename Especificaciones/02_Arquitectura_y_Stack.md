# 02 — Arquitectura y Stack (consolidado y fijado)

**Propósito:** fijar el stack, las versiones, el layout de la solución y las fronteras de módulos para el MVP. Resume y **no contradice** la arquitectura aprobada (`ARQ`). Donde haya detalle, prima `ARQ`; donde haya versión o convención concreta, prima este documento.

---

## 1. Resumen de la arquitectura aprobada

El Tejido es un **monolito modular** desplegable en un solo proceso (`ARQ §0`). Cuatro capas (`ARQ §1.1`):

1. **Canal / Edge:** webhook de WhatsApp, API REST admin, hosting del SPA, verificación de firma y rate limiting.
2. **Aplicación (dominio modular):** orquestación conversacional, campañas, evaluación, Markdown, auth, seguridad.
3. **Datos y artefactos:** Cosmos DB (operacional), Blob Storage (Markdown), Key Vault (secretos).
4. **Integraciones externas:** WhatsApp Cloud API y proveedor LLM configurable.

Los módulos funcionales y sus fronteras están en `ARQ §1.3` y mapeados a carpetas en `01 §2`.

---

## 2. Stack y versiones (vinculante)

| Capa | Tecnología | Versión MVP | Notas |
|---|---|---|---|
| Runtime backend | .NET | **8 LTS** | Fijado en `global.json`. Soporte LTS, encaje Azure nativo (`ARQ §14 D2`). |
| Lenguaje backend | C# | 12 | `Nullable` on, warnings-as-errors. |
| Web framework | ASP.NET Core | 8 | Webhook + `/api/*` + hosting estático. |
| Base de datos | Azure Cosmos DB for NoSQL | Serverless | SDK `Microsoft.Azure.Cosmos` v3. (`ARQ §14 D3`) |
| Secretos | Azure Key Vault | Standard | Acceso por Managed Identity + RBAC. (`ARQ §14 D5`) |
| Artefactos | Azure Blob Storage | Standard LRS, Hot | Markdown + logs fríos. (`ARQ §14 D9`) |
| Observabilidad | Application Insights | — | Telemetría, trazas, alertas. |
| Hosting | Azure App Service | Linux **B1 Basic** | Always On para webhook estable. (`ARQ §2.2`) |
| Frontend | **Angular** | **22** (última estable, jun-2026) | Standalone components, signals, OnPush. (Decisión del cliente; sustituye la sugerencia React de `ARQ §14 D8`.) |
| LLM (por defecto) | **Azure OpenAI** | — | Proveedor por defecto **configurable**; el sistema soporta OpenAI directo u otro (`REQ §19`). Decisión final pendiente; el código no asume un proveedor único. |
| CI/CD | GitHub Actions | — | Sin Bicep en el MVP (decisión del cliente). Recursos creados a mano por el portal. |

> **Nota sobre el frontend:** la arquitectura aprobada sugería React/Vite, pero el cliente fijó **Angular (última versión)**. Esta es la única desviación respecto a `ARQ` y queda formalizada aquí. Todo lo demás de `ARQ` se mantiene.

> **Nota sobre el LLM:** el proveedor por defecto se documenta como **Azure OpenAI** (mantiene datos en el tenant, secretos y facturación unificados, `ARQ §14 D6`), pero el módulo de Evaluación (`08`) trata el proveedor como **configurable**: proveedor, modelo, endpoint y `apiKeyRef` viven en datos (`ConfigLLM`), no en código. La guía de Azure incluye Azure OpenAI como opcional.

---

## 3. Layout de la solución y hosting

```
ElTejido.sln
├─ ElTejido.Api            (host web; webhook + /api/*; sirve el SPA build)
├─ ElTejido.Application    (casos de uso / módulos de dominio)
├─ ElTejido.Domain         (entidades, value objects, interfaces; sin infra)
├─ ElTejido.Infrastructure (Cosmos, Blob, KeyVault, WhatsApp, LLM, AppInsights)
└─ ElTejido.Web            (Angular 22)
```

**Dirección de dependencias** (Clean/Onion):

```
Api ──▶ Application ──▶ Domain
 │            │
 └──▶ Infrastructure ──▶ Domain
```

`Domain` no depende de nadie. `Application` define interfaces (puertos); `Infrastructure` las implementa (adaptadores). `Api` es el composition root que cablea todo.

**Hosting del SPA (decisión MVP):** el build de Angular (`ElTejido.Web/dist`) se publica como **contenido estático servido por `ElTejido.Api`** (carpeta `wwwroot`), un solo App Service, un solo despliegue (`ARQ §3.1`). *(Alternativa equivalente: Azure Static Web App; se mantiene como opción si se prefiere separar, pero el MVP usa hosting único para minimizar recursos.)*

---

## 4. Endpoints expuestos por el host

| Ruta | Método | Propósito | Auth |
|---|---|---|---|
| `/webhook/whatsapp` | GET | Verificación del webhook (Meta `hub.challenge`). | Token de verificación |
| `/webhook/whatsapp` | POST | Recepción de mensajes entrantes. | Firma `X-Hub-Signature-256` |
| `/api/auth/*` | POST | Login admin por OTP de WhatsApp. | Pública (con rate limit) |
| `/api/admin/*` | varios | API del portal (CRUD, envíos, consultas). | Sesión admin + rol |
| `/` y estáticos | GET | SPA del portal. | Pública (la app exige login client-side; la API protege los datos) |
| `/health` | GET | Liveness/readiness para App Service. | Pública, sin datos sensibles |

Detalle completo en `04`.

---

## 5. Procesamiento asíncrono (in-process)

Para el volumen del MVP (5→120 usuarios) los trabajos en segundo plano (envío masivo, compilación Markdown, procesamiento del webhook tras el ack) se ejecutan **dentro del mismo proceso** con una **cola en memoria + `IHostedService`** (`ARQ §3`, `D10`). La frontera queda lista para extraer a Azure Functions + Service Bus en post-MVP, sin reescritura.

Reglas:
- El webhook responde **200 OK inmediato** y encola el trabajo (`ARQ §4.2`).
- Los handlers de cola son idempotentes (dedupe por `whatsappMessageId`).
- Si el proceso se reinicia, los trabajos en cola en memoria se pierden: aceptable en MVP porque (a) Meta reintenta el webhook y (b) el envío masivo es re-disparable desde el portal por estado de participante. Documentar este límite en `13` (riesgos de prueba).

---

## 6. Configuración por entornos

Un solo entorno para el MVP (`mvp`/`staging`), opcionalmente un deployment slot (`ARQ §2.3`).

Fuentes de configuración (orden de precedencia, mayor gana):
1. Variables de entorno de App Service (Application settings).
2. `appsettings.{Environment}.json`.
3. `appsettings.json` (valores por defecto, sin secretos).

Secciones de configuración esperadas (claves exactas en cada doc de módulo):

```
Cosmos:        AccountEndpoint, DatabaseName, contenedores
Blob:          AccountUrl, ContainerName
KeyVault:      Uri
WhatsApp:      GraphApiBaseUrl, PhoneNumberId, VerifyTokenSecretName, AppSecretSecretName, AccessTokenSecretName, plantillas
Llm:           Provider, Model, Endpoint, ApiKeySecretName, Timeout, MaxRetries, límites de tokens
Auth:          SessionTtl, OtpTtl, intentos, SigningKeySecretName, OtpSaltSecretName
Seguridad:     límites de longitud, rate limits, máximos por campaña
ApplicationInsights: ConnectionString (vía secreto o setting)
```

Los **nombres de secretos** referenciados aquí DEBEN existir en Key Vault con el mismo nombre que define la guía de Azure (`Guia_Azure_Portal_Paso_a_Paso.md §0`).

---

## 7. Entorno de desarrollo local

- **.NET SDK 8** (vía `global.json`). `dotnet restore && dotnet build && dotnet test`.
- **Cosmos:** emulador de Azure Cosmos DB o una cuenta serverless de desarrollo. Endpoint y key por `user-secrets`.
- **Blob:** Azurite local o cuenta de desarrollo.
- **Key Vault:** en local, los secretos van por `dotnet user-secrets`; `DefaultAzureCredential` cae a credenciales del desarrollador (Azure CLI `az login`) cuando se apunta a un Key Vault real.
- **WhatsApp:** en local, el webhook se expone con un túnel (p. ej. dev tunnel / ngrok) hacia `/webhook/whatsapp` para pruebas manuales; o se mockea el `IWhatsAppClient`.
- **LLM:** mock del `ILlmClient` para pruebas; opcionalmente un endpoint real de desarrollo.
- **Angular:** `ng serve` con proxy a la API local (`proxy.conf.json`) durante desarrollo; en build de producción se publica a `wwwroot` del Api.

---

## 8. Principios no funcionales heredados (resumen)

De `REQ §31`: flexibilidad sin tocar código (todo configurable en datos), baja fricción conversacional, parametrización, seguridad básica desde el día uno, control de consumo, portabilidad (regenerar Markdown), y mantenibilidad por separación de módulos. Cada documento de módulo detalla cómo cumple los que le aplican.

*Fin del documento.*
