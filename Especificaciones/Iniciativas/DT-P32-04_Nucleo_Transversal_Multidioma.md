# DT-P32-04 — Núcleo transversal multidioma y resolutores especializados

> **Estado vigente:** IMPLEMENTACIÓN 3/3 DONE; **freeze de Convención 2026 aprobado con condiciones**
> para `28c3cb1`, una sola campaña inmutable, ambiente limpio y WhatsApp real. `DT-P32-05` y
> `DT-QA-03` quedan post-convención. Parametrización, D5, smoke/UAT real y acta de flags son gates
> operativos. Ver `../Decision_Congelamiento_Codigo_Convencion_2026.md`.
>
> **Evidencia histórica:** la QA controlada del 2026-08-16 quedó **BLOCKED — NO ACTIVAR** bajo su
> alcance de simulación. El reporte confirmó el artefacto y registró `DEF-P32-04-01`.
> **Naturaleza:** refactor incremental sin cambio funcional ni migración de datos.
> **Precondiciones cumplidas:** DT-P32-03-01 tiene smoke green; DT-I20-02 está desplegada con QAS/21
> pruebas 1–8 PASS; DT-RUB-01 fue aceptada condicionalmente para una inicialización limpia. Las deudas
> de rúbricas no amplían ni bloquean este alcance.

## 1. Decisión arquitectónica

Sí se centraliza la **política** multidioma, pero no todo el contenido en una sola clase, tabla o
archivo JSON. El sistema conserva tres fuentes con ciclos de vida distintos:

1. catálogo global versionado en Cosmos para mensajes/frases editoriales comunes;
2. localizaciones embebidas en campaña para contenido propio de la campaña;
3. App Settings para alias físicos Meta, códigos de idioma del canal, flags y límites operativos.

La centralización se hace mediante tipos y resolutores pequeños en Domain/Application. No se crea una
“clase dios”, no se mueve contenido editable a código y un JSON sigue siendo solo transporte de
borradores, no fuente runtime.

## 2. Objetivos

- Una única definición de idioma interno soportado y normalización.
- Un único snapshot efectivo del contenido de campaña por idioma.
- Resolutores especializados para texto global, contenido de campaña, plantilla de canal y directiva LLM.
- Readiness agregado que consulte las mismas políticas usadas en runtime.
- Reducir puntos de contacto sin cambiar JSON/Cosmos/API visibles.

## 3. Diseño objetivo

```text
IdiomaConversacion
        |
        v
ContextoLocalizacion
        |
        +-- IResolutorTextosGlobales --------> catálogo Cosmos/cache/LKG
        +-- IResolutorContenidoCampania -----> localizaciones de Campania
        +-- IResolverPlantillaCanal ---------> App Settings Meta
        +-- IPoliticaIdiomaLlm --------------> directiva de prompt

ReadinessMultiidioma consulta los cuatro, sin duplicar sus reglas.
```

### 3.1 `IdiomaConversacion`

Value object o tipo equivalente en Domain con:

- códigos internos soportados inicialmente `es|en`;
- `Crear/TryCrear`, normalización y comparación;
- default histórico explícito `es` solo en fronteras de deserialización/migración;
- conversión controlada a string para persistencia y DTO.

No se cambia la forma almacenada: `Usuario.idioma`, `Campania.idiomasHabilitados`,
`Conversacion.idioma`, `EnrutamientoAporte.idioma` y `EnvioMensaje.idioma` siguen siendo cadenas en
Cosmos/API. Se reemplazan gradualmente las cinco validaciones duplicadas por la misma política.

El código Meta (`es_CO`, `en_US`, etc.) no pertenece a este value object. Solo
`IResolverPlantillaCanal` crea el puente entre idioma interno y código aprobado por Meta.

### 3.2 `ContextoLocalizacion`

Objeto inmutable de Application construido desde el snapshot del hilo:

- idioma interno;
- gate efectivo;
- campaña/pregunta/mensaje cuando aplique;
- correlationId solo para telemetría, nunca para decidir contenido.

No consulta repositorios ni configuración por sí mismo.

### 3.3 `ContenidoCampaniaEfectivo`

`IResolutorContenidoCampania` devuelve un resultado completo y coherente para una campaña/idioma:

- nombre, descripción, objetivo y mensaje de cierre;
- mensajes iniciales por id (`texto`, `plantillaRef`);
- preguntas por id (`texto`, `instruccion`);
- idioma y origen (`legacy|localizacion`);
- problemas tipificados si falta contenido obligatorio.

Con gate OFF genera el snapshot legacy exacto. Con gate ON exige la localización del mismo idioma y
no mezcla campos localizados con respaldos legacy. Orquestador, evaluador y servicio de envíos dejan
de reconstruir partes de este objeto por separado.

