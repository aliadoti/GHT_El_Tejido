# 16 — P-32: conversación en español/inglés y textos editables

**Estado:** corte 1 y corte 2a DONE local. La API, semillas, caché/LKG, emergencia, snapshot de
idioma por hilo y gate OFF están implementados; el catálogo aún no alimenta las salidas visibles ni
el portal. No se pueden aprobar las pruebas bilingües de punta a punta hasta completar los cuatro
cortes.

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

## Prueba 3 — cambiar un texto sin desplegar

1. Abre **Textos de conversación**, elige el idioma inglés y crea una versión borrador desde la
   versión activa.
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
3. Activa una versión válida y luego usa **Volver a esta versión** sobre la anterior.
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
