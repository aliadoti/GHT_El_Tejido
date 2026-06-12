# Documento inicial de requerimientos

## El Tejido

### Sistema conversacional para captura, evaluación, compilación y consulta de conocimiento institucional mediante WhatsApp, LLM y Markdown

**Versión:** 0.4
**Estado:** Borrador formal refinado
**Propósito del documento:** Servir como base para construir la propuesta comercial, escribir las especificaciones funcionales/técnicas y definir el primer MVP.

---

# 1. Resumen

**El Tejido** será un sistema conversacional para capturar conocimiento, ideas, criterios, experiencias y aprendizajes de participantes mediante WhatsApp, evaluarlos con un LLM usando rúbricas parametrizables y convertirlos en una memoria institucional atribuida, calificada e interconectada.

El sistema no debe entenderse únicamente como una herramienta para recolectar respuestas. Su objetivo es transformar conocimiento que hoy vive en la cabeza de líderes, expertos y participantes en un repositorio durable, consultable, auditable y reutilizable.

El caso inicial estará enfocado en una validación técnica con un grupo reducido de usuarios. Posteriormente, el sistema deberá servir como capa de participación para una convención con aproximadamente 120 contribuyentes.

El MVP debe demostrar que es posible:

1. Iniciar conversaciones por WhatsApp.
2. Capturar pensamientos o ideas de usuarios matriculados.
3. Evaluar las respuestas con una rúbrica.
4. Dar retroalimentación conversacional.
5. Guardar trazabilidad completa.
6. Compilar el conocimiento capturado en archivos Markdown.
7. Preparar la información para una futura capa de consulta semántica.

---

# 2. Objetivo general

Construir una plataforma flexible que permita capturar conocimiento institucional mediante WhatsApp, evaluarlo con LLM y rúbricas configurables, conservarlo como Markdown versionable y preparar una base de conocimiento atribuida, calificada e interconectada para consulta y curaduría posterior.

---

# 3. Objetivos específicos

1. Permitir que administradores configuren campañas de captura de conocimiento.
2. Permitir que administradores seleccionen participantes específicos para una campaña.
3. Enviar mensajes iniciales configurables por WhatsApp a participantes seleccionados.
4. Identificar participantes por número de WhatsApp normalizado.
5. Validar que los participantes estén matriculados, activos y asociados a la campaña.
6. Capturar respuestas desde WhatsApp.
7. Evaluar respuestas mediante un LLM configurable.
8. Usar rúbricas configurables en Markdown.
9. Aplicar criterios y pesos definidos en la rúbrica.
10. Generar retroalimentación breve, humana y útil para el participante.
11. Permitir máximo una repregunta en el MVP.
12. Guardar conversaciones, mensajes, respuestas, evaluaciones y trazabilidad.
13. Compilar respuestas y evaluaciones en documentos Markdown.
14. Mantener autoría y atribución de cada aporte.
15. Preparar los artefactos para indexación semántica futura.
16. Permitir consulta administrativa de respuestas, calificaciones y explicaciones.
17. Permitir validación humana de la calidad de respuestas del LLM.
18. Permitir aprobación humana de prompts y rúbricas.
19. Diseñar el sistema para futuros casos de uso más allá de la convención.

---

# 4. Principios del sistema

## 4.1 No es solo una encuesta

El sistema no debe comportarse como un formulario tradicional. La experiencia debe ser conversacional, breve y de baja fricción.

## 4.2 No es una wiki tradicional

El sistema debe capturar conocimiento mediante conversación y luego compilarlo en artefactos estructurados, atribuidos y reutilizables.

## 4.3 La autoría importa

Cada aporte debe conservar trazabilidad del participante que lo generó.

## 4.4 La evaluación importa

Cada respuesta debe poder calificarse con una rúbrica clara, versionada y auditable.

## 4.5 Markdown como formato durable

Los aportes consolidados deben poder representarse en Markdown para permitir portabilidad, auditoría, versionamiento y futura compilación.

## 4.6 El humano cura, el agente organiza

El LLM puede evaluar, sugerir, compilar y detectar relaciones, pero la validación final de calidad corresponde a personas.

---

# 5. Objetivos del MVP

El MVP debe validar el flujo técnico completo con aproximadamente 5 personas.

El MVP será exitoso si:

1. Un administrador puede ingresar al portal con un código enviado a WhatsApp.
2. Un administrador puede registrar participantes y administradores.
3. Un administrador puede configurar una campaña.
4. Un administrador puede seleccionar participantes para una campaña.
5. Un administrador puede configurar mensajes iniciales.
6. Un administrador puede enviar mensajes por WhatsApp desde el portal.
7. Un participante recibe el mensaje inicial.
8. El participante responde desde WhatsApp.
9. El sistema reconoce al participante por su número normalizado.
10. El sistema valida que el participante pertenece a la campaña.
11. El sistema evalúa la respuesta con el LLM configurado.
12. El sistema usa una rúbrica Markdown configurada.
13. El sistema responde con retroalimentación breve y conversacional.
14. El sistema permite máximo una repregunta.
15. El sistema cierra la interacción con un mensaje de agradecimiento.
16. El sistema guarda conversación, respuesta, evaluación, prompt y rúbrica usada.
17. El sistema genera o actualiza un archivo Markdown con el aporte capturado.
18. El administrador puede consultar respuestas, calificaciones, explicaciones y Markdown generado.
19. Los mensajes iniciales, preguntas, prompts, tags y rúbricas pueden modificarse sin tocar código.

---

# 6. Alcance del MVP

## 6.1 Incluido

El MVP debe incluir:

1. Integración con API de WhatsApp.
2. Recepción de mensajes entrantes desde WhatsApp.
3. Envío de mensajes salientes por WhatsApp.
4. Registro administrativo de usuarios.
5. Registro de números de celular normalizados.
6. Autenticación administrativa con código enviado a WhatsApp.
7. Gestión básica de participantes.
8. Gestión básica de administradores.
9. Gestión básica de tags.
10. Tags iniciales: `área` y `empresa`.
11. Gestión básica de campañas.
12. Asociación de participantes a campañas.
13. Configuración de mensajes iniciales parametrizables.
14. Envío manual de mensajes iniciales desde el portal administrativo.
15. Reenvío de mensajes iniciales a participantes que no respondan.
16. Preguntas iniciales configurables:

    * Escribe una idea para mejorar los ingresos.
    * Escribe una idea para reducir los costos.
    * Escribe una idea para mejorar la productividad.
