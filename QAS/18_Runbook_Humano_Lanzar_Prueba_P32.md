# 18 — Runbook humano para lanzar la prueba P-32 en Azure

Esta guía la sigue el humano que autoriza y lanza al agente. El agente crea los datos de prueba y
ejecuta las pruebas; el humano controla el acceso temporal y los secretos.

## Antes de iniciar

1. Confirma que usarás un **ambiente aislado de pruebas**, nunca producción. Verifica además el canal
   saliente: la simulación entrante no reemplaza el `WhatsAppGateway` real. Debe existir aislamiento
   del emisor o todos los números deben ser de prueba y estar autorizados.
2. Ten la URL del ambiente y permiso administrativo para el portal y para cambiar su configuración.
3. Confirma que existen y están activos, sin modificarlos: rúbrica **`rúbrica OpenBrain v3.4`**,
   prompt **`Evaluación con rubrica OpenBrain Thought-Scoring`** y configuración LLM
   **`OpenRouter-Terra`**. El agente los reutiliza; no necesita ni debe recibir la key de OpenRouter.
4. Si se probará envío real, confirma que las plantillas Meta en inglés están aprobadas. Si no lo están,
   la prueba de envío real queda `BLOCKED`. Las simulaciones conversacionales solo continúan si el
   emisor está aislado o usa números de prueba autorizados; `Simulacion__Habilitada=true` por sí sola
   **no evita llamadas salientes reales a Meta**.

## Preparar la simulación

1. Un administrador autorizado obtiene de Key Vault la clave diagnóstica configurada para el App
   Service, normalmente el secreto **`diag-key`**. No la envíes por chat, correo, prompt, archivo ni
   captura.
2. En Azure Portal abre el **App Service de pruebas** → **Configuration** → **Application settings**.
3. Cambia temporalmente `Simulacion__Habilitada` a `true` y guarda. Espera a que el App Service reinicie
   y responda de nuevo. No cambies `Diagnostico__ClaveSecretName`, `wa-appsec` ni `llm-key`.
4. Abre una terminal PowerShell nueva y controlada. Pega este bloque; pedirá la clave sin mostrarla y la
   entregará únicamente a los procesos iniciados desde esa terminal:

```powershell
$claveSegura = Read-Host 'Pega la diag-key (no se mostrará)' -AsSecureString
$punteroClave = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($claveSegura)

try {
    $env:GHT_DIAG_KEY = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($punteroClave)

    # Inicia aquí UN agente. Elige uno de estos comandos si está instalado:
    # claude
    # codex
}
finally {
    if ($punteroClave -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($punteroClave)
    }

    Remove-Item Env:\GHT_DIAG_KEY -ErrorAction SilentlyContinue
}
```

5. Dentro del agente recién iniciado escribe solo:

```text
Lee y ejecuta estrictamente QAS/17_Prompt_Ejecutar_Validacion_Completa_P32.md.
```

El archivo contiene las instrucciones para crear usuarios y campañas nuevos en cada corrida, reutilizar
la rúbrica, prompt y OpenRouter existentes, usar la clave únicamente como `X-Diag-Key` y guardar el
reporte. No pegues la clave en el mensaje al agente.

## Preparar la ventana controlada del catálogo (gate OFF → ON → OFF)

El gate no crea semillas ni publica borradores. Las semillas `es/en` se crean, revisan y activan desde
**Textos de conversación** mientras el gate todavía está OFF. El gate solo decide si la conversación
usa el catálogo global activo o conserva el camino legacy.

Antes de abrir la ventana, el operador configura para cada par requerido por **Preparación**:

- `WhatsApp__PlantillaEnvioInicial__Mapeos__{plantillaRef}__es__Nombre` e `Idioma`;
- `WhatsApp__PlantillaEnvioInicial__Mapeos__{plantillaRef}__en__Nombre` e `Idioma`;
- `...__Componentes__0..N` en el orden exacto del body aprobado, solo si tiene variables.

No inventes estos valores: cópialos de las plantillas aprobadas en Meta. Si el body usa, por ejemplo,
nombre y campaña, el orden habitual del sistema es `Componentes__0=nombre` y
`Componentes__1=campania`, pero manda el orden real de Meta. Guarda, espera el reinicio y exige
`listoParaGateOn=true`. Readiness no reemplaza la revisión humana de aprobación/variables.

