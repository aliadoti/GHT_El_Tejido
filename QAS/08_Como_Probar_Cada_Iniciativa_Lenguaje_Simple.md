# 08 — Cómo probar cada iniciativa (en palabras simples)

> **Para quién es esto:** cualquier persona del equipo, **sin necesidad de saber de programación**,
> que quiera comprobar que cada mejora funciona. Cada punto dice **qué abrir, qué hacer y qué deberías
> ver**. Si algo no se comporta así, es señal de que hay un problema y hay que avisar al equipo técnico.
> Última actualización: 2026-07-29 (P-25 **completa en local**: coaching directo sin confirmación repetitiva;
> idea única, varias ideas, reapertura y Resultados por idea, el coach ya redacta cada mensaje de forma
> natural y el documento explica el listón y la escala de la nota. Pendiente solo la validación con
> GHT antes de activarlas).

## Antes de empezar (léelo una vez)

- **Dos formas de probar:**
  1. **Simulación** (recomendada para el día a día): una página del portal que imita a WhatsApp **sin
     costo y sin usar un teléfono real**. Ideal para repetir pruebas.
  2. **WhatsApp real**: un teléfono de prueba conversando de verdad con el sistema. Se usa para los
     puntos críticos (que llegue el mensaje, la ventana de tiempo, etc.).
- **Para repetir una prueba desde cero** existe el botón/función de **"Reiniciar datos"** (ver punto
  **P-03** abajo): borra las respuestas de un participante o de toda una campaña **sin borrar la
  campaña**, para que puedas volver a conversar como si fuera la primera vez.
- **"Activado por campaña" / "apagado por defecto":** algunas mejoras vienen **apagadas** a propósito
  (para no encender nada sin probar). Para probarlas hay que pedirle al equipo técnico que las
  **encienda en una campaña de prueba**. Están marcadas abajo con 🔧.
- **Semáforo de estado:**
  ✅ listo para probar · 🔧 listo pero hay que encenderlo primero · ⏳ aún no construido ·
  💤 aplazado (no se prueba para la convención) · 📄 no es software (es contenido/decisión, no se "prueba" aquí).

---

## A. Lo que ya puedes probar en el portal

### ✅ P-14 — Ver rúbricas y prompts (solo lectura)
**Qué es:** poder **leer** el contenido completo de una rúbrica o un prompt sin riesgo de cambiarlo.
1. Entra al portal como administrador y abre la pantalla de **Rúbricas** (luego repite en **Prompts**).
2. En cualquier fila, pulsa **"Ver"**.
3. **Deberías ver:** una ventana con **todo el contenido** (su texto, versión y estado) y **sin ningún
   botón para editar o guardar** — solo **Cerrar**.
4. Cierra y confirma que los botones de siempre (**Editar / Nueva versión / Archivar / Aprobar**)
   siguen ahí y funcionan.
**Algo va mal si:** no aparece "Ver", o desde esa ventana se puede modificar algo.

### ✅ P-16 — Usar la pantalla de Campañas sin perder acciones
**Qué es:** una reorganización interna de la pantalla de **Campañas**. Debe verse y funcionar igual,
pero ahora cada parte de la pantalla se puede mantener sin afectar a las demás.
1. Entra al portal como administrador y abre **Campañas**. Elige una campaña de la lista.
2. Recorre las secciones **Configuración**, **Mensajes iniciales**, **Preguntas** y **Participantes**.
3. Haz un cambio de prueba sencillo en cada parte (por ejemplo, editar la descripción, agregar un mensaje,
   agregar o editar una pregunta y usar la vista previa de participantes). Guarda cada cambio.
4. **Deberías ver:** que la campaña elegida sigue abierta, cada cambio queda guardado y las demás secciones
   conservan su información. Al pedir la vista previa, las personas encontradas quedan marcadas para asociar,
   pero puedes desmarcar alguna antes de continuar.
5. Si tienes datos de prueba, prueba **Reiniciar conversación** de una persona y el reinicio de toda la
   campaña: deben pedir confirmación antes de borrar datos de prueba.
**Algo va mal si:** desaparece una sección, se cambia o se pierde la campaña que estabas viendo, un botón
de administrador aparece para alguien sin permiso, se guarda dos veces la misma acción o alguna función
que existía antes deja de responder.

### ✅ P-18 — Identificar claramente qué hace cada casilla y campo
**Qué es:** una mejora para que las personas que usan teclado o lector de pantalla sepan qué seleccionan
antes de hacerlo. No cambia los envíos ni la carga de participantes.
1. Entra al portal y abre **Envíos** de una campaña que tenga al menos dos personas.
2. Recorre con la tecla Tab la casilla de arriba y dos casillas de personas distintas.
3. **Deberías oír o ver en el panel de accesibilidad:** que la primera casilla selecciona todos los envíos
   visibles y que cada una de las otras nombra a la persona correspondiente.
4. Abre **Usuarios**. En la sección **Tags**, confirma que los tres campos se identifican como nombre,
   tipo y descripción de la etiqueta.
5. En **Carga masiva de participantes**, confirma que el selector indica que espera un archivo CSV y que
   explica las columnas necesarias.
**Algo va mal si:** una casilla se anuncia solo como “casilla”, un campo solo dice “editar”, o el selector
de archivo no deja claro que debe ser un CSV de participantes.