17. Carga o configuración de rúbrica inicial en Markdown.
18. Lectura de criterios, pesos y escala desde la rúbrica Markdown.
19. Gestión de prompts editables.
20. Configuración del proveedor/modelo LLM.
21. Almacenamiento seguro de API keys.
22. Evaluación de respuestas mediante LLM.
23. Una sola repregunta máxima por respuesta en el MVP.
24. Controles básicos contra abuso, prompt injection y consumo excesivo.
25. Generación de retroalimentación conversacional.
26. Almacenamiento de conversaciones, mensajes, respuestas y evaluaciones.
27. Generación de artefactos Markdown por respuesta o por entidad.
28. Portal web administrativo básico.
29. Consulta de respuestas, calificaciones, explicaciones y Markdown generado.
30. Persistencia flexible para datos operativos.
31. Preparación para versionamiento futuro de Markdown en Git.
32. Prueba inicial con aproximadamente 5 usuarios.

---

## 6.2 Excluido del MVP

El MVP no incluirá inicialmente:

1. Integración con directorio activo o Entra ID.
2. Aplicación móvil nativa.
3. Dashboard avanzado.
4. Exportación a Excel, PDF o PowerPoint.
5. Gamificación.
6. Premios o reconocimientos.
7. Ranking definitivo de ideas.
8. Flujo formal de aprobación de ideas.
9. Consolidación automática avanzada de ideas.
10. Curaduría colaborativa avanzada dentro del sistema.
11. Multiidioma completo.
12. Analítica histórica avanzada.
13. Integración con sistemas corporativos externos.
14. Automatización del plan de trabajo final.
15. Recordatorios automáticos avanzados.
16. Envíos recurrentes programados.
17. Aprobación automática de conocimiento.
18. Base vectorial productiva.
19. Chat semántico avanzado sobre el corpus.
20. Compilación avanzada de páginas de entidades múltiples.

---

# 7. Roadmap conceptual

## 7.1 Hito 1: Validación técnica

Objetivo: demostrar que se puede capturar, calificar y compilar pensamientos en Markdown con pocos usuarios.

Incluye:

* WhatsApp.
* Usuario matriculado.
* Campaña.
* Rúbrica.
* LLM.
* Retroalimentación.
* Persistencia.
* Markdown generado.
* Consulta administrativa.

## 7.2 Hito 2: Convención

Objetivo: escalar la participación a aproximadamente 120 contribuyentes mediante una capa de participación motivadora y de baja fricción.

Incluye en fase futura:

* Mayor volumen de participantes.
* Monitoreo de participación.
* Reenvíos controlados.
* Métricas de captura.
* Mayor robustez operativa.
* Posible dashboard ejecutivo.

## 7.3 Hito 3: Curaduría

Objetivo: permitir que un consejo o grupo de líderes senior revise, canonice y valide el mejor conocimiento capturado cuando el corpus tenga masa crítica.

Incluye en fase futura:

* Revisión humana avanzada.
* Estados de curaduría.
* Consolidación de conocimiento.
* Páginas de entidad.
* Índices por capítulo o área.
* Base vectorial para consulta semántica.

---

# 8. Usuarios y roles

## 8.1 Participante

Usuario que recibe mensajes por WhatsApp y responde con ideas, aprendizajes, experiencias o conocimiento.

### Funciones principales

* Recibir mensaje inicial de una campaña.
* Leer instrucciones enviadas por WhatsApp.
* Responder a las preguntas.
* Recibir retroalimentación del sistema.
* Responder una repregunta, si aplica.
* Recibir mensaje de cierre.

### Restricciones

* Debe estar previamente matriculado.
* Debe tener número de WhatsApp registrado y normalizado.
* Debe estar activo.
* Debe estar asociado a la campaña.
* No accede al portal administrativo.

---

## 8.2 Administrador de plataforma

Usuario encargado de configurar y operar el sistema.

### Funciones principales

* Ingresar al portal con código enviado a WhatsApp.
* Crear y editar usuarios.
* Registrar números de celular normalizados.
* Asignar área, empresa y tags a usuarios.
* Crear y editar campañas.
* Asociar participantes a campañas.
* Crear y editar mensajes iniciales.
* Enviar mensajes iniciales por WhatsApp.
* Reenviar mensajes iniciales a participantes que no respondan.
* Crear y editar preguntas.
* Cargar o editar rúbricas en Markdown.
* Configurar prompts.
* Configurar proveedor/modelo LLM.
* Configurar credenciales de forma segura.
* Consultar conversaciones.
* Consultar respuestas.
* Consultar evaluaciones.
* Consultar artefactos Markdown generados.

---

## 8.3 Validador humano

Persona o equipo responsable de revisar la calidad de las respuestas generadas por el LLM.

### Funciones principales

* Revisar calificaciones generadas por el LLM.
* Revisar explicaciones generadas por el LLM.
* Validar si la retroalimentación entregada al usuario fue útil.
* Aprobar o solicitar ajustes a prompts.
* Aprobar o solicitar ajustes a rúbricas.
* Revisar la calidad del Markdown generado.

En el MVP, esta validación puede realizarse manualmente fuera del sistema, revisando la información consultada en el portal administrativo.

---

## 8.4 Visor de resultados

Usuario que puede consultar información generada, pero no necesariamente modificar configuración.

### Funciones principales

* Consultar respuestas.
* Consultar calificaciones.
* Consultar explicaciones generadas por el LLM.
* Consultar Markdown generado.
* Filtrar información por campaña, usuario, área, empresa, tag, pregunta, categoría o calificación.

---

# 9. Canal de interacción

## 9.1 Canal principal para participantes

El canal principal será WhatsApp.

La conversación será iniciada por el sistema cuando el administrador envíe los mensajes iniciales desde el portal.

La experiencia debe ser breve, clara y humana.

---

## 9.2 Canal administrativo

El canal administrativo será una aplicación web.

Desde el portal administrativo se configurarán:

* Campañas.
* Participantes.
* Mensajes iniciales.
* Preguntas.
* Rúbricas.
* Prompts.
* Proveedor/modelo LLM.
* Credenciales.
* Resultados.
* Markdown generado.

---

# 10. Autenticación administrativa

El acceso administrativo se realizará mediante código enviado a WhatsApp.

## 10.1 Flujo de autenticación

1. El administrador ingresa a la pantalla de login.
2. La pantalla solicita el número de celular.
3. La pantalla muestra instrucciones de normalización del número.
4. El administrador ingresa su número.
5. El sistema normaliza o valida el número.
6. El sistema verifica que el número exista en la base de datos.
7. El sistema verifica que el usuario tenga rol administrativo.
8. El sistema genera un código temporal de autenticación.
9. El sistema envía el código por WhatsApp.
10. El administrador ingresa el código en la pantalla.
11. Si el código es válido, el sistema permite acceso al portal.
12. Si el código es inválido o vencido, el sistema rechaza el acceso.

---

## 10.2 Normalización de números

Los números de celular deben almacenarse normalizados en la base de datos.

El sistema debe mostrar instrucciones claras en la pantalla de login.

Ejemplo:

```text
Ingresa tu número en formato internacional, sin espacios ni símbolos.
Ejemplo Colombia: 573001112233
Ejemplo Estados Unidos: 13055551234
```

## 10.3 Reglas

