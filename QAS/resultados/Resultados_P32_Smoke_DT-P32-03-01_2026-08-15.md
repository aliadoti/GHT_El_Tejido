# Resultados — P-32 revalidación acotada DT-P32-03-01 · 2026-08-15

Revalidación acotada según `QAS/23 §Revalidación acotada de DT-P32-03-01`: **únicamente pruebas 4, 5 y 6**.
Las pruebas 1 a 3 **no se repitieron**; se conservan como evidencia sus PASS del smoke previo
(`Resultados_P32_Smoke_DT-P32-03_2026-08-15.md`), y no se reabrió el gate para repetir cierres. No se
ejecutaron `QAS/17` completo, `QAS/21`, D5, UAT ni la prueba 7. No se implementaron correcciones, no hubo
push ni despliegue, y no se cambiaron secretos. Ninguna clave aparece en este reporte, en los comandos
ejecutados ni en archivos.

## 1. Ambiente, despliegue y autorización

| Campo | Valor |
|---|---|
| Ejecutor | Claude Code (Opus 5), sesión iniciada con `GHT_DIAG_KEY` en el entorno |
| Fecha | 2026-08-15, 22:49Z – 23:0xZ |
| Ambiente | **Azure `app-eltejido-mvp`**. `/health` 200; `/health/ready` 200, 10 componentes `ok` y 1 `no_aplica` (`conversacion:umbralResumenConsolidacion`) |
| **Microajuste desplegado** | **Sí.** `60b520d` (DT-P32-03-01 1/1) está en Azure: el endpoint publica `mapeosMeta[].bloqueaGateOn`, campo que no existía en la corrida anterior. Verificado en vivo antes de ejecutar nada |
| Gate inicial | **OFF** (`readiness.gateHabilitado=false`), verificado por API a las 22:49Z |
| Ventana ON | Abierta y cerrada por el operador humano. El agente no tocó App Settings |
| Autorización recibida | (a) ventana ON controlada para la guarda de activación; (b) confirmación de los 6 valores de App Settings como evidencia externa de la prueba 5 |
| Tráfico WhatsApp | **Ninguno.** No se creó ni asoció participante, no se ejecutó `POST /campanias/{id}/envios` y no se inyectó webhook. Sólo lecturas de readiness y `PATCH` de estado sobre campañas de prueba sin participantes |

### 1.1 Datos creados

**Identificador:** `P32-0301-20260815`

| Campaña | Id | Alias (`plantillaRef`) | Papel | Estado final |
|---|---|---|---|---|
| `CAMP-P32-0301-20260815-A-MAPEADA` | `c_b9266bd9479340cb82806294a7d9b341` | `inicio_campania` (configurado) | caso positivo de la guarda | **archivada** |
| `CAMP-P32-0301-20260815-B-SINMAPEO` | `c_e76234e662d24e58842d7f673fbde5ce` | `smoke_sin_mapeo` (ausente) | caso negativo de la guarda | **borrador** |
| `CAMP-P32-0301-20260815-B2-SINMAPEO` | `c_dd25637e46534380aa7d8b990a795d59` | `smoke_sin_mapeo` (ausente) | caso «activa con par ausente» | **archivada** |

Las tres son bilingües `es`/`en` con localizaciones completas, sin participantes asociados. `B` y `B2`
comparten alias a propósito, para poder observar la deduplicación del par. `B` se conserva en `borrador`:
es el fixture vivo del sub-caso «par ausente que no bloquea» y, por diseño del microajuste, no altera la
señal.

## 2. Resultados por prueba

| Prueba | Resultado |
|---|---|
| 4 — Readiness sin mapeo: activa frente a borrador | **PASS** |
| 5 — Readiness estructural completo | **PASS** |
| 6 — Componentes, límite y guarda de activación | **PASS** |

### Prueba 4 — readiness sin mapeo: activa frente a borrador · **PASS**

Los tres sub-casos se observaron **en vivo contra Azure**, no sólo en fixture, construyendo el estado con
campañas de prueba y sin tocar configuración compartida.

**(a) Par ausente exigido sólo por borradores** — con `B` y `B2` en `borrador`:

| Par | `configurado` | `bloqueaGateOn` | Problemas | `listoParaGateOn` |
|---|---|---|---|---|
| `smoke_sin_mapeo` / `es` | false | **false** | `nombre_faltante`, `idioma_meta_faltante` | **true** |
| `smoke_sin_mapeo` / `en` | false | **false** | `nombre_faltante`, `idioma_meta_faltante` | **true** |

