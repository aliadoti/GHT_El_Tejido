# 04 — Evidencias

## Fase 1 — Reconocimiento

| Acción | Resultado | Limitación |
|---|---|---|
| Inspección de raíz, solución, proyectos, configuración, workflows, estructura y documentos | Se identificaron siete proyectos de solución, Angular 22, documentación canónica, CI/CD y puntos de entrada. | No equivale todavía a revisión completa de implementación. |
| `dotnet --info` | SDK efectivo 8.0.423 sobre Windows x64; `global.json` fija 8.0.400 con `latestFeature`. | Entorno local, no runner CI ni App Service. |
| `node --version`; `npm --version` | Node v22.17.0 y npm 10.9.2. | El CI declara Node 22.x; no se verificó exactamente la misma revisión. |
| `dotnet build -c Release -warnaserror` | Correcto: 0 advertencias, 0 errores. | No prueba ejecución ni recursos externos. |
| `dotnet test -c Release --no-build --filter "Category!=Calibracion"` | Correcto: 371 pruebas unitarias y 52 de integración, 423 en total. | Excluye explícitamente calibración que llama un LLM real. |
| `dotnet format --verify-no-changes` | Correcto. | Solo revisa reglas configuradas para el formato/análisis aplicado. |
| `npm run lint` en `src/ElTejido.Web` | Correcto: Prettier no detectó archivos fuera de estilo. | No evalúa comportamiento ni accesibilidad completa. |

## Rutas y fuentes revisadas

- `QAS/Audit/Prompt_auditoria_tecnica.md`.
- `Especificaciones/README.md`, `AVANCES.md`, `SUPUESTOS.md`, `Iniciativas/TODO.md`.
- Arquitectura, especificaciones base 02/03/04/10/11/12/13 y plan QAS indicados en `00_contexto_auditoria.md`.
- `ElTejido.sln`, `global.json`, `Directory.Build.props`, proyectos, configuración API, `Program.cs`, `package.json`, `angular.json` y workflows de GitHub Actions.

## Limitaciones vigentes

- No se consultaron secretos ni servicios externos.
- No se iniciaron servicios locales persistentes.
- Las pruebas y el build Angular se intentaron, pero la CLI no arranca con el Node 22.17.0/24.14.0 disponible; exige 22.22.3, 24.15.0 o superior.
- Se ejecutó el análisis de vulnerabilidades de dependencias; no se ejecutaron pruebas E2E con infraestructura real ni validación manual con teclado, zoom o lector de pantalla.

## Fase 2 — Arquitectura y calidad/mantenibilidad

| Acción | Resultado | Limitación |
|---|---|---|
| Revisión de referencias de proyecto y de imports prohibidos en Domain/Application | La dirección declarada se respeta en el alcance revisado: Domain sin referencias; Application → Domain; Infrastructure → Application/Domain; Api compone Application + Infrastructure. Sin referencias estáticas a Api/Infrastructure/ASP.NET/Azure desde Domain/Application. | El comando `dotnet list ElTejido.sln reference` no es válido para la solución; las referencias se verificaron directamente en cada `*.csproj`. No prueba comportamiento en ejecución. |
| Revisión de `Program.cs` y extensiones de registro | Los adaptadores se registran mediante DI y los casos de uso se cablean desde el host. | No se validó aún cada ciclo de vida ni todos los escenarios de resolución DI. |
| Métrica y revisión de `OrquestadorConversacion` | 1.479 líneas, 27 campos privados, 13 colaboraciones en constructor; método de entrada de 266 líneas y varias políticas en la misma clase. | No hay un límite de líneas documentado; se evaluó concentración de responsabilidades comprobable. |
| Métrica y revisión de `CampaniasPage` | 1.169 líneas; plantilla inline aproximada de 680 líneas y cinco flujos administrativos en el mismo componente. | No se ejecutaron aún pruebas/build frontend; eso corresponde a la fase UX/accesibilidad. |
| Revisión de pruebas relacionadas | `OrquestadorConversacionTests` (1.399 líneas) y pruebas de integración cubren rutas conversacionales; `portal-admin.e2e.spec.ts` cubre operaciones de campaña seleccionadas. | La cobertura no se midió porcentualmente y no se infiere ausencia de prueba a partir de búsquedas de texto. |
| Búsqueda de excepciones vacías y marcadores de deuda | No se encontraron `catch` vacíos ni `TODO`/`FIXME`/`HACK` productivos inequívocos. | Búsqueda textual; no sustituye inspección de todos los caminos de error. |

## Fase 3 — Seguridad y persistencia

