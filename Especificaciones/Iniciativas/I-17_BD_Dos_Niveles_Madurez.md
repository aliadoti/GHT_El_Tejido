# I-17 — Base de ideas de dos niveles: maduras vs. incubación

> **Origen:** reunión de priorización GHT del **20-jul-2026** (nueva lógica central del MVP; no estaba
> en la hoja `Iniciativas` original). **Tipo:** Desarrollo (aditivo) · **Prioridad:** Alta — **MVP,
> lógica central de la interacción** · **Ventana:** Sprint 1b–2 · **Dependencia:** I-01/P-13 (umbral
> y su resolución por campaña), I-05 (paráfrasis tras umbral) · **Riesgo:** Medio (clasificación
> deriva de una calificación no determinista → se acota con umbral determinista). Cubre REQ §9/§22/§25,
> ARQ §4.2/§6; specs base `03 §3.8`, `05 §4.4`, `08`, `09`.

## 1. Qué pide GHT / por qué
La reunión del 20-jul definió la **lógica central de guardado en dos niveles**:

- **Ideas maduras:** se guardan en la base "principal" (madura) **solo después de superar el umbral
  de la rúbrica** (p. ej. 60–80 %).
- **Ideas en incubación:** las respuestas que **no** alcanzan el umbral se guardan en una base
  **separada de incubación**, como material "a medio cocer" para que los coaches humanos lo usen de
  inspiración en sesiones futuras (análisis post-evento).

Es un sistema de **dos niveles** que convierte la calificación en una decisión de *dónde y con qué
madurez* queda registrada la idea, no solo en un número.

Ligado a esto (misma reunión):
- **Paráfrasis y transparencia (I-05) solo tras el umbral:** el coach parafrasea "esto es lo que
  entendí" **únicamente cuando la idea alcanza el umbral**, confirmando que está lista para guardarse
  en la base madura; **la idea se guarda salvo que el usuario diga explícitamente "no"**.
- **Cierre no determinista:** sin límite fijo de turnos; termina por intención del usuario ("no más",
  I-02) o por **inactividad** (~5 min de sesión). Ver §7 (nota de granularidad de expiración).

## 2. Estado actual del build
Nuevo como *clasificación*. Hoy toda `Respuesta` evaluada se persiste igual y se compila su Markdown
sin distinguir madurez; el umbral existe pero solo como **cierre anticipado** (I-01
`UmbralCierreAnticipado`, `05 §4.4`), no como criterio de *nivel de guardado*. La calificación
(`Evaluacion.CalificacionTotal`) y la escala (`RubricaSnapshot.Escala`) ya están; falta la etiqueta de
madurez y su consumo en consultas/Markdown.

## 3. Diseño técnico (clasificación determinista aditiva)

### 3.1 Un contenedor, dos niveles lógicos (no dos contenedores físicos)
La "base madura" y la "base de incubación" se modelan como un **campo de clasificación** sobre el
contenedor `responses` existente (partición `campaniaId`), **no** como dos contenedores Cosmos. Razón:
mantiene la arquitectura aprobada (`03`), evita duplicar particiones/índices y las consultas ya se
acotan por `campaniaId`; "las dos bases" son **dos vistas/filtros** sobre `responses`. (Alternativa
descartada: contenedor `responses_incubacion` separado — cambia `03`, duplica pipeline de Markdown y
consultas, sin beneficio a escala de convención.)

### 3.2 Campo y cálculo
- **Campo aditivo** `nivelMadurez` en `Respuesta` (`03 §3.8`): enum `maduro | incubacion`
  (documento viejo sin el campo → tratado como `incubacion` por defecto seguro, o recomputado en
  regeneración de Markdown). **Se sella al evaluar**, server-side, no lo decide el LLM.
- **Regla determinista** (idéntica fórmula que I-01/P-13, reutiliza la escala de la campaña):
  `maduro` ⇔ `CalificacionTotal >= Min + UmbralMadurez · (Max − Min)`, con `UmbralMadurez` fracción
  `[0,1]` de la escala de la rúbrica. `incubacion` en caso contrario o si la evaluación cae en
  **fallback** (`evaluacionPendiente` nunca es madura).
- **Umbral parametrizable por campaña** (mismo patrón P-13): global
  `Conversacion:UmbralMadurez` (default, p. ej. `0.6`) + override
  `configConversacional.umbralMadurez` (`double?`, null = hereda). **Independiente del umbral de
  cierre anticipado** (I-01): una idea puede alcanzar madurez para *guardarse* sin que eso fuerce el
  *cierre* de la conversación; son dos decisiones distintas sobre la misma calificación. (Decisión de
  diseño a confirmar: ¿un único umbral compartido o dos? Ver §5, punto abierto.)

### 3.3 Consumo
- **Consultas/portal:** las consultas de resultados (`04 §5.8`) aceptan filtro aditivo por
  `nivelMadurez` (default: todas). La pantalla de Resultados separa "Maduras" e "Incubación"; el
  informe consolidado (P-11, post) exporta las maduras.
