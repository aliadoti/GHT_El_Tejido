# Resultados — P-32 Multidioma / Catálogo de Textos · 2026-08-12/13

## Ambiente, fecha, ejecutor y autorización

| Campo | Valor |
|---|---|
| Ejecutor | Claude Code (Sonnet 5), sesión en background |
| Fecha de ejecución | 2026-08-12 → 2026-08-13 (timestamps UTC de la corrida: 2026-08-13T00:2x–00:3x) |
| Ambiente probado | **Local, `ASPNETCORE_ENVIRONMENT=Development`**, `dotnet run --project src/ElTejido.Api`, persistencia **en memoria** (efímera; se perdió al detener el proceso) |
| Azure (`app-eltejido-mvp`) | **No se probó en vivo.** Se confirmó que el build de hoy (deploy `2026-08-12T23:24:33Z`, activo) sí expone P-32 (`/api/admin/catalogos-textos` → `401`, ruta real es `catalogos-textos` con "s", no `catalogo-textos`), pero el filtro `FiltroClaveDiagnostico` exige `X-Diag-Key` fuera de Development para crear el admin de prueba, emitir OTP e inyectar mensajes simulados. El usuario decidió explícitamente no proveer esa clave ("usa otra vía sin la clave"), así que la validación se movió a Local, que no exige esa clave por diseño. |
| Autorización | Jason Perez C (dueño de la sesión) autorizó: crear usuarios y campaña bilingüe nuevos, reutilizar la lógica de rúbrica/prompt/config LLM ya existente (recreada localmente porque Azure no era alcanzable sin la clave de diagnóstico), y **simular** la interacción de WhatsApp porque las plantillas Meta en inglés **no están aprobadas todavía**. No se autorizó ni se usó la key real del LLM (`llm-key` en Key Vault): el clasificador de permisos bloqueó mi intento de leerla y el usuario no la proporcionó. |
| Participantes de prueba | `U-000002` es `573001112401`, `U-000003` en `573001112402`, `U-000004` es→en `573001112403` (Prueba 5), `U-000004`(campaña 2) en `573001112404` (Prueba 6). Sin datos reales. |
| Campaña principal | `CAMP-QA-P32-BILINGUE` (`c_066e3b71d2874e4b9facae4d7f9d7efc`), bilingüe es/en completa |
| Campaña de Prueba 6 | `CAMP-QA-P32-INCOMPLETA` (`c_27b9f8a9e490427da3292330970b488c`), `en` habilitado a propósito sin contenido |
| Catálogo | `familiaId=catalogo_conversacion`; `es` v1 (semilla, activa); `en` v1 (semilla) → v2 (editado, activada) → intento de rollback a v1 **rechazado** (ver Prueba 4) |
| Rúbrica/Prompt/ConfigLLM | `RUB-QA-P32`, `PR-EVALUAR-QA-P32` (aprobado), `LLM-QA-P32` con `apiKeyRef` **ficticia** (`llm-key-test`, valor de prueba local, sin presupuesto real ni relación con el `llm-key` real de Key Vault) |
| Estado final del gate | **OFF.** `appsettings.Development.json` revertido (sin diff en git) y el proceso local detenido; no queda ningún proceso ni dato vivo. Azure nunca tuvo el gate encendido — no se tocó ningún App Setting en la nube. |
| Costo/latencia LLM | **$0 / sin llamadas reales.** La key de configuración era ficticia; toda evaluación cayó en fallback neutro (`motivoCierre=fallbackEvaluacion`), documentado como comportamiento correcto (`QAS/06 §7`), no una medición de calidad. |

## Hallazgo previo relevante (corregido en la sesión)

Al inicio interpreté mal la ruta del catálogo (`catalogo-textos` en vez de `catalogos-textos`) y reporté por error que P-32 no estaba desplegado en Azure. El usuario corrigió esto; con la ruta correcta el endpoint responde `401` (existe). El deploy de hoy sí lo incluye. El bloqueo real para probar contra Azure fue la falta de `X-Diag-Key`, no un problema de despliegue.

## Tabla de resultados

