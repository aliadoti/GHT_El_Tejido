# I-20 — Redacción conversacional fluida y Markdown ejecutivo por idea

> **Origen:** observación de la campaña interna del 28-jul-2026: el participante recibe textos
> repetitivos como “Entendí que propones…” y, en ciertos cambios de turno, una pregunta de mejora se
> concatena con una confirmación. El Markdown canónico tampoco muestra el umbral efectivo ni la nota en
> una escala legible.
> **Tipo:** Desarrollo + prompt versionado + Markdown determinístico. **Prioridad:** alta.
> **Dependencias:** I-03, I-17, I-18, I-19 y P-23. Cubre REQ §9/§20/§21/§22/§25/§26/§27 y ARQ §4/§6/
> §7/§12/§13; afecta `03 §3.3`, `05 §4.4–§4.5`, `08`, `09`, `13` y Reglas.
> **Estado (2026-07-28):** implementación WIP local — **cortes 1-4/5 hechos** (`242b0f4` contratos y
> política, `4697de3` redactor con guardas, `afcceaf`+`045b199` composición por acto, `c813cda`
> Markdown ejecutivo). La confirmación, la mejora, la aclaración, la reapertura y el cierre se
> redactan, con respaldo idéntico al texto anterior, y el artefacto ya explica umbral, origen y escala.
> Corte 5 de código cerrado: regresión completa y E2E con redactor inyectado. Solo quedan D5/UAT/costo
> operativos antes de desplegar. I-19 continúa siendo la fuente de la versión canónica.

## 1. Resultado esperado

El participante conversa con un agente breve, natural y pertinente al tema de la campaña, pregunta e
idea activa. No recibe rótulos repetidos ni dos peticiones distintas en un mismo mensaje.

I-20 no cambia la unidad de evaluación: cada nota se obtiene solo de la
`VersionIdeaConsolidada` completa y confirmada. La versión nueva incorpora lo confirmado más el aporte
actual, salvo corrección explícita. Aportes y versiones siguen auditables.

El Markdown canónico muestra campaña, pregunta, umbral realmente aplicado y la nota en escala legible,
por ejemplo `2,6 de 5 puntos` y `3 de 5 puntos (60 %; campaña)`.

## 2. Responsabilidades y regla de integridad

| Aspecto | Responsable | Regla |
| --- | --- | --- |
| Estado, idea activa, cola, límites, umbral, madurez, pendiente, rechazo y cierre | Servidor | Determinístico; el LLM no puede decidirlos. |
| Consolidar versión | LLM consolidador | Fiel al aporte y a la versión anterior; siempre requiere confirmación. |
| Evaluar y proponer acompañamiento | LLM evaluador | Sobre la versión completa confirmada. |
| Puente, confirmación, transición, aclaración, reapertura y acuse | LLM redactor | Un solo acto visible y lenguaje contextual. |
| Guardrails, fallback, cupos y formato de Markdown | Servidor | Determinístico y auditable. |

La nota no debe forzarse a subir: puede aumentar, mantenerse o bajar si una corrección elimina
precisión, contradice un hecho o debilita la idea. La garantía es evaluar siempre la versión completa,
no inflar la calificación.

## 3. Flujo conversacional visible

### 3.1 Confirmación

1. El servidor persiste el aporte y el consolidador genera la propuesta completa I-19.
2. El redactor recibe campaña, pregunta, tema, propuesta y contexto reciente de la misma idea.
3. Devuelve un puente y una sola pregunta de confirmación.
4. El servidor muestra: puente contextual + propuesta íntegra + pregunta de confirmación.

No se anexa retroalimentación, pregunta socrática ni transición en ese turno. Ejemplo orientativo:
“Para dejar clara la propuesta de votación durante la presentación, recogí esta versión: …
¿Representa lo que quieres plantear?” No es una plantilla fija.

### 3.2 Mejora, transición y cierre

Tras evaluar una versión confirmada bajo umbral, el redactor formula una sola intervención: reconoce
brevemente un avance real y hace una pregunta abierta sobre el foco seguro de I-03, sin rúbrica,
criterios, puntajes ni respuesta sugerida. Para transición, aclaración, reapertura o cierre, solo
redacta el mensaje del acto que el servidor ya resolvió. Nunca combina actos.

## 4. Contrato interno del redactor

Se añade `IRedactorTurnoConversacional`, puerto interno sin endpoint ni DTO administrativo nuevo.
Recibe datos delimitados: campaña/pregunta/instrucción, acto ordenado por el servidor
(`confirmar|mejorar|transicionar|aclarar|reabrir|cerrar`), versión completa cuando aplique,
retroalimentación validada/foco I-03, historial mínimo de la misma idea y snapshots efectivos.

