# 16 — P-32: conversación en español/inglés y textos editables

**Estado:** cortes 1 a 4 DONE local. La API, semillas, caché/LKG, emergencia, snapshot de idioma,
mensajes globales, enrutamiento, detectores, localizaciones de campaña, envío inicial mixto y los
contextos LLM están implementados. El gate sigue OFF. Esta guía deja lista la validación operativa
pendiente; la activación productiva requiere una prueba controlada de ambos idiomas, plantillas Meta
aprobadas, D5, UAT, revisión de costo/latencia y acta de cambio.

## Qué se quiere comprobar

Que cada persona reciba toda la conversación en el idioma de su ficha y que un administrador pueda
cambiar un texto, publicarlo y revertirlo sin pedir una compilación o un despliegue.

## Preparación

1. Crea dos participantes de prueba: uno con idioma **Español (`es`)** y otro con **English (`en`)**.
2. Usa una campaña de prueba con versión española e inglesa completa.
3. Confirma con el equipo que hay una plantilla inicial aprobada por Meta para cada idioma.
4. No uses datos reales ni números de participantes de la convención.
5. Define una ventana aislada y autorizada. El responsable humano activa temporalmente
   `Conversacion:CatalogoTextosHabilitado=true` solo allí; fuera de esa ventana debe permanecer en
   `false`.
6. Conserva la versión activa actual de cada idioma y anota quién aprobó la prueba. No cambies
   secretos, URLs, rúbricas ni prompts para hacer que una prueba pase.
7. Si la ejecución será contra Azure, un operador autorizado habilita temporalmente
   `Simulacion__Habilitada=true` y entrega al proceso del agente la clave como variable secreta
   `GHT_DIAG_KEY`. El agente solo la usa como header `X-Diag-Key`; nunca debe verla en texto, buscarla
   en Key Vault ni registrarla. Al terminar, el operador apaga la simulación y elimina la variable de
   la sesión. El procedimiento humano completo está en `QAS/18_Runbook_Humano_Lanzar_Prueba_P32.md`.

## Orden de ejecución completo

Ejecuta primero la **Prueba 0** con la palanca apagada; debe comprobarse que el comportamiento
histórico no cambió. Después, en la ventana autorizada y con la palanca encendida, ejecuta las
pruebas 1 a 6. Completa luego D5 y UAT de esta misma guía. Si falta una plantilla Meta, una
traducción aprobada, acceso o autorización, marca el caso como **BLOCKED**; no lo conviertas en PASS
ni en FAIL técnico.

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
2. Conserva la prueba dentro de la ventana aislada autorizada; **no actives el gate fuera de ella**.
3. Abre un hilo nuevo de un participante `en` y provoca una repregunta.

**Deberías ver:** el saludo global y la coletilla de continuación salen del catálogo inglés. La
pregunta y el contenido de campaña también deben estar en inglés cuando la localización está completa.
Esta prueba aislada no autoriza activar el catálogo en un ambiente compartido.

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
desplegar. El historial de versiones sigue visible y la bitácora registra el rollback, sin guardar el
contenido editorial.

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

1. Deja a propósito una pregunta o cierre inglés sin diligenciar en una campaña de prueba que tenga
   inglés habilitado.
2. Intenta activarla y asociar al participante inglés. Repite la activación con la palanca multidioma
   apagada: una campaña que ya declara inglés tampoco puede quedar activa incompleta para encenderla
   después.
3. Si existe una asociación histórica creada antes de la corrección, intenta iniciar la conversación.

**Deberías ver:** la activación se bloquea y la asociación devuelve una explicación clara del contenido
faltante. Una asociación histórica inconsistente no abre conversación. No se envía español como
sustituto.

**Algo va mal si:** la campaña se activa incompleta, se puede asociar el participante inglés, se abre
una conversación histórica inconsistente, el participante recibe una mezcla o el sistema inventa una
traducción.

## Prueba 7 — D5 real: calidad equivalente del modelo

Esta prueba cuesta llamadas reales al modelo y requiere las credenciales y presupuesto ya aprobados.
No se sustituye por las pruebas unitarias locales.

1. Ejecuta el banco de calibración real siguiendo `tests/Calibracion/README.md`, con los idiomas
   disponibles en el conjunto y con la configuración del ambiente aislado.
2. Añade y ejecuta, si no estaban ya incluidos, al menos cuatro pares equivalentes `es/en`: idea fuerte,
   idea débil, texto hostil/inyección y solicitud de terminar.
3. Compara por pares: decisión de estado, seguridad, ausencia de fuga de rúbrica/secretos y naturalidad.
   El texto no tiene que ser una traducción literal, pero debe conservar el mismo propósito y no mezclar
   idiomas.
4. Guarda el reporte generado, el identificador de la configuración usada y el costo/tokens observados.

**Deberías ver:** las decisiones deterministas coinciden entre idiomas, el modelo no revela información
protegida y el inglés responde naturalmente sin forzar español.

**Algo va mal si:** cambia un estado por el idioma, aparece información sensible, hay una respuesta en
el idioma equivocado o el costo/latencia supera el límite acordado. Detén la activación y conserva la
evidencia.

## Prueba 8 — UAT bilingüe: aceptación de negocio

1. Pide a una persona de GHT que complete el recorrido `es` y a otra el recorrido `en`, sin explicarles
   qué respuesta esperan.
2. Cada persona confirma que entiende saludo, pregunta, ayudas, coaching, cierre y reingreso; también
   revisa que la idea conserve el idioma que escribió.
3. El administrador repite el cambio de un saludo inglés, activación de la versión y rollback de la
   Prueba 3/4 mientras los participantes no tienen un hilo nuevo abierto.
4. Registra por separado: aceptado, observación menor, defecto o bloqueo; no sustituyas el visto bueno
   de GHT por el del ejecutor técnico.

**Deberías ver:** GHT acepta que ambos recorridos son claros y funcionalmente equivalentes. El cambio
editorial aplica a hilos nuevos y el rollback restaura el texto anterior.

## Cierre de la ventana y decisión

1. Apaga la palanca multidioma si la ventana no termina en una activación formal aprobada.
2. Conserva las versiones de catálogo y los datos de prueba; no borres evidencia para ocultar un fallo.
3. Registra el resultado en `QAS/resultados/Resultados_P32_Multidioma_<fecha>.md` con: ambiente,
   ejecutor, autorización, versiones/huellas, plantillas Meta, estado de cada prueba (PASS/FAIL/BLOCKED),
   enlaces a capturas/reportes, costo/latencia, decisión UAT y decisión final.
4. Solo se recomienda activación productiva si todas las pruebas aplicables están PASS, D5/UAT están
   aprobados y existe el acta de cambio. Cualquier FAIL o BLOCKED deja el gate OFF.

## Evidencia mínima

- capturas de los dos hilos completos;
- versión y estado del catálogo usado;
- resultado del lote mixto;
- captura del rechazo de contenido inválido;
- prueba de activación y rollback; y
- reporte D5 real con comparación `es/en`, costo y latencia;
- visto bueno o hallazgos UAT de GHT; y
- confirmación de que no hubo build, despliegue ni cambio de secretos fuera de la ventana autorizada.
