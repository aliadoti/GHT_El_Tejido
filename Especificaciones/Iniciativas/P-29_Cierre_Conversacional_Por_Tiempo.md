# P-29 — Cierre conversacional por tiempo (determinístico + no determinístico)

**Estado:** ESPECIFICADA — lista para implementación por cortes; **sin código implementado**.
**Requerimiento de negocio:** `Client_partner/.../Nuevas iniciativas/REQ-013_Cierre_conversacional_por_tiempo.md`.
**Fecha de decisión:** 2026-07-31 (reunión con Felipe Arango, GHT).
**Áreas afectadas:** orquestador conversacional, temporización/cierre por inactividad, persistencia
Cosmos, guardrails de salida, observabilidad y pruebas.
**Contratos relacionados:** `03 §3.3/§3.6`, `05 §4.4/§4.5`, `07 §4/§5`, `10 §6`, `13 §3/§4`,
`Reglas §2/§3`. **Extiende:** I-17 §7 (cierre por inactividad) e I-07 (cierre natural).
**Se coordina con:** P-28 (despertar) y P-30 (retomar).

---

## 1. Resumen ejecutivo

Hoy, cuando un participante deja de responder, la conversación queda colgada hasta un cierre por
inactividad relativamente seco (I-17 §7). GHT pidió que ese cierre sea **humano**: que el sistema, tras
un tiempo sin actividad, **avise con un mensaje natural** ("demos una pausa; hábleme cuando quiera y
seguimos") y deje la idea en un estado del que se pueda **retomar** después.

> **Nota de alineación (2026-08-03):** el cierre por inactividad **ya existe y está implementado**
> (I-17 §7, DONE local): el barrido `ServicioExpiracionConversaciones` cierra por campaña usando
> `ConfigConversacional.MinutosInactividadSesion` (override) ?? global `Conversacion:MinutosInactividadSesion`;
> y la entidad `Conversacion` ya tiene el campo `motivoCierre` (hoy con valor `"umbral"`). **P-29 NO
> reinventa el temporizador ni crea campos nuevos de umbral:** reutiliza los existentes y solo añade
> (a) el **mensaje de pausa humano** redactado por LLM con fallback y (b) el valor **aditivo**
> `motivoCierre="inactividad"` que deja la idea reanudable.

P-29 formaliza un cierre en dos partes, **sobre el mecanismo ya existente de I-17 §7**:

- **Determinística (ya existe):** el temporizador/barrido de inactividad de I-17 dispara el cierre de
  forma predecible con el umbral `MinutosInactividadSesion` (por campaña ?? global). P-29 no lo cambia.
- **No determinística (lo nuevo):** el **mensaje de pausa** lo redacta un LLM controlado por el servidor
  (I-20), con un **fallback** determinista si el modelo falla o está apagado.

El cierre por tiempo **no evalúa** una versión incompleta: respeta I-19 (solo la versión confirmada se
evalúa) y marca la conversación con `motivoCierre="inactividad"` (valor aditivo junto al `"umbral"`
existente), reanudable por P-28/P-30.

---

## 2. Decisiones confirmadas

| Decisión | Regla aprobada |
|---|---|
| Disparo | Determinístico, por inactividad. **Reutiliza el mecanismo de I-17 §7** (`ServicioExpiracionConversaciones`), no se crea uno nuevo. |
| Umbral | El existente `ConfigConversacional.MinutosInactividadSesion` (override por campaña) ?? global `Conversacion:MinutosInactividadSesion`; ~5 min de referencia. **No se crea un campo de umbral nuevo.** |
| Mensaje de pausa | **(Lo nuevo)** Redactado por LLM (no determinístico) con fallback determinista; **uno solo**, no se repite. |
| Estado resultante | Conversación `cerrada` con `motivoCierre="inactividad"` (**valor aditivo** junto al `"umbral"` existente); la idea queda **reanudable**. |
| Evaluación | No se fuerza; solo se evalúa la versión confirmada según I-19. Una idea sin confirmar no madura. |
| Ventana de WhatsApp | El aviso se envía solo si aún está dentro de la ventana de servicio de 24 h; si no, se omite el envío libre (no se fuerza HSM). |
| Interruptor | Kill-switch global `Conversacion:CierrePorTiempoHabilitado` (default `false` hasta UAT) que gobierna **solo el mensaje de pausa**; apagado ⇒ el cierre por inactividad de I-17 sigue operando, sin el aviso humano. |
| Compatibilidad | Conversaciones históricas conservan su `motivoCierre` actual (`"umbral"` u otros); ausente = cierre genérico. |

---

## 3. Alcance

### 3.1 Incluido
- **Reutilizar** el temporizador/barrido de inactividad de I-17 §7 (`ServicioExpiracionConversaciones`,
  umbral `MinutosInactividadSesion`); P-29 no lo reimplementa.
- Envío de **un** mensaje de pausa amable, redactado por LLM con fallback determinista **(lo nuevo)**.
- Añadir el valor **aditivo** `motivoCierre="inactividad"` que habilita la reanudación (P-28/P-30).
- Respeto de I-19: no se evalúa una versión no confirmada por el hecho de cerrar.
- Observabilidad del cierre y control por interruptor.

### 3.1.1 Reutilización explícita (no reinventar)
- Umbral: `ConfigConversacional.MinutosInactividadSesion` (`int?`) ?? `Conversacion:MinutosInactividadSesion` (I-17, `03 §3.3`).
- Barrido: `ServicioExpiracionConversaciones` per-campaña (I-17 Slice 6).
- Campo de cierre: `Conversacion.motivoCierre` (`03`), hoy `"umbral"`; se añade el valor `"inactividad"`.

### 3.2 Fuera de alcance
- La **reactivación** posterior de la conversación (P-28) y la selección de idea a retomar (P-30).
- Recordatorios proactivos fuera de la ventana de 24 h (P-08 / plantillas HSM).
- Cambios en la rúbrica, en la evaluación o en la consolidación (I-19/I-20).

---

## 4. Conceptos funcionales

| Concepto | Significado |
|---|---|
| Inactividad | Tiempo transcurrido desde el último mensaje del participante en una conversación abierta. |
| Umbral de cierre | Minutos de inactividad tras los cuales se dispara el cierre por tiempo. |
| Mensaje de pausa | Aviso natural que cierra amablemente y deja la puerta abierta a continuar. |
| Cierre por inactividad | Cierre con `motivoCierre="inactividad"`, distinto de la finalización explícita de una idea. |

---

## 5. Flujo funcional

### 5.1 Detección y disparo (determinístico, ya existente en I-17 §7)
1. El barrido existente `ServicioExpiracionConversaciones` evalúa la inactividad contra el umbral
   efectivo `ConfigConversacional.MinutosInactividadSesion` ?? `Conversacion:MinutosInactividadSesion`.
   P-29 **no** crea temporizador ni campo nuevos.
2. Al alcanzar el umbral, la conversación se cierra **una sola vez** (operación condicional/ETag),
   como hoy. P-29 engancha en ese cierre para fijar `motivoCierre="inactividad"` y, si el interruptor
   `CierrePorTiempoHabilitado` está encendido, disparar el mensaje de pausa (§5.2).

### 5.2 Mensaje de pausa (no determinístico con fallback)
3. Dentro de la ventana de servicio de 24 h, se solicita al LLM (I-20) un mensaje de pausa breve,
   cálido y sin revelar rúbrica ni puntajes. Si el LLM falla, está apagado, o se excede la ventana,
   se usa el **texto de fallback** determinista (o se omite el envío si ya no hay ventana).
4. Se envía **un** mensaje; no se reenvían recordatorios.

### 5.3 Estado resultante
5. La conversación pasa a `cerrada` con `motivoCierre="inactividad"` y `cerradaEn=ahoraUtc`.
6. La idea conserva su `ideaId`, versiones y estado de madurez de I-19; **no** se evalúa una versión
   sin confirmar por el solo hecho de cerrar.
7. La conversación queda **reanudable**: un mensaje posterior del participante la reactiva vía P-28 y,
   si corresponde, se retoma la idea vía P-30.

---

## 6. Parte determinista y parte LLM

| Parte del flujo | Tipo | Responsable |
|---|---|---|
| Medir inactividad y comparar con umbral | Determinista | Servidor |
| Decidir y marcar el cierre (una vez) | Determinista | Servidor/Cosmos (ETag) |
| Redactar el mensaje de pausa | No determinista, validado | LLM (I-20); servidor valida y aplica fallback |
| Fijar `motivoCierre` y dejar la idea reanudable | Determinista | Servidor |

---

## 7. Contratos de datos y configuración

Sin contenedores nuevos y **sin campos de umbral nuevos**. P-29 reutiliza lo de I-17 y solo agrega un
valor de enum y un prompt opcional:

**Reutilizado de I-17 (no se crea):**
- `Conversacion.motivoCierre` (`string`, `03`): campo **ya existente** (hoy `"umbral"`). Se añade el
  valor **aditivo** `"inactividad"`. Lectores históricos siguen aceptando los valores previos.
- `ConfigConversacional.MinutosInactividadSesion` (`int?`, `03 §3.3`) ?? global
  `Conversacion:MinutosInactividadSesion`: umbral de inactividad **ya existente**.
- `Conversacion.cerradaEn` (`03`): ya existe; lo fija el barrido al cerrar.
- `ServicioExpiracionConversaciones` (I-17 Slice 6): barrido per-campaña **ya existente**.

**Nuevo y aditivo:**
- **Kill-switch global:** `Conversacion:CierrePorTiempoHabilitado` (`bool`, default `false`). Gobierna
  **solo** el mensaje de pausa humano; el cierre por inactividad de I-17 sigue operando aunque esté OFF.
- **Prompt (opcional):** `promptRefs.cierre` por campaña/pregunta; ausente ⇒ fallback determinista.
  Versionado (`07 §4`).
- **Telemetría:** `LogSeguridad(cierrePorInactividad)` con
  `accion=avisoEnviado|avisoOmitidoSinVentana|fallbackUsado`, `correlationId` e ids internos; nunca
  texto del participante (`10 §6`). El evento de cierre en sí ya lo emite I-17.

---

## 8. Seguridad, privacidad y observabilidad
- El mensaje de pausa pasa por los guardrails de salida de I-20 (no revela rúbrica ni puntajes).
- Un solo aviso por cierre; sin reenvíos que puedan volverse spam.
- No registrar texto libre en telemetría técnica.
- Cierre idempotente: dos evaluaciones concurrentes no producen dos avisos ni dos cierres.

---

## 9. Manejo de condiciones especiales

| Caso | Comportamiento |
|---|---|
| El participante responde justo antes del umbral | Se reinicia el conteo; no hay cierre. |
| Ventana de 24 h ya vencida al disparar | Se omite el envío libre; la conversación se cierra igual con `motivoCierre="inactividad"`. |
| Falla el LLM al redactar la pausa | Se envía el texto de fallback determinista. |
| Interruptor apagado | Se conserva el cierre por inactividad actual de I-17. |
| Idea sin versión confirmada al cerrar | No se evalúa; queda reanudable para completarla luego. |
| Campaña se cierra administrativamente | Prevalece el cierre por campaña (`motivoCierre="cierreCampania"`), no el de inactividad. |
| Dos barridos concurrentes | ETag/condición garantiza un único cierre y un único aviso. |

---

## 10. Criterios de aceptación
1. Tras el umbral de inactividad configurado, el sistema envía **un** mensaje de pausa natural.
2. El disparo es determinístico (por tiempo) y el texto es no determinístico (LLM) con fallback.
3. La conversación queda `cerrada` con `motivoCierre="inactividad"` y **reanudable**.
4. No se evalúa una versión no confirmada por el solo hecho de cerrar (respeta I-19).
5. Si la ventana de 24 h ya venció, no se fuerza envío libre; el cierre se registra igual.
6. Con el interruptor apagado se conserva el cierre por inactividad actual (I-17).
7. El aviso no revela rúbrica ni puntajes.
8. El cierre es idempotente: no hay doble aviso ni doble cierre bajo concurrencia.
9. Una prueba simulada cubre inactividad → aviso (LLM y fallback) → cierre reanudable.

---

## 11. Plan de implementación por cortes

| Corte | Entrega verificable | Pruebas mínimas |
|---|---|---|
| 1 | Aditivos mínimos: valor `motivoCierre="inactividad"`, kill-switch `CierrePorTiempoHabilitado` y `promptRefs.cierre`. Enganche en el cierre existente de I-17 (`ServicioExpiracionConversaciones`) para fijar el motivo. **No** se toca el umbral (ya existe `MinutosInactividadSesion`). | Round-trip config, histórico conserva `motivoCierre` previo, el barrido fija `"inactividad"`. |
| 2 | Mensaje de pausa LLM con fallback, envío único, marca reanudable y telemetría; idempotencia. | LLM/fallback, ventana vencida, concurrencia, respeto de I-19. |
| 3 | Simulación E2E, QA y cierre documental. | Flujo completo, build/test/format/diff. |

Cada corte deja `TODO.md` y `AVANCES.md` actualizados. No desplegar sin instrucción posterior.

---

## 12. Rollback
1. Apagar `Conversacion:CierrePorTiempoHabilitado`.
2. Vuelve a operar el cierre por inactividad actual (I-17); nada persistido se borra.
3. `motivoCierre` es aditivo y legible por los flujos anteriores.
