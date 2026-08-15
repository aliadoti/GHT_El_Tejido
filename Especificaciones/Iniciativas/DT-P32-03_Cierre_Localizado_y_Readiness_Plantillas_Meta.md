# DT-P32-03 — Cierre localizado único y readiness de plantillas Meta

> **Estado:** **IMPLEMENTADA Y DESPLEGADA 2026-08-15 (2/2)** — corte 1/2 (cierre localizado único,
> `50fee37`) y corte 2/2 (readiness Meta y portal, `a9f4a6f`). `main` y `origin/main` apuntan a
> `a9f4a6f`; el push disparó CI y Deploy, que terminaron en success. **El despliegue no activa nada:**
> el despliegue no activó el gate. El smoke posterior confirmó que quedó OFF al terminar. Queda
> El smoke ya ejecutó QAS/23: pruebas 1–4 y 6 PASS, prueba 5 BLOCKED. El pendiente actual es el
> microajuste DT-P32-03-01 y la repetición acotada de pruebas 4–6.
> **Origen:** regresión P-32 del 2026-08-14, §§8.1 y 9.2.
> **Prioridad:** bloqueante para repetir P-32 con el gate ON.
>
> **Corte 2/2 entregado:** `ValidadorMapeosPlantillaMeta` (Application, puro) enumera los pares
> `plantillaRef + idioma` que exigirían las campañas `activa|borrador` con sus mensajes iniciales
> **activos**, deduplicados por alias + idioma y acumulando quién los pide. El veredicto de
> "configurado" delega en `OpcionesPlantillaEnvioInicial.TryResolver` —la misma política que aplica
> `ServicioEnvios`— y encima se reportan `plantilla_ref_faltante`, `nombre_faltante`,
> `idioma_meta_faltante`, `componente_vacio` y `componente_duplicado`; una plantilla sin variables
> puede declarar `componentes: []` y quedar estructuralmente lista.
> `ServicioReadinessCatalogosTextos` expone `MapeosMeta` y la señal agregada `ListoParaGateOn`
> (catálogos válidos **y** mapeos configurados) sin cambiar el significado de `idiomas[].listo`; el
> endpoint los publica de forma aditiva y el portal muestra en **Preparación** catálogos y plantillas
> como comprobaciones separadas, advirtiendo que esto no certifica la aprobación en Meta. Backend
> **854 unitarias + 105 de integración** (15 nuevas), portal **60** (3 nuevas); build Release
> `-warnaserror`, `dotnet format`, `ng test`, `ng build`, Prettier de los archivos tocados y
> `git diff --check` verdes.
>
> **Corte 1/2 entregado:** `IResolutorMensajeCierreCampania` /
> `ResolutorMensajeCierreCampania` (Application, puro) concentra la política OFF/ON y devuelve
> `Disponible(texto, idioma, origen)` o `NoDisponible(LOCALIZACION_CAMPANIA_INCOMPLETA, idioma)`. Las
> seis rutas de cierre del orquestador (`cierreEvaluacion`, `cierreIdeaConsolidada`,
> `cierreIdeasSegmentadas`, `cierreColaCoaching`, `cierreConAgradecimiento` y `cierreNeutro`, que
> cubren cierre normal, umbral/tope, intención de salida, rechazo/avance, cupo LLM, fallback de
> evaluación, inactividad y cierre visible P-33) resuelven **antes** de componer el mensaje o mutar el
> hilo; una localización ausente cierra con el manejo tipificado de configuración no disponible y deja
> auditoría sin texto. No queda ninguna lectura directa de `ConfigConversacional.MensajeCierre` en el
> orquestador y una prueba arquitectónica lo impide en el futuro. Backend **841 unitarias + 103 de
> integración**, build Release `-warnaserror`, `dotnet format` y `git diff --check` verdes.
> Decisión registrada en `SUPUESTOS.md#cierre-localizado-dt-p32-03`.
>
> **Smoke 2026-08-15:** pruebas 1–4 y 6 PASS; prueba 5 BLOCKED. El cierre bilingüe quedó
> demostrado. El bloqueo restante no modifica estos dos cortes: reveló que borradores incompletos
> participan indebidamente en la señal global. El microajuste de un corte se especifica en
> `DT-P32-03-01_Readiness_Gate_Solo_Campanias_Activas.md`. Hasta implementarlo y repetir QAS/23 4–6,
> P-32 smoke permanece NO GREEN.

