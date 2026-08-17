# Resultados P-32 multidioma — validación DT-P32-04 (2026-08-16, corrida `P32-20260816-1955`)

> **Resultado global: BLOCKED — NO ACTIVAR.** El artefacto con `DT-P32-04` corte 3/3 **sí está
> desplegado** y se verificó directamente sobre él el **readiness compuesto**, las guardas de
> activación bilingüe y toda la cadena de edición masiva de catálogos. Quedan BLOCKED todos los
> recorridos conversacionales, el lote mixto real, D5 y UAT, y además **el gate quedó ON**, estado que
> el agente no puede cambiar y que contradice la precondición de `QAS/22`/`QAS/23`.

> **Relación con el reporte previo del mismo día:** `Resultados_P32_Multidioma_2026-08-16.md` (corrida
> de las 19:18) concluyó BLOCKED porque DT-P32-04 solo existía como cambios sin confirmar. Esa premisa
> ya no aplica: el corte fue confirmado (`28c3cb1`), publicado en `origin/main` y desplegado. Este
> archivo usa un nombre con sufijo de corrida para **no sobrescribir aquella evidencia**; es la única
> desviación respecto del nombre prescrito en `QAS/17`.

---

## 1. Ambiente, fecha, ejecutor y autorización

| Campo | Valor |
|---|---|
| Fecha de ejecución | 2026-08-16, 19:33–20:05 (UTC-05:00) |
| Ejecutor | Agente QA (Claude Code), invocado con la frase corta de `QAS/17` §Invocación |
| Identificador de corrida | `P32-20260816-1955` |
| Ambiente | **Azure `app-eltejido-mvp`** — `https://app-eltejido-mvp-…eastus-01.azurewebsites.net`. `GET /health` → `200 {"status":"ok"}` |
| `GET /health/ready` | `200`, estado `ok`: 11 componentes, 10 en `ok` y 1 en `no_aplica` (`conversacion:umbralResumenConsolidacion`) |
| Suscripción | `ghtfalcon-dev-1`. **`az webapp list` devuelve un único App Service**: no existe un ambiente aislado separado del MVP |
| Autorización recibida | Frase de invocación + `GHT_DIAG_KEY` presente en el proceso + `Simulacion__Habilitada=true` verificado activo (el operador preparó la ventana de simulación) |
| Autorización **no** recibida | Confirmación escrita de ambiente aislado; aislamiento del emisor WhatsApp o números de prueba autorizados; ventana de gate; plantillas Meta inglesas aprobadas; presupuesto/credenciales D5; participantes UAT de GHT |
| Cambios remotos de configuración | **Ninguno.** Sin push, sin despliegue, sin App Settings, sin secretos |
| Cambios de código | **Ninguno.** No se corrigió nada durante la corrida |
| Mensajes enviados | **Ninguno.** Cero llamadas a `/api/admin/envios`, cero inyecciones al webhook simulado, cero `wamid` observados |

### 1.1 Manejo de `GHT_DIAG_KEY`

Se leyó **solo** desde el entorno del proceso y se envió **solo** como header `X-Diag-Key`, en dos
rutas: `GET /health/ready` y `POST /diagnostico/simulacion/{admin-inicial,otp-admin}`. Su valor no fue
mostrado, escrito en archivo, comando visible ni reporte, y no se intentó obtenerla de Key Vault, App
Settings ni del navegador. El App Secret de Meta y la key de OpenRouter no se solicitaron ni se usaron.

---

## 2. Artefacto desplegado — **confirmado con DT-P32-04 corte 3/3**

`QAS/17` regla 1 exige confirmar que el ambiente contiene el corte 3/3. Se confirmó por inspección
directa del artefacto publicado, no por inferencia de fechas:

| Comprobación | Resultado |
|---|---|
| `git rev-parse HEAD` / `origin/main` | Ambos `28c3cb1d5f8a20c4ae92d6aa4c45360cd1100cfe` — «DT-P32-04 multiidioma refactoring»; 0 commits de diferencia, árbol limpio |
| Despliegue activo (ARM `…/deployments`) | `b4d74484-1811-44cd-ba35-20eeb06a1fac`, `active:true`, `complete:true`, `end_time 2026-08-17T00:26:57Z`, deployer `OneDeploy` |
| `ElTejido.Domain.dll` publicado (lectura Kudu VFS, solo GET) | Contiene `IdiomaConversacion`, `CodigosSoportados`, `IdiomasInternosHabilitados`, namespace `Localizacion`. Tamaño **132 608 B**, idéntico al build local Release de `28c3cb1` |
| `ElTejido.Application.dll` publicado | Contiene `ContenidoCampaniaEfectivo`, `ResolutorTextosGlobales`, `IResolverPlantillaCanal`, `PoliticaIdiomaLlm`, `IReadinessMultiidioma`, `ModoResolucionTextosGlobales`, `TipoDirectivaIdiomaLlm`. Tamaño **1 078 272 B**, idéntico al build local Release de `28c3cb1` |

