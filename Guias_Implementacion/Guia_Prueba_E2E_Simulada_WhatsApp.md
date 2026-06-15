# Guia - Prueba E2E simulada de WhatsApp

Esta guia permite a un humano probar el MVP sin esperar mensajes reales de WhatsApp. La pagina de apoyo esta en `/simulacion-whatsapp` y usa endpoints bajo `/diagnostico/simulacion`. Estan disponibles en dos casos: (a) en local con `ASPNETCORE_ENVIRONMENT=Development` (sin clave), y (b) en el **despliegue real (Azure)** cuando se activa `Simulacion:Habilitada=true`, protegidos por la **clave de diagnostico** (header `X-Diag-Key`). El recorrido contra Azure esta en §7.

## 1. Preparar entorno local

En `Development` la API arranca con **persistencia en memoria** (volatil) gracias a
`appsettings.Development.json -> Persistencia:Modo=Memoria`. No se requiere Cosmos, Azurite ni Key
Vault para ejercitar la pagina de simulacion: los datos se pierden al reiniciar la API. Si quieres
persistencia real, sigue `Guia_Azure_Portal_Paso_a_Paso.md` y omite la clave `Persistencia:Modo`
(o ponla en `Cosmos`).

1. Configura secretos locales en el proyecto API. Minimo para login y webhook simulado:

```powershell
cd .\src\ElTejido.Api
dotnet user-secrets init
dotnet user-secrets set "Secretos:otp-salt" "pepper-local-cambiar"
dotnet user-secrets set "Secretos:jwt-sign" "clave-local-de-firma-con-mas-de-32-bytes"
dotnet user-secrets set "Secretos:wa-appsec" "appsec-local"
dotnet user-secrets set "Secretos:wa-verify-token" "verify-local"
```

2. Levanta la API en el puerto usado por el proxy Angular:

```powershell
cd ..\..
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project .\src\ElTejido.Api --urls "https://localhost:5001"
```

3. En otra terminal levanta el portal. Angular CLI 22 exige Node 22.22.3+, 24.15.0+ o 26+. Si tu
Node global no cumple, usa Node temporal con `npx -p node@24.15.0`:

```powershell
cd .\src\ElTejido.Web
npx -y -p node@24.15.0 npm run start -- --host=127.0.0.1 --port=4200
```

Abre `http://127.0.0.1:4200/simulacion-whatsapp`.

## 2. Primer login como administrador

1. En la pagina de simulacion, usa `Numero admin` con formato normalizado, por ejemplo `573001119999`.
2. Pulsa `Crear admin inicial`. Esto crea o actualiza un usuario `admin` activo en el contenedor `users`.
3. Deja `Codigo OTP` en `123456` o escribe otro codigo de 6 digitos.
4. Pulsa `Emitir OTP de prueba`. Esto guarda un OTP hasheado en el contenedor `security`.
5. Entra a `/login`, escribe el mismo numero admin y pulsa solicitar codigo.
6. Escribe el OTP emitido en la pagina de simulacion y confirma.

Nota: el envio real de OTP por WhatsApp todavia no esta conectado; esta guia usa el endpoint de simulacion de Development para poder iniciar la parametrizacion.

## 3. Parametrizacion minima en el portal

1. En `Usuarios`, crea al menos un participante activo con numero normalizado, por ejemplo `573001112233`.
2. En `Rubricas`, crea una rubrica activa.
3. En `Prompts`, crea un prompt `evaluar` y apruebalo.
4. En `Config LLM`, crea una configuracion con proveedor/modelo/endpoint y API key real. La respuesta debe mostrar solo `apiKeyRef` y mascara.
5. En `Campanias`, crea una campania con la rubrica, el prompt y la config LLM.
6. Agrega un mensaje inicial con texto como `Hola {{nombre}}, comparte tu idea.`
7. Agrega una pregunta activa.
8. Asocia el participante a la campania.
9. Cambia la campania a `activa`.
10. En `Envios`, consulta la campania, selecciona participantes y envia el mensaje inicial.

## 4. Simular respuesta de participante

1. Vuelve a `/simulacion-whatsapp`.
2. En `App secret`, escribe el mismo valor configurado en `Secretos:wa-appsec`, por ejemplo `appsec-local`.
3. En `Numero participante`, escribe el numero del usuario asociado a la campania.
4. En `Texto`, escribe la respuesta que quieres evaluar.
5. Pulsa `Enviar webhook firmado`.
6. El estado esperado es `200`. El backend responde inmediatamente y procesa el mensaje en segundo plano.

