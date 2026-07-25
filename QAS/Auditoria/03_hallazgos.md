# 03 — Hallazgos

## [ARQ-001] Dependencias de capas internas conforme al diseño declarado

- Dimensión: Arquitectura
- Clasificación: descartado
- Severidad: informativa
- Confianza: alta
- Archivo(s): `src/ElTejido.Api/ElTejido.Api.csproj:L3-L6`; `src/ElTejido.Application/ElTejido.Application.csproj:L3-L5`; `src/ElTejido.Infrastructure/ElTejido.Infrastructure.csproj:L3-L6`; `src/ElTejido.Domain/ElTejido.Domain.csproj:L1-L10`; `src/ElTejido.Api/Program.cs:L19-L45`
- Evidencia:
  - Api referencia Application e Infrastructure; Application solo Domain; Infrastructure Application y Domain; Domain no declara referencias de proyecto.
  - Las búsquedas estáticas no hallaron dependencias de Api, Infrastructure, ASP.NET Core o Azure desde Domain/Application. `IProveedorCorrelacion` solo documenta que su adaptador de edge usa `HttpContext` y no importa ese tipo.
  - El composition root registra adaptadores y casos de uso en `Program.cs` y extensiones de servicio; no se observó una referencia circular.
- Regla aplicable:
  - Domain no depende de nadie; Application define puertos; Infrastructure implementa adaptadores; Api actúa como composition root.
- Fuente:
  - `Especificaciones/base/02_Arquitectura_y_Stack.md` §3; `Especificaciones/base/01_Convenciones_para_Agentes.md` §3 y §4.1.
- Impacto:
  - No se confirma incumplimiento de dirección de dependencias en el alcance estático revisado.
- Corrección mínima sugerida:
  - No aplica. Mantener esta verificación en CI si se introducen nuevas capas o proyectos.
- Notas:
  - Esta conclusión no cubre comportamiento en ejecución ni todos los límites internos entre módulos.

## [CAL-001] Orquestador conversacional concentra demasiadas responsabilidades y dependencias

- Dimensión: Calidad y mantenibilidad
- Clasificación: confirmado
- Severidad: media
- Confianza: alta
- Archivo(s): `src/ElTejido.Application/Conversacion/OrquestadorConversacion.cs:L31-L1479`; `src/ElTejido.Application/Conversacion/OrquestadorConversacion.cs:L64-L113`; `src/ElTejido.Application/Conversacion/OrquestadorConversacion.cs:L114-L379`; `tests/ElTejido.UnitTests/Conversacion/OrquestadorConversacionTests.cs:L29-L1399`
- Evidencia:
  - La clase tiene 1.479 líneas, 27 campos privados y un constructor que recibe 13 colaboraciones/objetos de configuración.
  - `ProcesarMensajeEntranteAsync` ocupa 266 líneas (L114-L379) y coordina cupos, resolución de hilo, intención, reclasificación, evaluación, cierre, persistencia, Markdown, mensajería y telemetría.
  - El mismo archivo también contiene segmentación de ideas, clasificación de madurez, tejido colectivo, construcción de contexto/historial, límites, mensajes y utilidades de variantes. Su prueba unitaria asociada tiene 1.399 líneas y configura los mismos colaboradores.
  - Hay pruebas unitarias e integración que ejercitan rutas críticas; por tanto, el hallazgo es de facilidad de cambio y no una falta de prueba ni un defecto funcional confirmado.
- Regla aplicable:
  - La arquitectura exige fronteras claras y mantenibilidad por separación de módulos; la auditoría evalúa tamaño, cohesión y testabilidad.
- Fuente:
  - `Arquitectura/El_Tejido_Arquitectura_Tecnica_MVP.md` §1.1 y §1.3; `Especificaciones/base/02_Arquitectura_y_Stack.md` §1 y §8; `QAS/Audit/Prompt_auditoria_tecnica.md`, dimensión B.
- Impacto:
  - Cada regla nueva del flujo puede requerir comprender y modificar una ruta central extensa, con alto número de dobles de prueba. Esto eleva el riesgo de regresiones y el costo de incorporar cambios de campaña o conversación.
