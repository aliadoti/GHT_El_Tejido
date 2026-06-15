# Guía paso a paso — Creación de recursos en el Portal de Azure (El Tejido MVP)

**Para:** la persona que aprovisionará la infraestructura del MVP.
**Qué es esto:** instrucciones manuales, clic a clic, para crear en el **portal de Azure** todos los recursos que el sistema necesita. El código **no** crea estos recursos: los consume por configuración. Hazlo **una sola vez**.
**Portal:** [https://portal.azure.com](https://portal.azure.com) (versión vigente, junio 2026).

> **Importante sobre el portal:** Microsoft actualiza la interfaz con frecuencia. Los nombres de botones pueden variar ligeramente. Si no encuentras un texto exacto, usa la **barra de búsqueda superior** del portal (escribe el nombre del servicio) y busca la opción equivalente. La secuencia lógica se mantiene.

> **Convención de la guía:** cada vez que veas `<algo>` reemplázalo por tu valor real y **anótalo** en la tabla de §0, porque el equipo de desarrollo usará exactamente esos nombres en la configuración de la aplicación.

---

## §0. Antes de empezar — datos y nomenclatura

### 0.1 Requisitos previos
- Una **suscripción de Azure** activa y permisos de **Propietario** (Owner) o **Colaborador** (Contributor) sobre ella (para crear recursos y asignar roles).
- Acceso de administrador en **Microsoft Entra ID** (antes Azure AD) para crear el registro de aplicación de GitHub (§9).
- Un nombre corto de proyecto para prefijos. Sugerencia: `eltejido`.
- Región recomendada: una sola región cercana a los usuarios (p. ej. **East US 2** o **Brazil South**). **Cosmos serverless corre en una sola región y no se puede cambiar después**, así que elígela bien.

### 0.2 Tabla de nomenclatura (rellénala y compártela con el equipo)

| Recurso                  | Placeholder        | Valor que elegiste                        | Lo usa la app como                     |
| ------------------------ | ------------------ | ----------------------------------------- | -------------------------------------- |
| Grupo de recursos        | `<rg>`             | `rg-eltejido-mvp`                         | —                                      |
| Región                   | `<region>`         |                                           | —                                      |
| Cuenta Cosmos DB         | `<cosmos-account>` | `cosmos-eltejido-mvp`                     | `Cosmos:AccountEndpoint`               |
| Base de datos Cosmos     | `eltejido`         | `eltejido`                                | `Cosmos:DatabaseName`                  |
| Cuenta de Storage (Blob) | `<storage>`        | `steltejidomvp` (sin guiones, minúsculas) | `Blob:AccountUrl`                      |
| Contenedor Blob          | `markdown`         | `markdown`                                | `Blob:ContainerName`                   |
| Key Vault                | `<keyvault>`       | `kv-eltejido-mvp`                         | `KeyVault:Uri`                         |
| App Service Plan         | `<plan>`           | `plan-eltejido-mvp`                       | —                                      |
| App Service (Web App)    | `<webapp>`         | `app-eltejido-mvp`                        | host público                           |
| Application Insights     | `<appinsights>`    | `appi-eltejido-mvp`                       | `ApplicationInsights:ConnectionString` |
| (Opcional) Azure OpenAI  | `<aoai>`           | `aoai-eltejido-mvp`                       | `Llm:Endpoint`                         |

> Nombres con reglas: la **cuenta de Storage** debe ser global única, 3–24 caracteres, solo minúsculas y números (sin guiones). La **cuenta Cosmos**, el **Key Vault** y la **Web App** también deben ser únicos globalmente.

### 0.3 Secretos que cargarás en Key Vault (§5)
| Nombre del secreto (exacto) | Contenido                                            | De dónde sale              |
| --------------------------- | ---------------------------------------------------- | -------------------------- |
| `llm-key`                   | API key del proveedor LLM                            | Azure OpenAI (§8) u OpenAI |
| `wa-token`                  | Access token de WhatsApp Cloud API                   | Guía de Meta               |
| `wa-appsec`                 | App Secret de la app de Meta (firma webhook)         | Guía de Meta               |
| `wa-verify-token`           | Token de verificación del webhook (lo inventas tú)   | Tú lo defines              |
| `jwt-sign`                  | Clave para firmar sesiones (cadena aleatoria larga)  | Genérala tú                |
| `otp-salt`                  | Sal para el hash de los OTP (cadena aleatoria larga) | Genérala tú                |
| `diag-key` *(opcional)*     | Clave del endpoint de verificación `/health/ready`   | Genérala tú (§11)          |

> Para `jwt-sign` y `otp-salt` genera cadenas aleatorias de 32+ bytes (p. ej. en PowerShell: `[Convert]::ToBase64String((1..48|%{Get-Random -Max 256}))`).

---

## §1. Crear el Grupo de Recursos

1. En el portal, barra de búsqueda superior → escribe **Resource groups** (Grupos de recursos) → entra.
2. Clic en **+ Create** (Crear).
3. **Subscription:** tu suscripción. **Resource group:** `<rg>` (p. ej. `rg-eltejido-mvp`). **Region:** `<region>`.
4. **Review + create** → **Create**.

Todos los recursos siguientes se crean **dentro de este grupo** y en la **misma región**.

---

## §2. Crear Cosmos DB (NoSQL, serverless)

1. Búsqueda superior → **Azure Cosmos DB** → **+ Create**.
2. En la tarjeta **Azure Cosmos DB for NoSQL**, clic **Create**.
3. Pestaña **Basics**:
   - **Subscription / Resource group:** los de §1.
   - **Account Name:** `<cosmos-account>`.
   - **Location:** `<region>`.
   - **Capacity mode:** selecciona **Serverless**. *(Clave: serverless paga por consumo y es ideal para el MVP. Recuerda: una sola región, no ampliable después.)*
4. (Las pestañas Networking, Backup y Encryption quedan deshabilitadas o por defecto en serverless; déjalas como están.)
5. **Review + create** → **Create**. Espera a que termine el despliegue (unos minutos).
6. Ve al recurso. En el menú izquierdo, **Settings → Keys**: copia el **URI** (campo *URI*), que es tu `Cosmos:AccountEndpoint` (algo como `https://<cosmos-account>.documents.azure.com:443/`). *(No necesitas copiar las claves: la app usará Managed Identity. Si en el MVP prefieres clave, copia la PRIMARY KEY, pero lo recomendado es identidad administrada.)*

### 2.1 Crear la base de datos y los contenedores
1. En el recurso Cosmos, menú izquierdo **Data Explorer**.
2. Clic **New Database** (o **New Container** y crea la base al paso): **Database id** = `eltejido`. (En serverless no se asigna throughput.) **OK**.
3. Crea los **contenedores** uno a uno con **New Container**. Para cada uno: **Database id** = `eltejido` (Use existing), **Container id** y **Partition key** según la tabla:

   | Container id | Partition key |
   |---|---|
   | `users` | `/pk` |
   | `campaigns` | `/id` |
   | `participants` | `/campaniaId` |
   | `conversations` | `/campaniaId` |
   | `responses` | `/campaniaId` |
   | `config` | `/pk` |
   | `security` | `/pk` |
   | `leases` | `/id` |

4. **TTL** (expiración automática) en `security` y `leases`:
   - Abre el contenedor → **Settings** (Scale & Settings) → **Time to Live** → selecciona **On (no default)** → **Save**. Esto permite que cada documento defina su propio `ttl` (lo hace la app para los OTP y el dedupe del webhook).
5. **Unique key** en `users` (recomendado): al crear el contenedor `users`, en **Unique keys** agrega `/whatsappNormalizado`. *(Si ya lo creaste sin esto, no se puede añadir después; bórralo y recréalo si quieres la restricción. La app también valida unicidad por código, así que no es bloqueante.)*

---

## §3. Crear la cuenta de Storage (Blob) para los Markdown

1. Búsqueda superior → **Storage accounts** → **+ Create**.
2. **Basics:**
   - **Resource group:** `<rg>`. **Storage account name:** `<storage>` (minúsculas/números, sin guiones).
   - **Region:** `<region>`. **Primary service:** Azure Blob Storage.
   - **Performance:** Standard. **Redundancy:** **LRS** (suficiente y barato para el MVP).
3. **Review + create** → **Create**.
4. Ve al recurso → menú **Data storage → Containers** → **+ Container**:
   - **Name:** `markdown`. **Anonymous access level:** **Private (no anonymous access)**. **Create**.
5. Anota la **URL del Blob**: menú **Settings → Endpoints** → copia **Blob service** (algo como `https://<storage>.blob.core.windows.net/`) → es tu `Blob:AccountUrl`.

---

## §4. Crear Application Insights

1. Búsqueda superior → **Application Insights** → **+ Create**.
2. **Resource group:** `<rg>`. **Name:** `<appinsights>`. **Region:** `<region>`. **Resource Mode:** Workspace-based (por defecto; si pide un Log Analytics workspace, deja que cree uno o elige uno existente).
3. **Review + create** → **Create**.
4. Ve al recurso → **Overview** → copia la **Connection String** → es tu `ApplicationInsights:ConnectionString`.

---

## §5. Crear el Key Vault y cargar secretos

1. Búsqueda superior → **Key vaults** → **+ Create**.
2. **Basics:**
   - **Resource group:** `<rg>`. **Key vault name:** `<keyvault>`. **Region:** `<region>`. **Pricing tier:** Standard.
   - **Permission model:** elige **Azure role-based access control (RBAC)**. *(Para Key Vaults nuevos, RBAC es el modelo por defecto y el recomendado.)*
3. **Review + create** → **Create**.
4. **Dar acceso a tu propio usuario para poder cargar secretos:**
   - En el Key Vault → **Access control (IAM)** → **+ Add → Add role assignment**.
   - **Role:** **Key Vault Secrets Officer** (permite crear/leer secretos). **Members:** tu usuario. **Review + assign**.
   - Espera 1–2 minutos a que el rol propague.
5. **Cargar los secretos** (menú **Objects → Secrets → + Generate/Import**), uno por cada fila de §0.3:
   - **Name:** el nombre exacto (p. ej. `jwt-sign`). **Secret value:** el valor. **Create**.
   - Repite para: `jwt-sign`, `otp-salt`, `wa-verify-token`, y más adelante `llm-key`, `wa-token`, `wa-appsec` (estos tres salen de las guías de Meta/LLM; puedes cargarlos cuando los tengas).
6. Anota el **Vault URI**: en **Overview**, campo **Vault URI** (`https://<keyvault>.vault.azure.net/`) → es tu `KeyVault:Uri`.

---

## §6. Crear el App Service (Web App, Linux, .NET 8)

1. Búsqueda superior → **App Services** → **+ Create → Web App**.
2. **Basics:**
   - **Resource group:** `<rg>`. **Name:** `<webapp>` (será `https://<webapp>.azurewebsites.net`).
   - **Publish:** **Code**. **Runtime stack:** **.NET 8 (LTS)**. **Operating System:** **Linux**. **Region:** `<region>`.
   - **Pricing plan:** clic en **Create new** plan → nombre `<plan>` → **Pricing plan:** selecciona **Basic B1**. *(B1 permite Always On, necesario para el webhook estable.)*
3. **Monitoring / Application Insights:** si el asistente lo ofrece, **Enable** y selecciona `<appinsights>` (si no, lo conectas en §7.3).
4. **Review + create** → **Create**.
5. **Activar Always On:**
   - Ve al App Service → **Settings → Configuration → General settings** (en portales recientes: **Settings → Configuration**) → **Always On: On** → **Save**. *(Evita cold starts que romperían el webhook de WhatsApp.)*

---

## §7. Identidad administrada y permisos del App Service

### 7.1 Activar identidad administrada del App Service
1. En el App Service → **Settings → Identity**.
2. Pestaña **System assigned** → **Status: On** → **Save** → confirma. Se crea una identidad (anota su **Object (principal) ID** si quieres).

### 7.2 Dar a esa identidad acceso a Key Vault, Cosmos y Blob
Asigna estos roles a la **identidad administrada del App Service** (`<webapp>`):

**a) Key Vault — leer secretos:**
1. Ve a `<keyvault>` → **Access control (IAM) → + Add → Add role assignment**.
2. **Role:** **Key Vault Secrets User** (solo lectura de secretos; mínimo privilegio).
3. **Members → Assign access to: Managed identity → + Select members →** tipo **App Service →** elige `<webapp>` → **Select → Review + assign**.

