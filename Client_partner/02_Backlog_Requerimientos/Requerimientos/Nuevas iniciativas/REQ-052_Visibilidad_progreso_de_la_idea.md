# REQ-052 · Visibilidad del progreso de la idea (consolidación al alcanzar un umbral)

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | Propuesto |
| **Solicitado por / Fecha** | GHT · 2026-08-06 |
| **Estimación** | Por estimar (T&M) |

## Qué se necesita
Los participantes quieren mejor visibilidad del progreso de sus ideas. Hoy trabajan a ciegas: el
sistema mantiene una versión consolidada de la idea, pero solo se la muestra al pedir confirmación o
al reabrir una idea cerrada. Cuando la idea alcanza el umbral de madurez, el hilo se cierra sin que la
persona vea el resultado de su trabajo ni pueda decidir si quiere seguir.

Se pide que, al alcanzar un umbral predefinido de la rúbrica, el sistema presente **de forma
proactiva** la consolidación de la idea en la que se está trabajando y pregunte si quiere continuar
madurándola.

## Alcance
- Incluye: un umbral propio y configurable que dispara el envío; presentación del texto consolidado
  vigente tal cual lo guarda el sistema; pregunta de continuidad; envío una sola vez por idea;
  interruptor de encendido/apagado y overrides por campaña y por pregunta.
- No incluye: cambiar el umbral de madurez ni la forma en que se clasifican las ideas maduras;
  mostrar puntajes, criterios o porcentajes de rúbrica al participante; resúmenes periódicos por
  tiempo o por número de turnos; consulta bajo demanda ("muéstrame cómo va mi idea"), resuelta por
  separado en REQ-054/P-33; traducción o localización del mensaje.

## Criterios de aceptación
- [ ] Al alcanzar el umbral definido, el participante recibe la consolidación de su idea tal como el
      sistema la tiene registrada, sin que el modelo la altere.
- [ ] En el mismo mensaje se le pregunta si quiere seguir puliéndola o dejarla así.
- [ ] Responder que ya está conforme cierra la idea; responder con más contenido la mejora y la vuelve
      a evaluar.
- [ ] El mensaje llega una sola vez por idea.
- [ ] El umbral que dispara este mensaje es independiente del umbral que clasifica una idea como
      madura, y ambos se pueden ajustar por campaña y por pregunta.
- [ ] Ningún mensaje menciona rúbrica, criterios, puntajes ni porcentajes.
- [ ] Con la funcionalidad apagada, el comportamiento es exactamente el actual.

## Aprobación
Solicitado por GHT el 2026-08-06. Pendiente de aprobación T&M · relacionado con [[REQ-037]] (I-19
consolidación progresiva) y [[REQ-025]] (I-17 dos niveles de madurez).

## Especificación técnica
`Especificaciones/Iniciativas/P-31_Resumen_Consolidacion_Por_Umbral.md` (ESPECIFICADA, 3 cortes,
kill-switch OFF).

## Nota
La segunda solicitud recibida el mismo día —**soporte de inglés en el chatbot**— se registrará y
especificará por separado; requiere análisis previo del alcance real (plantilla HSM, prompts,
vocabularios deterministas y contenido de campaña).
