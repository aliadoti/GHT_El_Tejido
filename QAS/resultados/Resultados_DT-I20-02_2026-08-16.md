# Resultados — DT-I20-02: texto plano y prompt seguro · 2026-08-16

Corrida completa de `QAS/21_DT-I20-02_Texto_Plano_y_Prompt_Seguro_Como_Probar.md`, incluida la
preparación puntuada (familia de prompt nueva, aprobación, campaña aislada, asociación y rollback).
**Las ocho pruebas quedan en PASS.** D5 queda `BLOCKED` por credencial no disponible.

No se modificó la familia `1`, ninguna campaña preexistente, ninguna rúbrica, ninguna ConfigLLM,
ningún secreto, App Setting, plantilla Meta, catálogo P-32, código ni despliegue. Ninguna clave
aparece en este reporte, en los comandos ejecutados ni en archivos del repositorio.

## 1. Ambiente, autorización y alcance

| Campo | Valor |
|---|---|
| Ejecutor | Claude Code (Opus 5) |
| Identificador de corrida | **`I20-02-20260816-0243`** |
| Fecha | 2026-08-16, 02:43Z – 13:35Z |
| Ambiente | **Azure `app-eltejido-mvp`** — `https://app-eltejido-mvp-evd8ffcgd3fthshw.eastus-01.azurewebsites.net` |
| Salud | `/health` 200; `/health/ready` 200 con 10 componentes `ok` y 1 `no_aplica` (`conversacion:umbralResumenConsolidacion`) |
| Build desplegado | Deployment `522b2b5a-a259-4eea-a519-2eac77322563`, recibido **2026-08-16T02:28Z**, activo y confirmado en el log de plataforma (`Site is running with deployment version: 522b2b5a…`). Posterior al último commit de `DT-I20-02` (`54eb9db`, 02:24Z), por lo que los tres cortes están desplegados |
| Transporte | Webhook simulado `POST /diagnostico/simulacion/webhook-entrante` con header `X-Diag-Key`, más `/api/admin/*` con sesión admin. Cliente HTTP en Python (`requests`), UTF-8 verificado extremo a extremo |
| Tráfico WhatsApp real | Ninguno hacia personas: solo números sintéticos de prueba; no se ejecutó `POST /campanias/{id}/envios` |
| LLM | **Real** (`OpenRouter-Terra`, `openai/gpt-5.6-terra`), con autorización explícita de costo para las pruebas 1–8 |

### 1.1 Ventanas abiertas por el operador humano

El agente **no** tocó App Settings en ningún momento. El operador humano abrió y confirmó:

| App Setting | Estado inicial | Acción | Para qué |
|---|---|---|---|
| `Simulacion__Habilitada` | `false` (verificado por API: `/diagnostico/simulacion/*` → `404`) | → `true` (reinicio 02:41Z) | emitir OTP admin e inyectar entrantes; sin esto toda la corrida quedaba `BLOCKED` |
| `Conversacion__CatalogoTextosHabilitado` | `false` | → `true` | **requisito de la Prueba 5**: con el gate apagado, `OrquestadorConversacion.ResolverContenidoLlm` devuelve idioma fijo `"es"` y textos base, así que el hilo `en` respondía en español |

`GHT_DIAG_KEY` se recibió **solo como variable de entorno** y se usó exclusivamente como header
`X-Diag-Key`. No se leyó Key Vault, no se listaron secretos y su valor no se imprimió nunca.

### 1.2 Alcance autorizado y realmente ejecutado

Ejecutado: preparación puntuada completa (7 pasos), pruebas 1 a 8, escaneo determinista de toda la
evidencia visible, suite automatizada local y cierre de la campaña QA. No ejecutado: D5 (ver §6) y
cualquier migración de campaña real (fuera de la autorización, `Runbook §8`).

## 2. Datos creados

Todo lo creado es nuevo y trazable; no se reutilizó ni se editó ninguna entidad preexistente salvo
el usuario administrador de diagnóstico ya existente (`573001119999`, procedimiento estándar de QAS).

