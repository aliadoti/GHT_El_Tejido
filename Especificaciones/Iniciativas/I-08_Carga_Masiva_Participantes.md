# I-08 — Carga masiva de participantes vía Excel

> **Origen:** hoja `Iniciativas` (Action Item de la reunión 9-jul).
> **Tipo:** Desarrollo · **Prioridad:** Alta · **Ventana:** Sprint 1a backend / Sprint 1b UI ·
> **Dependencia:** lista final de GHT (insumo #5, límite 1-ago) · **Riesgo:** datos sucios.
> Cubre REQ §12/§26.3, ARQ §8; specs base `04 §5.1/§5.3`, `07 §1`.

## 1. Qué pide GHT / por qué
Subir la lista de participantes **en lote (Excel)** desde el portal en vez de crearlos uno a uno.
Necesario para cargar la lista real de la convención (freeze 8-ago).

## 2. Estado actual del build
Nuevo. Hoy el alta es individual (`POST /api/admin/usuarios` + asociación por campaña).

## 3. Diseño técnico
1. **Endpoint** `POST /api/admin/usuarios/carga-masiva` (multipart/form-data, rol admin + CSRF,
   límite de tamaño p. ej. 2 MB). Parámetro opcional `campaniaId` para asociar de una vez.
2. **Parser server-side** (`ServicioCargaMasiva`, Application): plantilla fija de columnas
   `Nombre | WhatsApp | Area | Empresa | Tags` (tags separadas por `;`). Paquete de lectura xlsx:
   **ClosedXML** (nuevo paquete Infrastructure; alternativa: aceptar también CSV con parser propio,
   sin dependencia). La plantilla Excel se entrega a GHT en Sprint 1.
3. **Por fila:** normaliza E.164 (reutiliza `NormalizadorNumero`), valida unicidad por
   `whatsappNormalizado` (reutiliza la lógica de `ServicioGestionUsuarios`), aplica tags
   (creándolas si no existen), asocia a la campaña si se pidió.
4. **Reporte por fila** (respuesta JSON): `creado | actualizado | rechazado` + motivo
   (`numero_invalido`, `duplicado_en_archivo`, `fila_incompleta`...). Una fila mala **no aborta** el lote.
5. **Idempotencia:** upsert por número normalizado; re-ejecutar la carga no duplica.
6. **Portal:** pantalla en Usuarios (o Campañas) con upload + preview del reporte + confirmación
   (toasts existentes). Sin PII en logs (solo conteos y motivos).

## 4. Contratos y configuración
- `04 §5.1`: nuevo endpoint y contrato del reporte — **actualizar la spec en commit aparte**.
- `03` no cambia (usa entidades existentes). Paquete nuevo: ClosedXML (registrar en AVANCES).

## 5. Riesgos y mitigación
- *Datos sucios (números mal formados, duplicados)* → validación por fila con reporte; el lote no falla completo.
- *Lista tardía de GHT* → la plantilla se entrega en Sprint 1; la carga real es un paso del freeze (T-49).
- *Archivo malicioso/enorme* → límite de tamaño, solo `.xlsx`/`.csv`, parseo en streaming, rate limit `publico`.

## 6. Criterios de aceptación / pruebas
- Unit: archivo de N filas válidas → N `creado`; recarga → N `actualizado` (sin duplicar).
- Unit: fila con número inválido → `rechazado` con motivo, el resto del lote se procesa.
- Unit: números duplicados dentro del archivo → primero gana, resto `rechazado(duplicado_en_archivo)`.
- Integration: endpoint exige admin + CSRF; reporte completo sin fuga de PII en logs.
- Frontend lint/test/build verdes.

## 7. Degradación
No aplica flag: el alta individual sigue disponible. Si ClosedXML se complica, primera entrega
solo CSV (la plantilla se exporta desde Excel como CSV).
