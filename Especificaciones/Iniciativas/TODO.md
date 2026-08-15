Eres un **equipo de ingeniería senior con más de 25 años de experiencia** construyendo software de producción. Actúas simultáneamente con la mentalidad y el rigor de estos roles, y declaras explícitamente desde qué rol decides en cada momento:

- **Arquitecto de software / Tech Lead** — protege las fronteras de módulos y los contratos; evita sobre-ingeniería.
- **Ingeniero backend senior (.NET 8 / C#)** — implementa el dominio y la infraestructura.
- **Ingeniero frontend senior (Angular 22)** — implementa el portal.
- **SDET / QA senior** — diseña y ejecuta pruebas en cada paso; nada se da por hecho sin verificación.
- **Ingeniero DevOps senior** — pipelines de GitHub Actions, build reproducible, CD por push a `main`.
- **Ingeniero de seguridad (AppSec)** — secretos, anti prompt-injection, manejo de PII.

Trabajas con humildad y disciplina: lees antes de escribir, avanzas en **pasos pequeños y verificables**, y **documentas tu avance** para que otro agente pueda retomar exactamente donde quedaste.

> **✅ `DT-P32-03` CIERRE LOCALIZADO Y READINESS META — DESPLEGADA 2026-08-15 (2/2, `a9f4a6f`, Claude Opus 5).**
> **Corte 2 (entregado):** `ValidadorMapeosPlantillaMeta` (Application, puro) enumera los pares
> `plantillaRef + idioma` que exigirían las campañas `activa|borrador` con sus mensajes iniciales
> **activos**, deduplicados por alias + idioma y con las campañas que los piden (`campaniaId`,
> nombre, estado, `mensajeInicialId`). "Configurado" delega en
> `OpcionesPlantillaEnvioInicial.TryResolver` —la misma política del envío real— y se suman los
> problemas estructurales `plantilla_ref_faltante`, `nombre_faltante`, `idioma_meta_faltante`,
> `componente_vacio` y `componente_duplicado`; `componentes: []` es legítimo para una plantilla sin
> variables. `GET /catalogos-textos/readiness` agrega `listoParaGateOn` y `mapeosMeta[]` **sin**
> cambiar `idiomas[].listo`, y el portal muestra **Preparación** con catálogos y plantillas como
> comprobaciones separadas, advirtiendo que esto no certifica la aprobación en Meta. Sin Graph API,
> sin secretos y sin escribir App Settings. Backend **854 unitarias + 105 de integración** (15
> nuevas), portal **60** (3 nuevas); build Release `-warnaserror`, `dotnet format`, `ng test`,
> `ng build`, Prettier de los archivos tocados y `git diff --check` verdes.
> **Desplegado 2026-08-15:** `origin/main` en `a9f4a6f`, CI y Deploy en success. El despliegue no
> activa nada: `Conversacion:CatalogoTextosHabilitado` no tiene override en Azure y sigue OFF.
> **Siguiente cambio de código: ninguno en DT-P32-03.** Sigue lo operativo: `QAS/23` pruebas 1 a 7 y
> después `QAS/17` completo; solo con green se retoma `DT-I20-02`.
> **Corte 1 (entregado):** `IResolutorMensajeCierreCampania` /
> `ResolutorMensajeCierreCampania` (Application, política pura) resuelve el cierre visible una sola
> vez. Gate OFF conserva `configConversacional.mensajeCierre` exacto; gate ON usa
> `localizaciones.{idioma}.mensajeCierre` —`es` mantiene su respaldo histórico dentro de la campaña— y
> una localización ausente o vacía devuelve `LOCALIZACION_CAMPANIA_INCOMPLETA`: el hilo cierra con el
> mensaje de configuración no disponible **de su propio idioma**, sin traducir, sin LLM y **sin caer
> nunca a español**. Las seis rutas del orquestador (`cierreEvaluacion`, `cierreIdeaConsolidada`,
> `cierreIdeasSegmentadas`, `cierreColaCoaching`, `cierreConAgradecimiento`, `cierreNeutro` — cubren
> cierre normal, umbral/tope, salida, rechazo/avance, cupo LLM, fallback de evaluación, inactividad y
> cierre visible P-33) resuelven antes de componer o mutar el hilo; en la cola I-18 el texto resuelto
> es además el respaldo determinista del redactor I-20. Auditoría
> `cierre_localizado:<codigo>:idioma=<x>:ruta=<x>` con `campaniaId` y sin texto. **No queda ninguna
> lectura directa de `ConfigConversacional.MensajeCierre` en el orquestador** y dos pruebas
> arquitectónicas lo impiden. Backend **841 unitarias + 103 de integración** (24 nuevas; las 12 de
> ruta se verificaron rojas sin el resolutor), build Release `-warnaserror`, `dotnet format` y
> `git diff --check` verdes. Sin frontend, push, despliegue ni configuración remota; gate OFF.
> Decisión en `SUPUESTOS.md#cierre-localizado-dt-p32-03`; regla de flujo en
> `Reglas_Conversacion_y_Participacion.md §2.12.10`.
> `DT-P32-04` sigue 0/3 para después del green; no bloquea `DT-I20-02`, que conserva la prioridad
> inmediata después del green.

> **✅ `DT-P32-02` SEMILLAS, JSON MASIVO Y READINESS — COMPLETA LOCAL 2026-08-14 (3/3).**
> **Corte 3 (portal):** **Textos de conversación** separa **Crear semilla base** de **Revisar
> configuración anterior**, permite descargar esa configuración aunque su revisión falle, descargar
> el **JSON para edición masiva** de cualquier versión y recorrer descargar → editar → revisar →
> confirmar: el archivo se revisa contra el servidor **sin escribir**, los motivos se explican en
> lenguaje de administrador y solo **Importar como nuevo borrador** crea la versión, que queda
> seleccionada y comparada contra la activa. El selector se limpia tras cada intento (se puede
> reelegir el mismo archivo corregido), valida el máximo configurado antes de leer y nunca repara
> JSON, trunca listas ni activa. El panel **Preparación** muestra el gate real, los límites y, por
> idioma, si hay activo/borrador y qué campañas quedan bloqueadas. Se conservan edición individual,
> guardar, activar, reactivar y rollback. Portal **57/57** (14 nuevas), `ng build` y Prettier verdes
> con Node 24.18.0; backend sin cambios (**817 + 103**).
> **Despliegue confirmado:** `4d0f35c`, CI #110 y Deploy #95 en success. **Siguiente (operativo,
> requiere autorización expresa):** confirmar aislamiento o teléfonos de prueba, ejecutar `QAS/22`
> con gate OFF, abrir la ventana ON controlada de `QAS/18`, completar `QAS/17` y volver a OFF salvo
> acta formal. Solo con ambas guías en green se retoma `DT-I20-02` corte 1/3.
>
> **Corte 2 (histórico):**
> **Corte 2:** `GET .../versiones/{v}/exportar` entrega la forma canónica editable
> (`formato:catalogo-textos/v1` + `metadatos` informativos, nombre `*-editable.json`);
> `POST /importar/prevalidar` revisa el mismo cuerpo que la importación real **sin escribir** y
> responde `200` con `valido:false` cuando el JSON es legible pero incumple reglas; `/importar` se
> reescribió sobre ese validador, exige `application/json`, corta por tamaño **antes de
> deserializar**, acota la profundidad, ignora los metadatos del archivo y crea siempre `v+1` en
> borrador. La selección del portal viaja como `?idioma=`/`?familiaId=` y una discrepancia devuelve
> `no_coincide_con_seleccion`. `GET /readiness` informa el gate **real**, los límites efectivos y, por
> idioma, activo/válida/borrador/semilla base/problemas legacy y campañas bloqueadas. Activar una
> campaña bilingüe exige catálogo activo y válido por idioma
> (`catalogosTextos.{idioma}: activo_requerido`) con el gate ON u OFF; las monolingües españolas
> legacy no cambian. Validado: build Release `-warnaserror`, **817 unitarias + 103 de integración**,
> `dotnet format` y `git diff --check` verdes. Contrato `04` en commit aparte (`77377ec`).
> **Siguiente cambio de código: corte 3/3** (portal, QAS y handoff; detalle en `§8`).
>
> **Corte 1 (histórico):**
> La semilla `es` ya no depende de una fotografía legacy válida: `CrearBase(idioma)` es contenido
> curado compilado que no lee App Settings y `CrearDesdeLegacy` es una fotografía aparte, completa y
> **sin truncar**, que se prevalida antes de guardar. El límite de frases por grupo pasó de 30
> compilado a `Conversacion:CatalogoTextos:MaxFrasesPorGrupo` (default `100`, techo `500`) y se sumó
> `MaxBytesImportacionJson` (256 KiB, techo 1 MiB); un valor fuera de rango se ajusta al techo y un
> exceso de contenido se rechaza entero. El validador expone `Prevalidar(...)` puro —mismos criterios
> que la escritura, conteos y todos los errores, sin contenido— y se agregaron las rutas
> `POST /semillas/{idioma}/base`, `GET /semillas/{idioma}/legacy/preview`,
> `GET /semillas/{idioma}/legacy/exportar` y `POST /semillas/{idioma}/legacy`;
> `POST /semillas/{idioma}` conserva su semántica P-32. Validado: build Release `-warnaserror`,
> **804 unitarias + 94 de integración**, `dotnet format` y `git diff --check` verdes.
>
> Ninguno de los dos cortes toca portal, push, despliegue ni configuración remota; el gate
> `Conversacion:CatalogoTextosHabilitado` sigue OFF.
> Spec `Iniciativas/DT-P32-02_*`; plan `planes/DT-P32-02_*`; QAS `22_*`; decisiones en
> `SUPUESTOS.md#semillas-y-limites-catalogo-dt-p32-02`.
> Después del despliegue autorizado y una corrida completa P-32 green se retoma `DT-I20-02`.
>
> **🟡 `DT-I20-02` CONTRATO VISIBLE EN TEXTO PLANO — ESPECIFICADA 2026-08-13 (0/3, SIN CÓDIGO).**
> El reporte real no proviene de leer un `.md`: el prompt de evaluación de runtime pidió encabezados
> Markdown y estado interno, la evaluación los persistió y WhatsApp transportó el cuerpo resultante.
> La solución aprobada valida únicamente fragmentos generados por el LLM y reemplaza por campo la
> retroalimentación o repregunta inválida, sin descartar puntajes válidos ni cambiar idea/versionId,
> umbrales, madurez, estados, cierres o presupuesto de repreguntas. Prohíbe sanitización global para
> conservar P-33 e ideas legítimas como `caja #3`; mantiene P-27, P-32 y `DT-I20-01`. Incluye selección
> runtime de la versión activa/aprobada más nueva y migración por familia nueva/campaña aislada, sin
> tocar Cosmos durante desarrollo. **Pospuesta hasta cerrar `DT-P32-02` y la nueva corrida P-32 en
> green.** Spec:
> `Iniciativas/DT-I20-02_*`; QAS: `QAS/21_*`; runbook: `planes/DT-I20-02_*`.
>
> **✅ `P-33` CONSULTA Y CIERRE VISIBLE DE LA IDEA — DONE LOCAL 2026-08-13 (3/3).** Consulta pura
> previa a menús/afinidades, selección activa→última propia no rechazada, versión I-19 exacta,
> afinidad de 24 h y reapertura de la misma idea ante una corrección. Incluye gate global OFF,
> opt-outs de campaña, catálogo `es/en`, persistencia/API/portal, telemetría sin contenido y cierre
> visible normal. Validado: build Release `-warnaserror`, **789 unitarias + 87 integración**. Sin
> push, despliegue, activación ni configuración remota. **Siguiente: D5/UAT y acta explícita de
> activación de flags**, empezando por un ambiente aislado. Spec `Iniciativas/P-33_*`, REQ-054 y
> `QAS/20_*`.
>
> **✅ `DT-I20-01` VARIACIÓN Y NO DUPLICACIÓN EN LA REDACCIÓN — DONE LOCAL 2026-08-13 (5/5 de `§7`).**
> `Queda claro que...` sigue permitida pero deja de ser la apertura obligatoria: las instrucciones de
> evaluación y del redactor piden alternar reconocimiento concreto, conexión, pregunta directa o
> transición breve, y cuando el turno lleva retroalimentación validada el redactor debe devolver
> `puente: null` si no aporta una función distinta. La guarda determinista nueva
> (`FiltroDuplicacionTurno`, pura) normaliza sin tildes ni puntuación, compara por oraciones y por
> prefijos de palabras y omite el puente que repita el cuerpo validado; una pregunta duplicada se
> omite si el acto lo admite y, si el acto la exige (`Confirmar`/`Mejorar`/`Aclarar`/`ResumirAvance`),
> el turno sale con su respaldo determinista. La auditoría de redacción añade `ajuste:<motivo>` sin
> texto. **Sin flag, contrato, portal, migración ni configuración remota:** aplica a todo mensaje
> nuevo de todas las campañas tras el despliegue. Backend: build Release `-warnaserror`, **785
> unitarias (766 sin Calibración) + 88 de integración**, `dotnet format` y `git diff --check` verdes.
> **Siguiente: D5 con ejemplos reales anonimizados antes de desplegar; no hay flag que apagar, el
> rollback es revertir el commit.** Spec: `Iniciativas/DT-I20-01_*`; QAS: `QAS/19_*`.
>
> **✅ `DT-P27-01` CONFIGURACIÓN VERSIONADA DE FRASES DE FINALIZACIÓN — DONE LOCAL 2026-08-08 (2/2).**
> Cada lista se valida después de normalizar: vacío, duplicado o límite descarta la lista completa y
> conserva el default compilado. El inicio registra en una bitácora append-only la versión aplicada,
> default o descartada sin exponer aliases; la reversión restaura una revisión del origen de
> configuración o vacía ambas listas. No cambió P-27, sus alias, flags, endpoint, portal ni
> configuración remota. **`DT-P32-02` quedó COMPLETA local (3/3) el 2026-08-14; lo pendiente es la corrida autorizada.**
>
> **✅ `P-32` CONVERSACIÓN MULTIDIOMA Y CATÁLOGO DE TEXTOS — 4/4 DONE LOCAL 2026-08-11.**
> Verificado: `Usuario.Idioma` ya existe como campo de primer nivel, admite `es|en`, usa `es` por
> defecto y está propagado a Cosmos/API/carga/portal. La iniciativa consume ese dato y define:
> catálogo global versionado por idioma en Cosmos `config`; localizaciones de campaña bajo los mismos
> ids; plantilla Meta resuelta por participante; snapshot de idioma por hilo; caché/última versión
> válida; API/portal, JSON como importación/exportación de borradores, auditoría sin contenido, plan de
> migración y QAS. **App Settings se conserva para operación** (flags, límites, timeouts, secretos
> referenciados, caché y mapeos Meta); mensajes/frases editoriales dejan de ser su destino final.
> Ya están implementados dominio, validación/huella, versionado/ETag, activación atómica, repositorios,
> API, import/export, permisos, auditoría, proveedor con caché/LKG, emergencia del mismo idioma,
> semillas explícitas `es/en` y gate `CatalogoTextosHabilitado=false`. La semilla `es` fotografía la
> configuración efectiva y toda semilla queda borrador. El corte 2a ya fija el `idioma` en cada
> conversación/ciclo, conserva `es` en documentos históricos, lo expone en Resultados y deja un
> adaptador proveedor→textos sin conectar a salidas visibles. El corte 2b1 ya conecta mensajes
> globales registrados y variantes del orquestador, sin cambiar el gate. El corte 2b2 completa
> enrutamiento, detectores y aclaraciones P-27 por catálogo/snapshot. El corte 3 ya entrega
> localizaciones de campaña, edición de portal, validación y envío inicial mixto por participante;
> conserva el gate OFF y el legado. **Los cuatro cortes están terminados:** el corte 4 propagó el
> idioma a evaluación, segmentación, consolidación y redacción, y completó el portal de catálogo y
> su rollback. La corrección local del 2026-08-13 habilita la reactivación real de una versión
> inactiva y bloquea campañas bilingües incompletas en activación, asociación y enrutamiento.
> Backend: 770 unitarias + 87 integración; portal 43/43 previo, build Angular y Prettier verdes.
> Sin despliegue,
> configuración remota ni activación; se requieren D5/UAT, traducción inglesa y plantillas HSM aprobadas.
> Spec: `Iniciativas/P-32_Conversacion_Multidioma_y_Catalogo_Textos.md`; inventario:
> `planes/P-32_Inventario_y_Migracion_Textos.md`; QAS: `QAS/16_*` y prompt ejecutable `QAS/17_*`.
>
> **Registro de preparación — INICIATIVA OBJETIVO 2026-08-08 — `DT-QA-02` LISTADO DE EVALUACIONES.
> IMPLEMENTACIÓN INMEDIATA. SPEC Y CONTRATO LISTOS, SIN CÓDIGO AÚN.**
> `GET /api/admin/evaluaciones` devuelve **404**: la colección no existe (sí existe
> `/evaluaciones/{id}`, `EndpointsAdminResultados.cs:32`). Es el único recurso de `04 §5.8` con
> detalle y sin listado, así que **una evaluación persistida sin quedar enlazada es invisible desde la
> API** — solo se llega a ella si ya se conoce su `id`. Bloquea el diagnóstico del dry-run E2E, y con
> el freeze el 11-ago es la ventana justa.
>
> **El 404 no es un olvido de ruta: falta el método en el puerto.** `IRepositorioRespuestas` solo
> tiene `ObtenerEvaluacionPorIdAsync` / `ObtenerEvaluacionPorRespuestaAsync` (ambos exigen conocer un
> id) y dos agregados (`ContarEvaluacionesUsuarioAsync`, `SumarTokensCampaniaAsync`). Hay que agregar
> `ListarEvaluacionesAsync` **sin implementación por defecto** —a diferencia de los métodos I-19/P-26
> del puerto— porque un default vacío haría que un adaptador no implementado reporte "cero huérfanas"
> en vez de fallar, que es justo el silencio contra el que sirve este endpoint.
>
> **Cuatro estados de enlace, derivados en consulta (no persistidos):** `enlazada`, `huerfana`
> (`respuesta_inexistente` | `respuesta_id_vacio`), `superada` (`evaluacion_mas_reciente_existe`) y
> `sin_version_idea`. ⚠️ **`superada` NO es un error:** I-16 ya contempla varias evaluaciones para el
> mismo `respuestaId` (campañas reutilizadas o legacy); marcarlas como huérfanas llenaría el dry-run
> de falsas alarmas. `sin_version_idea` importa porque, según `03 §3.9`, una evaluación sin
> `versionIdeaId` **no puede promover una idea a madura** — es la causa típica de "no entiendo por qué
> esta idea no salió madura".
>
> **El DTO de lista NO lleva texto libre** (`explicacion`, `retroalimentacionEnviada`,
> `parafraseoDevuelto`, `repreguntaSugerida`, `calificacionPorCriterio`, snapshots): parte de eso es
> el aporte del participante y la lista debe poder pegarse en un reporte de QA sin filtrar PII a mano.
> El contenido sigue en `/evaluaciones/{id}`.
>
> **Aditivo y de solo lectura:** no toca `03`, ni `/evaluaciones/{id}`, ni el criterio de "evaluación
> vigente" de I-16 (lo **refleja**; hay una prueba cruzada que lo fija). Sin flags ni dependencias
> nuevas. **Fuera de alcance:** reparar huérfanas (esto diagnostica, no arregla) y UI en el portal.
> Orden en `DT-QA-02 §8`. Spec: `Iniciativas/DT-QA-02_Listado_Evaluaciones_Y_Huerfanas.md`;
> contrato ya actualizado en `04 §5.8`.
>
> **✅ `I-08 v2` CARGA MASIVA — COMPLETA LOCAL 2026-08-07 (7/7 pasos de `§8`) Y DESPLEGADA.
> Commits `63fa3e7`, `d07b9f0`, `e5e4b37`, `982c7b7`. Desplegada y validada; el siguiente cambio de
> código es `DT-QA-02` (arriba).**
> **Corte 4 (paso 7, portal):** paneles standalone al estilo P-16; carga que acepta `.xlsx` y `.csv`
> con selector de modo, descarga de plantilla y reporte por fila; **resolución de conflictos de
> titular** (actual vs. propuesto, con `corregir_nombre`/`reasignar`/`omitir` y reenvío del mismo
> archivo); motivos traducidos a lenguaje del administrador; ficha con histórico del número y
> reasignación manual. Portal **43/43**, build de producción y prettier verdes; backend **808**.
> 💡 **`ng test`/`ng build` SÍ corren en esta máquina** pese al Node 22.17, con un Node temporal:
> `npx -y -p node@24.15.0 node ./node_modules/@angular/cli/bin/ng.js test --watch=false`.
> `tsc` solo **no basta**: pasó limpio mientras el compilador de Angular encontraba tres roturas reales.
> **`I-08 v2` DESPLEGADO** el 2026-08-07 y validado contra Azure en la corrida conversacional del
> 2026-08-08 (`QAS/resultados/Resultados_E2E_Conversacional_20260807.md`): **13 casos PASS, 0 fallos**.
>
> 📅 **Fechas vigentes (confirmadas 2026-08-08): freeze 11-ago, hito de envío 12-ago.** Ambas se
> corrieron dos días respecto del plan original (freeze 8–9, hito 10-ago). Todos los documentos ya lo
> reflejan.
> **Paso 3 hecho por el usuario (2026-08-07):** `users` recreado con unique key `/claveUnicidad`,
> admin `U-000001` sembrado, purga de campañas ejecutada desde el portal (5 campañas, 27
> conversaciones, 81 respuestas, 189 participantes). `security`, `config` y `leases` se conservaron a
> propósito. **Decisión del usuario: el campo se queda `claveUnicidad`**, no se renombra a inglés.
> **Corte 3 (paso 6 completo):** alta/edición con los campos del maestro, email único entre activos
> (`409`), `POST /usuarios/{id}/reasignar-numero` con compensación, `GET /usuarios/plantilla-carga`
> generada desde la misma definición que usa el lector, y filtros `empresaId`/`sede`/`idioma` +
> búsqueda por email y código. Backend **808** (725 + 83).
> **Falta solo el portal**, y luego un despliegue único (cortes 2 y 3 siguen sin push).
> **Corte 2 (pasos 4 y 5, mas el endpoint del paso 6):** `PlantillaParticipantes` centraliza las 9
> columnas y sus conversiones; lector `.xlsx` con ClosedXML (primario, toma la antigüedad del valor de
> la celda y no del texto formateado) y `.csv` de respaldo, con una prueba que compara que den las
> mismas filas; `ServicioCargaMasiva` en dos pasadas (planear sin escribir → ejecutar) con modos
> `upsert`/`solo_actualizar`, conflicto de titular ≥ 0,85 y sus tres resoluciones, reasignación
> ordenada con compensación, tag de empresa derivada y auditoría sin PII. Backend **799**
> (719 + 80). **Paquete nuevo: ClosedXML 0.105.1.**
> **Falta:** del paso 6, los request DTOs de alta/edición, `reasignar-numero` y la descarga de
> plantilla; y el paso 7 (portal).
> **Corte 1 (pasos 1 y 2 de `§8`) entregado:** `Usuario` con `codigoUsuario` (obligatorio, lo asigna
> la secuencia), `usuarioWhatsapp`, `empresaId`, `sede`, `cargo`, `email`, `antiguedadAnios`
> (`decimal?` sin redondear) e `idioma` (`es|en`, default `es`); `area`/`empresa` **opcionales**;
> `claveUnicidad` derivada **solo** en el mapeo a documento; `ObtenerUsuarioPorNumeroAsync` filtra
> `estado = activo` dentro del repositorio (Cosmos, memoria y los 6 dobles de prueba) y aparece
> `ListarUsuariosPorNumeroAsync`; contador `Secuencia` (`seq_usuario`) con ETag, reintento en
> `412`/`409` y reserva por bloque; DTO de `04 §5.1` ampliado de forma aditiva. Backend **762**
> (685 unitarias + 77 de integración), build Release `-warnaserror`, `dotnet format` y
> `git diff --check` limpios. **Sin push, sin desplegar y sin recrear la base.**
> ⚠️ **El paso 3 (recrear `users` con unique key `/claveUnicidad`, sembrar el admin y verificar el
> `409`) bloquea los pasos 4-7 y es IRREVERSIBLE** — las unique keys de Cosmos son inmutables. El
> esquema ya está cerrado en código, así que es el momento de hacerlo. Detalle en `AVANCES.md`
> ("Próximo paso") y en `I-08 §3.2`.
> `P-31` quedó **DONE 3/3 y desplegado** (commit `6d02492`), así que liberó la ruta. `I-08 v2` es lo
> único que bloquea el freeze (11 ago).
> ⚠️ **P-31 ya está desplegado:** la recreación de la base borrará el estado del entorno donde se
> validó. No se pierde código ni configuración, pero conviene **repetir la prueba de humo de P-31**
> (`QAS/14_P31_Resumen_Consolidacion_Como_Probar.md`) después de recrear y sembrar.
> GHT entregó la plantilla oficial (`Información asistentes convención gerentes 2026 V1.xlsx`, 129
> filas) y sus columnas **no son** las de la plantilla que se implementó en julio. I-08 figuraba `DONE`
> (backend 15-jul, UI 20-jul); **vuelve a estar abierta**. Columnas oficiales, en orden fijo:
> `Empresa | ID Empresa | Sede | Nombre | Cargo | Email | Antigüedad en la empresa en años | Idioma | Telefono`.
> Esto **cierra el insumo pendiente de Munir** (ítem 22): las variables demográficas son estas.
>
> **Decisiones del usuario (2026-08-07), ya incorporadas a la spec:**
> 1. Los campos nuevos van **de primer nivel** en `Usuario`, no en `propiedadesDinamicas`.
> 2. **Obligatorios solo `Nombre` y `Telefono`** (sin teléfono no hay WhatsApp). **`Email` deja de ser
>    obligatorio**; si viene, es único entre activos. `area`/`empresa` dejan de ser requeridos en
>    `Usuario.Crear`.
> 3. **`codigoUsuario`**: identificador secuencial legible (`U-000042`) vía documento `Secuencia` con
>    ETag y reserva de bloque por lote. El `id` técnico sigue siendo `u_<guid>` para no migrar las
>    referencias de `03`.
> 4. **Un número puede reasignarse entre personas**: a lo sumo **un usuario `activo` por teléfono**;
>    el anterior queda `inactivo` conservando número e historial. La trazabilidad de campañas cuelga
>    del `id` viejo.
> 5. **Conflicto de titular**: nombre distinto sobre teléfono existente **no** se resuelve solo →
>    `rechazado(conflicto_titular)` y el admin decide por fila (`corregir_nombre`/`reasignar`/`omitir`).
>    Similitud ≥ 0,85 se trata como typo y actualiza sin conflicto.
> 6. **`usuarioWhatsapp`** opcional (identificación por usuario de WhatsApp), solo portal, **no** se
>    carga del archivo y **no** participa aún en el enrutamiento.
> 7. Modo **`solo_actualizar`** para actualización masiva por teléfono sin crear registros.
> 8. **Antigüedad decimal** sin redondear; **`Idioma` default `es`** (`es|en`).
>
> **Dos trampas encontradas al especificar, que hay que respetar al implementar:**
> - **La unique key de `users` cambia a `/claveUnicidad`** (campo derivado: `wa|<numero>` si activo,
>   `hist|<id>` si inactivo, `tag|<id>`, `seq|<id>`). No basta con quitar la de
>   `/whatsappNormalizado`: Cosmos trata el path ausente como `null` y **también lo hace único**, con
>   lo que las `Tag` del mismo contenedor colisionarían entre sí. Las unique keys son **inmutables** →
>   hay que **recrear el contenedor**. El usuario confirmó que **la base se puede borrar y recrear con
>   solo el admin**, así que **no hay backfill ni migración** (`I-08 §3.2`).
> - **`ObtenerUsuarioPorNumeroAsync` debe filtrar `estado = activo`**, dentro del repositorio (los 7
>   puntos de uso lo requieren por igual). Si no, un mensaje entrante puede resolverse al titular
>   anterior. Ajustar también `RepositoriosMemoria`, que hoy hace `FirstOrDefault` sin filtrar, o las
>   pruebas pasarán con un comportamiento distinto al de producción. Se agrega
>   `ListarUsuariosPorNumeroAsync` para el histórico.
>
> **Orden de implementación** en `I-08 §8`. El paso 3 (recrear la base + semilla + verificar el `409`)
> **bloquea** todo lo que sigue y conviene hacerlo apenas el esquema esté cerrado.
> **Por ahora NO se carga ningún dato**: la carga real es un paso del freeze y GHT todavía debe
> entregar el archivo con la columna `Telefono` diligenciada (§9 de la spec).
> Spec: `Iniciativas/I-08_Carga_Masiva_Participantes.md`; plantilla vacía en
> `Iniciativas/plantillas/plantilla_participantes_v1.{xlsx,csv}`; contratos ya actualizados en
> `03 §2/§3.1/§3.1.1/§3.2/§5/§6`, `04 §5.1`, `06 §2.1/§3.2` y `Guia_Azure_Portal §2.1`;
> supuesto `SUPUESTOS.md#carga-masiva-plantilla-oficial-i08-v2`.
>
> **✅ `P-31` DONE 3/3 Y DESPLEGADO (2026-08-07).** Commits `6ba6ce0` (corte 1, perilla/política),
> `32794fb` (corte 2, auditoría y enganche) y `6d02492` (corte 3, E2E simulada: inicio → aporte sobre
> umbral → resumen → mejora sin repetirlo). Validación verde: build Release, **664 unitarias + 77 de
> integración**, formato y `git diff --check` limpios. Guía simple:
> `QAS/14_P31_Resumen_Consolidacion_Como_Probar.md`. Verificado en código: `ResolverUmbralResumen` ya
> se consume en `OrquestadorConversacion` (en el corte 1 solo se calculaba, sin efecto observable).
> **Flags siguen OFF** (`Conversacion:ResumenConsolidacionHabilitado` + opt-out por campaña):
> encenderlos exige D5 real, UAT y acta de flags. **Calibración pendiente (decisión de negocio):** con
> `umbralCierreAnticipado=0.6`, el umbral de resumen útil está entre **0.40 y 0.55**; si GHT lo quiere
> al 70 %, hay que subir el umbral base y eso mueve la distribución maduro/incubación de D5.
> **Decisión resuelta:** consulta bajo demanda y visibilidad al cierre pertenecen a P-33; P-31
> conserva solo su resumen proactivo por umbral.
> **`DT-P27-01` corte 2 sigue abierto**; retomar después de `I-08 v2`.
>
> <details><summary>Contexto original de P-31 (histórico)</summary>
>
> Viene de REQ-052 (GHT, 2026-08-06): los participantes quieren
> visibilidad del progreso de su idea. Hoy la versión consolidada de I-19 solo se muestra al confirmar
> (§4.1) o al reabrir (§4.7); en el coaching normal (P-25) nunca, y al cruzar el umbral base la rama
> `madura` de `ConfirmarOCorregirIdeaAsync` cierra idea e hilo sin mostrarla.
> `P-31` agrega un **umbral de resumen propio** —`Conversacion:UmbralResumenConsolidacion`, con
> override por campaña y por pregunta— **independiente** del `umbralCierreAnticipado` de I-17/P-13:
> cuando la evaluación lo cruza y la idea **sigue abierta**, el turno de coaching que ya se iba a
> enviar lleva el **texto de la versión vigente insertado server-side** más una pregunta de
> continuidad.
> **Invariantes que no se pueden romper:** no crea estado en la máquina conversacional (el hilo queda
> en `esperandoRepregunta`); no toca el sellado de `nivelMadurez` ni sus telemetrías; no incrementa
> `repreguntasUsadas`; no confirma, evalúa ni cierra; se envía **una sola vez por idea** (idempotencia
> persistida en `IdeaConsolidada` + documento Cosmos, ausente ⇒ `null`); **no depende de los flags de
> P-27** (la respuesta se resuelve con el vocabulario de continuar de 05 §4.4, que opera sin flags); y
> el LLM no puede alterar, resumir ni omitir el texto consolidado.
> Kill-switch `Conversacion:ResumenConsolidacionHabilitado=false` + opt-out por campaña.
> **Calibración (decisión de negocio, §11.2 de la spec):** con `umbralCierreAnticipado=0.6`, un umbral
> de resumen entre **0.40 y 0.55** deja espacio real; si GHT quiere el resumen al **70 %**, el umbral
> base debe subir por encima (p. ej. `0.8`), lo que mueve la distribución maduro/incubación que D5
> está calibrando. Si `umbralResumen >= umbralBase`, el resumen es inalcanzable: **diagnóstico de
> arranque, no error**.
> **Decisión resuelta el 2026-08-13:** la consulta bajo demanda se implementa en P-33 como intención
> reactiva independiente; no cambia el alcance proactivo de P-31.
> Spec: `Iniciativas/P-31_Resumen_Consolidacion_Por_Umbral.md`; requerimiento:
> `Client_partner/.../Nuevas iniciativas/REQ-052_Visibilidad_progreso_de_la_idea.md`; supuesto:
> `SUPUESTOS.md#resumen-consolidacion-p31`.
>
> </details>
>
> **Soporte de inglés P-32: 4/4 DONE local (2026-08-11); pendiente solo de validación operativa.**
> `I-08 v2` aporta `Usuario.Idioma` (`es|en`) y P-32 ya lo consume en runtime, campañas, envíos y
> contextos LLM. No activar sin traducciones aprobadas, plantillas Meta inglesas, D5/UAT bilingüe,
> revisión de costo/latencia y acta de cambio.
>
> **ESTADO VIGENTE 2026-08-04 — `P-30` COMPLETA local (3/3).** El participante puede pedir retomar
> una idea histórica propia dentro de la campaña y pregunta resueltas por P-26, elegirla por número o
> por título/resumen exacto no ambiguo y continuar sobre el mismo `ideaId`. El selector es
> determinista, no filtra por estado ni ciclo, suspende la curaduría mientras se resuelve y no usa
> búsqueda semántica/vectorial. Kill-switch `Conversacion:RetomarIdeasHabilitado=false`, telemetría
> `retomarIdea` sin texto, persistencia Cosmos y E2E simulada completa. Backend **729** (657 unitarias +
> 72 de integración), build Release, `dotnet format` y `git diff --check` verdes; sin push, despliegue
> ni configuración remota. Spec y QAS actualizados.
>
> **`DT-QA-01` DONE local 2026-08-05 — habilitador E2E sin exponer el App Secret.**
> Habilitador de las **pruebas E2E conversacionales contra el entorno desplegado** sin exponer el App
> Secret de Meta. `POST /diagnostico/simulacion/webhook-entrante` ya está en el grupo
> `/diagnostico/simulacion` (protegido por `FiltroClaveDiagnostico` = `X-Diag-Key`, mapeado solo en
> Development o con `Simulacion:Habilitada=true`): recibe `{numero, texto, whatsappMessageId?, phoneNumberIdDestino?}`,
> construye el `WhatsAppWebhookPayload` y **encola por `IColaWebhook.EncolarAsync`** — el mismo paso que
> hace `EndpointsWebhook.RecibirAsync` **tras** validar la firma — **sin exigir firma** (ya autenticado
> por la clave de diagnóstico). **NO** relajar la firma del webhook real (`/webhook/whatsapp` sigue
> exigiéndola). Auditoría sin número/texto, id estable para el dedupe, 7 pruebas focalizadas y backend
> no calibración 734/734 verdes.
> Aditivo; requiere **desplegar** para usarlo contra Azure. Spec:
> `Iniciativas/DT-QA-01_Inyeccion_Webhook_Simulado_Diagnostico.md`; supuesto
> `SUPUESTOS.md#inyeccion-webhook-diagnostico-dt-qa-01`. **Siguiente cambio: DT-P27-01 corte 2.**
>
> **INICIATIVA EN CURSO 2026-08-05 — `DT-P27-01` corte 1 de 2 DONE local.** Las dos listas de alias de
> finalización ya se leen desde `Conversacion:FrasesFinalizarIdea` y
> `Conversacion:FrasesFinalizarParticipacion`; configuración ausente/vacía conserva los defaults
> compilados y una lista configurada reutiliza la normalización vigente. Backend 730/730, build
> Release, regresión focalizada y formato verdes. **Siguiente: corte 2**, validación tras normalizar (vacíos,
> duplicados y límite) con descarte + registro del motivo, historial/rollback y cierre documental.
> No editar por campaña, no cambiar los alias vigentes ni activar P-27. Spec:
> `Iniciativas/DT-P27-01_Config_Versionada_Frases_Finalizacion.md`;
> supuesto `SUPUESTOS.md#config-frases-finalizacion-dt-p27-01`. En paralelo sigue pendiente la
> validación operativa D5/UAT/costo y el acta de flags de I-19/I-20/P-24/P-25/P-26/P-27/P-28/P-29/P-30.
>
> **CONTEXTO PREVIO — `P-29` corte 1 de 2.**
> **P-27 quedó COMPLETA local (5/5).** Corrige las salidas naturales con alias deterministas y, detrás
> de flags OFF, un clasificador LLM cuya etiqueta es validada y ejecutada exclusivamente por el
> servidor. El corte final incorpora consumo persistente de llamada/token, ventana móvil P-26, E2E,
> banco de variaciones y QAS. Backend **698/698** (632 unitarias + 66 de integración), build y formato
> verdes; no hubo push, despliegue ni configuración remota. Sigue pendiente la activación operativa
> (D5, UAT, costo/latencia y decisión de flags).
>
> **`P-28` quedó COMPLETA local (3/3).** Saludo/inicio breve sin flujo, selección P-26 cuando hay
> varias campañas, bienvenida LLM con fallback, telemetría sin texto y E2E simulada. El saludo nunca
> crea una idea; el aporte posterior sigue por P-26. `Conversacion:DespertarProactivoHabilitado=false`
> permanece apagado; sin push, despliegue ni configuración remota.
>
> El corte 1/2 (enganche del aviso humano al cierre por inactividad ya existente) quedó **DONE local**
> el 2026-08-04; el detalle está en la cabecera. Spec:
> `Iniciativas/P-29_Cierre_Conversacional_Por_Tiempo.md` y supuesto
> `SUPUESTOS.md#cierre-por-tiempo-p29`.
>
> **BACKLOG ACOTADO 2026-08-04 — `P-28`, `P-29` y `P-30` están completas localmente.**
> Vienen de REQ-012/013/014 y cubren vacíos, no capacidades ya entregadas:
> `P-28` entrega entrada humana para saludo/inicio no sustantivo sin flujo (el aporte sustantivo inicia
> ciclo directo por P-26); `P-29` solo mensaje humano de pausa tras el cierre determinista por
> inactividad ya existente en I-17/I-19; `P-30` selector histórico por participante, campaña y
> pregunta que amplía la reapertura reciente I-19/P-26. Los tres son aditivos, con kill-switch global OFF
> por defecto. Specs: `Iniciativas/P-28_Despertar_Proactivo_Coach.md`,
> `Iniciativas/P-29_Cierre_Conversacional_Por_Tiempo.md`, `Iniciativas/P-30_Retomar_Ideas_Del_Pasado.md`;
> decisiones en `SUPUESTOS.md#despertar-proactivo-p28`, `#cierre-por-tiempo-p29`, `#retomar-ideas-p30`.
>
> **✅ `P-26` (participación continua y selección de campaña/pregunta) — COMPLETA local 2026-07-31
> (6/6 cortes, Claude Fable 5).** Backend **654/654** (590 unit + 64 integración), portal **30/30**,
> format y diff limpios; **flag apagado por defecto**. Corte 6: acciones `cicloNuevo`/`reapertura` y
> `latenciaMs` completan las métricas §10 (derivables sin contadores nuevos, `10 §6.2`); E2E simulada
> del criterio 16 sin WhatsApp real; serialización §11 verificada (la cola del webhook tiene un solo
> lector); y los 16 criterios §12 cubiertos, añadiendo los dos que faltaban (campaña cerrada nunca es
> candidata; apagar el flag deja terminar la idea abierta y bloquea la siguiente). **Pendiente
> operativo, no de código:** D5 real, UAT y costo junto con I-19/I-20/P-24/P-25, y P-27 lista antes
> de activar estos flujos. Detalle de cortes 1-5 abajo.
>
> **HISTÓRICO — cortes 1 a 5 de `P-26` (Claude Fable 5).**
> Corte 1: flag `participacionContinua` (histórico = `false`) con round-trip Cosmos/API/duplicado,
> campos de ciclo en `Conversacion` y tipo `EnrutamientoAporte` (03 §3.6.1) con puerto y repos
> memoria/Cosmos idempotentes. Corte 2: el webhook resuelve la campaña de forma determinista antes
> del orquestador (05 §4.3 paso 0) — `ResolverCandidatosAsync` aditivo,
> `ServicioEnrutamientoParticipacion` con elegibilidad §5.2 (0 → silencio neutral, 1 → flujo actual,
> N → aporte conservado + menú numerado configurable), selección número/nombre exacto no ambiguo,
> intentos auditados sin texto, revalidación al aceptar, expiración lógica 24 h, entrega única
> `listo→enIdea` y telemetría `LogSeguridad(enrutamientoParticipacion)`. Corte 3: selección de
> pregunta (§5.4) con menú propio y revalidación —campaña continua completada reabre todas sus
> preguntas activas—, afinidad §5.6 que enruta el coaching sin menús mientras la conversación siga
> abierta y con ventana vigente (y se marca `completado` al cerrarse la idea), cambio explícito de
> campaña (`Conversacion:FrasesCambiarCampania`) que suspende la afinidad sin cerrar la idea, y
> ciclos nuevos §5.7 vía `ProcesarAporteEnrutadoAsync` con id determinista por mensaje raíz
> (`cicloParticipacion+1`, hilo anterior intacto). Corte 4: la reapertura explícita (§5.8) reabre el
> hilo que contiene la idea y conserva su `ideaId` reutilizando I-19 §4.7 en vez de abrir un ciclo
> —un aporte normal sí lo abre—, y los cupos por participante (§9) usan ventana móvil de 24 h en
> campañas continuas, con `presupuestoTokensCampania` acumulado y `MaxTurnosPorHilo` por ciclo sin
> cambio. Corte 5 (frontend-only): fieldset propio **“Participación continua”** en Campañas →
> Configuración —separado del estado— con el checkbox “Permitir nuevas ideas después de finalizar”
> (default OFF), ayuda asociada por `aria-describedby`, aviso en `role="status"` al apagarlo y
> round-trip completo del flag; admin edita y el visor no puede guardar. Backend **648/648**, portal
> **30/30**, prettier/tsc/build de producción limpios (Node 24.18.0); sin push.
> **Trabajo ejecutable actual: ninguno priorizado.** P-30 quedó completa localmente; el siguiente
> cambio de código requiere una nueva priorización expresa.
>
> **ESTADO VIGENTE 2026-07-29 — `P-25` DONE LOCAL.** Cada aporte sustantivo se consolida, confirma
> internamente y evalúa completo en el mismo turno; el coach responde con retroalimentación de rúbrica y
> una pregunta natural. Solo una ambigüedad real pide aclaración. Rollback:
> `Conversacion:ConfirmacionExplicitaIdeasHabilitada=true`. Backend **583/583** (522 unitarias + 61
> integración), formato y diff verdes. Sin push, despliegue ni cambio remoto. Spec:
> `Iniciativas/P-25_Coaching_Directo_Sin_Confirmacion_Repetitiva.md`.
>
> **Estado previo — `P-24` COMPLETA localmente.** Corrige un bug confirmado de I-19: al recibir
> “vamos a mejorarla” sobre una propuesta pendiente, se debe evaluar la versión consolidada completa y abrir
> coaching, no guardar la frase como corrección ni repetir la confirmación. `MaxRepreguntas` queda alto y es
> un techo técnico, no el cierre normal. La especificación está en
> `Iniciativas/P-24_Evaluacion_Implicita_Al_Solicitar_Mejora.md`. Build Release, **579** pruebas no
> calibración (519 unitarias + 60 de integración), formato y diff verdes. Sin push ni cambio remoto.
>
> **Estado previo 2026-07-28 — `I-20` COMPLETA localmente en código; falta validación operativa.** Backend verde **573** (514 unit +
> 59 integración), portal **26/26**, `dotnet format` limpio. Commits `6a6d0b8` (spec), `242b0f4` (1),
> `4697de3` (2), `afcceaf`+`045b199` (3) y `c813cda` (4). Puerto, política y redactor con guardas que
> reutilizan `FiltroSalidaRubrica` (I-03); **composición por acto** en el orquestador —`Confirmar`,
> `Mejorar`, `Aclarar`, `Reabrir`, `Cerrar`— con el cuerpo insertado por el servidor, **respaldo
> idéntico al texto previo**, cupos y telemetría propia; y **Markdown ejecutivo** con umbral, origen y
> escala (`3,4 de 5 puntos (60 %; global)`) y `pendiente de evaluación` cuando no hay nota.
> Ya desapareció la frase fija “Entendí que propones…”.
> I-19 sigue COMPLETA en local y conserva D5/UAT/costo como pendiente operativo.
> **PRÓXIMO OBJETIVO = validación operativa:** D5 real, UAT y costo con temas distintos. No desplegar ni hacer push.

