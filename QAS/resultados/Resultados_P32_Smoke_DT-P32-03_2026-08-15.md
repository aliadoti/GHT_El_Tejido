# Resultados — P-32 smoke DT-P32-03 (cierre localizado y readiness Meta) · 2026-08-15

Ejecución acotada de `QAS/23_DT-P32-03_Cierre_y_Readiness_Meta_Como_Probar.md`, **únicamente pruebas 1 a
6**, como smoke previo a `DT-I20-02`. No se ejecutó `QAS/17` completo, ni `QAS/21`, D5, UAT ni el lote real
de la prueba 7. No se implementaron correcciones, no hubo push ni despliegue, y no se cambiaron secretos.
Ninguna clave aparece en este reporte, en los comandos ejecutados ni en archivos.

## 1. Ambiente, autorización y alcance

| Campo | Valor |
|---|---|
| Ejecutor | Claude Code (Opus 5), sesión iniciada con `GHT_DIAG_KEY` en el entorno |
| Fecha | 2026-08-15, 14:53Z – 20:25Z (con una pausa larga entre la preparación y la ventana) |
| Ambiente | **Azure `app-eltejido-mvp`** — `https://app-eltejido-mvp-evd8ffcgd3fthshw.eastus-01.azurewebsites.net`. `/health` 200; `/health/ready` 200, 10 componentes `ok` y 1 `no_aplica` (`conversacion:umbralResumenConsolidacion`, resumen desactivado) |
| Build desplegado | Corte 2/2 (`a9f4a6f`) confirmado en vivo: el endpoint publica `listoParaGateOn` y `mapeosMeta[]` |
| Aislamiento | ⚠️ **No es un ambiente dedicado.** El emisor saliente sigue sin aislarse (`WhatsAppGateway` real) |
| Autorización recibida | (a) **tráfico WhatsApp saliente real** a los teléfonos de prueba nuevos de esta corrida, autorizado explícitamente por el responsable humano; (b) ventana con el gate ON, abierta y cerrada por el operador; (c) verificación manual de plantillas en Meta; (d) limpieza de campañas residuales de QA |
| `Simulacion__Habilitada` | **true** (heredado de la corrida del 2026-08-14). El webhook entrante fue siempre simulado |
| Cambios hechos por el operador humano | los 6 App Settings de mapeo Meta y el gate ON→OFF. **El agente no tocó App Settings ni secretos** |
| No autorizado / no ejecutado | prueba 7 (lote real), `QAS/17` completo, `QAS/21`, D5, UAT |

### 1.1 Identificador de corrida y datos creados

**Identificador:** `P32-SMOKE-20260815`

| Rol | Id | Código | Idioma | Últimos 4 |
|---|---|---|---|---|
| Admin de diagnóstico | `u_admin_25a8481b4e6f432f84f1646bcc87bd7b` | U-000024 | `es` | `5001` |
| Participante `es` | `u_1cf779115db0490191612f50cbef7d52` | U-000025 | `es` | `5002` |
| Participante `en` | `u_004d3c4b69d449cd8037f76e17968145` | U-000026 | `en` | `5003` |

Los tres números son nuevos: se verificó con `GET /api/admin/usuarios/por-numero/{numero}` que devolvían
`[]` antes de crearlos.

**Fixture de campaña — desviación declarada.** No se creó campaña nueva: se reutilizó
`CAMP-P32-20260814-1503-COMPLETA` (`c_415f2b7acb42414081d7111128ecc88a`, activa, `es`/`en`), **sin modificar
sus textos**. Se eligió deliberadamente porque es el fixture exacto donde el defecto §8.1 del 2026-08-14 se
reprodujo 2/2, lo que convierte esta corrida en una comparación antes/después directa, y porque sus tres
variantes de cierre son distinguibles entre sí (ver §2). El único cambio sobre ella fue asociar los dos
participantes nuevos.

## 2. El discriminador usado en todas las pruebas de cierre

La campaña declara tres textos de cierre **mutuamente distinguibles**, verificados byte a byte:

| Origen | Valor exacto (escapado) |
|---|---|
| `configConversacional.mensajeCierre` (legacy) | `"Gracias. Tu aporte quedo registrado."` — sin tilde |
| `localizaciones.es.mensajeCierre` | `"Gracias. Tu aporte quedÃ³ registrado."` |
| `localizaciones.en.mensajeCierre` | `"Thank you. Your contribution has been recorded."` |

