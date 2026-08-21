# Adenda a la Decisión de congelamiento — Convención 2026

> **Fecha:** 2026-08-20.
> **Modifica:** `Decision_Congelamiento_Codigo_Convencion_2026.md` (2026-08-16).
> **Motivo:** el montaje del ambiente de producción y el piloto controlado obligaron a desviarse de
> cuatro condiciones del acta original y a publicar tres cambios de código posteriores al artefacto
> congelado.
> **Estado:** APROBADO CONDICIONADO — el alcance sigue siendo una sola campaña operativa en un
> ambiente exclusivo, con base de datos y configuración creadas desde cero.

## 1. Qué cambió respecto del acta original

El acta congelaba `28c3cb1`. El ambiente de producción se montó en `rg-eltejido-prod-eus2`
(East US 2) y hoy opera el artefacto `v1.0.3-convencion`. Entre el commit congelado y el desplegado
hay **20 archivos modificados en `src/` y `tests/`, 1158 inserciones y 82 eliminaciones**.

Los tres cambios de código salieron de defectos encontrados durante el piloto controlado en
producción, que es la función que el acta le asignaba a las puertas operativas. Ninguno responde a
alcance nuevo.

## 2. Versiones publicadas en producción

| Tag | Commit | Contenido |
|---|---|---|
| `v1.0.0-convencion` | `6ba24a6` | Artefacto congelado más los archivos de pipeline. Sin cambios en `src/` ni `tests/` respecto de `1215872`. |
| `v1.0.1-convencion` | `1b74a9d` | Respaldo del evaluador en el idioma del hilo; reconocimiento de la consulta de idea en inglés. |
| `v1.0.2-convencion` | `ff54bb0` | `DT-P33-01`: clasificación semántica de consulta y confirmación de idea; `GuardaCuposLlm`. |
| `v1.0.3-convencion` | `85b78f8` | Hotfix determinista DT-P33-01: afinidad P-33 + alias exacto antes del LLM y catálogo inglés ampliado. |

### 2.1 Observación de trazabilidad sobre `v1.0.1-convencion`

El commit `1b74a9d` lleva el mensaje `docs: plan de produccion, prompt de evaluacion y catalogos
exportados del montaje PRD`, pero contiene **además** los cuatro archivos de `src/` y los tres de
`tests/` que componen la corrección de `v1.0.1`. El commit `fix(conversacion)` previsto nunca llegó a
crearse: el índice quedó cargado y el commit siguiente lo arrastró.

El contenido es íntegramente rastreable —`git diff v1.0.0-convencion..v1.0.1-convencion -- src tests`
devuelve exactamente la corrección— pero **el mensaje del commit no describe su contenido**. No se
reescribe la historia: está publicada, etiquetada y desplegada en producción. Queda registrada aquí.

El mismo commit incorporó `.obsidian/workspace.json`, estado local del editor que no debía versionarse.
Corregir en un commit posterior añadiéndolo a `.gitignore`.

## 3. Cambios de código posteriores al congelamiento

### 3.1 `v1.0.1` — Respaldo del evaluador fijado en español

**Defecto.** El respaldo de seguridad del evaluador estaba escrito en español como constante. Ante
fallo del proveedor, fuga de rúbrica o salida inválida, un hilo en inglés recibía
`Gracias, registramos tu aporte.` seguido de contenido en inglés.

**Detección.** Piloto del 2026-08-20. Corresponde a un criterio de detención explícito de
`DT-I20-02`: mezcla de idiomas.

**Corrección.** El respaldo se resuelve por el idioma del hilo. Se retiró además un patrón inglés
puntual del detector de consulta de idea, que era un parche.

### 3.2 `v1.0.2` — Clasificación semántica de consulta y confirmación (`DT-P33-01`)

**Limitación.** El reconocimiento de intenciones dependía de que cada paráfrasis estuviera en el
catálogo activo. Expresiones legítimas caían en el menú de aclaración.

