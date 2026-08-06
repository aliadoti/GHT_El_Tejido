# 12 — Prompt para Claude Code: ejecutar y revisar las pruebas E2E

> **Cómo usarlo (resumen):** abre una terminal en la raíz del repo → `claude --model fable` →
> `/model fable` (verifica con `/status`) → opcional `/effort high` → pega el bloque "INICIO DEL PROMPT".
> Recomendado: deja **la API y el portal ya levantados** en dos terminales antes de empezar (§ Arranque).
> Modelo sugerido: **Claude Fable 5** (agente de larga duración con Playwright); usa **Opus 5** solo para
> analizar un fallo difícil.

---

## Arranque del entorno (hazlo tú antes, o deja que el agente lo haga)

**Terminal A — API:**
```powershell
cd .\src\ElTejido.Api
dotnet user-secrets init
dotnet user-secrets set "Secretos:otp-salt" "pepper-local-cambiar"
dotnet user-secrets set "Secretos:jwt-sign" "clave-local-de-firma-con-mas-de-32-bytes"
dotnet user-secrets set "Secretos:wa-appsec" "appsec-local"
dotnet user-secrets set "Secretos:wa-verify-token" "verify-local"
cd ..\..
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project .\src\ElTejido.Api --urls "https://localhost:5001"
```
**Terminal B — Portal:**
```powershell
cd .\src\ElTejido.Web
npx -y -p node@24.15.0 npm run start -- --host=127.0.0.1 --port=4200
```
**Terminal C — Claude Code:** `claude --model fable`

---

## ▼ INICIO DEL PROMPT — copia desde aquí ▼

Eres **Claude Code** actuando como **ingeniero de QA / SDET senior**. Ejecuta de punta a punta (E2E)
las pruebas de los requerimientos implementados de "El Tejido", valida los resultados contra la BD/API
y el portal, y entrega un archivo de resultados con evidencia. Trabajas en la raíz de este repositorio.

### Regla de oro
**Si algo falta, no está claro, o no puedes obtener un acceso, DETENTE y pregúntame antes de continuar.**
No inventes secretos, URLs ni resultados. No marques PASS sin evidencia real. Prefiero una pregunta a una
suposición. Declara en una línea, al inicio, qué vas a probar y desde qué rol decides.

### Permisos y herramientas que usarás
- **Shell:** para levantar/consultar servicios y correr comandos. Pide aprobación cuando el sistema te
  la solicite; no ejecutes nada destructivo fuera de los reinicios de prueba documentados.
- **Playwright (navegador):** para el portal `http://127.0.0.1:4200` y la página `/simulacion-whatsapp`.
  Si Playwright no está, instálalo (`npx playwright install chromium`). Si prefieres MCP de navegador y
  no está disponible, dímelo.
- **API/HTTP:** para validar vía `/api/admin/*` (cookie de sesión + `X-CSRF-Token`).
- **Archivos:** para leer specs/guías y escribir SOLO el archivo de resultados y evidencia.
- **No** hagas `git push`, despliegue ni cambios de configuración remota. **No** modifiques código de
  producto ni specs para "hacer pasar" una prueba: si algo no cumple, es un FAIL.

### Paso 1 — Lee las fuentes de verdad (en este orden)
1. `QAS/10_Guia_E2E_Ejecutable_Agente_o_Humano.md` — guía principal (accesos, datos, catálogo E1–E19, formato de resultados).
2. `QAS/02_Casos_de_Prueba_E2E.md` — detalle por caso.
3. `QAS/04_Datos_de_Prueba_y_Reinicio.md` — identidades, campaña `CAMP-QA`, preguntas, respuestas y reinicio (P-03).
4. `Guias_Implementacion/Guia_Prueba_E2E_Simulada_WhatsApp.md` — simulación de WhatsApp.
5. `Especificaciones/AVANCES.md` y `Especificaciones/Iniciativas/TODO.md` — estado real; `Especificaciones/base/04_Contrato_API_REST.md` — rutas exactas.

### Paso 2 — Verifica el entorno
- Comprueba que responde `GET https://localhost:5001/health` = 200 y que el portal carga en `http://127.0.0.1:4200`.
- Si **no** están levantados, puedes iniciarlos tú (ver los comandos de "Arranque" que te dio el usuario),
  cada uno en su propia terminal en segundo plano. Si algo no compila o no responde, **detente y repórtalo**.
- Para validar en **BD**: por defecto la persistencia es en memoria. Si necesitas inspeccionar Cosmos,
  arranca con `Persistencia:Modo=Cosmos` y usa el emulador (endpoint y clave en `QAS/10 §1.4`). Si no está
  disponible, valida por la API y anótalo. Si no puedes validar de ninguna forma, **pregunta**.

### Paso 3 — Prepara los datos (una vez)
Crea admin y entra (`Crear admin inicial` → `Emitir OTP de prueba` → `/login`, número `573001119999`,
OTP `123456`). Parametriza `CAMP-QA` con las 3 preguntas, la rúbrica `RUB-QA`, el prompt `evaluar`
aprobado y `LLM-QA` (ver `QAS/10 §2`). Usa las respuestas de prueba de `QAS/10 §2.1`.
Si la evaluación LLM necesita una `llm-key` real que no tienes, **pregúntame**; no la inventes (marca
como `BLOCKED` los casos que dependan de LLM real si te lo indico).