La diferencia entre el legacy y la localización `es` es **solo la tilde**, así que cualquier confusión entre
ambos es detectable. `localizaciones.es.mensajeCierre` está almacenado con mojibake (`quedÃ³`, bytes UTF-8 de
`ó` leídos como Latin-1); es un artefacto **preexistente** de la creación de datos del 2026-08-14, no un
efecto de DT-P32-03, y se conserva porque refuerza el discriminador. Queda anotado en §7.

## 3. Estado del gate

| Momento | `readiness.gateHabilitado` | Verificado |
|---|---|---|
| **Inicial** (14:53Z) | **OFF** (`false`) | por API |
| Ventana autorizada (≈16:24Z – ≈20:12Z) | **ON** (`true`) | por API, abierta por el operador |
| **Final** (20:24Z) | **OFF** (`false`) | por API, tras el reinicio del App Service |

El gate quedó **OFF**, como exige el encargo. Clave usada por el operador:
`Conversacion__CatalogoTextosHabilitado=false` (en Linux, `__` se traduce a `Conversacion:CatalogoTextosHabilitado`).

## 4. Resultados por prueba

| Prueba | Resultado |
|---|---|
| 1 — Regresión gate OFF | **PASS** |
| 2 — Matriz de cierres bilingües (gate ON) | **PASS** |
| 3 — Localización inconsistente | **PASS** |
| 4 — Readiness sin mapeo | **PASS** |
| 5 — Readiness estructural completo | **BLOCKED** |
| 6 — Componentes y límite de la comprobación | **PASS** |

### Prueba 1 — regresión gate OFF · **PASS**

Con el gate en **OFF**, ruta de **salida explícita** ejecutada en un hilo `es` y uno `en`:

| Hilo | `conversacion.idioma` | Estado final | Cierre recibido | Legacy exacto | Fuga a loc.`es` | Fuga a loc.`en` |
|---|---|---|---|---|---|---|
| `…5002` | `es` | cerrada | `"Gracias. Tu aporte quedo registrado."` | **sí** | no | no |
| `…5003` | `en` | cerrada | `"Gracias. Tu aporte quedo registrado."` | **sí** | no | no |

El hilo inglés recibe el cierre legacy **en español**, que es exactamente el comportamiento histórico que la
regresión debe conservar. Además, todo el flujo del hilo `en` con gate OFF salió en español conservando
`idioma="en"`: la instantánea de idioma es independiente del gate.

Las rutas restantes de la prueba 1 (cierre normal por umbral, tope/cupo, fallback e inactividad) **no son
disparables externamente en un smoke**. Quedan cubiertas por la matriz automatizada que el propio `QAS/23`
declara equivalente, y por la garantía arquitectónica: las dos pruebas
`ArquitecturaCierreLocalizadoTests` impiden que exista cualquier lectura directa del campo legacy fuera del
resolutor único, cuya rama gate-OFF devuelve ese campo sin tocarlo
(`GateApagado_ConservaElCierreLegacyEnCualquierIdiomaDelHilo(es|en)` y
`P32_CierreConAgradecimiento_GateApagado_ConservaElCierreLegacyAunEnHiloIngles`).

### Prueba 2 — matriz de cierres bilingües, gate ON · **PASS**

Cuatro cierres reales observados, dos rutas × dos idiomas. **Cero fallback cruzado en las cuatro.**

| Ruta | Hilo | `idioma` | Cierre recibido | Coincide con | Fuga cruzada |
|---|---|---|---|---|---|
| Cierre por umbral (`cierreEvaluacion`) | `…5002` | `es` | `"…quedÃ³ registrado."` | `localizaciones.es` **byte a byte** | ninguna |
| Cierre por umbral (`cierreEvaluacion`) | `…5003` | `en` | `"Thank you. Your contribution has been recorded."` | `localizaciones.en` exacto | ninguna |
| Salida explícita (`cierreConAgradecimiento`) | `…5002` | `es` | `"…quedÃ³ registrado."` | `localizaciones.es` **byte a byte** | ninguna |
| Salida explícita (`cierreConAgradecimiento`) | `…5003` | `en` | `"Thank you. Your contribution has been recorded."` | `localizaciones.en` exacto | ninguna |

**Ninguna salida inglesa contuvo el cierre español**, ni el legacy ni el localizado. En particular, la ruta
de **salida explícita** —que el 2026-08-14 devolvía `"Gracias. Tu aporte quedo registrado."` en el hilo
inglés, reproducido 2/2— ahora devuelve el texto inglés. El defecto §8.1 de aquella corrida está **cerrado y
verificado en el mismo fixture**.