### ✅ P-19 — Escuchar errores y confirmaciones sin perder el foco
**Qué es:** los avisos que aparecen después de una acción ahora se anuncian también para quien usa lector
de pantalla, sin repetir el mismo mensaje.
1. Abre la pantalla de **Ingreso** y escribe un número de prueba inválido o provoca una validación fallida.
2. Mantén el cursor en el campo y envía el formulario.
3. **Deberías ver y oír:** el mensaje de error, mientras el cursor sigue en el mismo campo para poder corregirlo.
4. En una pantalla administrativa, guarda un cambio permitido (por ejemplo, editar un usuario de prueba).
5. **Deberías ver y oír una sola vez:** la confirmación de que se guardó, sin interrumpir lo que estabas haciendo.
**Algo va mal si:** el mensaje solo se ve pero no se anuncia, se oye dos veces, el cursor salta sin explicación
o el campo con error no permite leer el motivo.

### ✅ P-20 — Cambiar las secciones de una campaña con teclado
**Qué es:** las secciones de una campaña ahora se pueden recorrer claramente con teclado y lector de pantalla.
1. Entra al portal, abre **Campañas** y selecciona una campaña existente.
2. Coloca el cursor en **Configuración** usando la tecla Tab.
3. Pulsa **Flecha derecha** varias veces, y luego **Flecha izquierda**, **Inicio** y **Fin**.
4. **Deberías ver:** que el nombre resaltado y el contenido cambian juntos entre Configuración, Mensajes
   iniciales, Preguntas y Participantes. Con un lector de pantalla debe anunciarse el nombre y si está
   seleccionada.
5. Desde una sección activa, pulsa Tab: el cursor debe pasar a su contenido. Después selecciona otra sección
   con el mouse y confirma que muestra el contenido correspondiente.
**Algo va mal si:** el cursor queda en un nombre mientras se muestra otra sección, las flechas desplazan la
página en vez de cambiar de sección, o el contenido activo no tiene un nombre reconocible.

### ✅ P-22 — Preparar una campaña paso a paso
**Qué es:** la pantalla de **Campañas** ahora guía el orden recomendado para prepararla y explica lo que falta, sin cambiar las acciones que ya existían.

1. Entra al portal como administrador y abre **Campañas**. **Deberías ver:** la lista de campañas y el botón **“+ Nueva campaña”**; el formulario no debe ocupar la pantalla antes de pulsarlo.
2. Pulsa **“+ Nueva campaña”**, completa una campaña de prueba y guárdala. **Deberías ver:** que el formulario se cierra y queda abierta la campaña nueva.
3. En una campaña abierta, revisa los cuatro pasos: **1 Configuración**, **2 Mensajes iniciales**, **3 Preguntas** y **4 Participantes**. Agrega un mensaje activo, una pregunta activa y una persona de prueba. **Deberías ver:** una marca de completado al lado de cada paso cuando ya tiene lo necesario, o una advertencia discreta cuando falta algo.
4. Abre **Configuración**. **Deberías ver:** los campos separados en **Evaluación**, **Conversación** y **Seguridad y costo**, con explicaciones junto a los valores de umbral, tiempo sin respuesta y presupuesto. Si no hay preguntas o participantes, al intentar activar la campaña debe aparecer un mensaje claro que diga qué falta.
5. Pulsa **“Ver envíos”**. **Deberías ver:** los envíos de esa misma campaña. Vuelve a una campaña sin preguntas o sin participantes: debe indicarte cómo agregar el primer elemento, no dejar una pantalla vacía sin explicación.

**Algo va mal si:** el formulario de creación aparece siempre, se abre otra campaña después de guardar, un paso marca completo sin tener el elemento indicado, “Ver envíos” muestra otra campaña, o activar falla sin explicar qué hace falta.

### ✅ P-23 — Revisar resultados con una vista más clara

**Qué es:** la pantalla de **Resultados** ahora abre una campaña disponible por sí sola y permite elegir una respuesta para ver juntos su evaluación y su documento.

1. Entra al portal y abre **Resultados**. **Deberías ver:** una campaña ya elegida y sus resultados cargados, sin tener que pulsar un botón de consulta. Si no hay campañas, debe aparecer un mensaje amable que explique que aún no hay nada para revisar.
2. Revisa la parte superior. **Deberías ver:** cuántas respuestas hay, cuántas son maduras y cuántas están en incubación, además de una pequeña leyenda que explica esas marcas y si una respuesta fue evaluada.
3. En la lista de la izquierda, busca una respuesta larga. **Deberías ver:** el nombre de la persona, sus marcas y solo un fragmento corto de lo que escribió, para poder comparar varias respuestas sin leerlas completas.
4. Pulsa una respuesta. **Deberías ver:** que queda resaltada y, a la derecha, aparecen su calificación, comentarios y documento. Si el documento existe, puedes abrir **Descargar .md** para guardarlo.
5. Si entras como administrador, pulsa **Regenerar documento** en una respuesta que ya tenga documento. **Deberías ver:** un aviso de que se regeneró. Si entras como visor, ese botón no debe aparecer.
6. Abre **Actividad de la campaña** al final. **Deberías ver:** la lista de personas y si su conversación está en curso o cerrada, sin quitar espacio a las respuestas principales.

**Algo va mal si:** aparece el mensaje técnico que pide un id de campaña, hay que consultar manualmente una campaña disponible, al elegir una respuesta no se muestra su detalle, se mezclan datos de otra respuesta, un visor puede regenerar documentos, o una pantalla vacía no explica qué hacer.

### ✅ I-08 — Cargar participantes en lote (Excel/CSV)
**Qué es:** subir muchos participantes de una vez con un archivo, en lugar de uno por uno.
1. En el portal, ve a la pantalla de **Usuarios**, **descarga la plantilla vacía** y llénala. Las
   columnas son las de GHT: *Empresa, ID Empresa, Sede, Nombre, Cargo, Email, Antigüedad en la empresa
   en años, Idioma, Telefono*. **Solo Nombre y Telefono son obligatorios** (sin teléfono no hay
   WhatsApp).