1. Los números deben guardarse en formato normalizado.
2. El login administrativo debe comparar contra números normalizados.
3. Solo usuarios con rol administrativo pueden recibir código de acceso.
4. El código debe tener expiración.
5. El código debe ser de un solo uso.
6. Debe existir límite de intentos.
7. Debe existir límite de solicitudes de código por número.
8. Los códigos no deben guardarse en texto plano.
9. El sistema debe registrar intentos exitosos y fallidos.
10. El sistema no debe revelar de forma explícita si un número existe o no ante intentos no autorizados.

---

# 11. Campañas

Una campaña representa un evento, convención, capítulo, área o caso de uso donde se recopila conocimiento.

## 11.1 Datos mínimos de campaña

Cada campaña debe tener:

* ID.
* Nombre.
* Descripción.
* Objetivo.
* Estado.
* Participantes asociados.
* Tags aplicables.
* Mensajes iniciales.
* Preguntas asociadas.
* Rúbrica asociada.
* Documento de rúbrica en Markdown.
* Prompt asociado.
* Configuración del LLM.
* Configuración conversacional.
* Configuración de generación Markdown.
* Fecha de creación.
* Fecha de actualización.

## 11.2 Estados sugeridos

* Borrador.
* Activa.
* Cerrada.
* Archivada.

## 11.3 Reglas

1. Solo campañas activas deben permitir envío de mensajes iniciales.
2. Solo campañas activas deben recibir respuestas.
3. Las preguntas deben ser configurables por campaña.
4. Los mensajes iniciales deben ser configurables por campaña.
5. La rúbrica debe poder asociarse a una campaña.
6. El prompt debe poder asociarse a una campaña.
7. Una campaña debe tener participantes asociados antes del envío.
8. Una campaña debe poder reutilizarse como plantilla para futuros casos.
9. El sistema debe guardar la configuración usada al momento de cada interacción.
10. La campaña debe definir si genera Markdown por respuesta, por participante, por pregunta o por entidad.

---

# 12. Participantes

## 12.1 Datos mínimos del participante

Cada participante debe tener:

* ID.
* Nombre.
* Número de WhatsApp normalizado.
* Estado.
* Área.
* Empresa.
* Tags asociados.
* Campañas habilitadas.
* Propiedades dinámicas.
* Fecha de creación.
* Fecha de actualización.

## 12.2 Reglas

1. El número de WhatsApp será el identificador principal.
2. El número debe estar normalizado.
3. Un participante no matriculado no puede participar.
4. Un participante inactivo no puede participar.
5. Un participante puede tener varios tags.
6. Un participante puede pertenecer a varias campañas.
7. El sistema debe registrar qué mensajes se enviaron a cada participante.
8. El sistema debe registrar si el participante respondió o no.
9. El sistema debe conservar atribución del conocimiento capturado al participante.

---

# 13. Tags parametrizables

El sistema debe permitir clasificar participantes y conocimiento mediante tags.

## 13.1 Tags iniciales

Para el MVP, los tags iniciales serán:

1. Área.
2. Empresa.

## 13.2 Reglas

1. Los tags deben ser parametrizables.
2. Un participante puede tener varios tags.
3. Los tags deben poder activarse o desactivarse.
4. Los tags deben poder usarse para filtrar participantes.
5. Los tags deben poder usarse para filtrar resultados.
6. Los tags deben poder usarse para clasificar Markdown generado.
7. El sistema no debe depender de una lista fija quemada en código.

---

# 14. Selección de participantes por campaña

El sistema debe permitir seleccionar qué usuarios participarán en una campaña específica.

## 14.1 Formas de selección

El administrador podrá seleccionar participantes:

* Manualmente.
* Por área.
* Por empresa.
* Por tags.
* Por búsqueda de nombre.
* Por número de WhatsApp.

## 14.2 Reglas

1. Un participante debe estar activo para ser seleccionado.
2. Un participante debe tener número de WhatsApp válido y normalizado.
3. Un participante puede pertenecer a múltiples campañas.
4. El sistema debe registrar los participantes seleccionados por campaña.
5. El sistema debe permitir consultar a quiénes se les envió mensaje inicial.
6. El sistema debe permitir reenviar mensaje inicial a participantes que no respondan.
7. No se requiere confirmación adicional si el envío es ejecutado desde un administrador autenticado.
8. El sistema debe registrar cada envío y reenvío.

---

# 15. Mensajes iniciales configurables

El sistema debe permitir configurar uno o varios mensajes iniciales por campaña.

Estos mensajes serán enviados por WhatsApp para iniciar la interacción con los participantes.

## 15.1 Mensaje inicial de la primera campaña

Mensaje inicial base:

```text
Hola [nombre], ayúdanos a contestar las siguientes preguntas.
```

Este mensaje debe ser parametrizable.

## 15.2 Datos mínimos del mensaje inicial

Cada mensaje inicial debe tener:

* ID.
* Campaña asociada.
* Nombre interno.
* Texto del mensaje.
* Orden.
* Estado.
* Variables dinámicas permitidas.
* Fecha de creación.
* Fecha de actualización.

## 15.3 Variables dinámicas

Los mensajes iniciales podrán usar variables como:

```text
{{nombre}}
{{campaña}}
{{empresa}}
{{area}}
```

Ejemplo:

```text
Hola {{nombre}}, ayúdanos a contestar las siguientes preguntas para la campaña {{campaña}}.
```

## 15.4 Reglas

1. Los mensajes iniciales deben ser editables sin tocar código.
2. Una campaña puede tener uno o varios mensajes iniciales.
3. Si hay varios mensajes iniciales, deben enviarse en el orden configurado.
4. El sistema debe registrar fecha y hora de envío.
5. El sistema debe registrar estado de envío por participante.
6. El sistema debe permitir consultar errores de envío.
7. El sistema debe permitir reenviar mensajes a participantes que no respondan.
8. El sistema debe registrar cada reenvío.
9. Los mensajes iniciales deben ser compatibles con las reglas de la API de WhatsApp.
10. Si la API de WhatsApp exige plantillas aprobadas para iniciar conversaciones, los mensajes iniciales deberán gestionarse como plantillas compatibles.

---

# 16. Preguntas configurables

## 16.1 Preguntas iniciales

Para el MVP, las preguntas iniciales serán:

1. **Escribe una idea para mejorar los ingresos.**
2. **Escribe una idea para reducir los costos.**
3. **Escribe una idea para mejorar la productividad.**

Estas preguntas reflejan el caso inicial, pero el sistema debe permitir reemplazarlas por preguntas enfocadas en conocimiento técnico, conocimiento de clientes, producción, DSD, cultura, estrategia u otros capítulos futuros.

## 16.2 Datos mínimos de pregunta

Cada pregunta debe tener:

* ID.
* Campaña asociada.
* Texto de la pregunta.
* Instrucción visible para el usuario.
* Categoría.
* Orden.
* Estado.
* Rúbrica asociada.
* Prompt asociado.
* Número máximo de repreguntas.
* Límites de seguridad aplicables.
* Configuración de generación Markdown.

