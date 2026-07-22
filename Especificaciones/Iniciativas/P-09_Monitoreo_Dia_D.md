# P-09 — Panel de monitoreo en vivo (día-D)

> **⚠️ PANEL DIFERIDO del MVP — reunión GHT 20-jul-2026.** GHT decidió que **una verificación simple
> del estado del sistema es suficiente** para el MVP; el panel/dashboard en vivo se aplaza. Las
> **métricas de consumo de tokens tampoco son prioridad** (las plataformas actuales dan seguimiento
> básico). **SE CONSERVA para el Hito** lo que ya existe y no es "panel": `/health` + `/health/ready`,
> los logs de entrega estructurados que ya emite el `TrabajadorWebhook`, el **acta de flags del día-D
> (§3.4)** y el **runbook de rollback** — esos siguen siendo entregables del go-live (ver
> `QAS/03_Smoke_y_Checklist_Dia_D.md` y `QAS/07_Runbook_Rollback_Contingencia.md`). Lo diferido es
> únicamente el **workbook/pantalla de monitoreo en vivo** (§3.1–§3.2).
>
> **Origen:** hoja `Iniciativas` (propuesta Aliado TI). **Tipo:** Desarrollo (parcial: las señales
> ya existen) · **Prioridad:** ~~Media~~ → **Panel diferido; health-check + acta de flags CONSERVADOS**
> · **Ventana:** semana de Pruebas (4–8 ago) · **Riesgo:** Bajo. Cubre REQ §30, ARQ §13; specs base `10 §6`.

## 1. Qué pide / por qué
Ver en tiempo casi real la salud de **envíos/entregas/errores** para operar el 10-ago sin entrar
a Cosmos ni al Log Stream.

## 2. Estado actual del build
Parcial: `TrabajadorWebhook` ya loguea estados de entrega (`sent/delivered/read/failed` con
`code/detalle`), motivos de rechazo del webhook, errores del LLM y motivos de fallback
(`salida_invalida:<razon>`), todo estructurado y sin PII.

## 3. Diseño técnico (workbook primero, código solo si sobra tiempo)
1. **Entrega principal (sin código):** **workbook de Application Insights** con: envíos por
   estado, tasa de entrega, errores por code (131042 y otros), latencia y tasa de fallback LLM,
   tokens/costo estimado (cuando P-10 los emita), % cierres por umbral vs por techo, distribución
   *ideas por mensaje* (I-06). Alertas: fallo de entrega > X%, fallback LLM > Y%, costo > 80% del
   cupo de campaña.
2. **Opcional (código, solo si hay tiempo antes del freeze):** pantalla mínima del portal que
   consuma `GET .../envios` + un endpoint de métricas agregadas. Preferir el workbook para no
   meter código nuevo antes del freeze.
3. **Runbook del día-D (T-40):** por cada síntoma del dashboard, qué flag apagar
   (`SegmentacionIdeas`, `tejidoColectivo`, `UmbralCierreAnticipado`, cupos) y el procedimiento de
   rollback. Nunca hotfix en caliente el 10-ago.

### 3.4 Acta de flags del día-D (6-ago) — decisión registrada, no omisión
Cada flag queda **ON solo si pasó** su precondición (calibración/carga/UAT/costo); en el acta se marca
la decisión explícita (evita que una activación se quede implícita en un runbook). Rollback de todos:
poner el valor en `0`/`false` sin redeploy (App Setting), coherente con el runbook §3.3.

- [ ] **`Conversacion:SegmentacionIdeas` (I-06 multi-idea)** — ON/OFF. Precondición: carga + UAT + costo.
- [ ] **`tejidoColectivo` por campaña (I-09/I-10)** — ON/OFF. Precondición: carga + UAT + costo + consentimiento (P-07).
- [ ] **`Conversacion:UmbralCierreAnticipado` (I-01)** — valor/OFF. Precondición: **rúbrica I-11 congelada
  (18-jul)** + **corrido D5 real** + valor elegido (P85–P90). Regla D2: no aflojar el tope determinístico
  de revisiones hasta que los cupos P-10 estén activos en producción. Ver `Runbook_I-01_Umbral_Cierre_Anticipado.md`.
- [ ] **Cupos/costo P-10** (`Conversacion:CuposHabilitados`, presupuesto por campaña, rate por número) — activos y dimensionados.

## 4. Criterios de aceptación
- El workbook queda validado en la semana de pruebas (ensayo de monitoreo, T-41).
- El día del envío se ve en tiempo casi real cuántos mensajes salieron, cuántos se entregaron y
  qué errores hubo, sin entrar a Cosmos.
- Si se emite código (pantalla/endpoint), sigue el guard admin y no expone PII.
