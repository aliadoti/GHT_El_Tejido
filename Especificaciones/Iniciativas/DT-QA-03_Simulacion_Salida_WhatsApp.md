# DT-QA-03 — Simulación observable de salida WhatsApp

> **Estado:** DIFERIDA POST-CONVENCIÓN — no bloquea el congelamiento condicionado.
> **Origen:** la corrida P32-20260816-1955 confirmó que `Simulacion__Habilitada` solo inyecta entrada;
> el `WhatsAppGateway` saliente continúa siendo real. El requisito humano vigente es probar la
> interacción WhatsApp simulada, sin usar el ambiente desplegado para envíos reales.

> **Decisión Convención 2026:** la campaña usa servicios reales de WhatsApp y teléfonos autorizados;
> `Simulacion__Habilitada` permanece apagada. Por ello esta capacidad no forma parte del camino de
> ejecución de la convención y se retoma después del evento.

## Objetivo

En un modo de QA explícito y seguro, sustituir el envío saliente por un doble que capture el mensaje
como evidencia consultable por el flujo de diagnóstico. No debe obtener token, contactar Graph/Meta ni
enviar a un teléfono. Fuera de ese modo, el gateway real y su comportamiento actual no cambian.

## Límites obligatorios

- El modo debe estar apagado por defecto y no puede habilitarse implícitamente por Development.
- La selección del doble ocurre en composición/DI; el dominio y el orquestador no conocen Azure ni Meta.
- La evidencia no expone secretos, tokens ni PII innecesaria y tiene retención/limpieza acotada.
- Debe quedar imposible que una llamada de simulación use simultáneamente el gateway real.
- No se inventan plantillas Meta ni se cambian los mapeos existentes `...__es/en__Nombre`, `Idioma` y
  `Componentes`; estos se validarán después contra el doble, no contra Meta.

## Criterios de aceptación

1. Una entrada simulada genera una salida observable sin tráfico externo.
2. El texto, tipo de envío y correlación necesarios para QAS quedan disponibles sin secretos.
3. Con el modo apagado, las pruebas existentes prueban que se conserva el gateway real.
4. Una configuración incompleta o no autorizada falla cerrada y no permite enviar.
5. Las pruebas integradas cubren entrada → cola → orquestador → salida simulada.

## Revalidación posterior

No se repiten los PASS sin relación. Se retoman los recorridos conversacionales P-32 que quedaron
BLOCKED por falta de aislamiento, más la regresión de `DEF-P32-04-01`; D5, UAT y aprobación manual de
plantillas Meta permanecen gates separados.
