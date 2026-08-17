# Resultados P-32 multidioma — validación DT-P32-04 (2026-08-16)

> **Resultado global: BLOCKED.** El gate local del checkout quedó **verde**, pero **no existe artefacto
> desplegable que contenga `DT-P32-04` corte 3/3**: el corte vive únicamente como cambios sin commit en
> el checkout local. Por `QAS/17` regla 1, `QAS/16` §Preparación 10 y `QAS/23` §Precondiciones, toda la
> validación remota (QAS/22, QAS/23 y QAS/16 pruebas 0–8) queda **BLOCKED** y no se ejecutó.

---

## 1. Ambiente, fecha, ejecutor y autorización

| Campo | Valor |
|---|---|
| Fecha de ejecución | 2026-08-16 19:18 (UTC-05:00) |
| Ejecutor | Agente QA (Claude Code) invocado con la frase corta de `QAS/17` §Invocación |
| Ambiente ejecutado | **Solo local** — checkout `GHT_Tejido_de_la_red`, rama `main`, .NET SDK 8.0.424, Windows 11 |
| Ambiente remoto candidato | `app-eltejido-mvp` (`https://app-eltejido-mvp-…eastus-01.azurewebsites.net`). `GET /health` → `200 {"status":"ok"}` (única llamada remota de esta corrida, sin credenciales) |
| Autorización recibida | Solo la implícita en la frase de invocación + `GHT_DIAG_KEY` presente en el entorno del proceso |
| Autorización **no** recibida | URL de ambiente aislado confirmada por operador; confirmación de artefacto desplegado con DT-P32-04 3/3; aislamiento del emisor saliente o números de prueba autorizados; ventana de gate ON; plantillas Meta inglesas aprobadas; presupuesto/credenciales D5; participantes UAT de GHT |
| Cambios remotos realizados | **Ninguno.** Sin push, sin despliegue, sin App Settings, sin secretos, sin datos |
| Cambios de código realizados | **Ninguno.** No se corrigió nada durante la corrida |

### 1.1 Manejo de `GHT_DIAG_KEY`

`GHT_DIAG_KEY` **está presente** en el entorno del proceso del agente. **No se usó ni se transmitió**
en esta corrida: al quedar BLOCKED la precondición de artefacto, cualquier llamada de simulación
habría corrido contra un artefacto **sin** DT-P32-04 y no constituiría evidencia del corte 3/3. Su
valor no fue leído, mostrado, escrito en archivo, comando ni reporte. La única llamada remota fue
`GET /health` sin cabeceras de autenticación.

---

## 2. Bloqueo principal — el artefacto DT-P32-04 3/3 no existe fuera del checkout

Evidencia verificable (comandos de solo lectura, sin modificar el repositorio):

| Comprobación | Resultado |
|---|---|
| `git rev-parse HEAD` | `20ee675…` — `test(evaluacion): … (DT-RUB-01 4/4)` |
| `git rev-parse origin/main` | `f44008a` — `spec para DT-RUB-01` (2026-08-16) |
| `git rev-list --left-right --count origin/main...HEAD` | `0  5` → HEAD está **5 commits adelante** de `origin/main`; **nada de lo local está publicado** |
| `git log --all --oneline -- src/ElTejido.Domain/Localizacion` | **vacío** → el núcleo de DT-P32-04 no existe en **ningún** commit del repositorio |
| `git status --porcelain` | 67 entradas; DT-P32-04 aparece como archivos **sin seguimiento** (`ContenidoCampaniaEfectivo.cs`, `ResolutorTextosGlobales.cs`, `ResolverPlantillaCanal.cs`, `PoliticaIdiomaLlm.cs`, `Domain/Localizacion/`, y sus suites de prueba) más modificaciones no confirmadas en orquestador, envíos, evaluación y readiness |

Consecuencia: el pipeline `deploy.yml` publica lo que se empuja a `main`; el artefacto desplegable más
reciente posible corresponde a `f44008a`, que **no contiene DT-P32-04 ni DT-RUB-01**. Por tanto no es
posible identificar —ni podría existir— un despliegue con el corte 3/3.

`QAS/16` §Preparación punto 10 prescribe exactamente esta ruta: *«Si el corte solo existe en el
checkout local o no puede identificarse el artefacto desplegado, ejecuta únicamente los gates locales
permitidos y marca la validación remota como BLOCKED; no despliegues por cuenta propia.»* Es lo que se
hizo.

---

## 3. Gate local del checkout (`QAS/17` regla 5) — **PASS**

Ejecutado secuencialmente sobre el checkout tal cual, sin modificar código:

| # | Comando | Resultado | Evidencia |
|---|---|---|---|
| 1 | `dotnet build -c Release -warnaserror` | **PASS** | `Build succeeded. 0 Warning(s), 0 Error(s)`; 7 proyectos; 48,68 s; exit `0` |
| 2 | `dotnet test -c Release --no-build --filter "Category!=Calibracion"` | **PASS** | `ElTejido.UnitTests`: Failed 0, Passed **1030**, Skipped 0 (8 s). `ElTejido.IntegrationTests`: Failed 0, Passed **120**, Skipped 0 (22 s). exit `0` |
| 3 | `dotnet format --verify-no-changes --no-restore` | **PASS** | exit `0`, sin diferencias de formato |
| 4 | `git diff --check` | **PASS** | exit `0`, sin espacios/conflictos |

Los conteos **1030 unitarias + 120 de integración** coinciden exactamente con el cierre declarado del
corte 3/3 en `Especificaciones/Iniciativas/DT-P32-04_Nucleo_Transversal_Multidioma.md` §8.3 y en
`Especificaciones/planes/DT-P32-04_Plan_Refactor_Multidioma.md` §Corte 3/3. El gate local **no** detiene
la validación remota por fallo propio; la detiene la precondición de artefacto de la §2.

> Alcance de esta evidencia: confirma que el checkout compila, pasa su suite y respeta formato. **No**
> sustituye ninguna prueba de `QAS/22`, `QAS/23` ni `QAS/16`, que exigen ambiente desplegado.

---

## 4. Identificador de corrida y datos de prueba

| Campo | Valor |
|---|---|
| Identificador reservado | `P32-20260816-1918` |
| Usuarios de prueba creados | **Ninguno** (bloqueado: sin ambiente con el artefacto ni garantía de aislamiento del emisor) |
| Últimos cuatro dígitos de teléfonos | **No aplica** — no se reservó ni se consultó ningún teléfono |
| Campañas creadas | **Ninguna**. `CAMP-P32-20260816-1918-COMPLETA` y `…-INCOMPLETA` no se crearon |
| Catálogos/borradores creados | **Ninguno** |
| Datos borrados | **Ninguno**. No se tocó evidencia ni datos de corridas anteriores |

---

## 5. Recursos LLM a reutilizar (`QAS/17` regla 6c) — **BLOCKED**

No se pudo verificar la existencia, unicidad ni estado activo de `rúbrica OpenBrain v3.4`,
`Evaluación con rubrica OpenBrain Thought-Scoring` ni `OpenRouter-Terra`: la consulta exige sesión
administrativa autenticada contra el ambiente desplegado, que está bloqueado por §2. **No se
solicitó, leyó ni manipuló** la key de OpenRouter ni ninguna configuración LLM. Los tres recursos
quedan **sin verificar y sin modificar**.

---

## 6. Versiones/huellas de catálogo y plantillas Meta

**BLOCKED.** No se creó, descargó, importó ni activó ninguna versión de catálogo, por lo que no hay
versión ni huella que registrar. No se consultó ni configuró ningún mapeo
`WhatsApp__PlantillaEnvioInicial__Mapeos__…`; el agente no crea ni modifica App Settings. No se
registran nombres físicos ni códigos de plantilla Meta.

---

## 7. Resultado de `QAS/22` — semilla base, JSON masivo, prevalidación y readiness

| Prueba `QAS/22` | Estado | Motivo |
|---|---|---|
| 1 — semilla base independiente del legacy | BLOCKED | Requiere portal del ambiente con DT-P32-04 3/3 (§2) |
| 2 — preview de configuración anterior | BLOCKED | Ídem |
| 3 — descargar JSON para edición masiva | BLOCKED | Ídem |
| 4 — editar y cargar masivamente | BLOCKED | Ídem |
| 5 — errores completos y cero escritura | BLOCKED | Ídem |
| 6 — permisos y auditoría (`visor`/`admin`) | BLOCKED | Ídem; además no hay usuarios de prueba autorizados |
| 7 — readiness real | BLOCKED | Ídem |
| 8 — campaña bilingüe protegida | BLOCKED | Ídem |
| 9 — regresión y corrida P-32 | BLOCKED | Depende de 1–8 y de la ventana autorizada |

---

## 8. Resultado de `QAS/23` — cierres, mapeos Meta y readiness compuesto

`QAS/23` §Revalidación vigente (2026-08-16) exige repetir las pruebas **1–7 sobre un despliegue
autorizado del corte 3/3** y declara explícitamente que los PASS del 2026-08-15 son **baseline, no
evidencia del nuevo artefacto**. Ese despliegue no existe (§2).