- Corrección mínima sugerida:
  - Conservar `IOrquestadorConversacion` como fachada y extraer gradualmente colaboradores internos por responsabilidad: resolución/transición de estado, políticas de límite/cierre y procesamiento posterior a evaluación. Añadir pruebas dirigidas a esos colaboradores antes de mover cada bloque.
- Notas:
  - No existe un umbral numérico de longitud impuesto por el repositorio; la severidad se fundamenta en la concentración comprobable de responsabilidades, dependencias y rutas, no solo en el número de líneas.

## [CAL-002] La página de campañas reúne cinco flujos administrativos y toda su plantilla en un componente

- Dimensión: Calidad y mantenibilidad
- Clasificación: confirmado
- Severidad: media
- Confianza: alta
- Archivo(s): `src/ElTejido.Web/src/app/features/campanias/campanias.page.ts:L21-L1169`; `src/ElTejido.Web/src/app/features/campanias/campanias.page.ts:L702-L1169`; `src/ElTejido.Web/src/app/e2e/portal-admin.e2e.spec.ts:L344-L426`
- Evidencia:
  - Un solo archivo de 1.169 líneas contiene una plantilla inline de aproximadamente 680 líneas y el componente de L702-L1169.
  - El componente implementa listado/selección y creación/edición de campañas, mensajes iniciales, preguntas, vista previa/asociación de participantes y dos reinicios destructivos; además transforma y conserva campos de configuración antes de emitir requests.
  - Cumple las convenciones visibles de Angular: es standalone, usa signals y delega HTTP a `AdminApiService`. Las pruebas E2E cubren algunas operaciones de campaña, pero no eliminan la concentración de cambios en un único componente.
- Regla aplicable:
  - La SPA debe usar componentes standalone, signals para estado local y servicios tipados para API; la auditoría evalúa cohesión, tamaño y facilidad de cambio.
- Fuente:
  - `Especificaciones/base/01_Convenciones_para_Agentes.md` §4.2 y §4.3; `Especificaciones/base/11_Frontend_Portal_Angular.md` §1-§4; `QAS/Audit/Prompt_auditoria_tecnica.md`, dimensión B.
- Impacto:
  - Cambios independientes en cualquiera de los cinco flujos pueden producir conflictos y regresiones en la misma plantilla/componente, y hacen más costosa la revisión y prueba focalizada.
- Corrección mínima sugerida:
  - Mantener la ruta y el servicio API, pero separar de forma incremental formularios y operaciones en componentes hijos (por ejemplo, mensajes/preguntas y participantes/reinicio), con contratos de entrada/salida y pruebas de componente para cada flujo extraído.
- Notas:
  - No se informa una infracción por llamadas HTTP en componente: el acceso se realiza a través de `AdminApiService`.

## [SEC-001] Una configuración LLM puede redirigir una clave de proveedor a un host no aprobado

- Dimensión: Seguridad
- Clasificación: confirmado
- Severidad: media
- Confianza: media
- Archivo(s): `src/ElTejido.Application/Configuracion/ServicioGestionConfiguracion.cs:L267-L298`; `src/ElTejido.Infrastructure/Llm/LlmClientHttp.cs:L42-L62`; `src/ElTejido.Infrastructure/Llm/LlmClientHttp.cs:L171-L200`.
- Evidencia:
  - Una actualización administrativa puede cambiar `Endpoint` y conservar un `ApiKeyRef` existente; no se valida URI HTTPS, host permitido ni asociación proveedor-clave.
  - En evaluación, `LlmClientHttp` resuelve la clave, deriva la ruta desde el endpoint persistido y aplica autenticación antes de `SendAsync`.
  - La ruta exige `Admin` y CSRF. Las pruebas con handler en memoria confirman URI y header sin tráfico externo; egress y Key Vault efectivos no son evidencia del repositorio.
