# 08 — Cómo probar cada iniciativa (en palabras simples)

> **Para quién es esto:** cualquier persona del equipo, **sin necesidad de saber de programación**,
> que quiera comprobar que cada mejora funciona. Cada punto dice **qué abrir, qué hacer y qué deberías
> ver**. Si algo no se comporta así, es señal de que hay un problema y hay que avisar al equipo técnico.
> Última actualización: 2026-07-27 (I-19 especificada; implementación pendiente de aprobación).

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
1. En el portal, ve a la pantalla de **Usuarios** y busca la opción de **cargar archivo**.
2. Sube un archivo con la lista (nombre, WhatsApp, área, empresa, etiquetas).
3. **Deberías ver:** un **resumen** que dice cuántos se crearon, cuántos se actualizaron y cuántos se
   rechazaron, con el **motivo** de cada rechazo (por ejemplo, un número mal escrito).
4. Vuelve a subir el mismo archivo: **no debe duplicar** a nadie.
**Algo va mal si:** una fila con un error tumba toda la carga (debería rechazar solo esa fila), o se
crean participantes repetidos.

### ✅ P-03 — Reiniciar datos para volver a probar
**Qué es:** dejar una conversación "como nueva" sin borrar la campaña, para repetir pruebas.
1. En el portal (Envíos o Resultados), usa **"Reiniciar conversación"** de un participante, o
   **"Reiniciar datos"** de toda la campaña.
2. Confirma la acción.
3. **Deberías ver:** que desaparecen las respuestas/resultados de ese participante, **pero la campaña,
   sus preguntas y los usuarios siguen ahí**.
4. Escribe de nuevo como ese participante: el sistema te vuelve a saludar y hacer la pregunta desde cero.
**Algo va mal si:** se borra la campaña o su configuración, o si el participante no puede volver a empezar.

### ✅ I-16 — La calificación correcta queda en el resumen
**Qué es:** un arreglo para que el resumen final (Markdown) muestre **la última** calificación, no una vieja.
1. Haz que un participante responda y sea evaluado; luego que envíe una versión mejorada.
2. Abre el **resumen/resultado** de esa respuesta en el portal.
3. **Deberías ver:** que la calificación y el texto corresponden a **la última** evaluación, no a un intento anterior.
**Algo va mal si:** el resumen muestra una nota que ya no corresponde.

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

### 📝 I-19 — Construir y calificar una sola idea completa

**Qué es:** cada respuesta del participante complementa la misma idea. El sistema devuelve lo que
entendió para confirmarlo y califica la versión completa, no solo el último mensaje. Esta prueba se
ejecutará cuando la implementación sea autorizada; hoy sirve como criterio de revisión.

1. Responde una pregunta con una idea incompleta. **Deberías ver:** una paráfrasis de lo entendido y
   una petición de confirmación.
2. Corrige un dato. **Deberías ver:** una nueva paráfrasis completa, con la corrección y sin perder lo
   anterior.
3. Confirma. Solo entonces debe llegar la evaluación o una pregunta para mejorar.
4. Responde únicamente con el dato faltante. La siguiente paráfrasis debe contener la idea completa,
   no solo esa última frase.
5. Termina la idea y abre **Resultados**. **Deberías ver:** una sola fila, marcada como madura,
   pendiente o rechazada, con los aportes/versiones dentro del detalle.
6. Escribe **“quiero complementar la anterior”**. Debe reabrir esa misma idea, volver a confirmar la
   nueva versión y recalificarla.
7. Si queda madura, debe decir **Pendiente de curaduría**; no debe enviarse automáticamente a
   implementación, conocimiento o acta.

**Algo va mal si:** una frase complementaria aparece/califica como otra idea; el sistema olvida o
inventa información; evalúa antes de confirmar; muestra una fila por revisión; conserva una nota de
otra versión; o publica sin curaduría.

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

### ✅ Salud del sistema (lo que quedó de P-09)
**Qué es:** una comprobación simple de que el sistema está "vivo".
1. Pídele al equipo la dirección de **estado de salud** del sistema (o míralo en el panel de Azure).
2. **Deberías ver:** un "OK" / estado 200.
**Algo va mal si:** no responde o marca error.

---

## D. Aún no se prueba (por ahora)

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
