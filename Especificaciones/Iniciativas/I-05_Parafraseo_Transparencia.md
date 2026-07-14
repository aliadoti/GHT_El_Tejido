# I-05 — Parafraseo y transparencia del proceso

> **Origen:** hoja `Iniciativas` de `Presentacion/Plan_Trabajo_El_Tejido.xlsx`.
> **Tipo:** Prompt + Desarrollo · **Prioridad:** Alta · **Ventana:** Sprint 1b (21–25 jul) ·
> **Dependencia:** — · **Riesgo:** No determinismo (medio).
> Cubre REQ §21, ARQ §4.2; specs base `08 §4`, `05 §4.5`.

## 1. Qué pide GHT / por qué
El coach devuelve "**esto es lo que entendí**": parafrasea y organiza el aporte del participante
antes de la retro/invitación, mostrando la elaboración. Evita el seco "gracias, registrado".

## 2. Estado actual del build
Nuevo: hoy la retro es breve y no hay parafraseo.

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
- **Parametrización por campaña (candidata, decidir al implementar — `00_Indice §4.3`):** campo
  aditivo `parafraseo` (bool) en `ConfigConversacional` para que una campaña formal/corta pueda
  apagarlo; si no se agrega, queda global-on con degradación natural (campo LLM ausente = retro clásica).

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

## 7. Degradación
El campo es opcional de punta a punta: si el modelo no lo produce o la validación lo descarta,
el sistema opera exactamente como hoy.