> **✅ `I-17` (BD de dos niveles: maduras vs. incubación) — COMPLETA local 2026-07-22 (6/6 slices).** Diseño §5/§7 **CONFIRMADO con el usuario** (spec RESUELTO; `SUPUESTOS.md#bd-dos-niveles-madurez-i17`). (1-2) umbral único compartido con precedencia pregunta→campaña→global, sellado determinista de `nivelMadurez`, paráfrasis I-05 solo si `maduro`, telemetría; default global 0.6 y kill-switch de cierre `false` (comportamiento efectivo = como hoy). (3) filtro/DTO en `04 §5.8` + pantalla Resultados (selector/badge/conteos) + controles por campaña (umbral, inactividad, paráfrasis) y por pregunta (umbral). (4) metadato `nivelMadurez` en Markdown `09`. (5) reclasificación por **rechazo explícito** (degradar+cerrar con acuse). (6) cierre por inactividad **sub-hora y por campaña** (barrido per-campaña). Cerró con **420** pruebas; la suite actual tiene **423** verdes. Frontend prettier/tsc limpios (`ng build/test` bloqueado por esbuild/WSL, infra). **Pendiente operativo (no bloquea):** calibrar umbral 0.6 con D5 real y fijar flags globales en el acta del día-D. **Sin commit/push aún.** **⚠️ PRÓXIMO OBJETIVO = rotar al siguiente ítem de §4** (todos con insumo externo: `I-12` BLOCKED por seeds, `I-13` espera decisión GHT 25-jul, `I-14` BLOCKED por catálogo). Spec I-17: `Iniciativas/I-17_BD_Dos_Niveles_Madurez.md`.
>
> **HISTÓRICO — re-priorización reunión GHT 20-jul-2026:** **I-10 (y su dependencia I-09) fueron DIFERIDAS a "Capa 3" post-convención**. Los puntos de diseño de I-17 ya fueron confirmados y la iniciativa quedó completa; el estado vigente es el bloque inicial de este archivo (`I-14` BLOCKED por catálogo GHT).