| Entidad | Id | Estado final |
|---|---|---|
| Familia de prompt QA | `qa_dt_i20_02_20260816_0243` — «QA DT-I20-02 texto plano I20-02-20260816-0243», tipo `evaluar` | v1 `activo`+aprobada, v2 `activo`+aprobada |
| Campaña aislada | `c_b99e643e801d4f45b1de723244941da2` — `CAMP-QA-DT-I20-02-20260816-0243` | **archivada** |
| Mensaje inicial | `mi_4862fa8ed3114c5880c09c516d079f1d` (`inicio_campania`) | activo |
| Pregunta 1 (usada en Pruebas 1–4, 6) | `p_be57d2f8383c4031af5206a5553babba` | activa |
| Pregunta 2 (usada en Pruebas 3–7) | `p_553505faa0eb4d79af2287f8e8354d69` | activa |
| Preguntas 3–8 (Pruebas 6 y 8) | `p_0263f95f…`, `p_5f4abd17…`, `p_e31c40d1…`, `p_fda4354f…`, `p_ffc61639…`, `p_325bbf73…` | activas |
| Participante `es` | `u_91155cb14b7a4b95a7702dfa835b6e45` (`5730011124·1`) | activo |
| Participante `en` | `u_81225ae5a99149f1bd02fd04f71d3b51` (`5730011124·2`) | activo |
| Participante `es2` (ver §5, nota operativa) | `u_9bc7384725a1462ebdc0c2fe47a4d811` (`5730011124·3`) | activo |

Recursos **reutilizados sin editar**: rúbrica `2` («rúbrica OpenBrain v3.4», v1, activa) y ConfigLLM
`llm_ed60b0a76908451c9c0913019d91b2d0` («OpenRouter-Terra», activa).

La campaña se creó **desde cero** (opción prevista por `QAS/21 §Preparación paso 5` cuando no puede
distinguirse inequívocamente una fuente de QA), bilingüe `es`/`en` con localizaciones completas para
las 8 preguntas y el mensaje inicial.

## 3. Preparación puntuada — **PASS**

| Paso | Resultado | Evidencia |
|---|---|---|
| 1. Inventario de solo lectura | PASS | Familia de evaluación vigente: **`1` v2, `activo`, aprobada por `admin`**. `promptRef` efectivo de las campañas activas: `promptRefs.evaluar = "1"` a nivel campaña, `{}` a nivel pregunta. No se editó nada |
| 2. Familia nueva como borrador | PASS | `POST /api/admin/prompts` → `201`, id `qa_dt_i20_02_20260816_0243`, v1, `borrador`, tipo `evaluar` |
| 3. Prevalidación por lectura | PASS | `GET` independiente: id nuevo, `version=1`, `estado=borrador`, `aprobadoPor=null`, `tipoPrompt=evaluar` y **contenido idéntico byte a byte** al bloque «Contenido candidato» (2 231 caracteres, `sha256` local = remoto = `4edd9c1019be0328…`). No se creó bajo la familia `1` ni nació activo/aprobado |
| 4. Aprobación | PASS | `POST /aprobar` → v1 queda **`activo` y `aprobadoPor=admin`**, `fechaAprobacion=2026-08-16T02:47:07Z`, confirmado por lectura posterior; el contenido no cambió al aprobar |
| 5. Campaña aislada | PASS | Nace `borrador`, id nuevo, **sin participantes**, nombre `CAMP-QA-DT-I20-02-20260816-0243`. Ninguna campaña fuente fue modificada |
| 6. Asociación efectiva | PASS | `promptRefs` **anteriores** anotados; luego campaña **y** preguntas apuntando a la familia nueva, confirmado por lectura (ver §4) |
| 7. Participantes y activación | PASS | Dos usuarios `es`/`en` con `idioma` correcto, localizaciones `es`/`en` completas, campaña `activa`, sin lote proactivo real |

## 4. `promptRefs` antes, durante y después del rollback