## 16.3 Reglas

1. Las preguntas deben ser editables desde el portal administrativo.
2. Las preguntas deben poder asociarse a una rúbrica.
3. Las preguntas deben poder tener instrucciones visibles para el usuario.
4. Las preguntas no deben estar quemadas en código.
5. El sistema debe guardar la pregunta exacta respondida por el usuario.
6. El sistema debe enviar las preguntas después de los mensajes iniciales o como parte del flujo conversacional configurado.
7. Para el MVP, se permitirá máximo una repregunta.
8. Cada pregunta debe poder mapearse a una categoría o capítulo de conocimiento.

---

# 17. Rúbricas parametrizables

El sistema debe permitir configurar rúbricas para calificar respuestas.

## 17.1 Fuente inicial de la rúbrica

La rúbrica inicial será entregada en un documento Markdown.

Este documento debe poder ser consumido por el LLM.

El documento de rúbrica indicará:

* Criterios de evaluación.
* Escala de calificación.
* Pesos por criterio.
* Aspectos a tener en cuenta.
* Instrucciones de evaluación.

## 17.2 Datos mínimos de una rúbrica

Cada rúbrica debe tener:

* ID.
* Nombre.
* Descripción.
* Contenido Markdown.
* Escala de calificación.
* Criterios de evaluación.
* Pesos por criterio.
* Estado.
* Versión.
* Fecha de creación.
* Fecha de actualización.

## 17.3 Reglas

1. La rúbrica debe poder cargarse o editarse desde el portal administrativo.
2. La rúbrica debe conservar versión.
3. El sistema debe guardar qué versión de rúbrica fue usada.
4. El LLM debe recibir la rúbrica aplicable al evaluar.
5. El sistema debe permitir cambiar la rúbrica sin modificar código.
6. La escala y los pesos se tomarán del documento de rúbrica Markdown.
7. El sistema debe guardar la calificación total y la calificación por criterio.
8. La rúbrica debe ser suficientemente general para evaluar desde principios estratégicos hasta rutinas operativas.

---

# 18. Prompts editables

Los prompts deben poder editarse desde el portal administrativo.

## 18.1 Tipos de prompt

El sistema debe permitir configurar prompts para:

* Evaluar una respuesta.
* Generar retroalimentación.
* Formular una repregunta.
* Cerrar una conversación.
* Compilar Markdown.
* Detectar temas o entidades.
* Sugerir enlaces entre aportes.
* Controlar tono.
* Controlar longitud.
* Definir comportamientos permitidos.
* Definir comportamientos prohibidos.
* Prevenir instrucciones maliciosas del usuario.

## 18.2 Versionamiento

Cada prompt debe tener versión.

El sistema debe guardar qué versión de prompt fue usada en cada evaluación o compilación.

## 18.3 Reglas

1. Los prompts no deben estar quemados en código.
2. Un prompt debe poder estar activo o inactivo.
3. Las evaluaciones deben conservar trazabilidad del prompt usado.
4. Las compilaciones Markdown deben conservar trazabilidad del prompt usado.
5. El administrador debe poder editar prompts sin intervención técnica.
6. Los prompts deben ser aprobados por un equipo humano antes de usarse en campaña.
7. El prompt debe indicar que el LLM no debe prometer implementar ideas.
8. El prompt debe indicar que el LLM no debe ofrecer ejecutar acciones.
9. El prompt debe indicar que el LLM debe responder corto, natural y práctico.

---

# 19. Configuración del LLM

El sistema debe permitir configurar el proveedor o modelo LLM.

## 19.1 Datos configurables

El sistema debe permitir configurar:

* Proveedor.
* Modelo.
* Endpoint, si aplica.
* API key o credencial.
* Parámetros básicos del modelo.
* Prompt activo.
* Límites de tokens.
* Timeout.
* Número máximo de reintentos.
* Estado de la configuración.

## 19.2 Seguridad de credenciales

Las API keys deben almacenarse de forma segura.

Reglas:

1. Las API keys no deben guardarse en texto plano en la base de datos.
2. Las API keys no deben mostrarse completas en pantalla.
3. El sistema debe permitir actualizar una API key.
4. El sistema debe registrar quién actualizó la configuración.
5. El sistema debe restringir esta función solo a administradores autorizados.
6. Se recomienda usar un servicio de secretos como Azure Key Vault o equivalente.
7. En base de datos solo debe guardarse una referencia o identificador seguro, no la clave expuesta.

---

# 20. Evaluación con LLM

## 20.1 Información enviada al LLM

El sistema debe enviar al LLM:

* Contexto de la campaña.
* Mensajes iniciales enviados.
* Pregunta realizada.
* Respuesta del usuario.
* Documento de rúbrica Markdown.
* Pesos y escala definidos en la rúbrica.
* Tags relevantes del usuario.
* Prompt configurado.
* Historial reciente de conversación.
* Límites de comportamiento.

## 20.2 Información esperada del LLM

El LLM debe devolver una respuesta estructurada con:

* Calificación por criterio.
* Justificación breve por criterio.
* Calificación total.
* Explicación general.
* Retroalimentación para el usuario.
* Recomendación de cerrar o hacer repregunta.
* Repregunta sugerida, si aplica.
* Temas identificados.
* Entidades mencionadas, si aplica.
* Indicador de seguridad o anomalía, si aplica.

## 20.3 Reglas

1. La respuesta del LLM debe ser estructurada.
2. La retroalimentación al usuario debe ser corta y conversacional.
3. El sistema debe guardar la evaluación completa.
4. El sistema debe guardar prompt y versión usados.
5. El sistema debe guardar rúbrica y versión usadas.
6. El sistema debe guardar pesos usados.
7. El sistema no debe permitir que el LLM prometa implementar la idea.
8. El sistema no debe permitir que el LLM ofrezca ejecutar acciones.
9. El sistema debe aplicar controles de seguridad antes y después de llamar al LLM.
10. El sistema debe manejar errores del LLM sin romper la conversación.

---

# 21. Retroalimentación conversacional

La retroalimentación que recibe el usuario debe ser breve, natural y práctica.

## 21.1 Características esperadas

La respuesta debe ser:

* Corta.
* Clara.
* Humana.
* Conversacional.
* No robótica.
* Útil.
* Sin fricción.
* Orientada a mejorar la respuesta.

## 21.2 El sistema puede

* Hacer una sola repregunta.
* Sugerir que el usuario concrete mejor la idea.
* Pedir que explique el beneficio.
* Pedir que precise impacto en ingresos, costos o productividad.
* Pedir que aclare cómo se implementaría.
* Pedir que precise un aprendizaje, práctica, criterio o experiencia.
* Ayudar al usuario a pensar mejor su respuesta.

## 21.3 El sistema no debe