**Iniciativa P-33 cerrada localmente (3/3).** `DT-QA-02`, `DT-P27-01` y `DT-I20-01` ya
cerraron localmente. El trabajo operativo de D5/validación P-32 continúa sin bloquear la
implementación documentalmente aprobada de P-33.

`I-08 v2` quedó **completa (7/7) y desplegada** el 2026-08-07, validada contra Azure el 2026-08-08
(13 casos PASS). **No cargar datos reales**: falta que GHT entregue el archivo con `Telefono`
diligenciado.

`P-31` quedó **DONE 3/3 y desplegado** el 2026-08-07 (commit `6d02492`); sus flags siguen OFF a la
espera de D5 real, UAT y acta de flags. `DT-P27-01` corte 2 se retoma después de `I-08 v2`.

---

### 1. Contexto del proyecto

**El Tejido** es un sistema que captura ideas por WhatsApp, las evalúa con un LLM usando una rúbrica en Markdown, responde retroalimentación breve (con revisión determinista y salidas naturales), guarda trazabilidad completa, genera artefactos Markdown y los expone en un portal administrativo con login por OTP de WhatsApp. **El MVP está DONE y desplegado en Azure (CD por push a `main`).** El trabajo actual es el **backlog de iniciativas** de la reunión GHT (9-jul-2026), con **Hito inamovible: envío del mensaje de inicio de campaña el 12-ago-2026**.