## 1. Problema confirmado

P-32 exige `localizaciones.{idioma}.mensajeCierre` para activar una campaña bilingüe, pero varias
rutas del orquestador leen directamente `Campania.ConfigConversacional.MensajeCierre`. Por eso un
hilo `en` puede cerrar con el respaldo legacy español. El defecto fue reproducido 2/2.

Además, con `Conversacion:CatalogoTextosHabilitado=true`, el envío proactivo deja de usar la plantilla
legacy y resuelve `plantillaRef + idioma` desde
`WhatsApp:PlantillaEnvioInicial:Mapeos`. El readiness actual no revisa esos mapeos y puede presentar
los catálogos como listos aunque el lote inicial vaya a fallar para todos los participantes.

## 2. Objetivo

1. Resolver el cierre visible una sola vez, con la misma política de idioma en todas las rutas.
2. Impedir cualquier respaldo cruzado `en → es` con el gate ON.
3. Mostrar en readiness todos los mapeos Meta requeridos por campañas activas o borrador. La
   semántica corregida de cuáles bloquean el gate se define en DT-P32-03-01.
4. Distinguir configuración local completa de aprobación/verificación real en Meta.

## 3. Alcance de código

### 3.1 Resolución única del cierre

Crear en Application un único resolutor testeable de cierre de campaña. Puede comenzar como
`IResolutorMensajeCierreCampania` y será absorbido por `IResolutorContenidoCampania` en DT-P32-04 sin
cambiar su política.

Entrada mínima:

- campaña;
- idioma snapshot de la conversación;
- estado efectivo de `Conversacion:CatalogoTextosHabilitado`.

Resultado explícito:

- `Disponible(texto, idioma, origen)`; o
- `NoDisponible(codigo, idioma)`.

Política obligatoria:

| Gate | Resolución |
|---|---|
| OFF | `ConfigConversacional.MensajeCierre`, conservando el comportamiento legacy exacto. |
| ON | `localizaciones[idioma].mensajeCierre`, normalizado con la política P-32. |
| ON y localización ausente/vacía | fallo tipificado `LOCALIZACION_CAMPANIA_INCOMPLETA`; nunca usar el cierre español ni otro idioma. |

Todas las rutas de cierre del orquestador deben consumir este resultado antes de componer el mensaje
o confirmar la transición. Esto incluye, como mínimo, cierre normal, umbral/tope, intención de salida,
rechazo/avance, cupo LLM, fallback de evaluación, inactividad y cierre visible P-33. Se elimina toda
lectura directa de `ConfigConversacional.MensajeCierre` fuera del resolutor, salvo fixtures o código
de administración/persistencia.

Si el contenido localizado no está disponible, la ruta debe conservar estado e idempotencia y usar el
manejo tipificado ya definido para configuración incompleta. No se inventa una traducción, no se llama
al LLM para traducir y no se cae a español.

### 3.2 Readiness de mapeos Meta

Extender `ServicioReadinessCatalogosTextos` —sin consultar secretos ni Graph API— para inspeccionar
las campañas `activa|borrador`, sus mensajes iniciales activos y cada par requerido
`plantillaRef + idioma`.

El chequeo debe reutilizar la misma política de `OpcionesPlantillaEnvioInicial.TryResolver` que usa
`ServicioEnvios`; no se admite una segunda interpretación de “configurado”. Debe reportar, por par:

- `plantillaRef` e idioma interno;
- campañas que lo requieren (`id`, nombre y estado);
- `configurado`;
- presencia de `Nombre` y `Idioma` Meta;
- cantidad y nombres de `Componentes` configurados, porque no son secretos;
- problemas estructurales: nombre faltante, código Meta faltante, componente vacío o duplicado.