| Prueba `QAS/23` | Estado | Motivo |
|---|---|---|
| 1 — regresión gate OFF | BLOCKED | Sin artefacto desplegado con DT-P32-04 3/3 |
| 2 — matriz de cierres bilingües (gate ON) | BLOCKED | Ídem + sin ventana autorizada ni aislamiento del emisor |
| 3 — localización inconsistente | BLOCKED | Ídem |
| 4 — readiness sin mapeo: activa vs. borrador | BLOCKED | Ídem |
| 5 — readiness estructural completo + readiness compuesto DT-P32-04 | BLOCKED | Ídem; requiere además evidencia humana Meta |
| 6 — componentes y límite de la comprobación | BLOCKED | Ídem + ventana ON |
| 7 — lote mixto real | BLOCKED | Sin autorización de tráfico real ni plantillas inglesas aprobadas confirmadas |

**No se reutiliza ningún PASS histórico como evidencia de DT-P32-04.**

---

## 9. Matriz de pruebas `QAS/16` (pruebas 0 a 8)

| Prueba | es | en | Estado | Evidencia | Observación |
|---|---|---|---|---|---|
| 0 — snapshot del hilo | — | — | BLOCKED | — | Requiere ambiente con el corte 3/3 (§2) |
| 0.1 — mensajes globales conectados | — | — | BLOCKED | — | Requiere catálogo inglés activo y ventana aislada |
| 0.2 — menú pendiente con snapshot | — | — | BLOCKED | — | Ídem |
| 0.3 — comandos y aclaración P-27 en inglés | — | — | BLOCKED | — | Ídem |
| 0.4 — readiness compuesto DT-P32-04 | — | — | BLOCKED | — | Es la prueba central del corte; sin artefacto no hay evidencia posible |
| 1 — mismo recorrido, dos idiomas | — | — | BLOCKED | — | Sin garantía de aislamiento del emisor: no se envía nada (`QAS/17` regla 4) |
| 2 — lote mixto de WhatsApp | — | — | BLOCKED | — | Sin plantillas Meta inglesas aprobadas confirmadas ni autorización de tráfico real |
| 3 — edición masiva JSON sin desplegar | — | — | BLOCKED | — | Requiere portal del ambiente |
| 4 — validación y rollback | — | — | BLOCKED | — | Ídem |
| 5 — cambio de idioma del maestro | — | — | BLOCKED | — | Ídem |
| 6 — campaña incompleta | — | — | BLOCKED | — | Ídem; la campaña `…-INCOMPLETA` no se creó |
| 7 — D5 real | — | — | BLOCKED | — | `CALIBRACION_CONFIG` y `CALIBRACION_API_KEY` ausentes en el entorno; sin presupuesto aprobado para esta corrida |
| 8 — UAT bilingüe GHT | — | — | BLOCKED | — | Sin participantes de GHT convocados ni recorrido ejecutable |
| **Gate local del checkout** | n/a | n/a | **PASS** | §3 de este reporte | Build/test/format/diff verdes; 1030 + 120 |

---

## 10. Lote mixto, activación/rollback y campaña incompleta

- **Lote mixto real:** BLOCKED. No se ejecutó ningún envío. No se observó ningún `wamid` ni llamada a
  Meta durante la corrida (no se abrió ninguna ruta que las genere).
- **Activación / rollback de catálogo:** BLOCKED. Ninguna versión fue creada, activada ni reactivada.
- **Campaña incompleta:** BLOCKED. No se creó; el rechazo de activación no fue observado en esta corrida.

---

## 11. D5, costo/tokens/latencia y equivalencia `es`/`en`

**BLOCKED.** El banco de calibración de `tests/Calibracion/README.md` es opt-in y **no-op sin sus
variables de entorno**; se verificó que `CALIBRACION_CONFIG` y `CALIBRACION_API_KEY` están **ausentes**.
No se ejecutó ninguna llamada real al LLM, por lo que **no hay costo, tokens ni latencia observados** ni
comparación de pares equivalentes (idea fuerte, idea débil, inyección, salida). Los criterios de
`QAS/06` no pudieron aplicarse por falta de salidas reales que juzgar.

---

## 12. Decisión UAT de GHT

**PENDIENTE / BLOCKED.** No hubo recorrido ejecutable, por lo que no se convocó a GHT. No existe
aceptación, observación ni rechazo de negocio para DT-P32-04.

---

## 13. Estado final del gate y de la simulación

