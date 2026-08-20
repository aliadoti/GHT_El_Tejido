# Plan de montaje del ambiente de producción — El Tejido de la Red

**Versión:** 1.0 (borrador para revisión)
**Fecha:** 2026-08-17
**Alcance:** crear desde cero el ambiente de **producción** en Azure para la Convención GHT 2026, conservando intacto el ambiente actual de desarrollo/QAS.
**Ejecutable por:** una persona con conocimientos técnicos básicos, acceso de Owner a la suscripción de Azure, acceso de administrador en Microsoft Entra ID, acceso de admin al repositorio de GitHub y acceso al Business Manager de Meta.
**Estado:** PLAN — nada de esto se ha ejecutado todavía.

> **Cómo leer este documento.** Las fases 1 a 11 son secuenciales salvo donde se indica lo contrario. Cada paso dice dónde hacer clic y qué anotar. Todo lo que aparece como `<algo>` se reemplaza por un valor real y se registra en la tabla del **Anexo A**, que es la hoja de trabajo del despliegue.

---

## §0. Resumen ejecutivo

El sistema es un **monolito modular .NET 8** que se despliega como **un único artefacto** (la API sirve también el portal Angular desde `wwwroot`) sobre **un único App Service**. Eso hace que montar producción sea, en esencia, replicar siete recursos de Azure y volver a parametrizar la aplicación desde su propia consola administrativa.

El ambiente de producción nace **sin un solo dato de negocio**. Lo único que existirá en la base de datos al terminar la Fase 7 es **un usuario administrador**, creado a través de la página de simulación de WhatsApp del propio portal. A partir de ahí, toda la configuración —catálogos de textos en español e inglés, campaña, preguntas, rúbrica, prompts, ConfigLLM— se teclea a mano desde la consola administrativa. Nada se importa desde el ambiente de desarrollo.

**Duración estimada:** 10 a 12 días hábiles, con la parametrización manual (Fase 8) como camino crítico.

### Decisiones ya tomadas

| # | Decisión | Elección |
|---|---|---|
| 1 | Aislamiento | Nuevo grupo de recursos en la **misma suscripción** |
| 2 | Región | **East US 2** (Cosmos serverless es de región única e inmutable) |
| 3 | Acceso admin inicial | Simulación de WhatsApp **habilitada de forma permanente**, protegida por `X-Diag-Key` |
| 4 | Flujo de despliegue | **Tag de release `v*` + GitHub Environment `production` con aprobación humana**; identidad OIDC propia limitada al RG de producción |
| 5 | WhatsApp | **Mismo WABA, segundo número** para producción, con *webhook override* por número |
| 6 | Configuración inicial | **100 % manual desde el portal**; no se importa ningún JSON desde dev/QAS |
| 7 | Proveedor LLM | **Proveedor externo** vía API key en Key Vault; **no se crea Azure OpenAI** |
| 8 | Dominio | **Hostname por defecto** de Azure; sin dominio personalizado ni certificados |
| 9 | Respaldo y monitoreo | Cosmos con **backup continuo**, alertas de Application Insights, **budget alert** sobre el RG |
| 10 | Plazo | Menos de 2 semanas hasta el primer envío real |

### Las tres cosas que pueden hundir este despliegue

Están desarrolladas en §4. En una línea cada una:

1. **La clave de diagnóstico es, en la práctica, la credencial raíz del sistema.** Con la simulación habilitada de forma permanente, quien tenga esa clave puede fabricarse un usuario administrador y entrar al portal. Su custodia no es un detalle operativo, es *el* control de seguridad del ambiente.
2. **La *unique key* del contenedor `users` es inmutable.** Si se crea mal, la única salida es borrar y recrear el contenedor. Hay que verificarla antes de cargar cualquier dato.
3. **Cambiar un Application Setting reinicia el proceso y vacía las colas en memoria.** Durante el evento, tocar la configuración tiene un costo real: mensajes en vuelo que se pierden.

---

## §1. Estado actual verificado

Este inventario sale de la lectura directa del repositorio, no de la documentación.

### 1.1 Estructura de la solución

```
ElTejido.sln
├─ src/ElTejido.Api             host web: webhook, /api/*, sirve el SPA desde wwwroot
├─ src/ElTejido.Application     casos de uso y módulos de dominio
├─ src/ElTejido.Domain          entidades y puertos, sin dependencias de infraestructura
├─ src/ElTejido.Infrastructure  Cosmos, Blob, Key Vault, WhatsApp, LLM
├─ src/ElTejido.Web             Angular 22 (compila directo a ElTejido.Api/wwwroot)
├─ src/ElTejido.Calibracion     banco de calibración del LLM (consume tokens de pago)
└─ tests/                       ElTejido.UnitTests, ElTejido.IntegrationTests
```

`Directory.Build.props` y `global.json` fijan .NET 8 con *nullable* activo y *warnings as errors*.

### 1.2 Estado de Git

```
1215872  ready to production deploy     ← HEAD actual
28c3cb1  DT-P32-04 multiidioma refactoring  ← commit congelado por el acta
20ee675  test(evaluacion): ...
```

> **Acción requerida antes de la Fase 5.** El acta de congelamiento (`Especificaciones/Decision_Congelamiento_Codigo_Convencion_2026.md`, 2026-08-16) congela `28c3cb1`, pero HEAD es `1215872`. Hay que ejecutar `git diff 28c3cb1..1215872` y decidir formalmente cuál de los dos se etiqueta como release. Si `1215872` contiene cambios de código y no solo de documentación, el acta debe revalidarse con CI en verde antes de etiquetar. **No se etiqueta nada hasta que esta decisión esté firmada.**

### 1.3 Pipelines existentes

| Workflow | Disparo | Qué hace |
|---|---|---|
| `ci.yml` | pull request y push a `main` | Backend: `restore`, `build -warnaserror`, `dotnet format --verify-no-changes`, `test` excluyendo `Category=Calibracion`. Frontend: `npm ci`, `lint`, `test`, `build --configuration production`. |
| `deploy.yml` | push a `main` y manual | Compila el SPA, hace `dotnet publish` de la Api, entra a Azure por OIDC y publica al App Service que indique `vars.AZURE_WEBAPP_NAME`. Termina con smoke test de `/health`. **No usa `environment:`** porque la credencial federada se creó de tipo *Branch = main*. |

`deploy.yml` **no se modifica**. Los tags no disparan un workflow configurado con `on: push: branches: [main]`, así que dev/QAS y producción quedan naturalmente aislados.

**Variables de Actions a nivel de repositorio (estado actual):** `AZURE_WEBAPP_NAME` = `app-eltejido-mvp`, más `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` y `AZURE_SUBSCRIPTION_ID`. Son las que consume `deploy.yml` hoy y **apuntan todas a dev/QAS**. No se tocan: producción las sobreescribe desde su propio Environment (§10.4).

**Existe un GitHub Environment `production` huérfano.** Lo creó GitHub automáticamente cuando `deploy.yml` todavía llevaba `environment: production` (commit `16b8aef`), y quedó sin uso al quitarse esa línea en `dbb4226`. Está vacío y sin restricciones. Es un riesgo activo para el plan y se trata en detalle en §10.4.0.

### 1.4 Modelo de datos en Cosmos

Ocho contenedores. Nótese que varios repositorios comparten contenedor: `config` aloja configuración y catálogos de textos; `conversations` aloja conversaciones y enrutamientos de aporte; `security` aloja los códigos OTP y el log de seguridad.

| Contenedor | Partition key | Ajustes especiales |
|---|---|---|
| `users` | `/pk` | **Unique key `/claveUnicidad`** — obligatoria, solo se puede definir al crear |
| `campaigns` | `/id` | — |
| `participants` | `/campaniaId` | — |
| `conversations` | `/campaniaId` | — |
| `responses` | `/campaniaId` | — |
| `config` | `/pk` | — |
| `security` | `/pk` | **TTL: On (no default)** |
| `leases` | `/id` | **TTL: On (no default)** |

Los nombres son sobreescribibles vía `Cosmos:Containers:<Clave>`, pero **no hay ninguna razón para cambiarlos**: usar los nombres por defecto reduce la superficie de error.

### 1.5 Secretos canónicos

Definidos en código en `ElTejido.Application/Seguridad/NombresSecretos.cs`. Los nombres deben coincidir **exactamente**:

`llm-key`, `wa-token`, `wa-appsec`, `wa-verify-token`, `jwt-sign`, `otp-salt`, más `diag-key` (nombre libre, referenciado por `Diagnostico__ClaveSecretName`).

### 1.6 Mecanismo de arranque del administrador

Este es el punto que hace posible una instalación limpia con un solo usuario. Los endpoints viven en `ElTejido.Api/Diagnostico/EndpointsSimulacion.cs`, se mapean cuando el entorno es `Development` **o** cuando `Simulacion:Habilitada = true`, y fuera de `Development` exigen el header `X-Diag-Key`:

| Endpoint | Qué hace |
|---|---|
| `POST /diagnostico/simulacion/admin-inicial` | Crea el usuario administrador. Si el contador `seq_usuario` no existe, `ReservarCodigosUsuarioAsync` lo crea con concurrencia optimista. **No hace falta sembrar ningún documento a mano.** |
| `POST /diagnostico/simulacion/otp-admin` | Emite un OTP para ese admin (por defecto `123456`) sin pasar por WhatsApp. |
| `POST /diagnostico/simulacion/webhook-entrante` | Inyecta un mensaje entrante en la misma cola que el webhook real, ya autenticado. |

La página `/simulacion-whatsapp` del portal es el frente visual de estos tres endpoints e incluye un campo para la `X-Diag-Key`.

### 1.7 Hallazgos que cambian la configuración de producción

Tres cosas que la guía de Azure existente no cubre y que este plan corrige:

**a) `Seguridad:PermitirReinicioDatos` viene en `true`.** Está así tanto en `appsettings.json` como en el *default* del código (`GetValue("Seguridad:PermitirReinicioDatos", true)`). Gatea el reinicio masivo de datos de campaña (`POST /api/admin/campanias/{id}/reiniciar-datos`) y la purga de campañas (`POST /api/admin/mantenimiento/purgar-campanias`). **En producción debe quedar explícitamente en `false`.** Es el borrado destructivo más peligroso del sistema y hoy está abierto por omisión.

**b) `ApplicationInsights__ConnectionString` no lo lee nadie.** El proyecto `ElTejido.Api.csproj` no referencia el paquete del SDK de Application Insights. La telemetría llega exclusivamente por el **agente de auto-instrumentación** del App Service, que se activa desde la hoja *Application Insights* del recurso y define por su cuenta `APPLICATIONINSIGHTS_CONNECTION_STRING` y `ApplicationInsightsAgent_EXTENSION_VERSION`. La fila `ApplicationInsights__ConnectionString` de la guía anterior es inerte: se puede omitir sin consecuencias.

**c) Las filas `Llm__Provider`, `Llm__Endpoint` y `Llm__ApiKeySecretName` tampoco se leen.** El proveedor, el modelo, el endpoint y la referencia a la API key viven en el documento **ConfigLLM en Cosmos** y se parametrizan desde el portal administrativo. En Application Settings solo hay que asegurarse de que el secreto exista en Key Vault.

**d) `Auth__SigningKeySecretName` y `Auth__OtpSaltSecretName` tampoco existen.** La clase `OpcionesAuth` solo expone TTL, longitud e intentos del OTP, la ventana de solicitudes y la vigencia de la sesión. Los nombres `jwt-sign` y `otp-salt` están **fijados en código** en `NombresSecretos.cs` y no son configurables. La guía anterior los listaba como si lo fueran; declararlos no rompe nada, pero induce a pensar que se pueden renombrar los secretos, y renombrarlos deja la aplicación sin poder firmar sesiones.

---

## §2. Qué viaja a producción y qué se queda

### 2.1 Viaja — dentro del artefacto de `dotnet publish`

- `ElTejido.Api` con su `wwwroot`, que contiene el SPA Angular compilado en modo `production`.
- Las DLL de `ElTejido.Application`, `ElTejido.Domain` y `ElTejido.Infrastructure`.
- `appsettings.json` con los valores por defecto no sensibles.

Eso es todo. `dotnet publish src/ElTejido.Api/ElTejido.Api.csproj` arrastra únicamente el grafo de dependencias de la Api.

### 2.2 No viaja