Devuelve JSON estricto:

```json
{ "puente": "string breve o null", "pregunta": "string breve o null" }
```

El servidor inserta la versión propuesta exacta entre ambas piezas al confirmar. Así el LLM no puede
ocultarla, reemplazarla ni convertirla en una evaluación. Para mejora se usa la retroalimentación ya
validada y la única pregunta aprobada.

### 4.1 Guardrails, costo y fallback

- límite de longitud; máximo una pregunta visible y solo para el acto que la exige;
- prohibidos rúbrica, criterio, calificación, umbral, patrones `N/M` y promesas de implementación;
- no añadir hechos ni mezclar ideas/participantes; el `acto` del JSON se ignora;
- salida inválida, timeout o fuga → respaldo breve y seguro para ese acto, sin registrar texto;
- la llamada cuenta para los cupos existentes y sus tokens/latencia se distinguen de consolidación y
  evaluación; cupo agotado conserva el aporte y aplica el cierre seguro vigente.

El respaldo es excepcional, no el texto normal de la campaña.

## 5. Prompt y parametrización

`promptRefs.conversacion` es una referencia opcional de campaña o pregunta; la pregunta prevalece.
Define voz, idioma y reglas de redacción, es versionada/aprobada y no puede cambiar rúbrica, estado ni
límites. Una campaña existente sin ella usa `retro` efectivo solo como guía de tono más las
instrucciones de seguridad del redactor.

No existe opt-in por campaña. `Conversacion:RedaccionConversacionalFluidaHabilitada` es un
kill-switch global de emergencia, con default `true`; al apagarlo se usan respaldos determinísticos sin
alterar consolidación, evaluación, cola ni Markdown.

## 6. Evaluación y Markdown

### 6.1 Regla de evaluación

```text
texto evaluado = VersionIdeaConsolidada confirmada referida por evaluacion.versionIdeaId
```

Un aporte aislado, la confirmación “sí” o una propuesta no confirmada nunca son el texto calificado.
Una evaluación nueva existe solo después de confirmar la versión completa.

### 6.2 Metadatos ejecutivos

Con evaluación vigente, el compilador renderiza de forma determinística:

```markdown
- Campaña: {{campania.nombre}}
- Pregunta: {{pregunta.texto}}
- Umbral de madurez: {{corte}} de {{escala.max}} puntos ({{porcentaje}} %; {{origen}})
- Calificación total: {{evaluacion.calificacionTotal}} de {{escala.max}} puntos
```

`corte = escala.min + umbralEfectivo × (escala.max - escala.min)`. Los números usan cultura `es-CO`,
sin ceros decimales innecesarios; el origen es `pregunta`, `campaña` o `global`. Sin evaluación de esa
versión se muestra `Calificación total: pendiente de evaluación`; no se inventa una nota ni un umbral
alcanzado.

## 7. Alcance y exclusiones

Incluye puerto/redactor, prompt efectivo/snapshot, composición por acto, guardrails/fallback,
observabilidad/cupos, formato del Markdown y regresiones. No incluye cambiar rúbrica, garantizar una
nota creciente, revelar notas al participante, API pública nueva, curaduría, publicación, ni reapertura
entre preguntas.

## 8. Cortes de implementación

1. **[Hecho local 2026-07-28, commit `242b0f4`]** Contratos/documentos internos: `03`, `05`, `08`,
   `09`, `13` y Reglas ya especificados (commit `6a6d0b8`); en código, puerto interno
   `IRedactorTurnoConversacional`, enum `ActoConversacional`, `PoliticaRedaccionConversacional` (pura:
   kill-switch global sin opt-in por campaña, largo máximo normalizado, prompt efectivo
   `conversacion` → `retro` y actos que admiten pregunta) y las opciones
   `Conversacion:RedaccionConversacionalFluidaHabilitada` (default `true`) y
   `Conversacion:MaxCaracteresRedaccionTurno` (320). Sin API pública y **sin cambio observable**: nada
   lo consume todavía. `promptRefs` ya era un diccionario libre, así que la clave `conversacion` resultó
   aditiva por construcción, sin tocar dominio ni Cosmos. Verde: 543 pruebas (+14).