Los siete tipos verificados son exactamente los que `DT-P32-04` §3.4 y §3.5 introducen en el **corte
3/3** (fachadas especializadas, política LLM y readiness compuesto). La confirmación es técnica y
verificable; **no sustituye la confirmación formal del operador** exigida por `QAS/18` §5, que sigue
pendiente por escrito.

---

## 3. Gate local del checkout (`QAS/17` regla 5) — **PASS 4/4**

Ejecutado secuencialmente sobre el checkout, sin modificar código:

| # | Comando | Resultado | Evidencia |
|---|---|---|---|
| 1 | `dotnet build -c Release -warnaserror` | **PASS** | `Build succeeded. 0 Warning(s), 0 Error(s)`; 7 proyectos; 31,71 s; exit `0` |
| 2 | `dotnet test -c Release --no-build --filter "Category!=Calibracion"` | **PASS** | `ElTejido.UnitTests`: Failed 0, Passed **1030**, Skipped 0. `ElTejido.IntegrationTests`: Failed 0, Passed **120**, Skipped 0. exit `0` |
| 3 | `dotnet format --verify-no-changes --no-restore` | **PASS** | exit `0` |
| 4 | `git diff --check` | **PASS** | exit `0` |

Los conteos **1030 + 120** coinciden con el cierre declarado del corte 3/3 en
`DT-P32-04_Nucleo_Transversal_Multidioma.md` §8.3. Ningún fallo local detuvo la validación remota.

---

## 4. Datos de la corrida

Creados por esta corrida, **conservados** para auditoría (no se borró nada, ni de esta corrida ni de
las anteriores):

| Tipo | Nombre | Id | Detalle |
|---|---|---|---|
| Usuario | `P32-20260816-1955 ES` | `u_3f0d5161de264199809a3707559d1db5` | idioma `es`, activo, participante, tel. …**3901** |
| Usuario | `P32-20260816-1955 EN` | `u_e8151e955cb24c81ab793afc9e017f18` | idioma `en`, activo, participante, tel. …**3902** |
| Usuario | `P32-20260816-1955 EN-RESERVA` | `u_edf49d67dd314cd986d797980406e0d6` | idioma `en`, activo, participante, tel. …**3903** |
| Campaña | `CAMP-P32-20260816-1955-COMPLETA` | `c_928e4e8c4ce942629594b14fbc3f0973` | **activa**, `es/en` completos, 2 participantes asociados |
| Campaña | `CAMP-P32-20260816-1955-INCOMPLETA` | `c_bd3bc8bf522c4f54acb871215ca78db7` | **borrador**, `en` vacío a propósito, 0 participantes |
| Catálogo | `catalogo_conversacion` / `en` v5 | — | borrador nuevo creado por la importación masiva |

Los tres teléfonos se consultaron previamente (`GET /api/admin/usuarios/por-numero/…` → 0 resultados);
son nuevos y están fuera de los rangos reservados en `QAS/datos/` (`5730011122xx`, `573009999999`) y de
los usados en corridas anteriores. **Solo se registran los últimos cuatro dígitos.**

Administrador usado: el de diagnóstico ya existente (`…9999`, `u_admin_ec4a11b2…`, «Administrador QA»),
conforme a `QAS/17` §6a. No se creó ningún administrador nuevo.

---

## 5. Recursos LLM reutilizados sin editar (`QAS/17` §6c) — **verificado**

| Recurso | Encontrado | Estado | Unicidad |
|---|---|---|---|
| Rúbrica `rúbrica OpenBrain v3.4` | `id=2`, v1 | **activa** | única con ese nombre |
| Prompt `Evaluación con rubrica OpenBrain Thought-Scoring` | `id=1`, v2 | **activo** | único con ese nombre |
| Config LLM `OpenRouter-Terra` | `id=llm_ed60b0a76908451c9c0913019d91b2d0` | **activa** | única con ese nombre |