| Qué | Por qué |
|---|---|
| `tests/ElTejido.UnitTests`, `tests/ElTejido.IntegrationTests` | No están en el grafo de dependencias de la Api. |
| `src/ElTejido.Calibracion` | Proyecto independiente, no referenciado por la Api. Llama al LLM real y cuesta dinero; por eso el propio CI lo excluye con `Category!=Calibracion`. |
| `Especificaciones/`, `QAS/`, `Guias_Implementacion/`, `Arquitectura/`, `Requeriments/`, `Rubricas/`, `Semillas/`, `Marca/`, `Presentacion/`, `jdocs/`, `Client_partner/` | Documentación del repositorio. Nunca entra al artefacto. |
| `20260808_participantes_campania_prueba_UTF8.csv`, `QAS/datos/*.csv`, `EF-P32-04-01_*.md` | Datos y evidencias de prueba. |
| `node_modules/`, `dist/`, `bin/`, `obj/` | Artefactos de build. |

**Caso especial:** `appsettings.Development.json` sí se copia físicamente al publicar, pero **nunca se lee**, porque `ASPNETCORE_ENVIRONMENT` será `Production`. Es inerte. Aun así conviene verificar que no contenga secretos antes de etiquetar (hoy son 166 bytes, sin credenciales).

### 2.3 Datos que viajan: ninguno

La base de datos de producción arranca vacía. Al terminar la Fase 7 contendrá exactamente **dos documentos**, ambos creados automáticamente por el endpoint de arranque:

1. Un `Usuario` con `rol = admin`, `estado = activo`, `codigoUsuario = 1` y `claveUnicidad = "wa|<número del admin>"`.
2. El contador `{ "id": "seq_usuario", "pk": "secuencia", "type": "Secuencia", "ultimoValor": 1 }`.

No se copia ni un solo documento desde dev/QAS: ni usuarios, ni campañas, ni catálogos, ni conversaciones, ni rúbricas, ni prompts.

### 2.4 Comportamiento que se desactiva solo

Con `ASPNETCORE_ENVIRONMENT = Production`, `Program.cs` deja fuera automáticamente:

- `/diagnostico/error`, `/diagnostico/validacion`, `/diagnostico/limitado` y los endpoints admin de diagnóstico.

Y activa:

- HSTS y redirección forzada a HTTPS.

### 2.5 Comportamiento que hay que desactivar a mano

| Application Setting | Valor en dev | Valor en producción | Por qué |
|---|---|---|---|
| `Seguridad__PermitirReinicioDatos` | `true` | **`false`** | Cierra el borrado masivo de datos de campaña y la purga. |
| `Simulacion__Habilitada` | `true` | **`true`** *(decisión tomada)* | Permite crear el admin y recuperar acceso. Ver los controles compensatorios de §4.1. |

---

## §3. Arquitectura destino y nomenclatura

### 3.1 Recursos a crear: siete, ni uno más

```
                      Internet
                          │
              ┌───────────┴───────────┐
              │  App Service (Linux)  │  app-eltejido-prod-eus2
              │  .NET 8 · B1 · AlwaysOn│  ← SPA Angular + API + webhook
              └───┬────────┬────────┬──┘
    Managed Identity│      │        │
       ┌────────────┘      │        └──────────────┐
       ▼                   ▼                       ▼
┌──────────────┐  ┌─────────────────┐  ┌───────────────────┐
│  Key Vault   │  │   Cosmos DB     │  │  Blob Storage     │
│  (RBAC)      │  │   serverless    │  │  contenedor       │
│  6 secretos  │  │   8 contenedores│  │  markdown         │
└──────────────┘  └─────────────────┘  └───────────────────┘
       │
       └── llm-key ──▶ proveedor LLM externo (fuera de Azure)

  Application Insights ──▶ Log Analytics workspace
```

| # | Recurso | SKU / modo |
|---|---|---|
| 1 | Grupo de recursos | — |
| 2 | Azure Cosmos DB for NoSQL | Serverless, backup continuo 7 días |
| 3 | Cuenta de Storage | Standard LRS, un contenedor privado |
| 4 | Key Vault | Standard, modelo **RBAC** |
| 5 | App Service Plan | Linux **B1 Basic** |
| 6 | App Service (Web App) | .NET 8, Linux, Always On |
| 7 | Application Insights | Workspace-based (crea su Log Analytics) |

**No se crea:** Azure OpenAI (el proveedor LLM es externo), Front Door, CDN, VNet, Private Endpoints, Static Web App, Service Bus, Azure Functions, ni deployment slots (B1 no los soporta).

> **Escalado previsto para la convención.** Está contemplado ampliar el App Service Plan antes del evento. Dos consecuencias que conviene tener presentes: **(a)** el firewall de Cosmos debe ser indiferente al tier — por eso se eligió la Opción A de §6.2.5 y no fijar IPs; **(b)** si se sube a **Standard (S1) o superior**, se habilitan los *deployment slots*, y entonces conviene revisar §17.1, porque el rollback pasa de "redesplegar el tag anterior con 3-5 min de caída" a "swap instantáneo". Escalar el plan **no** requiere redesplegar la aplicación.

### 3.2 Nomenclatura

Convención del Cloud Adoption Framework de Azure: `<abreviatura>-<carga>-<ambiente>-<región>`. East US 2 abrevia `eus2`.

| Recurso | Nombre de producción | Nombre actual en dev (referencia) |
|---|---|---|
| Grupo de recursos | `rg-eltejido-prod-eus2` | `rg-eltejido-mvp` |
| Cosmos DB | `cosmos-eltejido-prod-eus2` | `cosmos-eltejido-mvp` |
| Base de datos | `eltejido` | `eltejido` |
| Storage | `steltejidoprodeus2` | `steltejidomvp` |
| Contenedor de blobs | `markdown` | `markdown` |
| Key Vault | `kv-eltejido-prod-eus2` | `kv-eltejido-mvp` |
| App Service Plan | `asp-eltejido-prod-eus2` | `plan-eltejido-mvp` |
| Web App | `app-eltejido-prod-eus2` | `app-eltejido-mvp` |
| Application Insights | `appi-eltejido-prod-eus2` | `appi-eltejido-mvp` |
| Log Analytics | `log-eltejido-prod-eus2` | — |
| App registration del CD | `gh-eltejido-deploy-prod` | `gh-eltejido-deploy` |
| GitHub Environment | `production` | — |
| Tag de release | `v1.0.0-convencion` | — |

Restricciones que ya están verificadas en estos nombres: `steltejidoprodeus2` tiene 18 caracteres, solo minúsculas y dígitos, sin guiones (el límite del Storage es 24). `kv-eltejido-prod-eus2` tiene 21 caracteres (el límite del Key Vault es 24). Los cuatro nombres de Cosmos, Storage, Key Vault y Web App deben ser **globalmente únicos**: si Azure rechaza alguno, añadir un sufijo corto y **anotarlo en el Anexo A**.

### 3.3 Etiquetas obligatorias

Aplicar estas etiquetas al grupo de recursos y dejar que se hereden. Sirven para filtrar costos y para que nadie confunda un recurso de producción con uno de pruebas.

| Etiqueta | Valor |
|---|---|
| `ambiente` | `produccion` |
| `proyecto` | `eltejido` |
| `evento` | `convencion-2026` |
| `owner` | `<correo del responsable>` |
| `criticidad` | `alta` |

---

## §4. Riesgos y controles

### 4.1 Riesgo crítico: la simulación permanente convierte la clave de diagnóstico en credencial raíz

**Desviación formal.** La condición 5 del acta de congelamiento exige `Simulacion__Habilitada=false` y no usar clave de diagnóstico. La decisión tomada la contradice. **Esto requiere una adenda firmada al acta antes del primer envío.**

**El riesgo, en concreto.** Quien tenga la `X-Diag-Key` puede, sin ninguna credencial adicional:

- Llamar a `admin-inicial` con cualquier número. Como el endpoint busca al usuario existente por número y **reutiliza su `id`**, puede sobrescribir al administrador legítimo.
- Llamar a `otp-admin` indicando el código que él mismo elija, y entrar al portal como administrador.
- Llamar a `webhook-entrante` para inyectar mensajes en nombre de cualquier participante, contaminando los datos reales de la convención con aportes falsos que son indistinguibles de los verdaderos.

No es una vulnerabilidad del sistema: es el diseño previsto de una herramienta de diagnóstico. El problema es exponerla en producción. Si esa clave se filtra, no hay segundo factor que lo detenga.

**Controles compensatorios obligatorios:**

1. Generar la `diag-key` con **al menos 32 bytes aleatorios**, distinta de la de dev/QAS.
2. Guardarla **únicamente en Key Vault**, referenciada por `Diagnostico__ClaveSecretName=diag-key`. **Nunca** usar `Diagnostico__Clave` con el valor en texto plano en Application Settings, donde la ve cualquiera con lectura sobre el App Service.
3. No escribirla jamás en el repositorio, en un chat, en un ticket ni en una captura de pantalla.
4. Crear en Application Insights una **alerta sobre cualquier petición a `/diagnostico/simulacion/*`** dirigida al responsable. En operación normal, después de la Fase 7, ese endpoint no debería recibir tráfico. Cualquier llamada es un evento a investigar.
5. **Rotar la clave** al terminar la parametrización (Fase 8) y otra vez al terminar el evento.
6. Tener listo y ensayado el **procedimiento de apagado en 60 segundos** de §17.3.
7. Revisar periódicamente el contenedor `security` buscando eventos `SimulacionWebhookEntrante`, que es la huella que deja toda inyección.

> **Recomendación técnica, para que quede registrada:** el riesgo baja mucho si la simulación se apaga durante la ventana del evento (desde el primer envío hasta el cierre) y se vuelve a encender solo si hace falta recuperar acceso. El apagado toma 60 segundos y no requiere redesplegar. La decisión es del negocio; el plan soporta ambas.

### 4.2 La *unique key* de `users` es irreversible

`/claveUnicidad` solo se puede declarar **en el momento de crear el contenedor** y no se puede modificar después. Dos formas clásicas de equivocarse:

- **Dejarla vacía.** Es la única barrera que impide dos usuarios activos con el mismo teléfono, cosa que rompe el enrutamiento de WhatsApp.
- **Poner `/pk` por error.** Como todos los usuarios comparten `pk = "usuario"`, el segundo usuario y todos los siguientes fallan con `409 Conflict`. El síntoma es que solo se puede crear un usuario.

La verificación del paso 6.2.4 es obligatoria y hay que hacerla **antes** de cargar la lista real de participantes.

### 4.3 Cambiar un Application Setting reinicia el proceso

Los trabajos en segundo plano (envío masivo, compilación de Markdown, procesamiento del webhook tras el *ack*) corren en **colas en memoria** dentro del mismo proceso. Un reinicio las vacía. Meta reintenta el webhook y el envío masivo es redisparable desde el portal, así que es recuperable, pero **durante la ventana del evento no se toca ningún Application Setting**. Toda la configuración debe quedar cerrada antes del primer envío.

### 4.4 El *webhook override* de Meta no cubre todos los eventos

El override por número aplica a `messages`, `message_echoes`, `calls` y campos relacionados. Los webhooks de **estado de plantillas y de cuenta siempre se entregan a la URL por defecto de la app**, que seguirá siendo la de dev/QAS. Impacto operativo: bajo, porque el sistema no depende de esos eventos en runtime. Impacto práctico: las notificaciones de aprobación o rechazo de plantillas de producción llegarán al ambiente de desarrollo. Documentarlo para que nadie lo interprete como una falla.

### 4.5 B1 no tiene slots: el rollback tiene downtime

Al haber elegido B1, no hay swap. El rollback es redesplegar el tag anterior: entre 3 y 5 minutos con el sitio caído. Aceptado. El procedimiento está en §17.1.

### 4.6 Cosmos serverless es de región única e inmutable

Confirmado East US 2. No hay marcha atrás sin recrear la cuenta y perder los datos. Verificar en la Fase 1 que la región elegida es la correcta **antes** de pulsar Create.

---

## §5. Fase 0 — Prerrequisitos

Ninguna de estas cosas se puede resolver a mitad del despliegue. Confirmarlas todas antes de empezar.