Nótese que el hilo `es` recibió el texto **con** el mojibake de la localización y **no** el legacy sin
tilde: prueba de que el resolutor leyó `localizaciones.es.mensajeCierre` y no cayó al respaldo histórico.

**Alcance declarado:** 2 de las 6 rutas de cierre se ejecutaron en vivo. Las otras cuatro
(`cierreIdeaConsolidada`, `cierreIdeasSegmentadas`, `cierreColaCoaching`, `cierreNeutro`) no son alcanzables
por webhook simulado; están cubiertas por la matriz automatizada con sus dos variantes por ruta
(`P32_Cierre*_HiloIngles_UsaElCierreLocalizado`), que `QAS/23` declara equivalente.

### Prueba 3 — localización inconsistente · **PASS**

Ejecutada por la vía que `QAS/23` prescribe («en una prueba automatizada o fixture aislado»), no contra
configuración compartida. **52 unitarias verdes, 0 fallidas.** Las seis rutas tienen su caso
`P32_Cierre{Ruta}_SinCierreLocalizado_CierraTipificadoSinRespaldoEspanol`, cuyo aserto exige simultáneamente:

- **fallo tipificado** — se envía `MensajeConfiguracionNoDisponibleDefault` con `TipoEnvioMensaje.Cierre`;
- **cero fallback cruzado** — `DidNotReceive()` sobre cualquier salida que contenga el cierre español;
- **sin duplicación al reintentar** — `Received(1)`, exactamente un envío;
- **sin transición parcial** — la conversación queda en `EstadoConversacion.Cerrada`;
- **auditoría sin texto** — `cierre_localizado:LOCALIZACION_CAMPANIA_INCOMPLETA:idioma=en:ruta={ruta}` con
  `campaniaId`.

Complementan `GateEncendidoSinCierreLocalizado_FallaTipificadoYNuncaCaeAEspanol` (con `null`, `""` y `"   "`)
y `GateEncendidoConIdiomaNoHabilitadoEnLaCampania_FallaTipificado`.

### Prueba 4 — readiness sin mapeo · **PASS**

No se retiró ningún mapeo de configuración compartida. **No hizo falta:** el ambiente ya exhibía pares
faltantes reales, y readiness los señaló con precisión (lectura de las 14:53Z, gate OFF):

| Par | `configurado` | Problemas reportados | Campañas listadas |
|---|---|---|---|
| `inicio_segunda` / `es` | false | `nombre_faltante`, `idioma_meta_faltante` | `CAMP-P32-20260814-1503-SEGUNDA` (activa) |
| `inicio_segunda` / `en` | false | `nombre_faltante`, `idioma_meta_faltante` | `CAMP-P32-20260814-1503-SEGUNDA` (activa) |
| *(sin alias)* / `es` | false | `plantilla_ref_faltante` | 3 campañas activas de QA |
| *(sin alias)* / `en` | false | `plantilla_ref_faltante` | 2 borradores INCOMPLETA |

`listoParaGateOn=false` en presencia de cualquiera de ellos, mientras `listo=true` conservaba su significado
editorial. Readiness nombra **exactamente** el par faltante, su motivo y las campañas que lo exigen, con
`campaniaId`, nombre, estado y `mensajeInicialId`, y sin teléfonos ni contenido de participante.

Equivalente en fixture aislado, verde: la integración
`Readiness_ConMapeoMetaSoloEnEspanol_ReportaElParInglesFaltante` (falta selectiva de `en`).

### Prueba 5 — readiness estructural completo · **BLOCKED**

**La mitad estructural sí pasa.** Tras el reinicio del App Service, ambos pares requeridos por la campaña
bajo prueba aparecen configurados y sin problemas:

| Par | `configurado` | `nombreConfigurado` | `idiomaMetaConfigurado` | `componentes` | `problemas` |
|---|---|---|---|---|---|
| `inicio_campania` / `es` | true | true | true | `["nombre"]` | `[]` |
| `inicio_campania` / `en` | true | true | true | `["nombre"]` | `[]` |

**Se declara BLOCKED por dos razones, ninguna imputable al código de DT-P32-03:**

1. **No es verificable por API que los mapeos apunten a las plantillas aprobadas.** El endpoint expone
   `nombreConfigurado` / `idiomaMetaConfigurado` como **booleanos**, nunca el nombre ni el código de idioma.
   Por diseño readiness no consulta Graph API. En consecuencia, el payload es **idéntico** antes y después de
   que el operador aplicara las 6 entradas nuevas, y la corrida no puede afirmar que hoy resuelven a
   `tejido_start_es_co` / `tejido_start_en_us`. Confirmarlo exigiría un envío real, que es la prueba 7,
   explícitamente fuera de alcance.
