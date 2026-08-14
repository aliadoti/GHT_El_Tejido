# P-32 — Inventario y migración de textos conversacionales

**Estado:** P-32 4/4 DONE local. `DT-P32-02` está especificada 0/3 desde 2026-08-14 para separar
semilla base de fotografía legacy, formalizar edición masiva JSON y readiness antes de repetir la
validación operativa. El corte 2a fija `idioma` en el
hilo/ciclo; el 2b1 conecta al adaptador los mensajes globales registrados y las variantes que emite
`OrquestadorConversacion`, sin activar el catálogo. El 2b2 completa menús/frases de enrutamiento, detectores
del orquestador y aclaraciones P-27 por snapshot de idioma. El corte 3 agrega localizaciones embebidas,
edición administrativa, validación y envío mixto por participante. Sin configuración remota, despliegue ni activación.
**Spec rectora:** `../Iniciativas/P-32_Conversacion_Multidioma_y_Catalogo_Textos.md`.

## 1. Objetivo

Evitar una migración incompleta. Este documento clasifica los textos actuales por destino y fija el
orden para que ningún mensaje visible permanezca accidentalmente en español durante un hilo inglés.

## 2. Clasificación de configuración

| Tipo | Ejemplos actuales | Destino P-32 |
|---|---|---|
| Contenido editorial global | `Conversacion:Mensajes:*`, variantes de acuses/invitaciones, ayudas de menús. | `CatalogoTextosConversacion.mensajes`, por idioma. |
| Vocabulario determinista | `Conversacion:FrasesContinuar`, `FrasesFinalizarIdea`, `FrasesFinalizarParticipacion`, `FrasesSolicitarMejora`, `FrasesRevisitar*`, `FrasesCambiarCampania`, frases proactivas. | `CatalogoTextosConversacion.frases`, por idioma. |
| Contenido de campaña | `MensajeInicial.Texto`, `Pregunta.Texto`, `Pregunta.Instruccion`, `ConfigConversacional.MensajeCierre`, nombre/objetivo visible. | `localizaciones[idioma]` dentro de la misma campaña/mensaje/pregunta. |
| Identificador operativo | Nombre/código de plantilla Meta, alias de número, endpoint, nombres de secretos. | App Settings por ambiente; la campaña usa `plantillaRef` lógica. |
| Comportamiento operativo | Flags, kill-switches, límites, timeouts, cuotas, intervalos y cache TTL. | App Settings/env; **no** se mueve al catálogo. |
| Prompts/rúbricas | Prompts y rúbricas versionados existentes. | Permanecen en `config`; reciben una instrucción dura de idioma, no se mezclan con el catálogo. |

## 3. Inventario global inicial

P-32 originalmente hizo que la semilla `es` tomara los valores efectivos del ambiente objetivo. La
corrida del 2026-08-13 demostró que una lista legacy inválida puede bloquear todo el borrador.
`DT-P32-02` separa dos rutas: **semilla base** curada `es/en`, siempre válida e independiente del
ambiente, y **fotografía legacy** prevalidada. Ambas crean borrador y nunca activan contenido. La ruta
P-32 original se conserva por compatibilidad, pero el portal usa las rutas explícitas.

### 3.1 Mensajes de `OpcionesMensajesConversacion`

Claves mínimas conocidas:

- `encabezadoResumenAvance`
- `preguntaContinuarMadurando`
- `saludoPrimerContacto`
- `saludoSiguientePregunta`
- `saludoReactivacion`
- `pausaPorInactividad`
- `invitacionMejora` e `invitacionMejoraVariantes`
- `invitacionContinuarVariantes`
- `mensajeConfiguracionNoDisponible`
- `mensajeCalificacionAlta`
- `acuseContinuar` y `acuseContinuarVariantes`
- `acuseRechazoGuardado`
- `acuseReaperturaIdea`
- `invitacionReaperturaIdea`
- `preguntaSeleccionIdea`
- `instruccionSeleccionIdea`
- `sinIdeasHistoricas`
- `encabezadoSeleccionCampania`
- `instruccionSeleccionCampania`
- `ayudaSeleccionCampaniaInvalida`
- `encabezadoSeleccionPregunta`
- `instruccionSeleccionPregunta`