Los tres se **referenciaron** desde las campañas de prueba y **no se modificaron**. No se solicitó ni
manipuló la key de OpenRouter.

---

## 6. Versiones/huellas de catálogo y plantillas Meta

| Idioma | Versiones al cierre | Activa | Huella activa (prefijo) |
|---|---|---|---|
| `es` | v4 borrador, v3 borrador, v2 inactivo, **v1 activo** | v1 | `2e30d0c6fbf6926f…` |
| `en` | **v5 borrador (nueva)**, v4 inactivo, v3 borrador, v2 inactivo, **v1 activo** | v1 | `ceee6b460d308d1a…` |

Nueva versión creada: `en` **v5**, estado `borrador`, huella `bcbec42079b80893…`, con `ETag` devuelto.
**Ninguna versión fue activada ni desactivada.** Límites efectivos: `maxFrasesPorGrupo=100`,
`maxBytesImportacionJson=262144`.

Mapeos Meta (estructura reportada por readiness; la API no certifica el nombre físico):

| plantillaRef | idioma | configurado | bloqueaGateOn | componentes | problemas |
|---|---|---|---|---|---|
| `inicio_campania` | `es` | sí | **sí** | `[nombre]` | — |
| `inicio_campania` | `en` | sí | **sí** | `[nombre]` | — |
| *(vacío)* | `en` | no | no | — | `plantilla_ref_faltante` |
| `smoke_sin_mapeo` | `es` | no | no | — | `nombre_faltante`, `idioma_meta_faltante` |
| `smoke_sin_mapeo` | `en` | no | no | — | `nombre_faltante`, `idioma_meta_faltante` |

No se configuró ni modificó ningún App Setting `WhatsApp__PlantillaEnvioInicial__Mapeos__…`. **La
verificación humana en Meta (aprobación, nombre físico, orden y significado de variables) no se
ejecutó y sigue pendiente**; readiness no la reemplaza.

---

## 7. `QAS/22` — semillas, JSON masivo, prevalidación y readiness

| Prueba | Estado | Evidencia / motivo |
|---|---|---|
| 1 — semilla base independiente del legacy | **BLOCKED** | La precondición es «ambiente sin catálogo persistido», pero `es` y `en` ya tienen activo válido. Reproducirla exigiría desactivar/borrar una activa, prohibido por `QAS/17` §6d. Cubierta por la suite automática (verde local) |
| 2 — preview de configuración anterior | **PASS** | `GET …/semillas/{es,en}/legacy/preview` → `valido=true`, `es`: 29 mensajes / 16 grupos / 176 frases; `en`: 29 / 16 / 88; cero errores. Export legacy completo (8 594 B `es`, 6 135 B `en`), grupo mayor 31 (`es`) y 14 (`en`), sin truncar. **Sin versiones nuevas** antes/después |
| 3 — descargar JSON para edición masiva | **PASS** | `en` v3 → `formato=catalogo-textos/v1`, `familiaId=catalogo_conversacion`, `idioma=en`, 29 mensajes, 16 grupos, indentado, `Content-Disposition: …-en-v3-editable.json`. `metadatos` informativos (versión/estado/huella). Sin secretos, sin plantillas físicas Meta, sin datos de participantes |
| 4 — editar y cargar masivamente | **PASS** | 2 mensajes modificados + grupo `continuar` 9→10 (modifica y agrega) + grupo `confirmar` 14→13 (retira), sin cambiar claves. Prevalidación `valido=true`, 29/16/88, 0 errores. `POST /importar` → **201**, **v5 `borrador`**, huella nueva, `ETag` presente. Origen v3 y activa v1 conservan su huella `ceee6b46…`. Sin compilar ni desplegar |
| 5 — errores completos y cero escritura | **PASS** | 7/7 detectados con campo/motivo: `mensajes.encabezadoConsultaIdea=vacio`; `mensajes.claveQueNoExiste=clave_desconocida`; `mensajes.encabezadoCierreIdea=placeholder_no_permitido:placeholderInventado`; `frases.continuar=frase_duplicada`; `frases.continuar=debe_tener_entre_1_y_100_elementos`; `idioma=no_coincide_con_seleccion`; `formato=no_soportado`. Todos `200 valido=false`, **cero versiones creadas**. El sub-paso de reseleccionar el mismo archivo es conducta del portal, no verificable por API |
| 6 — permisos y auditoría | **BLOCKED** | No es posible autenticar un `visor`: `otp-admin` exige rol `admin` y `request-code` enviaría un OTP real por WhatsApp. Ningún endpoint admin expone la auditoría para inspeccionarla. Evidencia parcial de las guardas: mutación sin `X-CSRF-Token` → **403 FORBIDDEN**; petición sin sesión → **401 UNAUTHENTICATED** |
| 7 — readiness real | **PASS (parcial)** | Readiness distingue borrador/activo y refleja el **gate real** (`gateHabilitado=true`, coincide con el App Setting). `GET …/efectivo` expone solo `origen`+`catalogo` (`origen=catalogo`, v1) y **no reporta el gate por sí solo**. Idioma no soportado (`fr`) → `400 idioma=valor_invalido` por la política única de `IdiomaConversacion`. **No ejecutado**: activar explícitamente `es`/`en`, porque ambos ya están activos y activar una versión nueva **con el gate ON** sería un cambio editorial en vivo |
| 8 — campaña bilingüe protegida | **BLOCKED** | Exige dejar un idioma sin catálogo activo, es decir desactivar una activa: prohibido. Evidencia equivalente parcial en la §9 (activación y asociación rechazadas por localización incompleta) |
| 9 — regresión y corrida P-32 | **BLOCKED** | Requiere gate OFF, que el agente no puede establecer |

