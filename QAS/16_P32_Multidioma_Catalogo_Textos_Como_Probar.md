# 16 — P-32: conversación en español/inglés y textos editables

**Estado:** cortes 1 a 4 DONE local. La API, semillas, caché/LKG, emergencia, snapshot de idioma,
mensajes globales, enrutamiento, detectores, localizaciones de campaña, envío inicial mixto y los
contextos LLM están implementados. El gate sigue OFF; la activación requiere una prueba controlada de
ambos idiomas, plantillas Meta aprobadas, D5/UAT y revisión de costo.

## Qué se quiere comprobar

Que cada persona reciba toda la conversación en el idioma de su ficha y que un administrador pueda
cambiar un texto, publicarlo y revertirlo sin pedir una compilación o un despliegue.

## Preparación

1. Crea dos participantes de prueba: uno con idioma **Español (`es`)** y otro con **English (`en`)**.
2. Usa una campaña de prueba con versión española e inglesa completa.
3. Confirma con el equipo que hay una plantilla inicial aprobada por Meta para cada idioma.
4. No uses datos reales ni números de participantes de la convención.

## Prueba 0 — snapshot del hilo (disponible desde corte 2a)

1. Crea o selecciona un participante de prueba con idioma `en` y abre un hilo/ciclo nuevo.
2. Consulta `GET /api/admin/conversaciones/{id}?campaniaId=...` como administrador y verifica
   `conversacion.idioma`.
3. Cambia el maestro del participante a `es` y continúa el mismo hilo; después ciérralo o reinícialo
   y abre un ciclo nuevo.

**Deberías ver:** el hilo existente conserva `en`; el ciclo nuevo queda con `es`. Mientras
`Conversacion:CatalogoTextosHabilitado=false`, ambos siguen mostrando el flujo legacy en español:
esto es la regresión segura esperada, no un fallo de traducción.

## Prueba 0.1 — mensajes globales conectados (técnica, corte 2b1)

1. En un entorno de prueba aislado, prepara un catálogo inglés activo con un saludo y una variante de
   continuación reconocibles.
2. Con los demás cambios de P-32 aún incompletos, **no actives el gate fuera de esa prueba técnica**.
3. Abre un hilo nuevo de un participante `en` y provoca una repregunta.

**Deberías ver:** el saludo global y la coletilla de continuación salen del catálogo inglés. La
pregunta y el contenido de campaña pueden seguir en español hasta el corte 3; por eso esta prueba no
autoriza activar el catálogo en un ambiente compartido.

## Prueba 0.2 — menú pendiente con snapshot (técnica, tramo 2b2)

1. En un entorno aislado y con el catálogo inglés activo, usa dos campañas elegibles para un
   participante `en` y envía un aporte para provocar el menú de campaña.
2. Cambia el idioma de ese participante a `es` en el maestro antes de responder un valor inválido al menú.

**Deberías ver:** ambos menús siguen en inglés. La selección pendiente conserva el idioma que tenía
cuando nació; el cambio del maestro solo aplica a rutas y ciclos nuevos. Con el gate OFF, se conserva el
texto español heredado como regresión segura.

## Prueba 0.3 — comandos y aclaración P-27 en inglés (técnica, corte 2b2)

1. En un entorno aislado con catálogo inglés activo, abre un hilo `en` que esté esperando una mejora.
2. Envía una frase corta de salida como `stop now`; repite el recorrido con una frase ambigua para abrir
   el menú de aclaración.

**Deberías ver:** `stop now` termina el recorrido sin evaluar ese texto como una idea. La aclaración
pregunta en inglés qué hacer y las opciones 1, 2 y 3 conservan el mismo efecto que en español. Con el
gate OFF, el sistema mantiene el texto y la interpretación heredados en español.

## Prueba 1 — mismo recorrido, dos idiomas

1. Envía el mensaje inicial a los dos participantes.
2. Responde desde ambos teléfonos con una idea equivalente.
3. Sigue el coaching, pide mejorar, termina la idea y vuelve a entrar.
4. Provoca también una selección de campaña/pregunta y una opción inválida.

**Deberías ver:** el recorrido del participante `es` está completo en español y el del participante
`en` está completo en inglés: saludo, pregunta, ayudas, coaching, cierre y reingreso. Las ideas se
guardan en el idioma en que fueron escritas.