| Momento | `campania.promptRefs.evaluar` | `pregunta.promptRefs.evaluar` | Familia/versión efectiva en runtime |
|---|---|---|---|
| Anterior (anotado en la preparación) | `1` | *(vacío)* | `1` v2 |
| Durante las pruebas 1–7 | `qa_dt_i20_02_20260816_0243` | `qa_dt_i20_02_20260816_0243` (las 8 preguntas) | **`qa_dt_i20_02_20260816_0243` v1** |
| Prueba 8 con v2 en borrador | ídem | ídem | **v1** (el borrador no se usa) |
| Prueba 8 con v2 aprobada | ídem | ídem | **v2** |
| Después del rollback | `1` | *(vacío)* en las 8 preguntas | **`1` v2**, verificado por recorrido aislado |

## 5. Resultados por prueba

| Preparación/Prueba | es | en | Estado | Evidencia | Observación |
|---|---|---|---|---|---|
| Preparación puntuada | ✔ | ✔ | **PASS** | §3 | familia, versión, campaña, preguntas y participantes trazables; nada preexistente modificado |
| 1 — Reproducir el caso reportado | ✔ | n/a | **PASS** | `eval_9a947813…`, hilo `…5206a5553babba` | mensaje breve y natural, **una** pregunta, ligado a la idea aportada; **ninguno** de los elementos de falla |
| 2 — Sin cambio en la decisión de negocio | ✔ | n/a | **PASS** | `eval_9a947813…` | ver §5.1 |
| 3 — Una sola pregunta | ✔ | ✔ | **PASS** | escaneo de los 24 salientes | ningún saliente con más de una pregunta; ninguna retro trae pregunta cuando el turno envía repregunta |
| 4 — `caja #3` y formato del participante | ✔ | n/a | **PASS** | `idea_resp_d033205b…` | `caja #3` conservado literal en los 2 aportes y en las 2 versiones consolidadas; el texto de la idea no se alteró. P-33 no está habilitada en el ambiente (`VisibilidadIdeaParticipanteHabilitada` ausente ⇒ `false`), así que la consulta de idea no aplica |
| 5 — Español e inglés | ✔ | ✔ | **PASS** | hilos `…5206a5553babba` y `…2287f8e8354d69` del usuario `en` | ver §5.2 |
| 6 — Salidas y continuidad P-27 | ✔ | n/a | **PASS** | ver §5.3 | ambas frases de configuración honradas; el servidor decide el cierre |
| 7 — No duplicación I-20 | ✔ | ✔ | **PASS** | ver §5.4 | el puente nunca parafrasea el cuerpo insertado por el servidor |
| 8 — Rollback de prompt | ✔ | n/a | **PASS** | ver §5.5 | borrador ignorado, avance a v2 aprobada, rollback efectivo |
| D5 — calibración | — | — | **BLOCKED** | §6 | credencial no disponible en la sesión controlada |

### 5.1 Prueba 2 — la corrección no tocó la decisión de negocio

Mismo turno de la Prueba 1, leído por `/api/admin/evaluaciones/{id}` e `/api/admin/ideas/{id}`:

| Campo | Valor |
|---|---|
| `ideaId` / `versionIdeaId` | `idea_resp_4c852a5f…` / `…_v1` (versión 1, `confirmada`) |
| `origenTextoEvaluado` / `enlace` | `ideaConsolidada` / `enlazada` |
| Puntajes por criterio | Especificidad 2, Ajuste con Prioridades Actuales 1, Accionabilidad 2, Transferibilidad 2, Completitud 1 |
| `calificacionTotal` / `recomendacion` | `2` / `repreguntar` |
| Rúbrica | `2` v1 |
| `repreguntasUsadas` | `1` (una, la del turno) |
| `anomaliaSeguridad` | `false` |
| Estado del hilo | quedó `cerrada` **por pausa de inactividad P-29**, no por decisión del prompt |

No cambió la idea ni la versión evaluada, no se perdió ningún puntaje, no se consumió una repregunta
extra y el prompt no anunció ni forzó guardado o cierre.

