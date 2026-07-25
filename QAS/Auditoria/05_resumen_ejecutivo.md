# 05 — Resumen ejecutivo

## Estado general observado

La auditoría técnica documental está cerrada: se revisaron arquitectura, calidad, seguridad, persistencia, API, operabilidad, UX y accesibilidad. El diseño de capas se respeta en el alcance estático priorizado y la línea base backend es reproducible. No se identificaron hallazgos confirmados críticos o altos; eso no certifica seguridad, cumplimiento completo ni estado de producción.

## Controles ejecutados

- Backend: build Release con cero advertencias/errores, 423 pruebas sin calibración y formato .NET correctos.
- API/operación: 10 pruebas focalizadas de salud, readiness y modelo de errores aprobadas; rutas, middleware, workers, CI/CD y dependencias revisados.
- Seguridad: escaneo especializado con 474 archivos inventariados, 263 rutas runtime priorizadas y dos hallazgos reportables; análisis NuGet sin advisories en proyectos productivos.
- Frontend: Prettier y typecheck directo correctos; rutas, formularios, estados, responsive y semántica revisados estáticamente.

## Controles no ejecutados y causa

- `ng test` y build Angular no iniciaron porque los runtimes disponibles eran Node 22.17.0/24.14.0 y Angular 22 requiere 22.22.3, 24.15.0 o superior.
- No hubo pruebas contra Azure, Key Vault, Cosmos, WhatsApp, LLM, GitHub Actions ni App Service, ni E2E con usuarios reales, teclado/zoom o lector de pantalla.

## Hallazgos consolidados

| Grupo | Hallazgos | Lectura ejecutiva |
|---|---|---|
| Confirmados medios (7) | `SEC-001`, `API-001`, `CAL-001`, `CAL-002`, `UXA11Y-001`, `UXA11Y-002`, `UXA11Y-003` | La prioridad es impedir la exfiltración de claves LLM, restaurar el contrato de errores y eliminar barreras de accesibilidad; después, reducir concentración de mantenimiento. |
| Confirmado bajo (1) | `SEC-002` | El webhook debe limitar bytes antes de copiar y validar HMAC. |
| Revisión humana media (2) | `PER-001`, `OPS-001` | Faltan evidencia de concurrencia/durabilidad y prueba de telemetría/alertas en Azure. |
| Revisión humana baja (1) | `DEP-001` | Advisories transitivos limitados a proyectos de prueba/CI; no a los proyectos productivos según el escaneo actual. |
| Descartado informativo (1) | `ARQ-001` | No se observó ruptura de la dirección de dependencias declarada. |

No hay riesgo crítico confirmado en el repositorio local revisado. El riesgo de mayor impacto antes de una campaña real es `SEC-001`: una edición administrativa puede asociar una clave LLM existente con un host no aprobado.

## Excepciones y desconocidos

- Las colas y jobs in-process son una decisión explícita del MVP; su comportamiento bajo reinicio/concurrencia sigue en `PER-001`, no se afirma pérdida confirmada.
- La purga masiva habilitada por defecto es una observación operativa con Admin+CSRF, no una vulnerabilidad final; requiere decisión de operación fuera de QA.
- Egress, WAF, Key Vault, Application Insights, alertas y configuración efectiva de App Service no son observables desde este checkout.

## Recomendaciones priorizadas

1. Antes de habilitar LLM en producción, resolver `SEC-001` con catálogo proveedor-host, HTTPS y compatibilidad de clave, más egress restringido.
2. Antes de una campaña real, verificar `OPS-001` y `PER-001`: trazabilidad/alertas en staging y comportamiento ante concurrencia/reinicio.
3. En la siguiente entrega, resolver `API-001`, `SEC-002` y las tres barreras `UXA11Y-*`; restablecer el runtime Node compatible para ejecutar la suite Angular.
4. Programar deuda de mantenibilidad y dependencias de prueba sin mezclarla con cambios funcionales.

Las especificaciones de corrección para `CAL-001`, `CAL-002`, `API-001` y `UXA11Y-001` a `UXA11Y-003` quedaron registradas como `P-15` a `P-20` el 2026-07-24. Están planificadas para iniciar por P-15; este registro no significa que los hallazgos estén corregidos aún.

Las evidencias, alcance y limitaciones detalladas están en `03_hallazgos.md` y `04_evidencias.md`; el orden implementable está en `06_backlog_recomendado.md`.