- **Markdown (`09`):** el artefacto registra el `nivelMadurez` de su respuesta (metadato en la
  plantilla determinista, sin secretos). Regenerable.
- **Paráfrasis (I-05):** el orquestador solo antepone la paráfrasis "esto es lo que entendí" cuando
  `nivelMadurez == maduro`; en incubación mantiene la retro/invitación a mejorar habitual (la idea
  aún no está lista para "guardar y confirmar"). Alinea I-05 con la decisión del 20-jul.

### 3.4 Observabilidad
`LogSeguridad`/métrica al sellar madurez: distribución `maduro` vs `incubacion` por campaña (permite a
GHT dimensionar la base de incubación y calibrar el `UmbralMadurez` en pruebas). Sin PII.

## 4. Contratos y configuración
- **`03 §3.8` (aditivo, commit aparte):** `Respuesta.nivelMadurez` (`maduro|incubacion`, default
  seguro `incubacion`). **`03 §3.3`:** `configConversacional.umbralMadurez` (`double?`, null = hereda).
- **`04 §5.8` (aditivo):** filtro `nivelMadurez` en las consultas de respuestas/markdown; el DTO de
  respuesta expone el campo. Documento/consulta viejos sin el campo siguen funcionando.
- **Config global:** `Conversacion:UmbralMadurez` (default configurable; `<=0` → todo `incubacion`
  hasta calibrar, o `maduro` según decisión §5). Kill-switch no aplica (la clasificación no gasta
  recursos ni llama al LLM; es una etiqueta derivada).
- **`09`:** metadato `nivelMadurez` en la plantilla de Markdown.

## 5. Puntos de diseño a confirmar con el usuario (antes de implementar)
1. **¿Umbral único o dos umbrales?** ¿`UmbralMadurez` (guardado) es el **mismo** valor que
   `UmbralCierreAnticipado` (cierre I-01), o son parámetros independientes? La reunión habla de "el
   umbral de la rúbrica" en singular → *sugerencia:* **un solo umbral** por campaña que gobierne ambas
   (madurez de guardado + paráfrasis + posibilidad de cierre), para no multiplicar perillas antes del
   freeze; separables después. **Requiere tu confirmación.**
2. **Default de `UmbralMadurez`** (60 % vs 80 %): la reunión da el rango 60–80 %. *Sugerencia:*
   arrancar en `0.6` y calibrar con el banco D5 en Pruebas.
3. **Semántica de "guardar salvo que diga no":** ¿la idea madura se marca guardada automáticamente y
   el "no" del usuario la degrada a incubación/descartada, o se pide confirmación blanda? *Sugerencia:*
   auto-guardar como `maduro` + permitir que una intención de rechazo explícita la reclasifique
   (reutiliza `DetectorIntencionContinuar`/frases de salida), sin fricción.

## 6. Criterios de aceptación / pruebas
- Unit: calificación ≥ umbral → `nivelMadurez=maduro`; por debajo → `incubacion`; fallback/pendiente →
  `incubacion` siempre.
- Unit: la fórmula usa la escala de **esa** campaña; override `configConversacional.umbralMadurez`
  gana sobre el global (regresión con doc viejo sin el campo = default seguro).
- Unit: paráfrasis (I-05) solo se antepone cuando `maduro`; en `incubacion` no.
- Integration/consulta: filtro `nivelMadurez` separa maduras de incubación por campaña; Markdown
  registra el nivel.
- Contrato: documento `03` viejo sin `nivelMadurez` se deserializa al default seguro (compatibilidad).
- Build `-warnaserror`/test/format verdes.

## 7. Nota — cierre por inactividad de sesión (~5 min, reunión 20-jul)
La reunión fijó cierre de sesión por **inactividad ~5 min**. Hoy la expiración es por **horas**
(`Conversacion:HorasExpiracionSinRespuesta`, `Reglas §2.6`). Cerrar en minutos requiere **granularidad
sub-hora** en el barrido (`Conversacion:MinutosExpiracionSinRespuesta` o reinterpretar el parámetro).
Es un ajuste pequeño y **aditivo**; se especifica aquí como dependencia de la "lógica central de
interacción" del 20-jul, pero puede ir en su propio paso. **Decisión de alcance a confirmar:** ¿entra
en I-17 o como ítem aparte del cierre no determinista?

## 8. Degradación
Sin el umbral configurado o con el campo ausente, el sistema se comporta como hoy (todas las
respuestas se guardan; la clasificación es una etiqueta que, en el peor caso, marca todo `incubacion`
sin romper nada). Rollback: ignorar el campo `nivelMadurez` en consumo vuelve al comportamiento
plano. El Hito conversacional no se rompe si I-17 no se activa; I-17 **habilita el valor de negocio**
de separar ideas maduras para el informe y las de incubación para coaching posterior.
