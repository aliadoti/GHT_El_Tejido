# P-09 — Panel de monitoreo en vivo (día-D)

> **Origen:** hoja `Iniciativas` (propuesta Aliado TI). **Tipo:** Desarrollo (parcial: las señales
> ya existen) · **Prioridad:** Media · **Ventana:** semana de Pruebas (4–8 ago) · **Riesgo:** Bajo.
> Cubre REQ §30, ARQ §13; specs base `10 §6`.

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

## 4. Criterios de aceptación
- El workbook queda validado en la semana de pruebas (ensayo de monitoreo, T-41).
- El día del envío se ve en tiempo casi real cuántos mensajes salieron, cuántos se entregaron y
  qué errores hubo, sin entrar a Cosmos.
- Si se emite código (pantalla/endpoint), sigue el guard admin y no expone PII.
