# P-32 — Conversación multidioma y catálogo versionado de textos

**Estado:** **DONE local 2026-08-11 (4/4)**. Catálogo/API, caché/LKG, emergencia `es/en`,
localizaciones, envío inicial mixto, contextos LLM, portal operativo y gate OFF listos. La corrida del
2026-08-13 abrió `DT-P32-02` (0/3): semilla base independiente de legacy, JSON masivo prevalidado,
readiness y catálogo activo obligatorio para campaña bilingüe. Debe cerrar green antes de la
activación controlada y antes de retomar `DT-I20-02`.
**Solicitud:** conversación en español e inglés según el idioma del maestro de usuarios y edición de
textos sin recompilar.
**Áreas afectadas:** maestro de usuarios, campañas, envío inicial, enrutamiento, orquestador,
evaluación/redacción LLM, configuración, Cosmos, portal, seguridad, observabilidad y QA.
**Contratos relacionados:** `03 §3.1/§3.3/§3.5/§3.6/§3.9/§3.13.1`, `04 §5.3/§5.4/§5.7.1`,
`05 §2/§4`, `07 §2/§5.3`, `08 §3`, `10 §6`, `11 §6`, `13 §2–§4`, `Reglas §2.12/§3`.
**Extiende:** I-08 v2 (`Usuario.Idioma`), I-20 (redacción visible), P-26/P-27/P-28/P-29/P-30/P-31.

---

## 1. Resumen ejecutivo

El maestro ya contiene el dato necesario: `Usuario.Idioma` existe, admite `es|en`, usa `es` por
defecto y está persistido y expuesto por API, carga masiva y portal. P-32 **no vuelve a crear ese
campo**; lo consume como fuente de verdad.

El flujo todavía no es bilingüe. Los mensajes de campaña son escalares, el envío inicial resuelve una
sola plantilla/idioma antes de recorrer participantes, varios detectores y respaldos están compilados
en español y `Conversacion:Mensajes:*`/`Conversacion:Frases*` mezclan contenido editorial con
configuración operativa de App Service. Por eso un lote con participantes `es` y `en` no puede producir una conversación coherente de punta a punta.

P-32 introduce dos capacidades complementarias:

1. **Resolución determinista del idioma efectivo** desde `Usuario.Idioma`, con snapshot por hilo y
   uso consistente en mensajes, menús, detección de intenciones, LLM y plantilla Meta.
2. **Catálogo de textos conversacionales versionado en Cosmos**, editable desde el portal y con
   importación/exportación JSON. Los valores cambian sin compilar ni desplegar; sus claves y reglas
   siguen siendo contrato del software.

La fuente de verdad del contenido global será el contenedor existente `config`; no se agrega un
recurso Azure. El código conserva un catálogo mínimo de emergencia en `es` y `en`, pero solo como
respaldo de continuidad, no como mecanismo editorial habitual.

---

## 2. Verificación del estado actual

### 2.1 Campo de idioma confirmado

- `Usuario.Idioma` está en `Domain/Usuarios/Usuario.cs`.
- Valores admitidos hoy: `es` y `en`; ausencia/vacío se normaliza a `es`.
- `UsuarioCosmosDocument` persiste el campo JSON `idioma` y lo rehidrata al dominio.
- Alta/edición/listado, filtros, CSV/XLSX y portal ya lo transportan.
- Los contratos `03 §3.1` y `04 §5.1` ya lo documentan.

### 2.2 Brechas encontradas

| Superficie | Estado actual | Brecha P-32 |
|---|---|---|
| Textos genéricos | Defaults españoles en `OpcionesMensajesConversacion`; muchos overrides en `Conversacion:Mensajes:*`. | No hay catálogo por idioma, aprobación ni edición desde portal. |
| Frases de intención | Listas españolas compiladas o en `Conversacion:Frases*`; un caso aislado en inglés. | La detección no se resuelve por idioma ni tiene una administración unificada. |
| Campaña | `MensajeInicial.Texto`, `Pregunta.Texto/Instruccion` y `MensajeCierre` son escalares. | No existe contenido equivalente `es/en` bajo el mismo id semántico. |
| Envío inicial | La plantilla global se resuelve antes del `foreach` de participantes. | Un lote mixto usa un único idioma de Meta. |
| LLM | Parte del contexto conoce `Idioma`, pero no se propaga en todos los caminos; el redactor exige español. | La salida visible puede ignorar el idioma del participante. |
| Trazabilidad | Conversación, envío y evaluación no fijan el idioma efectivo. | Un cambio posterior del maestro dificulta reproducir lo ocurrido. |

