# 01 — Plan de auditoría

## Objetivo y método

Auditar el repositorio sin modificar comportamiento funcional. Cada conclusión se registrará con evidencia reproducible, regla aplicable, fuente, severidad, confianza y corrección mínima sugerida. Los criterios sin evidencia suficiente se clasificarán como `desconocido`, `requiere_revision_humana` o `not_applicable`; no se convertirán preferencias en defectos.

## Dimensiones, criterios y fuentes

| Dimensión | Criterios aplicables | Fuentes principales | Revisión manual | Comandos/herramientas previstos |
|---|---|---|---|---|
| Arquitectura | Direcciones de referencia, responsabilidades, acoplamiento, excepciones documentadas | `02`, ARQ, `*.csproj`, composición en `Program.cs` | Capas, puertos/adaptadores y módulos | `dotnet list reference`; búsquedas de dependencias |
| Calidad y mantenibilidad | Analizadores, errores, duplicación, cohesión, testabilidad, convenciones | `Directory.Build.props`, `01`, código y pruebas | Casos de uso, manejo de errores y zonas de alta complejidad | build, test, format, búsquedas focalizadas |
| Seguridad | Autenticación, autorización por recurso, secretos, validación, inyección, logs, dependencias | `04`, `06`, `10`, OWASP ASVS/API Security | Endpoints, middleware, almacenamiento y flujos de secretos | `dotnet list package --vulnerable`, revisión de rutas y configuración |
| Persistencia | Integridad, idempotencia, particiones, índices, concurrencia, carga y compatibilidad | `03`, Infraestructura, pruebas | Mapeos Cosmos y repositorios | pruebas focalizadas y revisión de consultas |
| API | Rutas, verbos, códigos, errores, autorización, compatibilidad y contratos | `04`, endpoints y pruebas de integración | Mapeo contrato → implementación | inventario de `Map*`, pruebas de integración |
| Operabilidad | Logs estructurados, correlación, health/readiness, reintentos, timeout, cancelación y recuperación | `10`, `12`, `Program.cs`, workers | Flujos asíncronos e integraciones | build/test, revisión de configuración y workflows |
| UX y accesibilidad | Estados de interfaz, formularios, foco, semántica, etiquetas, teclado, WCAG 2.2 AA aplicable | `11`, componentes y pruebas web | Rutas, plantillas, estilos y guards | lint, pruebas/build frontend y revisión estática |

## Secuencia

1. **Reconocimiento — completado:** contexto, inventario y plan; compilación, pruebas, formato y lint básicos.
2. **Arquitectura y calidad — completado:** se contrastaron referencias, dependencias de espacio de nombres, composition root, concentraciones de responsabilidades y pruebas relevantes. Se documentaron dos riesgos de mantenibilidad y una validación arquitectónica descartada.
3. **Seguridad y persistencia — completado:** se revisaron autenticación, autorización, secretos, ConfigLLM, webhook, Cosmos, idempotencia y rutas administrativas mediante inventario/ranking, recibos de lectura, validación y análisis de ruta de ataque. Dos hallazgos sobrevivieron la calibración final; las dudas de despliegue y concurrencia quedaron explícitamente diferidas.
4. **API y operabilidad — completado:** se contrastaron rutas, fallback SPA, códigos/errores, correlación, salud/readiness, colas/workers, telemetría, CI/CD y dependencias. Se confirmó una desviación de error uniforme; Azure/alertas y telemetría efectiva quedan para comprobación humana.
5. **UX y accesibilidad — completado:** se revisaron rutas, shell, formularios, tablas, estados dinámicos, responsive, controles y pruebas. Se confirmaron tres brechas WCAG; las pruebas/build Angular quedan bloqueados por versión de Node insuficiente en el entorno, no por un error de fuente demostrado.
6. **Consolidación — completado:** se deduplicaron los hallazgos, se agruparon por severidad/clasificación y se reordenó el backlog por decisión de lanzamiento. El cierre conserva límites de evidencia, excepciones operativas y validaciones externas pendientes.

Antes de cada dimensión se releerán `00_contexto_auditoria.md` y este plan; se actualizarán `03_hallazgos.md` y `04_evidencias.md` antes de iniciar la siguiente.

## Fuera de alcance y limitaciones iniciales

- No se modifica código productivo, contratos, migraciones, infraestructura, secretos ni configuración desplegada.
- No se realizan acciones contra recursos externos ni pruebas que requieran credenciales reales o generen tráfico a WhatsApp/LLM.
- No se puede confirmar configuración efectiva de Azure/GitHub ni ejecutar una prueba E2E real sin acceso y autorización explícitos.
- La auditoría de dependencias será una instantánea del feed accesible durante la ejecución; no sustituye monitoreo continuo.