**Cero respaldos neutros en toda la corrida:** en las 11 evaluaciones, ningún fragmento visible cayó
a `RetroNeutra` ni a `RepreguntaNeutra`. Es decir, el prompt candidato produjo texto válido por sí
mismo y la guarda de código (corte 1/3) nunca tuvo que corregir un síntoma. No se observó ningún
fallback, así que no hay motivo fijo que registrar.

### 5.2 Prueba 5 — español e inglés

Con el gate `Conversacion__CatalogoTextosHabilitado` en **ON**, el hilo `en` (conversación con
`idioma=en`) respondió íntegramente en inglés:

```
OUT: A one-hour booking window gives carriers a clear arrival process and targets a meaningful
     reduction in dock waiting time.

     Thank you. Your contribution has been recorded.
OUT: Let's continue with the next question:

     What would you change in your warehouse inventory handling?
```

Texto natural en el idioma del hilo, sin encabezados, listas ni etiquetas internas, máximo una
pregunta y sin mezcla de idiomas, en ambos idiomas. Evaluación `en`: `eval_…2838dec8`, prompt
`qa_dt_i20_02_20260816_0243` v1, total 4.65, `cerrar`.

> **Dependencia de ambiente comprobada en vivo (no es un defecto de `DT-I20-02`).** Con el gate en
> OFF, `ResolverContenidoLlm` devuelve `("es", …)` y `ResolverTextoMensajeInicialVisible` devuelve el
> texto base, de modo que un hilo `en` recibe la pregunta en español y el LLM recibe
> `IDIOMA_DE_SALIDA_OBLIGATORIO: es`. Quedó observado antes de abrir la ventana ON.

### 5.3 Prueba 6 — salidas y continuidad P-27

Desde un turno que esperaba mejora (`estadoMaquina=esperandoRepregunta`), con frases que viven en
configuración:

| Frase enviada | Lista | Efecto observado |
|---|---|---|
| `asi esta bien` | `Conversacion__FrasesContinuar__7` | «¡Perfecto, sigamos!» + cierre del servidor + siguiente pregunta; hilo `cerrada`, `repreguntasUsadas=1` |
| `cerremos esta idea` | `Conversacion__FrasesFinalizarIdea__10` | idéntico comportamiento de cierre y avance a la siguiente pregunta |

La intención del participante se honró en ambos casos, ninguna instrucción del prompt forzó otra
pregunta y el cierre lo decidió el servidor con su `mensajeCierre` configurado.

### 5.4 Prueba 7 — no duplicación I-20

Turno en el que el servidor inserta el cuerpo (la retroalimentación validada) entre el puente y la
pregunta del redactor:

```
Para precisar el impacto económico, enfoquémonos en ese control.      ← puente (redactor)
La propuesta ubica una mejora concreta en el proceso de despachos     ← cuerpo (servidor)
y facilita controlar lo que sale por el muelle.
¿Cómo reduciría costos verificar el peso de cada despacho?            ← pregunta (redactor)
```

Una sola formulación del mismo contenido: el puente aporta foco y no reaparece como segunda frase
casi idéntica. Verificado además sobre los 24 salientes, sin ningún caso de puente duplicado.

### 5.5 Prueba 8 — selección de versión en runtime y rollback

| Paso | Estado de la familia QA | Telemetría del recorrido efectivo |
|---|---|---|
| 3 — v2 en **borrador** | v1 `activo`+aprobada, v2 `borrador` | `13:20:34` `eval_…1e4e3edb` → **`qa_dt_i20_02_20260816_0243` v1** |
| 4 — aprobar v2 | v1 y v2 `activo`+aprobadas | lectura confirma `version=2`, `estado=activo`, `aprobadoPor=admin` |
| 5 — v2 activa y aprobada | ídem | `13:24:07` `eval_…b00ff8a9` → **v2** |
| 6/7 — rollback de ambos niveles | familia QA intacta | `13:25:47` `eval_…9f65f562` y `13:25:55` `eval_…2bbc0d0d` → **familia `1` v2** |

