# QAS 21 — DT-I20-02: texto plano y prompt seguro

> **Estado:** guía preparada; ejecutar después de implementar `DT-I20-02`.  
> **Objetivo:** comprobar que WhatsApp responde de forma natural, sin mostrar formato editorial ni instrucciones internas, y sin cambiar las reglas de evaluación y conversación.

## Antes de empezar

Necesitas:

- un ambiente aislado, nunca una campaña productiva;
- una campaña en español y otra en inglés, o dos hilos aislados con esos idiomas;
- una pregunta que permita aportar una idea y mejorarla;
- acceso de lectura al resultado para comparar idea, versión, evaluación y estado;
- autorización separada si la prueba usa un LLM real y genera costo.

No cambies prompts ni campañas activas de producción para ejecutar esta guía.

## Prueba 1 — Reproducir el caso reportado

1. Inicia un hilo aislado.
2. Responde con una idea equivalente a: comparar diferencias en almacenamiento de racks en el primer punto de arribo en Estados Unidos.
3. Continúa hasta recibir la retroalimentación y la siguiente pregunta.

Debe aparecer:

- un mensaje breve y natural;
- como máximo una pregunta;
- una relación clara con la idea aportada.

Es falla si aparece cualquiera de estos elementos:

- `###`;
- `Lo que ya queda claro`;
- `Lo que todavía falta`;
- `Siguiente ajuste recomendado`;
- `Pregunta clave`;
- `Estado`;
- `ready_to_save`, `save now` o una orden de guardar/cerrar;
- dos preguntas que pidan prácticamente lo mismo.

## Prueba 2 — Confirmar que no cambió la decisión de negocio

Para el mismo turno, consulta el resultado con las herramientas autorizadas y compara:

- identificador de idea;
- versión evaluada;
- puntajes;
- clasificación o madurez;
- estado del hilo;
- número de repreguntas usadas.

La corrección solo puede cambiar el fragmento visible inválido por un texto seguro. Es falla si cambia la idea o versión evaluada, se pierde un puntaje válido, se consume una repregunta adicional o el prompt decide cerrar/guardar por sí mismo.

## Prueba 3 — Una sola pregunta

1. Usa una idea que todavía necesite precisión.
2. Revisa el mensaje completo recibido.

Debe contener exactamente una pregunta útil. Es falla si la retroalimentación incluye una pregunta y después aparece otra repregunta separada, o si no aparece ninguna cuando el flujo exige continuar.

## Prueba 4 — Contenido legítimo con `#` y formato del participante

1. Aporta una idea que incluya literalmente `caja #3`.
2. Si P-33 está habilitada en el ambiente aislado, solicita ver la idea.

Debe conservarse `caja #3` exactamente. Es falla si desaparece el carácter, se cambia el texto de la idea o se aplica una limpieza general al mensaje.

## Prueba 5 — Español e inglés

Ejecuta el mismo flujo una vez en `es` y otra en `en`.

Debe cumplirse en ambos casos:

- texto natural en el idioma del hilo;
- sin encabezados, listas ni etiquetas internas;
- máximo una pregunta;
- sin mezcla de idiomas.

## Prueba 6 — Salidas y continuidad P-27

Desde un turno que espere mejora, usa las frases de continuar y terminar ya aceptadas por el sistema.

Debe ocurrir exactamente el comportamiento previo. Es falla si una instrucción del prompt ignora la intención del participante, fuerza otra pregunta o cambia el cierre decidido por el servidor.

## Prueba 7 — No duplicación I-20

Usa una idea cuya retroalimentación pueda parafrasear el cuerpo que el servidor inserta.

Debe verse una sola formulación del mismo contenido. Es falla si reaparece un puente como `Ya quedó claro que...` seguido de una segunda frase prácticamente idéntica.

## Prueba 8 — Rollback de prompt en ambiente aislado

Solo después de implementar la selección segura de versiones:

1. deja una versión anterior activa y aprobada;
2. crea una versión posterior en borrador o inactiva;
3. ejecuta el flujo;
4. confirma en telemetría autorizada que runtime usó la anterior activa/aprobada;
5. activa y aprueba la nueva versión y repite;
6. restaura el `promptRef` anterior según el runbook.

Es falla si runtime intenta usar el borrador/inactivo o si inactivar la última deja el flujo sin el prompt anterior disponible.

## Evidencia mínima

Guardar, sin datos personales:

- fecha y ambiente;
- campaña/hilo técnico anonimizado;
- idioma;
- familia y versión de prompt;
- captura o transcripción anonimizada del mensaje;
- idea/versionId y resultado funcional, sin texto sensible;
- aprobado o fallido por cada prueba;
- motivo fijo de cualquier fallback observado.

## Resultado final

La deuda puede cerrarse funcionalmente solo si:

- las ocho pruebas pasan;
- las regresiones automáticas están verdes;
- no se modificaron reglas de negocio ni contenido histórico;
- D5 no muestra degradación relevante de calidad;
- cualquier activación remota tiene aprobación humana y plan de rollback.
