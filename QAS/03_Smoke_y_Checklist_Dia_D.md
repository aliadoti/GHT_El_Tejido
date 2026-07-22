# 03 — Smoke Post-Deploy y Checklist Día-D (Go/No-Go)

> **Uso:** el smoke se corre tras **cada despliegue**; el checklist día-D es el **gate final** antes del envío del 10-ago.
> Marca cada ítem `OK / FALLA / N/A`. **Cualquier FALLA en un ítem CORE = No-Go** hasta cierre del defecto.

---

## 1. Smoke post-deploy (≈15 min, corre tras cada deploy)

Objetivo: detectar rupturas obvias antes de invertir en la suite completa. Todo en **SIM**.

| # | Chequeo | Cómo | Esperado | OK/FALLA |
|---|---|---|---|---|
| S1 | Salud API | `GET /health` | 200 | |
| S2 | Salud con diagnóstico | `GET /health/ready` (con `X-Diag-Key`) | OK | |
| S3 | Portal servido | abrir `https://<webapp>/health` y `/login` | 200 / carga | |
| S4 | Simulación activa | abrir `/simulacion-whatsapp` | carga; pide `X-Diag-Key` | |
| S5 | Login admin OTP | crear admin + emitir OTP + login | entra al portal | |
| S6 | Config mínima presente | ver campaña QA con rúbrica/prompt/config activos | todo activo/aprobado | |
| S7 | Webhook firmado 200 | enviar respuesta simulada de P1 (firma válida) | 200; procesa | |
| S8 | Firma inválida 401 | enviar con app secret errado | 401; sin efecto | |
| S9 | Evaluación básica | P1 responde evaluable | Evaluación con snapshots; retro saliente | |
| S10 | No fuga de rúbrica (humo) | leer la retro de S9 | sin nombre de criterio / patrón `N/M` / "rúbrica" | |
| S11 | Markdown sin secretos | ver Markdown generado | no vacío; sin API key/secreto | |
| S12 | Reinicio P-03 | reiniciar participante P1 | reporte de conteos; cold-start listo | |

**Gate del smoke:** S1–S12 todos OK antes de seguir con la suite CORE. S8 y S10 son **críticos** (seguridad); su falla detiene todo.

---

## 2. Checklist Día-D (Go/No-Go) — gate de release

### 2.1 CORE funcional (bloqueante)
- [ ] CNV-01..07 en verde (cold-start, evaluación+snapshots, retro+1 invitación, repregunta única, multi-pregunta, salida natural, cierre+Markdown).
- [ ] ROB-01..07, ROB-09 en verde (fallback ×3, dedupe ×2, firma, ventana 24h, multi-pregunta).
- [ ] ADM-01..07 en verde (CRUD, envíos, snapshots/regeneración).
- [ ] ADM-08..09 en verde (carga masiva + idempotente) — **con la lista real del freeze**.
- [ ] ADM-10..11 en verde (P-03 participante y campaña + 409 con flag off).
- [ ] AUT-01..02, AUT-04..05 en verde (OTP válido/ inválido, key enmascarada, CSRF/sesión).

### 2.2 CORE seguridad/privacidad (bloqueante — cero tolerancia)
- [ ] SEC-01..05: **no fuga de rúbrica** (3 corridas c/u, 0 fugas). `FiltroSalidaRubrica` intercepta.
- [ ] Verificar que **`tejidoColectivo` = OFF en todas las campañas** (I-09 diferido, reunión 20-jul). SEC-06..08 (no fuga de PII) solo aplican si por error quedara ON — en ese caso son bloqueantes.
- [ ] SEC-09..12: **prompt-injection** directa y transitiva neutralizadas; sin exfiltración de secretos.
- [ ] SEC-13: rechazo neutral de no autorizados.
- [ ] SEC-14: sin secretos/PII en Markdown/logs.
- [ ] SEC-15: firma inválida rechazada.

