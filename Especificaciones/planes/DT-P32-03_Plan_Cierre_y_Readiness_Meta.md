# Plan de implementación — DT-P32-03

## Resultado buscado

Cerrar el único defecto bilingüe bloqueante y hacer visible, antes de encender el gate, si las
plantillas Meta necesarias están configuradas localmente.

## Corte 1/2 — cierre localizado único

1. Crear una matriz de todas las rutas que terminan, avanzan o cierran una conversación.
2. Añadir primero regresiones que reproduzcan el cierre español en un hilo `en`.
3. Introducir el resolutor único en Application, con política OFF/ON y fallo sin cruce de idioma.
4. Migrar todas las composiciones de cierre; resolver antes de mutar el estado.
5. Eliminar lecturas directas restantes en `OrquestadorConversacion`.
6. Verificar unitarias focalizadas, build y suite backend.

## Corte 2/2 — readiness Meta y portal

1. Actualizar primero el contrato aditivo de `GET /catalogos-textos/readiness`.
2. Extraer/reutilizar una validación estructural única del mapeo que también use `TryResolver`.
3. Enumerar pares requeridos desde campañas activa/borrador y mensajes iniciales activos.
4. Agregar `listoParaGateOn` y `mapeosMeta`, sin cambiar `idiomas[].listo`.
5. Mostrar en Preparación catálogos y plantillas como comprobaciones separadas.
6. Añadir unitarias, integración API y pruebas del portal.
7. Actualizar QAS/16–18 y ejecutar QAS/23.

## Gate de calidad

Ejecutar secuencialmente:

```text
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build --filter "Category!=Calibracion"
dotnet format --verify-no-changes --no-restore
cd src/ElTejido.Web
npx --yes -p node@22.22.3 -c "node node_modules/@angular/cli/bin/ng.js test --watch=false"
npx --yes -p node@22.22.3 -c "node node_modules/@angular/cli/bin/ng.js build"
npx prettier . --check
git diff --check
```

## Rollback

- Código: revertir el cambio; el gate permanece OFF hasta recuperar green.
- Operación: volver `Conversacion__CatalogoTextosHabilitado=false` y reiniciar.
- No borrar catálogos ni mapeos: son configuración recuperable y auditable.