- Impacto: un administrador comprometido o en uso indebido puede provocar que el proceso envíe una clave de proveedor y el cuerpo de evaluación a un host elegido. No es un vector anónimo ni prueba acceso a secretos fuera de la referencia conservada.
- Corrección mínima sugerida: validar la configuración efectiva en creación, parche, importación y activación: URI absoluta HTTPS, catálogo proveedor-host y compatibilidad proveedor-referencia de secreto; complementar con allowlist de egress.
- Notas: subiría de severidad solo con evidencia de egress irrestricto o alcance de secretos más amplio.

## [SEC-002] El webhook materializa el cuerpo completo antes de verificar su firma HMAC

- Dimensión: Seguridad y operabilidad
- Clasificación: confirmado
- Severidad: baja
- Confianza: media
- Archivo(s): `src/ElTejido.Api/WhatsApp/EndpointsWebhook.cs:L76-L93`; `src/ElTejido.Api/WhatsApp/EndpointsWebhook.cs:L113-L117`.
- Evidencia:
  - Tras el rate limit por IP, `LeerCuerpoAsync` copia todo `Request.Body` a `MemoryStream` y crea un segundo arreglo con `ToArray()`.
  - La firma `X-Hub-Signature-256` se comprueba después; no se halló límite de tamaño versionado ni prueba de rechazo temprano.
  - El límite de 60 solicitudes/minuto por IP y cola cero reduce presión de una IP, no el presupuesto de bytes por solicitud ni fuentes distribuidas.
- Impacto: un remitente sin sesión ni secreto puede inducir trabajo de red/memoria y HMAC sobre cuerpos que terminarán en `401`; el límite real de App Service/proxy/WAF es desconocido.
- Corrección mínima sugerida: aplicar un máximo de bytes específico del endpoint antes de `CopyToAsync`, incluso para streaming/chunked, y probar `413` previo a firma/cola.

## [PER-001] Persistencia concurrente y durabilidad de entrega requieren prueba focalizada adicional

- Dimensión: Persistencia
- Clasificación: requiere_revision_humana
- Severidad: media
- Confianza: media
- Archivo(s): `src/ElTejido.Infrastructure/Conversaciones/ConversationsCosmosContainer.cs:L15-L20`; `src/ElTejido.Application/WhatsApp/ServicioEnvios.cs`; `src/ElTejido.Application/WhatsApp/ProcesadorEnvio.cs`.
- Evidencia: los repositorios priorizados de participantes, respuestas y usuarios usan PK de campaña y consultas parametrizadas; no se confirmó inyección NoSQL ni cruce de campaña. Conversaciones usa `UpsertItemAsync` sin precondición de versión visible y la cola/job de envío es in-process para el MVP.
- Impacto: no se afirma pérdida ni duplicación confirmada. Las garantías de una transición/entrega bajo solicitudes simultáneas o reinicio deben probarse con Cosmos y workers.
- Corrección mínima sugerida: definir invariantes y crear pruebas coordinadas de concurrencia, reintento y reinicio; aplicar ETag/transacción o reserva idempotente solo si la prueba demuestra una carrera material.
- Notas: conclusión diferida, no vulnerabilidad de seguridad confirmada ni autorización para rediseñar la cola.

## [API-001] Los errores devueltos directamente no respetan el cuerpo uniforme ni la correlación del contrato

- Dimensión: API y operabilidad
- Clasificación: confirmado
- Severidad: media
- Confianza: alta
- Archivo(s): `Especificaciones/base/04_Contrato_API_REST.md:L41-L66`; `src/ElTejido.Api/Errores/EscritorRespuestaError.cs:L18-L38`; `src/ElTejido.Api/Diagnostico/EndpointsPreparacion.cs:L35-L54`; `src/ElTejido.Api/WhatsApp/EndpointsWebhook.cs:L46-L65,L76-L92`; `src/ElTejido.Api/Admin/EndpointsAdminEnvios.cs:L76-L84`; `src/ElTejido.Api/Diagnostico/FiltroClaveDiagnostico.cs:L30-L42`.
- Evidencia:
  - El contrato exige que todos los errores devuelvan `error.code`, `message`, detalles opcionales y `correlationId`; el escritor común es el único componente que serializa ese formato.
  - Readiness sin clave devuelve `Results.NotFound()`, los rechazos de verificación/firma del webhook devuelven solo `StatusCode(401/403)` y el job inexistente crea un JSON distinto, sin `correlationId`.
  - El middleware sí normaliza excepciones y rate limiting, y las 10 pruebas focalizadas de health, readiness y modelo de errores pasan; las pruebas actuales de readiness comprueban solo el estado 404, por lo que no detectan la divergencia de cuerpo.