1. Empieza con el gate OFF. En Azure Portal abre **App Services** → el App Service de pruebas →
   **Settings** → **Environment variables** → **App settings**. Si
   `Conversacion__CatalogoTextosHabilitado` no existe, el default del sistema es `false`; también
   puedes dejarlo explícitamente en `false`. En Linux se usan dos guiones bajos `__` porque .NET los
   interpreta como `Conversacion:CatalogoTextosHabilitado`.
2. Con el gate OFF, deja que el agente complete `QAS/22` Pruebas 1 a 8: debe crear/revisar los
   catálogos `es/en`, activarlos explícitamente y ejecutar la regresión legacy aplicable. Activar un
   catálogo no enciende el gate.
3. Cuando el agente indique que llegó al recorrido gate-ON, autoriza la ventana. Agrega o edita la
   variable `Conversacion__CatalogoTextosHabilitado` con valor `true` y pulsa **Apply** en el diálogo
   y nuevamente en la página. El cambio de un App Setting reinicia automáticamente el App Service.
4. Espera a que `/health` vuelva a responder `200`. Pide al agente refrescar el panel **Preparación**
   y comprobar que muestra el gate encendido y `es/en` activos y válidos. La vista de contenido
   efectivo no sustituye esta comprobación.
5. Ejecuta únicamente el recorrido bilingüe autorizado. Si falta un catálogo, aparece mezcla de
   idioma o se detecta un envío real no previsto, detén la corrida y aplica el rollback del paso 6.
6. Al terminar la ventana, salvo que exista acta formal para dejar P-32 activo, cambia
   `Conversacion__CatalogoTextosHabilitado` a `false`, pulsa **Apply**, espera el reinicio y confirma
   en **Preparación** que el gate volvió a OFF. No borres catálogos ni versiones.

El estado deseado antes de una activación estable es: catálogos `es/en` activos y válidos,
`Simulacion__Habilitada=false`, gate OFF y evidencia QAS conservada. Solo después de QAS green, D5,
UAT, costo/latencia, plantillas Meta aplicables y acta de cambio puede dejarse el gate ON para uso.

## Durante la ejecución

1. Revisa que el agente identifique el ambiente, autorización y plan antes de actuar.
2. Debe crear usuarios y campañas con un identificador único de corrida; no debe reutilizar ni borrar
  datos anteriores.
3. Si informa `404` en simulación, no le entregues secretos adicionales: debe marcar `BLOCKED`. Revisa
  después, como humano, que la simulación esté habilitada y que la variable se haya inyectado en la
  misma sesión que inició el agente.
4. D5 real, UAT y envío WhatsApp real solo se ejecutan si sus autorizaciones externas existen. Un
  bloqueo externo es resultado válido; no se debe forzar con claves o datos no autorizados.
5. Si el agente detecta un `wamid` o cualquier llamada a Meta no prevista, detén la corrida, conserva
   evidencia y marca los recorridos restantes `BLOCKED`; no cambies secretos para continuar.

## Cierre obligatorio

1. Espera el reporte `QAS/resultados/Resultados_P32_Multidioma_<fecha>.md` y revisa el estado de cada
   prueba antes de cerrar la terminal.
2. Cierra el agente. El bloque PowerShell elimina `GHT_DIAG_KEY` al salir; si interrumpes la sesión,
   ejecuta manualmente `Remove-Item Env:\GHT_DIAG_KEY` en esa misma terminal.
3. En Azure Portal vuelve `Simulacion__Habilitada` a `false`, guarda y espera el reinicio. Este cierre
   es obligatorio también después del smoke acotado DT-P32-03-01.
4. Confirma que el reporte no contiene claves, teléfonos completos ni contenido confidencial.
5. No actives P-32 en producción: solo puede decidirse después de PASS en las pruebas aplicables, D5,
   UAT, plantillas Meta, revisión de costo/latencia y acta de cambio.

## Si el agente se ejecuta dentro de una aplicación y no en la terminal

Una variable creada en PowerShell no llega a una sesión de Codex/ChatGPT que ya estaba abierta. Usa la
función de **Secrets** o **Environment variables** de esa plataforma para crear `GHT_DIAG_KEY` al iniciar
una sesión nueva. Si la plataforma no la ofrece, ejecuta Claude Code o Codex CLI desde la terminal
controlada descrita arriba. Nunca copies la clave en el chat como alternativa.
