# REQ-006 · Rúbricas, prompts versionados y configuración LLM

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT (alcance base MVP) · 2026-06-12 |
| **Estimación** | Alcance base del MVP (no facturable por T&M) |

## Qué se necesita
Permitir al admin cargar/editar la rúbrica Markdown versionada, editar y aprobar los prompts, y configurar el proveedor/modelo LLM con su API key de forma segura.

## Alcance
- Incluye: rúbrica Markdown versionada; prompts versionados con aprobación; configuración de proveedor/modelo LLM; API key enmascarada (solo `apiKeyRef` en BD, secreto en Key Vault).
- No incluye: capa vectorial / embeddings (post-MVP).

## Criterios de aceptación
- [x] Carga/edita una rúbrica Markdown versionada; edita y aprueba prompts.
- [x] Configura proveedor/modelo LLM y guarda la API key enmascarada (solo `apiKeyRef` en BD).
- [x] La configuración cambia sin tocar código.

## Aprobación
Entregado como parte del MVP base · Ref. spec: `Especificaciones/base/07 §3-§5`, `10 §4`