El par y sus problemas **siguen visibles** —no se ocultan ni se presentan como listos— y la señal global
permanece en verde. Es exactamente el comportamiento que faltaba: en la corrida anterior este mismo tipo de
par mantenía `listoParaGateOn=false` de forma indefinida.

**(b) Par ausente exigido por una campaña activa** — tras activar `B2` con el gate OFF:

| Par | `bloqueaGateOn` | `listoParaGateOn` |
|---|---|---|
| `smoke_sin_mapeo` / `es` | **true** | **false** |
| `smoke_sin_mapeo` / `en` | **true** | **false** |

**(c) Mismo par exigido por activa y borrador** — con `B2` activa y `B` borrador, readiness devolvió
**un solo par por idioma** (deduplicado), listando **ambas** campañas con su estado, y bloqueó por la
consumidora activa:

```
smoke_sin_mapeo  es  configurado=False bloqueaGateOn=True  problemas=[nombre_faltante, idioma_meta_faltante]
      <- activa    "CAMP-P32-0301-20260815-B2-SINMAPEO"
      <- borrador  "CAMP-P32-0301-20260815-B-SINMAPEO"
```

Al archivar `B2`, el mismo par volvió a `bloqueaGateOn=false` y `listoParaGateOn` regresó a `true`, cerrando
el ciclo en ambas direcciones.

**Equivalente automatizado, verde:** `Evaluar_ParFaltanteDeCampaniaActiva_BloqueaElGate`,
`Evaluar_ParFaltanteSoloDeBorrador_SeEnumeraPeroNoBloquea`,
`Evaluar_ParCompartidoPorActivaYBorrador_BloqueaUnaSolaVezYConservaAmbas`,
`Readiness_ConMapeoFaltanteSoloEnBorrador_QuedaListoParaGateOn`,
`Readiness_ConActivaCompletaYBorradorIncompleto_ConservaLaSenalEnVerde`,
`Readiness_ConCatalogoListoPeroSinMapeoMeta_NoQuedaListoParaGateOn`, e integración
`Readiness_ConBorradorIncompleto_LoMuestraSinBloquearElGate`.

### Prueba 5 — readiness estructural completo · **PASS**

Estaba **BLOCKED** en la corrida anterior. Las dos causas quedaron resueltas:

**(a) `listoParaGateOn=true` aunque existan borradores incompletos.** Verificado en vivo, con el gate OFF y
con el gate ON:

| Momento | `listo` | `listoParaGateOn` | Borradores incompletos presentes |
|---|---|---|---|
| 22:49Z (gate OFF) | true | **true** | sí — los dos `CAMP-P32-…-INCOMPLETA` |
| 23:00Z (gate ON) | true | **true** | sí — más `B-SINMAPEO` |

Los pares activos `inicio_campania` `es`/`en` aparecen `configurado=true`, `bloqueaGateOn=true`,
`componentes=["nombre"]`, `problemas=[]`. Los pares que sólo piden borradores quedan visibles con
`bloqueaGateOn=false`. La señal ya no confunde «hay trabajo de edición pendiente» con «no se puede encender».

**(b) Referencia externa aceptada.** `QAS/23` ahora admite explícitamente la verificación del operador en
Azure/Meta en lugar de ampliar el endpoint. El responsable humano **confirmó** que las 6 entradas de App
Settings contienen exactamente:

| Par | `Nombre` | `Idioma` | `Componentes__0` |
|---|---|---|---|
| `inicio_campania` / `es` | `tejido_start_es_co` | `es_CO` | `nombre` |
| `inicio_campania` / `en` | `tejido_start_en_us` | `en_US` | `nombre` |

Se registra como evidencia externa aceptada. Se mantiene la limitación de diseño, ahora explícita en la
guía: la API confirma estructura y **no** devuelve ni certifica el nombre físico de la plantilla.

### Prueba 6 — componentes, límite y guarda de activación · **PASS**

**Componente vacío o duplicado se reporta como inválido.** Verde en
`Evaluar_ComponenteVacioODuplicado_SeReportaAunqueElParResuelva`, que además comprueba que el par puede
resolver y aun así quedar `Listo=false`. En vivo se observaron los otros tres códigos del validador
(`nombre_faltante`, `idioma_meta_faltante`, `plantilla_ref_faltante`).

**`Componentes=[]` es válido sólo sin variables de cuerpo.** Verde en
`Evaluar_PlantillaSinVariables_PuedeQuedarListaConComponentesVacios`. Las plantillas aprobadas de este
ambiente tienen una variable de cuerpo, así que `Componentes__0=nombre` es lo correcto y `[]` sería erróneo.

**Verificación manual en Meta** (la que readiness no puede hacer), aportada por el responsable humano:

