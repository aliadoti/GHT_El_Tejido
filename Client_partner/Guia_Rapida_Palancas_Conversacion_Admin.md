# Guía rápida — Palancas que cambian la conversación

**Para:** administrador del portal y operador de plataforma.
**Supuesto:** la campaña ya existe, con participantes asociados, mensajes, preguntas, rúbrica,
prompts y LLM configurados.
**Alcance:** solo lo que **modifica cómo conversa el coach** con el participante.

---

## 0. Dónde se toca cada cosa — léelo antes de nada

Hay **dos lugares** de configuración y **no se pueden confundir**:

| | **App Settings** (Azure) | **Portal** (Campañas → Configuración) |
|---|---|---|
| Quién | Operador / DevOps | Administrador |
| Alcance | **Todas** las campañas | Solo esa campaña (algunos, solo esa pregunta) |
| Cómo se aplica | Reinicia la app; sin redeploy | Inmediato al guardar |
| Para qué sirve | Encender/apagar funciones y fijar defaults | Ajustar la campaña concreta |

**La regla que explica el 90 % de los "no funciona":**

> 🔴 **Si el interruptor de App Settings está apagado, lo que configures en el portal NO tiene
> efecto.** El global apagado gana siempre.
>
> Si el global está encendido, el valor efectivo se resuelve **pregunta → campaña → global**
> (el más específico gana).

Muchas funciones tienen **dos controles**: un interruptor en App Settings y una casilla en el portal.
**Hacen falta los dos.** Están marcados abajo con 🔗.

⚠️ **Casi todo nace apagado**, a propósito: el sistema arranca con el flujo más simple y cada palanca
se enciende conscientemente. No asumas que algo está activo porque aparece en pantalla.

---

# PARTE A — App Settings (Azure)

> **Quién:** operador de plataforma. **Cuándo:** una vez, antes del evento, y queda registrado en el
> **acta de flags**. El administrador de campañas normalmente **no** toca esta parte.
>
> **Formato de la clave:** en Azure el separador es doble guion bajo →
> `Conversacion__CierreAnticipadoHabilitado`.
>
> ⚠️ **Las listas de frases van INDEXADAS por clave**, una entrada por frase:
> `Conversacion__FrasesContinuar__0`, `__1`, `__2`… **Nunca** todo el listado en un solo valor.
> Ya ocurrió: al pegarlas como bloque único, el saludo dejó de reconocerse y **el bot no respondía
> nada**.

## A1. Interruptores que habilitan funciones

| Clave | Default | Qué hace si lo enciendes |
|---|---|---|
| `CierreAnticipadoHabilitado` | **`false`** | Permite cerrar la conversación cuando la idea ya es buena, sin gastar las repreguntas. 🔗 |
| `ResumenConsolidacionHabilitado` | **`false`** | Permite mostrarle al participante cómo va su idea antes de terminarla. 🔗 |
| `CuposHabilitados` | **`false`** | Activa **todos** los límites por usuario y el presupuesto de tokens. Sin esto, ningún límite del portal se aplica. 🔗 |
| `ClasificacionIntencionControl` | **`false`** | Interpreta frases libres de "parar/seguir" con el LLM. **Cuesta una llamada extra por mensaje.** 🔗 |
| `DespertarProactivoHabilitado` | **`false`** | Responde a un "hola" suelto cuando no hay conversación en curso. |
| `CierrePorTiempoHabilitado` | **`false`** | Envía un mensaje de pausa amable al cerrar por inactividad, en vez de cerrar en silencio. |
| `RetomarIdeasHabilitado` | **`false`** | El participante puede pedir volver a una idea histórica suya y elegirla de una lista. |
| `SegmentacionIdeas` | `true` | Kill-switch. En `true` **respeta la casilla de la campaña**; en `false` fuerza el flujo de 1 idea en todas. 🔗 |
| `CoachingSecuencialIdeas` | `true` | Kill-switch. Igual que el anterior. 🔗 |
| `Parafraseo` | `true` | Kill-switch. Igual que el anterior. 🔗 |
| `TejidoColectivo` | `true` | Kill-switch. ⚠️ Ver A5. |
| `ConsolidacionProgresivaHabilitada` | `true` | **Déjalo encendido.** Es lo que hace que los aportes se acumulen en una sola idea. Apagarlo solo en emergencia. |
| `RedaccionConversacionalFluidaHabilitada` | `true` | Turnos redactados con naturalidad. Apagado cae a textos fijos: funciona, pero suena robótico. |
| `ConfirmacionExplicitaIdeasHabilitada` | **`false`** | ⚠️ **Es un rollback, no una mejora.** En `true` vuelve al flujo antiguo que preguntaba "¿es correcto?" en cada versión. Déjalo en `false`. |

