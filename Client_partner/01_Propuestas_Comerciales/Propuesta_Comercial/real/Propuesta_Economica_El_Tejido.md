# Propuesta Económica — El Tejido

**Sistema conversacional de memoria institucional por WhatsApp, LLM y Markdown**

| | |
|---|---|
| **Cliente** | GHT |
| **Proveedor** | GTA — Global Technology Ally / Aliado TI |
| **Fase** | Hito 1 · MVP básico de validación técnica |
| **Fecha** | 12 de junio de 2026 |
| **Entrega primera versión** | 22 de junio de 2026 |

---

## 1. Contexto y objetivo

El conocimiento más valioso de GHT vive en la cabeza de sus líderes. Se pierde con la rotación, el retiro y la falta de un momento para escribirlo. Las wikis y bases de datos anteriores fueron abandonadas.

**El Tejido** convierte ese conocimiento en una memoria institucional viva: atribuida, calificada e interconectada. El sistema conversa con los participantes, captura ideas y aprendizajes, los evalúa con un LLM usando la rúbrica que GHT ya entregó y los compila en archivos Markdown auditables y versionables.

GHT quiere iniciar ya con un **MVP básico**. Proponemos un sprint intensivo: primera aproximación navegable el **17 de junio** y primera versión del MVP el **22 de junio**.

---

## 2. Qué entrega el MVP básico (22 de junio)

1. **Flujo conversacional completo.** Pregunta → respuesta del participante → evaluación con LLM → retroalimentación breve → máximo una repregunta → cierre.
2. **Evaluación con la rúbrica de GHT.** La rúbrica ya entregada se consume en Markdown; criterios, pesos y escala aplican sin tocar código.
3. **Canal garantizado.** El flujo opera sobre una **página web simple** desde el primer día. WhatsApp se activa en cuanto GHT habilite la línea (ver sección 4).
4. **Memoria institucional.** Cada aporte se compila en Markdown con autoría, calificación y trazabilidad completa.
5. **Consulta de resultados.** Vista simple de respuestas, calificaciones, explicaciones y Markdown generado. La campaña inicial y los ~5 participantes se configuran con asistencia de GTA.

### Se difiere a la evolución continua

Portal administrativo completo de autoservicio (login por código WhatsApp, gestión de campañas/tags/prompts en pantalla), reenvíos automáticos, plantillas Meta aprobadas para envíos masivos, dashboard, robustez para ~120 contribuyentes y todo el Hito 2 y 3.

---

## 3. Cronograma — sprint al 22 de junio

| Fecha | Entregable |
|---|---|
| **Vie 13 jun** | Kickoff. Confirmación de insumos y arranque de arquitectura. |
| **Lun 16 – Mié 17 jun** | Núcleo conversacional + evaluación LLM con la rúbrica de GHT. **Primera aproximación navegable: miércoles 17 de junio** (canal web). |
| **Jue 18 – Vie 19 jun** | Generación Markdown, trazabilidad y consulta de resultados. Parametrización WhatsApp si la línea ya está habilitada. |
| **Sáb 20 – Lun 22 jun** | Prueba con usuarios reales, ajustes y **entrega de la primera versión del MVP: lunes 22 de junio**. |

---

## 4. Dependencia de WhatsApp y canal alterno

La parametrización de WhatsApp la realiza GTA **sobre la plataforma Meta de GHT** (las credenciales ya fueron entregadas). Para activar el canal, GHT debe aportar:

1. La **línea celular** a parametrizar.
2. La **tarjeta de crédito** registrada en la plataforma Meta.

Reglas del sprint:

- Si la línea queda habilitada **antes del 19 de junio**, la entrega del 22 sale por WhatsApp y web.
- Si no, la entrega del 22 sale por la **página web simple** con el flujo completo, y WhatsApp se activa dentro de los **2 días hábiles** siguientes a la habilitación de la línea, sin costo adicional.

---

## 5. Inversión

### 5.1 Precio fijo del sprint MVP

| Concepto | Valor |
|---|---|
| **MVP básico El Tejido — sprint 13 a 22 de junio** | **USD 9.750** |

Equivale a dos semanas de la capacidad de referencia del equipo (USD 19.500 / 160 horas al mes), aplicada en modalidad intensiva.

| Hito de pago | % | Valor |
|---|---|---|
| Aprobación y kickoff (13 jun) | 50 % | USD 4.875 |
| Entrega primera versión (22 jun) | 50 % | USD 4.875 |

El precio es cerrado para el alcance de la sección 2. Cambios de alcance se estiman y aprueban como requerimientos independientes.

### 5.2 Infraestructura y consumo

- Durante el MVP, la solución opera en **infraestructura de GTA**. Los costos de Azure, WhatsApp Cloud API y consumo del LLM se facturan a GHT **con los soportes correspondientes**, sin margen (estimado: USD 150–300/mes a escala de piloto).
- Para producción, la solución se despliega en **infraestructura de GHT**, que asume su operación y costo directo.

### 5.3 Evolución continua posterior al MVP

Entregada la primera versión, el alcance restante del Hito 1 (portal administrativo de autoservicio, robustez) y los Hitos 2 y 3 se ejecutan bajo el modelo de capacidad dedicada que GHT ya conoce:

| Capacidad mensual | Valor de referencia |
|---|---|
| Equipo dedicado — 160 horas/mes | USD 19.500 |

Se factura solo el esfuerzo aprobado, trabajado y entregado; si es menor, la facturación es proporcional (ej.: 2 semanas = USD 9.750). Prioridades revisadas cada ciclo con GHT mediante trade-offs explícitos.

---

## 6. Compromisos de las partes

**Ya entregado por GHT**

- Rúbrica de evaluación en Markdown. ✔
- Credenciales de la plataforma Meta. ✔

**Pendiente de GHT**

- Línea celular a parametrizar en WhatsApp y tarjeta de crédito en la plataforma Meta.
- Lista de ~5 usuarios de prueba con números de WhatsApp.
- Aprobación de prompts (GTA propone la versión inicial).
- Validación oportuna de entregables durante el sprint y pagos acordados.

**GTA**

- Equipo dedicado en modalidad sprint del 13 al 22 de junio.
- Parametrización de WhatsApp sobre la plataforma Meta de GHT.
- Canal web simple garantizado desde la primera aproximación (17 de junio).
- Reporte diario corto de avance durante el sprint.

---

## 7. Roadmap posterior (sin precio en esta propuesta)

| Hito | Objetivo |
|---|---|
| **Hito 1 (completar)** | Portal administrativo de autoservicio y robustez operativa. |
| **Hito 2 — Convención** | Escalar a ~120 contribuyentes: monitoreo de participación, reenvíos controlados y métricas de captura. |
| **Hito 3 — Curaduría** | Consejo de líderes senior canoniza el mejor conocimiento: páginas de entidad, índices por capítulo y base vectorial para consulta semántica. |

---

## 8. Próximos pasos

1. Aprobación de esta propuesta (hoy, 12 de junio).
2. Pago del 50 % y kickoff el viernes 13 de junio.
3. GHT gestiona la línea celular y la tarjeta en Meta.
4. Primera aproximación el 17 de junio; primera versión el 22 de junio.

---

*Propiedad GTA — Global Technology Ally / Aliado TI · Información Confidencial.*
*Las cifras en USD no incluyen impuestos aplicables.*
