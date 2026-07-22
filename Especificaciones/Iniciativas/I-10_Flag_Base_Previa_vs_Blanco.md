# I-10 — Parametrizar campaña: base previa vs papel en blanco

> **⚠️ DIFERIDA del MVP — reunión GHT 20-jul-2026 (arrastra a I-09).** I-10 es la UI de activación del
> tejido colectivo (I-09); al diferirse I-09 a la Capa 3, su parametrización pierde objeto para el
> Hito. El flag `configConversacional.tejidoColectivo` ya existe en el modelo (lo declaró I-09) y
> queda **OFF**; **no se construye el checkbox de activación** en el portal para el MVP. Retomar con
> I-09 en la Capa 3.
>
> **Origen:** hoja `Iniciativas`. **Tipo:** Desarrollo/Config · **Prioridad:** ~~Alta~~ → **Diferida
> (Capa 3)** · **Ventana:** ~~Sprint 2~~ → **post-convención** · **Dependencia:** I-09 (diferida) ·
> **Riesgo:** Bajo (es un flag). Cubre REQ §11/§26.2, ARQ §6; specs base `03 §3.3`, `04 §5.3`, `07 §2`.

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
