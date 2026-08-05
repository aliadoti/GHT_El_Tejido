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
8. Consulta una fila por idea consolidada, con estado, calificación, explicación, Markdown e historial
   de aportes/versiones; los resultados históricos siguen disponibles.
9. Configura “Permitir nuevas ideas después de finalizar” al crear/editar una campaña y distingue ese
   permiso del estado activa/cerrada.

## 3. Criterios de aceptación — Participante (`REQ §33.2`)
1. Recibe el mensaje inicial por WhatsApp tras el envío del admin.
2. Al responder, el sistema lo reconoce por su número normalizado.
3. Un no matriculado recibe mensaje neutral de no-acceso; uno activo y asociado continúa.
4. Su aporte se guarda; el sistema parafrasea la idea completa y pide confirmación.
5. Puede corregir la paráfrasis sin perder lo dicho anteriormente.
6. Solo la versión consolidada confirmada se evalúa con el LLM y la rúbrica configurada.
7. Recibe retroalimentación corta y útil y trabaja una idea a la vez.
8. Puede volver a una idea anterior mientras la campaña esté activa.
9. La interacción cierra dejando la idea madura, pendiente o rechazada; una madura queda pendiente de
   curaduría y no se publica automáticamente.
10. En campaña continua puede volver después y crear otra idea independiente.
11. Con varias campañas o preguntas elegibles elige de una lista y su aporte original se procesa sin
    escribirlo otra vez.
12. Durante el coaching sus respuestas continúan en la idea activa sin repetir la selección.
13. Durante una mejora puede pedir con palabras naturales dejar la idea o terminar por ahora; el
    sistema no evalúa esa orden como parte de su idea y aclara con opciones cuando el alcance no es
    seguro.

## 4. Criterios de aceptación — Sistema y Seguridad (`REQ §33.3`, `§36.6`)
1. Guarda historial, mensajes iniciales enviados, estado de envío, aportes, ideas consolidadas,
   versiones, confirmaciones y evaluaciones.
2. Guarda **prompt+versión, rúbrica+versión, config LLM** usadas (snapshots reproducibles).
3. Genera Markdown, permite consultarlo y **regenerarlo** desde datos operativos.
4. Permite cambiar configuración sin tocar código.
5. Controla el máximo configurado de repreguntas por idea; una corrección no se evalúa hasta
   confirmarse.
6. Aplica límites de seguridad (longitud, cupos, rate limit, intentos).
7. Verifica la firma del webhook; idempotencia ante reintentos de Meta.
8. No filtra secretos en logs, telemetría ni Markdown; auth neutral; anti prompt-injection efectivo.
9. Mantiene separación entre configuración, conversación, evaluación, envío, seguridad, persistencia y Markdown.
10. P-26 revalida autorización, evita duplicar ciclos/aportes, conserva selecciones vencidas como
    auditoría y aplica cupos móviles de 24 h sin reiniciar el presupuesto de campaña.
11. P-27 trata la clasificación LLM como candidato no confiable: solo el servidor valida y ejecuta la
    transición; fallos, cupo agotado o salida inválida no cierran ideas ni conversaciones.

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
9. Cada usuario responde; verificar paráfrasis completa y pedir confirmación.
10. En al menos un caso, responder después solo con un dato faltante y comprobar que la nueva
    paráfrasis mantiene lo anterior; confirmar y verificar que esa versión completa es la evaluada.
11. Probar una idea madura, una pendiente, una rechazada y la reapertura de “la anterior”.
12. Verificar cierre, Markdown canónico por idea y una sola fila por idea en Resultados.
13. Verificar trazabilidad (aportes, versiones, confirmaciones, snapshots) y ausencia de secretos en
    artefactos/logs.
14. Verificar que confirmación, mejora y transición son un solo acto natural; que no se revelan
    puntajes al participante; y que Markdown muestra umbral/origen y nota `X de Y puntos`.