| Plantilla aprobada | Idioma Meta | Variables de cuerpo | Cuerpo |
|---|---|---|---|
| `tejido_start_en_us` | English (US) → `en_US` | 1 | `Hello {{name}}. Your Tejido de Red session is scheduled to start now.` |
| `tejido_start_es_co` | Spanish (COL) → `es_CO` | 1 | `Hola {{name}}. Tu sesión de Tejido de Red está programada para iniciar ahora.` |

Una variable ⇒ un componente en posición 0. El emisor sustituye **por posición**
(`WhatsAppGateway.cs:102-103`), y `nombre` es la clave interna que resuelve al nombre del participante
(`RenderizadorMensaje.cs:35`).

#### Guarda de activación (añadida por DT-P32-03-01) — ejecutada en ventana ON controlada

**Caso negativo — con gate ON, activar un borrador con su mapeo incompleto:**

`PATCH /api/admin/campanias/c_e76234e662d24e58842d7f673fbde5ce/estado {"estado":"activa"}` → **`400
VALIDATION_ERROR`** (`corr_1653fa60e582428f92c47dd760f02825`), con un detalle **por problema y por idioma**
bajo `mapeosMeta.{mensajeInicialId}.{idioma}`:

```
mapeosMeta.mi_0f6ddb26ad6247f6998b6386560a8221.es -> nombre_faltante
mapeosMeta.mi_0f6ddb26ad6247f6998b6386560a8221.es -> idioma_meta_faltante
mapeosMeta.mi_0f6ddb26ad6247f6998b6386560a8221.en -> nombre_faltante
mapeosMeta.mi_0f6ddb26ad6247f6998b6386560a8221.en -> idioma_meta_faltante
```

Mensaje: *«La campaña necesita una plantilla de WhatsApp configurada por mensaje inicial e idioma.»*
**Estado tras el intento: `borrador`**, confirmado por lectura independiente. No hubo cambio de estado.

**Caso positivo — con gate ON, activar un borrador con su mapeo completo, habiendo otro borrador
incompleto:**

`PATCH …/c_b9266bd9479340cb82806294a7d9b341/estado {"estado":"activa"}` → **`200`, `estado=activa`**, con
`B-SINMAPEO` todavía en `borrador` e incompleto. La guarda mira **sólo la campaña objetivo**: ningún otro
borrador bloquea la transición.

**Caso de regresión — con gate OFF, la transición conserva la conducta previa:**

`PATCH …/c_dd25637e46534380aa7d8b990a795d59/estado {"estado":"activa"}` con el gate **OFF** y alias sin
mapeo → **`200`, `estado=activa`**. Con el gate apagado la activación no exige mapeos, exactamente como
antes del microajuste.

**Equivalente automatizado, verde:** `Activar_ConGateOnYSinMapeoPropio_DevuelveValidacionYNoCambiaElEstado`,
`Activar_ConGateOnYMapeoPropioCompleto_ActivaLaCampania`,
`Activar_ConGateOffYSinMapeo_ConservaLaConductaPrevia`,
`Activar_ConGateOnYCampaniaEspanolaSinAlias_DevuelvePlantillaRefFaltante`, e integración
`ActivarCampania_ConGateOnYSinMapeoPropio_Responde400YConservaElBorrador`,
`ActivarCampania_ConGateOnYMapeoPropio_ActivaAunqueHayaOtroBorradorIncompleto`,
`ActivarCampania_ConGateOffYSinMapeo_ConservaLaConductaPrevia`.

## 3. Suite automatizada

Ejecutada localmente sobre el mismo commit desplegado:

| Suite | Filtro | Resultado |
|---|---|---|
| Unitarias | `ValidadorMapeosPlantillaMeta`, `Readiness`, `GestionCampanias` | **36 passed, 0 failed** |
| Integración | `Readiness`, `CatalogosTextos`, `Campanias` | **28 passed, 0 failed** |

## 4. Componentes configurados (sin secretos)

| Par | Nombre | Idioma Meta | Componentes | Orden | `bloqueaGateOn` |
|---|---|---|---|---|---|
| `inicio_campania` / `es` | `tejido_start_es_co` (confirmado por el operador; la API sólo expone que está configurado) | `es_CO` (ídem) | `["nombre"]` | 1 componente, posición 0 | true |
| `inicio_campania` / `en` | `tejido_start_en_us` (ídem) | `en_US` (ídem) | `["nombre"]` | 1 componente, posición 0 | true |

Cantidad y orden coinciden con las plantillas aprobadas verificadas a mano. Referencia verificable:
plantillas `tejido_start_es_co` y `tejido_start_en_us` en el administrador de WhatsApp de la cuenta del
proyecto. No se leyeron ni registraron tokens, App Secret de Meta ni la key de OpenRouter.

