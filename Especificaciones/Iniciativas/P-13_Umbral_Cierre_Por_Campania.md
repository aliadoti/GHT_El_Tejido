# P-13 — Umbral de cierre anticipado parametrizable por campaña

> **Origen:** decisión de ingeniería (2026-07-15) al diseñar I-09/discutir I-01; formaliza el
> candidato ya listado en `00_Indice §4.3` ("I-01 umbral de cierre → `umbralCierreAnticipado` por
> campaña/pregunta").
> **Tipo:** Desarrollo (aditivo, pequeño) · **Prioridad:** Media · **Ventana:** post-Hito (rama de
> deseables); **adelantable a Sprint 1b** si se decide usarlo para simplificar la calibración de I-01.
> **Dependencia:** I-01 (calibrar primero el **default global**) · comparte el contenedor
> `configConversacional` con I-06/I-09/I-10 (sin conflicto). · **Riesgo:** Bajo (aditivo con default
> de herencia; cero cambio de comportamiento por defecto). Cubre REQ §9/§25, ARQ §4.2; specs base
> `05 §4.4`, `03 §3.3`.
> **Estado:** **IMPLEMENTACIÓN DONE local (2026-07-21).** Override nullable por campaña, default
> numérico global y kill-switch booleano independiente, con portal/API/Cosmos, observabilidad de origen
> y regresiones. Pendiente operativo: baseline D5 real y calibración humana de I-01 en staging.

## 1. Qué pide / por qué
Hoy el **umbral de cierre anticipado por calificación alta** (`Conversacion:UmbralCierreAnticipado`,
`05 §4.4`) es un **único valor global**: todas las campañas del ambiente comparten la misma
agresividad de corte. Pero *cuándo* cerrar antes es una **decisión editorial del coach**, no una
salvaguarda de plataforma: una campaña de screening rápido puede querer cortar en `0.7`, y una de
reflexión profunda quererlo **apagado** para no saltarse repreguntas. Por el principio rector del
índice §4 (*todo lo que define el comportamiento del coach es parametrizable por campaña; las
salvaguardas quedan globales*), el umbral debe poder **sobrescribirse por campaña**, manteniendo el
número global como default y un kill-switch booleano separado.

Beneficio colateral para **I-01**: con override por campaña, "activar el umbral en staging" deja de
ser un flip de App Setting **global** en Azure (operación humana que afecta a todas las campañas) y
pasa a ser un valor en la config de **una** campaña de prueba — calibrable y reversible por campaña,
sin tocar el entorno global.

## 2. Estado actual del build
El mecanismo existe y está probado, pero **solo lee el valor global**:
- `OpcionesConversacion.UmbralCierreAnticipado` (sección `Conversacion`, `double`, default `0` = off).
- `OrquestadorConversacion` lo captura una vez (`_umbralCierreAnticipado`) y decide en
  `UmbralAlcanzado(calificacionTotal, escala)`: `<= 0` desactiva; si no,
  `calificacionTotal >= escala.Min + fracción·(escala.Max − escala.Min)`.
- La **escala** ya es por campaña (sale de `RubricaSnapshot.Escala`); lo único global es la fracción.
- Ya hay dos bolsas de config **por campaña** al lado de la rúbrica: `Campania.ConfigConversacional`
  (de ahí sale `MensajeCierre`) y `Campania.ConfigSeguridad`. El umbral encaja en la primera.
- Decisión previa que este ítem revisa: `SUPUESTOS.md#orquestador-conversacional` (2026-06-23) dejó
  el umbral en config global "para no tocar contratos `03`/`04`… Un umbral/frases por pregunta queda
  como trabajo futuro si se necesita granularidad".

## 3. Diseño técnico (patrón "default global + override por campaña + kill-switch")
1. **Campo aditivo por campaña** `configConversacional.umbralCierreAnticipado` (`double?`, **default
   ausente/null**). Ausente/null → la campaña **hereda** el global. Presente → **sobrescribe** el
   global para esa campaña (misma semántica: `<= 0` desactiva; fracción `[0,1]` de la escala).
2. **Kill-switch booleano global:** `Conversacion:CierreAnticipadoHabilitado` (`bool`, default `true`)
   es la salvaguarda operativa independiente. En `false` el cierre anticipado queda apagado para
   **todas** las campañas, incluidos los overrides, sin redeploy.
3. **Resolución en el orquestador:** el valor efectivo es
   `!_cierreAnticipadoHabilitado ? off : campania.ConfigConversacional.UmbralCierreAnticipado ?? _umbralCierreAnticipado`
   (override si viene, global si no). `UmbralAlcanzado` recibe el valor efectivo en vez de leer solo
   el campo global. Cero cambio en la fórmula ni en la escala (que ya es por campaña).
4. **El número global permanece** como *default de sistema*: si una campaña no declara el campo, se
   comporta **exactamente igual que hoy**. Poner el número global en `0` no fuerza a las campañas con
   override propio; el botón de pánico es exclusivamente el booleano global.
5. **Sin granularidad por pregunta** en este ítem: se mantiene a nivel campaña (como `MensajeCierre`
   y `MaxRepreguntas`-por-pregunta ya cubren la granularidad fina). Por-pregunta queda como trabajo
   futuro si se necesita.
6. **Portal:** un campo numérico opcional en la edición de campaña, vacío = "heredar global". El
   booleano global no se expone por campaña: se opera únicamente por App Setting.

## 4. Contratos y configuración
- **`03 §3.3` (aditivo, commit aparte):** `configConversacional.umbralCierreAnticipado` (`double?`,
  default ausente/null = hereda el global). Documento viejo sin el campo = comportamiento actual.
- **Config global:** `Conversacion:UmbralCierreAnticipado` sigue siendo el default numérico y
  `Conversacion:CierreAnticipadoHabilitado=true` es el kill-switch global independiente.
- **`04` (portal):** el DTO de campaña acepta el campo opcional (aditivo, default `null`) y el portal
  permite editarlo; el kill-switch no forma parte del DTO.
- **Reglas de flujo:** actualizar `Reglas_Conversacion_y_Participacion.md §2.4`/`§3` para reflejar que
  el umbral efectivo es "campaña ?? global".

## 5. Riesgos y mitigación
- *Cambio de comportamiento inadvertido* → default de **herencia** (null): ninguna campaña existente
  cambia hasta que alguien fije el campo. Respeta D1 (nada nuevo activo por defecto).
- *Conflicto con la regla D2* (no aflojar el tope determinístico de revisiones sin cupos P-10 en
  producción) → **se mantiene**: el override no retira el techo de revisiones; solo mueve el punto de
  cierre por calificación alta, sujeto a la misma D2. No encender overrides que aflojen el corte hasta
  que los cupos estén activos en producción.
- *Dos fuentes de verdad* → no: el número global es el único **default**, el campo de campaña es un
  **override** explícito y opcional, y el booleano global es una salvaguarda separada. Una sola regla
  de resolución (`!habilitado ? off : campaña ?? global`).
- *Observabilidad* → al cerrar por umbral, registrar el **valor efectivo y su origen** (campaña vs.
  global) en el log/métrica que introdujo I-01 (para calibrar por campaña).

## 6. Criterios de aceptación / pruebas
- Unit: campaña **sin** el campo → usa el global (comportamiento idéntico al actual, regresión verde).
- Unit: campaña **con** `umbralCierreAnticipado` → usa el override, ignora el global (ambos sentidos:
  override activa con global off, y override `0`/off con global activo).
- Unit: `<= 0` en el override desactiva el cierre anticipado para esa campaña.
- Unit: `CierreAnticipadoHabilitado=false` apaga el cierre anticipado incluso con override activo.
- Unit: la fórmula usa la escala de **esa** campaña (`RubricaSnapshot.Escala`) con el valor efectivo.
- Contrato: documento `03` viejo sin el campo se deserializa a null y hereda (compatibilidad aditiva).

## 7. Degradación
Campo ausente/null en todas las campañas ⇒ sistema **idéntico al actual** (global manda). Rollback
operativo: `Conversacion:CierreAnticipadoHabilitado=false` apaga todos los cierres sin redeploy.
Retirar/ignorar un override de campaña vuelve al global sin migración. P-13 añade granularidad sin
cerrar fronteras.