- Regla aplicable:
  - Modelo uniforme de errores y `correlationId` en toda respuesta de error.
- Fuente:
  - `Especificaciones/base/04_Contrato_API_REST.md` §3 y §8; `Especificaciones/base/10_Seguridad_Guardrails_y_Observabilidad.md` §6.2; `QAS/Audit/Prompt_auditoria_tecnica.md`, dimensión E.
- Impacto:
  - Los clientes y la operación no pueden tratar de forma homogénea errores 401/403/404 ni correlacionarlos con logs. Esto complica soporte y rompe el contrato explícito, aunque algunos 404/401 sean deliberadamente poco reveladores.
- Corrección mínima sugerida:
  - Exponer un creador de `IResult` que reutilice `EscritorRespuestaError`/`RespuestaError` y usarlo en todos los retornos de error directos, preservando mensajes neutros para readiness y webhook. Añadir pruebas de integración para 401, 403, 404 y job inexistente que validen cuerpo y cabecera de correlación.
- Notas:
  - No se recomienda convertir el 404 deliberado de readiness en 401/403: debe mantenerse indistinguible, pero con el envoltorio contractual y sin revelar detalle.

## [OPS-001] La telemetría técnica y alertas exigidas no se pueden acreditar desde el código ni la configuración local

- Dimensión: Operabilidad
- Clasificación: requiere_revision_humana
- Severidad: media
- Confianza: alta sobre la ausencia local; media sobre el estado desplegado
- Archivo(s): `Especificaciones/base/10_Seguridad_Guardrails_y_Observabilidad.md:L67-L84`; `src/ElTejido.Api/ElTejido.Api.csproj:L1-L15`; `src/ElTejido.Infrastructure/ElTejido.Infrastructure.csproj:L1-L29`; `src/ElTejido.Api/Program.cs:L18-L76`; `src/ElTejido.Api/appsettings.json:L1-L58`; `src/ElTejido.Infrastructure/Llm/LlmClientHttp.cs:L263-L279`.
- Evidencia:
  - La especificación exige Application Insights para trazas de requests/dependencias, latencias, errores, métricas LLM y alertas.
  - Los proyectos no referencian SDK de Application Insights/OpenTelemetry, `Program.cs` no registra un proveedor de telemetría y los `appsettings` no declaran conexión/configuración de telemetría. Sí existen `ILogger` estructurado, scope de correlación y log de tokens por campaña.
  - CI/CD hace smoke de `/health`; no verifica telemetría, alertas ni el `correlationId` de extremo a extremo en Azure.
- Regla aplicable:
  - Telemetría técnica, propagación de correlación y alertas operativas antes de abrir campañas reales.
- Fuente:
  - `Especificaciones/base/10_Seguridad_Guardrails_y_Observabilidad.md` §6.2-§6.3; `Especificaciones/base/12_CICD_GitHub_Actions.md` §4 y §7; `Especificaciones/base/13_Plan_de_Pruebas_y_Aceptacion.md` §7.
- Impacto:
  - No puede demostrarse localmente que incidencias de Cosmos/WhatsApp/LLM, consumo y cadena de correlación lleguen al plano de operación ni que existan alertas. Un agente de App Service o una configuración Azure externa podría aportar parte de ello, por lo que no se afirma su ausencia desplegada.
- Corrección mínima sugerida:
  - Decidir y registrar el mecanismo (SDK/Application Insights u OpenTelemetry + configuración Azure), instrumentar dependencias y métricas necesarias sin PII, y ejecutar una prueba en staging que siga un `correlationId` y dispare una alerta no productiva.
- Notas:
  - La readiness protegida y los logs actuales son controles útiles, pero no sustituyen trazas de dependencias, métricas ni alertas.

## [DEP-001] El escaneo de NuGet reporta paquetes transitivos vulnerables en los proyectos de prueba

