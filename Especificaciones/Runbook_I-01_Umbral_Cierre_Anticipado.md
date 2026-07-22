# Runbook — I-01: activar y calibrar el umbral de cierre anticipado en staging

> **Tipo:** operación (activación + calibración), **no** desarrollo de código nuevo.
> **Agente:** Claude · **Ventana:** Sprint 1a · **Depende de:** `P-10 cupos` ✓, baseline D5, rúbrica congelada (I-11).
> Cubre `REQ §21`, `ARQ §6`; P-13 permite calibrar el umbral en una campaña de prueba y conserva un
> kill-switch global independiente para el día-D.
> Mecanismo ya implementado (2026-06-23): ver `SUPUESTOS.md#orquestador-conversacional`.

## 1. Qué es I-01 y qué NO es
I-01 **ya existe en código**: el orquestador cierra un hilo **sin ofrecer más revisiones** cuando una
evaluación válida alcanza un umbral configurable de la escala de la rúbrica (`OrquestadorConversacion.UmbralAlcanzado`).
No hay que programar nada nuevo. El trabajo de I-01 es **elegir el valor del umbral con datos y activarlo
en staging**, observarlo, y decidir su promoción al día-D. **No** retira el tope determinístico de
revisiones (`MaxRepreguntas`) — ver regla D2 en §7.

## 2. Mecanismo (recordatorio)
- **Default numérico:** `Conversacion:UmbralCierreAnticipado` (env
  `Conversacion__UmbralCierreAnticipado`), fracción de la escala en `[0,1]`; `0` (default) = off
  para campañas sin override.
- **Override por campaña (P-13):** `configConversacional.umbralCierreAnticipado`; ausente/null hereda
  el default global y `<= 0` desactiva solo esa campaña.
- **Botón de pánico global:** `Conversacion:CierreAnticipadoHabilitado` (env
  `Conversacion__CierreAnticipadoHabilitado`), default `true`. En `false` apaga el cierre anticipado
  en todas las campañas e ignora los overrides.
- **Regla de corte:** cierra anticipadamente si
  `calificacionTotal >= escala.Min + umbral·(escala.Max − escala.Min)`.
  Ej.: escala 1..5, `umbral=0.85` → corte en `1 + 0.85·4 = 4.4`; una calificación de 5 cierra, una de 3 no.
- **Comportamiento:** en vez de la invitación a mejorar, antepone `Conversacion:Mensajes:MensajeCalificacionAlta`
  al cierre y compila el Markdown. Es **determinista y server-side** (no usa la `recomendacion` del LLM
  para el corte — R-01, "el sistema dispone").
- **Alcance:** para la calibración en staging, fijar el override solo en la campaña de prueba; el valor
  global sigue siendo el default de las demás campañas.
- **Observabilidad (I-01):** cada disparo emite `LogSeguridad(cierreUmbralAnticipado, resultado=cierre_anticipado)`
  con `detalle=umbral:<fracc>;score:<total>;valor:<corte>;escala:<min>-<max>` (sin PII de texto). Ver §5.

## 3. Precondiciones (bloqueantes — verificar antes de activar)
1. **`P-10 cupos` activos** camino a producción (`Conversacion:CuposHabilitados=true` en staging con
   límites dimensionados). D2: sin este techo, el umbral no debe aflojar la terminación.
2. **Baseline D5 congelado** con un corrido real contra staging (`tests/Calibracion/README.md`;
   `SUPUESTOS.md#banco-calibracion-d5`). El umbral se elige sobre la **distribución de scores** de ese
   corrido — sin baseline no hay dato para calibrar.
3. **Rúbrica congelada (I-11)**, no en `borrador` (workshop GHT 18-jul). Un umbral es una fracción de la
   escala: si la rúbrica/escala cambia, el valor absoluto de corte cambia. No activar sobre rúbrica en borrador.

> Mientras 2 y 3 no estén, I-01 queda **BLOCKED** (ver `AVANCES.md`). Este runbook y la observabilidad ya
> están listos; el resto es operación humana.

## 4. Elegir el valor del umbral (calibración con D5)
1. Corre el banco D5 contra staging con la rúbrica/prompt congelados y exporta el reporte
   (`EscritorReporteJson`/`Markdown`): tienes la **distribución de `calificacionTotal`** del golden set.
2. Traduce a fracción: `fraccion = (corteDeseado − escala.Min) / (escala.Max − escala.Min)`.
3. **Criterio de arranque conservador:** elige el umbral en el **percentil alto** de la distribución
   (p. ej. P85–P90) para que solo cierren temprano las respuestas claramente fuertes; el resto sigue
   recibiendo la revisión determinista. Empieza alto (menos cierres tempranos) y baja si el UAT lo pide.
4. Registra el valor elegido y su justificación (percentil, corte absoluto, nº de casos afectados en el
   golden set) en `SUPUESTOS.md#activacion-umbral-i01`.

## 5. Activar en staging (campaña de prueba + App Settings — operación humana)
En el App Service de **staging** (no en el del agente):
```
Conversacion__CierreAnticipadoHabilitado = true
Conversacion__Mensajes__MensajeCalificacionAlta = <opcional; default compilado si se omite>
```
En el portal, fija `configConversacional.umbralCierreAnticipado = <fraccion elegida, p. ej. 0.85>`
solo en la campaña de prueba. Reinicia el App Service únicamente si cambias el kill-switch o textos;
la campaña se actualiza por su API. **No** requiere redeploy de código.

**Observar (App Insights / consulta de `security`):** filtra `LogSeguridad` por
`tipoEvento = cierreUmbralAnticipado`. Métricas útiles durante el pilotaje:
- **frecuencia** de cierres tempranos vs. total de cierres,
- **distribución de `score`** en el `detalle` (¿el corte cae donde se esperaba?),
- correlación con el UAT (¿los participantes cuyo hilo cerró temprano quedaron satisfechos?).

## 6. Criterios de aceptación (salida de la calibración)
- El umbral cierra temprano **solo** respuestas fuertes (falsos-positivos aceptables en UAT).
- La tasa de cierres tempranos es la esperada según la distribución D5 (sin sorpresas).
- Ninguna regresión en los cupos/terminación (D2 intacto).
- El evento `cierreUmbralAnticipado` aparece en App Insights con el `score`/`valor` correctos.

## 7. Regla D2 (no negociable)
**No se retira el tope determinístico de revisiones (`MaxRepreguntas`) hasta que los cupos (P-10) estén
activos en producción.** I-01 solo permite cerrar *antes* por calificación alta; el techo de revisiones
sigue vigente como red de seguridad de terminación/costo (`SUPUESTOS.md#guardrails-cupos-conversacion`).

## 8. Rollback (sin redeploy)
`Conversacion__CierreAnticipadoHabilitado = false` → apaga el cierre anticipado en todas las campañas,
incluidos sus overrides; comportamiento idéntico al MVP probado (siempre se ofrece la revisión
determinista). Reversible en caliente vía App Setting.

## 9. Día-D (acta de flags, 6-ago)
El umbral **solo** se promueve a producción si pasó calibración D5 + UAT y con los cupos P-10 activos en
producción. Ante síntoma en el Hito, se apaga con `Conversacion__CierreAnticipadoHabilitado = false`
(nunca hotfix en caliente), coherente con el runbook de rollback del día-D (P-09).
