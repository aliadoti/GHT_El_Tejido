# I-16 — Fix: calificación no reflejada en resumen/Markdown

> **Origen:** hoja `Iniciativas` (bug observado en la demo del 9-jul).
> **Tipo:** Desarrollo (fix) · **Prioridad:** Media (visible en demo → priorizar Sprint 1a) ·
> **Dependencia:** — · **Riesgo:** Bajo.
> Cubre REQ §22, ARQ §6; specs base `09 §5/§7`.

## 1. Síntoma
En la demo, al **reusar una campaña vieja**, la nueva calificación no quedó reflejada en el
resumen/Markdown. Posible efecto de la campaña determinística previa o de un artefacto anterior.

## 2. Estado actual del build
Por verificar. El compilador incrementa versión y guarda contenido canónico (`09 §7`); el
orquestador compila por cada evaluación válida.

## 3. Diseño técnico (diagnóstico primero)
1. **Reproducir con simulación:** limpiar `conversations`/`responses` del participante de prueba,
   recorrer el flujo sobre una campaña reutilizada y comparar `calificacion_total` de la última
   `Evaluacion` válida contra el Markdown regenerado.
2. **Hipótesis a verificar (en orden):** (a) el artefacto `md_<respuestaId>` de la corrida
   anterior no se regenera porque la nueva corrida usa otra `respuestaId` y Resultados muestra el
   artefacto viejo; (b) `CompiladorMarkdown` lee una evaluación distinta a la última válida;
   (c) datos residuales de la campaña anterior (limpieza incompleta).
3. **Fix según causa** + **test de regresión**: el Markdown compilado refleja la calificación de
   la **última evaluación válida** de esa respuesta (`calificacion_total` del `.md` == `Evaluacion`).

## 4. Contratos y configuración
Sin cambios esperados. Si el fix toca la plantilla del Markdown, actualizar `09 §5` en el mismo cambio.

## 5. Criterios de aceptación / pruebas
- Test de regresión unit: `calificacion_total` del Markdown == última `Evaluacion` válida de la respuesta.
- Reproducción manual en simulación documentada (pasos + resultado) en AVANCES.
- Build/test/format verdes.