- [ ] Acceso de **Owner** (o Contributor + User Access Administrator) sobre la suscripción de Azure. Contributor a secas **no basta**: hay que asignar roles.
- [ ] Acceso de administrador en **Microsoft Entra ID** para crear un registro de aplicación.
- [ ] Acceso de **administrador del repositorio** en GitHub (para crear Environments, variables y reglas de protección).
- [ ] Acceso al **Business Manager de Meta** con permisos sobre el WABA existente.
- [ ] **Azure CLI** instalado en la máquina de quien ejecuta (`az --version`). Hace falta para el rol de datos de Cosmos, que no siempre aparece en el portal.
- [ ] **Decisión firmada** sobre qué commit se etiqueta: `28c3cb1` o `1215872` (§1.2).
- [ ] **Adenda al acta de congelamiento** que autoriza la simulación permanente (§4.1).
- [ ] **API key del proveedor LLM** de producción, y confirmación de qué proveedor, modelo y endpoint se usarán.
- [ ] **Número de WhatsApp del administrador**, en formato E.164 sin símbolos (ej. `573001112233`).
- [ ] **Segundo número de teléfono** disponible para agregar al WABA, con acceso al SMS o llamada de verificación.
- [ ] **Presupuesto mensual aprobado** para el budget alert.
- [ ] **Correo de destino** para las alertas.

### 5.1 Nota sobre la consola: PowerShell vs. bash

Los comandos de este plan se ejecutan desde Windows, normalmente en **PowerShell 5.1** (el que trae Windows por defecto). PowerShell **no es bash** y tres diferencias rompen los comandos si se copian tal cual:

| Sintaxis bash | Qué pasa en PowerShell 5.1 | Qué usar |
|---|---|---|
| `cmd1 && cmd2` | `The token '&&' is not a valid statement separator in this version` | Dos líneas separadas |
| `\` al final de línea (continuación) | Rompe el comando | Backtick `` ` ``, o escribir todo en **una sola línea** |
| `curl` | Es un **alias de `Invoke-WebRequest`**, con parámetros totalmente distintos | `curl.exe` (viene con Windows 10+) |

En este plan **todos los comandos están escritos en una sola línea y usan `curl.exe`**, de modo que funcionan igual en PowerShell, en CMD y en bash. Si copias comandos de otra documentación de Azure o de Meta, casi siempre vendrán en formato bash: conviértelos antes de ejecutarlos.

> Si prefieres trabajar en bash, tienes **Git Bash** (viene con Git para Windows) o **WSL**. Ahí la sintaxis original de la documentación de Microsoft funciona sin cambios.

---

## §6. Fase 1 — Aprovisionamiento en Azure

> Todo se hace en [portal.azure.com](https://portal.azure.com). Microsoft cambia la interfaz con frecuencia: si un botón no aparece con el texto exacto, usar la barra de búsqueda superior. La secuencia lógica no cambia.

### 6.1 Grupo de recursos

1. Barra de búsqueda superior → **Resource groups** → **+ Create**.
2. **Subscription:** la suscripción de la organización. **Resource group:** `rg-eltejido-prod-eus2`. **Region:** **East US 2**.
3. Pestaña **Tags** → agregar las cinco etiquetas de §3.3.
4. **Review + create** → **Create**.

Todos los recursos siguientes van dentro de este grupo y en esta misma región.

### 6.2 Cosmos DB

1. Búsqueda → **Azure Cosmos DB** → **+ Create** → tarjeta **Azure Cosmos DB for NoSQL** → **Create**.
2. **Basics:** Resource group `rg-eltejido-prod-eus2`, Account Name `cosmos-eltejido-prod-eus2`, Location **East US 2**, **Capacity mode: Serverless**.
3. Pestaña **Networking** → **Public network access: All networks**, o bien *Selected networks* **marcando obligatoriamente la casilla "Accept connections from within public Azure datacenters"**. Ver §6.2.5.
4. Pestaña **Backup Policy** → seleccionar **Continuous (7 days)**. *(Es la decisión 9. En serverless, si esta pestaña no está disponible al crear, se cambia después desde el recurso → **Backup & Restore**.)*
5. **Review + create** → **Create**. Tarda unos minutos.
6. Ir al recurso → **Settings → Keys** → copiar el campo **URI** (`https://cosmos-eltejido-prod-eus2.documents.azure.com:443/`). Va al Anexo A como `Cosmos:AccountEndpoint`. **No copiar las claves:** la aplicación usa Managed Identity.

#### 6.2.5 ⚠️ El firewall de IP de Cosmos y el App Service

Si el filtro de IP queda activo (`ipRules` con entradas), **el App Service no puede conectarse**, porque sale por direcciones que no están en la lista. Es una trampa doble:

- Cosmos responde **HTTP 403**, el mismo código que devuelve por falta de rol de datos.
- El readiness lo reporta como `"Acceso denegado (HTTP 403). Revisa el rol de datos de la identidad administrada."` — un mensaje **engañoso** en este caso, que envía a diagnosticar RBAC cuando el problema es de red.

Comprobar el estado real:

```powershell
az cosmosdb show --name cosmos-eltejido-prod-eus2 --resource-group rg-eltejido-prod-eus2 --query "{publicNetworkAccess:publicNetworkAccess, ipRules:ipRules, vnetFilter:isVirtualNetworkFilterEnabled}" -o json
```

Si `ipRules` sale **vacío**, no hay filtro y no hay nada que hacer. Si trae entradas, hay tres caminos, y **la elección depende de si el App Service Plan va a escalarse**.

##### Opción A — Permitir los datacenters de Azure ✅ *(elegida para este despliegue)*

Cosmos → **Networking** → marcar **"Accept connections from within public Azure datacenters"** → **Save**. Eso añade la IP especial `0.0.0.0` a la lista.

Equivalente por CLI, conservando las IPs existentes:

```powershell
$rg = "rg-eltejido-prod-eus2"; $cosmos = "cosmos-eltejido-prod-eus2"
$actuales = az cosmosdb show -n $cosmos -g $rg --query "ipRules[].ipAddressOrRange" -o tsv
$todas = ((@("0.0.0.0") + $actuales) | Select-Object -Unique) -join ","
az cosmosdb update -n $cosmos -g $rg --ip-range-filter $todas
```

**Por qué esta y no la B:** está previsto **ampliar el App Service Plan para la convención**. Un cambio de tier puede mover la aplicación de unidad de despliegue y **cambiar sus IPs de salida**, con lo que una lista fijada dejaría de servir justo en el momento de mayor exposición, y el síntoma sería el 403 engañoso de arriba. La opción A es indiferente al tier: se escala sin tocar nada.

**Lo que se acepta a cambio:** Microsoft advierte que `0.0.0.0` admite peticiones desde **cualquier suscripción de Azure**, incluidas las de otros clientes, de modo que el firewall de IP queda prácticamente decorativo. El control real pasa a ser íntegramente **la autenticación por AAD**: quien llegue por red todavía necesita un token con un rol de datos sobre *esta* cuenta, y ese rol solo lo tiene la identidad administrada del App Service. Es una postura defendible para el MVP, pero conviene que quede escrita en la adenda.

##### Opción B — Fijar las IPs de salida del App Service ❌ *(descartada aquí)*

Más restrictiva, pero **frágil ante un cambio de tier**:

```powershell
$rg = "rg-eltejido-prod-eus2"; $app = "app-eltejido-prod-eus2"; $cosmos = "cosmos-eltejido-prod-eus2"
$actuales = az cosmosdb show -n $cosmos -g $rg --query "ipRules[].ipAddressOrRange" -o tsv
$salida = (az webapp show -n $app -g $rg --query possibleOutboundIpAddresses -o tsv).Split(",")
$todas = (($actuales + $salida) | Select-Object -Unique) -join ","
az cosmosdb update -n $cosmos -g $rg --ip-range-filter $todas
```

Solo tiene sentido si el plan **no** se va a escalar. Escalar *hacia fuera* (más instancias del mismo tier) es seguro, porque `possibleOutboundIpAddresses` ya cubre el conjunto completo; lo que rompe es escalar *hacia arriba* (B1 → S1 → P1v3). Si algún día se elige este camino, hay que reejecutar el script después de **cada** cambio de tier.

##### Opción C — Integración con VNet y service endpoint *(endurecimiento post-evento)*

El camino realmente seguro y a la vez estable frente al escalado: el tráfico de salida se enruta por una subred y Cosmos autoriza por **regla de red virtual**, no por IP. Sobrevive a cualquier cambio de tier.

No se adopta ahora por tres razones: añade recursos (VNet y subred delegada) al inventario mínimo; exige enrutar todo el tráfico saliente por la VNet, lo que también afecta a las llamadas al proveedor LLM y a Graph API de Meta; y son piezas nuevas a menos de dos semanas del evento. Queda como **deuda de endurecimiento post-convención**, junto con `DT-P32-05` y `DT-QA-03`.

Los cambios de firewall tardan unos **5 minutos** en propagar. No hace falta reiniciar el App Service.

#### 6.2.1 Crear la base de datos

En el recurso → **Data Explorer** → **New Database** → **Database id:** `eltejido` → **OK**. En serverless no se asigna throughput.

#### 6.2.2 Crear el contenedor `users` — el paso delicado

Hay que hacerlo con cuidado porque la *unique key* no se puede cambiar después.

1. **Data Explorer** → **New Container**.
2. **Database id:** *Use existing* → `eltejido`.
3. **Container id:** `users`.
4. **Partition key:** `/pk`
5. Expandir las opciones avanzadas y localizar **Unique keys** → **+ Add unique key** → escribir exactamente:
   ```
   /claveUnicidad
   ```
6. Verificar antes de pulsar OK: partition key `/pk`, unique key `/claveUnicidad`. **No es `/pk` la unique key. No es `/whatsappNormalizado`.**
7. **OK**.

#### 6.2.3 Crear los siete contenedores restantes

Uno por uno con **New Container**, siempre con *Use existing* → `eltejido`, y **sin unique keys**:

| Container id | Partition key |
|---|---|
| `campaigns` | `/id` |
| `participants` | `/campaniaId` |
| `conversations` | `/campaniaId` |
| `responses` | `/campaniaId` |
| `config` | `/pk` |
| `security` | `/pk` |
| `leases` | `/id` |

Después, activar TTL en dos de ellos. Para `security` y para `leases`: abrir el contenedor → **Settings** (o *Scale & Settings*) → **Time to Live** → **On (no default)** → **Save**. Esto permite que cada documento defina su propio `ttl`, que es lo que hace la aplicación con los OTP y con el deduplicado del webhook.

#### 6.2.4 Verificación obligatoria de la unique key

Esta prueba se ejecuta en la **Fase 7**, cuando ya exista el usuario administrador y se pueda entrar al portal. Se enuncia aquí para que no se olvide:

> Intentar crear desde el portal administrativo un **segundo usuario activo con el mismo número** que el administrador. El sistema debe responder **`409 Conflict`**.
>
> Si lo crea, la unique key quedó mal y **hay que borrar y recrear el contenedor `users`** desde §6.2.2. Como la base está vacía, no hay migración de datos: es un retroceso de cinco minutos si se detecta ahora, o un incidente serio si se detecta con 200 participantes cargados.

### 6.3 Cuenta de Storage

1. Búsqueda → **Storage accounts** → **+ Create**.
2. **Resource group:** `rg-eltejido-prod-eus2`. **Storage account name:** `steltejidoprodeus2`. **Region:** East US 2. **Primary service:** Azure Blob Storage. **Performance:** Standard. **Redundancy:** **LRS**.
3. **Review + create** → **Create**.
4. Ir al recurso → **Data storage → Containers** → **+ Container** → **Name:** `markdown` → **Anonymous access level: Private (no anonymous access)** → **Create**.
5. **Settings → Endpoints** → copiar **Blob service** (`https://steltejidoprodeus2.blob.core.windows.net/`) al Anexo A como `Blob:AccountUrl`.

### 6.4 Application Insights

1. Búsqueda → **Application Insights** → **+ Create**.
2. **Resource group:** `rg-eltejido-prod-eus2`. **Name:** `appi-eltejido-prod-eus2`. **Region:** East US 2. **Resource Mode:** Workspace-based. Si pide un workspace de Log Analytics, dejar que cree uno y renombrarlo `log-eltejido-prod-eus2`.
3. **Review + create** → **Create**.

No hace falta copiar la connection string: el agente del App Service la inyecta solo (§1.7b).

### 6.5 Key Vault

1. Búsqueda → **Key vaults** → **+ Create**.
2. **Resource group:** `rg-eltejido-prod-eus2`. **Key vault name:** `kv-eltejido-prod-eus2`. **Region:** East US 2. **Pricing tier:** Standard.
3. **Permission model:** **Azure role-based access control (RBAC)**.
4. En **Recovery options**, dejar *soft delete* activo (viene por defecto) con retención de 90 días. Es la red de seguridad si alguien borra un secreto por error.
5. **Review + create** → **Create**.
6. **Darse acceso a uno mismo para poder cargar secretos:** en el Key Vault → **Access control (IAM)** → **+ Add → Add role assignment** → **Role: Key Vault Secrets Officer** → **Members:** tu propio usuario → **Review + assign**. Esperar 1 o 2 minutos a que propague.
7. **Overview** → copiar **Vault URI** (`https://kv-eltejido-prod-eus2.vault.azure.net/`) al Anexo A.

### 6.6 App Service Plan y Web App

1. Búsqueda → **App Services** → **+ Create → Web App**.
2. **Basics:**
   - **Resource group:** `rg-eltejido-prod-eus2`
   - **Name:** `app-eltejido-prod-eus2`
   - **Publish:** Code
   - **Runtime stack:** **.NET 8 (LTS)**
   - **Operating System:** **Linux**
   - **Region:** East US 2
   - **Pricing plan:** **Create new** → nombre `asp-eltejido-prod-eus2` → SKU **Basic B1**
3. Pestaña **Monitoring** → **Enable Application Insights: Yes** → seleccionar `appi-eltejido-prod-eus2`.
4. **Review + create** → **Create**.
5. Ir al App Service → **Settings → Configuration → General settings** → **Always On: On** → **Save**. *(Sin esto el proceso se duerme y el webhook de WhatsApp falla de forma intermitente.)*
6. En la misma pantalla, verificar **HTTPS Only: On** y **Minimum TLS Version: 1.2**.
7. **Overview** → copiar el **Default domain** al Anexo A.

> ⚠️ **El hostname no es necesariamente `app-eltejido-prod-eus2.azurewebsites.net`.** Los App Service creados recientemente reciben un dominio único con sufijo aleatorio y región, por ejemplo `app-eltejido-prod-eus2-a1b2c3d4.eastus2-01.azurewebsites.net`. **Copiar el valor real de Overview → Default domain** y usar ese en todas las URLs, incluida la del webhook de Meta.

---

## §7. Fase 2 — Identidad administrada y permisos

Este es el corazón del modelo de seguridad: la aplicación no guarda ninguna contraseña de infraestructura. Se autentica ante Key Vault, Cosmos y Blob con su propia identidad de Azure.

### 7.1 Activar la identidad

App Service → **Settings → Identity** → pestaña **System assigned** → **Status: On** → **Save** → confirmar.

**Copiar el `Object (principal) ID`** que aparece. Se necesita en el paso 7.3 y es fácil perderlo de vista.

### 7.2 Key Vault y Blob: roles desde el portal

**a) Key Vault — lectura de secretos**