## 5. Estado final

- **Gate `Conversacion:CatalogoTextosHabilitado` → OFF**, devuelto por el operador y verificado por API tras
  el reinicio (ver §5.1).
- Catálogos: `es` v1 activa y válida, `en` v1 activa y válida; `listo=true`, **0 campañas bloqueadas** en
  ambos idiomas. No se creó, activó ni borró ninguna versión de catálogo.
- `listoParaGateOn=true`.
- Pares pendientes, todos con `bloqueaGateOn=false` y por tanto sin bloquear el uso: *(sin alias)* / `en` de
  los dos borradores INCOMPLETA, y `smoke_sin_mapeo` `es`/`en` del borrador `B` de esta corrida.
- `Simulacion__Habilitada` sigue en **true**: el operador debe volverla a `false` y retirar `GHT_DIAG_KEY`
  de la sesión (`QAS/18` §Cierre obligatorio, y punto 5 de la revalidación de `QAS/23`).
- Datos conservados, nada borrado: 3 campañas nuevas (2 archivadas, 1 en borrador). Ninguna campaña
  preexistente cambió de estado en esta corrida. No se creó ningún usuario ni conversación.
- La clave de diagnóstico se usó exclusivamente como header `X-Diag-Key`; su valor no aparece en este
  reporte, en ningún archivo del repositorio ni en los comandos ejecutados.
- El único archivo nuevo del repositorio es este reporte. No se tocó código, configuración ni secretos.

### 5.1 Verificación del retorno a OFF

El operador confirmó el cambio a las 23:03Z. Las lecturas inmediatas (23:03:51Z y 23:04:19Z) seguían
devolviendo `gateHabilitado=true`, coherente con que el App Service aún no había completado el reinicio: la
configuración se lee al arrancar. Se reintentó con espera hasta obtener la confirmación:

| Lectura | `gateHabilitado` |
|---|---|
| 23:03:51Z | true |
| 23:04:19Z | true |
| **23:04:44Z** | **false** ← confirmado |
| 23:05:59Z (captura final) | **false** |

Captura final a las 23:05:59Z: `gateHabilitado=false`, `listo=true`, `listoParaGateOn=true`, `es` v1 y `en`
v1 activas y válidas con 0 campañas bloqueadas; pares con `bloqueaGateOn=true`: `inicio_campania` `es` y
`en`; pares pendientes sin bloquear: *(sin alias)* `en`, `smoke_sin_mapeo` `es` y `en`.

**Lección operativa:** confirmar el estado del gate leyendo readiness **después** de que el reinicio
complete, no inmediatamente tras aplicar el App Setting. Entre el `Apply` y el efecto real pasó cerca de un
minuto, durante el cual `/health` ya respondía 200 y la API seguía sirviendo la configuración anterior.

## 6. Conclusión

Las tres pruebas del alcance —4, 5 y 6— están en **PASS**, con la guarda de activación ejecutada en ventana
ON controlada y con retorno a OFF. Sumadas a los PASS conservados de las pruebas 1 a 3 del smoke previo, las
seis pruebas de `QAS/23` están en PASS y la evidencia humana de Meta está aceptada.

El microajuste hace lo que prometía y se comprobó en vivo en ambas direcciones: un borrador a medio
construir ya **no** secuestra la señal global —sigue visible, con sus problemas, pero sin bloquear— y, para
que esa permisividad no se vuelva mentira, la transición `borrador → activa` con el gate ON exige los mapeos
**propios** de la campaña, responde `400` con un detalle por problema e idioma, y no cambia el estado. Con
el gate OFF la activación conserva exactamente la conducta previa.

## **P-32 SMOKE GREEN**

Alcance de esta declaración, según `QAS/23`: el smoke acotado habilita el inicio de `DT-I20-02`. **P-32 no se
declara cerrada**: la corrida completa de `QAS/17` se ejecutará después de `DT-I20-02`, según el orden
acordado, y la prueba 7 (lote mixto real) sigue sin ejecutarse.

### Pendientes operativos

1. Volver `Simulacion__Habilitada` a `false` y retirar `GHT_DIAG_KEY` de la sesión.
2. Decidir qué hacer con el borrador `CAMP-P32-0301-20260815-B-SINMAPEO`: es inofensivo para la señal, pero
   si no se quiere conservar como fixture, conviene completar su mapeo o retirarle `en` de
   `idiomasHabilitados`. No admite archivado directo: no existe transición desde `Borrador`.
3. La prueba 7 sigue pendiente y es la única que puede confirmar que los mapeos resuelven de verdad contra
   las plantillas aprobadas en Meta.