Antes de cerrar el corte 2, una prueba de arquitectura debe enumerar las propiedades visibles de
`OpcionesMensajesConversacion` y compararlas con el registro de claves del catálogo. Una propiedad
nueva sin clave hace fallar CI.

### 3.2 Frases de detección

- continuar/siguiente pregunta;
- solicitar mejora;
- finalizar idea;
- finalizar participación;
- revisitar la idea anterior;
- seleccionar otra idea histórica;
- cambiar de campaña;
- saludo/inicio proactivo; y
- equivalentes deterministas del menú de aclaración P-27.

La normalización y los límites actuales se conservan. Duplicados después de quitar tildes, signos,
espacios y diferencias de mayúsculas invalidan la lista completa; el catálogo activo anterior sigue
vigente.

### 3.3 Extensión compatible P-33 (2026-08-13)

P-33 agrega cinco mensajes (`encabezadoConsultaIdea`, `invitacionConsultaIdea`,
`encabezadoCierreIdea`, `otrasIdeasGuardadas`, `sinIdeaDisponible`) y tres listas de frases
(`consultarIdea`, `acuseConsultaIdea`, `nuevaIdea`). El registro cerrado pasa de **24 a 29 mensajes**
y de **13 a 16 listas**.

Las versiones históricas activas no se mutan: durante el corte 1 de P-33 resuelven estas claves desde
el respaldo compilado del mismo idioma. Toda versión nueva creada después de ampliar el registro debe
traer las 29/16 claves y queda sujeta a la validación atómica normal de P-32.

## 4. Hardcodes a revisar en código

| Componente | Riesgo | Acción del corte 2/4 |
|---|---|---|
| `Conversacion/OpcionesConversacion.cs` | Defaults y variantes españolas. | Conservar solo respaldo mínimo `es/en`; lecturas normales por clave. |
| `Conversacion/OrquestadorConversacion.cs` | Acuses, aclaraciones y fallbacks incrustados. | **2b DONE:** mensajes globales, variantes, detectores y aclaraciones P-27 usan el adaptador. Siguen las salidas que dependen de localización de campaña (corte 3). |
| `Conversacion/ServicioEnrutamientoParticipacion.cs` | Menús de campaña/pregunta y errores de selección. | **2b DONE:** resuelve catálogo y frases con `EnrutamientoAporte.Idioma`, persistido en Cosmos; los nombres de campaña/pregunta quedan para localizaciones del corte 3. |
| `Conversacion/DetectorEntradaProactiva.cs` | Vocabulario español. | Catálogo de frases por idioma con guardas equivalentes. |
| `Conversacion/DetectorIntencionContinuar.cs` | Listas compiladas y cobertura inglesa parcial. | Resolver listas del idioma; comandos críticos bilingües de respaldo. |
| `Conversacion/RedactorTurnoConversacional.cs` | Instrucción explícita “en español”. | Recibir `idioma` y producir en el idioma efectivo. |
| `Conversacion/ClasificadorIntencionControl.cs` | El contexto admite idioma, pero no todos los llamadores lo envían. | Propagación obligatoria y pruebas de ambos idiomas. |
| `WhatsApp/ServicioEnvios.cs` | Plantilla/idioma se resuelve una vez antes de recorrer usuarios. | Resolver localización y plantilla dentro del `foreach`. |

La búsqueda de cierre debe cubrir toda cadena visible, no solo estos archivos:

```powershell
rg -n 'EnviarTextoAsync|new TrabajoEnvio|MensajeCierre|TextoConfirmacion|"[^"\r\n]{12,}"' src/ElTejido.Application src/ElTejido.Api
```

Cada hallazgo se clasifica como texto visible, contrato interno, log técnico o mensaje de error
administrativo. P-32 solo exige extraer lo visible al participante.