---

## 8. `QAS/23` — cierre localizado y readiness Meta (revalidación sobre el nuevo artefacto)

`QAS/23` §Revalidación vigente exige repetir 1–7 sobre el despliegue del corte 3/3 y prohíbe reutilizar
los PASS del 2026-08-15. **No se reutilizó ningún PASS histórico.**

| Prueba | Estado | Evidencia / motivo |
|---|---|---|
| 1 — regresión gate OFF | **BLOCKED** | El gate está **ON** en el ambiente y el agente no modifica App Settings |
| 2 — matriz de cierres bilingües (gate ON) | **BLOCKED** | Requiere conversación real/simulada; el emisor WhatsApp **no** está aislado (`wa-token` y `PhoneNumberId` operativos). `QAS/17` regla 4: no se envía nada |
| 3 — localización inconsistente | **BLOCKED** (remoto) | Requiere recorrido conversacional. Cubierta por la suite automática (`ArquitecturaCierreLocalizadoTests`, `ResolutorMensajeCierreCampaniaTests`), verde local |
| 4 — readiness sin mapeo: activa vs. borrador | **PASS** | Los tres supuestos, sobre el nuevo artefacto: par requerido por **activa** → `bloqueaGateOn=true`; pares requeridos solo por **borradores** (`smoke_sin_mapeo` es/en, y el par de `plantillaRef` vacío) → siguen **visibles** con `bloqueaGateOn=false`, con sus problemas, y **no impiden** `listoParaGateOn=true`; par requerido por activa **y** borrador (`inicio_campania|es`) → **deduplicado**, lista las 5 campañas consumidoras y bloquea por la activa |
| 5 — readiness estructural + **readiness compuesto DT-P32-04** | **PASS** (estructural remoto; verificación humana Meta pendiente) | Estructural: `inicio_campania` es/en configurado, `Componentes=[nombre]`, sin problemas, `listoParaGateOn=true` con borradores incompletos presentes. **Compuesto:** ver §9, prueba 0.4 |
| 6 — componentes y guarda de activación | **PASS (parcial)** | Guarda con gate ON verificada en ambos sentidos: borrador con contenido/mapeo propio incompleto → **`400 VALIDATION_ERROR`** y **conserva `borrador`**; borrador con lo suyo completo → **activa correctamente aunque otros borradores sigan incompletos** (`CAMP-…-INCOMPLETA` y `CAMP-P32-0301-20260815-B-SINMAPEO` permanecen en borrador). **BLOCKED**: componente vacío/duplicado y `Componentes=[]`, porque exigen editar App Settings, y la verificación de número/orden/significado en Meta, que es humana |
| 7 — lote mixto real | **BLOCKED** | Sin autorización de tráfico real ni confirmación de plantillas inglesas aprobadas |

---

## 9. Matriz `QAS/16` (pruebas 0 a 8)