| Señal | Estado |
|---|---|
| `Conversacion__CatalogoTextosHabilitado` | **No modificado por esta corrida.** Último estado confirmado: **OFF** (reporte `Resultados_P32_Smoke_DT-P32-03-01_2026-08-15.md`). **No reverificado hoy**: la comprobación exige sesión administrativa contra el ambiente, fuera de lo ejecutado |
| `Simulacion__Habilitada` | **No modificado por esta corrida.** La presencia de `GHT_DIAG_KEY` sugiere que un operador preparó la ventana; **debe apagarse en el cierre obligatorio** (`QAS/18` §Cierre obligatorio 3) |
| `GHT_DIAG_KEY` | Presente en la sesión y **no utilizada**. El operador debe retirarla (`Remove-Item Env:\GHT_DIAG_KEY`) al cerrar |

---

## 14. Recomendación final

### `BLOCKED` — no procede acta de activación

Razones, en orden:

1. **Precondición de artefacto incumplida.** DT-P32-04 corte 3/3 no está en ningún commit ni en
   `origin/main`; no puede estar desplegado. `QAS/23` exige repetir 1–7 sobre el nuevo artefacto y
   prohíbe reutilizar los PASS del 2026-08-15.
2. **Cero evidencia remota.** QAS/22 (1–9), QAS/23 (1–7) y QAS/16 (0–8) quedan BLOCKED sin ejecutar.
3. **Bloqueos externos independientes** que persistirían aun con el artefacto desplegado: aislamiento
   del emisor sin confirmar, plantillas Meta inglesas sin confirmar aprobadas, D5 sin credenciales ni
   presupuesto, UAT de GHT sin convocar.
4. **Lo único verde es el gate local**, que es condición necesaria y claramente insuficiente.

**No se declara P-32 lista para producción.** El gate multidioma debe permanecer **OFF**.

### Acciones humanas requeridas para desbloquear

1. Decidir sobre los 67 cambios sin confirmar del checkout (DT-P32-04 3/3 + documentación + DT-RUB-01
   sin publicar) y publicar `main` — decisión de un humano autorizado, **no** del agente de pruebas.
2. Autorizar y ejecutar el despliegue al ambiente aislado, e **identificar el artefacto** resultante.
3. Confirmar por escrito: URL del ambiente aislado, aislamiento del emisor WhatsApp o números de prueba
   autorizados, plantillas Meta `es/en` aprobadas y mapeadas, presupuesto/credenciales D5, participantes
   UAT y ventana de gate ON.
4. Reejecutar `QAS/17` completo desde una sesión nueva con `GHT_DIAG_KEY`.
5. Cierre obligatorio de esta sesión: apagar `Simulacion__Habilitada`, confirmar gate OFF y retirar
   `GHT_DIAG_KEY`.

---

## 15. Resumen de diez líneas

1. **Hecho verificado:** el gate local del checkout está verde — build Release `-warnaserror` con 0
   warnings/0 errores, 1030 unitarias + 120 de integración sin Calibración, `dotnet format` y
   `git diff --check` limpios.
2. **Hecho verificado:** esos conteos coinciden con el cierre declarado del corte 3/3 de DT-P32-04.
3. **Hecho verificado:** DT-P32-04 3/3 existe solo como cambios sin commit; `git log --all` no lo
   encuentra en el historial y `origin/main` (`f44008a`) está 5 commits detrás del HEAD local.
4. **Hecho verificado:** el ambiente documentado responde `GET /health` 200, pero solo puede estar
   ejecutando `f44008a`, sin DT-P32-04 ni DT-RUB-01.
5. **Bloqueo externo:** sin artefacto desplegado del corte 3/3, QAS/22 (1–9), QAS/23 (1–7) y QAS/16
   (0–8) quedan BLOCKED sin ejecutar; no se reutiliza ningún PASS histórico.
6. **Bloqueo externo:** sin confirmación de aislamiento del emisor no se envió ni un mensaje simulado;
   no se creó ningún usuario, campaña ni catálogo.
7. **Bloqueo externo:** D5 imposible (`CALIBRACION_CONFIG`/`CALIBRACION_API_KEY` ausentes) → sin costo,
   tokens, latencia ni comparación `es`/`en`; UAT de GHT sin convocar.
8. **Decisión humana:** publicar el trabajo local y autorizar el despliegue al ambiente aislado,
   identificando el artefacto — el agente no despliega ni hace push.
9. **Decisión humana:** confirmar URL aislada, plantillas Meta inglesas aprobadas, presupuesto D5,
   participantes UAT y la ventana de gate ON antes de reintentar `QAS/17`.
10. **Cierre pendiente del operador:** apagar `Simulacion__Habilitada`, verificar gate OFF y retirar
    `GHT_DIAG_KEY`; la clave estuvo disponible pero **no se usó ni se transmitió** en esta corrida.
