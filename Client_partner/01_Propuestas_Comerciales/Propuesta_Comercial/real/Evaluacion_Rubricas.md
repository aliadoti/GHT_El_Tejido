# Evaluación de la Propuesta Económica — El Tejido (v2, sprint)

**Documento evaluado:** `Propuesta_Economica_El_Tejido.md` (fuente del deck PPTX con marca GTA/Aliado TI)
**Fecha:** 2026-06-12

---

## 1. Legibilidad (Flesch-Kincaid y equivalente para español)

| Métrica | v1 | **v2 (actual)** | Lectura |
|---|---|---|---|
| Palabras / frase | 10,9 | **10,8** | Frases cortas — muy bueno para aprobación ejecutiva |
| Sílabas / palabra | 2,35 | **2,18** | Mejoró al simplificar vocabulario |
| Flesch-Kincaid Grade Level | 16,4 | **14,3** | Inflado: fórmula calibrada para inglés |
| **Fernández-Huerta (Flesch en español)** | 54,7 | **65,1** | **"Normal/accesible"** — objetivo cumplido |

**Interpretación.** La versión sprint es más legible que la v1: el Fernández-Huerta subió de 54,7 a 65,1 y cruzó el umbral de accesibilidad (60). El FK Grade Level sigue sobreestimando la dificultad porque el español tiene más sílabas por palabra; la métrica de referencia en español es Fernández-Huerta. Las decisiones clave (precio, fechas, dependencia de WhatsApp) están en tablas y cifras, no en prosa.

---

## 2. Rúbrica de seis dimensiones (v3.4 — SP/IE/AC/TR/CO/DU)

Fórmula: `score = SP×0.25 + IE×0.20 + AC×0.20 + TR×0.15 + CO×0.10 + DU×0.10` · Piso de escritura: 0,60

| # | Afirmación central | SP | IE | AC | TR | CO | DU | Score | Tier |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Dependencia WhatsApp: GTA parametriza sobre la plataforma Meta de GHT; faltan línea y tarjeta; con línea antes del 19-jun la entrega sale por WhatsApp, si no, por web con activación en 2 días hábiles | 0.92 | 0.70 | 0.95 | 0.55 | 0.90 | 0.55 | **0.79** | ◈ developing |
| 2 | Precio fijo USD 9.750 (≈2 semanas de la capacidad USD 19.500/160 h) con pagos 50/50 el 13 y el 22 de junio | 0.95 | 0.55 | 0.95 | 0.45 | 0.92 | 0.50 | **0.75** | ◈ developing |
| 3 | Capacidad mensual posterior: USD 19.500/160 h con facturación proporcional al esfuerzo entregado | 0.90 | 0.55 | 0.85 | 0.60 | 0.90 | 0.60 | **0.75** | ◈ developing |
| 4 | MVP en infraestructura GTA con consumos refacturados con soportes; producción en infraestructura GHT | 0.80 | 0.60 | 0.90 | 0.50 | 0.85 | 0.55 | **0.72** | ◈ developing |
| 5 | Sprint con primera aproximación el 17-jun y primera versión el 22-jun | 0.95 | 0.50 | 0.90 | 0.35 | 0.85 | 0.40 | **0.70** | ◈ developing |
| 6 | El conocimiento de los líderes GHT se evapora y las wikis anteriores fueron abandonadas | 0.45 | 0.50 | 0.60 | 0.60 | 0.70 | 0.70 | **0.56** | — bajo el piso |

**Promedio de las afirmaciones accionables (1–5): 0.74 — tier developing.**

### Notas de calificación

- **#1 subió a 0.79** y es ahora la afirmación más fuerte: la regla WhatsApp/web tiene el execution edge más claro de toda la propuesta — disciplina una decisión concreta de GHT (habilitar la línea antes del 19) con consecuencia explícita.
- **#2** mantiene especificidad máxima y permite decidir con una sola lámina; TR/DU moderados son correctos para condiciones contractuales.
- **#5** queda en el borde del tier developing: el cronograma es válido solo para este sprint (TR/DU bajos por naturaleza). No requiere corrección.
- **#6** (framing del problema) sigue bajo el piso de 0,60 por baja especificidad. Si GHT aporta un dato concreto (intentos de wiki abandonados, líderes próximos a retiro), la lámina 2 ganaría fuerza persuasiva.

### Veredicto

La versión sprint mejora ambas rúbricas: legibilidad de 54,7 → 65,1 (Fernández-Huerta) y promedio de afirmaciones accionables de 0.73 → 0.74, con la nueva regla de dependencia WhatsApp como la afirmación mejor calificada. Documento apto para presentar y aprobar hoy.

---

## 3. Conformidad de marca (guía `Marca/GTA/guidelines/commercial-proposals.md`)

- Un lockup script (Borel) + heavy (Garet Heavy) por página, sin solapamientos. ✔
- Un solo registro de color (azul del logo); énfasis primario en tinta carbón. ✔
- Pie en todas las páginas: solo el logo + "Información Confidencial". ✔
- Portada y cierre fotográficos con scrim crema; páginas de contenido en crema plano. ✔
- Español, primera persona del plural, marco de aliado, sin emojis; cifras concretas. ✔
- Colores del cliente (verde/rojo GHT) fuera del chrome de Aliado TI. ✔

*Nota: el cuerpo usa Mulish (sustituto documentado de Garet body). Si tienes la licencia de Garet Book, instala la fuente y el PPTX la tomará al editar.*

---

# Anexo — Evaluación de "Aliado Digital GHT" (propuesta general, USD 20.000)

**Documento evaluado:** `Propuesta_Aliado_Digital_GHT.md` · 2026-06-12

## Legibilidad

| Métrica | Valor | Lectura |
|---|---|---|
| Palabras / frase | 9,3 | La más ágil de las tres versiones |
| Flesch-Kincaid Grade Level | 13,9 | Inflado por las sílabas del español |
| **Fernández-Huerta** | **65,7** | "Normal/accesible" — la mejor lectura del set |

Con 426 palabras es además la propuesta más corta, como se pidió.

## Rúbrica de seis dimensiones (afirmaciones centrales)

| # | Afirmación | SP | IE | AC | TR | CO | DU | Score | Tier |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Programa de USD 20.000: MVP Tejido USD 9.500 (pagos 50/50, entrega 22-jun) + bolsa USD 10.500 facturada por entrega | 0.95 | 0.55 | 0.95 | 0.50 | 0.92 | 0.50 | **0.76** | ◈ developing |
| 2 | Reglas de la bolsa: ≈86 h a tarifa de referencia, nada se factura sin aprobación, vigencia 31-dic-2026, excedentes se cotizan aparte | 0.90 | 0.65 | 0.92 | 0.60 | 0.90 | 0.55 | **0.77** | ◈ developing |
| 3 | Dependencia WhatsApp con canal web garantizado y parametrización en 2 días hábiles sin costo | 0.90 | 0.70 | 0.92 | 0.55 | 0.88 | 0.55 | **0.78** | ◈ developing |
| 4 | Propuesta de valor GTA: equipo senior, conocimiento del negocio, modelo por requerimientos aprobados | 0.55 | 0.50 | 0.65 | 0.65 | 0.80 | 0.70 | **0.62** | · seed |

**Promedio: 0.73 — developing.** Las tres afirmaciones contractuales (1–3) sostienen la decisión con especificidad alta. La afirmación de valor (#4) pasa el piso pero es la más genérica: ganaría fuerza con un dato verificable (años de experiencia promedio del equipo, un caso con resultado medible, o la referencia del modelo ya operando con Falcon).