**Corrección.** El clasificador LLM reconoce `consultarIdea` y `confirmarIdea`, con un máximo de una
clasificación por mensaje. El servidor conserva autoridad sobre campaña, idea, versión y transición.
Gate nuevo `Conversacion:ClasificacionSemanticaConsultaIdeaHabilitada`, apagado por defecto.

**Costo.** Introduce una llamada adicional al LLM por mensaje entrante. Con revisiones ilimitadas,
cupos deshabilitados y presupuesto de tokens en cero, no existe techo de consumo por participante.

### 3.3 Cambio de modelo LLM

**Defecto.** Toda evaluación caía en fallback con `salida_invalida:no_json`. El cliente HTTP solo
envía `response_format: json_object` a proveedores OpenAI y compatibles; el proveedor configurado
inicialmente devolvía texto libre y el parseo se aplica directo sobre la respuesta, sin extraer el
bloque JSON.

**Efecto observado.** Calificación 0, motivo de cierre `fallbackEvaluacion`, nivel `incubacion` y
retroalimentación neutra en todas las ideas del piloto. Ninguna evaluación se completó.

**Corrección.** Cambio de modelo en ConfigLLM. Es parametrización, no código.

### 3.4 `v1.0.3` — Hotfix determinista posterior a `v1.0.2`

**Defecto.** Con `v1.0.2-convencion` desplegada y los gates semántico/visibilidad activos,
`How is my idea going?` mostró la idea, pero la respuesta `No is all right for me` fue tratada como
aporte y generó otra pregunta. La frase no estaba en el catálogo inglés activo y el clasificador puede
proponer `aportar` para redacciones atípicas.

**Corrección preparada.** Si existe la afinidad exacta creada por el envío P-33 y el mensaje completo
coincide con `frases.confirmar`, el servidor transporta `ConfirmarIdea` antes del LLM y sin tokens. La
transición conserva todas las validaciones server-side. Los mensajes mixtos no coinciden de forma
exacta y siguen como aporte. Se agregaron siete alias a la semilla inglesa y al catálogo v3 importado
como borrador; el español v3 activo se revisó sin modificarlo.

**Estado.** `85b78f8` / `v1.0.3-convencion` desplegado; workflow verde, `/health/ready=ok` y catálogo
inglés v3 activo. El gate local fue 1053 unitarias + 121 integración, build Release, formato y diff
verdes. La validación conversacional se integra a la corrida final cuando termine el fix completo.

## 4. Desviaciones de configuración aceptadas

### 4.1 Simulación habilitada de forma permanente

**Contradice la condición 5** del acta, que exigía `Simulacion__Habilitada=false` y no usar clave de
diagnóstico.

**Motivo.** Es el mecanismo de creación del administrador inicial y la única vía de recuperación de
acceso administrativo.

**Riesgo asumido.** Quien posea la `X-Diag-Key` puede sobrescribir el usuario administrador, emitir un
OTP con el código que elija e inyectar mensajes en nombre de cualquier participante. Es la credencial
raíz de facto del ambiente.

**Controles compensatorios.** Clave de 48 bytes alfanuméricos, almacenada únicamente en Key Vault y
referenciada por `Diagnostico__ClaveSecretName`; alerta de Application Insights sobre cualquier
petición a `/diagnostico/simulacion/*`; rotación al cerrar la parametrización y al cerrar el evento;
procedimiento de apagado en 60 segundos documentado en el plan de producción §17.3.

### 4.2 Firewall de Cosmos abierto a los datacenters de Azure

La cuenta de Cosmos tiene filtro de IP activo con la dirección especial `0.0.0.0`, que admite
peticiones desde cualquier suscripción de Azure.

**Motivo.** Está previsto ampliar el App Service Plan antes de la convención, y un cambio de tier
altera las IP de salida del App Service. Fijarlas habría dejado de funcionar en el momento de mayor
exposición, con un error que el sistema reporta como problema de rol de datos.

