# DT-P32-03-01 — Readiness del gate limitado a campañas activas

> **Estado:** IMPLEMENTADA local (1/1) el 2026-08-15 por Claude Opus 5; sin push, despliegue ni
> cambio remoto. Backend **863 unitarias + 109 de integración**, portal **62**; build Release
> `-warnaserror`, `dotnet format`, `ng test`, `ng build`, Prettier y `git diff --check` verdes.
> Pendiente: despliegue autorizado y repetición de `QAS/23` pruebas 4–6.
> **Origen:** `QAS/resultados/Resultados_P32_Smoke_DT-P32-03_2026-08-15.md`, prueba 5.
> **Prioridad:** microajuste bloqueante para cerrar P-32 y retomar DT-I20-02.

## 1. Problema

`GET /api/admin/catalogos-textos/readiness` muestra correctamente los mapeos Meta requeridos por
campañas activas y borrador, pero hoy todos participan en `listoParaGateOn`. Un borrador bilingüe a
medio construir es un estado normal de trabajo y puede no tener todavía localizaciones o mapeos. Como
`borrador` no tiene transición a `archivada`, ese dato de preparación puede mantener la señal global en
`false` indefinidamente aunque todo lo que está activo esté listo.

El smoke también comprobó que readiness informa presencia estructural (`nombreConfigurado`,
`idiomaMetaConfigurado`, `componentes`) y deliberadamente no certifica el nombre aprobado ni consulta
Graph API. Esa limitación es correcta y no requiere ampliar el endpoint.

## 2. Decisión

Separar dos conceptos:

- **visibilidad de preparación:** campañas `activa|borrador` continúan enumeradas para que el
  administrador conozca qué debe completar antes de activar cada borrador;
- **bloqueo del uso actual:** solo los pares requeridos por al menos una campaña `activa` participan
  en `listoParaGateOn`.

Un mapeo compartido por campañas activas y borrador bloquea porque existe al menos una consumidora
activa. Un mapeo requerido exclusivamente por borradores se muestra como pendiente, pero no impide
encender el gate para las campañas ya activas.

Para impedir que el gate quede ON y luego se active una campaña incompleta, la transición
`borrador → activa` debe validar **los mapeos de esa campaña** cuando
`Conversacion:CatalogoTextosHabilitado=true`. No debe consultar el readiness global ni permitir que
otra campaña, activa o borrador, bloquee la transición.

## 3. Contrato aditivo

Cada elemento de `mapeosMeta[]` agrega:

```json
{
  "bloqueaGateOn": false
}
```

`bloqueaGateOn=true` si alguna campaña de `campanias[]` está `activa`. El cálculo agregado queda:

```text
listoParaGateOn = catalogosRequeridosListos
                  && todos(mapeosMeta donde bloqueaGateOn, configurado)
```

Los problemas estructurales se conservan también para pares no bloqueantes. El portal **Preparación**
debe distinguir “bloquea el gate” de “pendiente antes de activar este borrador”; no debe ocultar ni
presentar como listo un borrador incompleto.

No se exponen `Nombre` ni `Idioma` físicos. La coincidencia exacta con la plantilla aprobada, el orden
de variables y su aprobación siguen requiriendo evidencia del operador o el envío controlado de
QAS/23 prueba 7.

## 4. Guarda de activación

Al solicitar `PATCH /api/admin/campanias/{id}/estado` hacia `activa`:

1. conservar todas las validaciones actuales de campaña y catálogo;
2. si el gate está OFF, conservar exactamente el comportamiento vigente;
3. si el gate está ON, enumerar solo los mensajes iniciales activos y los idiomas habilitados de la
   campaña objetivo;
4. validar cada `plantillaRef + idioma` con la misma política estructural que usa `ServicioEnvios` y
   readiness;
5. ante alias ausente, `Nombre`/`Idioma` faltante o componente vacío/duplicado, responder
   `400 VALIDATION_ERROR`, incluir un detalle estable bajo `mapeosMeta.{mensajeInicialId}.{idioma}` y no
   cambiar el estado.