1. `kv-eltejido-prod-eus2` → **Access control (IAM) → + Add → Add role assignment**.
2. **Role:** **Key Vault Secrets User** *(solo lectura: la aplicación nunca escribe secretos)*.
3. **Members → Assign access to: Managed identity → + Select members →** tipo **App Service** → `app-eltejido-prod-eus2` → **Select → Review + assign**.

**b) Blob Storage — lectura y escritura de blobs**

1. `steltejidoprodeus2` → **Access control (IAM) → + Add → Add role assignment**.
2. **Role:** **Storage Blob Data Contributor**.
3. **Members:** la identidad administrada de `app-eltejido-prod-eus2` → **Review + assign**.

### 7.3 Cosmos: rol del plano de datos, por CLI

El acceso a **datos** de Cosmos usa roles del plano de datos que normalmente **no aparecen** en la pestaña IAM del portal. El rol de plano de control *DocumentDB Account Contributor* **no sirve** para leer y escribir documentos. Se asigna por Azure CLI:

```bash
az login

az cosmosdb sql role assignment create --account-name cosmos-eltejido-prod-eus2 --resource-group rg-eltejido-prod-eus2 --role-definition-id 00000000-0000-0000-0000-000000000002 --principal-id <OBJECT-ID-DE-LA-IDENTIDAD-DEL-APP-SERVICE> --scope "/"
```

*(Una sola línea, a propósito: ver §5.1.)*

`00000000-0000-0000-0000-000000000002` es el identificador fijo del rol integrado **Cosmos DB Built-in Data Contributor**; se escribe tal cual.

> ⚠️ **`--principal-id` es el Object ID, no el Client ID.** Es el error más frecuente de este paso, y falla en silencio: el comando se ejecuta sin protestar (Cosmos no valida que el GUID corresponda a un principal existente) y el 403 sigue apareciendo. Para obtener el valor correcto sin ambigüedad:
>
> ```powershell
> az webapp identity show --name app-eltejido-prod-eus2 --resource-group rg-eltejido-prod-eus2 --query principalId -o tsv
> ```

**Propagación.** La asignación tarda unos minutos en surtir efecto. Si tras crearla `/health/ready` sigue devolviendo `cosmos: error`, espera 3 a 5 minutos y reintenta; si persiste, reinicia el App Service (**Overview → Restart**) para forzar que el cliente renueve su token.

Verificar que quedó:

```bash
az cosmosdb sql role assignment list --account-name cosmos-eltejido-prod-eus2 --resource-group rg-eltejido-prod-eus2 -o table
```

La salida debe listar una asignación con `RoleDefinitionId` terminado en `...000000000002` y el `PrincipalId` de la identidad del App Service. **Si la tabla sale vacía, la asignación no existe** por más que el comando de creación pareciera haber funcionado.

> Si este paso se omite, la aplicación arranca y `/health` responde 200, pero **todo lo que toque la base falla con 403**. Es el error más común de este despliegue. El síntoma en `/health/ready` es inconfundible, y se distingue de un problema de identidad porque **el resto sigue en verde**:
>
> ```json
> {"componente":"blob","estado":"ok", ...}
> {"componente":"cosmos","estado":"error",
>  "detalle":"Acceso denegado (HTTP 403). Revisa el rol de datos de la identidad administrada."}
> ```
>
> Si Blob y los secretos están en `ok` y solo Cosmos falla, la identidad administrada funciona correctamente.
>
> **Pero atención: ese 403 tiene dos causas posibles y el mensaje no las distingue.** Antes de dar por hecho que falta el rol, verifica el **firewall de IP de la cuenta** (§6.2.5): Cosmos devuelve el mismo 403 cuando bloquea por red. Orden de diagnóstico recomendado:
>
> 1. `az cosmosdb sql role assignment list ...` → ¿existe la asignación, con rol `...002`, scope de cuenta y el `principalId` correcto?
> 2. Si la asignación es correcta, **el problema es de red**: `az cosmosdb show ... --query ipRules` → si trae entradas, aplica §6.2.5.

---

## §8. Fase 3 — Secretos en Key Vault

### 8.1 Generar los tres secretos que se inventan

En PowerShell:

```powershell
# jwt-sign — firma de las sesiones administrativas
[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))

# otp-salt — sal del hash de los OTP
[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))

# diag-key — clave de diagnóstico y de la simulación (§4.1)
[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))
```

O en bash: `openssl rand -base64 48`

**Los tres valores de producción deben ser distintos de los de dev/QAS.** Reutilizarlos anula el aislamiento entre ambientes.

> ⚠️ **`wa-verify-token` debe ser URL-safe: solo letras y dígitos.** No sirve una cadena Base64. Meta envía este token a tu webhook **en la query string** (`?hub.verify_token=...`), y ahí `+` se decodifica como espacio, mientras que `/` y `=` también se maltratan. El valor que llega a la aplicación deja de coincidir con el de Key Vault y la verificación falla con `403 Verificación rechazada` — o, si Meta agota su timeout de 6 s antes, con el críptico `(#2200) Callback verification failed ... curl_errno = 28`.
>
> Genera este en particular así:
>
> ```powershell
> -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 48 | ForEach-Object { [char]$_ })
> ```
>
> Los demás secretos **sí** pueden ser Base64: `diag-key` viaja en un header (`X-Diag-Key`), y `jwt-sign` y `otp-salt` nunca salen del servidor.
>
> Si hay que cambiarlo después, recuerda que la aplicación cachea las lecturas exitosas de secretos durante 5 minutos: reinicia el App Service o el cambio no surte efecto de inmediato.

### 8.2 Cargar los secretos

Para cada uno: Key Vault → **Objects → Secrets → + Generate/Import** → **Name** exacto → **Secret value** → **Create**.

| Nombre exacto | Contenido | Cuándo se puede cargar |
|---|---|---|
| `jwt-sign` | Cadena aleatoria generada arriba | Ahora |
| `otp-salt` | Cadena aleatoria generada arriba | Ahora |
| `diag-key` | Cadena aleatoria generada arriba | Ahora |
| `wa-verify-token` | Cadena **solo alfanumérica** para el webhook (ver aviso abajo) | Ahora |
| `llm-key` | API key del proveedor LLM de producción | Ahora |
| `wa-token` | Access token de WhatsApp Cloud API | Fase 9 |
| `wa-appsec` | App Secret de la app de Meta | Fase 9 |

Los nombres se validan contra `NombresSecretos.cs`: una sola letra distinta y `/health/ready` reportará `secreto:<nombre>` como `faltante`.

> **Nota sobre `wa-appsec`:** al ser el mismo WABA y la misma app de Meta que dev/QAS, el App Secret es **el mismo valor** que ya está en el Key Vault de desarrollo. Es la única excepción a la regla de no reutilizar secretos entre ambientes, y es inevitable: lo determina Meta, no nosotros.

---

## §9. Fase 4 — Application Settings

App Service → **Settings → Environment variables** (o *Configuration → Application settings*) → **+ Add** para cada fila → **Apply** al final.

El doble guion bajo `__` es la forma de anidar secciones de configuración de .NET en variables de entorno: `Cosmos__AccountEndpoint` equivale a `Cosmos:AccountEndpoint`.

| Name | Value | Nota |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Apaga los endpoints de diagnóstico y enciende HSTS |
| `Cosmos__AccountEndpoint` | `https://cosmos-eltejido-prod-eus2.documents.azure.com:443/` | Del paso 6.2.5 |
| `Cosmos__DatabaseName` | `eltejido` | |
| `Blob__AccountUrl` | `https://steltejidoprodeus2.blob.core.windows.net/` | Del paso 6.3.5 |
| `Blob__ContainerName` | `markdown` | |
| `KeyVault__Uri` | `https://kv-eltejido-prod-eus2.vault.azure.net/` | Del paso 6.5.7 |
| `Seguridad__PermitirReinicioDatos` | `false` | **Crítico.** Cierra el borrado masivo (§1.7a) |
| `Simulacion__Habilitada` | `true` | Decisión 3. Controles en §4.1 |
| `Diagnostico__ClaveSecretName` | `diag-key` | **Nunca** usar `Diagnostico__Clave` |
| `WhatsApp__GraphApiBaseUrl` | `https://graph.facebook.com/v21.0` | La versión que indique la guía de Meta vigente |
| `WhatsApp__PhoneNumberId` | *(Fase 9)* | El Phone Number ID del **segundo número** |
| `Conversacion__CatalogoTextosHabilitado` | `true` | **Obligatorio en un despliegue bilingüe.** Ver la nota de abajo |

Son **doce** settings. Opcionalmente se pueden añadir `WhatsApp__VerifyTokenSecretName = wa-verify-token`, `WhatsApp__AppSecretSecretName = wa-appsec` y `WhatsApp__AccessTokenSecretName = wa-token`: existen de verdad en `OpcionesWhatsApp`, pero **sus valores por defecto en el código ya son exactamente esos**, así que declararlos solo aporta explicitud.

> ⚠️ **`Conversacion__CatalogoTextosHabilitado` no es opcional aquí.** Nace apagado, y con el gate en `false` el runtime **no consulta Cosmos**: devuelve la semilla española compilada en el binario e **ignora el idioma `en` por completo**. Es decir, todo el catálogo bilingüe de la Fase 8 quedaría sin usarse y los participantes anglófonos recibirían texto en español. No afecta la validación de activación de la campaña —esa exige catálogos activos por idioma con gate o sin él— pero sí determina qué texto se envía. Enciéndelo **antes** de crear la campaña, para no gastar un reinicio a mitad de la parametrización.

> **Pendientes de la Fase 9 (mapeo de plantillas Meta).** `plantillaRef` en la campaña es solo un **alias lógico**; el nombre físico de la plantilla aprobada por Meta vive en App Settings bajo `WhatsApp__PlantillaEnvioInicial__*`, único puente entre el idioma interno (`es`/`en`) y el código de Meta (`es_CO`, `en_US`). Se completan al tener el Phone Number ID.

