# DT-P32-05 — Guarda de edición de campaña activa

> **Estado:** DEUDA ACEPTADA POST-CONVENCIÓN — no bloquea el congelamiento condicionado.
> **Origen:** defecto P1 `DEF-P32-04-01`, registrado en
> `EF-P32-04-01_Campania_Activa_Localizacion_Incompleta.md.md` tras la corrida P32-20260816-1955.
> **Alcance:** corregir exclusivamente la persistencia inválida de localizaciones de una campaña activa.

> **Mitigación Convención 2026:** una sola campaña se completa en borrador antes de activarla y queda
> prohibido editarla después de activar o realizar el primer envío. Esta excepción solo cubre el
> ambiente exclusivo de la convención; antes de habilitar edición continua se debe implementar esta
> iniciativa.

## Problema confirmado

Una campaña bilingüe activa puede editarse y quedar con un campo obligatorio vacío. El readiness lo
detecta después de guardar, pero la campaña ya quedó en un estado que no debió persistirse. Runtime no
mezcla idiomas, aunque una conversación puede cerrar con una respuesta neutra por falta de contenido.

## Decisión

Antes de guardar una actualización de localizaciones de una campaña **activa**, validar el estado
efectivo completo de sus idiomas requeridos. Si falta contenido obligatorio, devolver el error de
validación existente con los campos concretos y conservar sin cambios el documento almacenado.

Para borradores se conserva la edición parcial. Al activar una campaña se mantiene la validación ya
existente. No se crean campos, migraciones ni representaciones nuevas: DTO y Cosmos siguen usando
strings y el formato actual de `localizaciones`.

## Criterios de aceptación

1. Editar una localización completa de campaña activa continúa funcionando.
2. Vaciar nombre, mensaje inicial, cierre o una pregunta obligatoria de una localización activa es
   rechazado antes de persistir; la lectura posterior conserva el valor previo.
3. El mensaje de error identifica idioma y campo faltante sin exponer contenido del participante.
4. Un borrador puede seguir incompleto hasta su activación.
5. Las rutas existentes de activación y asociación de participantes conservan sus validaciones.
6. No se modifica Azure, App Settings, Meta, DTO/Cosmos, frontend ni DT-RUB-01.

## Verificación

- pruebas unitarias del validador y del servicio de gestión: rechazo, no escritura y edición válida;
- prueba de integración del endpoint si el contrato actual ya está cubierto;
- `dotnet build -c Release -warnaserror`, suite no-Calibración, formato y `git diff --check`;
- no repetir QAS P-32 completa: al terminar, solo la regresión de este defecto y los casos que toque
  la ruta de edición activa.