**b) Cosmos DB — datos:**
1. Ve a `<cosmos-account>` → **Access control (IAM) → + Add → Add role assignment**.
2. **Role:** **Cosmos DB Built-in Data Contributor** (rol de datos del plano de datos). *(Si no aparece en la lista de IAM porque es un rol de datos del plano de Cosmos, asígnalo con Azure CLI: `az cosmosdb sql role assignment create` — ver nota 7.4. En el portal, el rol del plano de control "DocumentDB Account Contributor" no basta para leer/escribir datos.)*
3. **Members:** la identidad administrada de `<webapp>` → **Review + assign**.

**c) Blob Storage — leer/escribir blobs:**
1. Ve a `<storage>` → **Access control (IAM) → + Add → Add role assignment**.
2. **Role:** **Storage Blob Data Contributor**.
3. **Members:** la identidad administrada de `<webapp>` → **Review + assign**.

### 7.3 Conectar Application Insights (si no se hizo en §6.3)
1. En el App Service → **Settings → Application Insights** → **Turn on / Enable** → selecciona `<appinsights>` → **Apply**.

### 7.4 Nota sobre el rol de datos de Cosmos
El acceso a **datos** de Cosmos (leer/escribir documentos con Managed Identity) usa roles del **plano de datos**, que a veces no se ven en la pestaña IAM del portal. Si es tu caso, pídele a quien tenga Azure CLI que ejecute (una sola vez):
```bash
az cosmosdb sql role assignment create \
  --account-name <cosmos-account> \
  --resource-group <rg> \
  --role-definition-id 00000000-0000-0000-0000-000000000002 \  # Built-in Data Contributor
  --principal-id <object-id-de-la-identidad-del-webapp> \
  --scope "/"
```
(El `principal-id` es el Object ID de §7.1.)