> **Application Insights.** Enciéndelo desde App Service → **Settings → Application Insights**, no a mano: el agente inyecta por su cuenta `APPLICATIONINSIGHTS_CONNECTION_STRING` y `ApplicationInsightsAgent_EXTENSION_VERSION`. **Si esos dos settings no aparecen en la lista, la telemetría no está conectada** y las alertas de la Fase 10 no tendrían datos que vigilar — incluida la del endpoint de simulación, que es el control compensatorio de §4.1.

**No configurar — settings inertes que el código nunca lee:**

- `Auth__SigningKeySecretName` y `Auth__OtpSaltSecretName` — `OpcionesAuth` no tiene esas propiedades (solo TTL, longitud e intentos del OTP y vigencia de sesión). El código toma `jwt-sign` y `otp-salt` directamente de las constantes de `NombresSecretos`. Ponerlos da la falsa impresión de que el nombre del secreto es configurable: **no lo es**.
- `ApplicationInsights__ConnectionString` — la telemetría viene del agente del App Service (§1.7b).
- `Llm__Provider`, `Llm__Endpoint`, `Llm__ApiKeySecretName` — ConfigLLM se parametriza desde el portal (§1.7c).

**No configurar — settings peligrosos:**

- `Cosmos__AccountKey` — si existe, la aplicación usa la clave en vez de Managed Identity y se pierde todo el modelo de seguridad. **Debe estar ausente.**
- `Diagnostico__Clave` — expondría la clave raíz en texto plano en una pantalla que ve cualquiera con lectura sobre el App Service.
- `Persistencia__Modo` — si se pone en `Memoria`, la aplicación arranca con repositorios volátiles y **pierde todo al reiniciar**. Debe estar ausente.

Al pulsar **Apply**, el App Service reinicia. Es normal.

---

## §10. Fase 5 — CI/CD hacia producción

### 10.1 Registrar la aplicación en Entra ID

Una identidad **distinta** de la de dev/QAS. Si la de desarrollo se compromete, no debe poder tocar producción.

1. Búsqueda → **Microsoft Entra ID** → **App registrations** → **+ New registration**.
2. **Name:** `gh-eltejido-deploy-prod`. **Supported account types:** *Accounts in this organizational directory only*. → **Register**.
3. En **Overview**, copiar **Application (client) ID** y **Directory (tenant) ID** al Anexo A.

### 10.2 Credencial federada de tipo Environment

Este es el paso donde más gente se equivoca. El *subject* del token OIDC tiene que coincidir **exactamente** con lo que GitHub va a enviar.

1. En `gh-eltejido-deploy-prod` → **Certificates & secrets → Federated credentials → + Add credential**.
2. **Credential scenario:** **GitHub Actions deploying Azure resources**.
3. **Organization:** la organización o usuario de GitHub. **Repository:** el repositorio de El Tejido.
4. **Entity type:** **Environment** *(no Branch, no Tag)*.
5. **Environment name:** `production` — en minúsculas, idéntico al nombre del GitHub Environment que se creará en 10.4.
6. **Audience:** `api://AzureADTokenExchange` (valor por defecto). **Name:** `gh-env-production`. → **Add**.

El subject resultante es `repo:<ORG>/<REPO>:environment:production`. Si no coincide con lo que envía GitHub, `azure/login` falla con **`AADSTS700213: No matching federated identity record found`**.

#### 10.2.1 ⚠️ Discordancia de formato de subject (legacy vs. inmutable)

Existen **dos** formatos de subject y el match entre GitHub y Entra es **literal**:

| Formato | Aspecto |
|---|---|
| **Legacy** (por nombre) | `repo:aliadoti/GHT_El_Tejido:environment:production` |
| **Inmutable** (por ID) | `repo:aliadoti@<owner-id>/GHT_El_Tejido@<repo-id>:environment:production` |

GitHub emite el inmutable solo en repositorios **creados o transferidos después del 15-jul-2026**, o que se hayan adherido explícitamente. `aliadoti/GHT_El_Tejido` es anterior, así que **emite el formato legacy**.

**El problema:** el asistente del portal de Azure ya genera el formato **inmutable** por defecto. Si se crea la credencial con el asistente sin más, queda con un subject que este repositorio nunca va a presentar, y el login falla con `AADSTS700213` aunque todo lo demás esté bien.

**Solución — crear la credencial con el subject legacy.** El asistente de "GitHub Actions" no permite editar el subject a mano, así que se usa Azure CLI, que es determinista:

Crear un archivo `fic.json`:

```json
{
  "name": "gh-env-production-legacy",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:aliadoti/GHT_El_Tejido:environment:production",
  "audiences": ["api://AzureADTokenExchange"]
}
```

Y ejecutar:

```bash
az ad app federated-credential create --id <APPLICATION-CLIENT-ID-de-gh-eltejido-deploy-prod> --parameters "@fic.json"
```

*(Alternativa por portal: **Federated credentials → + Add credential → Credential scenario: "Other issuer"**, que sí deja escribir los tres campos a mano con los valores del JSON.)*

**No hace falta borrar la credencial inmutable.** Un app registration admite varias credenciales federadas (hasta 20) y Entra evalúa todas. La inmutable simplemente no hará match hoy, y quedará lista para el día en que GitHub migre este repositorio. Es la estrategia de convivencia que la propia documentación de migración recomienda.

> 🚫 **Lo que NO hay que hacer:** adherir el repositorio a los *immutable subject claims* desde GitHub para que coincida con la credencial existente. Eso cambiaría el subject de **todos** los workflows, incluida la credencial Branch = `main` de `gh-eltejido-deploy`, y **rompería los despliegues a dev/QAS**.

Verificar cómo quedó:

```bash
az ad app federated-credential list --id <APPLICATION-CLIENT-ID-de-gh-eltejido-deploy-prod> -o table
```

### 10.3 Permisos en Azure, acotados al RG de producción

1. **Grupo de recursos** `rg-eltejido-prod-eus2` → **Access control (IAM) → + Add → Add role assignment**.
2. **Role:** **Contributor**. *(Alternativa más estrecha: **Website Contributor**, suficiente si solo va a publicar la web. Contributor sobre el RG es más cómodo si hay que ajustar settings desde el pipeline.)*
3. **Members → Assign access to: User, group, or service principal →** buscar `gh-eltejido-deploy-prod` → **Review + assign**.

**El ámbito es el grupo de recursos, no la suscripción.** Esa es toda la diferencia entre "el pipeline de producción puede publicar la web de producción" y "el pipeline de producción puede hacer cualquier cosa en cualquier ambiente".

Esperar 1 o 2 minutos a que propague. Si se omite este paso, `azure/login` autentica pero el deploy falla con `No subscriptions found for <client-id>`.

### 10.4 Configurar el GitHub Environment `production`

> **Ya existe un Environment llamado `production` en el repositorio.** No hay que crearlo: hay que **configurarlo**, porque hoy está vacío y eso lo vuelve peligroso. Ver §10.4.0 antes de tocar nada.

#### 10.4.0 De dónde salió y por qué importa

GitHub crea un Environment automáticamente la primera vez que un workflow lo nombra. El commit `16b8aef` introdujo `deploy.yml` **con** `environment: production`, y GitHub lo creó solo. El commit `dbb4226` (14-jun-2026) quitó esa línea porque el login OIDC fallaba con `AADSTS700213`, y el Environment quedó **huérfano**: existe, pero ningún workflow lo referencia.

Dos cosas que conviene tener claras:

- **Ese Environment nunca apuntó a producción.** Cuando estaba activo, el destino seguía siendo `vars.AZURE_WEBAPP_NAME` = `app-eltejido-mvp`, es decir dev/QAS. Solo le puso la etiqueta "production" a despliegues que iban a desarrollo. Su historial de despliegues es engañoso y no debe leerse como evidencia de nada.
- **El Environment no decide el destino.** Lo decide `AZURE_WEBAPP_NAME`. El Environment aporta el gate de aprobación, el subject OIDC y un ámbito de variables. Nada más.

Estado auditado a 2026-08-17:

| Qué | Estado | Consecuencia |
|---|---|---|
| `production` → Environment variables | **Ninguna** | `vars.AZURE_WEBAPP_NAME` caería al valor de repositorio |
| `production` → Environment secrets | Ninguno | — |
| `production` → Required reviewers | **Ninguno** | Sin gate de aprobación |
| `production` → Deployment branches | **No restrictions** | Cualquier rama podría desplegar |
| Variable de repositorio `AZURE_WEBAPP_NAME` | `app-eltejido-mvp` | Es el App Service de **dev/QAS** |
| `gh-eltejido-deploy` → Federated credentials | Solo tipo **Branch = main** | Aislamiento intacto: la identidad de dev **no** puede autenticarse como `environment:production` |

> ⚠️ **El fallo silencioso que esto habría causado.** Con el Environment vacío, `deploy-prod.yml` habría evaluado `vars.AZURE_WEBAPP_NAME`, no lo habría encontrado en el Environment, y habría caído a la variable de repositorio: `app-eltejido-mvp`. El resultado sería publicar el artefacto congelado de producción **encima del ambiente de desarrollo**, con el gate de aprobación cumplido y el smoke test en verde, porque `/health` también responde 200 en dev. Todo correcto en apariencia, destino equivocado. La guarda del paso 4 del Anexo B existe precisamente para que esto sea imposible.

**Decisión tomada:** se **reutiliza** el Environment existente y se configura por completo. Al estar vacío, borrarlo y recrearlo no aportaría nada salvo limpiar el historial de despliegues; si prefieres esa limpieza, borrarlo es seguro (ningún workflow lo referencia) y al recrearlo con el mismo nombre el subject OIDC no cambia.

#### 10.4.1 Configuración

Repositorio → **Settings → Environments → `production`**:

1. **Required reviewers:** activar y agregar a las personas autorizadas a aprobar un despliegue a producción. Con una basta; dos es mejor.
2. **Wait timer:** dejar en 0.
3. **Deployment branches and tags:** cambiar de *No restrictions* a **Selected branches and tags** → **Add rule** → tipo **Tag** → patrón `v*`.
   Sin esto, cualquier rama puede desplegar al environment de producción.
4. **Environment variables** (la sección *Environment variables*, **no** *Secrets*) → agregar las tres:

| Variable | Valor | Por qué es obligatoria |
|---|---|---|
| `AZURE_CLIENT_ID` | Application (client) ID de `gh-eltejido-deploy-prod` | Si falta, cae a la de repo, que es la identidad de **dev** y no tiene permisos sobre el RG de producción |
| `AZURE_WEBAPP_NAME` | `app-eltejido-prod-eus2` | Si falta, cae a `app-eltejido-mvp` y **despliega a dev/QAS** |
| `AZURE_RESOURCE_GROUP` | `rg-eltejido-prod-eus2` | La usa el fallback del smoke test y la guarda de destino |

`AZURE_TENANT_ID` y `AZURE_SUBSCRIPTION_ID` ya existen a nivel de repositorio y son las mismas para ambos ambientes; no hace falta duplicarlas.

**Las variables de Environment tienen precedencia sobre las de repositorio.** Esa es exactamente la propiedad que hace funcionar todo este esquema: el mismo nombre de variable resuelve a un valor distinto según el ambiente. Y es también la que convierte una variable olvidada en un despliegue al ambiente equivocado.

#### 10.4.2 Verificación

Después de guardar, la pantalla del Environment `production` debe mostrar:

- [ ] Al menos un **Required reviewer**
- [ ] **Deployment branches and tags:** una regla de tag `v*` (ya no dice *No restrictions*)
- [ ] **Environment variables:** exactamente `AZURE_CLIENT_ID`, `AZURE_WEBAPP_NAME` y `AZURE_RESOURCE_GROUP`, con los valores de producción

Si alguna de las tres variables falta, la guarda del workflow abortará el despliegue antes de tocar Azure. Eso es lo deseado, pero es mejor no llegar ahí.

### 10.5 Agregar el workflow

Crear `.github/workflows/deploy-prod.yml` con el contenido del **Anexo B** y **commitearlo y empujarlo a `main`**.

> ⚠️ **No basta con crear el archivo en disco.** GitHub Actions lee los workflows **desde el ref que dispara la ejecución**. Si el archivo está sin commitear —o commiteado pero no empujado— el tag no encuentra el workflow y **no se ejecuta nada**: ni error, ni run fallido, ni aviso. Simplemente no aparece nada en la pestaña *Actions*. Es el síntoma más desconcertante de este paso.

```bash
git add .github/workflows/deploy-prod.yml
git commit -m "ci(deploy): workflow de despliegue a produccion por tag con gate de aprobacion"
git push origin main
```

Ese push dispara `ci.yml` y `deploy.yml`, es decir, **publica el estado actual de `main` en QAS**. Es el comportamiento normal y no afecta a producción.

