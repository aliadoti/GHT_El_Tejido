# 13 — Plan de Pruebas y Criterios de Aceptación del MVP

**Propósito:** definir cómo se verifica que el MVP cumple. Consolida los criterios de aceptación de `REQ §33` y los específicos de cada módulo, y da la matriz de trazabilidad.

---

## 1. Estrategia de pruebas

| Nivel | Alcance | Herramientas | Responsable |
|---|---|---|---|
| **Unitarias** | Lógica de dominio: normalización de números, máquina de estados conversacional, validación de salida LLM, parseo de rúbrica, guardrails, renderizado Markdown. | xUnit + FluentAssertions + NSubstitute. | Cada agente de módulo. |
| **Integración** | Flujos con I/O: persistencia Cosmos (emulador), webhook end-to-end (con `IWhatsAppClient` y `ILlmClient` mockeados), auth OTP, envío. | xUnit + WebApplicationFactory + Cosmos emulator/Azurite. | Agente de módulo + QA. |
| **Contrato** | Que los DTOs de la API coinciden con `04`; que el frontend consume las formas correctas. | Pruebas de API + tipos compartidos. | API + frontend. |
| **Frontend** | Componentes y servicios Angular. | Test runner del CLI v22 (headless). | Agente de frontend. |
| **E2E manual (MVP)** | El recorrido completo con 5 usuarios reales por WhatsApp. | Checklist §5. | Humano + QA. |
| **Seguridad** | Firma webhook, rate limits, no fuga de secretos, neutralidad de auth, anti-injection. | Pruebas dirigidas §4. | Agente transversal. |

Las llamadas reales a WhatsApp y al LLM se **mockean** en CI; las pruebas E2E reales se hacen en el entorno desplegado con la cuenta de WhatsApp de prueba.

---

## 2. Criterios de aceptación — Administrador (`REQ §33.1`)
1. Ingresa su número en el login con instrucciones de normalización visibles.
2. Recibe un código por WhatsApp y accede con un código válido; uno inválido/vencido es rechazado (mensaje neutral).
3. Crea/edita usuarios; asigna área, empresa, tags.
4. Crea/edita campañas; asocia usuarios; configura mensajes iniciales y preguntas.
5. Envía mensajes iniciales desde el portal; reenvía a quienes no respondieron; reintenta fallidos.
6. Carga/edita una rúbrica Markdown (versionada); edita y **aprueba** prompts.
7. Configura proveedor/modelo LLM y guarda la API key de forma segura (enmascarada; solo `apiKeyRef` en BD).
8. Consulta respuestas, calificaciones, explicaciones y Markdown; filtra por campaña, usuario, área, empresa, tag, pregunta, calificación, etc.

## 3. Criterios de aceptación — Participante (`REQ §33.2`)
1. Recibe el mensaje inicial por WhatsApp tras el envío del admin.
2. Al responder, el sistema lo reconoce por su número normalizado.
3. Un no matriculado recibe mensaje neutral de no-acceso; uno activo y asociado continúa.
4. La respuesta se guarda y se evalúa con el LLM y la rúbrica configurada.
5. Recibe retroalimentación corta y útil; como máximo **una** repregunta.
6. La interacción cierra con un mensaje de agradecimiento.

## 4. Criterios de aceptación — Sistema y Seguridad (`REQ §33.3`, `§36.6`)
1. Guarda historial, mensajes iniciales enviados, estado de envío por participante, respuestas, evaluaciones.
2. Guarda **prompt+versión, rúbrica+versión, config LLM** usadas (snapshots reproducibles).
3. Genera Markdown, permite consultarlo y **regenerarlo** desde datos operativos.
4. Permite cambiar configuración sin tocar código.
5. Controla **máximo una repregunta** (≤2 evaluaciones por hilo).
6. Aplica límites de seguridad (longitud, cupos, rate limit, intentos).
7. Verifica la firma del webhook; idempotencia ante reintentos de Meta.
8. No filtra secretos en logs, telemetría ni Markdown; auth neutral; anti prompt-injection efectivo.
9. Mantiene separación entre configuración, conversación, evaluación, envío, seguridad, persistencia y Markdown.