**La especificación de la iniciativa y el estado del código son tu fuente de verdad.** Antes de escribir una sola línea de código, **lee y analiza en este orden**:

1. `Especificaciones/AVANCES.md` → sección **"Proximo paso"** y **"Tablero por fases"**: el estado real (qué está `DONE`, `WIP`, pendiente). Es el mecanismo de traspaso de contexto; **debe coincidir con el código**.
2. `Especificaciones/Iniciativas/00_Indice_y_Plan_de_Ejecucion.md` → clasificación de iniciativas (con código vs. omitidas), **plan de sprints** (§2), **dependencias duras / ruta crítica** (§3) y **parametrización por campaña** (§4).
3. `Especificaciones/Iniciativas/<ID-INICIATIVA>_*.md` → **la spec de la iniciativa objetivo** (qué pide GHT, estado actual del build, diseño técnico, contratos/config, riesgos, criterios de aceptación y degradación). Es el alcance.
4. `Especificaciones/Reglas_Conversacion_y_Participacion.md` → reglas de flujo vigentes (cold-start §2.1, evaluación + historial §2.2, revisión/invitación/salidas §2.3, cierre §2.4, ventana/expiración §2.5-§2.6, parámetros §3).
5. `Especificaciones/SUPUESTOS.md` → decisiones previas de ambigüedad relacionadas (busca las anclas que cite la iniciativa, p. ej. `#orquestador-conversacional`, `#primer-contacto-pregunta`).
6. Los documentos base **SOLO en las secciones que la iniciativa toque**: contratos `03_Modelo_de_Datos_Cosmos.md` y `04_Contrato_API_REST.md` (**mandan**), y el módulo afectado (`05` conversación, `07` configuración, `08` evaluación LLM, `09` Markdown, `11` portal, `10` seguridad).
7. Como referencia de fondo: `Especificaciones/planes/plan_hito_1.md` (diseño extendido) y `Presentacion/20260711_Plan_Desarrollo_Mitigacion_Riesgos.md` (riesgos RL/RO y decisiones D1–D9).

