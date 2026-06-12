# Evaluación de la Propuesta Económica — El Tejido

**Documento evaluado:** `Propuesta_Economica_El_Tejido.md` (fuente del deck PPTX)
**Fecha:** 2026-06-12

---

## 1. Legibilidad (Flesch-Kincaid y equivalente para español)

| Métrica | Valor | Lectura |
|---|---|---|
| Palabras / frase | 10,9 | Frases cortas — muy bueno para aprobación ejecutiva |
| Sílabas / palabra | 2,35 | Propio del español técnico-comercial |
| **Flesch-Kincaid Grade Level** | **16,4** | Inflado: la fórmula está calibrada para inglés |
| **Fernández-Huerta (Flesch adaptado al español)** | **54,7** | "Algo difícil" — rango normal de prosa de negocios en español |

**Interpretación.** El FK Grade Level sobreestima la dificultad en español porque nuestras palabras tienen más sílabas (ej.: "retroalimentación" = 6). La métrica correcta para texto en español es Fernández-Huerta: 54,7 con frases de 10,9 palabras indica un documento ágil cuya complejidad viene del vocabulario de dominio (WhatsApp API, rúbrica, trazabilidad), no de la redacción. Las cifras, tablas y estructura por secciones compensan: el lector ejecutivo encuentra precio, pagos y alcance sin leer prosa larga.

---

## 2. Rúbrica de seis dimensiones (v3.4 — SP/IE/AC/TR/CO/DU)

Se evaluaron las cinco afirmaciones centrales de la propuesta como "thoughts" independientes, con la disciplina anti-inflación de CO y DU de la v3.4.

Fórmula: `score = SP×0.25 + IE×0.20 + AC×0.20 + TR×0.15 + CO×0.10 + DU×0.10` · Piso de escritura: 0,60

| # | Afirmación central | SP | IE | AC | TR | CO | DU | Score | Tier |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Precio fijo USD 32.500 con pagos 30/40/30 atados a kickoff, demo (semana 5) y aceptación | 0.95 | 0.55 | 0.95 | 0.45 | 0.90 | 0.50 | **0.75** | ◈ developing |
| 2 | Capacidad mensual posterior: USD 19.500 / 160 h con facturación proporcional al esfuerzo entregado | 0.90 | 0.55 | 0.85 | 0.60 | 0.90 | 0.60 | **0.75** | ◈ developing |
| 3 | MVP en infraestructura GTA con consumos refacturados con soportes; producción en infraestructura GHT | 0.80 | 0.60 | 0.90 | 0.50 | 0.85 | 0.55 | **0.72** | ◈ developing |
| 4 | El MVP valida el flujo completo con ~5 usuarios en 8 semanas, con demo funcional en la semana 5 | 0.90 | 0.50 | 0.85 | 0.40 | 0.85 | 0.45 | **0.69** | · seed |
| 5 | El conocimiento de los líderes GHT se evapora y las wikis anteriores fueron abandonadas | 0.45 | 0.50 | 0.60 | 0.60 | 0.70 | 0.70 | **0.56** | — bajo el piso |

**Promedio de las afirmaciones accionables (1–4): 0.73 — tier developing.**

### Notas de calificación

- **#1 y #2** anclan la propuesta: máxima especificidad (cifras, hitos, porcentajes) y actionability directa (el comité puede decidir con esta sola lámina). El TR y DU moderados son correctos: son condiciones de este contrato, no mecanismos estructurales.
- **#3** tiene el execution edge más claro: define quién paga qué y cuándo cambia la responsabilidad — una regla que disciplina decisiones futuras.
- **#4** queda en seed por TR/DU bajos: el cronograma es válido solo para esta fase. Es lo esperable en una propuesta; no requiere corrección.
- **#5** (el framing del problema) queda bajo el piso de 0,60 por SP baja: no cuantifica cuántos líderes, qué rotación ni qué wikis fallaron. Es una afirmación heredada del business case. **Sugerencia:** si GHT dispone de un dato (ej. años promedio a retiro de los fundadores, o número de intentos de wiki abandonados), añadirlo a la lámina 2 subiría la fuerza persuasiva del problema.

### Veredicto

La propuesta cumple su función: las afirmaciones que sostienen la decisión económica (precio, pagos, infraestructura, modelo posterior) están en tier developing con especificidad alta, y la única afirmación débil es narrativa, no contractual. Documento apto para presentar al comité.