---

## §8. (Opcional) Azure OpenAI como proveedor LLM por defecto

> Solo si usarán **Azure OpenAI**. Si usarán **OpenAI directo**, omite esta sección: basta con cargar `llm-key` en Key Vault (§5) con la API key de OpenAI.

> **Acceso:** Azure OpenAI puede requerir que tu suscripción tenga habilitado el servicio. Si al crear no aparece, solicita acceso desde la página del servicio y espera la aprobación.

1. Búsqueda superior → **Azure OpenAI** → **+ Create**.
2. **Resource group:** `<rg>`. **Name:** `<aoai>`. **Region:** una soportada. **Pricing tier:** Standard S0.
3. **Review + create** → **Create**.
4. Ve al recurso → **Azure AI Foundry / Model deployments** (o **Model deployments → Manage deployments**) → **+ Create new deployment**:
   - Elige un modelo (p. ej. `gpt-4o-mini`) → ponle un **Deployment name** (anótalo: es tu `Llm:Model` / nombre de deployment).
5. **Keys and Endpoint** (menú **Resource Management → Keys and Endpoint**): copia el **Endpoint** (`https://<aoai>.openai.azure.com/`) → es tu `Llm:Endpoint`; copia **KEY 1** → cárgala en Key Vault como el secreto **`llm-key`** (§5).
6. *(Recomendado)* En vez de clave, puedes dar a la identidad del App Service el rol **Cognitive Services OpenAI User** sobre `<aoai>` (IAM → Add role assignment). Si lo haces, la app puede autenticarse por identidad; coordina con el equipo cuál camino usarán.