2. Sube el archivo (`.xlsx` o `.csv`) con la opción de **cargar archivo**.
3. **Deberías ver:** un **resumen** que dice cuántos se crearon, cuántos se actualizaron y cuántos se
   rechazaron, con el **motivo** de cada rechazo (por ejemplo, un número mal escrito). Cada persona
   creada recibe un **código de usuario** consecutivo (`U-000042`) que ya no cambia nunca.
4. Vuelve a subir el mismo archivo: **no debe duplicar** a nadie, y los códigos de usuario deben ser
   los mismos.
5. **Prueba el cambio de titular:** cambia el nombre de una persona en el archivo por otro
   completamente distinto y vuelve a subirlo. El sistema **no debe decidir solo**: esa fila queda
   marcada como *conflicto* mostrando el nombre actual y el propuesto, y tú eliges si es una
   corrección de nombre o si el teléfono pasó a otra persona. Solo si eliges **reasignar**, la persona
   anterior queda inactiva y se crea un registro nuevo — sus aportes de campañas anteriores **siguen
   siendo suyos**, no del nuevo titular.
6. Si solo quieres completar datos (por ejemplo el idioma) sin crear a nadie, usa el modo
   **"solo actualizar"**: los teléfonos que no existan se reportan como *no encontrado*.

**Algo va mal si:** una fila con un error tumba toda la carga (debería rechazar solo esa fila), se
crean participantes repetidos, el sistema **inactiva a alguien solo porque su nombre tenía una tilde
distinta**, o después de reasignar un teléfono los aportes viejos aparecen bajo el nombre del nuevo
titular.

### ✅ P-03 — Reiniciar datos para volver a probar
**Qué es:** dejar una conversación "como nueva" sin borrar la campaña, para repetir pruebas.
1. En el portal (Envíos o Resultados), usa **"Reiniciar conversación"** de un participante, o
   **"Reiniciar datos"** de toda la campaña.
2. Confirma la acción.
3. **Deberías ver:** que desaparecen las respuestas/resultados de ese participante, **pero la campaña,
   sus preguntas y los usuarios siguen ahí**. En la sección **Participantes**, su estado de envío debe
   quedar como **pendiente**.
4. Abre **Envíos** y envía de nuevo la campaña a esa persona. Debe quedar disponible para seleccionarla
   y recibir el mensaje inicial otra vez.
5. Escribe de nuevo como ese participante: el sistema te vuelve a saludar y hacer la pregunta desde cero.
**Algo va mal si:** se borra la campaña o su configuración, el envío permanece como “enviado”, o el
participante no puede volver a empezar.

### ✅ I-16 — La calificación correcta queda en el resumen
**Qué es:** un arreglo para que el resumen final (Markdown) muestre **la última** calificación, no una vieja.
1. Haz que un participante responda y sea evaluado; luego que envíe una versión mejorada.
2. Abre el **resumen/resultado** de esa respuesta en el portal.
3. **Deberías ver:** que la calificación y el texto corresponden a **la última** evaluación, no a un intento anterior.
**Algo va mal si:** el resumen muestra una nota que ya no corresponde.

### ✅ DT-QA-02 — Revisar evaluaciones que no aparecen en Resultados
**Qué es:** una consulta para el equipo de pruebas. Permite ver todas las evaluaciones de una campaña,
incluso una que no quedó conectada a una respuesta. No cambia ningún dato ni muestra el texto de los
participantes.
1. Pide al equipo técnico la dirección de pruebas y entra como **administrador** o **visor**.
2. Abre la dirección `.../api/admin/evaluaciones?campaniaId=ID_DE_LA_CAMPANA`.
3. **Deberías ver:** una lista ordenada de la más reciente a la más antigua y un resumen con cuántas
   están enlazadas, huérfanas, superadas o sin versión de idea.
4. Para revisar solo un problema, añade `&enlace=huerfana` al final de la dirección.
5. **Deberías ver:** solo las evaluaciones cuyo `respuestaId` está vacío o ya no existe. Una evaluación
   marcada como **superada** no es un problema: hay una evaluación más reciente para la misma respuesta.
6. Revisa una fila: no deben aparecer explicaciones, comentarios ni el texto de la persona. Si hace falta
   leer ese detalle, el equipo técnico debe abrir la evaluación individual.
**Algo va mal si:** la dirección devuelve 404, se ven comentarios o texto de participantes, una persona
sin sesión puede abrirla, o una evaluación antigua se reporta como huérfana aunque exista una más reciente.

### ✅ I-17 — Separar ideas maduras de ideas en incubación
**Qué es:** cada idea queda identificada como **madura** cuando alcanza el nivel esperado o como
**incubación** cuando todavía necesita trabajo. No se pierde ninguna idea.
1. En una campaña de prueba, envía una idea bien explicada y otra muy corta o incompleta.
2. Abre la pantalla de **Resultados**.
3. **Deberías ver:** una marca junto a cada respuesta que dice **Madura** o **Incubación**, y un conteo
   de cada grupo.
4. Usa el selector de esa pantalla para ver solo maduras y luego solo incubación.
5. Si el resumen "esto es lo que entendí" está habilitado, comprueba que aparece solo para la idea madura.
6. Tras una idea madura, escribe **"no lo guardes"**: debe pasar a incubación y el chat debe cerrarse con
   un acuse amable.
**Algo va mal si:** una idea desaparece, el filtro mezcla ambos grupos, una idea rechazada sigue madura,
o el resumen aparece para una idea en incubación.

---

## B. Lo que se prueba conversando (simulación o WhatsApp real)

### ✅ DT-QA-01 — Probar una conversación de ensayo sin compartir una clave sensible