2. **`listoParaGateOn` sigue en `false`** y no puede llegar a `true` en este ambiente. Tras la limpieza
   autorizada de §6, el único par bloqueante restante es *(sin alias)* / `en`, exigido por los dos borradores
   `CAMP-P32-…-INCOMPLETA`, que habilitan `en` sin localización inglesa. **No existe transición
   `Borrador → Cerrada/Archivada`** (`ServicioGestionCampanias.cs:483-487` sólo permite
   `Borrador→Activa`, `Activa→Cerrada`, `Cerrada→Archivada`), y esos borradores no pueden activarse
   precisamente porque su localización está incompleta: es el fixture de la defensa en profundidad. Quedan
   por tanto atrapados, bloqueando el agregado de forma permanente.

La señal **sí** conmuta a `true` con un mapa completo: la integración
`Readiness_ConMapaCompleto_QuedaListoParaGateOn` está verde. El problema es de alcance de la señal en un
ambiente real, no de su cálculo. Ver §7.1.

### Prueba 6 — componentes y límite de la comprobación · **PASS**

**Componente vacío o duplicado se reporta como inválido.** Verificado por
`Evaluar_ComponenteVacioODuplicado_SeReportaAunqueElParResuelva` (verde), que además comprueba el matiz
importante: el par puede **resolver** y aun así reportarse con problema, de modo que `Listo` sea `false`. Los
códigos `componente_vacio` y `componente_duplicado` son los definidos en `ValidadorMapeosPlantillaMeta`.
Reproducir estos dos casos en vivo habría exigido escribir App Settings compartidos, reservado al operador;
en su lugar se observaron en vivo los otros tres códigos del validador (`nombre_faltante`,
`idioma_meta_faltante`, `plantilla_ref_faltante`), listados en la prueba 4.

**`Componentes=[]` es válido sólo sin variables de cuerpo.** Verificado por
`Evaluar_PlantillaSinVariables_PuedeQuedarListaConComponentesVacios` (verde). Aplicado a este ambiente: las
plantillas aprobadas **sí** tienen una variable de cuerpo, así que `[]` sería incorrecto para ellas y
`Componentes__0=nombre` es la configuración correcta.

**Verificación manual en Meta (la que readiness no puede hacer), aportada por el responsable humano:**

| Plantilla aprobada | Idioma Meta | Variables de cuerpo | Cuerpo |
|---|---|---|---|
| `tejido_start_en_us` | English (US) → `en_US` | 1 | `Hello {{name}}. Your Tejido de Red session is scheduled to start now.` |
| `tejido_start_es_co` | Spanish (COL) → `es_CO` | 1 | `Hola {{name}}. Tu sesión de Tejido de Red está programada para iniciar ahora.` |

Una sola variable de cuerpo en cada una ⇒ un único componente en posición 0. El emisor sustituye **por
posición**, no por el nombre de la variable en Meta: `plantilla.Componentes` se proyecta en orden sobre
`components[0].parameters` (`WhatsAppGateway.cs:102-103`) y `nombre` es la clave interna que resuelve al
nombre del participante (`RenderizadorMensaje.cs:35`). Las entradas correctas son, por tanto:

```
WhatsApp__PlantillaEnvioInicial__Mapeos__inicio_campania__es__Nombre          = tejido_start_es_co
WhatsApp__PlantillaEnvioInicial__Mapeos__inicio_campania__es__Idioma          = es_CO
WhatsApp__PlantillaEnvioInicial__Mapeos__inicio_campania__es__Componentes__0  = nombre
WhatsApp__PlantillaEnvioInicial__Mapeos__inicio_campania__en__Nombre          = tejido_start_en_us
WhatsApp__PlantillaEnvioInicial__Mapeos__inicio_campania__en__Idioma          = en_US
WhatsApp__PlantillaEnvioInicial__Mapeos__inicio_campania__en__Componentes__0  = nombre
```

**El límite de la comprobación quedó demostrado en vivo, no sólo enunciado.** A las 14:53Z readiness
reportaba `inicio_campania` `es`/`en` como `configurado=true`, `componentes=["nombre"]`, `problemas=[]`,
mientras el responsable humano confirmaba que **las plantillas aprobadas todavía no estaban asociadas**. Es
decir: un par puede aparecer estructuralmente listo apuntando a una plantilla que no es la aprobada. Readiness
no puede afirmar aprobación ni correspondencia de variables, y esta corrida lo observó ocurriendo.