- Dimensión: Operabilidad y dependencias
- Clasificación: requiere_revision_humana
- Severidad: baja
- Confianza: alta
- Archivo(s): `tests/ElTejido.UnitTests/ElTejido.UnitTests.csproj`; `tests/ElTejido.IntegrationTests/ElTejido.IntegrationTests.csproj`; resultado de `dotnet list ElTejido.sln package --vulnerable --include-transitive`.
- Evidencia:
  - El comando de NuGet no informa vulnerabilidades en Api, Domain, Application, Infrastructure ni Calibracion.
  - Solo UnitTests e IntegrationTests resuelven transitivamente `System.Net.Http` 4.3.0 (GHSA-7jgj-8wvc-jh57) y `System.Text.RegularExpressions` 4.3.0 (GHSA-cmhx-cq75-c4mj), ambos con severidad upstream alta.
- Regla aplicable:
  - El pipeline debe mantener dependencias actualizadas y la auditoría no debe atribuir severidad de librería a producción sin trazar su alcance.
- Fuente:
  - `Especificaciones/base/12_CICD_GitHub_Actions.md` §6; `QAS/Audit/Prompt_auditoria_tecnica.md`, dimensiones C y F.
- Impacto:
  - La evidencia apunta a superficies de ejecución de pruebas/CI, no al artefacto de producción. Aun así, el aviso puede bloquear o degradar el cumplimiento de higiene de dependencias y debe resolverse o aceptarse explícitamente.
- Corrección mínima sugerida:
  - Trazar qué paquete de prueba introduce las dependencias, actualizar el paquete padre o aplicar una exclusión/versionado compatible; volver a ejecutar el escaneo y documentar cualquier aceptación temporal.
- Notas:
  - No se clasificó como vulnerabilidad de producción porque el escáner reportó limpios todos los proyectos productivos.

## [UXA11Y-001] Los controles de selección y algunos campos de formulario no tienen nombre accesible

- Dimensión: UX y accesibilidad
- Clasificación: confirmado
- Severidad: media
- Confianza: alta
- Archivo(s): `src/ElTejido.Web/src/app/features/envios/envios.page.ts:L101-L121`; `src/ElTejido.Web/src/app/features/usuarios/usuarios.page.ts:L149-L159`; `src/ElTejido.Web/src/app/features/usuarios/usuarios.page.ts:L182-L197`.
- Evidencia:
  - La tabla de envíos presenta un checkbox de selección total y uno por participante sin `label`, `aria-label` ni `aria-labelledby`; un lector de pantalla anuncia controles de selección sin propósito distinguible.
  - El formulario inline para crear tags tiene tres `input` cuyo único texto descriptivo es `placeholder`; el selector de archivo CSV tampoco tiene etiqueta asociada.
  - El resto de formularios principales suele usar `label` envolvente, por lo que el defecto está acotado y no se infiere de todos los controles del portal.
- Regla aplicable:
  - Todo control de interfaz debe exponer nombre, rol y valor; las instrucciones o placeholders no sustituyen una etiqueta persistente.
- Fuente:
  - WCAG 2.2 AA, criterios 1.3.1, 3.3.2 y 4.1.2; `QAS/Audit/Prompt_auditoria_tecnica.md`, dimensión G; `Especificaciones/base/11_Frontend_Portal_Angular.md` §7.
- Impacto:
  - Una persona que navega con lector de pantalla no puede saber qué participante selecciona ni identificar de forma fiable los campos para crear tags o cargar un archivo, lo que bloquea acciones administrativas relevantes.
- Corrección mínima sugerida:
  - Asociar `label for`/`id` o `aria-label` a los tres campos y al selector de archivo. Para cada checkbox, usar un nombre que incluya la acción y, en filas, el usuario; el selector total debe indicar que selecciona todos los participantes visibles.
- Notas:
  - La corrección debe conservar la etiqueta visible cuando sea viable; `aria-label` es apropiado para controles compactos de tabla.

## [UXA11Y-002] Los errores y confirmaciones dinámicos de las pantallas no se anuncian de forma fiable