## A2. Números que fijan el comportamiento por defecto

| Clave | Default | Nota |
|---|---|---|
| `UmbralCierreAnticipado` | `0.6` | Fracción de la escala de la rúbrica (0–1). Ver la advertencia de A4. |
| `UmbralResumenConsolidacion` | `0` | Debe ser **menor** que el anterior. Ver A4. |
| `MinutosInactividadSesion` | `0` (desactivado) | Cierra el hilo si el participante deja de responder. **Recomendado: 5.** |
| `MinutosCoachingPorIdea` | `0` (sin límite) | Tiempo dedicado a cada idea cuando el coaching secuencial está activo. |
| `MaxTurnosPorHilo` | `0` (desactivado) | Techo duro que garantiza que cualquier conversación termine. |
| `MaxIdeasPorMensaje` | `5` | Máximo de ideas que se extraen de un mismo mensaje. |
| `LongitudMinimaIdea` | `30` | Evita partir el mensaje en fragmentos sin sentido. |
| `MaxCaracteresParafraseo` | `400` | Corta en frase completa. |
| `MaxCaracteresIntencionContinuar` | `40` | Evita que una respuesta larga que contenga "listo" se lea como cierre. |
| `MaxCaracteresDespertarProactivo` | `80` | Qué se considera un saludo. |
| `MaxCaracteresClasificacionIntencionControl` | `160` | Texto máximo elegible para el clasificador LLM. |
| `MaxCaracteresRedaccionTurno` | `320` | Más largo se descarta y cae al respaldo. |
| `MaxCaracteresIdeaConsolidada` | `4000` | Techo de la idea acumulada. |
| `HorasExpiracionSinRespuesta` | `0` (desactivado) | Cierre por abandono largo, en horas. Distinto del de inactividad (minutos). |
| `IntervaloRevisionMinutos` | `15` | Cada cuánto barre buscando hilos a cerrar. |

## A3. Textos y vocabulario

Todos los textos operativos viven aquí bajo `Conversacion__Mensajes__*`: saludo inicial, saludo de
siguiente pregunta, saludo de reactivación, mensaje de pausa, invitación a mejorar, acuses, mensaje de
configuración no disponible, encabezados de los menús de selección…

> 💡 **Excepción importante:** el **mensaje de cierre** de la campaña se edita **en el portal**, no
> aquí (ver B1).

💡 **Listas de variantes** (`InvitacionMejoraVariantes`, `AcuseContinuarVariantes`,
`InvitacionContinuarVariantes`): el sistema las **rota turno a turno** para no repetir siempre la
misma frase. Vacías = usa el texto único. **Vale la pena llenarlas**: es lo que evita que la
conversación se sienta mecánica.

**Listas de frases que el sistema reconoce** (vacías = usa las compiladas por defecto):
`FrasesContinuar`, `FrasesFinalizarIdea`, `FrasesFinalizarParticipacion`, `FrasesSolicitarMejora`,
`FrasesRechazoGuardado`, `FrasesRevisitarAnterior`, `FrasesRevisitarIdea`, `FrasesCambiarCampania`,
`FrasesDespertarProactivo`.

Estas frases funcionan **por coincidencia determinista y siempre están activas** — no dependen del
clasificador LLM. Tenerlas bien pobladas es la forma barata de que el coach entienda al participante.

## A4. Las dos trampas de calibración

**Trampa 1 — el umbral hace dos cosas a la vez.**
`UmbralCierreAnticipado` viene en `0.6` pero `CierreAnticipadoHabilitado` está **apagado**. Cambiar el
número sin encender el interruptor no hace nada.

Y al revés: **ese mismo `0.6` también decide si una idea se guarda como "madura" o en "incubación"**, y
si se muestra la paráfrasis — y eso **sí funciona con el interruptor apagado**. Consecuencia: subir el
umbral para que el coach cierre menos hará que **menos ideas salgan como maduras**. No son
independientes.

