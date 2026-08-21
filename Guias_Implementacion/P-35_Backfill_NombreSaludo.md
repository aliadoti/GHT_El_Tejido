# P-35 — Backfill controlado de `nombreSaludo`

## Objetivo

Completar la propiedad `nombreSaludo` en documentos `Usuario` existentes sin modificar `nombre` ni
sobrescribir correcciones manuales. La operación es idempotente.

## Antes de ejecutar

1. Desplegar un artefacto que contenga P-35 y verificar `/health/ready`.
2. Identificar explícitamente el ambiente y confirmar que es el esperado.
3. Verificar que el respaldo continuo de Cosmos esté disponible.
4. Iniciar sesión en el portal con rol `admin`. No copiar tokens o cookies en el documento.
5. Consultar `GET /api/admin/usuarios/nombres-saludo/pendientes` y registrar solo el conteo.

No ejecutar el POST si el ambiente, el respaldo o el conteo no son los esperados.

## Ejecución

Con la sesión administrativa y el mecanismo CSRF normal del portal/API:

```http
POST /api/admin/usuarios/nombres-saludo/completar
```

Respuesta:

```json
{ "completados": 125 }
```

La implementación consulta únicamente documentos `type = "Usuario"`, selecciona aquellos donde
`nombreSaludo` está ausente o vacío y los vuelve a persistir con el cálculo P-35. Un valor existente
—incluida una corrección manual— no se toca.

## Verificación

1. Repetir `GET /api/admin/usuarios/nombres-saludo/pendientes`; debe devolver `0`.
2. Revisar una muestra desde el portal, incluyendo nombres de cuatro palabras y apellidos compuestos.
3. Corregir los casos ambiguos en **Usuarios → Editar → Nombre para saludo**.
4. En una campaña de prueba autorizada, verificar `Hola {{nombre}}` sin iniciar envíos reales fuera
   del alcance aprobado.
5. Repetir el POST debe devolver `{ "completados": 0 }`.

## Detención y recuperación

- Si el conteo crece, el endpoint falla o aparecen cambios en `nombre`, detener la operación y no
  repetirla hasta revisar el artefacto y el ambiente.
- No borrar documentos ni restaurar toda la cuenta como primera acción. `nombreSaludo` es aditivo y
  el runtime mantiene compatibilidad con documentos donde el campo no existe.
- Una corrección individual se hace desde el portal; no editar masivamente `nombre`.

## Deuda deliberada

La plantilla CSV/XLSX de I-08 todavía no contiene `Nombre para saludo`. Las altas masivas lo calculan
y las actualizaciones conservan el valor persistido. Agregar esa columna requiere una ampliación
posterior y compatible de la plantilla y su lector.