- Dimensión: UX y accesibilidad
- Clasificación: confirmado
- Severidad: media
- Confianza: alta
- Archivo(s): `src/ElTejido.Web/src/app/features/auth/login.page.ts:L62-L67`; `src/ElTejido.Web/src/app/features/dashboard/dashboard.page.ts:L20-L24`; `src/ElTejido.Web/src/app/features/campanias/campanias.page.ts:L46-L50`; `src/ElTejido.Web/src/app/layout/notificaciones.component.ts:L10-L28`.
- Evidencia:
  - Login y las pantallas de dashboard, campañas, envíos, resultados y configuración insertan `p.form-error` después de una respuesta asíncrona sin `role="alert"`, `role="status"` ni `aria-live`.
  - El componente global de toasts sí declara `aria-live="polite"`, pero login se muestra fuera del shell y varios caminos solo actualizan el error local; por tanto no hay anuncio garantizado para todos los resultados.
  - No se hallaron pruebas de componentes/E2E que ejerciten foco, lector de pantalla o regiones vivas.
- Regla aplicable:
  - Los mensajes de estado o error que aparecen sin mover foco deben estar disponibles para tecnologías asistivas.
- Fuente:
  - WCAG 2.2 AA, criterios 3.3.1, 3.3.3 y 4.1.3; `QAS/Audit/Prompt_auditoria_tecnica.md`, dimensión G; `Especificaciones/base/11_Frontend_Portal_Angular.md` §7.
- Impacto:
  - Tras solicitar/verificar OTP o cargar datos, usuarios de lector de pantalla pueden no percibir el resultado ni saber que deben corregir o reintentar, aunque el texto sea visualmente visible.
- Corrección mínima sugerida:
  - Definir un componente/atributo común para errores (`role="alert"` o región `aria-live="assertive"`) y para confirmaciones no críticas (`role="status"`/polite); usarlo también en login y enlazar errores de validación de campo con `aria-describedby` cuando aplique.
- Notas:
  - No conviene anunciar mensajes de éxito y fallo con la misma urgencia: conservar `polite` para confirmaciones y reservar `assertive` para errores que requieren atención.

## [UXA11Y-003] El detalle de campaña declara un tablist sin completar el patrón ARIA de pestañas

- Dimensión: UX y accesibilidad
- Clasificación: confirmado
- Severidad: media
- Confianza: alta
- Archivo(s): `src/ElTejido.Web/src/app/features/campanias/campanias.page.ts:L189-L225`; `src/ElTejido.Web/src/styles.scss:L303-L329`.
- Evidencia:
  - El contenedor usa `role="tablist"`, pero los cuatro botones no declaran `role="tab"`, `aria-selected`, `aria-controls` ni relación con un panel `role="tabpanel"`.
  - El cambio de pestaña solo se implementa mediante clic y una clase visual `active`; no hay manejo de flechas/Home/End ni prueba de teclado.
  - Los botones nativos siguen siendo activables con Tab/Enter/Espacio, pero al declarar explícitamente un tablist el árbol accesible comunica un patrón incompleto e inconsistente.
- Regla aplicable:
  - Los componentes compuestos deben exponer semántica, estado y operación por teclado coherentes con el patrón ARIA elegido.
- Fuente:
  - WCAG 2.2 AA, criterios 2.1.1, 4.1.2 y 4.1.3; patrón WAI-ARIA Tabs; `QAS/Audit/Prompt_auditoria_tecnica.md`, dimensión G.
- Impacto:
  - Tecnologías asistivas no reciben cuál pestaña está activa ni su panel asociado; el uso de teclado no sigue la interacción esperada de una interfaz de pestañas.
- Corrección mínima sugerida:
  - Implementar el patrón completo: ids estables, `role="tab"`, `aria-selected`, `aria-controls`, paneles con `role="tabpanel"`/`aria-labelledby`, roving tabindex y navegación con flechas/Home/End; añadir prueba de teclado. Alternativamente, retirar `role="tablist"` si se decide mantener botones independientes.
- Notas:
  - Esta observación no cuestiona la funcionalidad visual de las secciones ni exige adoptar un componente externo.