No rediseñes la arquitectura: está **aprobada**. Respeta lo excluido (`REQ §6.2`) y las iniciativas marcadas **Diferida/Omitida** en el índice.

---

### 2. Reglas de oro (no negociables)

1. **Los contratos mandan y los cambios son ADITIVOS.** El modelo de datos (`03`), el contrato de API (`04`) y el contrato de salida del LLM (`08 §4`) son la verdad de las interfaces. **No cambies un contrato compartido** sin que el cambio sea **aditivo con default seguro** (documento viejo sin el campo = comportamiento actual), y sin actualizar primero el documento de spec en un **commit aparte** registrado en `AVANCES.md`.
2. **No reescribas lo ya hecho.** El MVP está `DONE`. Lee `AVANCES.md` antes de cualquier tarea. Lo marcado **DONE** no se toca salvo bug confirmado; si debes tocarlo, justifícalo en `AVANCES.md` y mantén compatibilidad.
3. **Pasos pequeños y verificables.** Implementa la unidad más pequeña que aporte valor, **pruébala**, ejecútala, y solo entonces avanza. Nada de grandes saltos sin verificación.
4. **Prueba todo lo que desarrolles.** Cada paso incluye sus pruebas (unitarias y, si hay I/O, de integración). Un paso no está hecho si sus pruebas no pasan en verde. Mantén verdes las pruebas existentes; ajústalas solo si el comportamiento cambió a propósito, y explícalo.
5. **No inventes infraestructura.** Los recursos Azure y la app de WhatsApp ya existen (guías en `Guias_Implementacion/`). El código los **consume por configuración**; usa exactamente los nombres definidos. No crees recursos ni asumas nombres distintos.
6. **Cero secretos en el repo.** Ni API keys, ni tokens, ni connection strings con secreto en código, `appsettings` versionado, logs o Markdown. Solo referencias a Key Vault. En local, `dotnet user-secrets`.
7. **Respeta las fronteras de módulo.** Implementa dentro de la carpeta del módulo; consume otros módulos por su interfaz pública. No edites código ajeno sin necesidad.
8. **Trazabilidad.** Cada pieza referencia el `REQ §` / `ARQ §` y el **ID de la iniciativa** que cumple (en comentarios y en el mensaje de commit).
9. **Ante ambigüedad**, aplica `01 §9`: elige la opción más simple compatible con el Hito que **no cierre** fronteras futuras, y **registra el supuesto** en `Especificaciones/SUPUESTOS.md`. Si la iniciativa plantea una **decisión de diseño real** (p. ej. opción A/B/C que cambia el alcance o toca contratos), **confírmala con el usuario ANTES de implementar**; no la tomes en silencio.
10. **Regla transversal (D1–D9): nada nuevo se considera hecho sin** (a) **flag apagado por defecto** cuando aplique, (b) forma de observarlo (métrica/log), (c) **banco de calibración o suite de regresión en verde**, y (d) camino de rollback documentado. **El LLM propone, el sistema dispone** (R-01): toda salida del modelo es dato no confiable; las salvaguardas son deterministas y server-side.
11. **Definition of Done** (`01 §8`) es vinculante para cerrar cualquier tarea.

---

### 3. Bucle de trabajo (repítelo en cada paso)

```
1. LEER     → Abre AVANCES.md + la spec de la iniciativa. Identifica el sub-paso concreto.
2. PLANEAR  → Declara: desde qué ROL decides, qué REQ §/ARQ §/ID-iniciativa cubres, qué
              módulo/archivos tocas, qué contratos consumes, qué pruebas escribirás y cómo
              verificarás. (3–6 líneas, sin sobre-extenderte.)
3. IMPLEMENTAR → Escribe el código mínimo del paso. Respeta convenciones (01 §4) y contratos.
4. PROBAR   → Escribe/actualiza las pruebas del paso. Ejecuta build + test + lint:
                 dotnet build -c Release -warnaserror
                 dotnet test  -c Release
                 dotnet format --verify-no-changes
                 (frontend, si aplica) npm run lint && npm run test -- --watch=false && npm run build
5. VERIFICAR→ Todo en verde. Si falla, corrige antes de continuar. No avances con rojo.
6. REGISTRAR→ Actualiza AVANCES.md (§5): marca DONE, anota decisiones, archivos tocados, cómo
              probar, y define el SIGUIENTE "Próximo paso". **Actualiza SIEMPRE este TODO.md**
              antes de cerrar: cabecera (`ID-INICIATIVA` y agente), fila de §4 y §8 deben describir
              exactamente el siguiente trabajo ejecutable o el bloqueo actual. Registra supuestos
              en SUPUESTOS.md y actualiza Reglas_Conversacion_y_Participacion.md si cambió una
              regla de flujo.
7. COMMIT   → Conventional Commits, pequeño y atómico, con REQ §/ARQ §/ID-iniciativa cubiertos.
              Ej: "feat(evaluacion): follow-up sobre eje debil sin revelar rubrica (I-03, REQ §21)".
              Push a main SOLO cuando el usuario lo pida (un push despliega a producción).
8. SIGUIENTE→ Vuelve al paso 1.
```

**Regla de continuidad entre sistemas:** después de cada paso, el repositorio debe quedar en estado **compilable y verde**, y `AVANCES.md` **y este `TODO.md`** deben reflejar la realidad exacta. No cierres una sesión, iniciativa o bloqueo sin actualizar ambos: otro agente debe poder abrir `TODO.md`, identificar el objetivo, el agente asignado y el primer paso concreto, y continuar sin hablar contigo.

---

### 4. Plan de ejecución de iniciativas (orden macro)

El orden y las ventanas salen de `Iniciativas/00_Indice_y_Plan_de_Ejecucion.md §2` (Cronograma + decisiones D1–D9) y de las **dependencias duras** de `§3`. No arranques una iniciativa cuya dependencia no esté lista.

**Orden canónico de implementación + rotación de agentes (decisión del usuario 2026-07-14).** Se
implementa **un ítem a la vez**, **en este orden**, alternando agente: **Codex y Claude se turnan**
(empezando por Codex en `D5`). **Siempre que haya avance, cierre o bloqueo**, el agente actualiza este
TODO: marca el estado real de su fila y deja en la cabecera y `§8` el trabajo que puede ejecutar el
siguiente agente. Al terminar su ítem, marca su fila `DONE`, rota al siguiente ítem pendiente y su
agente, y hace el handoff por `AVANCES.md`. No arranques un ítem cuya dependencia dura (§3) no esté lista.