### 3.4 Resolutores especializados

- `IResolutorTextosGlobales`: fachada del catálogo existente; mantiene cache/LKG/emergencia del mismo idioma.
- `IResolutorContenidoCampania`: contenido editorial propio de la campaña.
- `IResolverPlantillaCanal`: `plantillaRef + idioma interno → nombre/código/componentes Meta`.
- `IPoliticaIdiomaLlm`: produce únicamente la directiva de idioma; no traduce prompts ni decide negocio.

Cada resolutor devuelve `Disponible/NoDisponible` tipificado. Ninguno cambia el estado de la
conversación. Los servicios consumidores deciden la transición según su contrato.

### 3.5 Readiness agregado

`ReadinessMultiidioma` compone pruebas de disponibilidad de los mismos resolutores. No implementa otra
resolución paralela. Debe poder explicar, sin contenido sensible:

- idioma inválido/no soportado;
- catálogo global ausente o inválido;
- localización de campaña incompleta;
- mapeo Meta ausente o estructuralmente inválido;
- componente externo que requiere validación humana (aprobación Meta, UAT, costo).

## 4. Reglas de dependencia

- Domain conoce `IdiomaConversacion`, no Cosmos, Meta ni Options.
- Application define interfaces, resultados y orquestación.
- Infrastructure adapta Cosmos/cache y WhatsApp/Meta.
- API/portal solo proyectan DTOs; no resuelven idioma.
- Ningún resolutor llama al LLM para traducir faltantes.
- No hay fallback entre idiomas.

## 5. Compatibilidad

- Sin migración masiva de Cosmos.
- Documentos históricos sin idioma continúan como `es` en su frontera actual.
- Gate OFF conserva byte a byte las decisiones legacy visibles.
- No cambia la forma del JSON de catálogo ni la edición masiva.
- No cambia el contrato de campañas salvo campos aditivos de diagnóstico si fueran necesarios.

## 6. Criterios de aceptación

1. Existe una sola lista/política de idiomas internos soportados.
2. Los cinco puntos de dominio consumen esa política sin cambiar su serialización.
3. Orquestador y envíos consumen `ContenidoCampaniaEfectivo`; no resuelven localizaciones en paralelo.
4. El único puente a códigos Meta vive en `IResolverPlantillaCanal`.
5. Todas las directivas LLM usan `IPoliticaIdiomaLlm` y conservan sus contratos internos no traducidos.
6. Readiness reutiliza resolutores/runtime y no puede declarar listo algo que estos rechazarían.
7. Gate OFF y ON pasan la regresión completa P-32 sin mezcla de idioma.
8. Agregar un idioma nuevo concentra el cambio en política, semilla, contenido de campaña y mapeo Meta;
   no exige buscar lecturas dispersas en el orquestador.

## 7. Fuera de alcance

- Agregar un tercer idioma.
- Traducir el portal.
- Unificar catálogo global y campañas en una misma tabla/documento.
- Sustituir Cosmos por archivos JSON.
- Verificar aprobación Meta mediante Graph API.
- Cambiar prompts/rúbricas a versiones por idioma sin evidencia D5 que lo justifique.

## 8. Orden de implementación

1. **Corte 1/3 — DONE local 2026-08-16:** `IdiomaConversacion` y migración interna de validaciones,
   sin cambiar DTO/Cosmos. Cierre: 1011 unitarias + 120 integración sin Calibración; build, formato y
   diff verdes.
2. **Corte 2/3 — DONE local 2026-08-16:** `ContenidoCampaniaEfectivo` y migración de
   orquestador/envíos. El snapshot atómico cubre nombre, descripción, objetivo, cierre, mensajes y
   preguntas; gate OFF conserva legacy y gate ON rechaza toda localización incompleta sin mezclar.
   Cierre: 1018 unitarias + 120 integración sin Calibración; build, formato y diff verdes.
3. **Corte 3/3 — DONE local 2026-08-16:** fachadas especializadas, política LLM y readiness
   compuesto; duplicaciones protegidas con guarda arquitectónica. Cierre: 1030 unitarias + 120
   integración sin Calibración; build, formato y diff verdes.

Cada corte debe quedar compilable, probado y reversible. La precondición de DT-P32-03 green ya está
cumplida. La repriorización humana expresa del 2026-08-16 convierte este refactor en el siguiente
trabajo de código. Los tres cortes locales terminaron sin mezclar correcciones de DT-RUB-01. La
validación controlada ya ocurrió contra el artefacto real y descubrió el defecto P1. El paso siguiente
es `DT-P32-05`; después `DT-QA-03` habilita la salida simulada. No se repite ahora la corrida completa.