`deploy.yml` **no se toca**. Como está configurado con `on: push: branches: [main]`, un tag no lo dispara.

### 10.6 Prueba en vacío antes de etiquetar

> **Prerrequisito:** `deploy-prod.yml` debe estar ya en `origin/main` (§10.5). Verifícalo antes de etiquetar:
> ```bash
> git ls-tree -r --name-only origin/main .github/workflows/
> ```
> Si `deploy-prod.yml` no aparece en esa lista, el tag no ejecutará nada.

Antes de crear el tag de release, conviene verificar que la cadena OIDC funciona de punta a punta. **La prueba se hace empujando un tag de prueba, no con Run workflow desde `main`:**

```bash
git tag v0.0.1-test <cualquier-commit>
git push origin v0.0.1-test
```

> ⚠️ **Por qué no sirve *Run workflow* desde `main`.** La regla de protección del paso 10.4.1 restringe el Environment a tags `v*`. Un `workflow_dispatch` lanzado desde la rama `main` se detiene con *"Branch is not allowed to deploy to production due to environment protection rules"* antes de ejecutar nada. Si se quiere usar el disparo manual, hay que seleccionar **un tag `v*` en el desplegable "Use workflow from"**, no una rama.

Qué debe pasar, en orden: la guarda de destino imprime `Destino verificado: app-eltejido-prod-eus2 en rg-eltejido-prod-eus2`, `azure/login` obtiene el token, el deploy publica y el smoke test responde OK. Si la guarda aborta, faltan variables en el Environment (§10.4.1). Si falla `azure/login` con `AADSTS700213`, el *Environment name* de la credencial federada no coincide con `production` (§10.2).

#### Si falla con `AADSTS700213`

El mensaje incluye el subject que GitHub presentó. Si dice `repo:<org>/<repo>:environment:production`, GitHub hizo su parte bien y el problema está en Azure. Dos causas, en orden de frecuencia:

**a) El Client ID usado no es el de producción.** Si `AZURE_CLIENT_ID` no está definida **a nivel del Environment**, cae a la variable de repositorio, que apunta a `gh-eltejido-deploy` (la identidad de dev). Esa identidad solo tiene una credencial de tipo Branch = `main`, así que ningún subject de tipo Environment le hará match. El log del paso *Verificar que el destino es producción* imprime el Client ID en uso: compáralo con el de `gh-eltejido-deploy-prod`.

**b) El subject de la credencial no coincide carácter por carácter.** En Entra ID → `gh-eltejido-deploy-prod` → **Federated credentials**, abrir la credencial y leer el campo **Subject identifier**. Debe decir exactamente:

```
repo:aliadoti/GHT_El_Tejido:environment:production
```

Los fallos típicos: **que el subject esté en formato inmutable (`repo:owner@<id>/repo@<id>:...`) mientras GitHub emite el legacy — ver §10.2.1, es la causa más frecuente en este repositorio**; haberla creado con Entity type **Branch** o **Tag** en vez de **Environment**; haber escrito el nombre del environment con mayúscula (`Production`) cuando GitHub envía `production`; o haberla creado sobre el app registration equivocado.

> Las credenciales federadas propagan casi de inmediato, pero si acabas de crearla espera un par de minutos antes de reintentar. Para reintentar sin recrear el tag: **Actions → el run fallido → Re-run failed jobs**.

Al terminar, borrar el tag de prueba para no dejar ruido en el historial de releases:

```bash
git push origin --delete v0.0.1-test
git tag -d v0.0.1-test
```

---

## §11. Fase 6 — Primer despliegue y verificación

### 11.1 Etiquetar el commit de release

> ⚠️ **El tag no puede apuntar directamente a `28c3cb1` ni a `1215872`.** Ninguno de esos commits contiene `.github/workflows/deploy-prod.yml`, así que etiquetarlos no ejecutaría nada. El tag debe apuntar a un commit de `main` **posterior a §10.5**, es decir, que ya lleve el workflow.
>
> **Esto no viola el congelamiento.** El artefacto desplegado lo produce `dotnet publish src/ElTejido.Api`, que solo arrastra el grafo de dependencias de la Api: la carpeta `.github/` **no entra en el paquete**. Añadir un workflow de CI/CD encima del commit congelado deja el artefacto binario idénticamente igual. Basta con dejarlo consignado en la adenda: *"el tag de release apunta a `<SHA>`, que es `<commit congelado>` más los archivos de pipeline, sin cambios en `src/` ni en `tests/`."*
>
> Verificar que efectivamente no hay cambios de aplicación entre el commit congelado y el que se va a etiquetar:
> ```bash
> git diff --stat <COMMIT-CONGELADO>..main -- src tests
> ```
> Debe salir **vacío**. Si sale algo, hay que revalidar el congelamiento antes de etiquetar.

Con la decisión de §1.2 firmada y la verificación anterior en vacío:

```bash
git fetch --all
git checkout main
git pull
git tag -a v1.0.0-convencion -m "Artefacto congelado - Convencion GHT 2026"
git push origin v1.0.0-convencion
```

### 11.2 Aprobar el despliegue

1. GitHub → pestaña **Actions** → el workflow *Deploy Producción* aparece en estado **Waiting**.
2. Clic en el run → **Review deployments** → marcar `production` → **Approve and deploy**.
3. Seguir el log. El paso *Registrar el commit que se despliega* imprime el SHA exacto: **guardar ese valor como evidencia**.

### 11.3 Verificación en capas

Ejecutar en orden. Cada nivel supone que el anterior pasó.

**Nivel 1 — el proceso arrancó**

```bash
curl.exe -i https://<HOST-REAL>/health
```
Debe responder `200 OK` con `{"status":"ok"}`. Esto solo dice que el proceso vive: no verifica Key Vault, ni Cosmos, ni Blob.

**Nivel 2 — el portal se sirve**

Abrir `https://<HOST-REAL>/` en el navegador. Debe cargar el portal de El Tejido. Si aparece la página *"Your web app is running and waiting for your content"*, casi siempre es caché del navegador: probar en incógnito o `Ctrl+Shift+R`.

**Nivel 3 — las dependencias responden**

```bash
curl.exe -H "X-Diag-Key: <valor-de-diag-key>" https://<HOST-REAL>/health/ready
```

| Respuesta | Qué significa |
|---|---|
| `200` con `"estado":"ok"` | Todo listo |
| `503` con desglose por componente | Ver la tabla de abajo |
| `404` | `Diagnostico__ClaveSecretName` mal configurado, o el header no coincide. El endpoint se oculta a propósito cuando no hay clave |

Diagnóstico de los `503` más frecuentes:

| Componente en `error`/`faltante` | Causa habitual | Se corrige en |
|---|---|---|
| `cosmos` en `error` con 403 | **Dos causas posibles:** falta el rol de datos, **o** el firewall de IP de Cosmos bloquea al App Service. Verifica primero el rol; si es correcto, es la red | §7.3 y **§6.2.5** |
| `blob` en `error` con 403 | Falta *Storage Blob Data Contributor* | §7.2b |
| `secreto:jwt-sign` (o cualquier otro) en `faltante` | El secreto no existe, el nombre no coincide, o falta *Key Vault Secrets User* | §8.2 / §7.2a |
| `whatsapp:PhoneNumberId` en `faltante` | Aún no se ha hecho la Fase 9 | Normal en este punto |

En este momento del despliegue, **`whatsapp:PhoneNumberId` faltante es esperado**. Todo lo demás debe estar en `ok`.

> La caché de secretos es de 5 minutos, pero solo se cachean las lecturas exitosas: un secreto recién cargado se ve de inmediato.

---

## §12. Fase 7 — Crear el usuario administrador

Este es el único dato que se siembra. Todo lo demás se parametriza después desde la consola.

### 12.1 Crear el admin

1. Abrir `https://<HOST-REAL>/simulacion-whatsapp`.
2. En **Clave de diagnóstico**, pegar el valor de `diag-key`.
3. En **Acceso administrador**:
   - **Numero admin:** el número del administrador en E.164 sin símbolos ni `+` (ej. `573001112233`).
   - **Nombre:** el nombre real del administrador.
   - Clic en **Crear admin inicial**.
4. La respuesta muestra el `id`, el `codigoUsuario` y el número normalizado. **Anotarlos.**

Internamente esto crea el documento `Usuario` y, si no existía, el contador `seq_usuario`. No hay que insertar nada a mano en Data Explorer.

### 12.2 Emitir el OTP y entrar

1. En la misma página, campo **Codigo OTP**: dejar `123456` o escribir otro de 6 dígitos.
2. Clic en **Emitir OTP de prueba**. El código aparece en pantalla y vence según el TTL configurado (5 minutos por defecto).
3. Ir a `https://<HOST-REAL>/login`, escribir el número del administrador y el código.
4. Debe abrirse la consola administrativa.

### 12.3 Verificar la unique key — paso obligatorio

Ahora que hay consola, ejecutar la prueba diferida de §6.2.4:

1. En el portal → **Usuarios** → crear un usuario nuevo **activo** con **el mismo número** del administrador.
2. **El sistema debe rechazarlo con `409`.**
3. Si lo crea, **detener el despliegue**: la unique key está mal, hay que borrar y recrear el contenedor `users` (§6.2.2) y repetir desde §12.1. Todavía no cuesta nada; con participantes cargados costaría muchísimo.
4. Si aparece el usuario duplicado, borrarlo antes de seguir.

### 12.4 Verificar el estado limpio

En Cosmos → **Data Explorer**, confirmar que:

- `users` tiene exactamente **dos documentos**: el administrador y `seq_usuario`.
- `campaigns`, `participants`, `conversations`, `responses` y `config` están **vacíos**.
- `security` tiene solo los documentos del OTP y del log de la sesión.

Este es el estado "instalación limpia" que pedía el objetivo.

---

## §13. Fase 8 — Parametrización manual

Camino crítico del cronograma. Todo se teclea a mano desde la consola administrativa; **no se importa ningún JSON desde dev/QAS**.

El **cómo** de cada pantalla está en `Guias_Implementacion/Manual_Administrador_Parametrizar_Campania.md`. Aquí va el **orden**, que importa porque hay dependencias entre elementos.

### 13.1 Orden de parametrización

| # | Elemento | Depende de | Notas |
|---|---|---|---|
| 1 | **ConfigLLM** | — | Proveedor, modelo, endpoint y `apiKeyRef` = `llm-key`. Al guardar, el sistema **valida que el secreto exista y sea legible**; si falla, revisar §8.2 y §7.2a |
| 2 | **Catálogo de textos `es`** | — | Todos los grupos de frases del núcleo multiidioma |
| 3 | **Catálogo de textos `en`** | — | Redactado nativamente, no traducido |
| 4 | **Rúbrica** | — | Una sola versión operativa por familia (condición 4 del acta) |
| 5 | **Prompts** | Rúbrica | Igual: una sola versión operativa |
| 6 | **Campaña** (en borrador) | Catálogos, rúbrica, prompts | Se crea y se completa **antes** de activarla |
| 7 | **Preguntas** de la campaña | Campaña | En ambos idiomas |
| 8 | **Mensajes y localizaciones** | Campaña, catálogos | |
| 9 | **Mapeos de plantillas Meta** | Fase 9 | Se completa después de tener el Phone Number ID |
| 10 | **Flags de conversación** | Todo lo anterior | Según el acta de flags aprobada |
| 11 | **Usuarios reales** | — | Carga masiva con la plantilla oficial de GHT, cuando GHT entregue el archivo definitivo |

### 13.2 Reglas que no se pueden violar

Salen de las condiciones obligatorias del acta de congelamiento:

- **Una sola campaña.** Se crea, se completa entera en borrador, y solo entonces se activa.
- **Después de activar o del primer envío no se edita nada**: ni campaña, ni localizaciones, ni mensajes, ni preguntas, ni rúbrica, ni prompts, ni catálogos. Un cambio obliga a pausar y reevaluar el congelamiento.
- **Una sola versión operativa por familia** de rúbrica y de prompt. No crear versiones ni borradores posteriores mientras la campaña esté en uso.

### 13.3 Verificación de readiness multiidioma

El portal expone `GET /api/admin/catalogos/readiness`, que valida que los catálogos estén completos por idioma para las campañas activas. **Debe estar en verde antes de activar la campaña.** Es el mismo control que impide activar una campaña con localización incompleta.

---

## §14. Fase 9 — WhatsApp: segundo número con webhook propio