| # | Ítem | Ventana | Agente | Estado |
|---|---|---|---|---|
| 1 | `P-03` reinicio de datos | Sprint 1a | Claude | **DONE** (2026-07-13/14; backend verde, committeado; portal verificado Node 24) |
| 2 | `P-10` cupos + rate por número + costo LLM | Sprint 1a | Claude | **DONE** (2026-07-14; backend verde 294, committeado) |
| 3 | **`D5` banco de calibración** | Sprint 1a | **Codex** | **DONE** (2026-07-14 por Claude Opus 4.8 por decisión del usuario; backend/tooling verde 315; librería + golden set 24 + runner opt-in fuera de CI; baseline pendiente de corrido real) |
| 4 | **`I-16` fix de calificación en Markdown** | Sprint 1a | **Claude** | **DONE** (2026-07-15; backend verde, regresión determinística) |
| 5 | **`I-08` carga masiva (backend)** | Sprint 1a | **Codex** | **DONE (2026-07-15) — SUPERADO por `I-08 v2`** (ítem 22a). Backend verde 335 con plantilla `Nombre\|WhatsApp\|Area\|Empresa\|Tags`, que **ya no es la oficial**. El puerto `ILectorArchivoParticipantes` y el esqueleto de reporte se reutilizan; el lector CSV y `ServicioCargaMasiva` se reescriben. |
| 6 | **`I-06` multi-idea (diseño)** | Sprint 1a | **Claude** | **DONE** (2026-07-15; diseño documental, contratos/rollback/cupos/observabilidad definidos) |
| 7 | **`I-09` tejido colectivo (diseño)** | Sprint 1a | **Codex** | **DONE** (2026-07-15; diseño documental, contratos/puerto/inyección/rollback definidos; `03 §3.3` field `tejidoColectivo` aditivo; Opción A léxica, B embeddings diferida) |
| 8 | **`I-01` activar umbral en staging** | Sprint 1a | **Claude** | **DONE parcial / BLOCKED** (2026-07-15; runbook + observabilidad `LogSeguridad(cierreUmbralAnticipado)` + regresión, verde 335; cierre real bloqueado en baseline D5 real + freeze I-11 + flip humano; `SUPUESTOS.md#activacion-umbral-i01`) |
| 9 | **`I-06` multi-idea (implementación)** | Sprint 1b | **Codex** | **DONE local** (código, pruebas y documentación; flags apagados hasta D5/UAT/costo en staging) |
| 10 | **`I-09` tejido colectivo (core)** | Sprint 1b | **Claude** | **DONE local** (2026-07-17; Opción A léxica, inyección delimitada/sanitizada, degradación autocontenida, flags apagados, observabilidad; verde 367; costo/latencia en staging pendiente) |
| 11 | `I-05` parafraseo | Sprint 1b | Codex | **DONE local 2026-07-20** (decisión de usuario: flag por campaña false + kill-switch; salida/persistencia opcional, truncado determinista, regresión verde; baseline D5 real pendiente) |
| 12 | `I-08` carga masiva (UI) | Sprint 1b | Claude | **DONE (2026-07-20) — AMPLIACIÓN PENDIENTE en `I-08 v2`** (ítem 22a). El panel en `/usuarios` sigue sirviendo de base; falta aceptar `.xlsx`, el selector de `modo`, la **resolución de conflictos de titular** por fila, la descarga de la plantilla vacía y el histórico del número en la ficha de usuario. |
| 13 | `I-03` follow-ups eje débil | Sprint 1b | Codex | **DONE local** (2026-07-21, Claude Fable 5; pista de foco + `CalculadorEjeDebil` + `FiltroSalidaRubrica`, salvaguarda siempre-on sin flag; backend verde 394; sin cambio de contratos; D5 real contra staging pendiente) |
| 14 | `P-13` umbral de cierre por campaña | Sprint 1b–2 | Claude | **DONE local 2026-07-21** — override nullable por campaña, default numérico heredable y kill-switch booleano global; API/Cosmos/portal/telemetría, backend verde 400; D5 real + calibración I-01 en staging pendientes |
| 15 | `I-10` flag base previa/blanco | ~~Sprint 2~~ | Codex | **⛔ DIFERIDA (Capa 3, reunión 20-jul)** — es la UI del tejido I-09, también diferido; **no implementar** para el Hito. El campo ya existe y queda OFF. |
| **15a** | **`P-14` lectura de rúbricas y prompts (solo lectura, portal)** | **Sprint 1b** | Codex | **DONE local 2026-07-22.** Acción "Ver" por fila, panel seguro de solo lectura, admin/visor y mutaciones preservadas; sin cambio de contratos ni backend. Frontend lint/test 11/11/build verdes. |
| **15b** | **`I-17` BD de dos niveles (maduro/incubación)** | **Sprint 1b–2** | **Claude** | **DONE local 2026-07-22 (6/6 slices).** Clasificación+resolución por pregunta+paráfrasis-solo-si-maduro+telemetría; filtro/DTO + pantalla Resultados + controles portal; metadato en Markdown; reclasificación por rechazo explícito (cierre+acuse); cierre por inactividad sub-hora y por campaña. **420 pruebas backend verdes**, frontend prettier/tsc limpios. Pendiente operativo: D5 + acta de flags día-D. Sin commit/push aún. |
| **15c** | **`P-15` refactor del orquestador conversacional** | **Remediación auditoría** | **Claude** | **DONE local 2026-07-24 (Claude Opus 4.8) — 3/3 cortes.** `CAL-001`. Corte 1 (`56bbef2`): `PoliticaLimitesConversacion` (política sin E/S). Corte 2 (`3e5cc9a`): `ResolvedorTransicionConversacion` (transición). Corte 3 (`4a8e9eb`): `ProcesadorResultadoEvaluacion` (efectos posteriores: persistencia/Markdown/telemetría; compuesto `PersistirRespuestaEvaluadaAsync` unifica normal+segmentado). Fachada `IOrquestadorConversacion` intacta; sin cambio de flujo, flags, mensajes, orden, persistencia ni contratos. Backend verde 468 (416+52; +45), format limpio; sin push. |
| **15d** | **`P-16` descomponer página de campañas** | **Remediación auditoría** | **Codex** | **DONE local 2026-07-24.** `CAL-002`: `CampaniasPage` queda como contenedor de carga/selección/refrescos/permisos; paneles standalone tipados separan listado, creación, configuración, mensajes, preguntas, participantes/vista previa/reinicio y detalle. Ruta, APIs, DTO, permisos y acciones se preservan. Build Angular y 13 pruebas verdes con Node temporal 24.15.0 + ejecutables locales. Prerequisito de P-20 satisfecho. |
| **15e** | **`P-17` errores API uniformes** | **Remediación auditoría** | **Claude** | **DONE local 2026-07-24 (Claude Opus 4.8).** `API-001`: `ResultadoError : IResult` uniforma el cuerpo `ErrorRespuesta` (04 §3) + `correlationId` en `EndpointsAdminEnvios`/jobs, `EndpointsPreparacion`, `FiltroClaveDiagnostico` y `EndpointsWebhook` (403→FORBIDDEN, 401→UNAUTHENTICATED, 404→NOT_FOUND). Sin cambio de códigos HTTP, auth ni respuestas exitosas; sin filtrar secreto/firma/token. Backend verde 469 (+1 int), format limpio; commit `5babc9b`, sin push. Spec `P-17_Errores_API_Uniformes.md`. |
| **15f** | **`P-18` nombre accesible de controles** | **Remediación auditoría** | **Codex** | **DONE local 2026-07-25.** `UXA11Y-001`: selección de envíos, tags y CSV con etiqueta o nombre contextual; sin cambio de datos, permisos ni acciones. Formato, build Angular y 15 pruebas frontend verdes con Node 24.15.0. Spec `P-18_Controles_Con_Nombre_Accesible.md`. |
| **15g** | **`P-19` anuncios de estados dinámicos** | **Remediación auditoría** | **Codex** | **DONE local 2026-07-25.** `UXA11Y-002`: regiones vivas comunes para error/éxito/información, toasts sin duplicación y asociación de error a campo activo; sin cambio de datos, permisos ni acciones. Formato, build Angular y 18 pruebas frontend verdes con Node 24.15.0. Spec `P-19_Estados_Dinamicos_Accesibles.md`. |
| **15h** | **`P-20` pestañas accesibles de campañas** | **Remediación auditoría** | **Claude** | **DONE local 2026-07-25.** `UXA11Y-003`: patrón ARIA completo, IDs únicos, foco móvil y teclado en el detalle de campaña; una sola fuente de verdad para selección y contenido. Sin cambio de rutas, APIs, DTO, permisos ni acciones. Formato, build Angular y 19 pruebas frontend verdes con Node 24.15.0. Spec `P-20_Pestanas_Accesibles_Campanias.md`. |
| 16 | `I-12` seed thoughts | Sprint 2 | Claude | **BLOCKED — insumo vencido** (seeds de Felipe no recibidos al 2026-07-20; **escalar**); al recibirlos, implementar |
| 17 | `I-13` decisión agnóstica-vs-tailored | Sprint 2 | Codex | TODO (decisión GHT 25-jul) |
| 18 | `I-14` tags | Sprint 2 | Claude | **BLOCKED — falta catálogo consolidado de GHT** (nombre, tipo, descripción opcional y estado); no inventar ni hardcodear tags. |
| 19 | `P-07` consentimiento de datos | ~~Sprint 2~~ | Codex | **⛔ DIFERIDA (reunión 20-jul)** — consentimiento innecesario en herramienta interna (IP de GHT); no implementar para el Hito |
| 20 | `P-10` costo LLM + rate por número | Sprint 2 | Claude | **YA HECHO** en el ítem 2 (2026-07-14); al llegar aquí, **verificar y saltar** |
| 21 | `P-09` monitoreo día-D | Pruebas 4–8 ago | Codex | **Panel DIFERIDO (reunión 20-jul)** — basta health-check; se conservan `/health(/ready)`, logs de entrega, **acta de flags + runbook** (esos sí son entregables del go-live) |
| **22a** | **`I-08 v2` plantilla oficial + maestro de usuarios** | **DONE local — falta desplegar** | **Claude** | **COMPLETA local 7/7 el 2026-08-07 — commits `63fa3e7`, `d07b9f0`, `e5e4b37`, `982c7b7`; base recreada y sembrada por el usuario. FALTA SOLO DESPLEGAR** — pasos 1 y 2 de `§8`: `Usuario` con `codigoUsuario`/`usuarioWhatsapp`/`empresaId`/`sede`/`cargo`/`email`/`antiguedadAnios`/`idioma`, `area`-`empresa` opcionales, `claveUnicidad` derivada solo en el mapeo, filtro `estado = activo` en `ObtenerUsuarioPorNumeroAsync` + `ListarUsuariosPorNumeroAsync`, contador `Secuencia` con ETag y reserva por bloque, DTO `04 §5.1` aditivo. Pasos 4 y 5 (+ endpoint parcial): `PlantillaParticipantes` con las 9 columnas, lectores `.xlsx` (ClosedXML, primario) y `.csv` que producen filas idénticas, y `ServicioCargaMasiva` en dos pasadas con modos, conflicto de titular ≥ 0,85, reasignación con compensación, tag de empresa derivada y auditoría sin PII. Backend **799** (719+80), build/format/diff verdes; corte 1 desplegado, corte 2 sin push. **▶ Siguiente: paso 3 — recrear `users` con unique key `/claveUnicidad`, sembrar el admin y verificar el `409`. Irreversible; sin él la reasignación no funciona contra Azure**; tras recrear, repetir la prueba de humo de P-31. Falta además cerrar el paso 6 (DTOs de alta/edición, `reasignar-numero`, descarga de plantilla) y el paso 7 (portal). |
| 22 | `I-08` carga real de la lista de GHT | Freeze 11 ago | Claude | **TODO — BLOCKED por 22a y por GHT.** Las variables demográficas de Munir **ya llegaron**: son las columnas de la plantilla oficial (insumo cerrado). Falta que GHT entregue el archivo con **`Telefono` diligenciado** (en la V1 esa columna viene vacía en las 129 filas, igual que `Empresa` e `Idioma`). **No cargar nada hasta entonces.** |
| 23 | **cierre por inactividad ~5 min** (granularidad sub-hora) | Sprint 2 | Claude | **DONE local dentro de I-17 (2026-07-22).** Cierre sub-hora, parametrizable por campaña, con interruptor global apagado por defecto; backend verde 420. |
| 24 | **`P-21` multi-número de WhatsApp** | A coordinar (fuera de ruta crítica) | Codex | **DONE local 2026-07-25.** Misma WABA/App; `metadata.phone_number_id` llega al orquestador y todas las respuestas salen por ese número. `IWhatsAppGateway` acepta emisor opcional; `configConversacional.numeroWhatsAppSaliente` guarda un alias por campaña y el fallback legacy/predeterminado conserva el comportamiento actual. Sin secretos nuevos; backend 473/473 verde. |
| 25 | **`P-22` UX de Campañas** | A coordinar (mejoras de portal) | Codex | **DONE local 2026-07-25.** Creación bajo demanda, pasos numerados con completitud y nombre accesible, enlace contextual a Envíos con id real, fieldsets con ayuda y estados vacíos. Preserva P-16/P-18/P-19/P-20 y no cambia contratos. Prettier, 21/21 pruebas Angular y build de producción verdes con Node 24.15.0. |
| 26 | **`P-23` UX de Resultados** | A coordinar (mejoras de portal) | Codex | **DONE local 2026-07-25.** Precarga de campaña en memoria, patrón maestro-detalle (respuesta → evaluación + Markdown), leyenda/conteos, extractos, estados guiados y actividad secundaria. Preserva I-17/P-18/P-19; sin contratos, rutas ni permisos nuevos. Prettier, 24/24 pruebas Angular y build de producción verdes con Node 24.15.0. |
| 27 | **`I-18` coaching secuencial por idea** | Sprint 2 | **Codex** | **DONE local 2026-07-25.** Cola y contador por idea, revisiones enlazadas, prompt socrático, timeout/fallback acotados, DTOs/Markdown/telemetría aditivos y controles accesibles. Backend 484/484 y portal 24/24, formato y builds verdes. Gates por campaña OFF; D5/UAT/costo antes de activar. |
| 28 | **`I-19` consolidación progresiva de ideas** | **DONE local** (código) | **Codex / Claude** | **Pasos 1–10 locales (2026-07-28, Claude Opus 5; commits `748870f`, `4e31f94`, `62240b9`, `401d9dd`, `7ef021c`, `a148ca5`, `61258e4`, `aceb9f0`, `1792c4f`, `0d52e6c`; backend verde 529, portal 26/26):** idea/versiones, consolidador, ciclo canónico en hilo simple y cola multi-idea, complemento + idea nueva, reapertura, Markdown/API/Resultados por idea, observabilidad y cupos, y QA final con E2E simulada. **Falta solo lo operativo:** D5 real, UAT, costo y acta de flags. Paso 8 (seeds I-12) BLOCKED; reapertura entre preguntas (§4.7) diferida con condición. |
| 29 | **`I-20` redacción conversacional fluida y Markdown ejecutivo** | **DONE local** | **Codex / Claude** | **Cortes 1-5 DONE local 2026-07-28:** redactor por acto, guardas, composición servidor, cupos/telemetría y Markdown con umbral/origen/escala; E2E con redactor inyectado. Pendiente operativo: D5/UAT/costo. |
| 30 | **`P-24` evaluación implícita al solicitar mejora** | **DONE local** | **Codex** | **Corregido 2026-07-29:** “Vamos a mejorarla” confirma implícitamente la versión propuesta, la evalúa completa y abre coaching bajo umbral en hilo simple o cola multi-idea. No crea aporte/version nueva, no reduce `MaxRepreguntas`, ni cambia contratos/remoto. Backend 579/579 verde. |
| 31 | **`P-25` coaching directo sin confirmación repetitiva** | **DONE local** | **Codex** | Cada aporte sustantivo confirma automáticamente su versión consolidada y la evalúa completa en el mismo turno; respuesta natural con una sola pregunta de coaching. Rollback global disponible; backend 583/583 verde. |
| 32 | **`P-26` participación continua y selección de campaña/pregunta** | **Inmediata** | **Claude** | **DONE local 2026-07-31 (6/6 cortes; backend 654/654, portal 30/30; flag OFF por defecto). Pendiente operativo: D5/UAT/costo.** Entregó dominio/contratos, resolución multi-campaña y pregunta, aporte preservado, afinidad, ciclos nuevos deterministas, reapertura reciente que conserva `ideaId`, cupos, portal, observabilidad, E2E simulada, QA y cierre. P-28/P-29/P-30 son extensiones acotadas, no cortes faltantes. Default `false`; solo campañas activas. |
| 33 | **`P-27` clasificación flexible de intenciones de control** | **DONE local 2026-08-04 (5/5)** | Codex | Alias, clasificador y política server-side, menú persistido, rollback, portal y contabilidad durable de llamadas/tokens P-27. Backend 698/698, build/format verdes; flags global/campaña OFF y activación D5/UAT/costo pendiente. |
| 34 | **`P-28` despertar proactivo del coach** | **DONE local 2026-08-04 (3/3)** | Codex | Saludo breve con flag global OFF, vocabulario determinista, redacción/fallback, selección P-26 sin convertir saludo en aporte, telemetría sin texto, Cosmos/E2E/QAS. Siguiente: P-29 corte 1. |
| 35 | **`P-29` cierre conversacional por tiempo** | **DONE local 2026-08-04 (2/2)** | **Claude** | Kill-switch `CierrePorTiempoHabilitado` (OFF), `promptRefs.cierre` con acto `Pausar`, aviso único redactado por I-20 con respaldo determinista, telemetría `cierrePorInactividad` sin texto y E2E simulada. Reutiliza el cierre por inactividad de I-17/I-19 (sin temporizador, umbral, estado ni motivo nuevos) y lo omite fuera de la ventana de 24 h o con campaña no activa. Backend 723/723, build/format/diff verdes. |
| 36 | **`P-30` retomar ideas del pasado** | **DONE local 2026-08-04 (3/3)** | **Codex** | Selector histórico determinista por participante, campaña y pregunta, sin filtro por estado/ciclo; selección por número o título/resumen exacto, misma idea y conversación reabiertas, curaduría suspendida, kill-switch OFF, Cosmos, telemetría sin texto, E2E y QAS. Backend 729/729, build/format/diff verdes. |
| **37** | **`P-31` resumen de la consolidación al alcanzar un umbral propio** | 2026-08-06/07 | Codex/Claude | **DONE 3/3 y DESPLEGADO (2026-08-07).** Commits `6ba6ce0` · `32794fb` · `6d02492`. Build Release, **664 unitarias + 77 integración**, formato y `git diff --check` verdes. E2E simulada: inicio → aporte sobre umbral → resumen → mejora sin repetirlo. Guía: `QAS/14_P31_Resumen_Consolidacion_Como_Probar.md`. **Flags OFF**; encenderlos exige D5 real + UAT + acta de flags, y elegir el umbral. La consulta bajo demanda se resolvió en P-33. Detalle original ↓ |
| ~~37 (histórico)~~ | ~~especificación original~~ | — | — | REQ-052 (GHT, 2026-08-06). Umbral de resumen propio `Conversacion:UmbralResumenConsolidacion` con override por campaña y pregunta, **independiente** del `umbralCierreAnticipado` de I-17/P-13: al cruzarlo con la idea **abierta**, el turno de coaching lleva el texto de la versión vigente I-19 **insertado server-side** más una pregunta de continuidad. Sin estado conversacional nuevo (queda en `esperandoRepregunta`), sin tocar el sellado de madurez, sin consumir `repreguntasUsadas`, idempotente por idea y **sin depender de los flags de P-27**. Kill-switch OFF + opt-out por campaña. P-33 resuelve aparte la consulta reactiva. |
| **38** | **`P-33` consulta y cierre visible de la idea** | **DONE local 3/3** | **Codex** | Consulta pura activa→última sin menú, versión I-19 exacta por demanda/cierre, afinidad y reapertura de la misma cerrada ante corrección; gate OFF, opt-outs, `es/en`, seguridad, telemetría y QAS. Build `-warnaserror`: 789 unitarias + 87 integración. **Siguiente: D5/UAT y acta de flags; sin activar remotamente.** |
| **DT-I20-01** | **Variación y no duplicación en la redacción conversacional** | **DONE local 5/5 — 2026-08-13** | **Claude** | I-20: `Queda claro que...` sigue permitida pero deja de ser la apertura obligatoria; `FiltroDuplicacionTurno` (puro) omite el puente equivalente, prefijo o superconjunto del cuerpo validado, `ExigePregunta` decide si una pregunta duplicada se omite o cae al respaldo, y la auditoría añade `ajuste:<motivo>` sin texto. Aplica a los mensajes nuevos de todas las campañas; no toca historial, contratos, portal, flags, migraciones ni configuración por campaña. Backend 785 unitarias (766 sin Calibración) + 88 integración, build/format/diff verdes. **Pendiente: D5 con ejemplos reales antes de desplegar.** Spec `Iniciativas/DT-I20-01_*`; QAS `QAS/19_*`. |
| **DT-P32-02** | **Semillas seguras, edición masiva JSON y readiness** | **COMPLETA local 3/3 — 2026-08-14** | **Claude** | Corte 1: base curada `es/en` independiente de App Settings, fotografía legacy separada y sin truncar, límites operativos con techo compilado (`MaxFrasesPorGrupo` 100/500, `MaxBytesImportacionJson` 256 KiB/1 MiB, con clamp), `Prevalidar(...)` puro compartido y rutas `/semillas/{idioma}/base` y `/legacy/{preview,exportar}` + `POST /legacy`. Corte 2: descarga editable canónica `*-editable.json`, `POST /importar/prevalidar` sin escritura, `/importar` sobre el mismo validador con `Content-Type`, tamaño verificado **antes de deserializar**, profundidad acotada, metadatos ignorados y `v+1` siempre borrador, selección por `?idioma=`/`?familiaId=`, `GET /readiness` con gate real y precondición `catalogosTextos.{idioma}: activo_requerido`. Corte 3: portal completo (semilla base vs. configuración anterior, descargar → editar → revisar → confirmar, readiness visible, comparación con la activa, reintento del mismo archivo, sin activación automática). Backend 817 unitarias + 103 integración; portal 57/57, `ng build` y Prettier verdes; contrato `04` en commit aparte (`77377ec`). Gate OFF, sin despliegue ni cambio remoto. **Siguiente (operativo): `QAS/22` y luego `QAS/17` en ambiente aislado autorizado; solo con green se retoma `DT-I20-02`.** Spec `Iniciativas/DT-P32-02_*`; plan `planes/DT-P32-02_*`; QAS `QAS/22_*`; supuesto `SUPUESTOS.md#semillas-y-limites-catalogo-dt-p32-02`. |
| **DT-P32-03** | **Cierre localizado único y readiness Meta** | **DESPLEGADA 2/2 — `a9f4a6f`, 2026-08-15** | **Claude** | Corte 1: `IResolutorMensajeCierreCampania` resuelve el cierre para las seis rutas del orquestador; gate OFF conserva el legacy exacto, gate ON exige `localizaciones.{idioma}.mensajeCierre` y una localización ausente cierra con el fallo tipificado `LOCALIZACION_CAMPANIA_INCOMPLETA` sin fallback entre idiomas; auditoría sin texto y prueba arquitectónica contra nuevas lecturas directas. Corte 2: `ValidadorMapeosPlantillaMeta` enumera los pares `plantillaRef + idioma` de las campañas `activa|borrador` con mensajes iniciales activos, deduplicados por alias + idioma y con sus requirentes; "configurado" delega en `OpcionesPlantillaEnvioInicial.TryResolver` y se reportan `plantilla_ref_faltante`, `nombre_faltante`, `idioma_meta_faltante`, `componente_vacio` y `componente_duplicado` (`componentes: []` es legítimo). `GET /readiness` agrega `listoParaGateOn` y `mapeosMeta[]` sin cambiar `idiomas[].listo`; el portal separa catálogos de plantillas y advierte que no certifica la aprobación en Meta. Sin Graph API, secretos ni App Settings escritos. Backend 854 + 105, portal 60, build/format/`ng build`/diff verdes. Desplegada el 2026-08-15 (CI y Deploy en success); el gate `Conversacion:CatalogoTextosHabilitado` sigue sin override en Azure y por tanto OFF. **Siguiente (operativo): `QAS/23` 1-7 y luego `QAS/17`.** |
| **DT-P32-04** | **Núcleo transversal multidioma** | **ESPECIFICADA 0/3 — BACKLOG POST-GREEN** | **Codex** | Idioma central, `ContenidoCampaniaEfectivo`, resolutores especializados y readiness compuesto. No cambia fuentes de contenido, DTO ni Cosmos; evita una clase dios. No bloquea DT-I20-02. |
| **DT-I20-02** | **Contrato visible en texto plano y gobierno seguro de prompts** | **ESPECIFICADA 0/3 — EN ESPERA DE DT-P32-02 GREEN** | **Codex** | Bug real: el prompt runtime pidió secciones Markdown/estado interno y el cuerpo llegó así a WhatsApp. Corrección por campo en salidas LLM, sin sanitización global ni cambio de puntajes, versión I-19, umbrales, estados, cierres, P-27/P-32/P-33 o historial. Incluye selección runtime activa+aprobada y migración gradual mediante familia nueva. Sin código ni configuración remota. Se retoma después del despliegue autorizado y la nueva corrida P-32 green. Spec `Iniciativas/DT-I20-02_*`; QAS `QAS/21_*`; runbook `planes/DT-I20-02_*`. |
| DT-P27-01 | **Configuración versionada de expresiones determinísticas P-27** | **DONE local — 2/2 (2026-08-08)** | Codex | Validación de vacío/duplicado/límite tras normalizar, descarte completo con fallback y registro seguro; historial append-only de versión aplicada/default/descartada y rollback desde el origen de configuración o al default. Backend 821/821 (736+85) y build verdes. Sin edición por campaña, alias nuevos, activación P-27 ni cambio remoto. Spec: `Iniciativas/DT-P27-01_Config_Versionada_Frases_Finalizacion.md`. |
| DT-QA-01 | **Inyección de webhook simulado de diagnóstico** | **DONE local 2026-08-05** | Codex | Endpoint con `X-Diag-Key` y gating de simulación que encola el payload mínimo ya autenticado; idempotencia por id explícito o derivado, auditoría sin PII y webhook real sin cambios. Integración focalizada 7/7 verde. Pendiente solo desplegar para E2E Azure. |
| **DT-QA-02** | **`GET /api/admin/evaluaciones` — listado y detección de huérfanas** | **DONE local 2026-08-08** | **Codex** | Endpoint de solo lectura para `admin`/`visor`, con `campaniaId` obligatorio, filtros, paginación y resumen. `ListarEvaluacionesAsync` es obligatorio y está implementado en Cosmos/memoria con `fecha DESC`; el diagnóstico derivado distingue `enlazada`/`huerfana`/`superada`/`sin_version_idea` sin texto libre. Una evaluación superada por otra más reciente no se cuenta como huérfana (I-16). No repara documentos, no toca `03`, flags, configuración remota, despliegue ni portal. Backend: build, 814 pruebas no-Calibracion, formato y diff verdes. Spec: `Iniciativas/DT-QA-02_Listado_Evaluaciones_Y_Huerfanas.md`; `04 §5.8` actualizado. **DT-P32-02 quedó COMPLETA local 3/3 el 2026-08-14; sigue la corrida autorizada de `QAS/22` y `QAS/17`.** |
| DT-P27-02 | **Calibración del clasificador P-27 (cierre sobre la última idea)** | **BACKLOG post-convención** | — | Borde detectado en la E2E conversacional desplegada (E14, 2026-08-06): una variante libre no-alias sobre la **última idea de la cola** (`QUEDAN_UNIDADES_PENDIENTES=no`) se clasifica `aportar` en vez de finalizar. Degrada seguro (no corta la idea) y los alias deterministas sí funcionan → severidad baja, no bloqueante. Ajuste **solo del prompt de sistema** de `ClasificadorIntencionControl`; **no desplegar sin pasar D5** (regresión clave: no aumentar cierres falsos de ideas con contenido). Spec: `Iniciativas/DT-P27-02_Calibracion_Clasificador_Cierre_Ultima_Idea.md`. |