2. **[Hecho local 2026-07-28, commit `4697de3`]** Redactor implementado: `RedactorTurnoConversacional`
   con prompt efectivo + reglas duras y datos delimitados (`08 §5`), JSON estricto
   `{"puente","pregunta"}`, y `GuardasRedaccionTurno` que **reutiliza `FiltroSalidaRubrica` de I-03**
   —criterios, léxico del mecanismo y patrones `N/M`— más el léxico propio de I-20
   (`umbral|puntaje|puntos|nota|escala|madura|madurez`) y las promesas de implementación. Exige salida
   no vacía, longitud por pieza, **una sola pregunta** y solo en los actos que la admiten, y ninguna
   pregunta escondida en el puente. Una salida sospechosa se **rechaza entera** y degrada a `Fallback`
   sin registrar el texto; el `acto` que venga del modelo se ignora. Registrado en DI. Verde: 563
   pruebas (+20). **Cupos y telemetría se movieron al corte 3**, porque viven en el llamador.
3. **[Hecho local 2026-07-28, commits `afcceaf` contrato + `045b199`]** Composición por acto en el
   orquestador: `ComponerTurnoAsync` sustituye `TextoConfirmacion` (sus dos rutas) y las
   concatenaciones de `Mejorar`, `Aclarar`, `Reabrir` y `Cerrar`. El orden es **puente → cuerpo →
   pregunta**, con el cuerpo insertado por el servidor. Respaldo determinista idéntico al texto previo
   ante kill-switch apagado, redactor ausente, `ConfigLLM` inactiva o `Fallback`; cupos P-10 aplicados
   antes de gastar LLM; telemetría `redaccionConversacional` sin texto. Verde: 568 pruebas (+5). La
   **transición** no requirió cambio: hoy el paso a la siguiente idea/pregunta ya se envía como acuse o
   confirmación del acto siguiente, sin texto fijo propio.
4. **[Hecho local 2026-07-28, commit `c813cda`]** Markdown ejecutivo: `- Umbral de madurez: {corte} de
   {max} puntos ({porcentaje} %; {origen})` y `- Calificación total: {nota} de {max} puntos`, con
   umbral y origen resueltos por `PoliticaLimitesConversacion` (I-17) —misma precedencia que la
   madurez—, cultura `es-CO` sin ceros sobrantes, `pendiente de evaluación` cuando no hay nota (y sin
   imprimir umbral), y la escala tomada de la **versión exacta** de rúbrica que registró la evaluación.
   Se aplica al artefacto de idea **y** al legacy por respuesta. Verde: 572 pruebas (+4).
5. **[Hecho local 2026-07-28]** Nueva E2E simulada
   `I20_CicloCompleto_UsaRedactorEnConfirmacionSinCambiarLaVersionEvaluada`: atraviesa webhook real,
   redactor inyectado, propuesta íntegra y evaluación canónica. Build Release, 573 pruebas no
   calibración (514 unitarias + 59 integración), formato y diff limpios. Pendiente: D5/UAT/costo con
   temas distintos antes de desplegar.

## 9. Criterios de aceptación

- [ ] No aparece la frase fija “Entendí que propones” como patrón normal ni se unen mejora y
  confirmación en un mensaje.
- [ ] Cada turno tiene un único acto y como máximo una pregunta.
- [ ] La redacción depende de campaña, pregunta, idea y contexto sin inventar datos.
- [ ] Solo el servidor decide estado, umbral, madurez, límites y cola.
- [ ] Cada evaluación referencia la versión completa exacta; una nota puede bajar sin perder auditoría.
- [ ] El fallback no revela rúbrica ni convierte una propuesta en madura.
- [ ] El Markdown muestra campaña, pregunta, umbral con origen y `X de Y puntos`; nunca nota de otra
  versión.
- [x] Build, pruebas no-calibración, formato, frontend afectado y `git diff --check` quedan verdes.

## 10. Cómo probarlo en lenguaje simple

1. Responde con una idea corta: debe volver una reformulación natural y una sola confirmación.
2. Agrega un dato: la propuesta siguiente conserva lo anterior más el dato nuevo.
3. Confirma y responde a la pregunta de mejora: debe ser una sola pregunta clara, sin puntajes.
4. Comprueba que el sistema no repite mecánicamente la misma frase de confirmación.
5. Descarga el Markdown de Resultados: deben verse campaña, pregunta, umbral, nota `X de Y puntos`,
   estado e historial.
6. Es fallo si se califica solo el último mensaje, aparecen dos preguntas juntas, se revelan puntajes,
   se inventan datos o el Markdown no explica el umbral.

## 11. Handoff

Antes de código, leer `AVANCES.md`, `TODO.md`, esta spec,
`SUPUESTOS.md#redaccion-fluida-i20`, I-19, Reglas y `03`/`05`/`08`/`09`/`10`/`13`. Preservar los cambios
ajenos actuales `.obsidian/workspace.json` y `Semillas/`. Iniciar por el corte 1 y verificarlo antes de
continuar.