## 5. Componentes configurados (sin secretos)

| Par | Nombre | Idioma Meta | Componentes | Orden |
|---|---|---|---|---|
| `inicio_campania` / `es` | configurado (valor no expuesto por la API) | configurado (valor no expuesto) | `["nombre"]` | 1 componente, posición 0 |
| `inicio_campania` / `en` | configurado (valor no expuesto) | configurado (valor no expuesto) | `["nombre"]` | 1 componente, posición 0 |

Cantidad y orden coinciden con las plantillas aprobadas verificadas a mano (1 variable de cuerpo cada una).
Referencia verificable: plantillas `tejido_start_es_co` y `tejido_start_en_us` en el administrador de
WhatsApp de la cuenta del proyecto. No se leyeron ni registraron tokens, App Secret de Meta ni la key de
OpenRouter.

## 6. Limpieza autorizada de campañas residuales

Autorizada explícitamente por el responsable humano durante la corrida. **Reversible y sin borrado de
datos:** sólo cambio de estado `activa → cerrada → archivada`. No se eliminó ninguna campaña, versión de
catálogo, conversación ni idea.

| Campaña | Id | Estado anterior | Estado final |
|---|---|---|---|
| `CAMP-QA-CONV-20260807` | `c_ed2ebc36d1ef406dadf8c705c1b2c48f` | activa | **archivada** |
| `CAMP-QA-CONV-SIMPLE-20260807` | `c_1044c6b0fff548a0bcf4185b8032a85d` | activa | **archivada** |
| `Pruebas campaña convención` | `c_f62a229431b347ba9aae229db0d6b706` | activa | **archivada** |
| `CAMP-P32-20260814-1503-SEGUNDA` | `c_5625fb08cb564bcea117a29cb8de6e81` | activa | **archivada** |

Las cuatro son artefactos de QA (creadas 2026-08-08 y 2026-08-14) y las tres primeras son monolingües `es`
sin localizaciones. La limpieza retiró cuatro de los cinco pares bloqueantes. Efecto secundario a registrar:
al archivar `SEGUNDA` deja de haber dos campañas elegibles simultáneas, de modo que el caso del menú de
selección de campaña (`QAS/16` Prueba 0.2, observación §8.2 del 2026-08-14) requerirá crear de nuevo una
segunda campaña elegible cuando se retome.

## 7. Observaciones

### 7.1 `listoParaGateOn` es inalcanzable mientras exista un borrador bilingüe incompleto

Hallazgo de esta corrida, relevante para el acta de activación. El agregado incluye las campañas
`activa|borrador`. Un borrador que habilita `en` sin localización inglesa aporta el par
*(sin alias)* / `en` con `plantilla_ref_faltante`, que nunca puede quedar `Listo`; y ese borrador no puede
activarse (su localización está incompleta a propósito) ni archivarse (no hay transición desde `Borrador`).
El resultado es que la señal queda en `false` de forma permanente mientras alguien tenga un borrador
bilingüe a medio construir — que es el estado normal de trabajo de un administrador.

No es un defecto de cálculo: conservador es seguro, y el agregado cumple su propósito de impedir un encendido
a ciegas. Pero **como criterio de la prueba 5 no es alcanzable en un ambiente con trabajo en curso**. Merece
decisión de diseño: por ejemplo, excluir del agregado los borradores que ya son inactivables por validación,
o separar «bloqueantes de envío» (campañas activas) de «pendientes de edición» (borradores).

### 7.2 Deduplicación del webhook simulado por día UTC

`POST /diagnostico/simulacion/webhook-entrante` deriva el id del mensaje de
`sha256(numero + texto + fecha UTC)` (`EndpointsSimulacion.cs:205-212`). Repetir el **mismo texto desde el
mismo número el mismo día** produce el mismo id y el mensaje se descarta silenciosamente por el dedupe, sin
error visible: el `POST` responde `200` y no ocurre nada. Costó dos intentos de la prueba 1 hasta
diagnosticarlo. Es comportamiento correcto y deliberado, pero conviene que las guías QAS indiquen pasar
`whatsappMessageId` explícito y único al repetir comandos de control como `terminar por ahora` o `stop now`.

### 7.3 Mojibake preexistente en `localizaciones.es.mensajeCierre`

