# PROMPT — Reorganizar la documentación de `Especificaciones/` (sin perder contexto)

> Pega este contenido como primer mensaje de un chat NUEVO dentro de este mismo proyecto
> (`GHT_Tejido_de_la_red`). Es una tarea de **reorganización documental**, NO de desarrollo de código.

---

Eres un **arquitecto de información + tech lead** con 25 años de experiencia ordenando bases de
documentación de proyectos vivos. Tu trabajo aquí es **reorganizar la estructura de carpetas y archivos
de la documentación** de El Tejido **sin perder una sola pieza de contexto, historia, detalle de
especificación ni la forma de trabajar** que hoy usan los agentes. Lees antes de mover, avanzas en
pasos pequeños y verificables, y **propones la estructura y la confirmas conmigo ANTES de mover nada**.

## 0. Contexto del problema
La carpeta `Especificaciones/` creció orgánicamente y quedó desordenada: hay documentos en la raíz de
`Especificaciones/` y otros dentro de `Especificaciones/Iniciativas/`, mezclando **contratos canónicos**,
**estado/historia**, **reglas de negocio**, **especificaciones por iniciativa**, **planes** y **prompts
de trabajo**. Además hay documentación relacionada **fuera** de `Especificaciones/` (`QAS/`,
`Guias_Implementacion/`). Quiero una estructura clara y navegable **sin romper nada**.

## 1. LEE PRIMERO, EN ESTE ORDEN (no muevas nada hasta terminar de leer)
1. `Especificaciones/00_Indice_y_Guia_de_Uso.md` — el índice/guía actual y el orden de lectura oficial.
2. `Especificaciones/01_Convenciones_para_Agentes.md` — la forma de trabajar (Definition of Done, disciplina).
3. `Especificaciones/AVANCES.md` — **log append-only** de traspaso entre agentes (historia real; §"Proximo paso" y "Log cronologico").
4. `Especificaciones/SUPUESTOS.md` — decisiones de ambigüedad con **anclas `#slug`** referenciadas desde muchos docs.
5. `Especificaciones/Iniciativas/00_Indice_y_Plan_de_Ejecucion.md` — clasificación de iniciativas, plan de sprints, ruta crítica, parametrización.
6. `Especificaciones/Iniciativas/TODO.md` y `Especificaciones/Iniciativas/PROMPT_Inicio_Iniciativas.md` y `Especificaciones/PROMPT_Inicio_Desarrollo_Codex.md` — el **mecanismo de handoff** entre agentes.
7. Un vistazo a `QAS/README.md` y `Guias_Implementacion/` para saber qué referencia a `Especificaciones/` desde fuera.

## 2. Inventario actual (verifícalo con `ls`/`git ls-files`, no asumas)
**`Especificaciones/` (raíz):**
- **Contratos canónicos numerados 00–13** (`00_Indice_y_Guia_de_Uso`, `01_Convenciones_para_Agentes`,
  `02_Arquitectura_y_Stack`, `03_Modelo_de_Datos_Cosmos`, `04_Contrato_API_REST`,
  `05_Backend_WhatsApp_y_Conversacion`, `06_Backend_Identidad_y_Autenticacion`,
  `07_Backend_Campanas_y_Configuracion`, `08_Backend_Evaluacion_LLM`, `09_Backend_Markdown`,
  `10_Seguridad_Guardrails_y_Observabilidad`, `11_Frontend_Portal_Angular`,
  `12_CICD_GitHub_Actions`, `13_Plan_de_Pruebas_y_Aceptacion`).
- **Estado/historia:** `AVANCES.md`, `SUPUESTOS.md`.
- **Reglas de negocio:** `Reglas_Conversacion_y_Participacion.md`.
- **Planes/runbooks:** `plan_hito_1.md` (histórico, con banner de superado), `Runbook_I-01_Umbral_Cierre_Anticipado.md`.
- **Prompts de trabajo:** `PROMPT_Inicio_Desarrollo_Codex.md`, y este mismo `PROMPT_Reorganizar_Documentacion.md`.

**`Especificaciones/Iniciativas/`:** `00_Indice_y_Plan_de_Ejecucion.md`, `TODO.md`,
`PROMPT_Inicio_Iniciativas.md`, `D5_Banco_Calibracion.md`, y las specs por iniciativa
(`I-03, I-05, I-06, I-08, I-09, I-10, I-12, I-16, I-17, P-03, P-04, P-05, P-06, P-07, P-08, P-09,
P-10, P-11, P-13`).

**Fuera de `Especificaciones/`:** `QAS/` (00–07 + README) y `Guias_Implementacion/` (3 guías Azure/WhatsApp).

## 3. RESTRICCIONES NO NEGOCIABLES (romper una de estas = tarea fallida)
1. **NO renumerar ni renombrar los contratos 00–13.** Se referencian **por número** (`03 §3.3`,
   `08 §4`, `10 §2`…) en decenas de documentos y en comentarios de código C#/TS. El número ES el
   identificador. Puedes moverlos a una subcarpeta, pero **el nombre de archivo se conserva** (`03_...md`).
2. **`AVANCES.md` es append-only.** No borres ni edites entradas del log histórico. Si lo mueves,
   solo cambia su ubicación; su contenido histórico queda intacto. Añade UNA entrada nueva al final
   documentando la reorganización.
3. **`SUPUESTOS.md`: no cambies los `#slug` de las anclas** (`#orquestador-conversacional`,
   `#primer-contacto-pregunta`, etc.): están referenciadas por texto en todo el repo.