El inventario detallado y el orden de migración están en
`planes/P-32_Inventario_y_Migracion_Textos.md`.

---

## 3. Decisiones de arquitectura

| Decisión | Regla aprobada |
|---|---|
| Fuente del idioma | `Usuario.Idioma`; no se autodetecta ni se pide al LLM que lo decida. |
| Alcance inicial | Solo `es` y `en`, coherente con el dominio vigente. El modelo admite agregar otro código después mediante cambio explícito de contrato. |
| Consistencia del hilo | El idioma se copia al crear la conversación/ciclo. Cambiar el maestro aplica al siguiente hilo/ciclo, no a mitad de uno abierto. |
| Fuente de textos globales | `CatalogoTextosConversacion` versionado en Cosmos `config`, por idioma. |
| Contenido de campaña | Localizaciones embebidas bajo los mismos ids de campaña, mensaje y pregunta; no se duplican campañas por idioma. |
| Edición | Portal/API y JSON importable como **borrador**; una importación nunca activa contenido automáticamente. |
| Versionado | Borrador editable en sitio; activo/inactivo inmutable; editar un comprometido crea nueva versión. Exactamente una versión activa por idioma. |
| Activación | Cambio atómico dentro de la partición del catálogo y con ETag; la versión anterior queda inactiva y puede reactivarse como rollback. |
| Caché | Versión activa en memoria con expiración corta; ante fallo se usa la última versión válida conocida. |
| Respaldo | Catálogo mínimo compilado en `es/en` para continuidad. Nunca reemplaza la validación de contenido propio de campaña. |
| Traducción | Curada y aprobada por una persona. El sistema no traduce automáticamente preguntas, aportes ni historiales. |
| Decisiones | “El modelo propone, el servidor dispone”: idioma, estado, selección, límites y cierre siguen siendo deterministas. |

### 3.1 Por qué Cosmos y no un JSON del repositorio

Un JSON versionado dentro del repositorio sigue exigiendo commit, despliegue y validación operativa;
no cumple por sí solo “sin compilar”. Un archivo externo en Blob sí podría cambiarse en runtime, pero
obligaría a crear permisos, invalidación de caché, edición, versionado, auditoría y rollback que Cosmos
`config` ya resuelve dentro de la arquitectura aprobada. JSON queda como formato de intercambio, no
como fuente primaria.

### 3.2 Qué permanece en variables de entorno

Las variables de entorno/App Settings son correctas para **configuración operativa por ambiente**:
endpoints, nombres de secretos, ids/aliases del proveedor, feature flags y kill-switches, límites,
timeouts, cuotas, intervalos y duración de caché.

No son la opción recomendada para **contenido editorial multidioma**: mensajes largos, variantes,
menús, ayudas y listas de frases que el negocio calibra. En App Settings ese contenido carece de una
edición amigable, aprobación y preview; las listas son frágiles por índices/escaping/encoding; se
mezcla operación con contenido y el cambio suele reiniciar la aplicación. P-32 migra gradualmente
`Conversacion:Mensajes:*` y `Conversacion:Frases*` al catálogo. Las claves legacy se conservan solo
durante la transición y quedan deprecadas tras validar el corte final.

---

## 4. Modelo de contenido

### 4.1 Catálogo global

Cada versión contiene un solo idioma y la misma lista de claves permitidas:

> **Extensión P-33 (2026-08-13):** el registro pasa de 24 a 29 mensajes y de 13 a 16 listas. Las
> claves y la migración compatible están en `planes/P-32_Inventario_y_Migracion_Textos.md §3.3`.
> Versiones históricas no se mutan y usan respaldo compilado del mismo idioma solo para las claves
> nuevas; toda versión posterior debe estar completa.

```json
{
  "id": "catalogo_conversacion_en_v3",
  "type": "CatalogoTextosConversacion",
  "pk": "CatalogoTextosConversacion",
  "familiaId": "catalogo_conversacion",
  "idioma": "en",
  "version": 3,
  "estado": "activo",
  "mensajes": {
    "saludoPrimerContacto": "Hello! Thanks for reaching out.",
    "saludoSiguientePregunta": "Let's continue with the next question:",
    "mensajeConfiguracionNoDisponible": "We cannot continue right now. Please try again later."
  },
  "frases": {
    "continuar": ["that is fine", "next question"],
    "finalizarIdea": ["leave this idea"],
    "finalizarParticipacion": ["stop for now"]
  },
  "creadoPor": "u_admin",
  "aprobadoPor": "u_admin_2",
  "creadoEn": "2026-08-10T15:00:00Z",
  "activadoEn": "2026-08-10T16:00:00Z",
  "huella": "sha256:..."
}
```