`localizaciones.es.mensajeCierre` de la campaña reutilizada contiene `quedÃ³` en vez de `quedó`. Es un
artefacto de la creación de datos del 2026-08-14 —doble codificación UTF-8→Latin-1—, **no** un efecto de
DT-P32-03: el resolutor devolvió el valor almacenado byte a byte, que es lo correcto. Si se quiere el texto
limpio, hay que corregir el dato de la campaña. Conviene revisar la ruta de escritura que lo produjo.

### 7.4 Markdown y truncamiento en salidas de coaching

Persiste lo anotado en §8.3 del 2026-08-14: el coaching al participante `es` llegó con encabezados Markdown
(`### Lo que ya queda claro`, `### Lo que todavía falta`, `### Pregunta clave`). Es materia de `DT-I20-02`
(`QAS/21`), no de P-32, y se registra sólo porque apareció en esta evidencia.

### 7.5 El emisor saliente sigue sin aislarse

Sin cambios respecto del 2026-08-14: el webhook entrante fue simulado, pero las respuestas salientes van por
el `WhatsAppGateway` real. En esta corrida el tráfico estuvo **autorizado explícitamente** para los tres
teléfonos de prueba nuevos. No se detectó ninguna llamada a Meta no prevista ni ningún envío de plantilla:
no se ejecutó la prueba 7 ni `POST /campanias/{id}/envios`. La condición estructural sigue siendo un punto
de decisión abierto.

## 8. Estado final

- **Gate `Conversacion:CatalogoTextosHabilitado` → OFF**, verificado por API a las 20:24Z
  (`readiness.gateHabilitado=false`) tras el reinicio del App Service.
- Catálogos: `es` v1 activa y válida, `en` v1 activa y válida; `listo=true`, **0 campañas bloqueadas** en
  ambos idiomas. No se creó, activó ni borró ninguna versión de catálogo en esta corrida.
- `listoParaGateOn=false`, con un único par bloqueante restante: *(sin alias)* / `en`, de los dos borradores
  INCOMPLETA (ver §7.1).
- `Simulacion__Habilitada` sigue en **true**: el operador debe volverla a `false` y retirar `GHT_DIAG_KEY` de
  la sesión (`QAS/18` §Cierre obligatorio).
- Datos conservados, nada borrado: 3 usuarios nuevos, 6 conversaciones nuevas, 4 campañas cambiadas de estado
  de forma reversible. La campaña fixture conserva sus textos intactos.
- La clave de diagnóstico se usó exclusivamente como header `X-Diag-Key`; su valor no aparece en este
  reporte, en ningún archivo del repositorio ni en los comandos ejecutados.
- El único archivo nuevo del repositorio es este reporte. No se tocó código, configuración ni secretos.

## 9. Conclusión

Las pruebas 1, 2, 3, 4 y 6 están en **PASS**. La prueba 5 está en **BLOCKED**: readiness no expone el nombre
de plantilla configurado, de modo que no puede verificarse por API que los mapeos apunten a las plantillas
aprobadas, y `listoParaGateOn` no puede alcanzar `true` en este ambiente mientras existan borradores
bilingües incompletos que no admiten archivado. Un BLOCKED no cuenta como green.

Lo sustantivo de DT-P32-03 sí quedó demostrado contra el ambiente real: **el defecto bloqueante §8.1 del
2026-08-14 está cerrado**. En el mismo fixture donde el hilo inglés recibía `"Gracias. Tu aporte quedo
registrado."` 2 de 2 veces, ahora recibe `"Thank you. Your contribution has been recorded."`, y el hilo
español recibe su propia localización y no el respaldo legacy. Cero fallback cruzado en cuatro cierres reales
con el gate ON, y el cierre legacy exacto preservado en ambos idiomas con el gate OFF.

## **P-32 SMOKE NO GREEN**

P-32 **no** se declara cerrada: la regresión `QAS/17` sigue pendiente para después de `DT-I20-02`.

### Qué desbloquearía la prueba 5

1. **Confirmar el destino real de los mapeos** con un envío real controlado (prueba 7, hoy fuera de alcance),
   o exponer en readiness el nombre y el código de idioma configurados —no son secretos— para que la
   comprobación sea posible sin enviar.
2. **Resolver los dos borradores `CAMP-P32-…-INCOMPLETA`**: completar su localización `en`, o retirarles `en`
   de `idiomasHabilitados`, o decidir el ajuste de alcance de §7.1. Cualquiera de las tres deja
   `listoParaGateOn` alcanzable.