* Ofrecer implementar la idea.
* Prometer que la idea será ejecutada.
* Decir que aplicará las sugerencias.
* Responder de forma extensa.
* Sonar como un formulario rígido.
* Dar instrucciones complejas.
* Abrir conversaciones innecesarias.
* Hacer más de una repregunta en el MVP.

---

# 22. Compilación a Markdown

El sistema debe generar artefactos Markdown a partir de las respuestas capturadas y evaluadas.

## 22.1 Objetivo

Convertir respuestas conversacionales en conocimiento durable, portable, auditable y versionable.

## 22.2 Tipos de Markdown del MVP

Para el MVP se debe generar al menos uno de estos tipos:

1. Markdown por respuesta.
2. Markdown por participante.
3. Markdown por campaña.
4. Markdown por pregunta o categoría.

## 22.3 Estructura mínima sugerida

```markdown
# Título del aporte

## Metadatos

- Campaña:
- Participante:
- Área:
- Empresa:
- Fecha:
- Pregunta:
- Tags:
- Rúbrica:
- Versión de rúbrica:
- Prompt:
- Versión de prompt:
- Calificación total:

## Respuesta original

[Texto original del participante]

## Evaluación

### Calificación por criterio

| Criterio | Puntaje | Justificación |
|---|---:|---|

## Retroalimentación enviada

[Texto enviado por WhatsApp]

## Temas identificados

- Tema 1
- Tema 2

## Entidades mencionadas

- Entidad 1
- Entidad 2

## Notas de trazabilidad

- ID de conversación:
- ID de respuesta:
- ID de evaluación:
```

## 22.4 Reglas

1. El Markdown debe conservar autoría.
2. El Markdown debe conservar pregunta original.
3. El Markdown debe conservar respuesta original.
4. El Markdown debe conservar evaluación.
5. El Markdown debe conservar versión de rúbrica y prompt.
6. El Markdown debe poder regenerarse desde los datos operativos.
7. El Markdown debe estar preparado para versionamiento futuro.
8. El Markdown debe estar preparado para indexación futura.
9. El Markdown no debe contener secretos ni API keys.
10. El Markdown debe ser legible por humanos.

---

# 23. Fuente de verdad y almacenamiento

El sistema tendrá dos tipos de almacenamiento conceptual.

## 23.1 Almacenamiento operacional

Debe guardar:

* Usuarios.
* Campañas.
* Mensajes.
* Conversaciones.
* Respuestas.
* Evaluaciones.
* Estados.
* Configuraciones.
* Logs.
* Seguridad.

Para el MVP se recomienda una base documental de bajo costo, por ejemplo Azure Cosmos DB en modalidad serverless o equivalente.

## 23.2 Artefactos de conocimiento

Debe guardar o generar:

* Archivos Markdown.
* Representación compilada de respuestas.
* Metadatos de atribución.
* Estructura para futura indexación.

Para el MVP, los Markdown pueden guardarse en la misma base documental o en almacenamiento de archivos. Para fases posteriores, deben poder versionarse en Git.

## 23.3 Regla de diseño

El sistema debe permitir regenerar los artefactos Markdown a partir de los datos operativos.

---

# 24. Arquitectura conceptual objetivo

## 24.1 Capa 1: Captura y fuente durable

Responsable de:

* Capturar mensajes por WhatsApp.
* Guardar respuestas originales.
* Guardar atribución.
* Guardar evaluaciones.
* Generar Markdown.
* Mantener trazabilidad.

## 24.2 Capa 2: Síntesis LLM

Responsable de:

* Evaluar respuestas.
* Generar retroalimentación.
* Identificar temas.
* Identificar entidades.
* Compilar contenido en Markdown.
* Sugerir conexiones, duplicados o contradicciones en fases futuras.

## 24.3 Capa 3: Consulta semántica futura

Responsable de:

* Indexar Markdown o páginas compiladas.
* Permitir búsqueda semántica.
* Responder preguntas en lenguaje natural.
* Citar fuente y autor original.

Esta capa no forma parte obligatoria del MVP, pero el diseño debe prepararla.

---

# 25. Límites de seguridad y control de abuso

Aunque no se define un umbral mínimo de calidad para considerar una respuesta suficiente, el sistema debe tener límites de seguridad para protegerse de abuso, inyección de prompts, consumo excesivo y ataques.

## 25.1 Límites mínimos requeridos

El sistema debe configurar:

* Longitud máxima de mensaje entrante.
* Longitud máxima de respuesta enviada al LLM.
* Longitud máxima de historial conversacional enviado al LLM.
* Número máximo de repreguntas.
* Número máximo de mensajes por usuario por campaña.
* Número máximo de llamadas al LLM por usuario por campaña.
* Timeout de llamadas al LLM.
* Número máximo de reintentos.
* Límite de solicitudes por número de WhatsApp.
* Límite de intentos de autenticación administrativa.
* Límite de solicitudes de código de autenticación.

## 25.2 MVP

Para el MVP:

* Máximo una repregunta.
* Máximo una evaluación inicial y una evaluación posterior a repregunta.
* Máximo configurable de caracteres por mensaje.
* Máximo configurable de tokens enviados al LLM.
* Rechazo o truncamiento seguro de mensajes excesivamente largos.
* Registro de eventos anómalos.

## 25.3 Controles contra prompt injection

El sistema debe:

1. Tratar la respuesta del usuario como dato, no como instrucción.
2. Instruir al LLM para ignorar instrucciones del usuario que intenten modificar el sistema, la rúbrica o el prompt.
3. Separar instrucciones del sistema, rúbrica, pregunta y respuesta del usuario.
4. Validar que la respuesta del LLM cumpla el formato esperado.
5. Aplicar fallback si la respuesta del LLM es inválida.
6. Registrar intentos sospechosos.
7. Evitar enviar secretos, API keys o información sensible al LLM.
8. No incluir datos innecesarios en el contexto enviado al LLM.

---

# 26. Flujo conversacional esperado

## 26.1 Preparación de campaña

1. El administrador ingresa al portal.
2. El administrador configura o selecciona una campaña.
3. El administrador configura mensajes iniciales.
4. El administrador configura preguntas.
5. El administrador carga o configura la rúbrica Markdown.
6. El administrador configura el prompt.
7. El administrador configura reglas de Markdown.
8. El administrador selecciona participantes.
9. El administrador activa la campaña.

## 26.2 Inicio de conversación

1. El administrador presiona el botón de envío.
2. El sistema envía uno o varios mensajes iniciales a los participantes seleccionados.
3. El sistema registra el envío por participante.
4. El participante recibe el mensaje por WhatsApp.
5. El participante responde por WhatsApp.

## 26.3 Validación

1. El sistema identifica el número de WhatsApp.
2. El sistema normaliza o valida el número recibido.
3. El sistema verifica si el usuario está matriculado.
4. El sistema verifica si el usuario está activo.
5. El sistema verifica si el usuario pertenece a la campaña.
6. Si el usuario no está autorizado, el sistema responde con un mensaje de rechazo controlado.
7. Si el usuario está autorizado, el sistema continúa el flujo.