Un mensaje inicial activo sin `plantillaRef` no desaparece del diagnóstico: se reporta como
`plantilla_ref_faltante`, incluyendo campaña, idioma y `mensajeInicialId`, y deja
`listoParaGateOn=false`.

El contrato agrega de forma aditiva:

```json
{
  "listoParaGateOn": false,
  "mapeosMeta": [
    {
      "plantillaRef": "inicio_campania",
      "idioma": "en",
      "configurado": false,
      "nombreConfigurado": false,
      "idiomaMetaConfigurado": false,
      "componentes": [],
      "problemas": ["nombre_faltante", "idioma_meta_faltante"],
      "campanias": [{ "campaniaId": "...", "nombre": "...", "estado": "borrador" }]
    }
  ]
}
```

`idiomas[].listo` conserva su significado de catálogo para no romper consumidores existentes.
`listoParaGateOn` es la nueva señal operativa agregada: catálogos requeridos válidos **y** todos los
mapeos Meta requeridos estructuralmente configurados. El portal debe mostrar ambas dimensiones y no
presentar “listo para gate ON” si falta una.

Un arreglo de componentes vacío puede ser válido para una plantilla sin variables y no bloquea por sí
solo. El sistema no puede inferir el número/orden aprobado por Meta ni verificar la aprobación sin una
integración externa; esa comprobación queda identificada como manual en QAS/23.

## 4. Fuera de alcance

- Crear, aprobar o modificar plantillas en Meta.
- Escribir valores concretos de App Settings o inventar nombres/códigos.
- Consultar Graph API desde readiness.
- Traducir textos automáticamente.
- El refactor transversal completo de DT-P32-04.
- Internacionalizar el portal administrativo.

## 5. Seguridad y observabilidad

- Readiness no devuelve secretos, tokens, teléfonos ni contenido de participantes.
- Los nombres físicos Meta y componentes son configuración operativa no secreta.
- Registrar el fallo de resolución con campaña, idioma, ruta/origen y código; nunca copiar el texto.
- No registrar valores de App Settings completos en logs.

## 6. Criterios de aceptación

1. Con gate OFF, todas las rutas conservan exactamente el cierre legacy.
2. Con gate ON, todas las rutas de un hilo `en` usan `localizaciones.en.mensajeCierre`.
3. Ninguna ruta del orquestador lee directamente `ConfigConversacional.MensajeCierre`.
4. Una localización ausente con gate ON falla de forma tipificada y jamás responde en otro idioma.
5. Readiness enumera cada `plantillaRef + idioma` requerido y las campañas afectadas.
6. Falta de `Nombre` o `Idioma` Meta deja `listoParaGateOn=false`.
7. Componentes vacíos o duplicados dentro de una lista configurada se reportan como problema.
8. Una plantilla legítima sin variables puede declarar `Componentes=[]` y quedar estructuralmente lista.
9. Portal y API distinguen `catálogos listos` de `listo para gate ON`.
10. Pruebas de integración cubren lote mixto con mapa completo y falta selectiva de `es`/`en`.

## 7. Pruebas mínimas

- Tabla de regresión de cada ruta de cierre: gate OFF/es, gate ON/es, gate ON/en y localización ausente.
- Prueba estática o arquitectónica que impida nuevas lecturas directas del cierre en el orquestador.
- Unitarias del agregado de mapeos, deduplicación por alias+idioma y problemas estructurales.
- Integración de `GET /api/admin/catalogos-textos/readiness` y portal Preparación.
- Regresión de `ServicioEnvios`: un mapeo faltante falla solo al participante afectado.
- Ejecución manual de `QAS/23` y después `QAS/17` completo.

## 8. Orden de implementación

1. **Corte 1/2:** regresión roja, resolutor único, migración de todas las rutas y pruebas.
2. **Corte 2/2:** contrato/readiness Meta, portal Preparación, pruebas y QAS/23.

No comenzar DT-I20-02 hasta implementar DT-P32-03-01 y obtener green al repetir QAS/23 pruebas 4–6.
Las pruebas 1–3 ya cerraron el defecto bilingüe en el smoke del 2026-08-15.
