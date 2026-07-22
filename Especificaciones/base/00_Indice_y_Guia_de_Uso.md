# El Tejido — Especificaciones del MVP

**Proyecto:** El Tejido — Sistema conversacional de captura, evaluación y compilación de conocimiento institucional.
**Estado:** Especificación para construcción del MVP.
**Base normativa:** `Requeriments/GHT_banco_de_ideas_req_inicial.md` (v0.4) y `Arquitectura/El_Tejido_Arquitectura_Tecnica_MVP.md` (v1.0, **aprobada**).
**Audiencia:** Agentes de IA de desarrollo (uno o varios) y revisores humanos.
**Fecha:** Junio 2026.

---

## 0. Cómo usar este conjunto de documentos

Este directorio (`Especificaciones/`) contiene la especificación completa y autocontenida para construir el **MVP** de El Tejido. Está escrito para que **distintos agentes de IA puedan implementar módulos en paralelo** sin pisarse, partiendo de contratos explícitos.

Reglas de oro para cualquier agente que implemente a partir de estas specs:

1. **La arquitectura ya está aprobada.** No se rediseña. Si una decisión técnica no está en estas specs, se sigue lo definido en `Arquitectura/El_Tejido_Arquitectura_Tecnica_MVP.md`. Si tampoco está ahí, se aplica la convención del documento `01_Convenciones_para_Agentes.md` y se deja registrada la decisión.
2. **El alcance es exclusivamente el MVP.** Todo lo marcado como *fuera de MVP* (capa vectorial, dashboard avanzado, Entra ID, Git de Markdown, etc.) **no se implementa**, pero **no se obstruye**: se dejan las fronteras y los campos preparados.
3. **Los contratos mandan.** El modelo de datos (`03`) y el contrato de API (`04`) son la fuente de verdad de las interfaces entre módulos. Ningún módulo cambia un contrato compartido sin actualizar primero el documento correspondiente.
4. **Trazabilidad obligatoria.** Cada pieza de funcionalidad referencia el requisito que cumple (notación `REQ §x.y`) y/o la sección de arquitectura (`ARQ §x`). No se implementa funcionalidad sin requisito que la respalde.
5. **No inventar infraestructura.** Los recursos de Azure los crea un humano siguiendo `Guias_Implementacion/Guia_Azure_Portal_Paso_a_Paso.md`. El código **consume** esos recursos por configuración; no los crea ni asume nombres distintos de los allí definidos.

---

## 1. Mapa de documentos

### 1.1 Especificaciones (`Especificaciones/`)

| # | Documento | Qué define | Responsable típico |
|---|---|---|---|
| 00 | `00_Indice_y_Guia_de_Uso.md` | Este documento. Orden de lectura y reglas globales. | Todos |
| 01 | `01_Convenciones_para_Agentes.md` | Estructura del repo, estándares de código, naming, Git, Definition of Done, manejo de ambigüedad. | Todos (leer primero) |
| 02 | `02_Arquitectura_y_Stack.md` | Stack y versiones fijadas, layout de la solución, fronteras de módulos, configuración. | Líder técnico / scaffolding |
| 03 | `03_Modelo_de_Datos_Cosmos.md` | Contenedores, partition keys, esquemas JSON de cada entidad, indexado, TTL, idempotencia. | Agente de datos |
| 04 | `04_Contrato_API_REST.md` | Todos los endpoints (`/api/auth/*`, `/api/admin/*`, `/webhook/whatsapp`), DTOs, errores, paginación. | Agente de API |
| 05 | `05_Backend_WhatsApp_y_Conversacion.md` | WhatsApp Gateway + Orquestador conversacional (máquina de estados, repregunta única). | Agente de conversación |
| 06 | `06_Backend_Identidad_y_Autenticacion.md` | Resolución de participante por número, OTP admin por WhatsApp, sesiones y roles. | Agente de identidad |
| 07 | `07_Backend_Campanas_y_Configuracion.md` | Campañas, mensajes iniciales, preguntas, tags, rúbricas, prompts, ConfigLLM (CRUD y versionado). | Agente de configuración |
| 08 | `08_Backend_Evaluacion_LLM.md` | Construcción de contexto, llamada al proveedor, contrato de salida JSON, fallback. | Agente de evaluación |
| 09 | `09_Backend_Markdown.md` | Compilación de artefactos Markdown, plantilla, persistencia y regeneración. | Agente de Markdown |
| 10 | `10_Seguridad_Guardrails_y_Observabilidad.md` | Rate limiting, límites de consumo, anti prompt-injection, logging y telemetría. | Agente transversal |
| 11 | `11_Frontend_Portal_Angular.md` | Portal admin (Angular 22): pantallas, rutas, estado, marca GHT, consumo de API. | Agente de frontend |
| 12 | `12_CICD_GitHub_Actions.md` | Pipelines de build/test/deploy a Azure con GitHub Actions (sin Bicep en el MVP). | Agente de DevOps |
| 13 | `13_Plan_de_Pruebas_y_Aceptacion.md` | Estrategia de pruebas, criterios de aceptación del MVP, checklist de release. | QA / todos |