La validación es local y determinista: no consulta Graph API, no hace un envío y no verifica la
aprobación de Meta.

## 5. Alcance de código

- DTO y agregado de readiness.
- Servicio/validador compartido de mapeos Meta, sin duplicar reglas.
- Guarda en la transición de campaña.
- Panel Preparación y sus tipos/pruebas.
- Pruebas unitarias, integración API y actualización QAS.

## 6. Fuera de alcance

- Cambiar App Settings, gate o simulación en Azure.
- Exponer los nombres físicos de plantillas por API.
- Consultar o modificar plantillas en Meta.
- Añadir transición `borrador → archivada`.
- Corregir datos con mojibake o recrear fixtures/campañas de prueba.
- DT-P32-04 y DT-I20-02.

## 7. Criterios de aceptación

1. Un mapeo faltante requerido por una campaña activa deja `bloqueaGateOn=true` y
   `listoParaGateOn=false`.
2. Un mapeo faltante requerido solo por borradores se muestra con problemas,
   `bloqueaGateOn=false`, y no cambia a `false` una señal que por lo demás está lista.
3. Si un mismo par sirve a una campaña activa y otra borrador, bloquea una sola vez y conserva ambas
   referencias.
4. Con catálogos válidos y todos los pares de campañas activas configurados,
   `listoParaGateOn=true` aunque existan borradores incompletos.
5. Con gate ON no puede activarse una campaña cuyo mapeo propio esté incompleto; responde 400 y el
   estado permanece en borrador.
6. Con gate ON una campaña con mapeos propios completos puede activarse aunque exista otro borrador
   incompleto.
7. Con gate OFF se conserva la conducta previa de activación.
8. El portal distingue bloqueos actuales de pendientes de preparación.
9. No se registran secretos, teléfonos ni contenido de participantes.

## 7.1 Qué se entregó (2026-08-15)

- `MapeoPlantillaMetaEvaluado.BloqueaGateOn` (propiedad derivada: alguna campaña requirente `activa`)
  y `ReadinessCatalogosTextos.ListoParaGateOn` filtrando por ese campo. `idiomas[].listo` y la
  enumeración de pares no cambian: un borrador incompleto conserva sus problemas visibles.
- `GET /catalogos-textos/readiness` expone `mapeosMeta[].bloqueaGateOn`; el resto del cuerpo es igual.
- `ServicioGestionCampanias.CambiarEstadoCampaniaAsync` valida, **solo con el gate ON** y **solo sobre
  la campaña objetivo**, los pares que exigen sus mensajes iniciales activos, reutilizando
  `ValidadorMapeosPlantillaMeta` (que a su vez delega en `OpcionesPlantillaEnvioInicial.TryResolver`).
  El fallo es `400 VALIDATION_ERROR` con un detalle por problema bajo
  `mapeosMeta.{mensajeInicialId}.{idioma}` y el estado permanece en `borrador`. Con el gate OFF la
  transición conserva exactamente la conducta previa. La guarda alcanza también a una campaña
  monolingüe española: con el gate ON su envío inicial también resuelve por alias.
- Portal **Preparación**: cada plantilla pendiente dice si «bloquea el uso de estos textos» (campañas
  activas) o si «hay que configurarla antes de activar la campaña en borrador que la pide», y el
  resumen avisa cuántas quedan pendientes de borrador aunque ya se pueda empezar.
- Pruebas nuevas: 9 unitarias (validador, agregado de readiness y guarda de activación) y 4 de
  integración (readiness con borrador incompleto y las tres rutas de activación), más 2 del portal.

## 8. Cierre

Es un único corte. Tras desplegarlo, repetir únicamente QAS/23 pruebas 4–6. Si pasan y se acepta la
evidencia humana de Meta, P-32 smoke queda green y el siguiente cambio de código es DT-I20-02 corte
1/3. DT-P32-04 permanece como refactor posterior, no bloqueante.