**Qué es:** una forma segura de enviar un mensaje de prueba al sistema desplegado sin usar ni mostrar la
clave privada de WhatsApp. Requiere que el equipo técnico haya publicado esta versión y habilitado la
simulación solo durante la prueba.

1. Pide al equipo técnico la dirección de pruebas y una clave temporal de diagnóstico; no pidas la clave
   privada de WhatsApp.
2. Con la herramienta de pruebas, elige un participante de prueba y escribe un mensaje corto, por ejemplo
   “Hola”. Envía el mensaje.
3. **Deberías ver:** una confirmación inmediata de que el mensaje fue recibido. Espera unos segundos.
4. Abre **Resultados** o la conversación de esa persona. **Deberías ver:** la misma pregunta inicial o
   respuesta que recibiría una persona por WhatsApp normal.
5. Envía exactamente el mismo mensaje de ensayo otra vez con su mismo identificador. **Deberías ver:** que
   no se duplica la conversación ni aparecen dos resultados iguales.
6. Al terminar, pide al equipo técnico que cierre la simulación.

**Algo va mal si:** la herramienta pide la clave privada de WhatsApp, el mensaje crea dos conversaciones,
no aparece ninguna respuesta después de esperar, o un mensaje normal de WhatsApp deja de ser rechazado
cuando no tiene una firma válida.

### ✅ I-19/P-25 — Construir, calificar y mejorar una sola idea completa

**Qué es:** cada respuesta del participante complementa la misma idea. El sistema la consolida y
califica completa en el mismo turno, sin repetir “¿entendí bien?”. **Todos los pasos
de abajo ya están construidos y probados en el equipo**, incluida la pantalla de Resultados.

1. Responde una pregunta con una idea incompleta. **Deberías ver:** un reconocimiento breve y una sola
   pregunta útil para concretar la idea; no debe pedirte confirmar lo mismo.
2. Responde únicamente con el dato faltante. **Deberías ver:** una nueva retroalimentación basada en la
   idea inicial más el detalle nuevo, no solo en esa última frase.
5. Termina la idea y abre **Resultados**. **Deberías ver:** una sola fila por idea (no una por
   mensaje), marcada como **madura**, **pendiente** o **rechazada**; al abrirla, la idea completa, su
   calificación y, desplegando *Historial de la idea*, tus mensajes originales y las versiones por las
   que pasó. El selector de arriba permite ver solo maduras, pendientes o rechazadas. (El detalle de la
   nota y del listón en el documento se prueba en **I-20**, más abajo.)
6. Escribe **“quiero complementar la anterior”**. Debe reabrir esa misma idea, incorporar el cambio y
   recalificarla sin insertar una confirmación mecánica.
7. Si queda madura, debe decir **Pendiente de curaduría**; no debe enviarse automáticamente a
   implementación, conocimiento o acta.
8. Si la campaña tiene respuestas de antes de este cambio, aparecen aparte, en **Resultados
   históricos**, marcadas como tales. No deben mezclarse con las ideas ni desaparecer.

**Algo va mal si:** una frase complementaria aparece/califica como otra idea; el sistema olvida o
inventa información; repite “¿Es correcto?” después de cada aporte; muestra una fila por revisión; conserva una nota de
otra versión; o publica sin curaduría.

**Dos casos más que conviene probar:**

- **Mensaje confuso.** Responde algo muy ambiguo (por ejemplo, solo "eso"). El sistema debe **pedirte
  una aclaración corta** en vez de inventar un resumen, y tu mensaje no debe perderse.
- **Te vas a mitad de camino.** Deja una idea sin terminar y no respondas más. Pasado el tiempo de
  inactividad configurado, esa idea debe quedar como **pendiente** en Resultados —nunca como madura— y
  conservar lo que ya habías dicho.

**Varias ideas en un mensaje (ya disponible en pruebas del equipo).** Requiere una campaña con el
acompañamiento idea por idea encendido:

1. Responde la pregunta con **dos ideas distintas en un solo mensaje**.
2. **Deberías ver:** retroalimentación y una sola pregunta sobre **la primera idea**. La segunda queda
   esperando su turno, en silencio.
3. Si la primera ya está completa, el sistema la cierra naturalmente y empieza a trabajar la segunda,
   también sin pedir una confirmación mecánica.
4. Si la primera todavía necesita trabajo, el sistema hace **una sola pregunta** sobre ella y no pasa a
   la segunda hasta terminarla.
5. Escribe **"no lo guardes"** mientras trabajas una idea. **Deberías ver:** un acuse breve, esa idea
   descartada y el paso a la siguiente; las demás no se pierden.

**Algo va mal si:** el sistema mezcla las dos ideas, vuelve a preguntar “¿Es correcto?”, trabaja las dos
a la vez, o descarta todo cuando solo pediste descartar una.

**Se te ocurre otra idea a mitad de camino (ya disponible en pruebas del equipo).** Funciona con o sin
el acompañamiento idea por idea encendido:

