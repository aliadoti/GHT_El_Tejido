# P-03 — Sistema de reinicio de datos del flujo (participante y campaña)

> **Origen:** hoja `Iniciativas` (propuesta Aliado TI; deuda declarada en AVANCES) + ampliación
> solicitada por el usuario el 2026-07-13: borrado fácil de las entidades del flujo para que las
> **pruebas humanas** se repitan sin recrear la campaña.
> **Tipo:** Desarrollo · **Prioridad:** Alta — **PRIMER PASO de la implementación del Sprint 1a**
> (desbloquea todas las re-pruebas de I-01…I-16) · **Dependencia:** — · **Riesgo:** Bajo
> (destructivo por diseño, gated admin + flag). Cubre REQ §26, ARQ §6/§13; specs base `04 §5.3`,
> `03 §3.4-§3.10`.

## 1. Qué pide / por qué
Hoy, para re-probar el flujo conversacional tras un cambio, hay que borrar a mano en Cosmos los
docs del participante (`conversations`, `responses`, a veces `users`), campaña por campaña. Se
necesita un **sistema de borrado de las entidades del flujo** que:
- **conserve** la campaña y toda su configuración (mensajes iniciales, preguntas, rúbrica/prompt/
  ConfigLLM referenciados, participantes asociados y usuarios), y
- **borre fácilmente** lo producido por las interacciones (conversaciones, mensajes, respuestas,
  evaluaciones, Markdown), para que el participante pueda **volver a interactuar desde cero**
  (cold-start de `Reglas §2.1`) sin crear una campaña nueva.

## 2. Estado actual del build
Nuevo. Solo existe la limpieza manual en Cosmos (documentada en los prompts de prueba). El
borrado debe respetar las particiones (`campaniaId` en `conversations`/`responses`).

## 3. Diseño técnico

### 3.1 Qué se borra y qué se conserva (regla central)

| Contenedor / dato | Acción |
|---|---|
| `conversations`: `Conversacion` + `Mensaje` del alcance | **Borrar** (físico) |
| `responses`: `Respuesta` + `Evaluacion` + `ArtefactoMarkdown` del alcance | **Borrar** (físico) |
| Blob de Markdown del alcance | **Borrar si es alcanzable**; un fallo se tolera y se reporta (artefactos regenerables, REQ §22.4.6) |
| `participants`: `ParticipanteCampania` del alcance | **Conservar y resetear**: `estadoRespuesta=sinRespuesta`, `fechaUltimaRespuesta=null`. Con la opción `reiniciarEnvios=true` también `estadoEnvio=Pendiente` y `fechaPrimerEnvio=null` (permite re-disparar el envío inicial desde Envíos) |
| `campaigns` (campaña, mensajes iniciales, preguntas, refs) | **Conservar** |
| `users` (usuarios y tags) | **Conservar** |
| `config` (rúbricas, prompts, ConfigLLM) | **Conservar** |
| `security` (LogSeguridad) | **Conservar** (append-only) + registrar la acción |
| `leases` (WebhookDedupe) | **Conservar** (los mensajes nuevos traen `whatsappMessageId` nuevos; no interfiere) |

Se elige **borrado físico** (no lógico) porque el objetivo es reproducir el cold-start real: el
orquestador resuelve el hilo por existencia de la `Conversacion` (`SUPUESTOS.md#primer-contacto-pregunta`)
y los repos no tienen filtros de "descartado". La auditoría queda en `LogSeguridad`.

### 3.2 Dos alcances, una misma lógica
1. **Por participante:** `POST /api/admin/campanias/{id}/participantes/{usuarioId}/reiniciar`
   — borra/resetea solo lo de ese usuario en esa campaña. Cuerpo opcional:
   `{ "reiniciarEnvios": bool }` (default `false`).
2. **Por campaña (pruebas masivas):** `POST /api/admin/campanias/{id}/reiniciar-datos`
   — borra/resetea lo de **todos** los participantes de la campaña. Cuerpo opcional:
   `{ "usuarioIds": [..], "reiniciarEnvios": bool }` (`usuarioIds` acota a un subconjunto; vacío o
   ausente = todos).

Ambos responden un **reporte de conteos**:
`{ conversaciones, mensajes, respuestas, evaluaciones, artefactos, blobsBorrados, blobsFallidos, participantesReseteados }`
para que el humano confirme qué se limpió.

### 3.3 Servicio y puertos
- **`ServicioReinicioDatos`** (Application, nuevo): orquesta el borrado por alcance y arma el
  reporte. Idempotente: reinvocarlo sobre datos ya limpios devuelve conteos en 0 sin error.