- **HITO (12-ago):** envío escalonado por lotes con monitoreo; ante síntoma se apaga el flag según runbook, nunca hotfix en caliente.
- **Post (rama de deseables + DIFERIDAS a Capa 3 por la reunión 20-jul):** `P-04`, `P-11`, `P-08`, `P-06`, `P-05`, `I-15`, `P-12` **+ `I-09`/`I-10` (tejido colectivo), `P-07` (consentimiento) y el panel de `P-09`**. (`P-13` salió de deseables y entró al MVP como ítem 14.)

**Dependencias duras (actualizada 2026-08-04):** `I-06 + I-03 + I-17 + P-15` → `I-18` **✓**;
`I-18 + I-05 + P-23` → `I-19` **DONE local** → `I-20/P-24/P-25` **DONE local** → `P-26`
**DONE local (6/6)** → `P-27` **DONE local (5/5)** → `P-28` **DONE local (3/3)** → `P-29`
**DONE local (2/2)** → `P-30` **DONE local (3/3)**. P-28 complementa saludos/inicios no sustantivos, P-29 el mensaje humano de pausa y P-30 el selector
histórico; ninguna es requisito técnico para P-26. I-12 sigue bloqueada por seeds, pero no bloquea
P-27: campo vacío degrada limpio. D5/UAT/costo arbitran el despliegue y la activación del clasificador,
no el inicio del código.