| Prueba | es | en | Estado | Evidencia | Observación |
|---|---|---|---|---|---|
| 0 — Snapshot idioma, gate OFF | — | `conv...idioma:"en"` con gate `false` | **PASS** | `conversacionId=conv_..._u_7c74df3e..._p_4ee1...`, `idioma:"en"` mientras `CatalogoTextosHabilitado=false` | El idioma queda fijado en el hilo independientemente del gate, como exige la spec §5 |
| 0.1/0.2/0.3 — técnicas (mensajes globales, menú con snapshot, comandos en inglés) | — | — | **PARCIAL / no aisladas** | Cubiertas indirectamente por Prueba 1 y por `GET /catalogos-textos/efectivo?idioma=en` (`origen:"catalogo"`) | No se armaron los sub-casos exactos (menú de campaña doble, aclaración P-27 en inglés) por límite de tiempo; el mecanismo base (catálogo activo sirviendo `en`) sí se confirmó |
| 1 — Mismo recorrido, dos idiomas | `nivelMadurez:"incubacion"`, `motivoCierre:"fallbackEvaluacion"` | idéntico | **PARCIAL** | `idea_resp_447663b5...` (es) vs `idea_resp_d59eee19...` (en): misma decisión determinista para ideas equivalentes | Idioma y reglas deterministas iguales en ambos idiomas ✔. Calidad real de coaching/redacción **no verificada** (key LLM ficticia → fallback neutro en ambos, no hubo generación real) |
| 2 — Lote mixto WhatsApp | — | — | **BLOCKED (parcial PASS estructural)** | Campaña con `plantillaRef:"inicio_campania"` distinto por idioma persistido correctamente (`GET /campanias/{id}`) | Plantillas Meta en inglés no aprobadas (confirmado por el usuario) y sin `wa-token` local ⇒ no se pudo confirmar el envío real ni la selección de plantilla en tiempo de envío |
| 3 — Cambiar texto sin desplegar | — | v1→v2 activada en caliente | **PASS** | `efectivo?idioma=en` pasó de v1 a v2 (`saludoPrimerContacto` editado) sin build/deploy/reinicio; `efectivo?idioma=es` no cambió | Confirma AC #4 y #12 (aislamiento por idioma) |
| 4 — Validación y rollback | — | — | **FAIL** | Validación: `PUT .../versiones/3` con campos vacíos → `400 VALIDATION_ERROR` con detalle por campo, v2 activa intacta ✔. Rollback: `POST .../versiones/1/activar` (v1 en estado `inactivo`) → `409 CONFLICT "Solo una version en borrador puede activarse."` | **Defecto reproducible**: `ServicioGestionCatalogosTextos.ActivarAsync` exige `Estado==Borrador`; el botón "Reactivar esta versión" del portal (`catalogos-textos.page.ts:97-99,228`) llama al mismo endpoint sobre una versión `inactivo` sin clonarla primero a borrador. El rollback documentado en `QAS/16` Prueba 4 y en AC #5 de la spec **no funciona tal como está implementado** |
| 5 — Cambio de idioma del maestro | hilo abierto conservó `es` | maestro cambiado a `en` a mitad del hilo | **PASS** | `conv..._u_e36d2aa8...`: `idioma:"es"` antes y después de `PUT /usuarios/{id}` a `en`; el hilo cerró en `es` | Confirma AC #9. No se verificó el "ciclo nuevo" porque `participacionContinua=false` en la campaña de prueba impidió abrir un segundo ciclo para el mismo participante (comportamiento esperado, no defecto) |
| 6 — Campaña incompleta | — | `en` habilitado, 0% contenido | **FAIL CRÍTICO** | `PATCH /campanias/{id}/estado {"activa"}` con `en` habilitado y `localizaciones:{}` → `200 activa` (sin bloqueo). `POST /participantes` de un usuario `en` a esa campaña → `200` (sin `409 IDIOMA_CAMPANIA_NO_HABILITADO`). Conversación `en` se abrió igual (`estadoMaquina:"esperandoRespuestaInicial"`) | **Ninguna de las 3 capas de protección de la spec §10 actuó**: ni la activación de campaña, ni la asociación de participante, ni la apertura de conversación bloquearon el faltante de contenido inglés. Viola directamente AC #8 ("nunca entrega silenciosamente español a un usuario en") |
| 7 — D5 real (calidad LLM) | — | — | **BLOCKED** | N/A | La key real del LLM (`llm-key`, Key Vault) no se pudo leer (bloqueada por el clasificador de permisos) ni fue provista por el usuario. El banco formal `tests/Calibracion` (N repeticiones, comparación de regresión, reporte de costo/tokens) no se ejecutó — requiere `CALIBRACION_API_KEY` y presupuesto explícito, no otorgados. La comparación ad-hoc realizada (Prueba 1) usó una key ficticia y solo probó el camino de fallback, no calidad real del modelo |
| 8 — UAT bilingüe (GHT) | — | — | **BLOCKED** | N/A | Requiere personas reales de GHT completando el recorrido sin conocer la respuesta esperada; no hay personal disponible en esta sesión de agente |

## Activación / rollback y campaña incompleta (detalle)