- **Puertos nuevos (internos, no cambian el contrato de datos `03`):**
  - `IRepositorioConversaciones.EliminarPorUsuarioAsync(campaniaId, usuarioId?, ct)` → borra
    conversaciones y sus mensajes; `usuarioId=null` = toda la campaña. Devuelve conteos.
  - `IRepositorioRespuestas.EliminarPorUsuarioAsync(campaniaId, usuarioId?, ct)` → borra
    respuestas, evaluaciones y artefactos. Devuelve conteos y las rutas de blob de los artefactos
    borrados (para el paso de Blob).
  - `IAlmacenBlob.EliminarAsync(ruta, ct)` (nuevo método; fallo tolerado).
  - Implementaciones Cosmos (query de ids dentro de la partición + `DeleteItem` por doc; escala de
    pruebas, sin bulk) + Memoria + fakes de tests.
- **Auditoría:** valor de enum **aditivo** `AccionAdministrativa` al final de
  `TipoEventoSeguridad`; `LogSeguridad` con detalle
  `reinicio_datos:<campaniaId>[:<usuarioId>]:<conteos>` y el correlationId.

### 3.4 Seguridad y salvaguardas
- Guard existente: **rol admin + `X-CSRF-Token`**; rate limit `publico`.
- **Flag de entorno `Seguridad:PermitirReinicioDatos` (default `true`)**: se pone `false` en el
  **acta de flags del freeze (6-ago)** para que en la operación real del día-D el borrado masivo
  quede deshabilitado (el endpoint responde 409 con regla de negocio). El reinicio por
  participante puede permanecer habilitado (útil en soporte); decisión final en el acta.
- **Portal:** botón "Reiniciar datos de prueba" en el detalle de campaña y "Reiniciar
  conversación" por participante en Envíos → Estado por participante. El masivo exige
  **confirmación fuerte** (escribir el nombre de la campaña en el modal); ambos muestran el
  reporte de conteos en un toast/panel.
- Sin PII en logs (solo ids y conteos). Nada se revela al participante.

## 4. Contratos y configuración
- `04 §5.3`: **dos endpoints nuevos** + contrato del reporte — **actualizar la spec en commit
  aparte** (cambio aditivo).
- `03`: sin cambios (borrado físico de docs existentes; el reset de `ParticipanteCampania` usa
  campos existentes).
- Config nueva: `Seguridad:PermitirReinicioDatos` (bool, default `true`).
- Enum aditivo `AccionAdministrativa` en `TipoEventoSeguridad` (al final, preserva valores).

## 5. Riesgos y mitigación
- *Borrado accidental en producción real* → confirmación fuerte en UI + flag para apagarlo en el
  freeze + auditoría con conteos + acotado a una campaña por llamada.
- *Estado parcial si falla a mitad* → orden de borrado: `responses` → `conversations` → reset de
  participantes; el servicio es idempotente (re-ejecutar completa la limpieza). Un blob huérfano
  no afecta (se sobrescribe al regenerar).
- *Confusión con el envío inicial* → `reiniciarEnvios` es opt-in y está documentado: sin él, el
  participante reinicia por cold-start entrante; con él, se puede re-disparar la campaña desde el
  portal.

## 6. Criterios de aceptación / pruebas
- Unit (`ServicioReinicioDatos`): borra solo el alcance pedido; conteos correctos; idempotente
  (segunda llamada = ceros); `reiniciarEnvios` resetea `estadoEnvio` solo cuando se pide;
  campaña/usuarios/config intactos.
- Unit repos: eliminación acotada a `(campaniaId[, usuarioId])`; otras campañas/usuarios intactos.
- Integration: tras reiniciar, el siguiente webhook entrante del participante recibe la **pregunta
  vigente** (cold-start real) y Resultados no muestra registros viejos; sin sesión admin o sin
  CSRF → 401/403; con `Seguridad:PermitirReinicioDatos=false` el masivo responde 409.
- Queda `LogSeguridad(AccionAdministrativa)` con los conteos.
- Spec `04` actualizada en commit aparte; build `-warnaserror`/test/format y lint/test/build verdes.

## 7. Secuencia de implementación sugerida (pasos pequeños)
1. Enum `AccionAdministrativa` + puertos de borrado + impls Memoria + unit repos.
2. `ServicioReinicioDatos` + unit del servicio (conteos, idempotencia, alcances).
3. Impls Cosmos + endpoint por participante + integration cold-start.
4. Endpoint por campaña + flag `PermitirReinicioDatos` + integration 409.
5. Portal (botones + confirmación fuerte + reporte) + lint/test/build.
6. Spec `04 §5.3` en commit aparte; AVANCES/SUPUESTOS al cierre.
