# Inventario de puntos multidioma — El Tejido

**Fecha:** 2026-08-14 · revisión de prioridad 2026-08-16
**Alcance del inventario:** actualizado con `DT-P32-04` corte 3/3 local
**Idiomas soportados:** `es` | `en`
**Estado:** núcleo transversal implementado localmente; falta repetir QAS/23 y QAS/17 antes del cierre
operativo, sin encender flags ni operar Azure fuera de una ventana autorizada.

---

Conversacion.Idioma
        │
        ▼
ContextoLocalizacion
        │
        ├── IResolutorTextosGlobales
        │      └── Catálogo versionado en Cosmos
        │
        ├── IResolutorContenidoCampania
        │      └── Localizaciones dentro de Campania
        │
        ├── IResolverPlantillaCanal
        │      └── Alias + idioma → configuración Meta
        │
        └── IPoliticaIdiomaLlm
               └── Directiva de idioma para prompts

ReadinessMultiidioma consulta los cuatro resolutores

> **Arquitectura implementada localmente en `DT-P32-04`:** readiness consulta estos cuatro puertos;
> no reconstruye localizaciones, mapeos Meta ni directivas LLM. Las fuentes y formas persistidas no
> cambiaron.
## 0. Hallazgo transversal: dos espacios de códigos de idioma

Conviven **dos namespaces de código de idioma** que no son intercambiables:

| Espacio | Valores | Dónde se valida | Uso |
|---|---|---|---|
| **Interno (ISO corto)** | `es`, `en` | `IdiomaConversacion` | Todo el dominio, el catálogo de textos y el contenido editorial |
| **Meta / WhatsApp** | `es_CO`, `en_US`, … (código exacto de la plantilla aprobada) | No se valida en dominio; lo rechaza Meta con error `132001` | Solo el campo `language.code` del payload de plantilla |

El **único puente** entre ambos es el mapa `WhatsApp:PlantillaEnvioInicial:Mapeos` en App Settings
(`IResolverPlantillaCanal`). Cualquier idioma nuevo exige tocar los dos espacios.

---

## A. Dónde nace y se propaga el idioma

| # | Punto | Objetivo | Se define / almacena en | Se consume en |
|---|---|---|---|---|
| A1 | `Usuario.Idioma` | Idioma del participante; raíz de toda la cadena | Columna **H "Idioma"** de la plantilla oficial Excel/CSV (I-08 §3) → Cosmos `users`. Default `es` | `Usuario.cs:97`; validación `ServicioCargaMasiva.cs:158` (`MotivoRechazoCarga.IdiomaInvalido`); `PlantillaParticipantes.cs:24,35`; edición manual `ServicioGestionUsuarios.cs:80,131`; filtro de búsqueda `FiltroUsuarios.cs:48` |
| A2 | `Campania.IdiomasHabilitados` | Qué idiomas admite editorialmente la campaña | Cosmos `campanias` (documento histórico sin el campo equivale a `["es"]`) | `Campania.cs:76`; `Campania.TryObtenerLocalizacion` (`Campania.cs:142`); validación de activación `ValidadorLocalizacionesCampania.cs` |
| A3 | `Conversacion.Idioma` | **Snapshot**: fija el idioma al abrir el ciclo, para que un cambio del maestro no parta un hilo en curso | Cosmos `conversaciones` | `Conversacion.cs:95` (normaliza en `:287`); se propaga por todo `OrquestadorConversacion.cs` (~50 usos) |
| A4 | `EnrutamientoAporte.Idioma` | Snapshot equivalente para la selección de campaña / pregunta / idea | Cosmos `enrutamientos` | `EnrutamientoAporte.cs:71` (normaliza en `:392`); menús en `ServicioEnrutamientoParticipacion.cs` |
| A5 | `EnvioMensaje.Idioma` | Snapshot de auditoría del envío proactivo; no contiene contenido del participante | Cosmos `envios` | `EnvioMensaje.cs:61`, escrito por `ServicioEnvios.cs:166` |

---

## B. Contenido editorial por campaña (`LocalizacionCampania`)

Vive en el documento de la campaña, indexado por código ISO corto. Es deliberadamente independiente de
los identificadores técnicos: los IDs de mensaje y pregunta siguen siendo únicos en la campaña y no se
duplican por idioma.