Aquí se materializa la decisión 5. El mismo WABA y las mismas plantillas ya aprobadas, pero con un número dedicado a producción cuyos mensajes entrantes van a la URL de producción, dejando el número de dev/QAS intacto.

### 14.1 Agregar el segundo número al WABA

1. Meta Business Manager → **WhatsApp Manager** → el WABA existente → **Phone numbers** → **Add phone number**.
2. Registrar el número de producción y completar la verificación por SMS o llamada.
3. Al terminar, en la lista de números, copiar el **Phone number ID** del **número nuevo**. Es un valor numérico largo. **Anotarlo en el Anexo A: es el que va en `WhatsApp__PhoneNumberId`.**

> **No confundirlo con el Phone Number ID de dev.** Poner el equivocado hace que producción envíe mensajes desde el número de pruebas, que es exactamente el incidente que se quiere evitar.

### 14.2 Configurar el webhook override del número

Este es el mecanismo que permite que dos ambientes convivan bajo una misma app de Meta. La prioridad de entrega de Meta es: **número → WABA → app**. Al definir un override en el número de producción, sus mensajes van a producción; el número de dev, sin override, sigue cayendo en la URL por defecto de la app, que es la de desarrollo.

Se configura con una llamada a la Graph API:

```bash
# 1) Guardar el cuerpo en un archivo llamado override.json:
{
  "webhook_configuration": {
    "override_callback_uri": "https://<HOST-REAL>/webhook/whatsapp",
    "verify_token": "<EL-MISMO-VALOR-DE-wa-verify-token>"
  }
}
```

```powershell
# 2) Enviarlo (una sola linea; ver §5.1):
curl.exe -X POST "https://graph.facebook.com/v21.0/<PHONE-NUMBER-ID-DE-PRODUCCION>" -H "Authorization: Bearer <ACCESS-TOKEN>" -H "Content-Type: application/json" -d "@override.json"
```

> Se usa un archivo en vez de `-d '{...}'` porque las comillas simples de bash **no** delimitan cadenas en PowerShell y el JSON llegaría corrupto.

Requisitos y límites:

- Ambos campos son obligatorios. La URL tiene un máximo de 200 caracteres.
- El `verify_token` debe ser **exactamente** el valor cargado como `wa-verify-token` en Key Vault (§8.2).
- La app debe estar suscrita a webhooks en el WABA.
- El endpoint debe estar **desplegado y respondiendo** antes de configurar el override: Meta hace la verificación `GET` con `hub.challenge` en el momento de guardarlo.
- El override cubre `messages`, `message_echoes`, `calls` y campos relacionados. **Los webhooks de estado de plantillas y de cuenta seguirán llegando a la URL por defecto de la app** (§4.4).
- Para quitarlo: enviar `override_callback_uri` como cadena vacía.

Verificar que quedó:

```bash
curl.exe "https://graph.facebook.com/v21.0/<PHONE-NUMBER-ID-DE-PRODUCCION>?fields=webhook_configuration" -H "Authorization: Bearer <ACCESS-TOKEN>"
```

### 14.3 Cargar los secretos de WhatsApp

1. **App Secret:** Meta → App Dashboard → **Settings → Basic → App Secret → Show**. Cargarlo en Key Vault como `wa-appsec`. *(Es el mismo valor que en dev, porque es la misma app.)*
2. **Access token:** generar un token de sistema **permanente** para producción y cargarlo como `wa-token`. Un token temporal de 24 horas hará que el sistema deje de enviar mensajes en mitad del evento.

### 14.4 Completar los Application Settings

App Service → **Environment variables**:

- `WhatsApp__PhoneNumberId` = el Phone Number ID de producción de 14.1.

**Apply.** El App Service reinicia.

### 14.5 Verificación completa de readiness

```bash
curl.exe -H "X-Diag-Key: <diag-key>" https://<HOST-REAL>/health/ready
```

Ahora sí debe responder **`200`** con `"estado":"ok"` y **todos** los componentes en `ok`, incluidos los seis secretos y `whatsapp:PhoneNumberId`.

### 14.6 Prueba de extremo a extremo con un teléfono autorizado

1. Desde el portal, dar de alta un usuario de prueba con un teléfono real autorizado y asociarlo a la campaña.
2. Enviarle el mensaje inicial desde el portal.
3. Responder desde el teléfono.
4. Verificar en el portal que la respuesta llegó y que se evaluó.
5. **Confirmar que el ambiente de dev/QAS no recibió nada** — es la prueba de que el override funciona.
6. Borrar el usuario de prueba y sus datos antes del evento. *(Con `Seguridad__PermitirReinicioDatos=false`, el reinicio masivo está cerrado; se hace el borrado individual, que sigue disponible.)*

---

## §15. Fase 10 — Observabilidad, respaldo y costo

### 15.1 Confirmar el backup continuo de Cosmos

Cosmos → **Backup & Restore** → verificar **Continuous (7 days)**. Si al crear la cuenta quedó en *Periodic*, se puede migrar a continuo una sola vez desde esta pantalla.

Anotar en el runbook cómo se dispara una restauración *point-in-time*: crea una **cuenta nueva** a partir del instante elegido, no sobrescribe la existente. Eso significa que restaurar implica repuntar `Cosmos__AccountEndpoint` y reiniciar la aplicación. Conviene tenerlo escrito antes de necesitarlo.

### 15.2 Alertas de Application Insights

En `appi-eltejido-prod-eus2` → **Alerts → Create → Alert rule**. Crear cuatro:

| # | Señal | Condición | Severidad |
|---|---|---|---|
| 1 | Availability / Failed requests | Peticiones fallidas > 10 en 5 minutos | 2 |
| 2 | Exceptions | Excepciones del servidor > 5 en 5 minutos | 2 |
| 3 | Server response time | P95 > 5 segundos en 5 minutos | 3 |
| 4 | **Custom (log)** | Cualquier petición a `/diagnostico/simulacion/*` | **1** |

La cuarta es el control compensatorio de §4.1 y es la más importante de las cuatro. Consulta sugerida:

```kusto
requests
| where url contains "/diagnostico/simulacion"
| project timestamp, url, resultCode, client_IP, operation_Id
```

Todas apuntan a un *Action group* con el correo del responsable.

### 15.3 Budget alert

Portal → **Cost Management + Billing → Budgets → + Add**:

- **Scope:** el grupo de recursos `rg-eltejido-prod-eus2`.
- **Budget amount:** el presupuesto mensual aprobado.
- **Alert conditions:** 50 %, 80 % y 100 % del gasto real.
- **Alert recipients:** el correo del responsable.

El renglón más volátil es el consumo del LLM, que es justamente el que no está en Azure. El budget cubre la infraestructura; el gasto del proveedor externo hay que vigilarlo en su propia consola.

---

## §16. Fase 11 — Endurecimiento previo al evento

Checklist final antes de habilitar el primer envío real.

- [ ] `Seguridad__PermitirReinicioDatos = false` confirmado en Environment variables.
- [ ] `Diagnostico__Clave` **ausente**; solo existe `Diagnostico__ClaveSecretName`.
- [ ] `Cosmos__AccountKey` **ausente** (la app debe usar Managed Identity).
- [ ] `diag-key` **rotada** después de terminar la parametrización, y el valor nuevo comunicado solo al responsable.
- [ ] **`wa-token` es el de producción, no el de dev/QAS.** Durante el montaje se cargó temporalmente el token de QAS para desbloquear la configuración, a la espera de la aprobación del segundo administrador en Meta. Sustituirlo por el del system user `eltejido-prod` **antes del primer envío real**. Mientras siga el de QAS, una rotación o revocación hecha en el ambiente de pruebas deja producción sin enviar, en silencio y sin error visible.
- [ ] Verificado que el `wa-token` en uso **no expira** (system user con *Token expiration: Never*), no uno temporal.
- [ ] Alerta sobre `/diagnostico/simulacion/*` **probada**: hacer una llamada deliberada y confirmar que llega el correo.
- [ ] **HTTPS Only: On** y **Minimum TLS Version: 1.2** en el App Service.
- [ ] **Always On: On** confirmado.
- [ ] Acceso al RG de producción revisado en **IAM**: solo las personas que deben estar. Quitar cualquier asignación heredada innecesaria.
- [ ] Ningún usuario de prueba ni dato de prueba en la base.
- [ ] `/health/ready` en **`200` con todo `ok`**.
- [ ] Readiness multiidioma en verde.
- [ ] Ninguna versión de rúbrica ni de prompt posterior a la seleccionada.
- [ ] Campaña activada y **congelada**; sin borradores posteriores.
- [ ] **Acta de flags firmada.**
- [ ] **Adenda al acta de congelamiento** por la simulación permanente, firmada (§4.1).
- [ ] Banco de calibración D5 ejecutado con la configuración definitiva y con autorización de costo.
- [ ] Smoke y UAT bilingüe completados con teléfonos autorizados sobre WhatsApp real.
- [ ] Runbook de rollback impreso o accesible sin depender del portal.

---

## §17. Rollback y contingencia

### 17.1 Volver a una versión anterior de la aplicación

Como B1 no tiene slots, el rollback es redesplegar el tag anterior. Entre 3 y 5 minutos de indisponibilidad.

1. GitHub → **Actions → Deploy Producción → Run workflow**.
2. En el desplegable **"Use workflow from"**, seleccionar **el tag** anterior (ej. `v0.9.0`), no una rama. La regla de protección del Environment solo admite refs `v*`; lanzarlo desde `main` se bloquea antes de empezar.
3. En el input `tag`, repetir ese mismo tag.
4. Aprobar en el gate del environment.
5. Verificar que la guarda de destino imprime el App Service de producción, y luego `/health` y `/health/ready`.

Si el problema es que el despliegue nuevo ni siquiera arranca, el App Service permite volver a un despliegue previo desde **Deployment Center → Logs → Redeploy**, que es más rápido porque no reconstruye.

### 17.2 Restaurar datos

1. Cosmos → **Point in time restore** → elegir el instante anterior al incidente.
2. Azure crea una **cuenta nueva** (no sobrescribe). Anotar su nuevo endpoint.
3. Cambiar `Cosmos__AccountEndpoint` en el App Service al de la cuenta restaurada.
4. Asignar el rol de datos a la identidad del App Service **sobre la cuenta nueva** (§7.3): es un recurso distinto y no hereda las asignaciones.
5. **Apply** — reinicia.

El paso 4 es el que se olvida y el que provoca que después de restaurar todo devuelva 403.

### 17.3 Apagar la simulación en 60 segundos

Si hay sospecha de que la `diag-key` se filtró:

1. App Service → **Settings → Environment variables**.
2. `Simulacion__Habilitada` → `false` → **Apply**.
3. El reinicio tarda unos 30 segundos. Los endpoints `/diagnostico/simulacion/*` dejan de estar mapeados.
4. Rotar `diag-key` en Key Vault.
5. Revisar el contenedor `security` en busca de eventos `SimulacionWebhookEntrante` para evaluar si hubo inyección de datos.

Consecuencia a asumir: se pierde el mecanismo de recuperación de acceso administrativo. Si además se pierde el acceso del admin, la salida es editar el documento del usuario directamente en Data Explorer.

### 17.4 Perder acceso administrativo

Con la simulación habilitada, se resuelve solo: volver a `/simulacion-whatsapp`, emitir un OTP nuevo y entrar. Es precisamente el escenario que justificó la decisión 3.

---

## §18. Cronograma sugerido (menos de 2 semanas)

| Día | Fases | Duración | Puede paralelizarse con |
|---|---|---|---|
| 1 | Fase 0 (prerrequisitos) + Fase 1 (aprovisionamiento) | 4 h | Gestión del segundo número en Meta |
| 2 | Fase 2 (identidad) + Fase 3 (secretos) + Fase 4 (settings) | 3 h | — |
| 3 | Fase 5 (CI/CD) incluida la prueba en vacío de 10.6 | 3 h | Redacción de la adenda al acta |
| 4 | Fase 6 (primer despliegue) + Fase 7 (admin) | 3 h | — |
| 5–8 | **Fase 8 (parametrización manual)** | 4 días | Fase 9 desde el día 6 |
| 9 | Fase 9 (WhatsApp) + Fase 10 (observabilidad) | 4 h | Recepción del archivo de usuarios de GHT |
| 10 | Carga de usuarios reales + smoke bilingüe | 6 h | — |
| 11 | UAT con teléfonos autorizados + D5 calibración | 6 h | — |
| 12 | Fase 11 (endurecimiento) + firma de actas | 4 h | — |
| 13–14 | Buffer y verificación final pre-envío | — | — |

