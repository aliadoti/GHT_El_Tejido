# P-15 — Refactorizar el orquestador conversacional

**Estado:** WIP — Corte 1 de 3 DONE local (2026-07-24, Claude Opus 4.8)  
**Origen:** `CAL-001` de la auditoría técnica del 2026-07-24.  
**Dependencias:** ninguna externa. Conserva el contrato actual de `IOrquestadorConversacion`.

> **Avance 2026-07-24 (Claude Opus 4.8, Arquitecto/Backend/SDET) — Corte 1/3 DONE local.** Se extrajo la
> **política determinista y sin E/S** a `PoliticaLimitesConversacion` (`src/ElTejido.Application/Conversacion/`):
> resolución de umbral base/cierre/origen (precedencia pregunta → campaña → global), clasificación de
> madurez (I-17), valor de corte y elegibilidad de mejora. El orquestador delega en `_limites` sin cambiar
> flujo, mensajes, orden, persistencia, flags ni contratos. Sin lógica de límite duplicada entre la fachada
> y el colaborador. **Verificado local (SDK 8.0.412):** `dotnet build -c Release -warnaserror` 0/0;
> `dotnet test --filter "Category!=Calibracion"` **443 verde (391 unit + 52 integration; +20
> `PoliticaLimitesConversacionTests`)** — las 371 pruebas previas siguen verdes (regresión preservada);
> `dotnet format --verify-no-changes` limpio. **Pendiente:** Corte 2 (`ResolvedorTransicionConversacion`:
> resolución de la transición) y Corte 3 (`ProcesadorResultadoEvaluacion`: efectos posteriores).

## 1. Propósito

Separar las responsabilidades internas de `OrquestadorConversacion` sin cambiar lo que recibe una persona participante por WhatsApp, las transiciones de la conversación, los registros, ni los contratos HTTP o de mensajería. El archivo actual concentra lectura y persistencia de estado, límites, interpretación de intención, evaluación, mensajes y efectos posteriores; ello dificulta comprobar cambios aislados.

## 2. Alcance confirmado

Se trabajará dentro de `src/ElTejido.Application/Conversacion/` y sus pruebas. La fachada pública seguirá siendo `IOrquestadorConversacion` y `OrquestadorConversacion`; los llamadores no deben conocer los colaboradores nuevos.

La extracción se hará en tres cortes revisables:

1. Políticas deterministas y sin E/S: límites, elegibilidad de repreguntas y decisiones de cierre.
2. Resolución de la transición conversacional: interpretación de la situación actual y decisión de la siguiente acción.
3. Efectos posteriores a una evaluación: persistencia, Markdown, mensajes y registros de seguridad, conservando el orden actual de los efectos.

No se modifica en esta iniciativa la configuración funcional de campañas, el proveedor LLM, las reglas de cierre anticipado ni los textos destinados a participantes.

## 3. Diseño de implementación

- Mantener una fachada delgada que coordine colaboradores con nombres orientados al dominio, por ejemplo `PoliticaLimitesConversacion`, `ResolvedorTransicionConversacion` y `ProcesadorResultadoEvaluacion`.
- Cada política extraída recibirá entradas explícitas y devolverá una decisión tipada; no leerá configuración, reloj, repositorios ni servicios externos directamente.
- Los colaboradores que produzcan efectos dependerán de puertos ya existentes o de interfaces pequeñas introducidas para ese fin. No se duplicará la lógica de repositorios ni se trasladarán secretos o configuración al frontend.
- Conservar en una sola ruta la creación de correlación, los `LogSeguridad`, el manejo de idempotencia y el orden observable de persistir/enviar. Un refactor no debe enviar dos mensajes ni crear una transición adicional.
- El constructor de la fachada solo retendrá dependencias necesarias para coordinar los colaboradores; no se impondrá una cifra artificial de parámetros si perjudica la cohesión de los nuevos servicios.
- Extraer una unidad por vez, compilar y cubrir la decisión extraída antes de mover el siguiente bloque. El resultado debe dejar métodos principales cortos y legibles por etapas de conversación.

## 4. Contratos y compatibilidad

| Superficie | Regla de compatibilidad |
|---|---|
| `IOrquestadorConversacion` | Sin cambio de firma ni semántica. |
| Estado de conversación y respuestas | Misma transición, mensajes, orden y persistencia para la misma entrada. |
| Feature flags | Se respetan las banderas actuales; P-15 no las activa ni cambia sus valores por defecto. |
| Observabilidad | Se conservan correlación y eventos de seguridad existentes. |

## 5. Criterios de aceptación y pruebas

- Las pruebas existentes de conversación mantienen el mismo resultado para cierre, repregunta, continuidad, evaluación y errores recuperables.
- Se agregan pruebas unitarias para las políticas extraídas, incluidos los límites y los casos de campaña sin configuración opcional.
- Una prueba de regresión recorre una entrada representativa y verifica que no cambia el número ni el orden de persistencias y mensajes enviados.
- No quedan reglas de límite o de decisión de transición duplicadas entre la fachada y los colaboradores.
- La solución compila y supera la puerta backend acordada: `dotnet build -c Release -warnaserror`, `dotnet test -c Release --no-build --filter "Category!=Calibracion"` y `dotnet format --verify-no-changes`.

## 6. Cómo probarlo

1. Abrir una campaña de prueba ya existente y enviar una respuesta normal, una petición de continuar y una respuesta que alcance el cierre.
2. Confirmar que llegan los mismos mensajes que antes y que no se repite ningún envío.
3. Revisar el resultado de la conversación en el portal: deben aparecer una sola transición y los registros esperados.
4. Es un fallo si cambia el texto o el momento del mensaje sin una decisión funcional aprobada, si se duplica un envío o si no se conserva el registro de seguridad.

## 7. Riesgo, reversión y siguiente paso

El riesgo principal es alterar de forma accidental el orden de efectos. Los cortes pequeños, las pruebas de traza y conservar la fachada hacen que la reversión sea un cambio de código localizado. Al terminar P-15, el siguiente ítem es P-16; ambos pueden desarrollarse sin esperar los bloqueos externos de Sprint 2.
