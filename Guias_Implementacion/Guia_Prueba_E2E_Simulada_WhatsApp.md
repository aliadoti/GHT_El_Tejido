# Guia - Prueba E2E simulada de WhatsApp

Esta guia permite a un humano probar el MVP sin esperar mensajes reales de WhatsApp. La pagina de apoyo esta en `/simulacion-whatsapp` y usa endpoints bajo `/diagnostico/simulacion`, disponibles solo con `ASPNETCORE_ENVIRONMENT=Development`.

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
