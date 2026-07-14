# P-08 — Recordatorios/nudges a quien no ha respondido (rama deseable)

> **Origen:** hoja `Iniciativas`. **Tipo:** Desarrollo · **Prioridad:** Media · **Ventana:** rama
> deseable · **Dependencia:** P-01/P-02 (entrega real + plantillas) · **Riesgo:** costo de
> mensajes / ventana 24h.

## 1. Alcance
Recordatorios suaves a participantes con `estadoRespuesta=sinRespuesta` para subir la tasa de
participación durante la convención.

## 2. Diseño (borrador)
- **Restricción dura:** fuera de la ventana de 24h WhatsApp exige **plantilla HSM aprobada**
  (`05 §2.2`) → requiere una plantilla de recordatorio aprobada por Meta (radicar con antelación,
  como P-02) y tiene costo por mensaje.
- Reutiliza el mecanismo de reenvío existente (`ServicioEnvios` filtra por
  `estadoRespuesta=sinRespuesta`, ARQ §4.4): un **job programado** (`IHostedService` con
  `Seguridad:NudgeHorasSinRespuesta`, 0=off) o disparo manual desde Envíos (primera entrega:
  manual, más simple y controlable).
- Límite de nudges por participante (p. ej. 1) registrado en `EnvioMensaje.tipo=Recordatorio`
  (valor de enum aditivo) para no insistir.

## 3. Nota de alcance
No bloquea el Hito (el envío inicial + la ventana que abre cada respuesta cubren el flujo). Si se
retoma, actualizar `04 §5.4`/`03 §3.5` (tipo de envío) en commit aparte.
