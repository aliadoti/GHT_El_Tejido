# Plan de implementación — DT-P32-05

## Corte único

1. Localizar la ruta `ActualizarLocalizacionesAsync` y expresar primero una prueba roja de campaña
   activa bilingüe completa que intenta guardar una localización incompleta.
2. Reutilizar la validación de localizaciones vigente sobre la copia candidata antes de `Guardar`.
3. Preservar el comportamiento de borrador y las validaciones ya existentes al activar o asociar.
4. Probar que el documento almacenado no cambia ante el rechazo y que una edición completa sí persiste.
5. Ejecutar las validaciones locales indicadas en la iniciativa y actualizar AVANCES/TODO/handoff.

## Fuera de alcance

No hay Azure, despliegue, App Settings, Meta, cambio de DTO/Cosmos, migración ni trabajo de
DT-RUB-01. La simulación de salida se trata después, en `DT-QA-03`.