| # | Punto | Objetivo | Se consume en |
|---|---|---|---|
| B1 | `nombre`, `descripcion`, `objetivo` | Contexto de campaña que se inyecta al LLM en el idioma del hilo | `IResolutorContenidoCampania` → `ContenidoCampaniaEfectivo` |
| B2 | `mensajeCierre` | Texto de despedida del recorrido | El mismo `ContenidoCampaniaEfectivo`; sin lectura paralela en el orquestador |
| B3 | `mensajesIniciales[id].texto` | Cuerpo del mensaje de primer contacto | `ServicioEnvios` y orquestador consumen el snapshot efectivo |
| B4 | `mensajesIniciales[id].plantillaRef` | **Alias lógico** de la plantilla Meta. El nombre físico de Meta NO vive aquí (queda en App Settings) | Snapshot efectivo → `IResolverPlantillaCanal` |
| B5 | `preguntas[id].texto` y `.instruccion` | Pregunta visible al participante e instrucción que recibe el evaluador | Snapshot efectivo → contextos de evaluación, segmentación, consolidación y redacción |

**Completitud obligatoria antes de activar** (`ValidadorLocalizacionesCampania.Validar`): por cada idioma
habilitado se exigen los 4 campos de cabecera, más `texto` + `plantillaRef` de cada mensaje inicial
activo, más `texto` + `instruccion` de cada pregunta activa.

---

## C. Catálogo global de textos (`CatalogoTextosConversacion`)

Snapshot **inmutable y versionado por idioma**, administrado fuera del binario. Familia
`catalogo_conversacion`. Una versión activa nunca se edita en sitio; cambiar el contenido no requiere
redeploy ni reinicio.

| # | Punto | Objetivo | Fuente y consumo |
|---|---|---|---|
| C1 | **29 mensajes globales** (`saludoPrimerContacto`, `encabezadoCierreIdea`, `menuAclaracionSalida`, `instruccionSeleccionCampania`, `pausaPorInactividad`, …) | Todo el texto conversacional que **no** pertenece a una campaña concreta | Semilla compilada en `CatalogosTextosSemilla.MensajesEs()` / `MensajesEn()`; en runtime vía `ResolutorTextosConversacion.ResolverParaIdiomaAsync` → `OrquestadorConversacion.cs:4963` (`TextoGlobalParaIdiomaAsync`) y `ServicioEnrutamientoParticipacion.cs:1468+` |
| C2 | **16 grupos de frases** (`continuar`, `confirmar`, `finalizarIdea`, `finalizarParticipacion`, `solicitarMejora`, `rechazoGuardado`, `revisitarAnterior`, `revisitarIdea`, `cambiarCampania`, `despertarProactivo`, `consultarIdea`, `acuseConsultaIdea`, `nuevaIdea`, `invitacionMejoraVariantes`, `invitacionContinuarVariantes`, `acuseContinuarVariantes`) | **Vocabulario de detección de intención por idioma.** No es texto de salida: es *entrada*. Un hilo `en` no puede detectarse con frases españolas | `CatalogosTextosSemilla.FrasesEs()` / `FrasesEn()`; detectores invocados desde `ServicioEnrutamientoParticipacion.cs:212,243,253,297` |
| C3 | Gate `Conversacion:CatalogoTextosHabilitado` | Interruptor operativo. Con **OFF** el runtime no consulta Cosmos y devuelve la semilla española del binario, ignorando `en` | `Program.cs:24` → `ProveedorTextosConversacion.ObtenerParaRuntimeAsync`; rama legacy en `ResolutorTextosConversacion.cs:66-77` |
| C4 | Cascada de degradación | Nunca dejar una conversación sin texto | `Catalogo` → `Cache` → `UltimaVersionValida` → `Emergencia` (`ProveedorTextosConversacion.cs:104-120`), cada degradación auditada en `LogSeguridad` con `idioma`, `origen` y `motivo` |
| C5 | Readiness y precondición de campaña | Impedir activar una campaña `en` sin catálogo `en` activo **y válido** (huella recalculada) | `ServicioReadinessCatalogosTextos`, `DisponibilidadCatalogoTextos.ObtenerIdiomasSinCatalogoActivoAsync`; expuesto en `GET /catalogos-textos/readiness` |
| C6 | Administración por idioma | Editar, versionar, prevalidar y activar contenido sin desplegar | `EndpointsAdminCatalogosTextos.cs` — 17 rutas, la mayoría parametrizadas por `{idioma}` (semillas base/legacy, importación masiva JSON, exportación, activación); portal Angular `features/catalogos-textos` |

---

## D. WhatsApp y plantillas Meta

