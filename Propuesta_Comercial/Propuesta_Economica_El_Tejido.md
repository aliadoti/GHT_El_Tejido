# Propuesta Económica — El Tejido

**Sistema conversacional de memoria institucional por WhatsApp, LLM y Markdown**

| | |
|---|---|
| **Cliente** | GHT |
| **Proveedor** | GTA — Global Technology Ally |
| **Fase** | Hito 1 · MVP de validación técnica |
| **Fecha** | Junio de 2026 |
| **Validez** | 30 días |

---

## 1. Contexto y objetivo

El conocimiento más valioso de GHT vive en la cabeza de sus líderes. Se pierde con la rotación, el retiro y la falta de un momento para escribirlo. Las wikis y bases de datos anteriores fueron abandonadas.

**El Tejido** convierte ese conocimiento en una memoria institucional viva: atribuida, calificada e interconectada. El sistema inicia conversaciones por WhatsApp, captura ideas y aprendizajes, los evalúa con un LLM usando rúbricas configurables y los compila en archivos Markdown auditables y versionables.

El objetivo de esta fase es **validar el flujo técnico completo con un grupo de ~5 usuarios**, como base para escalar luego a la convención (~120 contribuyentes) y a la curaduría del corpus.

---

## 2. Qué entrega el MVP

Al cierre del MVP, GHT contará con un sistema en operación que demuestra el ciclo completo:

1. **Captura por WhatsApp.** El administrador envía mensajes iniciales configurables a participantes matriculados. El sistema reconoce a cada participante por su número normalizado.
2. **Evaluación con LLM.** Cada respuesta se califica contra una rúbrica Markdown parametrizable, con criterios, pesos y escala configurables sin tocar código.
3. **Retroalimentación conversacional.** El participante recibe una respuesta breve, humana y útil, con máximo una repregunta.
4. **Memoria institucional.** Cada aporte se compila en Markdown con autoría, calificación, trazabilidad completa y metadatos para indexación semántica futura.
5. **Portal administrativo.** Aplicación web con marca GHT para gestionar campañas, participantes, preguntas, rúbricas, prompts, configuración del LLM y consulta de resultados. Acceso con código enviado por WhatsApp.

### Incluido en el alcance

- Integración con WhatsApp Cloud API (envío, recepción, reenvíos y plantillas).
- Autenticación administrativa por código de un solo uso enviado a WhatsApp.
- Gestión de usuarios, tags (área y empresa), campañas y participantes por campaña.
- Mensajes iniciales y preguntas configurables con variables dinámicas.
- Rúbricas y prompts versionados, editables desde el portal.
- Configuración del proveedor/modelo LLM con credenciales en bóveda segura.
- Evaluación estructurada, retroalimentación y una repregunta máxima.
- Generación y consulta de artefactos Markdown regenerables.
- Controles de seguridad: límites de consumo, protección contra prompt injection y registro de eventos.
- Persistencia documental (Azure Cosmos DB serverless) y CI/CD con GitHub Actions.
- Prueba de extremo a extremo con ~5 usuarios reales.

### Excluido del alcance (fases futuras)

Dashboard avanzado, gamificación, exportaciones a Office, base vectorial productiva, chat semántico sobre el corpus, integraciones corporativas (Entra ID), envíos recurrentes programados y curaduría colaborativa. Estos elementos pertenecen a los Hitos 2 y 3 del roadmap.

---

## 3. Modelo de trabajo

El trabajo se ejecuta por requerimientos aprobados, con ciclos visibles y controlados:

| Paso | Descripción |
|---|---|
| **Reunión de entendimiento** | Los equipos GHT y GTA alinean objetivos y criterios de éxito del ciclo. |
| **Documento de requerimiento** | GTA formaliza objetivo, supuestos, criterios de aceptación y esfuerzo. Aprobado, se vuelve la referencia del alcance. |
| **Desarrollo iterativo** | GTA implementa en ciclos "hacer – probar – ajustar". |
| **Pruebas y validación** | GHT valida contra los criterios de aceptación definidos. |
| **Despliegue** | Las funcionalidades validadas pasan al ambiente del MVP. |
| **Informe de avances** | GTA presenta avance, decisiones y riesgos con periodicidad semanal. |

