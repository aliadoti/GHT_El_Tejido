# QAS — Paquete de QA · El Tejido

Paquete completo de pruebas E2E para validar **El Tejido** antes de producción (**Hito 12-ago-2026**), ejecutable por **1 tester manual**, enfoque **risk-based**.

## Contenido

| # | Documento | Para qué |
|---|---|---|
| 00 | [Plan de Pruebas](00_Plan_de_Pruebas.md) | Alcance, estrategia risk-based, ambientes SIM/REAL, roles (1 persona), cronograma freeze/día-D, criterios entrada/salida, **gestión del no determinismo**. |
| 01 | [Matriz de Trazabilidad](01_Matriz_Trazabilidad.md) | Iniciativa/REQ → caso(s) → prioridad → estado. Cobertura obligatoria de seguridad y guardrails. |
| 02 | [Casos de Prueba E2E](02_Casos_de_Prueba_E2E.md) | **50+ casos** CNV/SEC/AUT/ADM/GRD/ROB/FLG. Cada uno con resultado esperado + criterio tolerante. **CORE vs Ext**. |
| 03 | [Smoke y Checklist Día-D](03_Smoke_y_Checklist_Dia_D.md) | Smoke post-deploy (12 chequeos) + gate go/no-go + **acta de flags**. |
| 04 | [Datos de Prueba y Reinicio](04_Datos_de_Prueba_y_Reinicio.md) | Campañas/rúbrica/prompts/participantes de ejemplo, CSV de carga masiva, y procedimiento **P-03** para reiniciar entre corridas. |
| 05 | [Plantillas Defecto y Bitácora](05_Plantillas_Defecto_y_Bitacora.md) | Reporte de defecto (severidad/prioridad), bitácora de ejecución, resumen de cierre. |
| 06 | [Criterios de Aceptación LLM](06_Criterios_Aceptacion_LLM.md) | Cómo juzgar salidas no deterministas; detección de fuga de rúbrica/PII; qué NO es defecto. |
| 07 | [Runbook Rollback / Contingencia](07_Runbook_Rollback_Contingencia.md) | Síntoma → qué flag/kill-switch apagar en producción, sin hotfix en caliente. |
| 08 | [Cómo probar cada iniciativa (lenguaje simple)](08_Como_Probar_Cada_Iniciativa_Lenguaje_Simple.md) | Guía **sin tecnicismos** para que cualquiera compruebe cada mejora: qué abrir, qué hacer y qué debería verse. |
| 09 | [Banco de variaciones P-27](09_Banco_Variaciones_P27_Intenciones_Control.md) | Frases reales de finalización/control para calibrar el clasificador sin depender de una sola redacción. |
| 10 | [Guía E2E ejecutable (agente o humano)](10_Guia_E2E_Ejecutable_Agente_o_Humano.md) | Recorridos **E1–E19** con precondición, pasos, evidencia y flags. Es el mapa de ejecución de punta a punta. |
| 11 | [Prompt para Codex — pruebas E2E](11_Prompt_Codex_Ejecutar_Pruebas_E2E.md) | Prompt listo para pegar cuando ejecuta **Codex**. |
| 12 | [Prompt para Claude Code — pruebas E2E](12_Prompt_ClaudeCode_Ejecutar_Pruebas_E2E.md) | Prompt listo para pegar cuando ejecuta **Claude Code**. |
| 13 | [Prompt para Claude Code — pruebas conversacionales](13_Prompt_ClaudeCode_Pruebas_Conversacionales.md) | Variante centrada en el flujo del coach por WhatsApp simulado. |
| 14 | [P-31 · Resumen de la consolidación](14_P31_Resumen_Consolidacion_Como_Probar.md) | Cómo comprobar que el participante ve su idea acumulada antes de terminarla. |
| 15 | [I-08 v2 · Carga masiva](15_I08v2_Carga_Masiva_Como_Probar.md) | Guía ejecutable de la carga por Excel/CSV: datos sucios, idempotencia, modos, **conflicto de titular y reasignación**, y el guardarraíl de un solo activo por teléfono. |
| 16 | [P-32 · Español/English y textos editables](16_P32_Multidioma_Catalogo_Textos_Como_Probar.md) | P-32 4/4 DONE local. Guía de regresión, E2E bilingüe, lote mixto, catálogo, rollback, D5 y UAT antes de una activación. |
| 17 | [Prompt · validación completa P-32](17_Prompt_Ejecutar_Validacion_Completa_P32.md) | Prompt listo para delegar D5, E2E bilingüe, lote mixto, catálogo, rollback, UAT y pendientes operativos de P-32. |
| — | [`datos/`](datos/README.md) | **Archivos de prueba listos para usar** (carga masiva). No hay que transcribirlos del documento. |