Reglas:

- El registro de claves válidas y obligatorias vive en código; solo los **valores** son editables.
- Clave desconocida, faltante, duplicada después de normalizar, valor vacío, placeholder inválido o
  límite excedido ⇒ `400 VALIDATION_ERROR`; nunca se activa parcialmente.
- Texto plano UTF-8; sin HTML/Markdown ejecutable. Se permiten únicamente placeholders declarados
  (`{{nombre}}`, `{{campaña}}`/`{{campania}}`, `{{empresa}}`, `{{area}}`) y se rechaza uno desconocido.
- `estado` ∈ `borrador|activo|inactivo`. Solo `borrador` se edita en sitio.
- Una versión activa es inmutable. El contenido efectivo se identifica por `idioma+version+huella`.
- La activación de una versión inactiva anterior es el rollback; no se copia ni se reescribe.

### 4.2 Contenido localizado de campaña

La campaña conserva ids únicos y añade localizaciones:

```json
{
  "idiomasHabilitados": ["es", "en"],
  "localizaciones": {
    "es": { "nombre": "Convención 2026", "descripcion": "...", "objetivo": "..." },
    "en": { "nombre": "2026 Convention", "descripcion": "...", "objetivo": "..." }
  },
  "mensajesIniciales": [{
    "id": "mi_1",
    "localizaciones": {
      "es": { "texto": "Hola {{nombre}}...", "plantillaRef": "inicio_campania" },
      "en": { "texto": "Hello {{nombre}}...", "plantillaRef": "campaign_start" }
    }
  }],
  "preguntas": [{
    "id": "p_ingresos",
    "localizaciones": {
      "es": { "texto": "Escribe una idea...", "instruccion": "Sé concreto..." },
      "en": { "texto": "Share an idea...", "instruccion": "Be specific..." }
    }
  }],
  "configConversacional": {
    "mensajesCierrePorIdioma": {
      "es": "Gracias. Tu aporte quedó registrado.",
      "en": "Thank you. Your contribution has been recorded."
    }
  }
}
```

Compatibilidad:

- Documento histórico sin `idiomasHabilitados` equivale a `["es"]`.
- Para `es`, si falta `localizaciones.es`, se aceptan temporalmente los campos escalares actuales
  (`nombre`, `texto`, `instruccion`, `mensajeCierre`).
- Para `en` **nunca** se cae silenciosamente al español. Una campaña bilingüe incompleta no puede
  activarse ni asociar/enviar al participante afectado.
- El portal nuevo escribe `localizaciones.es` y mantiene los campos escalares españoles durante la
  ventana de compatibilidad para lectores antiguos.

### 4.3 Plantillas de WhatsApp

El texto de negocio y el nombre técnico aprobado por Meta no son la misma cosa. La campaña referencia
un alias lógico (`plantillaRef`) por idioma; App Settings mapea ese alias al nombre/código de plantilla
que existe en cada ambiente. Así el contenido se edita en Cosmos y los identificadores operativos de
Meta permanecen por ambiente.

El envío resuelve **dentro del ciclo de participantes**:

`Usuario.Idioma → localización del mensaje → plantillaRef → plantilla Meta del ambiente`.

Si falta la plantilla aprobada del idioma, ese participante queda en `error` con motivo tipificado;
no recibe la plantilla española y el resto del lote continúa.

---

## 5. Resolución del idioma efectivo

1. Cargar el `Usuario` activo y normalizar `Idioma`.
2. Al iniciar un envío o crear un hilo/ciclo, guardar el idioma efectivo en el documento de negocio.
3. Resolver campaña, catálogo, detectores y LLM con ese snapshot.
4. Un cambio del maestro se aplica al siguiente envío proactivo o hilo/ciclo nuevo. Un hilo abierto
   conserva su idioma para no mezclar idiomas ni cambiar el significado de selecciones pendientes.
5. Para aplicar un cambio inmediatamente, el administrador cierra/reinicia el hilo de prueba y abre
   uno nuevo; no se muta retroactivamente el historial.