Runtime nunca intentó usar el borrador, avanzó a la v2 solo cuando quedó activa **y** aprobada, y el
rollback de `promptRefs` en campaña **y** preguntas devolvió el recorrido efectivo a la familia
anterior. Es exactamente el comportamiento que el corte 2/3 prometía.

### 5.6 Escaneo determinista de toda la evidencia visible

Sobre **24 mensajes salientes** y **11 evaluaciones** (retro + repregunta), buscando anclado a inicio
de línea la estructura editorial y en cualquier posición las etiquetas y órdenes de proceso:

| Chequeo | Resultado |
|---|---|
| Encabezados `#`, viñetas, listas numeradas, citas, separadores, tablas, cercas de código | **0 hallazgos** |
| `Lo que ya queda claro`, `Lo que todavía falta`, `Siguiente ajuste recomendado`, `Pregunta clave` | **0 hallazgos** |
| `Estado`/`Resumen`/`Status`/`Summary` como línea completa o abriendo con dos puntos | **0 hallazgos** |
| `ready_to_save`, `save now`, `listo para guardar` | **0 hallazgos** |
| Claves del contrato JSON (`retroalimentacion_usuario`, `repregunta_sugerida`) | **0 hallazgos** |
| Mención de rúbrica/criterio/calificación y patrón de puntaje `n/m` | **0 hallazgos** |
| Más de una pregunta en un saliente | **0 hallazgos** |
| Retro con pregunta cuando el turno envía repregunta | **0 hallazgos** |
| Fragmentos sustituidos por respaldo neutro | **0** |

## 6. D5 — calibración · **BLOCKED**

**Causa concreta:** `CALIBRACION_API_KEY` no estuvo disponible en la sesión controlada (verificado
tres veces durante la corrida). Por indicación explícita del responsable, el agente **no** solicitó,
buscó, leyó de Key Vault ni recibió la clave. Sin credencial autorizada no se ejecutó ninguna llamada
real de calibración y no se inventó evidencia.

Queda todo listo para una ejecución humana controlada. Las dos tripletas están construidas desde la
configuración real (mismo modelo, misma rúbrica, misma campaña y pregunta; solo cambia el prompt) y
**no contienen ninguna credencial** — `apiKeyRef` es solo el nombre canónico del secreto:

| Tripleta | Prompt | Tamaño del contenido |
|---|---|---|
| `d5-triplet-anterior.json` | familia `1` v2 | 7 222 caracteres |
| `d5-triplet-candidata.json` | `qa_dt_i20_02_20260816_0243` v2 | 2 231 caracteres |

