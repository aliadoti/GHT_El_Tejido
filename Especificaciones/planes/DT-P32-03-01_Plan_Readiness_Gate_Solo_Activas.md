# Plan de implementación — DT-P32-03-01

> **Estado 2026-08-15:** pasos 1 a 6 ejecutados (código, portal y pruebas). Falta solo el paso 7:
> desplegar con autorización y repetir `QAS/23` pruebas 4–6.
>
> Frontera real encontrada: el agregado vive en `MapeoPlantillaMetaEvaluado` /
> `ReadinessCatalogosTextos` y la guarda en `ServicioGestionCampanias.CambiarEstadoCampaniaAsync`, que
> recibe `OpcionesPlantillaEnvioInicial` por inyección opcional y reutiliza
> `ValidadorMapeosPlantillaMeta` —no hay una segunda implementación de `TryResolver`—. El validador de
> localizaciones ya exigía `plantillaRef` en campañas bilingües, así que la guarda agrega cobertura
> real sobre el mapeo de App Settings y sobre la campaña monolingüe española con el gate ON.

## Resultado buscado

Evitar que borradores normales de trabajo bloqueen el gate global, sin perder su diagnóstico y sin
permitir activar una campaña incompleta mientras el gate está ON.

## Corte único 1/1

1. Escribir regresiones rojas para: activa faltante, borrador faltante, par compartido y agregado sin
   campañas activas.
2. Agregar `bloqueaGateOn` al DTO y calcularlo por presencia de una campaña activa; filtrar por ese
   campo únicamente al calcular `listoParaGateOn`.
3. Reutilizar el validador estructural existente en la transición `borrador → activa` cuando el gate
   esté ON, limitado a la campaña objetivo.
4. Mantener intacta la transición con gate OFF y las validaciones actuales de catálogos/localización.
5. Actualizar Preparación para diferenciar bloqueos actuales y pendientes de borrador.
6. Añadir pruebas unitarias, de integración de readiness/activación y del portal.
7. Desplegar solo con autorización; repetir QAS/23 pruebas 4–6 y registrar el resultado.

## Archivos probables

- `src/ElTejido.Application/Configuracion/ServicioReadinessCatalogosTextos.cs`
- `src/ElTejido.Application/WhatsApp/ValidadorMapeosPlantillaMeta.cs`
- `src/ElTejido.Application/Configuracion/ServicioGestionCampanias.cs`
- `src/ElTejido.Api/Admin/EndpointsAdminCatalogosTextos.cs`
- `src/ElTejido.Web/src/app/features/catalogos-textos/catalogos-textos.page.{ts,spec.ts}`
- `tests/ElTejido.UnitTests/WhatsApp/ValidadorMapeosPlantillaMetaTests.cs`
- `tests/ElTejido.UnitTests/Configuracion/ServicioGestionCampaniasLocalizacionesTests.cs`
- `tests/ElTejido.IntegrationTests/AdminCatalogosTextosIntegrationTests.cs`

Los nombres son orientación; se debe localizar la frontera real antes de editar y evitar una segunda
implementación de `TryResolver`.

## Verificación mínima

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

## Handoff obligatorio

- No modificar Azure, Meta ni datos remotos durante la implementación.
- No declarar P-32 green con pruebas locales solamente.
- Después del despliegue autorizado, repetir QAS/23 4–6; aceptar la comprobación manual del nombre,
  código y orden de componentes Meta como evidencia externa.
- Si el retest es green, actualizar AVANCES/TODO y comenzar DT-I20-02 corte 1/3.

## Rollback

Revertir el corte y mantener `Conversacion__CatalogoTextosHabilitado=false`. No borrar campañas,
catálogos ni mapeos.