15. En simulación, activar participación continua, cerrar una idea y enviar otra: comprobar
    conversación, `ideaId`, evaluación y Markdown diferentes.
16. Asociar el mismo usuario a dos campañas activas, enviar primero el aporte, seleccionar campaña y
    pregunta y comprobar que el aporte se procesa una sola vez.
17. Responder al coaching y verificar que no reaparecen los menús; pedir explícitamente otra campaña y
    comprobar el cambio sin cerrar la idea suspendida.
18. Apagar el flag durante una idea y verificar que termina pero no abre otra; cerrar una campaña y
    verificar el corte inmediato.
19. En una mejora, enviar “quiero parar aquí”, “stop now” y “quiero pasar a otra idea”; comprobar que
    no se consolidan ni evalúan y que cada transición ocurre una sola vez según su alcance.
20. Enviar “hay que parar la máquina durante el mantenimiento” y comprobar que sigue como aporte;
    provocar una intención ambigua y resolver el menú 1/2/3 sin consumir una repregunta.
21. Apagar ambos gates P-27 y simular timeout, JSON inválido y cupo agotado: los alias inequívocos
    siguen funcionando, mientras la ruta flexible degrada sin cerrar ni perder la idea.
22. Con P-30 encendida, sembrar ideas maduras, pendientes y rechazadas en ciclos distintos; pedir
    retomar, elegir por número y por resumen exacto, aportar una mejora y comprobar que se re-evalúa el
    mismo `ideaId`. Repetir con el flag apagado y confirmar que solo queda la reapertura reciente.

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
| §9/§20/§21/§22 Consolidación progresiva I-19 | I-19, 03 §3.8.1–2, 05 §4.4.2, 08 §2.2 | Unit (versiones/estados/intenciones) + integración (confirmar/evaluar/reabrir) + E2E §5.9–13 |
| §9/§20/§21/§22 Redacción fluida I-20 | I-20, 03 §3.3, 05 §4.4–§4.5, 08, 09 | Unit (JSON/guardrails/fallback/formato) + integración (un acto por turno) + E2E §5.9–14 |
| Participación continua y enrutamiento P-26 | P-26, 03 §3.3/§3.6.1, 05 §4.4.3, 06 §3 | Unit (elegibilidad/selección/ciclos/ventana) + integración (webhook y Cosmos) + E2E §5.15–18 |
| Intenciones de control flexibles P-27 | P-27, 03 §3.3/§3.6, 05 §4.4.4, 08 §2.3, 10 §2/§6 | Unit (enum/política/falsos positivos/fallback) + integración (Cosmos/API/orquestador) + E2E §5.19–21 |
| Retomar ideas históricas P-30 | P-30, 03 §3.6.1/§3.8.1, 05 §4.4.5, 10 §6 | Unit (consulta/0-1-N/selección/aislamiento/rollback) + Cosmos + E2E §5.22 |
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
- [ ] I-19: ninguna evaluación vigente usa solo el último complemento; ideas maduras quedan
  pendientes de curaduría.
- [ ] I-20: no se concatenan actos, la redacción no revela rúbrica y cada Markdown expone umbral,
  origen y calificación de la versión exacta.
- [ ] `SUPUESTOS.md` revisado (decisiones de ambigüedad documentadas).

---

## 8. Riesgos a vigilar en pruebas (de `ARQ §16`)
- Aprobación/tardanza de plantillas WhatsApp → probar temprano con plantillas reales.
- Ventana de 24h vencida antes de la repregunta → probar el camino de plantilla de repregunta.
- Pérdida de jobs en cola in-memory ante reinicio (`02 §5`) → verificar re-disparo de envío por estado de participante.
- Consistencia de la evaluación LLM → revisión humana de calificaciones en el MVP (`REQ §8.3`).
- Fidelidad de consolidación I-19 → probar correcciones, contradicciones, reaperturas, mezcla de
  complemento+nueva idea y fallback; una propuesta incorrecta nunca debe madurar sin confirmación.

*Fin del documento.*