**Algo va mal si:** aparece una frase española en el hilo inglés, el sistema traduce el aporte, mezcla
los dos idiomas o toma decisiones distintas para ideas equivalentes.

## Prueba 2 — lote mixto de WhatsApp

1. Desde **Envíos**, selecciona los dos participantes y envía el mismo mensaje inicial.
2. Revisa el mensaje recibido y el estado individual del envío.

**Deberías ver:** cada teléfono recibe la plantilla aprobada de su idioma y ambos envíos quedan
registrados por separado.

**Algo va mal si:** todo el lote usa una sola plantilla, el inglés recibe español o el error de una
plantilla detiene también al otro participante.

### Configuración previa del lote mixto (corte 3)

1. En **Campañas**, abre la campaña y entra a **Textos por idioma**.
2. Marca inglés, completa todos los campos de español e inglés y guarda. Cada mensaje inicial debe
   tener un alias de plantilla; no escribas claves ni secretos en esa pantalla.
3. En el ambiente de prueba, configura el mapeo de cada alias e idioma con la plantilla Meta ya
   aprobada. Ejemplo: `WhatsApp__PlantillaEnvioInicial__Mapeos__inicio_campania__en__Nombre`.
4. Prueba primero con la palanca multidioma apagada: ambos participantes conservan el envío histórico
   en español. Enciéndela solo en un entorno aislado cuando el catálogo global esté listo.

**Deberías ver:** al encender la palanca en pruebas, cada participante recibe la plantilla de su
idioma. Si falta contenido o mapeo inglés, solo ese envío queda en error; los demás continúan.

## Prueba 3 — cambiar un texto sin desplegar

1. Abre **Textos de conversación**, elige el idioma inglés y crea una versión borrador desde la
   semilla si aún no existe una versión; si existe, selecciona el borrador de la lista.
2. Cambia un saludo visible, guarda y usa la vista previa.
3. Comprueba que el borrador todavía no afecta la conversación.
4. Activa la versión y abre un hilo nuevo en inglés.

**Deberías ver:** el borrador no cambia nada; después de activarlo, el hilo nuevo muestra el texto
editado sin compilación ni despliegue.

**Algo va mal si:** guardar el borrador lo publica, hace falta reiniciar/desplegar o cambia también el
texto español.

## Prueba 4 — validación y rollback

1. Intenta guardar una versión con un campo obligatorio vacío, un placeholder inventado o una frase
   duplicada.
2. Confirma que el portal explica el error y que la versión activa sigue intacta.
3. Activa una versión válida y luego usa **Reactivar esta versión** sobre la anterior.
4. Abre un hilo nuevo.

**Deberías ver:** el contenido inválido nunca se publica y el rollback restaura el texto anterior sin
desplegar. El historial de versiones sigue visible.

**Algo va mal si:** se activa solo una parte, desaparece el historial o hay que copiar manualmente el
texto anterior.

## Prueba 5 — cambio de idioma del maestro

1. Abre un hilo con el participante en español.
2. Mientras el hilo está abierto, cambia su ficha a inglés y continúa el hilo.
3. Termina/reinicia la conversación y abre un ciclo nuevo.

**Deberías ver:** el hilo abierto termina en español; el hilo/ciclo nuevo comienza en inglés.

**Algo va mal si:** el idioma cambia a mitad de una selección o el ciclo nuevo sigue usando el idioma
anterior.

## Prueba 6 — campaña incompleta

1. Deja a propósito una pregunta o cierre inglés sin diligenciar en una campaña de prueba.
2. Intenta activar la campaña o asociar/enviar al participante inglés.

**Deberías ver:** la operación se bloquea con una explicación clara del contenido faltante. No se
envía español como sustituto.

**Algo va mal si:** la campaña se activa incompleta, el participante recibe una mezcla o el sistema
inventa una traducción.

## Evidencia mínima

- capturas de los dos hilos completos;
- versión y estado del catálogo usado;
- resultado del lote mixto;
- captura del rechazo de contenido inválido;
- prueba de activación y rollback; y
- confirmación de que no hubo build, despliegue ni cambio de secretos.