### 2.3 CORE guardrails deterministas (bloqueante)
- [ ] GRD-01 cupo mensajes/usuario (si `CuposHabilitados` va ON).
- [ ] GRD-02 cupo llamadas LLM/usuario (si `CuposHabilitados` va ON).
- [ ] GRD-03 techo de turnos por hilo.
- [ ] GRD-04 rate por número.
- [ ] GRD-06 umbral efectivo `campaña ?? global` (si I-01/umbral va ON) — **regla D2: no aflojar el tope de revisiones sin cupos activos en producción**.

### 2.4 Release (Definition of Release, `13 §7`)
- [ ] CI verde en `main` (build `-warnaserror` + test + lint).
- [ ] Deploy exitoso; `/health` y `/health/ready` OK.
- [ ] Recursos Azure y app de WhatsApp configurados.
- [ ] **Plantillas WhatsApp aprobadas por Meta** (inicio HSM, autenticación, repregunta).
- [ ] Secretos **solo** en Key Vault; ninguno en repo/logs/Markdown.
- [ ] **E2E real con 5 usuarios** ejecutado (recepción física + ventana 24h confirmadas).
- [ ] `SUPUESTOS.md` revisado; ambigüedades documentadas.

### 2.5 Acta de flags día-D (6-ago) — decisión ON/OFF
Registrar la decisión y quién la firma. **Un flag solo va ON si pasó carga + UAT + costo.**

| Flag / App Setting | Default | Decisión día-D | Firmado |
|---|---|---|---|
| `Conversacion:SegmentacionIdeas` (I-06) + `configConversacional.segmentacionIdeas` por campaña | kill-switch `true` / campo `false` | ON / **OFF** | |
| `Conversacion:TejidoColectivo` (I-09) + `tejidoColectivo` por campaña | kill-switch `true` / campo `false` | **OFF obligatorio — DIFERIDO reunión 20-jul (no es decisión)** | |
| `Conversacion:UmbralCierreAnticipado` (I-01) / `Conversacion:UmbralMadurez` (I-17) o override P-13 por campaña | `0` (off) | valor / **OFF** | |
| `Conversacion:Parafraseo` (I-05) + `parafraseo` por campaña | kill-switch `true` / campo `false` | ON / **OFF** (recordar: reunión 20-jul pide paráfrasis **solo tras umbral**, I-17) | |
| `Conversacion:CuposHabilitados` (P-10) | `false` | **ON** recomendado antes de aflojar I-01 (D2) / OFF | |
| `Conversacion:MaxTurnosPorHilo` | `0` (off) | valor ≈ `2+MaxRepreguntas` | |
| `Seguridad:RateNumeroWhatsAppPorMinuto` | `0` (off) | valor | |
| `Conversacion:HorasExpiracionSinRespuesta` | `0` (off) | p. ej. `72` | |
| `Seguridad:PermitirReinicioDatos` (P-03) | `true` | **`false`** en el freeze | |
| `Simulacion:Habilitada` | — | **`false`** al cerrar pruebas | |

> **Regla de dependencia (D2):** no retirar/aflojar el tope determinístico de revisiones (I-01 vía umbral) **hasta** que los cupos P-10 estén activos en producción. Si I-01 va ON, `CuposHabilitados` debe ir ON.

### 2.6 Freeze (8–9 ago)
- [ ] Code freeze aplicado.
- [ ] Carga real de participantes (I-08) ejecutada y verificada (reporte por fila sin rechazos inesperados).
- [ ] **Dry-run E2E completo** con datos reales (1 participante real de punta a punta).
- [ ] Rúbrica/prompts/seeds congelados; rúbrica **no** en estado `borrador`.
- [ ] `Seguridad:PermitirReinicioDatos=false`; `Simulacion:Habilitada=false`.
- [ ] Runbook de rollback (`07_*`) impreso/a mano; responsables de ops notificados.

### 2.7 Decisión final
- **GO** si: 2.1–2.4 completos, 0 defectos Críticos/Altos abiertos, acta de flags firmada (2.5), freeze cerrado (2.6).
- **NO-GO** si: cualquier CORE en falla, o defecto Crítico/Alto abierto en seguridad/guardrails, o plantillas HSM no aprobadas.

Firma go/no-go: __________________  Fecha/hora: __________  Responsable: __________

*Fin del documento.*
