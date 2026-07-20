# I-05 — Parafraseo y transparencia del proceso

> **Origen:** hoja `Iniciativas` de `Presentacion/Plan_Trabajo_El_Tejido.xlsx`.
> **Tipo:** Prompt + Desarrollo · **Prioridad:** Alta · **Ventana:** Sprint 1b (21–25 jul) ·
> **Dependencia:** — · **Riesgo:** No determinismo (medio) · **Estado:** DONE local 2026-07-20 (flags apagados).
> Cubre REQ §21, ARQ §4.2; specs base `08 §4`, `05 §4.5`.

## 1. Qué pide GHT / por qué
El coach devuelve "**esto es lo que entendí**": parafrasea y organiza el aporte del participante
antes de la retro/invitación, mostrando la elaboración. Evita el seco "gracias, registrado".

## 2. Estado anterior del build
Antes de I-05 la retro era breve y no incluía parafraseo. La entrega local conserva ese flujo cuando
los flags están apagados.

## 3. Diseño técnico
1. **Contrato de salida (aditivo):** agregar `parafraseo_devuelto` (string breve, opcional) al
   esquema JSON del evaluador (`08 §4`). `ConstructorMensajesEvaluacion` lo pide en el `system`
   ("resume con tus palabras lo que la persona aportó, 2–3 frases, fiel al texto, sin inventar").
2. **Dominio:** campo opcional `ParafraseoDevuelto` en `Evaluacion` (mapeo Cosmos aditivo,
   `03 §3.9`; los docs viejos leen `null`).
3. **Validación post-proceso (determinista):** límite duro de longitud
   (`Conversacion:MaxCaracteresParafraseo`, default 400); si excede se trunca en frontera de frase;
   si falta o queda vacío, el flujo actual queda intacto (retro breve sin parafraseo).
4. **Orquestador:** antepone el parafraseo a la retro en el mensaje de repregunta/cierre
   (`parafraseo + "\n\n" + retro + ...`), reutilizando `Combinar`.

## 4. Contratos y configuración
- `08 §4`: campo `parafraseo_devuelto` — **actualizar la spec en commit aparte** (cambio aditivo).
- `03 §3.9`: campo opcional en el doc de evaluación — mismo commit de spec.
- Config nueva: `Conversacion:MaxCaracteresParafraseo` (default 400).
- **Parametrización por campaña (decisión de usuario 2026-07-20):** campo aditivo `parafraseo`
  (bool, default `false`) en `ConfigConversacional`; una campaña formal/corta lo puede apagar. El
  kill-switch global `Conversacion:Parafraseo` (default `true`) evita solicitar y mostrar el campo
  para todas las campañas sin redeploy. Campo LLM ausente = retro clásica.

## 5. Riesgos y mitigación (R-01b)
- *Alucinación/deriva del parafraseo* → es **dato mostrado**, nunca acción; instrucción de
  fidelidad; banco de calibración (D5) valida fidelidad por muestreo.
- *Longitud excesiva* → truncado determinista.
- *Salida sin el campo* → comportamiento previo intacto (cero regresión).

## 6. Criterios de aceptación / pruebas
- Unit: salida con `parafraseo_devuelto` válido → el mensaje enviado empieza con el parafraseo.
- Unit: salida sin el campo → mensaje idéntico al comportamiento actual.
- Unit: parafraseo de 1000 chars → truncado a `MaxCaracteresParafraseo`.
- Unit de mapeo Cosmos: doc sin el campo deserializa con `null`.
- Build/test/format verdes; specs `08`/`03` actualizadas en commit aparte.

## 8. Entrega local 2026-07-20
- `SalidaLlmEvaluacion.parafraseo_devuelto` y `Evaluacion.ParafraseoDevuelto` son opcionales de punta
  a punta; Cosmos los persiste aditivamente y Resultados los expone.
- El orquestador solicita el campo únicamente con ambos flags activos y lo antepone a la retro en
  repregunta/cierre. El límite de 400 caracteres conserva la última frase completa o descarta el
  parafraseo, sin degradar el hilo.
- Regresión determinista: prompt con/sin campo, truncado, ausencia, flag por campaña, kill-switch y
  round-trip Cosmos. La medición operativa pendiente es el baseline D5 pagado contra staging.

## 7. Degradación
El campo es opcional de punta a punta: si el modelo no lo produce o la validación lo descarta,
el sistema opera exactamente como hoy.
