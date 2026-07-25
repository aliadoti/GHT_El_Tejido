# 00 — Contexto de auditoría

## Estado de esta memoria

- Inicio de auditoría: 2026-07-23.
- Fase actual: 6 — consolidación completada; auditoría cerrada documentalmente.
- Alcance autorizado: auditoría técnica integral sin cambios funcionales. Los únicos archivos que esta auditoría puede crear o actualizar están en `QAS/Auditoria/`.
- Estado del árbol antes de esta auditoría: modificación preexistente en `.obsidian/workspace.json` y carpeta no rastreada preexistente `QAS/Audit/` (incluye el encargo). Se preservan y quedan fuera del alcance.

## Stack detectado

| Área | Evidencia | Detección |
|---|---|---|
| Backend | `global.json`, proyectos `*.csproj` | .NET 8 / C# con nullable, implicit usings, warnings-as-errors y análisis de nivel `latest`. SDK efectivo local: 8.0.423. |
| Host | `src/ElTejido.Api/Program.cs` | ASP.NET Core minimal APIs; API, webhook de WhatsApp y SPA estática en un mismo host. |
| Capas | `Especificaciones/base/02_Arquitectura_y_Stack.md`, referencias de proyecto | Monolito modular: Api, Application, Domain, Infrastructure y Calibracion. |
| Datos e integraciones | `ElTejido.Infrastructure.csproj`, configuración | Azure Cosmos DB NoSQL, Blob Storage, Key Vault, WhatsApp Cloud API y proveedores LLM configurables. Application Insights está exigido por la especificación, pero no quedó cableado en el código local. |
| Frontend | `src/ElTejido.Web/package.json`, `angular.json` | Angular 22, TypeScript 6, SCSS, Vitest y Prettier. |
| Automatización | `.github/workflows/ci.yml`, `deploy.yml` | GitHub Actions; CI backend/frontend y CD a Azure App Service mediante OIDC. |

## Documentos y convenciones revisados

- `QAS/Audit/Prompt_auditoria_tecnica.md` — encargo y método de auditoría.
- `Especificaciones/README.md`, `AVANCES.md`, `SUPUESTOS.md` y `Iniciativas/TODO.md` — mapa, continuidad y estado del producto.
- `Arquitectura/El_Tejido_Arquitectura_Tecnica_MVP.md`.
- `Especificaciones/base/02_Arquitectura_y_Stack.md`, `03_Modelo_de_Datos_Cosmos.md`, `04_Contrato_API_REST.md`, `10_Seguridad_Guardrails_y_Observabilidad.md`, `11_Frontend_Portal_Angular.md`, `12_CICD_GitHub_Actions.md` y `13_Plan_de_Pruebas_y_Aceptacion.md`.
- `QAS/README.md` y `QAS/00_Plan_de_Pruebas.md`.
- Configuración, proyectos, puntos de entrada y workflows indicados en el inventario.

Convenciones verificadas: el diseño declara dirección de dependencias Api → Application → Domain e Infrastructure → Application/Domain; los contratos `03`, `04` y `08 §4` son aditivos; `AVANCES.md` es append-only; no se encontraron `AGENTS.md` ni `CLAUDE.md` con instrucciones de repositorio. `.claude/` contiene solo configuración local y un `launch.json`.

## Comandos reales identificados y resultado inicial

| Propósito | Comando | Fuente | Resultado en esta fase |
|---|---|---|---|
| Compilar backend | `dotnet build -c Release -warnaserror` | CI y `Directory.Build.props` | Ejecutado: correcto, 0 advertencias y 0 errores. |
| Pruebas backend sin calibración pagada | `dotnet test -c Release --no-build --filter "Category!=Calibracion"` | CI | Ejecutado: 423 aprobadas (371 unitarias, 52 integración). |
| Formato .NET | `dotnet format --verify-no-changes` | CI | Ejecutado: correcto. |
| Lint/formato frontend | `npm run lint` desde `src/ElTejido.Web` | `package.json` y CI | Ejecutado: correcto. |
| Typecheck frontend | `node node_modules/typescript/bin/tsc --noEmit` con runtime local | Validación estática adicional | Ejecutado: correcto. |
| Pruebas frontend | `ng test --watch=false` con CLI local | CI | Intentado; Angular 22 no inicia con Node 24.14.0 (mínimo 24.15.0). |
| Build frontend | `ng build --configuration production` con CLI local | CI | Intentado; bloqueado por el mismo mínimo de Node, sin error de fuente demostrado. |
| Ejecución local API | `dotnet run --project src/ElTejido.Api` | arquitectura y `Program.cs` | Identificado; no ejecutado (no se iniciarán servicios persistentes en reconocimiento). |
| Ejecución local SPA | `npm start` | `package.json` | Identificado; no ejecutado (no se iniciarán servicios persistentes en reconocimiento). |

## Supuestos y pendientes explícitos