**Camino crítico: la Fase 8.** Al haber decidido teclear toda la configuración a mano, es el bloque más largo y el que menos se puede comprimir. Si el cronograma se aprieta, es el punto donde conviene poner una segunda persona a validar en paralelo lo que la primera captura.

**Dependencia externa con riesgo:** la verificación del segundo número en Meta. Iniciarla el día 1 aunque no se use hasta el día 9. Es la clase de trámite que se atasca por un SMS que no llega.

---

## §19. Checklist maestro

**Fase 1 — Aprovisionamiento**
- [ ] RG `rg-eltejido-prod-eus2` en East US 2, con las 5 etiquetas
- [ ] Cosmos serverless con backup continuo
- [ ] Base `eltejido` + los 8 contenedores con sus partition keys
- [ ] Unique key `/claveUnicidad` en `users` (**no** `/pk`, **no** vacía)
- [ ] TTL *On (no default)* en `security` y `leases`
- [ ] Storage + contenedor `markdown` privado
- [ ] Application Insights workspace-based
- [ ] Key Vault en modo RBAC con soft delete
- [ ] App Service Linux .NET 8 B1 + Always On + HTTPS Only + TLS 1.2

**Fase 2 — Identidad**
- [ ] Managed Identity del App Service en On; Object ID anotado
- [ ] *Key Vault Secrets User* sobre el Key Vault
- [ ] *Storage Blob Data Contributor* sobre el Storage
- [ ] *Cosmos DB Built-in Data Contributor* por CLI, verificado con `role assignment list`

**Fase 3 — Secretos**
- [ ] `jwt-sign`, `otp-salt`, `diag-key`, `wa-verify-token`, `llm-key` cargados
- [ ] Los tres generados son distintos de los de dev/QAS

**Fase 4 — Configuración**
- [ ] Los 12 Application Settings de §9, incluido `Conversacion__CatalogoTextosHabilitado=true`
- [ ] Application Insights conectado desde su hoja (verificar que aparecen `APPLICATIONINSIGHTS_CONNECTION_STRING` y `ApplicationInsightsAgent_EXTENSION_VERSION`)
- [ ] `Seguridad__PermitirReinicioDatos = false`
- [ ] `Cosmos__AccountKey`, `Diagnostico__Clave` y `Persistencia__Modo` ausentes
- [ ] Ningún setting inerte que induzca a error (`Auth__*SecretName`, `Llm__*`)

**Fase 5 — CI/CD**
- [ ] App registration `gh-eltejido-deploy-prod`
- [ ] Credencial federada tipo **Environment**, nombre `production`
- [ ] Contributor sobre el **RG**, no sobre la suscripción
- [ ] Auditado el Environment `production` preexistente (§10.4.0)
- [ ] Environment `production` con required reviewers y regla de tags `v*` (ya no *No restrictions*)
- [ ] Las **tres** variables de Environment cargadas: `AZURE_CLIENT_ID`, `AZURE_WEBAPP_NAME`, `AZURE_RESOURCE_GROUP`
- [ ] Verificado que `gh-eltejido-deploy` (dev) **no** tiene credencial federada de tipo Environment
- [ ] Guarda de destino presente en `deploy-prod.yml`
- [ ] `deploy-prod.yml` mergeado en `main`
- [ ] Prueba en vacío con tag de prueba, exitosa

**Fase 6 — Despliegue**
- [ ] Decisión firmada sobre el commit a etiquetar
- [ ] Tag creado y pusheado; despliegue aprobado
- [ ] SHA desplegado registrado como evidencia
- [ ] `/health` → 200; portal carga; `/health/ready` sin errores salvo WhatsApp

**Fase 7 — Admin**
- [ ] Usuario administrador creado; login exitoso
- [ ] **Prueba de la unique key: segundo usuario con el mismo número → `409`**
- [ ] Base limpia verificada en Data Explorer

**Fases 8 a 11**
- [ ] Parametrización completa en el orden de §13.1
- [ ] Readiness multiidioma en verde
- [ ] Segundo número en Meta + webhook override configurado y verificado
- [ ] `wa-token` y `wa-appsec` cargados; `WhatsApp__PhoneNumberId` configurado
- [ ] `/health/ready` en 200 con **todo** en `ok`
- [ ] E2E con teléfono real; dev/QAS no recibió nada
- [ ] Alertas, budget y backup verificados
- [ ] Endurecimiento de §16 completo
- [ ] Actas firmadas

---

## Anexo A — Hoja de trabajo

Rellenar durante la ejecución. Este documento pasa a ser confidencial una vez completado; **no debe guardarse en el repositorio**.

| Concepto | Valor |
|---|---|
| Subscription ID | |
| Tenant ID | |
| Resource group | `rg-eltejido-prod-eus2` |
| Región | East US 2 |
| Cosmos account | `cosmos-eltejido-prod-eus2` |
| Cosmos endpoint | |
| Storage account | `steltejidoprodeus2` |
| Blob AccountUrl | |
| Key Vault | `kv-eltejido-prod-eus2` |
| Key Vault URI | |
| App Service Plan | `asp-eltejido-prod-eus2` |
| Web App | `app-eltejido-prod-eus2` |
| **Default domain (host real)** | |
| Application Insights | `appi-eltejido-prod-eus2` |
| Object ID de la Managed Identity | |
| App registration (client ID) | |
| GitHub org/repo | `aliadoti/GHT_El_Tejido` |
| Tag de release desplegado | |
| SHA desplegado | |
| Número del administrador | |
| Phone Number ID de producción | |
| WABA ID | |
| Proveedor LLM / modelo | |

**Nunca anotar aquí:** los valores de `jwt-sign`, `otp-salt`, `diag-key`, `wa-token`, `wa-appsec`, `wa-verify-token` ni la API key del LLM. Viven solo en Key Vault.

---

## Anexo B — `deploy-prod.yml`

```yaml
name: Deploy Producción

# CD de PRODUCCIÓN. Se dispara SOLO con un tag de release (v*) y exige aprobación humana
# a través del GitHub Environment "production". Publica exactamente el commit etiquetado.
# El workflow de dev/QAS (deploy.yml) NO se ve afectado: está atado a push sobre main.
on:
  push:
    tags:
      - 'v*'
  workflow_dispatch:
    inputs:
      tag:
        description: 'Tag de release a desplegar (ej. v1.0.0-convencion)'
        required: true

permissions:
  id-token: write   # requerido para OIDC
  contents: read

concurrency:
  group: deploy-produccion
  cancel-in-progress: false

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    # El environment aporta tres cosas:
    #   (a) el gate de aprobación humana (required reviewers),
    #   (b) las Variables propias de producción, que tienen precedencia sobre las del repo,
    #   (c) el subject OIDC `repo:<org>/<repo>:environment:production`, que debe coincidir
    #       con la credencial federada de tipo Environment creada en Entra ID.
    environment:
      name: production
      url: ${{ steps.deploy.outputs.webapp-url }}

    steps:
      - uses: actions/checkout@v4
        with:
          ref: ${{ github.event.inputs.tag || github.ref }}

      # Evidencia auditable de qué se publicó exactamente.
      - name: Registrar el commit que se despliega
        run: |
          echo "Ref:    ${{ github.event.inputs.tag || github.ref_name }}"
          echo "Commit: $(git rev-parse HEAD)"

      # 1) Build del SPA Angular. outputPath ya apunta a src/ElTejido.Api/wwwroot.
      - uses: actions/setup-node@v4
        with:
          node-version: 22.x
          cache: npm
          cache-dependency-path: src/ElTejido.Web/package-lock.json
      - working-directory: src/ElTejido.Web
        run: npm ci
      - working-directory: src/ElTejido.Web
        run: npm run build -- --configuration production

      # 2) Build + publish del backend (incluye wwwroot con el SPA).
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - run: dotnet publish src/ElTejido.Api/ElTejido.Api.csproj -c Release -o ./publish

      # 3) GUARDA DE DESTINO. Las variables de Environment tienen precedencia sobre las de
      #    repositorio; si el Environment `production` no las define, `vars.*` cae silenciosamente
      #    a los valores de repo, que apuntan al App Service de dev/QAS (app-eltejido-mvp).
      #    Esta guarda convierte ese fallo silencioso en un fallo ruidoso ANTES de tocar Azure.
      - name: Verificar que el destino es producción
        run: |
          destino="${{ vars.AZURE_WEBAPP_NAME }}"
          grupo="${{ vars.AZURE_RESOURCE_GROUP }}"
          cliente="${{ vars.AZURE_CLIENT_ID }}"
          fallo=0
          if [ -z "$cliente" ]; then
            echo "::error::AZURE_CLIENT_ID no está definida."; fallo=1
          fi
          if [ "$destino" != "app-eltejido-prod-eus2" ]; then
            echo "::error::Destino '$destino' no es el App Service de producción."
            echo "::error::Revisa las Environment variables de 'production' (guía §10.4.1)."
            fallo=1
          fi
          if [ "$grupo" != "rg-eltejido-prod-eus2" ]; then
            echo "::error::Grupo '$grupo' no es el resource group de producción."; fallo=1
          fi
          [ "$fallo" -eq 0 ] || exit 1
          # Se imprime el Client ID en uso: no es secreto y convierte un AADSTS700213
          # en un diagnostico de cinco segundos (¿es el de prod o cayo al de dev?).
          echo "Destino verificado: $destino en $grupo"
          echo "Client ID en uso:   $cliente"

      # 4) Login a Azure por OIDC y despliegue al App Service de producción.
      - uses: azure/login@v2
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

      - uses: azure/webapps-deploy@v3
        id: deploy
        with:
          app-name: ${{ vars.AZURE_WEBAPP_NAME }}
          package: ./publish

      # 5) Smoke test: /health debe responder 200 tras el despliegue.
      #    El fallback usa `az webapp show -g` (no `az webapp list`) porque el service principal
      #    solo tiene permisos sobre el resource group de producción.
      - name: Smoke test /health
        run: |
          url="${{ steps.deploy.outputs.webapp-url }}"
          if [ -z "$url" ]; then
            host=$(az webapp show \
              -g "${{ vars.AZURE_RESOURCE_GROUP }}" \
              -n "${{ vars.AZURE_WEBAPP_NAME }}" \
              --query defaultHostName -o tsv)
            [ -n "$host" ] && url="https://$host"
          fi
          url="${url%/}"
          echo "Smoke test contra: $url/health"
          for i in 1 2 3 4 5 6; do
            if curl -fsS "$url/health"; then echo; echo "health OK"; exit 0; fi
            echo "reintento $i..."; sleep 10
          done
          echo "El smoke test de /health falló"; exit 1
```

---

## Anexo C — Desviaciones respecto del acta de congelamiento

Para incorporar a la adenda.

| # | Condición del acta | Situación en este plan | Justificación y control |
|---|---|---|---|
| 5 | `Simulacion__Habilitada=false` y no se usa clave de diagnóstico | **Se incumple:** la simulación queda habilitada de forma permanente con clave de diagnóstico activa | Es el mecanismo elegido para crear el administrador inicial y para recuperar acceso durante el evento. Controles compensatorios en §4.1: clave de 32+ bytes solo en Key Vault, alerta sobre el endpoint, rotación programada y procedimiento de apagado en 60 segundos |
| — | Se congela el commit `28c3cb1` | **Pendiente de resolver:** HEAD es `1215872` | Requiere `git diff 28c3cb1..1215872` y decisión firmada antes de etiquetar (§1.2) |

Las condiciones 1, 2, 3, 4 y 6 del acta se cumplen íntegramente: ambiente exclusivo sin documentos legacy, una sola campaña creada y completada en borrador, prohibición de edición después de activar, una sola versión operativa por familia, y secretos inyectados por referencia sin quedar en archivos ni reportes.

---

## Fuentes consultadas

- [Configuring OpenID Connect in Azure — GitHub Docs](https://docs.github.com/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-azure)
- [OIDC claims used to define trust conditions — GitHub Docs](https://docs.github.com/en/actions/reference/security/oidc)
- [Webhook overrides — Meta for Developers](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/override/)
- [Webhooks overview — Meta for Developers](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/overview)
- [Deployment best practices — Azure App Service](https://learn.microsoft.com/en-us/azure/app-service/deploy-best-practices)
- [Set up staging environments — Azure App Service](https://learn.microsoft.com/en-us/azure/app-service/deploy-staging-slots)
- [Azure Cosmos DB serverless](https://learn.microsoft.com/en-us/azure/cosmos-db/serverless)
- [Key Vault RBAC guide](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide)

*Fin del plan.*