No hay autodetección. Los aportes se guardan tal como llegaron y no se traducen. Los comandos críticos
de salida pueden reconocerse en ambos catálogos como defensa adicional, pero el menú y las ayudas se
renderizan únicamente en el idioma efectivo.

---

## 6. Flujo de lectura y degradación

Con `Conversacion:CatalogoTextosHabilitado=false` se conserva exactamente el camino legacy.

Con el gate activo:

1. leer versión activa del idioma desde caché;
2. si expiró, refrescar desde Cosmos y validar huella/esquema;
3. si Cosmos falla o devuelve una versión inválida, conservar la **última versión válida conocida**;
4. si el proceso aún no tiene una versión válida, usar el catálogo mínimo compilado del idioma y
   emitir alerta; y
5. si falta contenido propio de campaña, detener esa transición/envío con un mensaje de emergencia en
   el idioma correcto, sin inventar pregunta ni cambiar estado.

La expiración de caché es configuración operativa (`Conversacion:CatalogoTextos:CacheSegundos`,
default recomendado: 60). Un cambio de catálogo no requiere recompilar; puede tardar como máximo esa
ventana en verse, salvo que la activación invalide la caché local de inmediato.

---

## 7. LLM y reglas deterministas

- Todos los contextos de evaluación, consolidación, segmentación, clasificación y redacción reciben
  `idioma` y una instrucción de salida inequívoca (`es` o `en`).
- `RedactorTurnoConversacional` deja de fijar “en español”.
- La pregunta/instrucción usada por el LLM es la localización correspondiente al snapshot del hilo.
- Los nombres de campos JSON, enums, motivos, estados, códigos de error y eventos de telemetría no se
  traducen: son contratos internos estables.
- Filtros de fuga de rúbrica, pregunta única, longitud, prompt-injection, cupos y fallback operan igual
  en ambos idiomas. Su vocabulario de detección debe incluir equivalentes `es/en`.
- El LLM no elige idioma, campaña, pregunta, idea, umbral ni transición.

---

## 8. API y portal

### 8.1 API administrativa del catálogo

- `GET /api/admin/catalogos-textos?idioma=&estado=`
- `POST /api/admin/catalogos-textos` — crea v1 en borrador.
- `GET /api/admin/catalogos-textos/{familiaId}/{idioma}/versiones`
- `PUT /api/admin/catalogos-textos/{familiaId}/{idioma}/versiones/{version}` — solo borrador.
- `POST /api/admin/catalogos-textos/{familiaId}/{idioma}/versiones` — clona a nueva versión borrador.
- `POST /api/admin/catalogos-textos/{familiaId}/{idioma}/versiones/{version}/activar`
- `GET /api/admin/catalogos-textos/efectivo?idioma=es|en` — preview con fuente/versión.
- `GET /api/admin/catalogos-textos/{familiaId}/{idioma}/versiones/{version}/exportar`
- `POST /api/admin/catalogos-textos/importar` — valida e importa siempre como borrador.
- `POST /api/admin/catalogos-textos/semillas/{idioma}` — fotografía `es` desde la configuración
  efectiva o usa la traducción curada `en`; crea una nueva versión en borrador y nunca activa.

**Extensión DT-P32-02:** separa rutas explícitas para semilla base y fotografía legacy, agrega
prevalidación sin escritura y readiness. La exportación/importación JSON pasa a ser un flujo masivo
de primera clase: descargar catálogo completo, editar valores/listas, prevalidar y confirmar una
versión nueva en borrador. Ver `DT-P32-02_Semillas_Edicion_Masiva_y_Readiness_Catalogo_Textos.md`.

GET: `admin|visor`. Mutaciones: `admin` + CSRF. Conflicto ETag o activación concurrente ⇒ `409`.

### 8.2 Portal

Nueva pantalla **Textos de conversación**:

- selector de idioma, estado y versión;
- edición por grupos (saludos, coaching, cierres, menús, errores y frases de intención);
- contador/límite, placeholders permitidos y errores al lado del campo;
- preview del texto efectivo y comparación con la versión activa;
- crear versión, guardar borrador, activar, reactivar una anterior, importar y exportar JSON;
- aviso de que activar afecta conversaciones nuevas y que el contenido de campaña se administra en
  la campaña.

Campañas añade pestañas `Español`/`English`, indicador de completitud y validación antes de activar.
El portal administrativo permanece en español en P-32; localizar toda la UI está fuera de alcance.

---

## 9. Seguridad, auditoría y privacidad

