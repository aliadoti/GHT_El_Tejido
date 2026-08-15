# Plan de implementación — DT-P32-02: semillas, JSON masivo y readiness

> **Spec rectora:** `../Iniciativas/DT-P32-02_Semillas_Edicion_Masiva_y_Readiness_Catalogo_Textos.md`
> **Estado:** implementación 3/3 y despliegue de `4d0f35c` completos; pendiente validación operativa.
> **Orden vigente:** ejecutar `QAS/22` con gate OFF, abrir la ventana ON según `QAS/18`, completar la
> corrida `QAS/17` y volver a OFF salvo acta formal; solo después retomar `DT-I20-02`.

## 1. Fronteras

- Domain conserva `CatalogoTextosConversacion`; no se crea otra entidad ni contenedor.
- Application separa semilla base, snapshot legacy, validación y readiness.
- Infrastructure conserva Cosmos `config`, ETag y lote transaccional.
- API agrega rutas aditivas y límites de importación.
- Angular amplía **Textos de conversación**; no crea otra pantalla.
- App Settings solo recibe límites operativos, nunca contenido editorial.
- No se cambia ningún recurso, secreto, flag o catálogo remoto durante implementación.

## 2. Corte 1/3 — base segura y prevalidación legacy

### Código previsto

- `CatalogosTextosSemilla`: separar `CrearBase(idioma)` de `CrearDesdeLegacy(idioma, opciones)`.
- `ValidadorCatalogoTextosConversacion`: recibir una política tipada de límites.
- `OpcionesCatalogoTextos`: agregar `MaxFrasesPorGrupo` y `MaxBytesImportacionJson`, con clamps.
- Crear resultado de prevalidación con conteos y errores tipificados, sin contenido.
- `EndpointsAdminCatalogosTextos`: rutas `/base`, `/legacy/preview`, `/legacy/exportar` y `/legacy`.
- Mantener `/semillas/{idioma}` sin cambio contractual.

### Pruebas primero

1. Semilla base `es/en` válida.
2. Legacy con 31 frases reproduce el defecto y la base sigue válida.
3. Preview legacy no persiste.
4. Export legacy conserva todas las entradas aun cuando el snapshot sea inválido.
5. Ninguna lista se trunca o mezcla con defaults.
6. Límite operativo acepta más de 30 y rechaza por encima del configurado/techo.

### Salida

Backend verde, sin cambio visible con gate OFF y sin frontend todavía.

## 3. Corte 2/3 — edición masiva, readiness y campaña

### Código previsto

- Contrato JSON aditivo `formato: catalogo-textos/v1`.
- Exportación UTF-8 indentada y nombre `*-editable.json`.
- `POST .../importar/prevalidar` usa exactamente el validador de la importación real.
- Importación válida crea nueva versión borrador; metadatos del archivo se ignoran.
- Readiness consulta versiones activas y reporta gate real/bloqueos sin textos.
- `ServicioGestionCampanias` recibe un puerto de disponibilidad de catálogo y bloquea activación
  bilingüe si falta un idioma activo válido.
- Auditoría nueva sin JSON ni frases.

### Pruebas primero

1. Export → editar → prevalidar → importar crea `v+1` borrador.
2. Error devuelve lista completa y cero escrituras.
3. Idioma incompatible se rechaza.
4. Readiness no confunde preview efectivo con gate ON.
5. Campaña bilingüe exige catálogos `es/en`; gate OFF legacy permanece igual.

### Salida

Contratos API/integración verdes y sin activación remota.

## 4. Corte 3/3 — portal y cierre documental

### UX

1. Botón **Crear semilla base**.
2. Acción separada **Revisar configuración anterior**.
3. Botón **Descargar JSON para edición masiva**.
4. Selector **Cargar JSON editado**.
5. Prevalidación visible: idioma, 29 mensajes, 16 grupos, total de frases y errores.
6. Confirmación **Importar como nuevo borrador**.
7. Selección automática del borrador creado y comparación con la activa.
8. Tarjeta readiness `es/en` y motivos de bloqueo.

### Pruebas frontend

- accesibilidad de botones/input/estado dinámico;
- archivo inválido, idioma distinto, cancelar y reintentar el mismo archivo;
- importación no activa;
- errores del backend se muestran por campo/grupo;
- flujo existente individual y rollback no regresan.

### Cierre

- actualizar `AVANCES.md`, `TODO.md`, P-32, contratos base, `QAS/16`, `QAS/17` y `QAS/22`;
- ejecutar secuencialmente:

```powershell
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build --filter "Category!=Calibracion"
dotnet format --verify-no-changes --no-restore
git diff --check
```

Desde `src/ElTejido.Web`, usar Node `22.22.3` para pruebas/build y ejecutar Prettier según los scripts
vigentes del proyecto.

## 5. Despliegue y corrida posterior

El despliegue de `4d0f35c` ya está confirmado. Esto no autoriza gate ON ni otros cambios remotos. La
corrida operativa, con autorización separada, sigue este orden:

1. confirmar ambiente/canal aislado o teléfonos de prueba autorizados;
2. con gate OFF, crear y revisar borradores base `es/en`;
3. probar descarga/prevalidación/importación JSON;
4. activar explícitamente los catálogos aprobados;
5. ejecutar `QAS/22` Pruebas 1 a 8 y la regresión legacy;
6. abrir la ventana gate ON según `QAS/18` y ejecutar `QAS/17` completo;
7. volver a gate OFF salvo acta formal, resolver cualquier FAIL y repetir hasta green;
8. registrar D5/UAT/Meta/costo/latencia y rollback;
9. cerrar DT-P32-02; y
10. cambiar el handoff a `DT-I20-02` corte 1/3.
