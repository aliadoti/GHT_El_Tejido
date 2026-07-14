# I-10 — Parametrizar campaña: base previa vs papel en blanco

> **Origen:** hoja `Iniciativas`. **Tipo:** Desarrollo/Config · **Prioridad:** Alta ·
> **Ventana:** Sprint 2 (28 jul–1 ago) · **Dependencia:** I-09 · **Riesgo:** Bajo (es un flag).
> Cubre REQ §11/§26.2, ARQ §6; specs base `03 §3.3`, `04 §5.3`, `07 §2`.

## 1. Qué pide GHT / por qué
Elegir **por campaña** si el coach parte del conocimiento ya construido (tejido, I-09) o de
página en blanco. Habilita otros casos de uso (ARMA, P-12) sin reescritura.

## 2. Estado actual del build
La configuración de campaña existe (pestaña Configuración con tabs reales); falta el flag.

## 3. Diseño técnico
1. **Dominio:** campo booleano `TejidoColectivo` en `Campania` (o en `ConfigConversacional`),
   **default `false`** (página en blanco = seguro). Cambio **aditivo** al contrato `03 §3.3` y al
   request/response de `04 §5.3` — **actualizar specs en commit aparte**; los docs existentes
   deserializan `false`.
2. **Backend:** mapeo Cosmos + `ServicioGestionCampanias` (crear/actualizar) + el orquestador lo
   lee para decidir si invoca `IBaseConocimientoCampania` (I-09).
3. **Portal:** checkbox "Usar base de conocimiento común (tejido)" en la pestaña Configuración del
   detalle de campaña (`campanias.page.ts`), con el `PUT` existente.
4. El cambio aplica **sin redeploy** (es configuración de campaña en BD).

## 4. Riesgos y mitigación
Activar el tejido es decisión explícita del admin; default seguro. La decisión de flags del día-D
(acta del 6-ago) define su estado para el Hito.

## 5. Criterios de aceptación / pruebas
- Unit: campaña con flag on → el orquestador consulta la base común; off → no.
- Unit de mapeo: docs viejos sin el campo → `false`.
- Integration: crear/editar campaña persiste el flag; portal lo refleja.
- Specs `03`/`04` actualizadas en commit aparte; build/test/format y lint/test/build verdes.