| Prueba | es | en | Estado | Evidencia | Observación |
|---|---|---|---|---|---|
| 0 — snapshot del hilo | — | — | BLOCKED | — | Requiere abrir hilo → envío real; emisor no aislado |
| 0.1 — mensajes globales conectados | — | — | BLOCKED | — | Ídem |
| 0.2 — menú pendiente con snapshot | — | — | BLOCKED | — | Ídem |
| 0.3 — comandos y aclaración P-27 en inglés | — | — | BLOCKED | — | Ídem |
| **0.4 — readiness compuesto DT-P32-04** | ✔ | ✔ | **PASS** | §9.1 | Prueba central del corte 3/3 |
| 1 — mismo recorrido, dos idiomas | — | — | BLOCKED | — | `QAS/17` regla 4: sin aislamiento no se envía |
| 2 — lote mixto de WhatsApp | — | — | BLOCKED | — | Sin plantillas Meta inglesas confirmadas ni tráfico autorizado |
| 3 — edición masiva JSON sin desplegar | — | ✔ | **PASS (parcial)** | §7 pruebas 3–4 | Edición e importación sin compilar ni desplegar: PASS. Activar el borrador y abrirlo en un hilo nuevo: BLOCKED |
| 4 — validación y rollback | — | ✔ | **PASS (parcial)** | §7 prueba 5 | El contenido inválido nunca se publica (7/7 rechazos, cero escrituras) y la activa queda intacta: PASS. Activación y `Reactivar esta versión`: BLOCKED (cambio editorial en vivo con gate ON) |
| 5 — cambio de idioma del maestro | — | — | BLOCKED | — | Requiere hilo abierto |
| **6 — campaña incompleta** | ✔ | ✔ | **PASS** | §9.2 | Activación y asociación rechazadas, sin sustitución por español |
| 7 — D5 real | — | — | BLOCKED | — | `CALIBRACION_CONFIG` y `CALIBRACION_API_KEY` **ausentes**; sin presupuesto aprobado |
| 8 — UAT bilingüe GHT | — | — | BLOCKED | — | Sin participantes de GHT convocados ni recorrido ejecutable |
| **Gate local del checkout** | n/a | n/a | **PASS** | §3 | 1030 + 120, build/format/diff verdes |

### 9.1 Prueba 0.4 — readiness compuesto (detalle)

Ejecutada **sobre la campaña propia de esta corrida**, sin tocar campañas compartidas, y restaurada de
inmediato como exige la guía:

| Momento | `es.listo` | `en.listo` | `listoParaGateOn` | Causa reportada |
|---|---|---|---|---|
| Estado inicial (campaña activa completa) | `true` | `true` | `true` | — |
| Retirando el contenido `en` de la campaña **activa** | `true` | **`false`** | **`false`** | `CAMP-P32-20260816-1955-COMPLETA` / `activa` / **`localizacion_campania_incompleta`** |
| Tras restaurar el contenido `en` | `true` | `true` | `true` | — |

Esto evidencia el criterio 6 de `DT-P32-04`: readiness **compone los mismos resolutores del runtime** y
no puede declarar listo lo que estos rechazarían. La causa es concreta y nombra la campaña; el catálogo
global seguía activo y válido, de modo que la señal provino del resolutor de **contenido de campaña**,
no del catálogo. No hubo contaminación entre idiomas: `es` permaneció listo.

### 9.2 Prueba 6 — campaña incompleta (detalle)

- **Activación:** `PATCH …/estado {activa}` → **`400 VALIDATION_ERROR`**, «La campaña no tiene
  localizaciones completas para activarse», con el detalle completo de lo que falta:
  `localizaciones.en.nombre`, `.descripcion`, `.objetivo`, `.mensajeCierre`,
  `.mensajesIniciales.mi_40d899ac…`, `.preguntas.p_a442e6ff…`. La campaña **conserva `borrador`**.
- **Asociación del tercer usuario `en`:** `POST …/participantes` → **`409 CONFLICT`**
  `CAMPANIA_IDIOMA_INCOMPLETA`. Participantes asociados tras el intento: **0**.
- No hubo sustitución por español ni traducción inventada, y no se abrió ninguna conversación.

---

## 10. Lote mixto, activación/rollback y campaña incompleta

- **Lote mixto real:** **BLOCKED**. Cero envíos, cero llamadas a Meta, ningún `wamid` observado.
- **Activación / rollback de catálogo:** **BLOCKED por decisión de seguridad**. Con el gate **ON**,
  activar una versión editorial o reactivar la anterior es un cambio en vivo del contenido que consumen
  las conversaciones del ambiente. Ninguna versión fue activada, desactivada ni reactivada.