Ambas: `openai/gpt-5.6-terra`, `temperature 0.2`, rúbrica `2` v1 (escala 1–5), `n=2` sobre las 24
entradas del golden set ⇒ ~48 llamadas por familia. Ruta de los archivos (fuera del repositorio, por
higiene: contienen el texto íntegro de los prompts):
`%LOCALAPPDATA%\Temp\claude\C--Users-JasonPerezCarvajal-…-GHT-Tejido-de-la-red\76a9a70a-0bbd-4653-85a9-5ec982ca4db3\scratchpad\`.

Comando exacto, una vez por familia:

```bash
export CALIBRACION_CONFIG=<ruta>/d5-triplet-anterior.json    # luego -candidata.json
export CALIBRACION_API_KEY=****                              # solo en la sesión, nunca en disco
export CALIBRACION_OUT=<ruta>/salida-anterior                # y salida-candidata
dotnet test tests/ElTejido.IntegrationTests -c Release --filter "Category=Calibracion"
```

No existe `tests/Calibracion/baseline.json`, así que el runner no comparará contra baseline: hay que
contrastar los dos reportes generados (calidad por eje, decisión, % de salida inválida, tokens,
costo y latencia). **No congelar baseline** sin decisión explícita.

Dato observable sin ejecutar D5: el prompt candidato es **~69 % más corto** que el de la familia `1`
(2 231 vs 7 222 caracteres), lo que apunta a menos tokens de prompt por evaluación; confirmarlo es
justamente parte de D5.

## 7. Suite automatizada

Ejecutada localmente sobre el mismo commit desplegado (`54eb9db`), `dotnet test ElTejido.sln -c Release`:

| Suite | Resultado |
|---|---|
| `ElTejido.UnitTests` | **925 passed, 0 failed, 0 skipped** |
| `ElTejido.IntegrationTests` | **112 passed, 0 failed, 0 skipped** |

## 8. Estado final y confirmaciones

- **Campaña QA `CAMP-QA-DT-I20-02-20260816-0243`: `archivada`**, por la transición soportada
  `activa → cerrada → archivada`. No quedó activa. No se borró nada.
- **Familia `1` intacta:** v2, `activo`, `aprobadoPor=admin` — idéntica al inventario inicial.
- **Ninguna campaña preexistente cambió de estado:** comparación de las 11 campañas previas antes y
  después de la corrida, sin diferencias. La única campaña nueva es la de esta corrida.
- Rúbricas (`1`, `2`) y ConfigLLM (dos) siguen `activa`s y sin editar.
- La familia QA se conserva con sus dos versiones para auditoría, como pide la guía.
- **Pendientes del operador humano (cierre obligatorio):** volver `Simulacion__Habilitada` a `false`,
  volver `Conversacion__CatalogoTextosHabilitado` a `false` y retirar `GHT_DIAG_KEY` de la sesión.
  Confirmar el retorno leyendo readiness **después** de que complete el reinicio.
- El único archivo nuevo del repositorio es este reporte.

## 9. Hallazgos operativos (no son defectos de DT-I20-02)

1. **Cierre por inactividad entre lotes.** Los hilos se cerraron a los ~20 minutos pese a
   `minutosInactividadSesion=180` en la campaña; el App Setting global
   `Conversacion__MinutosInactividadSesion=20` fue el que gobernó. Práctica para futuras corridas:
   ejecutar cada secuencia conversacional sin pausas y en un solo proceso.
2. **Los menús de P-26 no son mensajes de conversación.** Cuando un participante agota su recorrido,
   `EnviarMenuPreguntasAsync` envía el menú directamente por el gateway, fuera de cualquier
   conversación, así que **no aparece en `/api/admin/conversaciones`** y con un número sintético es
   invisible para el agente de pruebas: el hilo parece «mudo». Costó un rato de diagnóstico. Por eso
   las pruebas 6 y 8 se corrieron con un tercer participante (`es2`) en vez de reiniciar datos, para
   **no borrar la evidencia** de las pruebas 1 a 4 y 7.
3. **Agregar preguntas a una campaña no rescata a un participante que ya agotó su recorrido:** queda
   en el menú de selección de P-26, no en el flujo secuencial.
4. La rúbrica `2` declara un único criterio (`Impacto`, peso 1) en su registro, mientras el modelo
   devuelve cinco ejes tomados del markdown. Es previo a esta corrida y ajeno a `DT-I20-02`, pero
   conviene revisarlo antes de leer D5.

## 10. Conclusión

El prompt candidato corrige la **causa** del defecto reportado: en 24 mensajes salientes y 11
evaluaciones con LLM real, en español e inglés, no apareció un solo encabezado, etiqueta interna,
orden de guardado ni pregunta duplicada, y la retroalimentación se lee como un mensaje de WhatsApp.
Las decisiones de negocio —idea, versión, puntajes, madurez, presupuesto de repreguntas, cierres—
quedaron intactas, y la guarda de código no tuvo que sustituir ni un fragmento.

La selección de versión en runtime (corte 2/3) se comportó exactamente como se especificó: ignora el
borrador, avanza solo a la versión activa **y** aprobada, y el rollback de `promptRefs` en campaña y
pregunta devuelve el recorrido a la familia anterior.

**Las ocho pruebas están en PASS y las regresiones automáticas están verdes.** Para el cierre
funcional de la deuda falta únicamente **D5**, que requiere una ejecución humana controlada con la
credencial autorizada. Hasta entonces, y conforme al `Runbook §8`, **no se migra ninguna campaña
real**: la autorización de esta corrida termina aquí.
