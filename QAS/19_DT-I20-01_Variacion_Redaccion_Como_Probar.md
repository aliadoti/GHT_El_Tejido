# QAS 19 — Variación y no duplicación de redacción conversacional

## Propósito

Comprobar que las conversaciones nuevas suenan menos repetitivas y que un mensaje no muestra dos veces el mismo reconocimiento.

## Preparación

- Use una campaña activa con pregunta activa y una persona de prueba.
- Repita el recorrido en una segunda campaña para confirmar que la regla es global.

## Pasos

1. Envíe una respuesta con una idea concreta.
2. Cuando llegue la pregunta para mejorarla, añada un detalle de la misma idea.
3. Lea el mensaje completo recibido: debe contener un solo reconocimiento de ese avance y una sola pregunta clara.
4. Repita el paso anterior dos o tres veces con aportes diferentes.
5. Haga el mismo recorrido en otra campaña.

## Resultado esperado

- El sistema puede usar expresiones como `Queda claro que...`, pero no abre todos los mensajes con esa fórmula.
- No aparecen dos párrafos que digan lo mismo, especialmente dos variantes de `Ya queda claro...`, dentro de un solo mensaje.
- La pregunta de mejora sigue siendo una sola y corresponde al aporte.
- El flujo sigue en la misma idea y no se cierra ni cambia de pregunta por este ajuste editorial.

## Señales de fallo

- Una misma oración o reconocimiento aparece dos veces en el mismo envío.
- Todos los turnos comienzan con la misma fórmula.
- Se pierde la pregunta de mejora o aparece más de una.
- El cambio altera el estado de la conversación, la evaluación o el contenido histórico.
