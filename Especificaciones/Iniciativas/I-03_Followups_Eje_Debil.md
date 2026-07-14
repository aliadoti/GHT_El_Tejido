# I-03 — Follow-ups sobre el eje más débil, sin revelar la rúbrica

> **Origen:** hoja `Iniciativas` de `Presentacion/Plan_Trabajo_El_Tejido.xlsx` (reunión 9-jul-2026).
> **Tipo:** Prompt + Desarrollo · **Prioridad:** Crítica · **Ventana:** Sprint 1a-1b (14–25 jul) ·
> **Dependencia:** I-11 (rúbrica recalibrada) · **Riesgo:** No determinismo (alto).
> Cubre REQ §21, ARQ §4.2/§6; specs base `08 §4/§5`, `05 §4.5`.

## 1. Qué pide GHT / por qué
El coach debe repreguntar enfocándose en la **dimensión con menor puntaje** de la evaluación,
**sin mostrar la rúbrica ni decir cuál eje está bajo**. Hoy la `RepreguntaSugerida` del LLM es
genérica: no está orientada al criterio débil.

## 2. Estado actual del build
Parcial: existe evaluación por rúbrica (`calificacion_por_criterio`) y `RepreguntaSugerida`
(usada como núcleo de la invitación a mejorar, Opción B 2026-06-23). No existe orientación al
criterio mínimo ni filtro que impida filtrar la rúbrica.

## 3. Diseño técnico
1. **Cálculo determinista del eje débil (server-side, nunca el LLM):** en `EvaluadorLlm`
   (`src/ElTejido.Application/Evaluacion/EvaluadorLlm.cs`), tras validar la salida, calcular el
   criterio de menor puntaje sobre `calificacion_por_criterio` (desempate: menor peso, luego orden
   alfabético — reproducible).
2. **Pista de foco en el prompt:** `ConstructorMensajesEvaluacion` agrega al `system` la
   instrucción de generar `repregunta_sugerida` que profundice en el **aspecto** débil descrito en
   lenguaje natural (p. ej. la descripción del criterio), con prohibición explícita de nombrar la
   rúbrica, los criterios o los puntajes. La repregunta se pide en la MISMA llamada de evaluación
   (sin llamada LLM extra).
3. **Filtro de salida determinista:** nuevo helper puro `FiltroSalidaRubrica` (Application/Evaluacion)
   que, con la rúbrica activa como insumo, verifica que `retroalimentacion_enviada` y
   `repregunta_sugerida` no contengan nombres de criterios (lista negra derivada de la rúbrica) ni
   patrones de puntaje (`N/M`, `N de M`, "calificación"). Si detecta fuga → se descarta la
   repregunta sugerida (cae a la variante rotada de respaldo ya existente) y se registra
   `LogSeguridad(AnomaliaLlm, motivo="fuga_rubrica")`; la retro con fuga se reemplaza por la retro
   neutra.

## 4. Contratos y configuración
Sin cambio de contratos `03`/`04`. El esquema de salida `08 §4` no cambia (la pista viaja en el
`system`). Sin config nueva (el filtro es siempre-on: es una salvaguarda, no un feature).

## 5. Riesgos y mitigación (R-01 / D5)
- *El LLM filtra la rúbrica o el eje* → prohibición en prompt (capa 1) + filtro determinista de
  salida (capa 2) + LogSeguridad (capa 3).
- *Regresión de comportamiento del prompt (RL-7)* → validar cada versión contra el banco de
  calibración (D5) antes de desplegar.

## 6. Criterios de aceptación / pruebas
- Unit: con una evaluación cuyo criterio mínimo es conocido, el contexto enviado al LLM contiene
  la pista de foco de ese criterio y no la palabra "rúbrica"/nombres de criterio como exigencia de salida.
- Unit: `FiltroSalidaRubrica` detecta y enmascara nombre de criterio y patrón `3/5`; salida limpia pasa intacta.
- Unit: repregunta con fuga → invitación usa la variante de respaldo y se registra la anomalía.
- Banco de calibración: ninguna salida del golden set contiene nombres de criterio ni cifras de puntaje.
- Build `-warnaserror` + test + format verdes.

## 7. Degradación
Si el foco degrada la calidad de la repregunta en calibración, se quita la pista del prompt (el
filtro de salida se conserva). Comportamiento previo intacto.