| Acción | Resultado | Limitación |
|---|---|---|
| Inventario/ranking y lectura profunda | 474 archivos inventariados; 263 runtime relevantes; 52 de mayor prioridad revisados con 52/52 recibos conciliados. | El ranking no equivale a lectura exhaustiva de los 263; los límites de despliegue y concurrencia quedaron diferidos explícitamente. |
| Modelo de amenazas, cobertura y candidatos | 20 superficies de alto impacto cerradas; cinco candidatos crudos se reconciliaron en cuatro instancias; dos sobrevivieron validación y ruta de ataque. | Sin `SECURITY.md`, secretos, servicios externos ni configuración desplegada. |
| Validación LLM | `dotnet test -c Release --no-build --filter "FullyQualifiedName~LlmClientHttpTests"`: 2/2 aprobadas; traza endpoint persistido → clave resuelta → header HTTP sin red. | Sin egress, Key Vault ni proveedor real. |
| Validación purga | `dotnet test -c Release --no-build --filter "FullyQualifiedName~ServicioPurgaCampaniasTests"`: 3/3 aprobadas. El flag activado por defecto es observación operativa, no vulnerabilidad final por requerir Admin + CSRF. | Sin override efectivo de App Service. |
| Validación OTP | `dotnet test -c Release --no-build --filter "FullyQualifiedName~AuthAdminServiceTests|FullyQualifiedName~RepositoriosSeguridadCosmosTests|FullyQualifiedName~AuthEndpointsIntegrationTests"`: 14/14 aprobadas. La carrera requiere el mismo OTP y solo emite sesiones del mismo principal; no sobrevivió por política final. | Sin carrera contra Cosmos real. |
| Persistencia Cosmos priorizada | PK de campaña y `QueryDefinition`/filtros tipados en repositorios de participantes, respuestas y usuarios. | Sin prueba concurrente/reinicio real para conversaciones y envíos. |
| Escaneo especializado sellado | Dos hallazgos reportables: uno medio y uno bajo; informe Markdown, JSON canónico, SARIF, recibos y cartera de endurecimiento generados. | Artefactos de seguridad bajo directorio temporal de la herramienta; conservar/exportar si se requiere retención. |

## Fase 4 — API y operabilidad

| Acción | Resultado | Limitación |
|---|---|---|
| Inventario `Map*`, middleware y fallback SPA | Se revisaron rutas de auth/admin/webhook/diagnóstico, `/health` y `/health/ready`. El fallback conserva 404 para prefijos API/webhook/health/diagnóstico y sirve el SPA solo para rutas de cliente. | No se ejecutó contra App Service ni Meta reales. |
| Contraste de errores | El middleware y rate limiting usan el escritor uniforme, pero se hallaron retornos directos 401/403/404 y un 404 de job con formato distinto; se confirma `API-001`. | La intención de ocultar readiness es válida; el hallazgo trata la ausencia de envoltorio/correlación, no la elección de estado. |
| Salud, readiness y errores | `dotnet test -c Release --no-build --filter "FullyQualifiedName~ModeloErroresIntegrationTests|FullyQualifiedName~PreparacionEndpointTests|FullyQualifiedName~HealthEndpointTests"`: 10/10 integración aprobadas. | Las pruebas de readiness actuales validan estado, no cuerpo de error uniforme. |
| Workers, colas y recuperación | Webhook/envíos usan `Channel` in-process y dos `BackgroundService`; los fallos por ítem se aíslan y los reintentos/redespacho se documentan. | La pérdida ante reinicio ya está registrada en `PER-001`; no hubo reinicio de Azure/Cosmos real. |
| Telemetría y CI/CD | Hay `ILogger` estructurado, `X-Correlation-Id`, log de tokens LLM y smoke `/health` en deploy. No se hallaron SDK/registro/configuración local de Application Insights u OpenTelemetry; se registra `OPS-001` como verificación humana de Azure. | Agente de App Service, conexión y alertas configuradas fuera del repositorio no son observables localmente. |
| Vulnerabilidades NuGet | `dotnet list ElTejido.sln package --vulnerable --include-transitive`: proyectos productivos sin vulnerabilidades; dos transitivos 4.3.0 con advisory alto solo en UnitTests/IntegrationTests. | El comando refleja el feed accesible en esta ejecución y no prueba explotación; se registra `DEP-001` como higiene de CI. |
| Integridad del árbol | `git diff --check` correcto; se conservaron `.obsidian/workspace.json` y `QAS/Audit/` como cambios preexistentes. | `QAS/Auditoria/` contiene los documentos nuevos de auditoría y permanece sin seguimiento. |

## Fase 5 — UX y accesibilidad

| Acción | Resultado | Limitación |
|---|---|---|
| Revisión de shell, rutas, guards y controles nativos | Shell con `aside`/`nav`/`main`, rutas protegidas, botones/enlaces nativos, tablas y `label` envolvente en la mayoría de formularios. Existen responsive breakpoints a 1050 y 680 px. | Revisión estática: no sustituye ensayo con teclado, zoom ni lector de pantalla real. |
| Nombres de controles | Se confirmaron checkboxes sin nombre en envíos, inputs de tags solo con placeholder y selector CSV sin etiqueta (`UXA11Y-001`). | No se efectuó prueba manual con NVDA/VoiceOver. |
| Estados dinámicos | Toast global `aria-live="polite"` correcto; los errores/confirmaciones locales, incluido login, no tienen región viva o rol de estado (`UXA11Y-002`). | No hay pruebas A11y de componentes o E2E encontradas. |
| Pestañas de campañas | `role="tablist"` existe, pero faltan roles/estados/relación con paneles y teclado del patrón de pestañas (`UXA11Y-003`). | Los botones siguen operables con teclado básico; el defecto es semántico e interacción compuesta. |
| Validación frontend | Prettier correcto y `tsc --noEmit` directo correcto con Node 24.14.0. | `ng test --watch=false` y `ng build --configuration production` no arrancaron: Angular CLI 22 exige Node 22.22.3, 24.15.0 o superior, y el runtime disponible era 22.17.0/24.14.0. |
| Integridad del árbol | `git diff --check` correcto. | Se preservan `.obsidian/workspace.json` y `QAS/Audit/`; la carpeta `QAS/Auditoria/` es el único alcance documental de esta auditoría. |