| # | Punto | Objetivo | Se toma de | Se consume en |
|---|---|---|---|---|
| D1 | **Plantilla HSM de campaña (multidioma)** | Enviar el mensaje inicial de la campaña. Meta exige una plantilla aprobada **por idioma** | App Settings → `WhatsApp:PlantillaEnvioInicial:Mapeos:{plantillaRef}:{idioma}:{Nombre, Idioma, Componentes}` | `IResolverPlantillaCanal`; envío en `WhatsAppGateway` (`language.code`) |
| D2 | Plantilla HSM legacy (única) | Ruta con gate OFF: una sola plantilla para todos los participantes | `WhatsApp:PlantillaEnvioInicial:{Nombre, Idioma}` — default `es_CO` | `ServicioEnvios.cs:176` (`ResolverPlantillaEnvioInicial`) |
| D3 | Plantilla OTP de autenticación | Enviar el código de acceso al portal (categoría *Authentication*, con botón copy-code) | `Auth:OtpWhatsApp:{PlantillaNombre, PlantillaIdioma}` — default `es` | `NotificadorOtpWhatsApp.cs:54` → `WhatsAppGateway.cs:146` |

---

## E. LLM — el idioma como instrucción de prompt

No existen prompts ni rúbricas por idioma. El idioma se inyecta como **directiva** sobre un prompt único.

| # | Punto | Objetivo | Se consume en |
|---|---|---|---|
| E1 | `IDIOMA_DE_SALIDA_OBLIGATORIO` (evaluación y coaching) | Que la retroalimentación y las justificaciones por criterio salgan en el idioma del hilo | `IPoliticaIdiomaLlm` → evaluador |
| E2 | `IDIOMA_DE_SALIDA_OBLIGATORIO` (redacción de turno) | Idem para el turno conversacional compuesto | `IPoliticaIdiomaLlm` → redactor |
| E3 | `IDIOMA_ORIENTATIVO` (clasificador de intención) | Pista, no obligación: clasificar intención de control en el idioma del hilo | `IPoliticaIdiomaLlm` → clasificador |
| E4 | `IDIOMA_DE_SALIDA` (segmentar y consolidar ideas) | Que la idea consolidada quede escrita en el idioma del participante | `IPoliticaIdiomaLlm` → segmentador/consolidador |

---

## F. Gaps detectados

| Id | Gap | Evidencia |
|---|---|---|
| **G1 — RESUELTO** | `MensajeCierre` debía resolverse en un único lugar y cubrir todas las rutas. | DT-P32-03 desplegada; QAS/23 y smoke DT-P32-03-01 confirmaron cierres `es/en` sin fallback cruzado. Las referencias de línea originales quedan como evidencia histórica. |
| **G2** | **Rúbricas y prompts no tienen dimensión de idioma.** `Prompt.cs` y `Rubrica.cs` no declaran campo `Idioma`. El Markdown de la rúbrica viaja al `system` en su idioma original y solo la directiva E1 fuerza el idioma de salida | `grep -n idioma` sobre `Domain/Configuracion/Prompt.cs` y `Rubrica.cs`: 0 coincidencias |
| **G3 — RESUELTO** | Las lecturas de `_mensajes.*` que permanecen son respaldos legacy entregados al resolutor global; las políticas de campaña, Meta y LLM ya no se reconstruyen en consumidores | Guardas arquitectónicas de `DT-P32-04` |
| **G4** | **Encender el gate rompe el envío proactivo si faltan los mapeos D1**, en *todos* los idiomas: sin `Mapeos` operativos, `ServicioEnvios.cs:243-248` falla con `PLANTILLA_CAMPANIA_NO_CONFIGURADA` incluso en `es` | `QAS/resultados/Resultados_P32_Multidioma_2026-08-14.md` §9.2 |
| **G5** | El **portal de administración (Angular) es español fijo**, sin i18n. Gestiona idiomas pero no se traduce a sí mismo | `src/ElTejido.Web/src/app` — sin `@angular/localize` ni archivos de traducción |


---

## Checklist: qué tocar para habilitar un idioma nuevo

1. Ampliar `IdiomaConversacion`; las cinco entidades y readiness consumen esa única política.
2. Añadir columna/valor aceptado en la plantilla de carga (A1).
3. Crear la semilla compilada de mensajes y frases (C1, C2) — **las frases son detección, no traducción literal**.
4. Publicar y activar el catálogo global de ese idioma (C5, C6) antes de activar campañas.
5. Completar las localizaciones de cada campaña (B1–B5) hasta pasar el validador.
6. Aprobar en Meta la plantilla HSM y registrar el mapeo completo
   `plantillaRef → {idioma} → {Nombre, Idioma, Componentes}` (D1).
7. Confirmar `listoParaGateOn=true`; readiness debe aceptar catálogo, campaña, Meta y directiva LLM.
8. Ejecutar regresiones gate OFF/ON, cierres y envío mixto antes de habilitar el idioma.