- `unknown`: no se confirmó acceso a los recursos Azure, Key Vault, Cosmos, App Service, GitHub Actions ni WhatsApp; los controles que dependen de su estado desplegado requerirán evidencia adicional.
- `unknown`: se revisaron todas las dimensiones previstas mediante muestreo y rutas priorizadas, no cada línea, contrato o escenario de ejecución. Los límites de despliegue, integración real y accesibilidad asistida permanecen explícitos en el cierre.
- `requires_decision`: la auditoría no alterará código, configuración desplegada, secretos, migraciones ni documentos fuera de esta carpeta.
- `not_applicable` por ahora: no hay una migración EF Core que revisar; la persistencia documentada es Cosmos DB NoSQL. Se confirmará al revisar infraestructura.

## Riesgos iniciales a verificar (no son hallazgos)

1. La aplicación integra identidad, datos, mensajería y LLM; se revisarán especialmente autorización por recurso, secretos, firma/deduplicación del webhook, límites y registros sin PII.
2. La arquitectura acepta trabajo asíncrono en memoria para el MVP; se verificarán idempotencia, recuperación y límites operativos declarados.
3. La SPA se compila dentro de `wwwroot` del host; se verificará que autenticación, guards, estados de error y accesibilidad no dependan solo del cliente.
4. El estado funcional vigente indica flags globales y por campaña; se verificará que sus valores por defecto y controles administrativos mantengan una postura segura.

## Cierre de fase 3

- Se completó un escaneo de seguridad con inventario determinista de 474 archivos, 263 rutas runtime relevantes y 52 recibos de lectura profunda. El resultado final contiene dos hallazgos reportables: configuración LLM que puede enviar una clave de proveedor a un host no aprobado (media) y cuerpo de webhook sin límite local antes del HMAC (baja).
- La revisión de Cosmos confirmó aislamiento por clave de partición de campaña y consultas parametrizadas en los repositorios priorizados. No se confirmó inyección NoSQL ni cruce de campañas en ese alcance.
- Persisten como `requires_decision`/seguimiento la política efectiva de egress, Key Vault, App Service/WAF y los escenarios de concurrencia/durabilidad de conversación y envío; no se infieren desde el repositorio.

## Cierre de fase 4

- El inventario de rutas coincide con el contrato funcional principal y la API preserva 404 propio para prefijos API en el fallback del SPA. Liveness (`/health`), readiness protegida (`/health/ready`), rate limiting, correlación y el smoke test de CD tienen implementación y pruebas focalizadas en verde.
- Se confirmó una desviación de contrato: varios resultados de error se producen directamente y no pasan por el escritor uniforme, por lo que no llevan el cuerpo ni `correlationId` exigidos por `04 §3`.
- La aplicación deja logs estructurados y uso LLM por campaña, pero no contiene SDK/registro de Application Insights u OpenTelemetry. La telemetría, alertas y su estado Azure requieren comprobación humana antes de declarar cumplimiento operativo.
- El análisis de vulnerabilidades de NuGet no encontró paquetes vulnerables en los proyectos de producción; detectó dos paquetes transitivos con advisory alto en los proyectos de prueba, que se registran como higiene de CI sin atribuir impacto de runtime.

## Cierre de fase 5

- El portal conserva estructura semántica básica (`main`, `nav`, `header`, encabezados, tablas, botones y etiquetas envolventes), navegación por enlaces/botones nativos, responsive para 1050/680 px y avisos globales con `aria-live`.
- Se confirmaron tres brechas WCAG: controles sin nombre accesible, estados dinámicos de error sin región viva y pestañas que declaran `tablist` pero no completan el patrón ARIA.
- Prettier y el typecheck directo de TypeScript finalizaron correctamente. Las pruebas y el build Angular no se pudieron iniciar porque el único runtime compatible disponible es Node 24.14.0 y Angular exige como mínimo Node 24.15.0; no se atribuye ese bloqueo al código fuente.

## Cierre de fase 6

- Se consolidaron doce hallazgos: siete confirmados de severidad media, uno confirmado bajo, dos que requieren revisión humana de severidad media, uno que requiere revisión humana de severidad baja y `ARQ-001` como validación arquitectónica descartada. No se identificaron hallazgos confirmados críticos o altos en el alcance estático y local ejecutado.
- No se hallaron duplicados materiales: cada hallazgo retenido describe una causa, superficie y corrección mínima distintas. La cola in-process, la purga por defecto y los advisories solo de pruebas se conservan como excepciones o decisiones operativas, no se inflan a vulnerabilidades de producción.
- El cierre no certifica seguridad, cumplimiento completo ni disponibilidad de producción. Antes de abrir campañas reales siguen siendo necesarias las decisiones y verificaciones externas de egress/Key Vault/WAF, telemetría/alertas, concurrencia/reinicio y validación Angular/accesibilidad con tooling compatible.