- **Activación de versión de catálogo (Prueba 3):** funciona correctamente y en caliente.
- **Rollback (Prueba 4):** **roto**. Ver fila de la tabla; es un bug de código, no una limitación del ambiente de prueba — reproducible también contra Azure una vez desplegado, porque la regla vive en `ServicioGestionCatalogosTextos.ActivarAsync` (`src/ElTejido.Application/Configuracion/ServicioGestionCatalogosTextos.cs:174-177`) y el portal (`src/ElTejido.Web/src/app/features/catalogos-textos/catalogos-textos.page.ts:228-239`) no compensa esa restricción.
- **Campaña incompleta (Prueba 6):** **roto**. Bug de código en la capa de validación de activación/asociación de campañas (`IServicioGestionCampanias` / `ServicioGestionCampanias`, no revisado línea por línea por límite de tiempo, pero el comportamiento observado en la API contradice `Especificaciones/Iniciativas/P-32_Conversacion_Multidioma_y_Catalogo_Textos.md §10`).

## Reporte D5, costo/tokens/latencia

No hay reporte D5 real. Costo observado: **$0** (key ficticia, sin llamadas reales al proveedor). La comparación es/en disponible es solo estructural (ver Prueba 1/7): mismas decisiones deterministas de fallback en ambos idiomas, lo cual es evidencia parcial de AC #11 pero no sustituye la validación de calidad exigida por `QAS/16` Prueba 7.

## Decisión UAT de GHT

**Pendiente / no ejecutada.** No hubo personal de GHT disponible en esta sesión.

## Recomendación final

## **NO ACTIVAR**

Razones, en orden de severidad:

1. **Prueba 6 (crítico):** una campaña bilingüe puede quedar activa y recibir participantes en un idioma sin ningún contenido localizado, sin bloqueo en ninguna de las tres capas esperadas. Esto viola el criterio de aceptación más importante de P-32 (AC #8: nunca entregar español silenciosamente a un usuario `en`). Debe corregirse y volver a probarse antes de cualquier otra consideración.
2. **Prueba 4 (alto):** el rollback de una versión de catálogo —la operación de contingencia central del plan de rollback (`spec §14.2`)— falla con `409 CONFLICT` tal como está implementado en el portal y la API. Si un texto editorial sale con un error, hoy **no hay forma de revertirlo desde el portal** sin decir a un admin que edite manualmente una nueva versión.
3. **Bloqueos externos, no técnicos:** D5 real, UAT de GHT y plantillas Meta aprobadas siguen pendientes, como ya se sabía antes de esta corrida.

Con los hallazgos 1 y 2 corregidos y reprobados, y una vez resueltos los tres bloqueos externos, se puede reintentar esta validación.

## Corrección local posterior — 2026-08-13

Los dos defectos fueron corregidos localmente y requieren repetición controlada de las Pruebas 4 y 6:

1. **Rollback:** una versión `inactiva` puede volver a activarse con su ETag; la transición es atómica,
   invalida caché y queda auditada como `rollback` sin registrar textos.
2. **Campaña incompleta:** una campaña que declara `en` se valida al activarse aun con el gate runtime
   apagado; asociar a esa campaña incompleta devuelve `409 CAMPANIA_IDIOMA_INCOMPLETA` y el
   enrutamiento excluye datos históricos inconsistentes.

Evidencia local de la corrección: build Release sin warnings y 858 pruebas no-Calibracion verdes
(771 unitarias + 87 integración). Este resultado no cambia los bloqueos externos de D5 real, UAT de
GHT y plantillas Meta inglesas, ni autoriza activación.

## Confirmación de cierre

- No hubo push, despliegue, cambio de secretos productivos ni carga de datos reales.
- No se usó el App Secret de Meta ni la key real del LLM.
- El gate permaneció **apagado en Azure en todo momento** (nunca se tocó `app-eltejido-mvp`); en local quedó apagado y el proceso se detuvo (persistencia en memoria, sin datos remanentes).
- `git status` no muestra diferencias en archivos de configuración tras la corrida.

## Resumen (máximo 10 líneas)

**Hechos verificados:** idioma se fija por hilo independientemente del gate (P0) y sobrevive a un cambio del maestro a mitad de conversación (P5); editar/activar un texto en caliente funciona sin build/deploy y no cruza idiomas (P3); las decisiones deterministas de fallback son idénticas en es/en (P1, parcial). **Defectos encontrados (bloqueantes):** el rollback de catálogo ("Reactivar esta versión") devuelve `409` porque la API solo permite activar versiones en borrador, no inactivas (P4); una campaña bilingüe incompleta se activa, acepta participantes del idioma faltante y abre conversación sin ningún bloqueo (P6) — viola el AC de no filtración silenciosa de español. **Bloqueos externos:** D5 real y el banco formal `tests/Calibracion` requieren la key real del LLM (no autorizada ni disponible); UAT requiere personal de GHT; el envío real por WhatsApp requiere plantillas Meta aprobadas (no lo están) y `wa-token`. **Decisión que requiere humano:** priorizar el fix de P6 (crítico) y P4 antes de reintentar cualquier otra prueba; decidir si/cuándo se autoriza usar la key real del LLM para D5.