## Cómo empezar (tester)

1. Lee **00** (plan) y **06** (criterios cualitativos).
2. Carga los datos de **04** —los archivos de carga masiva ya están en **`datos/`**— y verifica
   ambiente con el **smoke** de **03 §1**.
3. Ejecuta **02** en orden: primero **CORE en SIM**, registrando en la bitácora de **05**. Reinicia con **P-03** (04 §6) entre corridas.
4. Confirma los casos `Ambiente: real` en WhatsApp real.
5. Llena el **checklist día-D** (03 §2) para el go/no-go. Ten a mano el **runbook 07**.

## Prioridad de un vistazo

- **CORE (bloquea go-live):** todo el flujo del coach (CNV), seguridad/privacidad (SEC), guardrails deterministas (GRD-01..04/06), robustez (ROB-01..07/09), auth (AUT), CRUD/envíos/carga/reinicio (ADM).
- **Extendido:** funciones bajo flag (FLG), filtros/límites secundarios, consultas.

## Riesgo #1

El **no determinismo del LLM** se gestiona en todo el paquete con **criterios cualitativos tolerantes**; lo determinista (cupos, dedupe, firma, umbral, estados) se valida al pie de la letra. Regla: *"el LLM propone, el sistema dispone"*.

## Nota de estado (2026-08-07)

**Actualización 2026-08-12:** P-32 está **4/4 DONE local**. La guía **16** y el prompt **17** cubren
la validación operativa pendiente. No debe reportarse como PASS de producción ni activarse fuera de
una ventana aislada y autorizada: faltan D5 real, UAT bilingüe, plantillas Meta inglesas aprobadas,
revisión de costo/latencia y acta de cambio.

**Corrección 2026-08-13:** se corrigieron localmente el rollback editorial y la protección de campañas
bilingües incompletas; se deben repetir las Pruebas 4 y 6 de la guía **16** antes de continuar con
D5/UAT. Validación local: 858 pruebas backend verdes.

**`I-08 v2` (carga masiva con la plantilla oficial de GHT) está completa** y es lo último que entró.
Cambia cosas que afectan a varias pruebas, así que conviene leerlo antes de ejecutar:

- El maestro de usuarios tiene campos nuevos y **`codigoUsuario`** (`U-000042`), que lo asigna el
  servidor y no cambia nunca. `area` y `empresa` **dejaron de ser obligatorios**.
- Un teléfono admite **un solo usuario activo**; los anteriores quedan inactivos conservando número e
  historial. La resolución por número **filtra por activo**, así que un número cuyo único registro
  está inactivo ya **no** resuelve participante.
- La base se **recreó** el 2026-08-07 con la unique key `/claveUnicidad`. Cualquier dato de prueba
  anterior a esa fecha ya no existe.
- Guía ejecutable: **15**; archivos de prueba: **`datos/`**.

**P-13** e **I-17** siguen completos localmente. El cierre anticipado sigue apagado hasta el acta:
el interruptor global es `Conversacion:CierreAnticipadoHabilitado=false`; la clasificación de madurez
usa umbral 0.6 aun con ese cierre apagado. Baseline **D5 real** pendiente → la calidad conversacional
se arbitra en UAT; la **seguridad** no espera al banco (filtro determinista). **P-31** está desplegado
con sus flags **apagados** (guía 14). **I-14 tags** está bloqueada hasta que GHT entregue el catálogo.