### Paso 4 — Ejecuta E1–E19 (catálogo de `QAS/10 §4`)
Para cada escenario, en orden:
1. **Reinicia** el participante o la campaña (cold-start) según `QAS/10 §5` / `QAS/04 §6`.
2. Enciende el **flag** de la columna del escenario (si aplica) y **apágalo al terminar**.
3. Ejecuta la acción (mensaje de prueba por `/simulacion-whatsapp` vía Playwright, o portal vía Playwright).
4. **Valida** el resultado esperado por API o BD (conversación, respuesta evaluada, Markdown sin secretos,
   `nivelMadurez`, `motivoCierre`, ciclo/idea, etc.).
5. **Registra** el resultado con evidencia antes de pasar al siguiente.
Los flags de P-26..P-30 y DT-P27-01 nacen apagados: enciéndelos solo para su escenario y déjalos apagados al final.

### Paso 5 — Archivo de resultados (entregable)
Crea `QAS/resultados/Resultados_E2E_AAAA-MM-DD.md` con el formato de `QAS/10 §6`: cabecera de entorno
(ejecutor = "Claude Code / Fable 5", commit/build, modo de persistencia, flags encendidos), tabla
`# | Requerimiento | Estado (PASS/FAIL/BLOCKED) | Evidencia | Observaciones`, y un resumen (totales,
fallos a reportar, bloqueos). Guarda capturas/Markdown/ids en `QAS/resultados/AAAA-MM-DD/`. Opcional:
`resultados.json`. Evidencia obligatoria por caso; un FAIL describe en una línea qué se esperaba y qué pasó.

### Paso 6 — Revisión final
Escribe una revisión breve (≤8 líneas, lenguaje simple): PASS/FAIL/BLOCKED, fallos a reportar y por qué
quedó bloqueado algo. Si un requerimiento implementado no está cubierto por E1–E19, propónlo (no lo
ejecutes sin confirmar). Deja al final la lista de preguntas/pendientes. No dejes flags encendidos ni
datos a medio reiniciar.

### Cómo trabajar (Claude Code)
- Avanza en pasos pequeños y verificables; muestra el comando/acción antes de ejecutarlo cuando sea relevante.
- Usa un plan corto (todo list) para seguir E1–E19 y no perder el hilo en la sesión larga.
- Si un fallo es difícil de diagnosticar, dímelo: puedo cambiarte a Opus 5 para ese análisis.

## ▲ FIN DEL PROMPT ▲

---

## Apéndice — Modalidad contra el sistema DESPLEGADO (Azure)

Si en vez de local se prueba contra Azure, reemplaza el "Arranque del entorno" y el Paso 2 por esto
(ver `QAS/10 §8`):

- **No** levantes servicios locales. Trabaja contra la URL desplegada: `https://<tuapp>.azurewebsites.net`.
- Verifica `GET /health` = 200. Un humano ya dejó `Simulacion:Habilitada=true` y la clave de diagnóstico.
- El humano te dará: **URL base**, **clave de diagnóstico** (header `X-Diag-Key`) y el valor de **`wa-appsec`**.
  **No** manejes la LLM key: la app la lee de Key Vault. Valida por `/api/admin/*` sobre esa URL.
- **Reutiliza** los activos ya cargados al crear la campaña (no los recrees): rúbrica
  `rúbrica OpenBrain v3.4`, prompt `Evaluación con rubrica OpenBrain Thought-Scoring`, config LLM
  `OpenRouter-Terra`. **Salta** los sub-pasos de E3 que crean rúbrica/prompt/LLM.
- Con LLM real, **E5/E6/E9/E18(puntaje)** dan PASS/FAIL (ya no BLOCKED). **E4** (envío real) puede
  fallar sin `wa-token` (esperado); el webhook simulado sí funciona.
- **NO borres datos al terminar:** conserva campañas, ideas y evaluaciones para revisión; registra los
  ids en el archivo de resultados. Al cerrar, pide al humano apagar `Simulacion:Habilitada`.
- No cambies App Settings remotos tú; eso lo hace el humano.

### Simular el mensaje entrante SIN App Secret (DT-QA-01) y alcance
- El despliegue está conectado a WhatsApp real; **NO** uses ni pidas `wa-appsec` (es el App Secret real
  de Meta). Simula el mensaje entrante con el endpoint de diagnóstico:
  `POST https://<tuapp>.azurewebsites.net/diagnostico/simulacion/webhook-entrante`, header `X-Diag-Key`,
  body `{ "numero": "...", "texto": "..." }` → 200; se procesa como un webhook real. Solo necesitas
  `X-Diag-Key`. Si responde **404**, DT-QA-01 aún no está desplegado → **detente y avísame** (no firmes
  con el App Secret real).
- **Alcance de esta corrida:** prueba solo los conversacionales **E2, E5, E6, E9, E10, E11, E12, E13,
  E14, E18, E19**. **Omite** E1/E4/E15/E16 (ya probadas). **E3 es preparación** (crear la campaña
  reutilizando rúbrica/prompt/LLM existentes), no una prueba puntuada.