### 1.2 Guías de implementación manual (`Guias_Implementacion/`)

Estas **no las ejecuta un agente**: las realiza una persona en consolas externas (portal de Azure, Meta Developers).

| Documento | Qué cubre |
|---|---|
| `Guia_Azure_Portal_Paso_a_Paso.md` | Creación de todos los recursos Azure desde el portal (última versión), paso a paso. |
| `Guia_WhatsApp_Cloud_API_Meta_Paso_a_Paso.md` | Alta de la app de WhatsApp Cloud API en Meta: número, plantillas, tokens, webhook. |

---

## 2. Orden de lectura recomendado

**Para arrancar el proyecto (scaffolding inicial):**
`01` → `02` → `03` → `04`. Con estos cuatro queda el esqueleto de la solución, la base de datos y los contratos.

**Para implementar un módulo concreto:** leer `01`, `02`, `03`, `04` y luego el documento del módulo (`05`–`11`) más `10` (seguridad/observabilidad aplica a todos).

**Para desplegar:** `12` (pipelines) requiere que un humano haya completado antes ambas guías de `Guias_Implementacion/`.

---

## 3. Diagrama de dependencias entre documentos

```
            01 Convenciones  ──────────────┐ (aplica a todos)
                                           │
            02 Arquitectura/Stack ─────────┤
                                           │
   ┌───────────────────────────────────────┤
   │                                        │
   ▼                                        ▼
03 Modelo de datos ───────────────▶ 04 Contrato API REST
   │                                        │
   │            ┌───────────────────────────┼───────────────┬───────────────┐
   ▼            ▼                           ▼                ▼               ▼
05 WhatsApp/  06 Identidad/            07 Campañas/      08 Evaluación   09 Markdown
   Conversación   Auth                    Config             LLM
   │            │                          │                │               │
   └────────────┴───────────┬──────────────┴────────────────┴───────────────┘
                            ▼
              10 Seguridad / Guardrails / Observabilidad  (transversal)
                            │
                            ▼
                 11 Frontend Angular ──▶ consume 04
                            │
                            ▼
              12 CI/CD GitHub Actions ──▶ requiere Guías Azure + Meta
                            │
                            ▼
              13 Plan de pruebas y aceptación
```

---

## 4. Convenciones de notación usadas en todo el set

- **`REQ §x`** → referencia a `Requeriments/GHT_banco_de_ideas_req_inicial.md`.
- **`ARQ §x`** → referencia a `Arquitectura/El_Tejido_Arquitectura_Tecnica_MVP.md`.
- **MUST / SHOULD / MAY** (RFC 2119) en su forma castellana: **DEBE / DEBERÍA / PUEDE**. "DEBE" es obligatorio para el MVP; "DEBERÍA" es recomendado; "PUEDE" es opcional.
- **`[MVP]`** marca algo dentro de alcance. **`[POST-MVP]`** marca una frontera preparada pero **no** implementada ahora.
- Identificadores de recursos Azure se escriben con el placeholder `<...>` cuando dependen del entorno (p. ej. `<cosmos-account-name>`), y su valor real se inyecta por configuración (ver `02 §6`).

---

## 5. Definición del producto en una frase

El Tejido captura ideas por **WhatsApp**, las **evalúa con un LLM** usando una **rúbrica en Markdown**, responde **retroalimentación breve** (máximo **una repregunta**), guarda **trazabilidad completa** y genera **artefactos Markdown** consultables desde un **portal administrativo** con login por **código OTP de WhatsApp**. Todo el MVP corre como un **monolito modular .NET 8** sobre **Azure App Service**, con **Cosmos DB serverless**, **Key Vault**, **Blob Storage** y **Application Insights**, y un **portal Angular 22**.

*Fin del documento.*