**Trampa 2 — el resumen de progreso debe ir por debajo del cierre.**
Con el cierre en `0.6`, un `UmbralResumenConsolidacion` de `0.6` o más **nunca se dispara**: la idea
cierra por madurez antes de llegar. **Rango útil: 0.40 – 0.55.** Si se quiere el resumen al 70 %, hay
que subir primero el umbral de cierre, con el efecto sobre maduras/incubación del párrafo anterior.

## A5. No lo actives

`TejidoColectivo` inyecta aportes anónimos de otros participantes en el contexto del coach.
**Fuera del alcance de esta convención: debe quedar apagado.** El código existe pero no está validado
para producción. Sus parámetros (`TopKAportes`, `PresupuestoTokensTejido`, `UmbralSolapamientoTejido`,
`RecuperacionSemantica`) son irrelevantes mientras esté apagado.

---

# PARTE B — Portal (Campañas → Configuración)

> **Quién:** administrador. **Efecto:** inmediato al guardar, solo en esa campaña.
> 🔗 = necesita además su interruptor de la Parte A encendido.

## B0. Primero: ¿la campaña recibe mensajes?

- **Estado** debe ser **`activa`**. En `borrador`, `cerrada` o `archivada` **no se envía ni se recibe
  nada**. Es la causa número uno de "no responde".
- Participantes **asociados** y con usuario **activo**.
- Al menos **una pregunta activa**.

## B1. Grupo Conversación

| Campo | Default | Qué cambia |
|---|---|---|
| **Máximo de repreguntas** | — | Cuántas veces el coach pide profundizar antes de cerrar. **Es la palanca que más cambia la duración.** `0` = evalúa y cierra sin repreguntar. |
| **Mensaje de cierre** | — | Texto con el que se despide. Único texto que se edita por campaña. |
| **Umbral de cierre anticipado** | vacío (hereda) | Vacío = usa el global. Un valor `≤ 0` lo apaga **solo** en esta campaña. 🔗 |
| **Minutos de inactividad** | vacío (hereda) | Vacío = usa el global; `≤ 0` lo desactiva en esta campaña. **No se puede fijar por pregunta.** |
| **Separar varias ideas de un mensaje** | apagado | Si alguien manda tres ideas juntas, las trata por separado. 🔗 |
| **Afinar ideas una por una** | apagado | Cola: trabaja una idea hasta terminarla y pasa a la siguiente. **Requiere que la separación de ideas esté activa.** 🔗 |
| **Minutos por idea** | vacío (hereda) | Tiempo por idea en el coaching secuencial; `0` = sin límite. |
| **Paráfrasis** | apagado | El coach resume lo que entendió antes de dar retroalimentación. **Solo se dispara después del umbral**, no en cada turno. 🔗 |
| **Permitir nuevas ideas después de finalizar** | apagado | Quien ya terminó puede volver con una idea nueva. ⚠️ Solo mientras la campaña esté **activa**. Apagarlo deja terminar la idea abierta pero bloquea la siguiente. |
| **Interpretar solicitudes escritas libremente** | apagado | Clasificador LLM de "parar/seguir". ⚠️ **Llamada adicional al LLM por mensaje**: impacta costo y latencia. 🔗 |
| **Tejido colectivo** | apagado | ⚠️ **Déjalo apagado** (ver A5). |
| **Número de WhatsApp saliente** | vacío | Alias del número desde el que salen los mensajes. Vacío = el predeterminado. |

## B2. Grupo Seguridad / costo

⚠️ **Nada de esto se aplica si `CuposHabilitados` está apagado en App Settings.** 🔗

| Campo | Qué pasa al alcanzarlo |
|---|---|
| **Máx. caracteres por mensaje** | Se rechaza el mensaje |
| **Máx. mensajes por usuario** | El mensaje se descarta en silencio con rechazo neutral |
| **Máx. llamadas LLM por usuario** | No se llama al LLM; el hilo cierra elegante con el mensaje de cierre |
| **Presupuesto de tokens de la campaña** (`0` = sin límite) | La campaña entera se trata como cupo agotado |