- Los textos de catálogo son **contenido de negocio**, no secretos; viven completos en `config`.
- `LogSeguridad` registra actor, acción, idioma, familia, versión, resultado, motivo y huella; **no
  duplica el texto ni las frases**.
- Acciones auditadas: `crearBorrador`, `editarBorrador`, `importar`, `activar`, `rollback`,
  `rechazarValidacion` y `fallbackRuntime`.
- Activación atómica en la misma partición Cosmos; ETag evita perder cambios concurrentes.
- Importación limita tamaño, profundidad, número de claves/variantes y longitud; no admite claves
  desconocidas, HTML ni placeholders no autorizados.
- Preview escapa el contenido; nunca usa `innerHTML` con texto administrado.
- La telemetría conversacional agrega `idioma`, `catalogoVersion` y `origenTexto`
  (`catalogo|cache|emergencia|legacy`), sin texto del participante ni del catálogo.

---

## 10. Activación y validaciones de campaña

Una campaña solo puede activarse como bilingüe si:

1. existe catálogo global activo y válido para cada idioma habilitado;
2. nombre visible, mensajes iniciales, preguntas, instrucciones y cierre están completos en esos
   idiomas;
3. todos los placeholders coinciden con los permitidos;
4. existe un mapeo de plantilla Meta para cada `plantillaRef+idioma` usado en envío proactivo; y
5. las rúbricas/prompts/config LLM cumplen sus validaciones actuales.

Asociar un participante cuyo idioma no está habilitado devuelve `409 IDIOMA_CAMPANIA_NO_HABILITADO`.
Si el idioma cambia después de asociarlo, el envío revalida por participante y lo deja en error
tipificado sin detener el lote. La entrada a una campaña inconsistente no avanza estado y alerta a
operación.

**Defensa en profundidad (corrección 2026-08-13):** una campaña que declara un idioma distinto de
`es` se valida completa al activarse aun cuando el gate de runtime esté apagado; no puede quedar
"bilingüe" e inválida para activarla después. La asociación a una campaña bilingüe incompleta devuelve
`409 CAMPANIA_IDIOMA_INCOMPLETA`, y el enrutamiento excluye de forma segura cualquier registro
histórico inconsistente que hubiera quedado antes de esta validación. Así, un participante `en` nunca
entra a una conversación que pueda degradar silenciosamente a español.

---

## 11. Plan de migración e implementación

| Corte | Entrega verificable | Pruebas mínimas |
|---|---|---|
| 1 | **DONE local 2026-08-10.** Contratos aditivos, entidad/repositorio de catálogo, validación/versionado, proveedor con caché/última versión válida y API admin. Gate OFF. Semilla `es` desde valores efectivos actuales y catálogo `en` curado. | Esquema, claves, límites, borrador/inmutabilidad, ETag/activación atómica, caché/fallo Cosmos, JSON import/export, permisos y regresión legacy. |
| 2 | **DONE local 2026-08-10.** `Conversacion.Idioma` queda fijado al crear o abrir un ciclo, persiste en Cosmos, conserva `es` en documentos históricos y se expone en Resultados. Los mensajes globales, variantes, menús/frases de enrutamiento, detectores del orquestador y aclaraciones P-27 se resuelven por el adaptador con el snapshot del hilo o ruta. `EnrutamientoAporte.Idioma` conserva el idioma de una selección pendiente aunque cambie el maestro. El gate sigue OFF. **Siguiente:** localizaciones de campaña del corte 3; después propagar idioma a evaluación/LLM en el corte 4. | Round-trip Cosmos, hilo/ruta nueva `en`, transiciones inmutables, menú y aclaración inglesa, comando determinista inglés y regresión legacy con gate OFF. |
| 3 | **DONE local 2026-08-11.** Localizaciones embebidas, validación de completitud, portal de campaña y envío inicial mixto con plantilla por participante. Gate OFF conserva el flujo histórico; con ON el fallo localizado se registra por participante y el lote sigue. | Campaña legacy `es`; campaña bilingüe; faltante `en` bloquea; lote mixto usa dos plantillas; fallo de una no detiene las demás; snapshots de envío. |
| 4 | **DONE local 2026-08-11; corrección P-32 local 2026-08-13.** Evaluación, segmentación, consolidación y redacción reciben idioma y contenido localizado; el redactor ya no impone español. También se localizan saludo/pregunta inicial y la siguiente pregunta. Portal administrativo: semilla, importación/exportación JSON, borrador, edición, activación y reactivación explícita con ETag. La reactivación de una versión inactiva ahora es un rollback real auditado; campañas bilingües incompletas se bloquean en activación, asociación y enrutamiento. QAS y deprecación documentada. | Backend 771 unitarias + 87 integración verdes; portal 43/43 previo, build Angular y Prettier verdes con Node temporal `22.22.3`; regresiones de rollback y campaña bilingüe incompleta. |