---

## §9. Conexión GitHub Actions → Azure (OIDC, sin contraseñas)

Esto permite que el pipeline de despliegue (ver `Especificaciones/12_CICD_GitHub_Actions.md`) publique al App Service sin guardar contraseñas.

### 9.1 Registrar la aplicación en Entra ID
1. Búsqueda superior → **Microsoft Entra ID** → **App registrations** → **+ New registration**.
2. **Name:** `gh-eltejido-deploy`. **Supported account types:** *Accounts in this organizational directory only*. **Register**.
3. En **Overview**, copia **Application (client) ID** y **Directory (tenant) ID** → serán `AZURE_CLIENT_ID` y `AZURE_TENANT_ID` en GitHub.

### 9.2 Agregar la credencial federada
1. En la app registrada → **Certificates & secrets → Federated credentials → + Add credential**.
2. **Credential scenario / type:** **GitHub Actions deploying Azure resources**.
3. **Organization:** tu organización/usuario de GitHub. **Repository:** el repo de El Tejido. **Entity type:** **Branch** → **Branch:** `main`.
4. **Audience:** deja `api://AzureADTokenExchange` (valor por defecto). **Name:** `gh-main`. **Add**.

### 9.3 Dar permiso de despliegue a esa app
1. Ve al **Grupo de recursos** `<rg>` → **Access control (IAM) → + Add → Add role assignment**.
2. **Role:** **Contributor** (o, más acotado, **Website Contributor** si solo desplegará la web). **Members:** busca la app `gh-eltejido-deploy` → **Review + assign**.