📐 **Cómo dimensionar antes de encender los cupos:**
`preguntas × (1 + máximo de repreguntas) + margen`.
Si lo dejas corto, los participantes se quedan sin cupo a mitad de camino y el hilo cierra antes de
tiempo.

## B3. Por pregunta (Campañas → Preguntas)

Solo tres cosas se afinan a este nivel, y son las de mayor precisión:

- **Máximo de repreguntas** de esa pregunta.
- **Umbral de cierre anticipado** de esa pregunta (gana sobre campaña y global).
- **Rúbrica y prompts** propios de esa pregunta.

Sirve cuando una pregunta es notoriamente más difícil o más abierta que el resto.

---

## ⚠️ Hueco conocido: el resumen de progreso no está en el portal

La función de **mostrar el avance de la idea** (P-31) tiene su interruptor y su umbral global en
App Settings, y el modelo soporta configurarla **por campaña y por pregunta** — pero **esos campos
todavía no aparecen en las pantallas del portal**. Hoy solo se pueden fijar por API.

En la práctica: **se enciende y se calibra desde App Settings para todas las campañas**, o se pide al
equipo técnico que lo ajuste por campaña vía API. Si la convención va a usar esta función con valores
distintos por campaña, conviene levantarlo como pendiente antes del evento.

---

## Checklist antes de abrir la campaña

**En App Settings (operador, queda en el acta de flags)**

1. [ ] Decidido si se enciende el **cierre anticipado**; si sí, interruptor **y** umbral configurados.
2. [ ] **Minutos de inactividad** en el valor acordado (recomendado 5); `0` lo deja desactivado.
3. [ ] Si se usa **resumen de progreso**: interruptor encendido y umbral **por debajo** del de cierre (0.40–0.55).
4. [ ] Decidido si se encienden **cupos** (sin esto, los límites del portal no aplican).
5. [ ] **Tejido colectivo apagado.**
6. [ ] `ConfirmacionExplicitaIdeasHabilitada` en **`false`**.
7. [ ] Listas de frases y variantes **indexadas** (`__0`, `__1`, …), nunca como bloque único.

**En el portal (administrador)**

8. [ ] Campaña en estado **`activa`**, con preguntas activas y participantes asociados.
9. [ ] **Máximo de repreguntas** dimensionado.
10. [ ] **Mensaje de cierre** revisado.
11. [ ] Si se encendieron cupos: los cuatro límites dimensionados con la fórmula de B2.
12. [ ] Casillas de separación de ideas / coaching secuencial / paráfrasis según lo acordado.

**Siempre**

13. [ ] Prueba real de punta a punta con un número propio **antes** de abrir a los participantes.

---

## Si algo no funciona

| Síntoma | Primero revisa |
|---|---|
| Activé algo en el portal y no pasa nada | 🔴 El **interruptor de App Settings** está apagado — gana sobre la campaña |
| El bot no responde nada | Estado de la campaña (`activa`); usuario activo y asociado; si fue un "hola" suelto, `DespertarProactivoHabilitado`; listas de frases pegadas como bloque en vez de indexadas |
| Las conversaciones se cortan muy rápido | Máximo de repreguntas; umbral de cierre demasiado bajo; cupos activos y cortos; `MaxTurnosPorHilo` |
| Las conversaciones no terminan nunca | Inactividad en `0`; sin cierre anticipado; sin techo de turnos |
| Nunca aparece el resumen de progreso | Su umbral es **mayor o igual** al de cierre — inalcanzable por diseño |
| Pocas ideas salen como "maduras" | El umbral está alto: el **mismo** número gobierna cierre y clasificación |
| El coach suena repetitivo | Listas de variantes vacías; redacción fluida apagada |
| No encuentro dónde configurar el resumen de progreso | No está en el portal — solo App Settings o API (ver el hueco conocido) |
| Costo del LLM más alto de lo esperado | Clasificador de intenciones encendido (llamada extra); paráfrasis; cupos desactivados |
| Los límites de la campaña no se respetan | `CuposHabilitados` apagado en App Settings |

---

> **Nota sobre los valores por defecto:** los defaults citados son los del entorno al 8-ago-2026. El
> valor efectivo del día del evento es el que quede registrado en el **acta de flags**, que es el
> documento que manda. Ante cualquier duda entre esta guía y el acta, manda el acta.