- **Campaña incompleta:** **PASS** (§9.2).

---

## 11. D5, costo/tokens/latencia y equivalencia `es`/`en`

**BLOCKED.** El banco de calibración es opt-in y no-op sin sus variables; se verificó que
`CALIBRACION_CONFIG`, `CALIBRACION_API_KEY`, `CALIBRACION_BASE_URL` y `CALIBRACION_MODELO` están
**ausentes**. No se ejecutó ninguna llamada real al LLM: **no hay costo, tokens ni latencia observados**
ni comparación de los cuatro pares equivalentes (idea fuerte, idea débil, inyección, salida). Los
criterios de `QAS/06` no pudieron aplicarse por falta de salidas reales que juzgar.

---

## 12. Decisión UAT de GHT

**PENDIENTE.** No hubo recorrido conversacional ejecutable, por lo que no se convocó a GHT. No existe
aceptación, observación menor, defecto ni rechazo de negocio para DT-P32-04.

---

## 13. Estado final del gate y de la simulación

| Señal | Estado al cierre | Nota |
|---|---|---|
| `Conversacion__CatalogoTextosHabilitado` | **ON** | **No fue modificado por esta corrida**; ya estaba ON al iniciar (`readiness.gateHabilitado=true`). El agente no cambia App Settings. `QAS/17` regla 4 y `QAS/18` §6 exigen dejarlo **OFF** salvo acta formal de activación productiva: **acción pendiente del operador** |
| `Simulacion__Habilitada` | **ON** (verificado activo) | Debe apagarse en el cierre obligatorio (`QAS/18` §Cierre 3) |
| `GHT_DIAG_KEY` | Presente y usada solo como `X-Diag-Key` | El operador debe retirarla (`Remove-Item Env:\GHT_DIAG_KEY`) |
| Catálogos `es`/`en` | Activos y válidos, sin cambios de activación | Solo se agregó `en` v5 en **borrador** |
| Datos de la corrida | Conservados | No se borró ninguna campaña, usuario, versión ni evidencia |

---

## 14. Observaciones para decisión humana (no son casos de prueba)

1. **El gate estaba ON antes de empezar.** Contradice la precondición «gate inicialmente OFF» de
   `QAS/22`, `QAS/23` y `QAS/18`, e impidió ejecutar la regresión gate-OFF (`QAS/23` prueba 1,
   `QAS/16` prueba 0). No se pudo determinar desde cuándo ni bajo qué autorización quedó ON.
2. **No existe un ambiente aislado.** La suscripción tiene un solo App Service, el mismo que conserva
   las campañas de la convención. Todas las guías asumen un ambiente aislado o números autorizados.
3. **El emisor WhatsApp está operativo** (`wa-token`, `wa-appsec`, `PhoneNumberId` en `ok`), de modo que
   `Simulacion__Habilitada=true` no impide salidas reales, tal como advierten `QAS/16` §9 y `QAS/18` §1.
4. **Una campaña activa puede editarse hasta quedar con un idioma incompleto.** `PUT
   …/localizaciones` aceptó vaciar el contenido `en` de una campaña ya **activa**; las guardas actúan al
   **activar** y al **asociar**, no al editar. El readiness compuesto de DT-P32-04 lo detecta y baja
   `listoParaGateOn` a `false` con causa concreta —esa es la red de seguridad y funcionó—, pero con el
   gate ON un participante inglés de esa campaña quedaría sin contenido resoluble. Se restauró el
   contenido de inmediato. Conviene decidir si el guard debe extenderse a la edición.

---

## 15. Recomendación final

### `BLOCKED` — **NO ACTIVAR**; no procede acta de activación

Razones, en orden:

1. **Cero evidencia conversacional.** Sin confirmación de aislamiento del emisor, `QAS/16` pruebas
   0–0.3, 1, 2 y 5 y `QAS/23` pruebas 2, 3 y 7 quedaron BLOCKED sin ejecutar. La equivalencia real
   `es`/`en` en un recorrido completo **no está demostrada** sobre este artefacto.
2. **Regresión gate-OFF imposible** con el gate ya en ON: falta la mitad del contrato de `QAS/23`
   prueba 1 y `QAS/16` prueba 0.