### 9.4 Cargar valores en GitHub
En el repo de GitHub → **Settings → Secrets and variables → Actions → Variables** (pestaña *Variables*), crea:
- `AZURE_CLIENT_ID` = Application (client) ID (§9.1)
- `AZURE_TENANT_ID` = Directory (tenant) ID (§9.1)
- `AZURE_SUBSCRIPTION_ID` = ID de tu suscripción (búscalo en **Subscriptions**)
- `AZURE_WEBAPP_NAME` = `<webapp>`

---

## §10. Configurar los Application Settings del App Service

La app lee su configuración de aquí. En el App Service → **Settings → Environment variables** (o **Configuration → Application settings** en portales previos) → **+ Add** para cada una → **Apply/Save**:

| Name                                    | Value                                                                       |
| --------------------------------------- | --------------------------------------------------------------------------- |
| `Cosmos__AccountEndpoint`               | `https://<cosmos-account>.documents.azure.com:443/`                         |
| `Cosmos__DatabaseName`                  | `eltejido`                                                                  |
| `Blob__AccountUrl`                      | `https://<storage>.blob.core.windows.net/`                                  |
| `Blob__ContainerName`                   | `markdown`                                                                  |
| `KeyVault__Uri`                         | `https://<keyvault>.vault.azure.net/`                                       |
| `ApplicationInsights__ConnectionString` | (la de §4.4)                                                                |
| `Llm__Provider`                         | `AzureOpenAI` u `OpenAI`                                                    |
| `Llm__Endpoint`                         | (si Azure OpenAI: `https://<aoai>.openai.azure.com/`)                       |
| `Llm__ApiKeySecretName`                 | `llm-key`                                                                   |
| `WhatsApp__GraphApiBaseUrl`             | `https://graph.facebook.com/v21.0` (la versión que indique la guía de Meta) |
| `WhatsApp__PhoneNumberId`               | (de la guía de Meta)                                                        |
| `WhatsApp__VerifyTokenSecretName`       | `wa-verify-token`                                                           |
| `WhatsApp__AppSecretSecretName`         | `wa-appsec`                                                                 |
| `WhatsApp__AccessTokenSecretName`       | `wa-token`                                                                  |
| `Auth__SigningKeySecretName`            | `jwt-sign`                                                                  |
| `Auth__OtpSaltSecretName`               | `otp-salt`                                                                  |
| `Diagnostico__ClaveSecretName`          | `diag-key` (si guardas la clave de verificación en Key Vault, recomendado)  |
| `Diagnostico__Clave`                    | (alternativa: la clave directa, si **no** usas Key Vault para ella)         |

> El doble guion bajo `__` es la forma de anidar secciones de configuración en variables de entorno de .NET (equivale a `Cosmos:AccountEndpoint`). El equipo de desarrollo confirmará los nombres exactos de cada clave según `Especificaciones/02_Arquitectura_y_Stack.md §6`; si alguna difiere, ajústala aquí.

> **Sobre el proveedor LLM (`Llm__*`):** en el MVP el **proveedor, el endpoint y la referencia de la API key del LLM se configuran desde el portal** (sección Config LLM, que se guarda en Cosmos), **no** por app settings. Basta con cargar el secreto `llm-key` en Key Vault (§5). Las filas `Llm__Provider`/`Llm__Endpoint`/`Llm__ApiKeySecretName` son opcionales y hoy **el código no las lee**; déjalas vacías salvo indicación del equipo.

> **Clave de diagnóstico (`Diagnostico__*`):** habilita el endpoint de verificación `GET /health/ready` (§11). Define **una** de las dos: `Diagnostico__ClaveSecretName` apuntando a un secreto de Key Vault (p. ej. `diag-key`, cárgalo en §5 con una cadena aleatoria larga) **o** `Diagnostico__Clave` con la cadena directa. Si **ninguna** se configura, `/health/ready` responde 404 (deshabilitado), de modo que nunca expone la postura de infraestructura por defecto.