1. Mientras trabajas una idea, escribe un mensaje que **corrija o complete la idea en curso y, además,
   proponga una idea distinta** (por ejemplo: "en realidad sería con soporte; además propongo un
   tablero semanal de seguimiento").
2. **Deberías ver:** retroalimentación sobre **la idea en curso** con tu corrección incluida. La idea
   nueva queda anotada esperando turno.
3. Termina la idea en curso siguiendo las preguntas de coaching.
4. **Deberías ver:** enseguida coaching sobre **la idea nueva**, sin que tengas que repetirla y sin una
   confirmación mecánica.

**Algo va mal si:** la idea nueva se mezcla dentro del resumen de la idea en curso, el sistema se pone
a trabajar la idea nueva antes de terminar la anterior, la conversación se cierra y pierdes la idea
nueva, o el sistema anota como "idea" un pedazo suelto de frase.

**Quieres volver a una idea que ya terminaste (ya disponible en pruebas del equipo).** La campaña debe
seguir abierta:

1. Termina una idea (hasta que el sistema la dé por cerrada) y empieza otra.
2. Escribe **"quiero volver a la anterior"**. **Deberías ver:** un mensaje que retoma **la última idea
   que cerraste**, te recuerda cómo quedó registrada y te pregunta qué quieres cambiar o agregar.
3. Escribe el cambio. **Deberías ver:** retroalimentación sobre **esa misma** idea ya actualizada y
   recalificada completa (puede mejorar o bajar), sin “¿Es correcto?”.
4. Si ya cerraste **varias** ideas y escribes algo más vago como **"quiero retomar una idea"**,
   deberías ver una lista corta **numerada** con un resumen de cada una —**sin notas ni puntajes**— y la
   pregunta de cuál retomar. Responde con el número.
5. Si en vez del número escribes otra cosa, el sistema **no adivina**: sigue con la idea en la que
   estabas y toma tu mensaje como un aporte más.

**Algo va mal si:** retoma una idea distinta a la que pediste, pierde lo que ya habías registrado en
ella, la lista muestra calificaciones, se queda "pegado" pidiendo un número, o permite cambios cuando la
campaña ya está cerrada.

### ✅ I-20 — Que el coach hable natural y que el documento explique la nota

**Qué es:** dos mejoras que van juntas. (1) El coach deja de repetir siempre la misma frase: cada
mensaje se redacta según la campaña, la pregunta y lo que acabas de decir. (2) El documento de cada
idea deja de mostrar un número suelto y explica **sobre cuánto** es esa nota y **qué tan alto estaba
el listón**. Está construido y probado en el equipo; falta la validación con ustedes.

**Parte 1 — la conversación (por WhatsApp o simulación):**

1. Responde una pregunta con una idea corta. **Deberías ver:** una frase natural que reconoce el punto
   de partida y **una sola pregunta de coaching** basada en lo que todavía falta.
2. Repite el ejercicio con otra persona o en otra campaña. **Deberías ver:** que las frases **no son
   calcadas**; cambian según el tema, aunque la idea completa siempre aparezca íntegra.
3. Sigue respondiendo a las preguntas para mejorar. **Deberías ver:** **una sola** pregunta por turno,
   sin números, sin "criterios" y sin mencionar la rúbrica.
4. Fíjate en que ningún mensaje mezcle dos cosas a la vez (por ejemplo, pedirte confirmar y a la vez
   preguntarte otra cosa distinta).

**Parte 2 — el documento (portal):**

5. Abre **Resultados**, elige una idea ya cerrada y mira su documento.
6. **Deberías ver** dos líneas claras, por ejemplo: `Umbral de madurez: 3,4 de 5 puntos (60 %; global)`
   y `Calificación total: 4 de 5 puntos`. Lo del paréntesis dice **de dónde salió el listón**: de la
   pregunta, de la campaña o del valor general.
7. Si una campaña o una pregunta tiene su propio listón configurado, ese número y esa palabra deben
   cambiar en consecuencia.
8. Abre una idea **rechazada** o que quedó sin evaluar: debe decir **"pendiente de evaluación"** y
   **no** mostrar ninguna línea de listón.

**Algo va mal si:** aparece “Entendí que propones… ¿Es correcto?”; llegan dos preguntas
juntas; el coach menciona rúbrica, criterios, puntajes, "umbral" o promete que la idea **se va a
implementar**; el documento muestra una
nota sin decir sobre cuánto es; el listón no coincide con el configurado; o una idea sin evaluar
aparece como si hubiera alcanzado el listón.

> **Si algo se ve raro en producción:** esta mejora se puede **apagar sin desplegar nada**. Al apagarla,
> el coach vuelve exactamente a los textos anteriores y **no se pierde ninguna idea ni ninguna nota**;
> solo cambia la forma de redactar. Pídeselo al equipo técnico.

### ✅ I-03 — El coach repregunta sobre lo más flojo, sin "delatar" la rúbrica
**Qué es:** cuando tu respuesta es floja en algún aspecto, el coach te invita a profundizar **en ese
aspecto**, pero **nunca** te muestra la rúbrica, los criterios ni los puntajes.
1. En simulación, responde a la pregunta de la campaña con algo **incompleto** a propósito.
2. Lee la respuesta del coach.
3. **Deberías ver:** una invitación a mejorar enfocada en lo que faltó, **en lenguaje natural**.
4. **Revisa con lupa:** en ningún mensaje deben aparecer palabras como "rúbrica"/"criterio", ni notas
   tipo "3/5" o "70%".
**Algo va mal si:** el coach menciona la rúbrica, nombra los criterios o muestra un puntaje. (Esto es
grave: avisar de inmediato.)

### ✅/🔧 I-18 — Afinar varias ideas, una por una

**Qué es:** cuando una respuesta contiene varias ideas, el coach trabaja primero una y luego la otra,
sin responder por ti. Viene apagada para las campañas existentes y solo debe encenderse en una
campaña de pruebas hasta completar calibración, pruebas con usuarios y revisión de costo.

1. Abre **Campañas**, entra a una campaña de prueba y, en **Configuración → Conversación**, activa
   **Detectar varias ideas** y **Afinar ideas una por una**. Define dos oportunidades de mejora y,
   si quieres probar el vencimiento, un tiempo corto por idea.
2. Desde simulación o WhatsApp, responde una pregunta con dos ideas distintas pero incompletas.
3. **Deberías ver:** una conversación natural sobre la primera idea, con una sola pregunta útil. No
   debe decir “Registramos 2 ideas”, mostrar notas ni ofrecer cerrar de inmediato.
4. Mejora la primera. Al alcanzar el nivel esperado —o escribir **“así está bien”**— debe pasar a la
   segunda, sin perder lo anterior.
5. Para comprobar el tiempo, deja vencer una idea: si la conversación de WhatsApp sigue abierta,
   debe llegar una sola pregunta sobre la siguiente idea; fuera de esa ventana no debe enviar texto
   libre y retomará de forma segura cuando la persona vuelva a escribir.
6. Termina la segunda. Solo entonces debe aparecer la siguiente pregunta de la campaña. En
   **Resultados**, cada idea debe conservar su versión inicial, sus mejoras y cuál quedó vigente.

**Algo va mal si:** mezcla ambas ideas, escribe una respuesta por ti, hace varias preguntas a la vez,
ignora tu última mejora, cierra toda la pregunta al decir “así está bien”, envía texto fuera de la
ventana de WhatsApp o salta una idea.

### 🔧 I-05 — El coach parafrasea ("esto es lo que entendí")
**Qué es:** que el coach te devuelva un resumen fiel de tu idea antes de la retroalimentación. **Viene
apagado**; pídele al equipo que lo **encienda en una campaña de prueba**.
1. Con la mejora encendida, responde con una idea concreta.
2. **Deberías ver:** un pequeño resumen "esto es lo que entendí…" fiel a lo que dijiste, breve, y
   **sin inventar** datos que no diste.
3. Pídele al equipo que lo **apague**: el coach vuelve a responder como antes (sin el resumen).
**Algo va mal si:** el resumen inventa cosas o es larguísimo.

### 🔧 I-06 — Detectar varias ideas en un solo mensaje
**Qué es:** si escribes **varias ideas** juntas, el sistema las separa y las trabaja una por una.
**Viene apagado**; pídele al equipo que lo encienda en una campaña de prueba.
1. Con la función encendida, envía un mensaje con **dos ideas claras** distintas.
2. **Deberías ver:** que el sistema las trata como **dos aportes separados** (dos evaluaciones / dos
   resúmenes), no como uno solo.
3. Envía un mensaje con **una sola** idea: debe quedar como **uno** solo (no partirlo en pedazos).
**Algo va mal si:** parte una idea sencilla en trozos, o fusiona dos ideas muy distintas en una.

### ✅/🔧 I-01 + P-13 — Cerrar antes cuando la respuesta ya está muy buena (umbral)
**Qué es:** si una respuesta **supera cierto nivel de calidad**, el coach felicita y avanza sin insistir.
El "nivel" se puede fijar **por campaña** (o incluso por pregunta). **La acción de cerrar viene apagada**
hasta calibrarla; pídele al equipo que la active en una campaña de prueba con un nivel elegido.
1. Con el umbral activo, responde con algo **muy completo**.
2. **Deberías ver:** que el coach **felicita y avanza** sin pedirte más mejoras.
3. Responde flojo en otra: debe **ofrecerte mejorar** normalmente.
**Algo va mal si:** cierra demasiado pronto con respuestas flojas, o nunca cierra.

### ✅ Cierre natural del chat ("no más" / silencio) *(ya existía: I-02/I-07)*
**Qué es:** puedes terminar cuando quieras y el sistema se despide con naturalidad.
1. Estando en una mejora, escribe algo como **"así está bien"** o **"listo"**.
2. **Deberías ver:** que el sistema lo toma como "quiero seguir/terminar", te da un cierre amable y
   guarda lo aportado, **sin volver a evaluar** ese último mensaje.
**Algo va mal si:** ignora tu intención y te sigue pidiendo más.

---

## C. Guardarraíles de seguridad y costo (para el equipo, pero puedes verlos)

### 🔧 P-10 — Límites por participante y control de costo
**Qué es:** topes para que nadie mande mensajes sin fin ni dispare el gasto. **Vienen apagados**;
se encienden por campaña.
1. Con los cupos encendidos y un límite bajo de prueba, haz que un participante mande **más mensajes
   del límite**.
2. **Deberías ver:** que a partir del tope el sistema **deja de responder/evaluar** de forma controlada
   (no se cuelga, no gasta de más).
**Algo va mal si:** sigue respondiendo/evaluando sin límite.

### 🧭 P-27 — Entender solicitudes naturales de parar o avanzar
**Qué es:** una ayuda opcional para interpretar una frase corta cuando el participante está mejorando
una idea. El modelo propone una etiqueta; el sistema decide siempre qué cerrar.

1. En una campaña de prueba, activa el checkbox **“Interpretar solicitudes de parar o avanzar escritas
   libremente”** y el interruptor global correspondiente. Lleva una idea a la pregunta de mejora.
2. Escribe “quiero parar aquí”, “stop now” y “no más”. **Deberías ver:** se cierra solo la idea o la
   participación según la frase, sin guardar esa orden como contenido ni pedir otra mejora.
3. En otro intento escribe “I think I should stop for today” y “ya no sé si seguir / I need a break”.
   **Deberías ver:** cierre seguro o el menú 1/2/3; nunca un cierre de campaña.
4. Escribe “hay que parar la máquina durante el mantenimiento”. **Deberías ver:** se trata como aporte
   de la idea, no como orden de salida.
5. Con cupos de llamada o presupuesto bajos, intenta una frase libre. **Deberías ver:** no llama al
   clasificador; conserva el comportamiento seguro y deja trazabilidad técnica sin texto del mensaje.

**Algo va mal si:** una frase de contenido cierra algo, la orden se evalúa como idea, aparece un
puntaje/rúbrica en la respuesta, se registra el texto libre en telemetría o el modelo cierra una campaña.

### ✅/🔧 DT-P27-01 — Cambiar las frases de salida sin cambiar el programa

**Qué es:** el equipo técnico ya puede preparar una lista global de frases para terminar una idea y
otra para terminar la participación. Si no prepara ninguna, siguen funcionando exactamente las frases
actuales. Esta prueba debe hacerse en un ambiente de pruebas y con la interpretación libre de P-27
apagada, para comprobar solo las listas.

1. Sin listas personalizadas, abre una conversación de prueba y llega a una pregunta de mejora.
   Escribe **“quiero parar aquí”**. **Deberías ver:** se termina solo esa idea, como antes.
2. En otro intento escribe **“no más”**. **Deberías ver:** termina la participación actual, también
   igual que antes.
3. Pide al equipo técnico cargar temporalmente **“cerrar esta propuesta”** para terminar la idea y
   **“terminar el ejercicio”** para terminar la participación.
4. Repite dos conversaciones y escribe esas frases con mayúsculas, tildes o signos, por ejemplo
   **“¿Cerrar ésta propuesta!”**. **Deberías ver:** ambas se entienden correctamente.
5. Mientras están las listas temporales, **“quiero parar aquí”** ya no debe actuar como alias fijo:
   la lista nueva reemplaza la anterior. Después pide retirar las listas temporales y comprueba que
   **“quiero parar aquí”** vuelve a funcionar.
6. Pide al equipo técnico preparar una lista de prueba con la misma frase dos veces, aunque cambie una
   tilde o un signo. **Deberías ver:** la aplicación sigue disponible y se usan las frases conocidas,
   no la lista defectuosa.
7. Pide revisar el registro técnico de inicio. **Deberías ver:** que indica cuál de las dos listas se
   aplicó, quedó en la línea base o fue descartada y por qué, pero nunca muestra las frases completas.
8. Para volver a una prueba anterior, pide restaurar la versión anterior de ambas listas y reiniciar la
   aplicación; para volver a lo original, pide retirar ambas listas. **Deberías ver:** que las frases
   de esa versión, o las originales, vuelven a responder igual que antes.

**Algo va mal si:** dejar las listas vacías rompe las frases actuales, la frase personalizada se guarda
como parte de la idea, mayúsculas o tildes cambian el resultado, una lista con error queda activa, el
registro muestra las frases completas, se activa la interpretación libre sin pedirlo, o la aplicación
deja de iniciar.

### ✅ Salud del sistema (lo que quedó de P-09)
**Qué es:** una comprobación simple de que el sistema está "vivo".
1. Pídele al equipo la dirección de **estado de salud** del sistema (o míralo en el panel de Azure).
2. **Deberías ver:** un "OK" / estado 200.
**Algo va mal si:** no responde o marca error.

---

## D. Validaciones operativas pendientes

- 🚧 **P-32 — Conversación en español/inglés y textos editables:** **4/4 DONE local; falta validación
  operativa y `DT-P32-02`.** Primero debe quedar disponible este recorrido simple: crear una semilla
  base, descargar el JSON completo, cambiar varios mensajes/frases, cargarlo, revisar errores y
  confirmarlo como borrador nuevo. La carga nunca publica. Usar `QAS/22`; después ejecutar `QAS/16`
  y el prompt `QAS/17_*`: dos participantes `es/en`, lote mixto, activación/rollback, campaña
  incompleta, D5 y UAT. **No aprobar ni activar en producción** hasta que todo esté green y existan
  plantillas Meta inglesas, costo/latencia, UAT y acta; fuera de la ventana la palanca sigue apagada.

- ✅ **P-28 — Volver a saludar antes de una idea nueva:** **implementada localmente (3/3); viene
  apagada.** Pide al equipo que la active solo en una prueba. Con una persona que ya terminó sus
  ideas en una campaña continua, escribe **“Hola”**. Debe llegar una bienvenida y no debe aparecer
  una idea nueva en Resultados. Después envía una propuesta concreta: debe abrirse una idea nueva,
  separada de la anterior. Si esa persona participa en dos campañas elegibles, primero debe aparecer
  una lista para escoger; al elegir, llega la bienvenida, pero el saludo no se guarda como aporte.
  Apaga la función al terminar. **Algo va mal si:** el saludo se evalúa o aparece como idea, escoge una
  campaña sin preguntar, mezcla la idea nueva con la cerrada, o responde a alguien no autorizado.
- ✅ **P-26 — Volver después con ideas nuevas:** **implementada (6/6 cortes, local).** Prueba:
  (1) en Campañas → Configuración, activar **“Permitir nuevas ideas después de finalizar”** (bloque
  propio, separado del estado de la campaña); (2) terminar una idea y enviar otra: debe comenzar
  **separada**, sin mezclarse con la anterior; (3) asociar el mismo teléfono a dos campañas activas y
  verificar que el sistema **pregunta cuál corresponde** con una lista numerada y **no pide repetir
  el aporte** —al responder el número, procesa la idea que ya habías escrito—; (4) con varias
  preguntas, elegir también la pregunta; (5) responder al coach y comprobar que **no vuelve a
  aparecer ninguna lista**; (6) escribir “quiero complementar la anterior” y verificar que retoma esa
  misma idea en vez de crear una vacía; (7) escribir “otra campaña” y ver que ofrece el menú **sin
  cerrar** la idea en curso; (8) apagar el interruptor: la idea abierta puede terminar, pero no se
  abren nuevas. **Algo va mal si:** una campaña cerrada aparece en la lista, se mezclan ideas, se
  pierde el aporte original, se repite el menú durante el coaching, “complementar la anterior” crea
  una idea vacía, o apagar el interruptor corta una idea que ya estaba en curso.
- ✅ **P-29 — Despedida amable cuando la conversación se queda quieta:** **implementada localmente
  (2/2); viene apagada.** El cierre por inactividad ya existía y no cambia: lo nuevo es un mensaje de
  pausa. Prueba: (1) con la función apagada, empieza una idea y deja de responder el tiempo de
  inactividad configurado —la conversación se cierra sola, tu idea queda guardada como pendiente y
  **no llega ningún mensaje**—; (2) pide al equipo que la encienda solo para la prueba y repite: ahora
  debe llegar **un solo** mensaje cálido que invita a retomar cuando quieras; (3) espera más tiempo:
  **no debe llegar un segundo mensaje**; (4) si pasaron más de 24 horas desde tu último mensaje, no
  llega nada, pero la idea igual queda guardada; (5) en una campaña que el administrador haya cerrado,
  tampoco llega el aviso; (6) escribe después una propuesta nueva: debe abrirse una idea nueva,
  separada de la anterior. Apaga la función al terminar. **Algo va mal si:** llegan dos o más mensajes
  de pausa, el mensaje menciona notas, puntajes o criterios de evaluación, hace una pregunta que
  espera respuesta, la idea desaparece o cambia de estado, o el mensaje llega en una campaña ya
  cerrada.
- ✅ **P-30 — Retomar una idea anterior:** **implementada localmente (3/3); viene apagada.** Pide al
  equipo que la active solo en una campaña de prueba que tenga, para el mismo participante, al menos
  dos ideas anteriores sobre la misma pregunta (pueden estar terminadas, pendientes o descartadas).
  (1) Escribe **“quiero retomar una idea”**; (2) si participaste en varias campañas o preguntas, elige
  primero el alcance; (3) debe aparecer una lista numerada con resúmenes breves y estados neutrales,
  nunca notas; (4) responde con el número o copia exactamente un resumen; (5) debe mostrarse la idea
  elegida y preguntarte qué deseas agregar o cambiar; (6) envía la mejora y revisa **Resultados**: debe
  actualizarse la misma idea y conservarse su historial, no aparecer otra idea nueva. Apaga la función
  y repite: el selector histórico ya no debe aparecer. **Algo va mal si:** ves ideas de otra persona,
  campaña o pregunta; el sistema adivina una opción ambigua; la orden o el número aparecen como aporte;
  se crea otra idea; se pierde la versión anterior; o una idea madura sigue en curaduría mientras se
  está modificando.
- ⏳ **I-12 — Ideas semilla:** en espera del material de Felipe. Cuando llegue: el coach se apoyará en
  esos temas guía. Prueba mínima hoy: una campaña **sin** ideas semilla funciona igual que siempre.
- 💤 **I-09 / I-10 — "Tejido colectivo"** (que el coach use aportes de otros): **aplazado** para después
  de la convención. Debe estar **apagado**; en el repaso previo, confirmar que ninguna campaña lo tiene
  encendido.
- 💤 **P-07 (aviso de privacidad), panel en vivo de P-09, P-04/P-05/P-06/P-08/P-11, I-15 (nombre):**
  **aplazados**; no se prueban para la convención.
- 📄 **I-04 (tono del saludo), I-11 (rúbrica), I-13 (tipo de rúbrica), P-01/P-02 (WhatsApp/Meta):** no
  son "software para probar aquí" — son contenido, decisiones o configuración que define GHT/el equipo.
  Se validan en las pruebas conjuntas.
- ⏳ **I-14 (etiquetas):** el sistema ya permite usarlas, pero falta que GHT entregue el catálogo
  consolidado. No se cargarán etiquetas inventadas. Cuando GHT lo entregue, la prueba será: (1) revisar
  que estén disponibles exactamente las etiquetas recibidas, con su estado correcto; (2) cargar o editar
  un participante y asignarle una de ellas; (3) guardar y volver a abrirlo para confirmar que sigue
  asignada; y (4) usar la etiqueta para encontrar a ese participante o sus resultados. **Algo va mal si:**
  falta una etiqueta del catálogo, aparece una que GHT no entregó, se pierde al guardar o el filtro no
  encuentra al participante correspondiente.

---

### P-21 - Usar el numero correcto de WhatsApp

**Que es:** una campana puede salir por el numero de pruebas o por el de produccion, y el bot siempre responde por el mismo numero al que la persona escribio.

1. Pidele al equipo que configure dos numeros de prueba y uno con un alias, por ejemplo **qas**. En **Campanas**, abre una campana de prueba, escribe **qas** en **Alias del numero de envio** y guarda.
2. Desde **Envios**, manda el mensaje inicial a un telefono de prueba. Debe llegar desde el numero asociado al alias.
3. Borra el alias, guarda y repite el envio: debe salir desde el numero predeterminado.
4. Responde desde el telefono: la pregunta, retroalimentacion y cierre deben llegar desde ese mismo numero.

**Algo va mal si:** el mensaje inicial usa otro numero, una respuesta cambia de numero a mitad de la conversacion, o un alias vacio no usa el predeterminado. No escribas ids tecnicos de Meta: usa solo aliases configurados por el equipo.

## Si encuentras un problema
Anótalo con: **qué probabas**, **qué hiciste**, **qué esperabas ver** y **qué viste**. Si es de
**seguridad o privacidad** (por ejemplo, que aparezca la rúbrica, un puntaje, o datos de otra persona),
márcalo como **urgente**. Puedes registrarlo con el formato de `05_Plantillas_Defecto_y_Bitacora.md`.
