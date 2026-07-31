# Backlog de Requerimientos — GHT (modelo T&M)

Esta carpeta lleva el seguimiento de **nuevos requerimientos, cambios y ajustes** que surgen después de la propuesta comercial. Está pensada para conseguir **aprobación del cliente**, mantener **control** y soportar el **cobro por Time & Materials (T&M)**.

## Contenido

- **`Seguimiento_Requerimientos.xlsx`** — hoja maestra de control, con cuatro pestañas:
  - **Alcance inicial** — los 11 requerimientos del MVP base (Cerrados y facturados).
  - **Nuevas iniciativas** — el backlog T&M con las iniciativas post-MVP (IDs nativos I-xx / P-xx / D5).
  - **Resumen** — conteo, horas y valor por estado de las nuevas iniciativas.
  - **Listas** — valores de los desplegables.
- **`Requerimientos/`** — un documento por requerimiento, organizado en:
  - **`Alcance inicial/`** — los `REQ-00X` del MVP base ya entregado.
  - **`Nuevas iniciativas/`** — un documento por iniciativa (`<ID>_nombre.md`) que resume la spec de `Especificaciones/Iniciativas/`.
  - **`REQ-000_PLANTILLA.md`** — plantilla ejecutiva para nuevos requerimientos.
- **`Aprobaciones/`** — evidencia de aprobación del cliente (correos, actas, PDFs firmados). El nombre del archivo debería incluir el ID del requerimiento.

## Flujo de estados (7)

```
Propuesto → Estimado → Aprobado → En curso → Entregado → Facturado → Cerrado
```

| Estado | Significado | Quién lo mueve |
|---|---|---|
| **Propuesto** | El requerimiento entra al backlog. | Aliado / Cliente |
| **Estimado** | Tiene documento, horas y valor estimado. | Aliado |
| **Aprobado** | El cliente autoriza ejecutar (evidencia en `Aprobaciones/`). | Cliente |
| **En curso** | En desarrollo; se registran horas reales. | Aliado |
| **Entregado** | Cumple criterios de aceptación. | Aliado / Cliente |
| **Facturado** | Incluido en una factura. | Aliado |
| **Cerrado** | Facturado y sin pendientes. | Aliado |

## Cómo agregar un requerimiento

1. Copia `Requerimientos/REQ-000_PLANTILLA.md` como `REQ-00X_nombre-corto.md` y complétalo.
2. Agrega una fila en `Seguimiento_Requerimientos.xlsx` con el **mismo ID**.
3. Cuando el cliente apruebe, guarda la evidencia en `Aprobaciones/` y pon el estado en **Aprobado**.
4. Registra horas reales durante la ejecución; el Excel calcula el valor a facturar.

> Regla de oro T&M: **nada se ejecuta sin estado "Aprobado" y evidencia**. Así el cobro siempre está respaldado.