## 5. Verificar resultado

1. En `Resultados`, escribe el `campaniaId`.
2. Pulsa `Consultar resultados`.
3. Verifica:
   - existe una conversacion cerrada o en el estado esperado;
   - existe una respuesta evaluada;
   - existe una evaluacion con snapshot de rubrica, prompt y config LLM;
   - existe Markdown generado y no contiene secretos;
   - el participante aparece con `estadoRespuesta=respondio`.

Si no aparece Markdown, revisa primero la configuracion LLM y los secretos. Si el proveedor LLM falla o la salida no cumple el contrato, el orquestador cierra con fallback neutro y deja la respuesta como `evaluacionPendiente`.

## 6. Prueba con WhatsApp real

Cuando la app de Meta y las plantillas esten aprobadas, usa `Guia_WhatsApp_Cloud_API_Meta_Paso_a_Paso.md` para configurar el webhook real. El recorrido funcional es el mismo, pero la respuesta del participante se envia desde el telefono real y no desde `/simulacion-whatsapp`.

## 7. Prueba simulada contra el despliegue real en Azure (sin WhatsApp todavia)

Permite ejercitar el sistema **ya desplegado** (Cosmos, Key Vault y Blob reales) **sin** conectar WhatsApp. El recorrido funcional es identico a §2–§5, pero la simulacion corre en Produccion y por eso queda **cerrada tras la clave de diagnostico**. Mantenla habilitada solo durante la prueba.

### 7.1 Requisitos previos (una vez)
1. Infra Azure creada y la app desplegada (`Guia_Azure_Portal_Paso_a_Paso.md`).
2. **Clave de diagnostico configurada** (Azure §10–§11): `Diagnostico__ClaveSecretName=diag-key` (secreto en Key Vault) **o** `Diagnostico__Clave=<cadena>`. Verifica con `GET /health/ready` (Azure §11.3).
3. Secretos minimos en Key Vault: `jwt-sign` y `otp-salt` (login admin). Para firmar el webhook simulado necesitas **`wa-appsec`**: como aun no tienes el real de Meta, carga un **valor temporal** que tu elijas (lo usaras en el campo *App secret* de la pagina). `wa-token`/`wa-verify-token` **no** hacen falta para el camino entrante simulado.
4. Para que la **evaluacion LLM** sea real, carga `llm-key` real y crea una `Config LLM` valida en el portal. Sin ella, el orquestador cierra con fallback neutro y deja la respuesta como `evaluacionPendiente` (no es un fallo del flujo).

### 7.2 Activar la simulacion en el App Service
1. App Service → **Settings → Environment variables** → agrega `Simulacion__Habilitada` = `true` → **Apply** (reinicia la app).
2. (Opcional, recomendado) Confirma que el portal esta servido: abre `https://<webapp>.azurewebsites.net/health` → 200.

### 7.3 Recorrido
1. Abre `https://<webapp>.azurewebsites.net/simulacion-whatsapp`.
2. En **Clave de diagnostico** (`X-Diag-Key`) escribe la clave del paso 7.1.2. Sin ella, `Crear admin inicial` y `Emitir OTP` responden 404.
3. Sigue §2 (crear admin + emitir OTP), §3 (parametrizacion en el portal contra Cosmos real), y §4 (en **App secret** usa el **mismo valor** que cargaste como `wa-appsec`; envia el webhook firmado → debe responder `200`).
4. Verifica resultados como en §5 (conversacion cerrada, respuesta evaluada, evaluacion con snapshots, Markdown sin secretos, participante `respondio`).

> Nota: el **envio inicial** desde el portal (`Envios`) llama a Graph API y **fallara** sin `wa-token`/`PhoneNumberId` reales; es esperado. La prueba simulada no lo necesita: el camino entrante (la respuesta del participante via `/webhook/whatsapp`) se evalua igual.

### 7.4 Cerrar la simulacion (importante)
Al terminar, pon `Simulacion__Habilitada` = `false` (o borra la variable) y reinicia. Asi los endpoints de simulacion dejan de mapearse en Produccion. Aunque estan protegidos por la clave, el principio es no dejar abierta una superficie que crea admins y emite OTP.