**Riesgo asumido.** El firewall de IP queda prácticamente decorativo. El control efectivo pasa a ser
íntegramente la autenticación por Entra ID: alcanzar el endpoint no basta, hace falta un token con rol
de datos sobre esta cuenta, que solo tiene la identidad administrada del App Service.

**Alternativa descartada.** Integración con VNet y service endpoint. Queda como deuda de endurecimiento
post-convención.

### 4.3 Dos campañas en el ambiente

**Contradice la condición 2** del acta, que exigía una sola campaña.

**Motivo.** El cliente solicitó una prueba controlada en producción con un subconjunto de usuarios. Se
creó por duplicación una campaña piloto desechable, de modo que los defectos encontrados se
corrigieran sobre la campaña real mientras seguía en borrador, sin editar una campaña activa —escenario
para el que `DT-P32-05` sigue sin guarda implementada.

**Condiciones.** El piloto se creó por duplicación y nunca se editó la original a partir de él; solo
una campaña estuvo activa en cualquier momento; el piloto queda en estado `cerrada` antes de activar
la campaña real.

### 4.4 Token de WhatsApp provisional

El secreto `wa-token` de producción contiene actualmente el token del ambiente de desarrollo, cargado
para desbloquear la configuración mientras se aprobaba la generación de un system user dedicado.

**Riesgo asumido.** Una rotación o revocación hecha en desarrollo deja producción sin capacidad de
envío, en silencio y sin error visible.

**Condición de cierre.** Sustituir por el token del system user `eltejido-prod`, con expiración
`Never`, **antes del primer envío real**. Mientras persista, ese token no se rota ni se revoca sin
verificar ambos ambientes.

## 5. Condiciones del acta original que se mantienen

1. Ambiente exclusivo, sin documentos legacy. La base nació con un único usuario administrador.
2. Una sola campaña **operativa**; se crea y completa en borrador antes de activarla.
3. Después de activar o del primer envío no se editan campaña, localizaciones, mensajes, preguntas,
   rúbrica, prompts ni catálogos.
4. Una sola versión operativa por familia de rúbrica y de prompt.
5. Los secretos se inyectan por referencia y no se guardan en archivos ni reportes.

## 6. Puertas operativas pendientes antes del primer envío

1. Al terminar el fix completo, ejecutar sobre `v1.0.3-convencion` y el catálogo inglés v3 la corrida
   integral abierto/cerrado/mixto de QAS/25; la validación anterior cubría `v1.0.2-convencion`.
2. Sustituir `wa-token` por el de producción (§4.4).
3. Rotar `diag-key` al cerrar la parametrización.
4. Alertas de Application Insights, incluida la del endpoint de simulación (§4.1).
5. Budget alert sobre el grupo de recursos y verificación del backup continuo de Cosmos.
6. Alinear el texto del mensaje inicial con el cuerpo aprobado de la plantilla de Meta.
7. Ejecutar D5 con la configuración definitiva y autorización de costo.
8. Cerrar la campaña piloto, asociar participantes reales y activar la campaña real.
9. Verificar readiness en verde inmediatamente antes del envío.

Si cualquiera de estas puertas falla, no se corrige en caliente: se detiene el envío, se conserva la
evidencia y se decide explícitamente si se parametriza de nuevo o se descongela el código.

## 7. Deuda aceptada fuera del alcance

- `DT-P32-05`: guarda de edición de campaña activa, sin implementar.
- `DT-QA-03`: salida simulada observable para QA.
- Integración con VNet para el acceso a Cosmos (§4.2).
- Encapsulamiento de `GuardaCuposLlm`: la clase calcula límites sin conocer el kill-switch
  `Conversacion:CuposHabilitados`, que aplican sus consumidores. Se verificó que las cinco rutas
  llamadoras lo respetan. Falta prueba de regresión que lo garantice ante refactores futuros.
- `.obsidian/workspace.json` versionado por error (§2.1).

La aceptación de estas deudas aplica solo al alcance descrito; no constituye aceptación general para
operación continua, múltiples campañas ni edición posterior a la activación.