3. **D5, UAT y verificación Meta pendientes**, cada uno bloqueante por sí solo según `QAS/16` §Cierre
   y `QAS/23` §Evidencia.
4. **El gate quedó ON sin acta formal**, condición que `QAS/17` regla 4 y `QAS/18` §6 no permiten.

**Lo que sí quedó demostrado sobre el artefacto DT-P32-04 3/3** y puede darse por bueno: el readiness
compuesto y su capacidad de bloquear con causa concreta; la distinción activa/borrador de los mapeos
Meta con deduplicación; las guardas de activación y asociación bilingüe; la cadena completa de edición
masiva JSON (descarga → prevalidación → importación como borrador nuevo) sin compilar ni desplegar; y
las guardas de sesión/CSRF del área administrativa.

### Acciones humanas requeridas para desbloquear

1. **Decidir y ejecutar el estado del gate**: apagarlo (`Conversacion__CatalogoTextosHabilitado=false`)
   y confirmar OFF, o emitir el acta formal que justifique dejarlo ON.
2. Apagar `Simulacion__Habilitada` y retirar `GHT_DIAG_KEY` de la sesión (cierre obligatorio `QAS/18`).
3. Confirmar por escrito: ambiente aislado o números de prueba autorizados con el emisor WhatsApp
   contenido; plantillas Meta `es/en` aprobadas y verificadas a mano en el administrador de WhatsApp.
4. Corregir el defecto registrado como `DEF-P32-04-01` antes de otra prueba: una edición no puede
   persistir una campaña activa bilingüe incompleta.
5. Implementar una salida WhatsApp simulada y observable para QA; no usar teléfonos reales ni Meta
   para reemplazarla. Después, reejecutar solo la regresión afectada y los recorridos que quedaron
   BLOCKED; no repetir los PASS sin relación.
6. Habilitar presupuesto y credenciales D5 (`CALIBRACION_CONFIG`, `CALIBRACION_API_KEY`) y convocar a
   los participantes UAT de GHT cuando se vayan a ejecutar esas puertas independientes.

---

## 16. Resumen de diez líneas

1. **Hecho verificado:** el artefacto desplegado (`b4d74484`, activo) **sí contiene DT-P32-04 corte
   3/3**; se confirmó leyendo los ensamblados publicados, no por fechas.
2. **Hecho verificado:** gate local 4/4 verde — build Release `-warnaserror` sin warnings, **1030 + 120**
   pruebas, `dotnet format` y `git diff --check` limpios.
3. **Hecho verificado:** el **readiness compuesto** funciona: al retirar el contenido inglés de una
   campaña activa, `en.listo` y `listoParaGateOn` pasan a `false` con causa
   `localizacion_campania_incompleta`; al restaurar, vuelven a `true`.
4. **Hecho verificado:** los mapeos Meta distinguen activa (`bloqueaGateOn=true`) de borrador
   (`false`, visibles y sin frenar el gate) y deduplican el par compartido.
5. **Hecho verificado:** la campaña bilingüe incompleta no se activa (`400`, conserva borrador) ni
   admite asociar al participante inglés (`409 CAMPANIA_IDIOMA_INCOMPLETA`).
6. **Hecho verificado:** edición masiva JSON completa —descarga, 7/7 errores detectados sin escribir,
   importación como **borrador nuevo** (`en` v5)— sin compilar ni desplegar; activa y origen intactas.
7. **Bloqueo externo:** el emisor WhatsApp está operativo y no hay ambiente aislado ni números
   autorizados; por eso **no se envió ni un mensaje** y todos los recorridos conversacionales, el lote
   mixto y las pruebas 2, 3 y 7 de `QAS/23` quedan BLOCKED.
8. **Bloqueo externo:** D5 imposible (variables de calibración ausentes) → sin costo, tokens, latencia
   ni comparación `es`/`en`; UAT de GHT sin convocar; verificación humana de plantillas Meta pendiente.
9. **Decisión humana:** el gate **quedó ON** —ya lo estaba al iniciar— y el agente no puede cambiarlo;
   debe apagarse o respaldarse con acta formal, junto con apagar la simulación y retirar la clave.
10. **Defecto registrado:** `DEF-P32-04-01` confirma que una campaña **activa** puede editarse hasta
    quedar con un idioma incompleto; `DT-P32-05` debe cerrar la guarda antes de persistir y `DT-QA-03`
    debe aportar salida simulada antes de retomar los recorridos BLOCKED.