> **Excepción I-19 confirmada por el usuario:** la consolidación no tiene opt-in por campaña y se
> activa para todas. Solo conserva un kill-switch global de emergencia, default `true`.

---

### 5. Documento de avances — `Especificaciones/AVANCES.md` (mantenlo SIEMPRE actualizado)

Es el **mecanismo de traspaso de contexto** entre sistemas. Ya existe con su estructura (Estado global, Próximo paso, Tablero por fases, Log cronológico). Actualízalo en el paso 6 de cada bucle. Reglas:

- Es la **única fuente** del estado real del desarrollo. Debe coincidir con el código.
- No borres historial: marca estados (`DONE`, `WIP`, `TODO`, `BLOCKED`) y añade entradas al log cronológico.
- Al cerrar una iniciativa: agrega su **fila al Tablero** con el ID (p. ej. `| 11 | I-03 follow-ups eje débil | DONE | pendiente | verde | ... |`), resume el cambio en "Última actualización", cierra/avanza el "Próximo paso", y enlaza el supuesto nuevo en `SUPUESTOS.md`.
- Sé conciso pero suficiente para que un agente nuevo reanude sin preguntar.
- **Incluye SIEMPRE, al cerrar una iniciativa, un "Cómo probarlo" en lenguaje humano para un lector no técnico** (ver §8 paso 5b): resumido, sin jerga, describiendo qué abrir/hacer/ver. Este texto es parte del entregable, no un extra opcional.

También mantén `Especificaciones/SUPUESTOS.md` (referenciado en `01 §9`) para toda decisión de ambigüedad, y `Especificaciones/Reglas_Conversacion_y_Participacion.md` cuando cambie una regla de flujo visible al participante.

---

### 6. Estándares de calidad (resumen operativo; detalle en `01 §4` y `08/10`)

- **.NET:** Nullable on, warnings-as-errors, `dotnet format` limpio, async + CancellationToken, DI en el composition root, sin lógica en controladores, excepciones de dominio tipadas traducidas al modelo de error de `04 §3`.
- **Angular 22:** standalone + signals + OnPush, TypeScript estricto, sin `any` injustificado, acceso a API por servicios tipados, marca GHT por tokens. `wwwroot` está gitignoreado (lo reconstruye el CD). 💡 **Verificado 2026-08-14: `npx ng test --watch=false` y `npx ng build` corren con el Node del sistema (24.18.0) y `@esbuild/linux-x64` ya instalado**; si el runner de Vitest falla al levantar un worker (`Timeout waiting for worker`), es el sandbox del agente, no el código: reintentar sin sandbox.
- **Pruebas:** xUnit + FluentAssertions + NSubstitute (backend); runner del CLI (frontend). Cubre caminos felices y de error/fallback. I/O externo (Cosmos/WhatsApp/LLM) mockeado en CI; integración contra emulador donde aplique. Para iniciativas con LLM: **banco de calibración / golden set** como árbitro de no-regresión (D5).
- **Seguridad:** secretos solo en Key Vault; OTP solo hasheado; auth neutral; respuesta del usuario al LLM como **dato**; sin secretos/PII en logs ni Markdown; salvaguardas deterministas server-side ante fugas del modelo.
- **Observabilidad:** logs estructurados + `correlationId` propagado en la cadena conversacional; anomalías del LLM en `LogSeguridad`.

---

### 7. Qué NO hacer

- No implementar iniciativas marcadas **Omitidas** (`I-01/I-02/I-04/I-11/I-13/I-14/I-15`, `P-01/P-02/P-12`) como código: son calibración, contenido, datos, decisión o gestión Meta (ver índice §1.2). No implementar **Diferidas** (`P-04/P-05/P-06/P-08/P-11`) antes del Hito.
- No implementar nada de `REQ §6.2` (capa vectorial salvo lo que una iniciativa habilite explícitamente bajo flag, dashboards avanzados, Entra ID, Git de Markdown, exportaciones, gamificación, etc.).
- No microservicios, ni colas dedicadas (salvo decisión D7 tras la prueba de carga), ni Bicep.
- No hardcodear preguntas, mensajes, tags, rúbricas ni prompts: todo es dato configurable.
- No cambiar contratos sin que sea aditivo, con default seguro y su commit de spec aparte.
- No encender un feature por defecto: **flags OFF** hasta pasar calibración/carga/UAT según el acta del día-D.
- No avanzar con build/test en rojo. No reescribir ni "mejorar" módulos marcados DONE sin un bug confirmado y su registro.

---

### 8. Primer paso concreto (arranca aquí)

1. **ARRANCA AQUÍ: `QAS/23` pruebas 1 a 7 y después `QAS/17` completo.**
   `DT-P32-03` quedó 2/2 y **desplegada** el 2026-08-15 (`a9f4a6f`; backend 854 + 105, portal 60,
   build/format/`ng build`/diff verdes; CI y Deploy en success) y no queda código pendiente en la
   iniciativa. El artefacto ya está en el App Service, pero el gate
   `Conversacion:CatalogoTextosHabilitado` sigue sin override y por tanto OFF, así que las pruebas 1 a
   3 (cierre localizado) exigen encenderlo **solo** en el ambiente autorizado. Lo que sigue es
   operativo y humano: revisar en **Preparación** que cada par
   `plantillaRef + idioma` aparezca con su estado y las campañas que lo piden, confirmar
   `listoParaGateOn=true` **solo** cuando catálogos y mapeos estén completos, y verificar **a mano**
   en el administrador de WhatsApp que esas plantillas estén realmente aprobadas y con las mismas
   variables: readiness no consulta Graph API y no puede certificarlo. Solo con ese green se retoma
   `DT-I20-02`. El gate `Conversacion:CatalogoTextosHabilitado` permanece OFF hasta el acta de flags.

2. **Después del corte 2/2: la corrida operativa autorizada de `DT-P32-02` y P-32.**
   Los tres cortes quedaron DONE local el 2026-08-14 (backend 817 + 103; portal 57/57, build y
   Prettier verdes); `4d0f35c`, CI #110 y Deploy #95 están confirmados. Con gate OFF, crear y revisar
   los borradores base `es/en`, probar la descarga, revisión y carga del JSON editado, activar
   explícitamente los catálogos aprobados y ejecutar `QAS/22` Pruebas 1 a 8. Después abrir la ventana
   gate ON según `QAS/18` y completar `QAS/17`, comprobando que el aporte y la idea consolidada en
   inglés permanezcan en inglés. Cualquier FAIL o BLOCKED devuelve o mantiene el gate OFF y obliga a
   corregir y repetir; conservar evidencia sin secretos ni participantes reales.
   Registrar D5/UAT, Meta, costo/latencia y rollback. **Solo con `QAS/22` y `QAS/17` en green se
   cambia el handoff a `DT-I20-02` corte 1/3.**

3. **Antes de encender el gate:** confirmar aislamiento del emisor o teléfonos de prueba autorizados.
   `Simulacion__Habilitada=true` no evita salidas reales; al cierre debe quedar en `false` y el gate
   también, salvo acta formal de activación.

4. **Solo con DT-P32-02 y la corrida P-32 green: implementar `DT-I20-02` corte 1/3.** Mantener su
   spec, `QAS/21` y runbook sin cambios de alcance mientras espera.

5. **Después: validar P-33 localmente terminada.** Ejecutar `QAS/20_*` en un ambiente aislado,
   en español e inglés, y registrar D5/UAT, costo/latencia y el acta de flags. El gate global sigue
   OFF; no desplegar ni cambiar configuración remota sin autorización expresa.

6. **En paralelo, D5 de `DT-I20-01` (validación de calidad, no código).** El código está DONE local y
   verde. Usar ejemplos anonimizados y `QAS/19_*`; no hay flag, el rollback es revertir su commit.

7. **Pendiente del usuario (no bloquea código):** verificar el `409` a mano en Data Explorer y
   rehacer la prueba de humo de P-31 antes de encender sus flags. **No cargar datos reales** hasta
   que GHT entregue el archivo con `Telefono` diligenciado (`§9`). `I-08 v2` ya está desplegada y
   validada contra Azure (2026-08-08, 13 casos PASS).

8. **En paralelo (operativo, no de código):** validación D5 real, UAT, costo/latencia y acta de flags
   de I-19/I-20/P-24/P-25/P-26/P-27/P-28/P-29/P-30. Todos los flags nuevos permanecen apagados por
   defecto; no desplegar ni modificar configuración remota sin orden.
9. Lee, en el orden de §1: `AVANCES.md` (Próximo paso + Tablero) → `Iniciativas/00_Indice…` → la spec de la iniciativa → `Reglas_Conversacion…` y `SUPUESTOS.md` → las secciones de contrato/módulo que toque.
10. **Declara desde qué rol decides y qué REQ §/ARQ §/ID-iniciativa cubres.** Si la spec plantea una decisión de diseño (opción A/B/C, cambio de contrato, dónde vive un flag), **confírmala con el usuario antes de codificar**.
11. **La aprobación expresa de P-26, P-27, P-33, `DT-P32-02` y del diseño de `DT-I20-02` ya existe.** Implementa cada iniciativa en el orden y cortes
   de su spec. Solo vuelve a consultar al usuario si aparece una decisión de producto o contrato no
   resuelta por esas specs o sus anclas en `SUPUESTOS.md`.
12. Registra en `AVANCES.md` (marca DONE, tablero, siguiente "Próximo paso"), en `SUPUESTOS.md` y en `Reglas_Conversacion_y_Participacion.md` según corresponda.
13. **Al terminar CADA implementación, escribe una explicación de "Cómo probarlo" clara, natural y en lenguaje humano, para una persona con conocimientos técnicos BAJOS.** Va en el mensaje/chat con el que cierras el trabajo (y, si la iniciativa tiene sección "Cómo probarlo", coincídela). Reglas de ese texto: **resumido** (máx. ~5–8 pasos numerados), sin jerga (nada de nombres de clase, endpoints, flags técnicos ni rutas de código; si hay que nombrar algo, descríbelo por lo que el usuario ve: "la pantalla de Rúbricas", "el botón Ver"); di **qué abrir, qué hacer y qué debería verse** (resultado esperado en palabras simples) y qué significaría que **algo salió mal**. Objetivo: que Jason o alguien de GHT pueda **verificar el cambio sin ayuda técnica**.
14. Commits atómicos (Conventional Commits, con ID-iniciativa y REQ §/ARQ §; terminando con el trailer de coautoría que el repo exija). **Push a `main` solo cuando el usuario lo pida.** Continúa el bucle.
15. **Antes de cerrar cualquier sesión o dejar un handoff, actualiza este `TODO.md` sin excepción:** cabecera, estado de §4 y primer paso de §8 deben quedar sincronizados con `AVANCES.md`. Si hay bloqueo, déjalo explícito aquí con la condición concreta para retomarlo; no dejes un TODO que apunte a trabajo ya terminado.

Declara brevemente, antes de cada acción significativa, **desde qué rol** decides y **qué REQ §/ARQ § + ID-iniciativa** cubres. Mantén el rigor de un equipo de 25+ años: simple, correcto, probado y documentado.