**Extensión posterior:** `DT-P32-02` se ejecuta en tres cortes antes de la siguiente corrida
operativa: (1) base segura/prevalidación legacy, (2) JSON masivo/readiness/guardia de campaña y (3)
portal/QAS. No reabre los cuatro cortes funcionales de idioma ni activa el gate.

El inventario de migración define qué clave sale de cada origen actual. Cada corte mantiene el gate
apagado y no cambia configuración remota. Activar requiere UAT bilingüe, plantillas Meta aprobadas y
acta de cambio.

---

## 12. Criterios de aceptación

1. Un usuario `es` completa el recorrido en español y uno `en` completa el mismo recorrido en inglés,
   incluyendo saludo, menús, coaching, cierres, errores y reingreso.
2. El idioma efectivo siempre parte del maestro y queda fijado en el hilo; el LLM no lo decide.
3. Un lote mixto selecciona texto y plantilla por participante, no una vez por campaña/job.
4. Editar y activar un texto desde portal cambia conversaciones nuevas sin compilar ni desplegar.
5. El historial conserva versión/huella; reactivar una versión anterior revierte el contenido sin
   recompilar.
6. Una versión inválida nunca queda activa ni reemplaza parcialmente el catálogo vigente.
7. Un fallo de Cosmos conserva la última versión válida; un cold start sin catálogo usa el respaldo
   mínimo del idioma y alerta sin romper estados.
8. Campaña bilingüe incompleta no se activa; nunca entrega silenciosamente español a un usuario `en`.
9. Cambiar el idioma del maestro no mezcla un hilo abierto; el siguiente ciclo usa el nuevo idioma.
10. Aportes e historial permanecen en su idioma original; no hay traducción automática.
11. Las reglas deterministas, guardrails, estados, cupos y decisiones son iguales en ambos idiomas.
12. Logs técnicos y de seguridad no contienen textos/frases del catálogo ni aportes; sí registran
    idioma, versión, origen, resultado y huella.
13. Importar JSON solo crea borrador; activar exige acción explícita de un admin.
14. Con el gate apagado, las pruebas actuales no cambian de resultado.
15. Una configuración legacy inválida no impide crear semillas base válidas `es/en`.
16. Descargar, editar y reimportar el JSON completo crea una versión nueva en borrador, nunca
    sobrescribe ni activa.
17. Una campaña bilingüe exige un catálogo global activo y válido para cada idioma habilitado.

---

## 13. Fuera de alcance

- Traducir automáticamente contenido existente o mensajes del participante.
- Detectar el idioma por el texto o permitir que el participante lo cambie dentro del chat.
- Localizar el portal administrativo completo.
- Soportar idiomas distintos de `es/en` sin ampliar primero el contrato del maestro.
- Administrar en el catálogo secretos, endpoints, límites, flags o ids físicos de Meta.
- Cambiar la rúbrica, la puntuación, estados o decisiones de cierre según el idioma.

---

## 14. Rollback

1. Apagar `Conversacion:CatalogoTextosHabilitado` para volver al comportamiento legacy durante la
   migración; no borrar versiones.
2. Para un error editorial aislado, reactivar la versión anterior del idioma desde el portal.
3. Para un error de contenido de campaña, cerrar/desactivar la campaña o retirar el idioma afectado;
   no caer a otro idioma.
4. Para un error de plantilla Meta, corregir el mapeo operativo o pausar envíos de ese idioma.
5. Los snapshots permanecen append-only y permiten explicar qué versión vio cada participante.

---

## 15. Insumos externos antes de activar

- Traducción inglesa aprobada de todos los textos y frases del inventario.
- Plantilla(s) HSM en inglés aprobada(s) por Meta y mapeada(s) en el ambiente.
- Al menos un participante de prueba `es` y otro `en`.
- UAT bilingüe con criterios de naturalidad, seguridad y equivalencia funcional.
- Decisión de operación sobre quién puede aprobar/activar contenido; técnicamente debe ser rol
  `admin`, y se recomienda separación entre autor y aprobador cuando haya dos personas disponibles.