---

## 5. Checklist E2E del MVP (recorrido recomendado, `REQ §35.1`)
1. Registrar 5 usuarios de prueba + 1 administrador (números normalizados reales).
2. Crear una campaña; asociar los 5 usuarios.
3. Configurar mensaje inicial (`Hola {{nombre}}, ...`) como plantilla aprobada.
4. Configurar 3 preguntas (ingresos, costos, productividad).
5. Cargar rúbrica Markdown; configurar y **aprobar** prompts de evaluación, retro y compilación.
6. Configurar proveedor/modelo LLM y API key.
7. Login del admin con código por WhatsApp.
8. Enviar mensajes iniciales; verificar recepción en los 5 teléfonos.
9. Cada usuario responde; verificar evaluación, retroalimentación breve y (si aplica) una repregunta.
10. Verificar cierre con agradecimiento.
11. Verificar generación de Markdown y consulta/filtros en el portal.
12. Verificar trazabilidad (versiones, snapshots) y ausencia de secretos en artefactos/logs.

---

## 6. Matriz de trazabilidad (requisito → spec → prueba)

| Requisito (REQ) | Documento spec | Prueba |
|---|---|---|
| §10 Auth admin OTP | 06, 04 §4 | Unit (hash/normalización) + integración (request/verify) + §2.2 |
| §12, §26.3 Identidad/matrícula | 06 §3 | Unit (resolución) + integración (rechazo neutral) + §3.3 |
| §11, §14 Campañas/participantes | 07 §2, 04 §5.3 | Integración CRUD + §2.4 |
| §15, §26.2 Mensajes iniciales/envío | 05 §2, 07 §2.3 | Integración envío (mock WA) + §2.5, §3.1 |
| §16 Preguntas | 07 §2.4 | Integración CRUD |
| §17 Rúbricas | 07 §3 | Unit (parseo/versionado) + §2.6 |
| §18 Prompts | 07 §4 | Unit (versionado) + integración (aprobación) + §2.6 |
| §19 Config/seguridad LLM | 07 §5, 10 §4 | Integración (key en Key Vault, no en BD) + §4 |
| §20, §25.3, §26.5 Evaluación LLM | 08 | Unit (validación salida, fallback) + §3.4, §4 |
| §21, §26.6 Retro y repregunta única | 05 §4 | Unit (máquina de estados) + §3.5 |
| §22, §26.7 Markdown | 09 | Unit (render) + integración (regenerar) + §4.3 |
| §25 Guardrails/abuso | 10 §2 | Unit + integración límites + §4.6 |
| §30 Trazabilidad | 10 §6 | Integración (snapshots, logs) + §4.1–2 |
| §27 Portal | 11, 04 §5 | Frontend + §2, §3 |
| §32 Marca GHT | 11 §5 | Revisión visual |
| §31.8 Mantenibilidad/separación | 01 §2, 02 §3 | Revisión de arquitectura + §4.9 |

---

## 7. Checklist de release (Definition of Release)
- [ ] Todos los criterios de §2–§4 verificados.
- [ ] CI verde en `main` (build + test + lint).
- [ ] Despliegue exitoso y `/health` OK (`12 §3.2`).
- [ ] Recursos Azure y app de WhatsApp configurados (guías completas).
- [ ] Plantillas de WhatsApp aprobadas por Meta (mensaje inicial, autenticación, repregunta).
- [ ] Secretos en Key Vault; ninguno en repo/logs.
- [ ] E2E §5 ejecutado con 5 usuarios reales.
- [ ] `SUPUESTOS.md` revisado (decisiones de ambigüedad documentadas).

---

## 8. Riesgos a vigilar en pruebas (de `ARQ §16`)
- Aprobación/tardanza de plantillas WhatsApp → probar temprano con plantillas reales.
- Ventana de 24h vencida antes de la repregunta → probar el camino de plantilla de repregunta.
- Pérdida de jobs en cola in-memory ante reinicio (`02 §5`) → verificar re-disparo de envío por estado de participante.
- Consistencia de la evaluación LLM → revisión humana de calificaciones en el MVP (`REQ §8.3`).

*Fin del documento.*
