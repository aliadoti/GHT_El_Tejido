# REQ-013 · Cierre conversacional por tiempo (determinístico + no determinístico)

| | |
|---|---|
| **Tipo** | Cambio |
| **Prioridad** | Alta |
| **Estado** | Propuesto |
| **Solicitado por / Fecha** | Felipe Arango (GHT) · 2026-07-31 |
| **Estimación** | Por estimar (T&M) |

## Qué se necesita
Cuando el participante deja de responder por un tiempo, el sistema cierra o pausa la conversación de forma natural, avisándole ("demos una pausa, hábleme cuando quiera / cuando tenga tiempo seguimos") en lugar de dejar la conversación colgada. Combina un disparo determinístico (por tiempo de inactividad) con una redacción no determinística (LLM) para que el cierre se sienta humano.

## Alcance
- Incluye: detección de inactividad por tiempo (parte determinística); mensaje de pausa amable redactado con LLM (parte no determinística); dejar la idea en un estado que permita retomarla luego.
- No incluye: el posterior despertar de la conversación, que se cubre en [[REQ-012]].

## Criterios de aceptación
- [ ] Tras el tiempo de inactividad configurado, el sistema envía un mensaje de pausa natural.
- [ ] El cierre combina disparo por tiempo (determinístico) y redacción por LLM (no determinístico).
- [ ] La conversación queda en un estado que permite reactivarla después.

## Aprobación
Solicitado en reunión 2026-07-31 (Felipe Arango, GHT). Pendiente de aprobación T&M · relacionado con [[REQ-012]] y [[REQ-014]].

## Especificación técnica
`Especificaciones/Iniciativas/P-29_Cierre_Conversacional_Por_Tiempo.md` (ESPECIFICADA, 3 cortes, kill-switch OFF).
