# 06 — Backlog recomendado

## Criterio de prioridad

- **P1 — antes de habilitar campañas reales/LLM:** exposición de secreto o ausencia de controles operativos que impidan detectar/recuperar un incidente.
- **P2 — siguiente entrega:** incumplimientos confirmados de contrato, resiliencia y accesibilidad.
- **P3 — deuda planificada:** higiene de CI/tooling o facilidad de mantenimiento sin defecto funcional confirmado.

## Backlog consolidado

| Prioridad | Origen | Acción mínima | Criterio de aceptación | Dependencias / límite |
|---|---|---|---|---|
| P1 | `SEC-001` | Catálogo de proveedor/host y validación de URI HTTPS, host permitido y compatibilidad con `ApiKeyRef` al crear, editar, importar y activar `ConfigLLM`; complementar egress. | Un cambio solo de endpoint a host no aprobado falla antes de persistir y ningún handler capturador recibe una clave falsa hacia ese host. | Decidir hosts, puertos/rutas y referencias permitidas; verificar egress Azure. |
| P1 | `OPS-001` | Decidir y probar instrumentación Azure (Application Insights u OpenTelemetry), dependencias y alertas mínimas. | En staging se consulta una traza por `correlationId`, latencia/error de dependencia y métrica de tokens sin PII; alerta de prueba llega al canal acordado. | Requiere acceso Azure y decisión de operación. |
| P1 | `PER-001` | Probar concurrencia y durabilidad de conversaciones/envíos antes de adoptar ETag, transacción o cola durable. | Dos solicitudes coordinadas y un reinicio verifican el invariante acordado; la solución se elige a partir del resultado. | Requiere Cosmos/worker de prueba; no escoger solución sin reproducir necesidad. |
| P2 | `SEC-002` | Límite de bytes del webhook antes de `CopyToAsync`, incluso sin `Content-Length`, con rechazo controlado. | Cuerpo excedido devuelve `413`; no se invoca firma ni cola; siguen `200`, `401` y `429` en sus casos. | Confirmar tamaño máximo WhatsApp y límites App Service/proxy/WAF. |
| P2 | `API-001` | Centralizar retornos de error directos en el escritor uniforme, manteniendo 404 neutro de readiness. | 401/403/404 y job inexistente contienen el esquema `error` y el mismo `X-Correlation-Id`; no se filtran detalles. | Coordinar con cliente/Meta si alguno depende de cuerpo vacío. |
| P2 | `UXA11Y-001` | Etiquetar checkboxes de envíos, campos de tags y selector CSV con texto visible o nombre ARIA contextual. | Un lector de pantalla anuncia propósito y destinatario/acción de cada control; placeholders no son la única etiqueta. | Prueba manual NVDA/VoiceOver recomendada. |
| P2 | `UXA11Y-002` | Centralizar mensajes dinámicos con `role="alert"`/`status` y asociar errores a campos cuando corresponda. | Login y pantallas de datos anuncian éxito/error sin mover foco de forma inesperada. | Definir tono de anuncio y evitar duplicar toast + alerta. |
| P2 | `UXA11Y-003` | Completar pestañas con patrón WAI-ARIA y prueba de teclado, o retirar el tablist si se mantienen botones independientes. | Pestaña activa/panel se anuncian y flechas/Home/End funcionan conforme al patrón elegido. | Mantener el flujo y diseño visual actuales. |
| P2 | Observación purga | Configurar `Seguridad:PermitirReinicioDatos=false` fuera de QA y cambiar el fallback a fail-closed cuando se autorice código. | Una instalación sin flag no expone purga masiva; habilitación temporal queda auditada. | No fue vulnerabilidad final: requiere Admin + CSRF. |
| P3 | Tooling frontend | Proveer Node 22.22.3, 24.15.0 o superior en el entorno de validación. | `ng test --watch=false` y build de producción se ejecutan y quedan registrados. | Bloqueo del entorno actual; no es defecto de fuente demostrado. |
| P3 | `DEP-001` | Actualizar/eliminar el padre de prueba que resuelve `System.Net.Http` y `System.Text.RegularExpressions` 4.3.0. | `dotnet list ... --vulnerable --include-transitive` no reporta esos advisories, o existe aceptación temporal documentada. | Alcance de test/CI, no producción según el escaneo actual. |
| P3 | `CAL-001` | Extraer gradualmente políticas del orquestador con pruebas dirigidas. | La fachada conserva contrato y cada colaborador tiene pruebas focalizadas. | Mantener flujo y flags existentes. |
| P3 | `CAL-002` | Separar flujos de campañas en componentes hijos incrementalmente. | Ruta/API intactas y pruebas cubren cada flujo extraído. | No mezclar con cambio funcional. |

## Trazabilidad de remediación iniciada

| Hallazgo | Iniciativa | Estado documental | Dependencia de secuencia |
|---|---|---|---|
| `CAL-001` | `P-15_Refactor_Orquestador_Conversacional.md` | TODO — próximo ejecutable | Ninguna externa. |
| `CAL-002` | `P-16_Refactor_Pagina_Campanias.md` | TODO | Después de P-15. |
| `API-001` | `P-17_Errores_API_Uniformes.md` | TODO | Después de P-16. |
| `UXA11Y-001` | `P-18_Controles_Con_Nombre_Accesible.md` | TODO | Después de P-17. |
| `UXA11Y-002` | `P-19_Estados_Dinamicos_Accesibles.md` | TODO | Después de P-18. |
| `UXA11Y-003` | `P-20_Pestanas_Accesibles_Campanias.md` | TODO | Después de P-19 y de la estructura P-16. |

Las iniciativas se incorporaron al plan de ejecución el 2026-07-24. Permanecen abiertas hasta que una implementación verificada actualice su estado y la evidencia de auditoría.
