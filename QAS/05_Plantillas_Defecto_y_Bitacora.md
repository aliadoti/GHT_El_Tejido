# 05 — Plantillas: Reporte de Defecto y Bitácora de Ejecución

---

## 1. Plantilla de reporte de defecto

Copiar un bloque por defecto. **ID** correlativo `DEF-001`, `DEF-002`…

```
DEF-###
Título:            (síntoma concreto, no la causa supuesta)
Caso relacionado:  (ID de 02_Casos, p. ej. SEC-02)
Iniciativa/REQ:    (p. ej. I-03 / spec 08 §3.4)
Severidad:         Crítica | Alta | Media | Baja
Prioridad:         P1 | P2 | P3
Ambiente:          sim | real   ·   Build/commit: ____   ·   Corrida #: ____
Flags activos:     (CuposHabilitados, Parafraseo, TejidoColectivo, umbral, etc.)
Precondición/datos:(participante, campaña, respuesta usada)
Pasos para reproducir:
  1.
  2.
  3.
Resultado esperado:
Resultado obtenido:
Evidencia:         (captura, campaniaId, correlationId, id de LogSeguridad, extracto de Resultados)
¿Reproducible?:    Sí (n/n corridas) | Intermitente (n/n) | No
Notas no determinismo: (si aplica: cuántas de 3 corridas fallaron y en qué propiedad)
Estado:            Abierto | En análisis | Corregido | Re-verificado | Cerrado | Aceptado(workaround)
```

### 1.1 Severidad (guía de clasificación)

| Severidad | Definición | Ejemplos | Efecto en go-live |
|---|---|---|---|
| **Crítica** | Fuga de seguridad/privacidad, pérdida de datos, o guardrail determinista que no aplica | Fuga de rúbrica (SEC-01..05), PII de tercero en tejido (SEC-06), injection obedecida (SEC-09/12), cupo que no corta (GRD-01/02), firma inválida procesada (SEC-15) | **No-Go** |
| **Alta** | Flujo CORE roto sin fuga de seguridad | Fallback no seguro (ROB-01..03), dedupe duplica (ROB-04), repregunta >1 (CNV-04), Markdown con secreto (SEC-14) | **No-Go** hasta corregir |
| **Media** | Función Ext falla o CORE con workaround | Filtro que sobre-neutraliza retro legítima (SEC-05), multi-idea mal segmenta (FLG-03) | Evaluar; puede ir con workaround |
| **Baja** | Cosmético / mensaje / no bloquea | Texto de mensaje, formato de reporte | No bloquea |

> **Regla de no determinismo (repite de `00 §6.4`):** una propiedad de **seguridad** que falla en ≥1 de 3 corridas es **Crítica** (la seguridad no admite variación). Una propiedad de **calidad** (tono/utilidad de la retro) que varía se escala al árbitro de calibración (banco D5), no es Crítica automática.

### 1.2 Prioridad
`P1` corregir antes del go-live · `P2` corregir esta semana · `P3` backlog post-convención.

---

## 2. Bitácora de ejecución

Una fila por **ejecución de caso** (un caso puede tener varias filas si se corre en varias corridas). Llevar en esta tabla o en `Bitacora_Ejecucion.csv` (plantilla en §3).

| Fecha/hora | Caso | Corrida # | Ambiente | Flags activos | Resultado | Defecto | Observación |
|---|---|---|---|---|---|---|---|
| | CNV-01 | 1 | sim | defaults | OK | — | |
| | SEC-01 | 1/3 | sim | defaults | OK | — | 0 fugas |
| | SEC-01 | 2/3 | sim | defaults | OK | — | |
| | SEC-01 | 3/3 | sim | defaults | OK | — | |
| | GRD-02 | 1 | sim | Cupos ON, maxLLM=1 | Falla | DEF-004 | 2ª eval no cortó |
| | … | | | | | | |

**Valores de "Resultado":** `OK` · `OK con obs.` · `Falla (DEF-###)` · `Bloqueado (motivo)` · `N/A (flag off)`.

---

## 3. Plantilla CSV de bitácora

Crear `QAS/Bitacora_Ejecucion.csv` con este encabezado y una fila por ejecución:

```csv
fecha_hora,caso_id,corrida,ambiente,flags_activos,resultado,defecto_id,observacion
2026-08-04T09:00,CNV-01,1,sim,defaults,OK,,cold-start correcto
2026-08-04T09:10,SEC-01,1,sim,defaults,OK,,0 fugas
```

---

## 4. Resumen de cierre de ciclo (para el go/no-go)

Al terminar la ejecución, llenar este resumen y adjuntarlo al checklist día-D:

| Métrica | Valor |
|---|---|
| Casos CORE totales / en verde | __ / __ |
| Casos Ext totales / en verde | __ / __ |
| Defectos Críticos abiertos | __ |
| Defectos Altos abiertos | __ |
| Defectos Media/Baja abiertos (con workaround) | __ |
| Corridas de seguridad (SEC-01/06/12) con 0 fugas en 3/3 | Sí / No |
| ¿Acta de flags día-D firmada? | Sí / No |
| **Recomendación QA** | **GO** / **NO-GO** |

*Fin del documento.*