## 5. Contenido de campaña a migrar

Para cada campaña que vaya a habilitar inglés:

1. traducir nombre visible, descripción y objetivo;
2. traducir todos los mensajes iniciales activos;
3. traducir texto e instrucción de cada pregunta activa;
4. traducir el mensaje de cierre;
5. revisar placeholders y longitud en ambos idiomas;
6. asignar una `plantillaRef` por idioma y comprobar su mapeo Meta; y
7. activar `en` solo cuando la validación de completitud sea verde.

Las campañas españolas históricas no se migran obligatoriamente: se interpretan como `es` y siguen
funcionando. No se crean copias `_en` porque dividirían participantes, resultados y trazabilidad.

## 6. Secuencia segura de migración

1. **Crear base:** generar borradores base `es/en`, completos y válidos, sin depender de App Settings.
2. **Prevalidar legacy:** revisar los valores efectivos sin escribir. Corregir excesos/duplicados en
   un JSON de trabajo o conservar la base; nunca truncar ni mezclar silenciosamente.
3. **Edición masiva:** descargar JSON editable, ajustar `mensajes`/`frases`, prevalidar y confirmar
   una nueva versión borrador.
4. **Traducir y aprobar:** producir la versión inglesa con revisión humana; no usar traducción
   automática como contenido final.
5. **Guardar borradores:** importar `es` y `en` en Cosmos; validar claves, frases y placeholders.
6. **Revisar readiness:** exigir versión activa por idioma y campañas/localizaciones completas.
7. **Probar con gate OFF:** verificar que la presencia del catálogo no cambia el flujo legacy.
8. **Activar catálogo en prueba:** gate ON solo en ambiente de prueba, con caché corta y dos usuarios.
9. **Migrar campañas:** agregar localizaciones completas y plantillas Meta; ejecutar lote mixto.
10. **UAT y rollback:** editar un texto, activar versión, comprobar propagación y reactivar la previa.
11. **Deprecar:** cuando la regresión sea verde, dejar de editar `Conversacion:Mensajes:*` y
   `Conversacion:Frases*`; documentar fecha de retiro antes de eliminarlas en una iniciativa posterior.

### 6.1 Mapeo operativo de plantillas (corte 3)

`plantillaRef` es editorial y se guarda con la localización del mensaje. El nombre físico aprobado por
Meta queda por ambiente bajo `WhatsApp:PlantillaEnvioInicial:Mapeos:{plantillaRef}:{idioma}` con
`Nombre`, `Idioma` y `Componentes`. Es configuración operativa no secreta: se puede administrar en
App Settings sin recompilar, pero nunca en Cosmos ni en los logs. El bloque legado `Nombre/Idioma/
Componentes` sigue siendo el respaldo exacto mientras el gate está apagado.

## 7. Checklist de cierre del inventario

- [ ] Cero texto visible al participante fuera de catálogo/localización, salvo el respaldo mínimo.
- [ ] Cada clave obligatoria existe en `es` y `en`.
- [ ] Cada frase respeta normalización, unicidad y límites.
- [ ] Cada placeholder existe y se resuelve en ambos idiomas.
- [ ] Cada campaña bilingüe tiene localizaciones completas.
- [ ] Cada plantilla lógica tiene mapeo Meta por ambiente e idioma.
- [ ] Los prompts reciben idioma y los guardrails reconocen fugas en `es/en`.
- [ ] Logs y auditoría no contienen contenido ni aportes.
- [ ] Edición, activación, caché y rollback funcionan sin build/deploy.
- [ ] Semillas base `es/en` se crean aunque la fotografía legacy sea inválida.
- [ ] Descargar/prevalidar/importar JSON completo crea un borrador nuevo y nunca activa.
- [ ] Readiness exige catálogo activo por idioma antes de activar campaña bilingüe.
- [ ] Variables editoriales legacy quedan marcadas como deprecadas y sin doble fuente silenciosa.
