# DT-P27-01 — Configuración versionada de las expresiones determinísticas de P-27

**Tipo:** Deuda técnica (refactor de configuración). Derivada de P-27.
**Estado:** DONE local — cortes 1 y 2 completos el 2026-08-08; pendiente solo validación operativa de
una futura calibración, sin activar P-27.
**Fecha de decisión:** 2026-08-04.
**Áreas afectadas:** detector de intención de control (`DetectorIntencionContinuar`), configuración de
la aplicación, orquestador conversacional (solo lectura de config), observabilidad y pruebas de
regresión.
**Contratos relacionados:** `05 §4.4`, `07 §4/§5`, `10 §6`, `Reglas §… (tabla de configuración)`.
**Reutiliza:** el patrón ya existente de `Conversacion:FrasesContinuar`,
`Conversacion:FrasesSolicitarMejora` y `Conversacion:FrasesRevisitarAnterior/Idea` (listas con default
compilado y override por app config). **No modifica** el comportamiento de P-27 ni lo activa.

---

## 1. Resumen ejecutivo

P-27 corrigió las "salidas naturales" del participante ("quiero parar aquí", "pasemos a otra idea",
"stop now") con **alias deterministas**. Antes del corte 1, esos alias vivían en dos listas **compiladas dentro de
`DetectorIntencionContinuar`**: `FrasesFinalizarIdeaPorDefecto` y
`FrasesFinalizarParticipacionPorDefecto`; cambiarlos o calibrarlos exigía recompilar y desplegar.

El resto de las listas de frases del sistema (`FrasesContinuar`, `FrasesSolicitarMejora`,
`FrasesRevisitarAnterior`, `FrasesRevisitarIdea`) **ya están externalizadas** a configuración global
(`Conversacion:Frases*`) con default compilado y fallback seguro. DT-P27-01 alinea las dos listas de
P-27 con ese mismo patrón: las saca a **configuración global versionada**, sin cambiar el
comportamiento actual y sin permitir edición libre por campaña.

Es una **deuda técnica de consistencia y operabilidad**, no una nueva capacidad de producto: con la
configuración ausente, el sistema se comporta exactamente igual que hoy.

---

## 2. Decisiones confirmadas

| Decisión | Regla aprobada |
|---|---|
| Qué se externaliza | Las dos listas compiladas de P-27: finalizar idea y finalizar participación. |
| Claves nuevas | `Conversacion:FrasesFinalizarIdea` y `Conversacion:FrasesFinalizarParticipacion` (app config / env). |
| Default | Las listas compiladas actuales (`FrasesFinalizarIdeaPorDefecto` / `FrasesFinalizarParticipacionPorDefecto`) siguen siendo el **fallback seguro**. Config ausente o vacía = comportamiento actual idéntico. |
| Alcance de edición | **Global**, no por campaña. No hay override por campaña ni edición libre desde el portal. |
| Normalización | Se aplica la **misma** normalización/guarda de longitud de `DetectorIntencionContinuar` antes de comparar (coherente con las otras listas `Frases*`). |
| Validación | Tras normalizar: sin entradas vacías, sin duplicados y sin exceder `MaxFrasesFinalizacion` (20 por defecto); una config inválida cae al default seguro y se registra. |
| Historial / rollback | Cada inicio registra la versión efectiva o el descarte en la bitácora append-only; el origen de configuración conserva las revisiones y permite volver a la anterior o al default. |
| No incluido | No se tocan los alias vigentes, no se modifica la lógica de P-27, ni se activa P-27 como parte de esta deuda. |

---

## 3. Alcance

### 3.1 Incluido
- Leer `Conversacion:FrasesFinalizarIdea` y `Conversacion:FrasesFinalizarParticipacion` desde app
  config/entorno, con el mismo mecanismo de las demás `Conversacion:Frases*`.
- **Fallback seguro** a las listas compiladas cuando la config está ausente o vacía.
- **Validación tras normalizar**: rechazar vacíos, duplicados y listas fuera de límite; ante error,
  usar el default y registrar el motivo.
- **Historial y rollback** de la configuración (volver a la versión previa o al default).
- **Pruebas de regresión** que prueben que, sin config, el comportamiento es idéntico al actual, y que
  con config válida las frases nuevas se reconocen igual que los alias.

### 3.2 Fuera de alcance
- Edición de estas listas **por campaña** o edición libre desde el portal.
- Cualquier cambio en la clasificación LLM de P-27, en sus alias vigentes o en su activación.
- Extraer o versionar otras listas ya externalizadas (no aplica; ya lo están).
- Cambios en el modelo de datos de Cosmos (esta deuda es solo de configuración de aplicación).

---

## 4. Contratos de configuración

Claves **aditivas** de aplicación (mismo estilo que `Conversacion:FrasesContinuar`, `Reglas §tabla`):

| Clave | Origen | Default | Qué controla |
|---|---|---|---|
| `Conversacion:FrasesFinalizarIdea` | App config / env `Conversacion__FrasesFinalizarIdea__0`, `...__1` | (lista compilada `FrasesFinalizarIdeaPorDefecto`) | Alias con los que el participante pide **terminar la idea actual** (P-27). Vacío = usa la lista por defecto. |
| `Conversacion:FrasesFinalizarParticipacion` | App config / env `Conversacion__FrasesFinalizarParticipacion__0`, `...__1` | (lista compilada `FrasesFinalizarParticipacionPorDefecto`) | Alias con los que el participante pide **terminar su participación** (P-27). Vacío = usa la lista por defecto. |
| `Conversacion:MaxFrasesFinalizacion` | App config / env `Conversacion__MaxFrasesFinalizacion` | `20` | Máximo de entradas por cada una de las dos listas; `<=0` conserva 20. |
| `Conversacion:VersionFrasesFinalizacion` | App config / env `Conversacion__VersionFrasesFinalizacion` | huella no reversible | Etiqueta opcional de la versión conjunta. Si falta, se genera una huella segura para poder identificarla en la bitácora. |

