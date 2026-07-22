# 01 — Convenciones para Agentes de IA

**Propósito:** reglas comunes de ingeniería para que varios agentes implementen el MVP de forma coherente, paralelizable y verificable. **Todo agente DEBE leer este documento antes de escribir código.**

---

## 1. Principios de trabajo para agentes

1. **Contrato antes que implementación.** Antes de codificar un módulo, valida que los contratos que consumes (`03 Modelo de datos`, `04 Contrato API`) están definidos. Si un contrato que necesitas no existe o es ambiguo, **no lo inventes silenciosamente**: aplica la regla de ambigüedad (§9) y deja constancia.
2. **Cambios locales, no globales.** Cada agente trabaja dentro de la frontera de su módulo (`ARQ §1.3`). No modifiques código de otro módulo; si necesitas algo de otro módulo, consúmelo por su **interfaz pública** (interface C# inyectada).
3. **Idempotencia y reintentos.** Todo lo que toque WhatsApp o el LLM DEBE ser seguro ante reintentos (ver `03 §8` idempotencia y `08` reintentos).
4. **Nada de secretos en código.** Ninguna API key, token o secreto se escribe en código, `appsettings.json` versionado, logs ni Markdown. Solo referencias a Key Vault (ver `10 §4`).
5. **Trazabilidad.** Cada PR referencia los `REQ §` y `ARQ §` que implementa.
6. **Definition of Done (§8) es vinculante.** Una tarea no está "hecha" hasta cumplirla completa.

---

## 2. Estructura del repositorio

Monorepo. Backend .NET y frontend Angular conviven; el frontend se compila y se sirve como estático desde el backend (o como Static Web App; ver `02 §3`).

```
/ (raíz del repo)
├─ Requeriments/                      # Insumo (existente, no se toca)
├─ Arquitectura/                      # Insumo aprobado (existente, no se toca)
├─ Especificaciones/                  # Estas specs (no se tocan al implementar; se actualizan vía PR si cambia un contrato)
├─ Guias_Implementacion/              # Guías para el humano (Azure, Meta)
│
├─ src/
│  ├─ ElTejido.Api/                   # Host ASP.NET Core: webhook, /api/*, sirve el SPA. Composition root.
│  ├─ ElTejido.Domain/                # Entidades, value objects, interfaces de dominio. SIN dependencias de infraestructura.
│  ├─ ElTejido.Application/           # Casos de uso (módulos): orquestador, evaluación, campañas, markdown, auth.
│  ├─ ElTejido.Infrastructure/        # Cosmos, Blob, Key Vault, WhatsApp client, LLM clients, App Insights.
│  └─ ElTejido.Web/                   # App Angular 22 (portal admin).
│
├─ tests/
│  ├─ ElTejido.UnitTests/
│  └─ ElTejido.IntegrationTests/
│
├─ .github/workflows/                 # Pipelines (ver 12)
├─ ElTejido.sln
├─ Directory.Build.props              # Versión .NET, nullable, analizadores, warnings-as-errors
├─ .editorconfig
├─ global.json                        # Fija el SDK de .NET
└─ README.md
```

**Mapeo módulo de dominio → carpeta** (cada módulo de `ARQ §1.3` vive en `ElTejido.Application/<Modulo>/` con su interfaz en `ElTejido.Domain/<Modulo>/`):

| Módulo (ARQ) | Carpeta | Documento spec |
|---|---|---|
| WhatsApp Gateway | `Application/WhatsApp/` | `05` |
| Conversación / Orquestador | `Application/Conversacion/` | `05` |
| Identidad y matrícula | `Application/Identidad/` | `06` |
| Autenticación admin | `Application/Auth/` | `06` |
| Campañas y configuración | `Application/Campanas/` | `07` |
| Rúbricas y Prompts | `Application/Configuracion/` | `07` |
| Evaluación LLM | `Application/Evaluacion/` | `08` |
| Generación Markdown | `Application/Markdown/` | `09` |
| Seguridad / Guardrails | `Application/Seguridad/` | `10` |
| Logging y trazabilidad | `Infrastructure/Observabilidad/` | `10` |
| Portal admin | `ElTejido.Web/` | `11` |

---

## 3. Stack y versiones fijadas

> Detalle ampliado en `02 §2`. Aquí el resumen vinculante.

- **.NET 8 LTS** (C# 12). `global.json` fija la banda del SDK. `Nullable` habilitado, `TreatWarningsAsErrors=true`.
- **ASP.NET Core 8** (Minimal APIs o Controllers; ver `04 §1`).
- **Azure Cosmos DB** vía SDK `Microsoft.Azure.Cosmos` (v3, última estable).
- **Azure SDK** con `Azure.Identity` (`DefaultAzureCredential` / Managed Identity), `Azure.Security.KeyVault.Secrets`, `Azure.Storage.Blobs`.
- **Application Insights** vía `Microsoft.ApplicationInsights.AspNetCore`.
- **Angular 22** (última estable, junio 2026) + TypeScript, standalone components, signals. Build con Angular CLI/Vite.
- **Pruebas .NET:** xUnit + FluentAssertions + NSubstitute (o Moq). **Pruebas Angular:** las del CLI por defecto (Vitest/Karma según scaffolding del CLI v22).

No se introduce ninguna otra dependencia mayor sin justificarla en el PR y, si afecta a varios módulos, sin actualizar `02`.

---

## 4. Convenciones de código

### 4.1 .NET / C#
- **Estilo:** el de `.editorconfig` (incluido en el repo). `dotnet format` debe pasar limpio.
- **Naming:** `PascalCase` para tipos y métodos públicos; `camelCase` para locales y parámetros; `_camelCase` para campos privados; interfaces con prefijo `I`.
- **Async:** todo I/O es `async`/`await`, con sufijo `Async` en el nombre del método y `CancellationToken` propagado.
- **Inmutabilidad:** DTOs y entidades de lectura como `record` cuando aplique. Nada de estado mutable estático compartido.
- **Errores:** excepciones de dominio tipadas (`DomainException`, `NotFoundException`, `ValidationException`, `RateLimitException`). El middlware de la API las traduce al modelo de error de `04 §3`.
- **Sin lógica en controladores:** los endpoints delegan en casos de uso de `Application`. Los controladores solo orquestan request → caso de uso → response.
- **Inyección de dependencias:** todo se registra en el composition root (`ElTejido.Api/Program.cs` y extensiones `AddXxxModule()`). Nada de `new` sobre dependencias con I/O.

### 4.2 Angular / TypeScript
- **Componentes standalone**, `OnPush`, **signals** para estado local; servicios `providedIn: 'root'` para estado compartido y acceso a API.
- **Tipado estricto** (`strict: true` en `tsconfig`). Prohibido `any` salvo justificación local comentada.
- **Naming:** componentes `kebab-case` en archivos, `PascalCase` en clases; servicios con sufijo `Service`.
- **Sin llamadas HTTP en componentes:** todo acceso a API pasa por servicios tipados que reflejan `04`.
- **Estilos:** tokens de marca GHT centralizados (ver `11 §5`); nada de colores hardcodeados fuera del archivo de tokens.

### 4.3 Comentarios y documentación
- Comentar el **porqué**, no el **qué**. XML-doc en interfaces públicas de dominio.
- Cada interfaz de módulo lleva un comentario con el/los `REQ §` que cumple.

---

## 5. Configuración (cómo el código consume Azure)

El código **nunca crea recursos**; los consume por configuración. Convenciones:

- Configuración no-secreta: `appsettings.json` (valores por defecto) + variables de entorno de App Service (override por entorno). Claves bajo secciones `Cosmos`, `Blob`, `WhatsApp`, `Llm`, `Auth`, `Seguridad`.
- **Secretos: solo referencias.** En config se guarda el **nombre del secreto** en Key Vault (p. ej. `Llm:ApiKeySecretName = "llm-key"`), nunca el valor. El acceso real va por `DefaultAzureCredential` + Key Vault SDK (ver `10 §4`).
- Los nombres de recursos y secretos DEBEN coincidir con los definidos en `Guias_Implementacion/Guia_Azure_Portal_Paso_a_Paso.md §0` (tabla de nomenclatura). No se inventan nombres nuevos.
- **Local dev:** secretos vía `dotnet user-secrets`; emulador de Cosmos y Azurite (Blob) opcionales. Ver `02 §7`.

---

## 6. Git y flujo de trabajo

- **Branching:** `main` siempre desplegable. Ramas de trabajo `feat/<modulo>-<corto>`, `fix/<corto>`, `chore/<corto>`.
- **Commits:** Conventional Commits (`feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`). Mensaje en español o inglés, consistente dentro del repo (por defecto español).
- **PRs pequeños y por módulo.** Cada PR: descripción, `REQ §`/`ARQ §` cubiertos, cómo se probó, checklist de Definition of Done.
- **Protección de `main`:** merge solo con CI verde (build + tests + lint) y al menos una revisión (humana o de otro agente revisor).
- **Conflictos de contrato:** si dos agentes necesitan cambiar el mismo contrato compartido, el cambio va primero al documento de spec correspondiente en un PR aparte; luego cada módulo se adapta.

---

## 7. Trazabilidad a requerimientos

- Todo endpoint, caso de uso y pantalla referencia su `REQ §`. La matriz completa vive en `13 §6` (trazabilidad de aceptación).
- Si implementas algo sin requisito que lo respalde, **es señal de sobre-ingeniería**: detente y verifica el alcance (`REQ §6.1` incluido / `§6.2` excluido).

---

## 8. Definition of Done (DoD)

Una unidad de trabajo está terminada cuando **todo** lo siguiente se cumple:

1. Cumple los `REQ §` y criterios de aceptación asociados (`13`).
2. Respeta los contratos de `03` y `04` (sin romper a otros módulos).
3. Compila sin warnings (`TreatWarningsAsErrors`), `dotnet format` y lint de Angular limpios.
4. **Pruebas:** unitarias para la lógica de dominio del módulo; al menos una prueba de integración para los flujos con I/O (Cosmos/WhatsApp/LLM mockeados o contra emulador). Cobertura razonable de los caminos felices y de los de error/fallback.
5. Sin secretos en el diff. Sin PII innecesaria en logs.
6. Observabilidad mínima: logs estructurados y, donde aplique, `correlationId` propagado (`10 §6`).
7. Idempotencia verificada donde aplique (webhook, envíos, evaluación).
8. PR con descripción, trazabilidad y notas de prueba. CI verde.

---

## 9. Manejo de ambigüedad y supuestos

Cuando una spec no resuelve un caso:

1. **Primero**, busca la respuesta en este orden: documento del módulo → `03`/`04` → `02` → arquitectura aprobada → requerimientos.
2. Si sigue sin estar definido, **elige la opción más simple compatible con el MVP** y que **no cierre** fronteras post-MVP.
3. **Registra el supuesto** en un archivo `Especificaciones/SUPUESTOS.md` (crear si no existe) con: fecha, agente/PR, decisión, alternativa descartada y `REQ §` afectado. Así el supuesto es visible y revisable, en vez de quedar oculto en el código.
4. Si el supuesto afecta un contrato compartido, además abre PR sobre el documento de spec correspondiente.

Nunca bloquees el avance por una ambigüedad menor; nunca tomes una decisión mayor de arquitectura sin dejar rastro.

---

## 10. Seguridad y privacidad (resumen; detalle en 10)

- Secretos solo en Key Vault; acceso por Managed Identity.
- OTP solo como hash (Argon2/bcrypt + sal). Nunca en claro, ni en logs.
- HTTPS forzado en todo endpoint.
- Respuestas neutrales que no revelan existencia de números (`REQ §10.3.10`).
- La respuesta del usuario al LLM es **dato, no instrucción** (`REQ §25.3`).
- Markdown y logs técnicos sin secretos ni PII sensible.

---

## 11. Anti-patrones a evitar

- Microservicios, colas dedicadas o IaC compleja en el MVP (`ARQ §15`). El MVP es monolito modular + GitHub Actions.
- Base relacional. El repositorio es documental (Cosmos).
- Implementar capa vectorial, dashboards avanzados, Entra ID, exportaciones, gamificación (todos `REQ §6.2`, fuera de MVP).
- Hardcodear preguntas, mensajes, tags, rúbricas o prompts en código (`REQ §31.1`): todo es configurable en datos.
- Lógica de negocio en controladores o componentes Angular.

*Fin del documento.*
