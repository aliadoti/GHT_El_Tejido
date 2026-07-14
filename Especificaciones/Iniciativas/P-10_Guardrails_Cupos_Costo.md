# P-10 — Guardrails y cupos para producción abierta

> **Origen:** hoja `Iniciativas` (propuesta Aliado TI; deuda declarada en AVANCES).
> **Tipo:** Config/Desarrollo · **Prioridad:** Media (requisito para abrir a todos) ·
> **Ventana:** Sprint 1a (cupos, HECHO) + Sprint 2 (costo y rate) · **Riesgo:** costo LLM.
> Cubre REQ §25.1/§25.2, ARQ §4.2/§6; specs base `10 §2`.

## 1. Qué pide / por qué
Límites/cupos de LLM y **control de costos** antes de abrir a todos los participantes. Con I-06
(multi-idea: 1 segmentación + N evaluaciones por turno) e I-09 (tejido: prompts más grandes) el
consumo por turno sube — el cupo debe contemplarlo.

## 2. Estado actual del build
- **HECHO (2026-07-13, `SUPUESTOS.md#guardrails-cupos-conversacion`):** cupos
  `maxMensajesPorUsuario`/`maxLlamadasLlmPorUsuario` de `Campania.ConfigSeguridad` aplicados en el
  orquestador (flag `Conversacion:CuposHabilitados`, default off) + techo duro
  `Conversacion:MaxTurnosPorHilo` (0=off). `LogSeguridad(RateLimit)` con motivo.
- **HECHO (2026-07-14, backend verde):** **rate por número WhatsApp**
  (`Seguridad:RateNumeroWhatsAppPorMinuto`, 0=off; `SUPUESTOS.md#rate-numero-entrante`) y
  **cupo/costo LLM por campaña** — metering de tokens (`ILlmClient`→`LlmRespuesta`, log con
  `campaniaId`) + presupuesto **por campaña** `Campania.ConfigSeguridad.PresupuestoTokensCampania`
  (aditivo, 0=off, editable en portal) con corte server-side reusando el camino de cupo LLM agotado
  (`SUPUESTOS.md#cupo-costo-llm`). Decisión del usuario: presupuesto configurable por campaña.
- **Pendiente:** ajuste del conteo cuando I-06 active la segmentación (diferido hasta que exista I-06);
  verificación del frontend (Node ≥ 22.22.3/24.15.0) y validación de la alerta de costo en pruebas (P-09).

## 3. Diseño técnico (restante, Sprint 2)
1. **Cupo de costo LLM por campaña:** `LlmClientHttp` ya recibe `usage` del proveedor — emitirlo
   como log estructurado/métrica (tokens prompt+completion por llamada, con campaniaId). Acumulado:
   consulta agregada en App Insights (alerta 80% del presupuesto `Conversacion:PresupuestoTokensCampania`)
   + **corte configurable**: al superar el presupuesto, el orquestador trata la campaña como cupo
   LLM agotado (cierre elegante, mismo camino ya implementado). Contador simple en Cosmos (doc
   `costoCampania` en `config` o `participants`) solo si la agregación en App Insights no alcanza
   — decidir en Sprint 2 y registrar supuesto.
2. **Rate por número WhatsApp** (p. ej. 10/min): ventana deslizante en memoria (patrón
   `LimitadorOtpMemoria`) aplicada en `ProcesadorWebhookEntrante` antes de resolver participante;
   al exceder → descarte + `LogSeguridad(RateLimit, "rate_numero")`. En memoria es suficiente para
   el MVP (una instancia); documentar la limitación.
3. **Conteo con multi-idea (I-06):** la llamada de segmentación cuenta en `maxLlamadasLlmPorUsuario`;
   cap adicional por mensaje = `1 + MaxIdeasPorMensaje`.

## 4. Contratos y configuración
Sin cambio de contratos `03`/`04` (config `Conversacion:*`/`Seguridad:*` y agregación en App
Insights). Config nueva: `Conversacion:PresupuestoTokensCampania` (0=off),
`Seguridad:RateNumeroWhatsAppPorMinuto` (0=off).

## 5. Criterios de aceptación / pruebas
- Unit: cada llamada LLM emite log/métrica de tokens con campaniaId y sin secretos.
- Unit: rate por número excedido → descarte + LogSeguridad; bajo el límite → flujo normal.
- Alerta de costo configurada y validada en la semana de pruebas (con P-09).
- Regla de freeze: cupos y presupuestos activos y dimensionados en el acta de flags (6-ago).
