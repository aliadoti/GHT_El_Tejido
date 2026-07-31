# P-21 · Multi-número de WhatsApp

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Media |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT · — |
| **Estimación** | Por estimar (T&M) |

## Qué se necesita
Soportar varios números de WhatsApp en la misma WABA: responder por el número entrante y permitir un número saliente por campaña.

## Alcance
- Incluye: captura de `phone_number_id` entrante; número saliente por campaña (`numeroWhatsAppSaliente`)
- No incluye: Sin secretos nuevos; ausencia de config conserva el número predeterminado. La respuesta conversacional sale siempre por el número entrante.

## Criterios de aceptación
- [x] El sistema responde por el número por el que llegó el mensaje.
- [x] El envío inicial puede usar el número saliente configurado por campaña.

## Aprobación / Referencia
Estado de desarrollo: DONE local 2026-07-25; backend 473/473 verde
Spec detallada: `Especificaciones/Iniciativas/P-21_Multi_Numero_WhatsApp.md`