---

## §11. Verificación final

1. **Despliega** una vez (haz merge a `main` para disparar el pipeline, o sube un build manual). Ver guía `12`.
2. Abre `https://<webapp>.azurewebsites.net/health` → debe responder `200 OK`. *(Esto solo confirma que el proceso arrancó: liveness. No verifica Key Vault, Cosmos ni Blob.)*
3. **Verificación de dependencias (readiness) — recomendada.** Llama al endpoint protegido `GET /health/ready` con el header `X-Diag-Key` igual a la clave de diagnóstico que configuraste en §10:
   ```powershell
   curl -H "X-Diag-Key: <tu-clave-de-diagnostico>" https://<webapp>.azurewebsites.net/health/ready
   ```
   - **`200`** con `"estado":"ok"` → todas las dependencias requeridas están presentes y accesibles.
   - **`503`** con `"estado":"faltante"` o `"error"` → revisa el desglose por componente. Cada componente reporta `ok` / `faltante` / `error` / `no_aplica`, **sin exponer el valor de ningún secreto**. Ejemplos: `secreto:wa-token` en `faltante` = aún no cargaste ese secreto en Key Vault; `cosmos` en `error` (HTTP 403) = falta el rol de datos de la identidad (§7.2b/§7.4); `blob` en `error` (HTTP 403) = falta *Storage Blob Data Contributor* (§7.2c).
   - **`404`** → no configuraste `Diagnostico__ClaveSecretName`/`Diagnostico__Clave` (§10), o el header no coincide. El endpoint queda oculto por defecto.

   > Úsalo justo después de cargar los **secretos de WhatsApp** (`wa-token`, `wa-appsec`, `wa-verify-token`) para confirmar de un vistazo que la identidad del App Service los lee. La caché de secretos es de 5 min, pero un secreto recién agregado se ve de inmediato porque solo se cachean lecturas exitosas.
4. En el App Service → **Log stream** o en Application Insights, verifica que la app **lee Key Vault** sin errores de autenticación (si falla, revisa el rol *Key Vault Secrets User* de §7.2a y que la identidad esté **On**).
5. En **Data Explorer** de Cosmos, confirma que la app puede leer/escribir (tras la primera operación aparecerán documentos).

---

## §12. Checklist de aprovisionamiento

- [ ] Grupo de recursos creado (§1).
- [ ] Cosmos serverless + base `eltejido` + 8 contenedores con sus partition keys (§2).
- [ ] TTL activado en `security` y `leases`; unique key en `users` (§2.1).
- [ ] Storage + contenedor `markdown` privado (§3).
- [ ] Application Insights + connection string copiada (§4).
- [ ] Key Vault (RBAC) + secretos `jwt-sign`, `otp-salt`, `wa-verify-token` (§5); `llm-key`, `wa-token`, `wa-appsec` cuando estén disponibles.
- [ ] App Service Linux .NET 8 B1 + Always On (§6).
- [ ] Identidad administrada On + roles en Key Vault, Cosmos y Blob (§7).
- [ ] (Opcional) Azure OpenAI + deployment + `llm-key` cargada (§8).
- [ ] OIDC GitHub↔Azure + variables en GitHub (§9).
- [ ] Application settings del App Service (§10).
- [ ] (Opcional) Secreto `diag-key` + `Diagnostico__ClaveSecretName` para habilitar `/health/ready` (§10–§11).
- [ ] `/health` responde 200 (§11).
- [ ] `/health/ready` responde 200 con `estado: ok` tras cargar todos los secretos (§11.3).

---

## Fuentes (documentación oficial consultada)
- Azure Cosmos DB serverless — creación y consideraciones: https://learn.microsoft.com/en-us/azure/cosmos-db/serverless
- Gestionar Cosmos por el portal: https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-manage-database-account
- Key Vault con RBAC (rol Secrets User, RBAC por defecto): https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide
- Managed identity de App Service hacia Key Vault: https://learn.microsoft.com/en-us/azure/key-vault/general/authentication
- OIDC GitHub Actions ↔ Azure (credencial federada): https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure-openid-connect
- Configurar OpenID Connect en Azure (GitHub Docs): https://docs.github.com/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-azure

*Fin de la guía.*