## 26.4 Captura

1. El sistema recibe la respuesta.
2. El sistema guarda el mensaje.
3. El sistema asocia la respuesta con usuario, campaña y pregunta.
4. El sistema envía la respuesta al LLM para evaluación.

## 26.5 Evaluación

1. El LLM evalúa la respuesta contra la rúbrica.
2. El LLM devuelve calificación, explicación, temas, entidades y retroalimentación.
3. El sistema guarda la evaluación.
4. El sistema determina si debe hacer una repregunta o cerrar.

## 26.6 Repregunta

Para el MVP, el sistema podrá hacer máximo una repregunta.

1. Si la configuración lo indica, el sistema envía una repregunta.
2. El participante responde.
3. El sistema evalúa nuevamente.
4. El sistema guarda la nueva evaluación.
5. El sistema cierra.

## 26.7 Compilación Markdown

1. El sistema toma la respuesta original.
2. El sistema toma la evaluación.
3. El sistema toma metadatos de usuario, campaña, pregunta, rúbrica y prompt.
4. El sistema genera o actualiza un artefacto Markdown.
5. El sistema guarda el Markdown.
6. El administrador puede consultarlo.

## 26.8 Cierre

Al finalizar, el sistema debe enviar un mensaje de agradecimiento.

Ejemplo:

```text
Gracias. Tu aporte quedó registrado correctamente.
```

---

# 27. Portal administrativo

El sistema debe contar con un portal web para administración y consulta.

## 27.1 Funciones administrativas

El portal debe permitir:

* Autenticarse mediante código enviado por WhatsApp.
* Gestionar usuarios participantes.
* Gestionar usuarios administradores.
* Gestionar tags.
* Gestionar campañas.
* Gestionar participantes por campaña.
* Gestionar mensajes iniciales.
* Enviar mensajes iniciales.
* Reenviar mensajes iniciales a participantes que no respondan.
* Gestionar preguntas.
* Cargar o editar rúbricas Markdown.
* Gestionar prompts.
* Configurar proveedor/modelo LLM.
* Configurar credenciales de forma segura.
* Configurar reglas de generación Markdown.
* Ver conversaciones.
* Ver respuestas.
* Ver evaluaciones.
* Ver Markdown generado.

## 27.2 Funciones de envío

El portal debe permitir:

* Seleccionar una campaña.
* Seleccionar participantes.
* Filtrar participantes por área.
* Filtrar participantes por empresa.
* Filtrar participantes por tags.
* Ver cantidad de destinatarios.
* Enviar mensajes iniciales desde un botón del administrador.
* Ver estado de envío por participante.
* Consultar errores de envío.
* Reintentar envíos fallidos.
* Reenviar a participantes sin respuesta.

## 27.3 Funciones de consulta

El portal debe permitir filtrar por:

* Campaña.
* Usuario.
* Número de WhatsApp.
* Área.
* Empresa.
* Tag.
* Pregunta.
* Categoría.
* Estado.
* Calificación.
* Fecha.
* Estado de envío.
* Estado de respuesta.
* Tema identificado.
* Entidad mencionada.

## 27.4 Seguridad

Solo usuarios con rol administrativo pueden acceder al portal.

---

# 28. Persistencia y repositorio de datos

## 28.1 Repositorio recomendado

Para el MVP se recomienda usar una base de datos documental de bajo costo.

La opción preferida es evaluar Azure Cosmos DB en modalidad serverless o equivalente, por su flexibilidad para documentos, estructura dinámica y escenarios de bajo uso o tráfico intermitente.

## 28.2 Requerimientos del repositorio

El repositorio debe permitir:

* Propiedades dinámicas.
* Tags variables.
* Campañas con configuraciones diferentes.
* Mensajes iniciales configurables.
* Documentos de rúbrica en Markdown.
* Prompts versionados.
* Historial conversacional.
* Evaluaciones estructuradas.
* Registro de envíos por participante.
* Estado de participación.
* Markdown generado.
* Temas y entidades detectadas.
* Trazabilidad de cambios relevantes.

## 28.3 Entidades principales

El repositorio debe soportar al menos:

* Usuario.
* Tag.
* Campaña.
* Participante de campaña.
* Mensaje inicial.
* Envío de mensaje.
* Pregunta.
* Rúbrica.
* Prompt.
* Configuración LLM.
* Conversación.
* Mensaje.
* Respuesta.
* Evaluación.
* Artefacto Markdown.
* Código de autenticación administrativa.
* Log de seguridad.

---

# 29. Modelo conceptual

## 29.1 Usuario

Representa una persona matriculada, participante o administrador.

Campos mínimos:

* ID.
* Nombre.
* Número de WhatsApp normalizado.
* Rol.
* Estado.
* Área.
* Empresa.
* Tags.
* Propiedades dinámicas.
* Fecha de creación.
* Fecha de actualización.

---

## 29.2 Tag

Representa una clasificación configurable.

Campos mínimos:

* ID.
* Nombre.
* Tipo.
* Descripción.
* Estado.

---

## 29.3 Campaña

Representa un evento, capítulo o caso de uso.

Campos mínimos:

* ID.
* Nombre.
* Descripción.
* Objetivo.
* Estado.
* Mensajes iniciales.
* Preguntas asociadas.
* Rúbrica asociada.
* Prompt asociado.
* Configuración LLM.
* Configuración Markdown.
* Usuarios habilitados.
* Configuración conversacional.

---

## 29.4 Participante de campaña

Representa la relación entre usuario y campaña.

Campos mínimos:

* ID.
* Usuario.
* Campaña.
* Estado.
* Fecha de inclusión.
* Estado de envío.
* Estado de respuesta.
* Fecha de primer envío.
* Fecha de última respuesta.

---

## 29.5 Mensaje inicial

Representa un mensaje configurado para iniciar la conversación.

Campos mínimos:

* ID.
* Campaña.
* Nombre interno.
* Texto.
* Orden.
* Variables dinámicas.
* Estado.
* Fecha de creación.
* Fecha de actualización.

---

## 29.6 Envío de mensaje

Representa el registro de un mensaje enviado por WhatsApp.

Campos mínimos:

* ID.
* Campaña.
* Usuario.
* Mensaje inicial.
* Número de WhatsApp.
* Estado de envío.
* Fecha de envío.
* Tipo de envío.
* Error, si aplica.

Tipos de envío:

* Inicial.
* Reenvío.
* Repregunta.
* Cierre.
* Autenticación.

---

## 29.7 Pregunta

Representa una pregunta enviada al usuario.

Campos mínimos:

* ID.
* Campaña.
* Texto.
* Instrucción.
* Categoría.
* Orden.
* Estado.

---

## 29.8 Rúbrica

Representa el documento usado para evaluar.

Campos mínimos:

* ID.
* Nombre.
* Descripción.
* Contenido Markdown.
* Versión.
* Estado.
* Fecha de creación.
* Fecha de actualización.