4. **Preserva el mecanismo de trabajo:** `TODO.md` (handoff vivo, cabecera + tabla §4 + §8),
   `PROMPT_Inicio_*` y `01_Convenciones` deben seguir cumpliendo su función y apuntando a rutas válidas.
5. **Usa `git mv`** para cada archivo (preserva la historia de git). **Nunca** borrar-y-recrear.
6. **No cambies el CONTENIDO/significado** de ninguna especificación, decisión, criterio o dato. Esto
   es una reorganización de **ubicación + referencias + índice**, no una reescritura. Historia, detalle
   y forma de trabajar quedan idénticos.
7. **No toques código** (`src/`, `tests/`) salvo que un comentario contenga una **ruta** de archivo que
   cambió (los comentarios que citan `03 §3.3` por número NO cambian; solo cambian rutas explícitas si
   las hubiera — búscalas).
8. **Push a `main` solo cuando yo lo pida** (un push despliega). Trabaja en commits atómicos locales.

## 4. ENTREGABLE — en tres fases, con aprobación entre la 1 y la 2
**Fase A — Propuesta (NO mover aún):**
- Presenta un **árbol de carpetas propuesto** para `Especificaciones/` con una taxonomía clara. Punto de
  partida sugerido (ajústalo con tu criterio y justifica): `contratos/` (00–13), `estado/`
  (`AVANCES`, `SUPUESTOS`), `reglas/` (`Reglas_Conversacion...`), `proceso/` (`01_Convenciones`,
  `PROMPT_Inicio_Desarrollo_Codex`, este prompt), `iniciativas/` (índice + specs + `TODO` +
  `PROMPT_Inicio_Iniciativas` + `D5`), `planes/` (`plan_hito_1`, `Runbook_I-01_*`). Decide si `QAS/` y
  `Guias_Implementacion/` se mueven bajo `Especificaciones/` o se dejan en la raíz del repo (recomienda,
  con pros/contras).
- Incluye una **tabla de mapeo `origen → destino`** de cada archivo.
- Lista **todas las referencias que habrá que actualizar** (ver Fase C) y estima el riesgo.
- **Pregúntame y espera mi aprobación** antes de ejecutar. Si algo es ambiguo, aplica `01 §9`: la
  opción más simple que no cierra puertas, y registra el supuesto.

**Fase B — Ejecución (tras aprobación):**
- `git mv` archivo por archivo según el mapeo aprobado.
- Crea/actualiza un **mapa maestro de navegación**: renueva `00_Indice_y_Guia_de_Uso.md` (o crea un
  `README.md` en `Especificaciones/`) que explique **qué hay en cada carpeta, el orden de lectura y
  dónde vive cada cosa** (contratos, estado, iniciativas, planes, pruebas, guías). Debe ser el punto de
  entrada único.

**Fase C — Reparar referencias y verificar (imprescindible):**
- Actualiza **enlaces markdown relativos** (`](...md)`, `](../...md)`) que apunten a rutas que cambiaron.
- Busca y corrige **menciones de ruta** en prosa (p. ej. "en la raíz de `Especificaciones/`",
  ``Especificaciones/Iniciativas/…``, `QAS/03_...`). Ojo: las referencias **por número/§** (`03 §3.3`,
  `08 §4`) **no cambian** (no son rutas). Distingue ruta de referencia lógica.
- Revisa referencias cruzadas desde `QAS/`, `Guias_Implementacion/`, `AVANCES.md`, `SUPUESTOS.md`,
  `TODO.md`, `00_Indice_*` y los `PROMPT_Inicio_*`.
- **Verificación final (obligatoria):** con `grep`/`rg`, demuestra que **no queda ningún enlace ni ruta
  rota**. Comando sugerido para listar enlaces y validarlos: extraer todos los `](...md...)` y comprobar
  que el destino existe. Reporta "0 referencias rotas" con la evidencia.

## 5. Definición de terminado (DoD)
- Estructura nueva aplicada con `git mv` (historia de git preservada).
- **Cero** archivos perdidos: `git ls-files Especificaciones/ | wc -l` antes == después (más el índice
  nuevo si lo creas).
- Mapa maestro de navegación creado y correcto.
- **0 referencias/enlaces rotos** (evidencia con grep).
- Contratos 00–13 con su nombre/número intacto; `AVANCES`/`SUPUESTOS` con contenido y anclas intactos;
  `TODO`/prompts funcionando con rutas válidas.
- Entrada nueva en `AVANCES.md` (log) describiendo la reorganización, el árbol final y el mapeo, para
  que el próximo agente sepa dónde quedó todo.
- `TODO.md` y `00_Indice_*` reflejan la nueva ubicación de las cosas.
- Commits atómicos (Conventional Commits, `docs(estructura): ...`), terminando con el trailer que el
  repo exija (revisa los commits recientes). **Sin push** hasta que yo lo pida.

## 6. Regla de oro
Ante la duda entre "mover y arriesgar una referencia" o "preguntar", **pregunta**. Es preferible una
estructura un poco menos perfecta a una referencia rota o una pieza de historia perdida. El objetivo es
que cualquiera (humano o agente) abra `Especificaciones/`, entienda en 30 segundos dónde está cada cosa,
y que **todo el contexto, la historia, el detalle y la forma de trabajar sigan intactos**.

Empieza leyendo (§1) y luego preséntame la propuesta de estructura (Fase A). No muevas nada todavía.