- Tipo: lista de cadenas.
- Precedencia: si la clave trae elementos válidos, reemplaza el default; si está ausente/vacía/ inválida,
  se usa el default compilado.
- No hay campo por campaña ni endpoint nuevo de API.

---

## 5. Validación y normalización

1. Cada entrada pasa por la **misma normalización** de `DetectorIntencionContinuar` (minúsculas,
   recorte de espacios, tildes y puntuación) antes de compararse y antes de validarse.
2. Reglas de validación (tras normalizar):
   - no se admiten entradas **vacías** ni solo-espacios;
   - no se admiten **duplicados** dentro de la misma lista;
   - la lista no puede **exceder el límite** de tamaño configurado (evita listas desbordadas).
3. Si la configuración provista **no pasa** la validación, el servidor **no** falla el arranque del
   flujo: descarta la config inválida, usa el default compilado y registra el motivo (ver §7).

---

## 6. Historial y rollback

- Al iniciar con almacenamiento configurado, el sistema deja una entrada append-only por lista en
  `LogSeguridad(configuracionFrasesFinalizacion)`: aplicada, default o descartada. Para una inválida
  conserva únicamente `lista` y `motivo`; nunca las frases.
- La versión es `Conversacion:VersionFrasesFinalizacion` cuando el operador la declara; si no, se usa
  una huella no reversible. La bitácora identifica qué versión estuvo vigente y el origen de
  configuración (App Settings/revisión del archivo) conserva sus valores y revisiones.
- Para volver a una versión previa, restaurar allí la pareja de listas y su etiqueta, reiniciar y
  comprobar `/health`; para volver al comportamiento base, quitar o vaciar ambas listas. El último
  destino siempre es el default compilado.

---

## 7. Seguridad y observabilidad
- La configuración no contiene datos de participante ni secretos; son frases de control.
- Al descartar una config inválida se registra `configuracionFrasesFinalizacion` con el **motivo**
  (vacío / duplicado / límite) y sin volcar la lista completa a logs técnicos (`10 §6`). La falla de
  esa auditoría nunca impide arrancar con el fallback seguro.
- No cambia la telemetría de P-27 (`clasificacionIntencionControl`): DT-P27-01 solo altera de dónde se
  leen los alias deterministas, no cómo se ejecutan.

---

## 8. Criterios de aceptación
1. Con la configuración **ausente o vacía**, el reconocimiento de "finalizar idea" y "finalizar
   participación" es **idéntico** al actual (regresión verde).
2. Con configuración **válida**, las frases nuevas se reconocen igual que los alias compilados, tras la
   misma normalización.
3. Una configuración **inválida** (vacíos, duplicados o fuera de límite) se descarta, se usa el default
   y se registra el motivo; el flujo no se rompe.
4. Existe **historial** y es posible **revertir** a la versión previa o al default.
5. No hay override por campaña ni edición libre desde el portal.
6. No se modifican los alias vigentes de P-27 ni su lógica, y P-27 no queda activada por esta deuda.
7. La tabla de configuración de `Reglas` documenta ambas claves con su default y semántica.

---

## 9. Plan de implementación por cortes

| Corte | Entrega verificable | Pruebas mínimas |
|---|---|---|
| 1 | **DONE local 2026-08-05.** Lectura de las dos claves con fallback al default compilado; normalización compartida. | Config ausente = comportamiento actual; config válida reconoce y reemplaza las frases; 29 pruebas focalizadas, 730 backend, build y formato verdes. |
| 2 | **DONE local 2026-08-08.** Resolutor común valida vacíos/duplicados/límite, descarta completo y aplica fallback; auditoría append-only segura e historial/rollback operativo por el origen de configuración. | Regresiones de vacío, duplicado normalizado, límite, versión, auditoría sin frases y mapeo Cosmos. |

Cada corte deja `TODO.md` y `AVANCES.md` actualizados. Cambio **aditivo**; no desplegar ni tocar
configuración remota sin instrucción posterior del usuario.

### 9.1 Implementación del corte 1

- `OpcionesConversacion` enlaza las dos listas globales desde app config/entorno; la aplicación
  distribuida declara ambas vacías para conservar el comportamiento histórico.
- `PoliticaIntencionControl` recibe las opciones ya enlazadas. Una lista con elementos reemplaza su
  default; una lista ausente o vacía usa exactamente los alias compilados vigentes.
- La comparación reutiliza `DetectorIntencionContinuar`, por lo que conserva la normalización de
  mayúsculas, acentos, espacios y puntuación y la misma guarda de longitud.
- No se cambió ningún alias, flag, estado, contrato Cosmos/API ni configuración remota.

---

## 10. Rollback
1. Para recuperar una calibración anterior, restaura en el origen de configuración la pareja de listas
   y la etiqueta `Conversacion:VersionFrasesFinalizacion` de la versión que figure como `aplicada` en
   la bitácora; luego confirma que `/health` responde correctamente.
2. Para volver a la línea base, vacía o quita ambas listas. El sistema usa los aliases compilados.
3. Si una lista nueva es inválida, no hay que intervenir para contenerla: se descarta completa y usa el
   default. Corrige la versión en el origen y reinicia cuando corresponda.
4. Nada persistido de conversaciones cambia; no hay endpoint, edición por campaña ni modificación del
   modelo Cosmos para esta deuda.