---

## 29.9 Prompt

Representa una instrucción configurable para el LLM.

Campos mínimos:

* ID.
* Nombre.
* Tipo.
* Contenido.
* Versión.
* Estado.
* Aprobado por.
* Fecha de aprobación.
* Fecha de creación.
* Fecha de actualización.

---

## 29.10 Configuración LLM

Representa la configuración del proveedor/modelo LLM.

Campos mínimos:

* ID.
* Nombre.
* Proveedor.
* Modelo.
* Endpoint.
* Referencia segura a API key.
* Parámetros.
* Estado.
* Fecha de creación.
* Fecha de actualización.

---

## 29.11 Conversación

Representa el historial de interacción con un usuario.

Campos mínimos:

* ID.
* Usuario.
* Campaña.
* Canal.
* Estado.
* Mensajes.
* Fecha de inicio.
* Fecha de cierre.

---

## 29.12 Respuesta

Representa una respuesta enviada por el usuario.

Campos mínimos:

* ID.
* Usuario.
* Campaña.
* Pregunta.
* Texto.
* Canal.
* Fecha.
* Estado.

---

## 29.13 Evaluación

Representa el resultado generado por el LLM.

Campos mínimos:

* ID.
* Respuesta evaluada.
* Rúbrica usada.
* Versión de rúbrica.
* Prompt usado.
* Versión de prompt.
* Configuración LLM usada.
* Calificación por criterio.
* Calificación total.
* Explicación.
* Retroalimentación enviada al usuario.
* Temas identificados.
* Entidades mencionadas.
* Recomendación de cierre o repregunta.
* Fecha.

---

## 29.14 Artefacto Markdown

Representa un documento Markdown generado a partir de una respuesta o conjunto de respuestas.

Campos mínimos:

* ID.
* Tipo.
* Campaña.
* Usuario.
* Pregunta.
* Respuesta asociada.
* Evaluación asociada.
* Contenido Markdown.
* Estado.
* Versión.
* Fecha de creación.
* Fecha de actualización.

Tipos posibles:

* Respuesta.
* Participante.
* Campaña.
* Entidad.
* Capítulo.

---

# 30. Trazabilidad

El sistema debe guardar trazabilidad suficiente para consultar y auditar el proceso.

## 30.1 Información mínima a guardar

* Usuario.
* Número de WhatsApp normalizado.
* Área y empresa.
* Tags del usuario al momento de responder.
* Campaña.
* Participantes seleccionados.
* Mensajes iniciales configurados.
* Mensajes iniciales enviados.
* Estado de envío por participante.
* Pregunta enviada.
* Respuesta original.
* Mensajes enviados y recibidos.
* Evaluación LLM.
* Rúbrica usada.
* Versión de rúbrica.
* Prompt usado.
* Versión de prompt.
* Configuración LLM usada.
* Markdown generado.
* Fecha y hora de cada interacción.
* Retroalimentación enviada al usuario.
* Eventos de seguridad.
* Intentos de login administrativo.

---

# 31. Requerimientos no funcionales

## 31.1 Flexibilidad

El sistema debe permitir cambiar campañas, mensajes iniciales, preguntas, rúbricas, prompts, tags y configuraciones sin modificar código.

## 31.2 Bajo nivel de fricción

La experiencia por WhatsApp debe ser rápida, clara y natural.

## 31.3 Parametrización

Los elementos principales deben ser configurables:

* Campañas.
* Participantes.
* Mensajes iniciales.
* Preguntas.
* Rúbricas.
* Prompts.
* Tags.
* Umbrales técnicos.
* Límites de seguridad.
* Número máximo de interacciones.
* Configuración del LLM.
* Configuración de Markdown.

## 31.4 Calidad conversacional

La interacción debe sentirse humana, breve y útil.

## 31.5 Seguridad básica

El sistema debe validar:

* Usuario matriculado.
* Usuario activo.
* Usuario asociado a campaña.
* Rol administrativo.
* Código de autenticación.
* Acceso permitido al portal.
* Límites de abuso.
* Envíos solo a participantes seleccionados.

## 31.6 Control de consumo

El sistema debe controlar:

* Número de llamadas al LLM.
* Longitud de mensajes.
* Cantidad de repreguntas.
* Reintentos.
* Timeouts.
* Errores de proveedor.

## 31.7 Portabilidad

El conocimiento capturado debe poder exportarse o regenerarse en Markdown.

## 31.8 Mantenibilidad

El sistema debe separar:

* Lógica conversacional.
* Configuración de campañas.
* Gestión de mensajes iniciales.
* Gestión de rúbricas.
* Gestión de prompts.
* Integración con WhatsApp.
* Integración con LLM.
* Persistencia operacional.
* Generación Markdown.
* Seguridad.
* Portal administrativo.

---

# 32. Diseño visual y marca GHT

El portal administrativo debe alinearse con la identidad visual de GHT.

## 32.1 Lineamientos base

Usar como referencia:

* Verde GHT: `#20431D`.
* Verde claro GHT: `#508D5E`.
* Rojo GHT: `#DB2B09`.
* Superficies claras y neutras.
* Tipografía corporativa basada en Avenir Next o fallback equivalente.
* Uso sobrio del rojo como acento.
* Estética de red, tejido, nodos o conexiones como metáfora visual.

## 32.2 Reglas

1. No compartir ni exponer archivos de fuente.
2. Usar fuentes del sistema o fallback si no se dispone de licencia web.
3. Mantener consistencia visual con el material ejecutivo.
4. Evitar una interfaz recargada.
5. Priorizar claridad operativa sobre animaciones.

---

# 33. Criterios de aceptación del MVP

## 33.1 Administrador

1. El administrador puede ingresar su número de celular en el login.
2. La pantalla muestra instrucciones para normalizar el número.
3. El sistema envía un código de autenticación por WhatsApp.
4. El administrador puede acceder si el código es válido.
5. El administrador puede crear o editar usuarios.
6. El administrador puede asignar área y empresa.
7. El administrador puede crear o editar campañas.
8. El administrador puede asociar usuarios a una campaña.
9. El administrador puede crear mensajes iniciales.
10. El administrador puede enviar mensajes iniciales desde el portal.
11. El administrador puede reenviar mensajes a usuarios que no respondan.
12. El administrador puede configurar preguntas.
13. El administrador puede cargar o editar una rúbrica Markdown.
14. El administrador puede editar prompts.
15. El administrador puede configurar proveedor/modelo LLM.
16. El administrador puede guardar credenciales de forma segura.
17. El administrador puede ver respuestas.
18. El administrador puede ver calificaciones.
19. El administrador puede ver explicaciones generadas por el LLM.
20. El administrador puede ver Markdown generado.
21. El administrador puede filtrar información por campaña, usuario, área, empresa, tag, pregunta o calificación.

---

## 33.2 Participante