---

## 4. Cronograma

Duración estimada: **8 semanas** desde el kickoff.

| Semanas | Foco |
|---|---|
| 1–2 | Fundaciones: arquitectura, Cosmos DB, integración WhatsApp, autenticación por código. |
| 3–4 | Campañas, participantes, tags, mensajes iniciales y preguntas configurables. |
| 5–6 | Evaluación LLM, rúbricas, prompts, retroalimentación y repregunta. |
| 7 | Generación Markdown, consultas administrativas y seguridad. |
| 8 | Prueba de extremo a extremo con usuarios reales, ajustes y entrega. |

---

## 5. Inversión

### 5.1 Precio fijo del MVP (Hito 1)

| Concepto | Valor |
|---|---|
| **MVP El Tejido — alcance completo de la sección 2** | **USD 32.500** |

Forma de pago propuesta:

| Hito de pago | % | Valor |
|---|---|---|
| Firma y kickoff | 30 % | USD 9.750 |
| Demo funcional del flujo conversacional (semana 5) | 40 % | USD 13.000 |
| Aceptación del MVP | 30 % | USD 9.750 |

El precio es cerrado para el alcance definido. Cambios de alcance se gestionan como requerimientos adicionales, estimados y aprobados de forma independiente antes de ejecutarse.

### 5.2 Infraestructura y consumo

- Durante el MVP, la solución opera en **infraestructura de GTA**. Los costos de Azure, WhatsApp Cloud API y consumo del LLM se facturan a GHT **con los soportes correspondientes**, sin margen. Estimado referencial: **USD 150–300 por mes** dada la escala del piloto.
- Para producción (Hito 2 en adelante), la solución se despliega en **infraestructura de GHT**, que asume su operación y costo directo.

### 5.3 Evolución continua posterior al MVP

Aprobado el MVP, GTA ofrece un modelo de capacidad dedicada para evolución y soporte, igual al que GHT ya conoce:

| Capacidad mensual | Valor de referencia |
|---|---|
| Equipo dedicado — 160 horas/mes | USD 19.500 |

- Se factura solo el esfuerzo aprobado, trabajado y entregado; si el esfuerzo es menor, la facturación es proporcional (ej.: 2 semanas = USD 9.750).
- Las prioridades se revisan cada ciclo con GHT mediante trade-offs explícitos.
- Este modelo cubriría el Hito 2 (convención, ~120 contribuyentes) y el Hito 3 (curaduría), cuyo alcance se estimará al cierre del MVP.

---

## 6. Compromisos de las partes

**GTA**

- Equipo técnico dedicado y conocimiento acumulado del negocio.
- Gestión formal de requerimientos y reporte semanal de avances.
- Entrega del MVP conforme a los criterios de aceptación.

**GHT**

- Lista de ~5 usuarios de prueba con números de WhatsApp y administradores designados.
- Rúbrica inicial en Markdown (criterios, pesos y escala) y aprobación de prompts.
- Acceso o credenciales de WhatsApp Business / Meta.
- Aprobación oportuna de requerimientos y entregables.
- Pagos según las condiciones acordadas.

---

## 7. Roadmap posterior (sin precio en esta propuesta)

| Hito | Objetivo |
|---|---|
| **Hito 2 — Convención** | Escalar a ~120 contribuyentes: monitoreo de participación, reenvíos controlados, métricas de captura y mayor robustez operativa. |
| **Hito 3 — Curaduría** | Consejo de líderes senior canoniza el mejor conocimiento: estados de curaduría, páginas de entidad, índices por capítulo y base vectorial para consulta semántica. |

---

## 8. Próximos pasos

1. Aprobación de esta propuesta.
2. Firma y pago del anticipo.
3. Kickoff y entrega de insumos por parte de GHT.
4. Inicio de la semana 1 del cronograma.

---

*Propiedad de GTA — Global Technology Ally · Información confidencial.*
*Las cifras en USD no incluyen impuestos aplicables.*