1. Dado un usuario seleccionado para una campaña, cuando el administrador envía el mensaje inicial, el usuario lo recibe por WhatsApp.
2. Dado un usuario matriculado, cuando responde por WhatsApp, el sistema lo reconoce.
3. Dado un usuario no matriculado, cuando responde por WhatsApp, el sistema informa que no tiene acceso.
4. Dado un usuario activo y asociado a la campaña, el sistema continúa la conversación.
5. Dado que el usuario responde, el sistema guarda la respuesta.
6. Dado que la respuesta fue guardada, el sistema la evalúa con el LLM.
7. Dado que el LLM evalúa la respuesta, el usuario recibe retroalimentación corta y útil.
8. Si aplica, el usuario recibe máximo una repregunta.
9. Dado que la interacción finaliza, el sistema envía un mensaje de agradecimiento.

---

## 33.3 Sistema

1. El sistema guarda historial conversacional.
2. El sistema guarda mensajes iniciales enviados.
3. El sistema guarda estado de envío por participante.
4. El sistema guarda respuestas.
5. El sistema guarda evaluaciones.
6. El sistema guarda prompt usado y su versión.
7. El sistema guarda rúbrica usada y su versión.
8. El sistema guarda configuración LLM usada.
9. El sistema genera Markdown.
10. El sistema permite consultar Markdown generado.
11. El sistema permite regenerar Markdown desde datos operativos.
12. El sistema permite cambiar configuración sin modificar código.
13. El sistema controla máximo una repregunta en el MVP.
14. El sistema aplica límites de seguridad.
15. El sistema mantiene separación entre configuración, conversación, evaluación, envío, seguridad, persistencia y generación Markdown.

---

# 34. Requerimientos de insumos del cliente

Para construir el MVP se requieren estos insumos:

1. Lista de 5 usuarios de prueba.
2. Nombre de cada usuario.
3. Número de WhatsApp normalizado de cada usuario.
4. Área de cada usuario.
5. Empresa de cada usuario.
6. Usuarios administradores.
7. Número de WhatsApp normalizado de cada administrador.
8. Documento de rúbrica inicial en Markdown.
9. Pesos y escala dentro del documento de rúbrica.
10. Prompt inicial o aprobación del prompt propuesto.
11. Mensaje inicial de la primera campaña.
12. Confirmación del proveedor/modelo LLM a utilizar.
13. Acceso o credenciales de WhatsApp API.
14. Definición del formato esperado del Markdown generado.
15. Lineamientos de marca para el portal administrativo, si aplica.

---

# 35. Recomendación de enfoque para el MVP

## 35.1 Flujo mínimo recomendado

1. Registrar 5 usuarios.
2. Registrar al menos un administrador.
3. Configurar una campaña.
4. Asociar usuarios a la campaña.
5. Configurar mensaje inicial:

   * `Hola [nombre], ayúdanos a contestar las siguientes preguntas.`
6. Configurar tres preguntas:

   * Mejorar ingresos.
   * Reducir costos.
   * Mejorar productividad.
7. Cargar rúbrica Markdown.
8. Configurar prompt de evaluación.
9. Configurar prompt de retroalimentación.
10. Configurar prompt de compilación Markdown.
11. Configurar proveedor/modelo LLM.
12. Administrador ingresa con código por WhatsApp.
13. Administrador envía mensajes iniciales.
14. Usuario responde por WhatsApp.
15. Sistema evalúa.
16. Sistema responde con retroalimentación breve.
17. Sistema hace máximo una repregunta, si aplica.
18. Sistema cierra.
19. Sistema genera Markdown.
20. Administrador consulta resultados.

---

## 35.2 Elementos críticos del MVP

Los elementos más importantes para validar son:

1. Integración con WhatsApp API.
2. Login administrativo por código enviado a WhatsApp.
3. Normalización de números.
4. Envío iniciado por administrador.
5. Mensajes iniciales parametrizables.
6. Identificación de usuarios matriculados.
7. Asociación usuario-campaña.
8. Rúbrica Markdown consumida por el LLM.
9. Evaluación con LLM configurable.
10. Calidad de la retroalimentación.
11. Generación Markdown.
12. Consulta administrativa de resultados.
13. Seguridad básica contra abuso e inyección.

---

# 36. Consideraciones críticas

## 36.1 El MVP debe demostrar memoria institucional, no solo captura

El valor del sistema no está únicamente en recibir respuestas por WhatsApp. Está en convertir esas respuestas en conocimiento atribuido, evaluado y durable.

## 36.2 La calidad del primer mensaje es determinante

Como el sistema inicia la conversación, el mensaje inicial debe ser claro, breve y motivador.

Debe explicar:

* Por qué se contacta al participante.
* Qué se espera de él.
* Qué tipo de conocimiento o idea debe compartir.
* Cómo responder.
* Que la interacción será breve.

## 36.3 La calidad de la rúbrica es determinante

Si la rúbrica es ambigua, el LLM evaluará de forma inconsistente.

La rúbrica debe tener criterios claros, pesos definidos, escala e instrucciones precisas.

## 36.4 La calidad del prompt es determinante

El prompt debe controlar tono, longitud, comportamiento permitido y comportamiento prohibido.

Debe evitar que el LLM prometa implementar ideas o genere respuestas largas.

## 36.5 WhatsApp exige bajo nivel de fricción

La interacción debe ser breve. Si el sistema pregunta demasiado o responde con textos largos, el usuario puede abandonar.

## 36.6 La seguridad debe estar desde el MVP

Aunque el MVP sea pequeño, debe tener límites de mensajes, límites de llamadas al LLM, validación de usuarios, control de roles y protección básica contra prompt injection.

## 36.7 Markdown no debe ser un detalle secundario

El Markdown es clave para portabilidad, auditoría, versionamiento y futura indexación.

---

# 37. Conclusión

**El Tejido** debe permitir iniciar conversaciones por WhatsApp con participantes seleccionados, capturar conocimiento o ideas, evaluarlas con un LLM usando una rúbrica Markdown parametrizable, entregar retroalimentación inmediata al participante y compilar los aportes en Markdown atribuido y trazable.

El primer MVP debe validar el flujo completo con 5 usuarios, una campaña, mensajes iniciales configurables, tres preguntas iniciales, una rúbrica Markdown, prompts editables, configuración segura del LLM, integración con WhatsApp API, login administrativo por código enviado a WhatsApp, generación Markdown y consulta administrativa básica.

El éxito del sistema dependerá principalmente de:

1. La claridad de los mensajes iniciales.
2. La simplicidad de la experiencia por WhatsApp.
3. La calidad de la rúbrica Markdown.
4. La calidad del prompt.
5. La configuración segura del LLM.
6. La trazabilidad de mensajes, respuestas y evaluaciones.
7. La generación de Markdown útil y auditable.
8. La protección básica contra abuso e inyección.
9. La capacidad de parametrizar campañas, participantes, mensajes, preguntas, tags, rúbricas, prompts y Markdown sin modificar código.
