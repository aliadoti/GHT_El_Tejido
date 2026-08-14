# QAS 22 — DT-P32-02: semillas, JSON masivo y readiness

> **Estado:** guía preparada; ejecutar después de implementar `DT-P32-02`.
> **Objetivo:** comprobar que un ambiente puede inicializar catálogos `es/en`, editar todo el
> contenido mediante descarga/carga JSON y bloquear campañas que no estén listas, sin compilar ni
> publicar accidentalmente.

## Antes de empezar

Necesitas:

- ambiente aislado o autorizado para QA;
- usuario `admin` y, para permisos, un usuario `visor`;
- gate `Conversacion:CatalogoTextosHabilitado` inicialmente OFF;
- no usar campañas ni participantes reales;
- carpeta local temporal para editar el JSON, fuera del repositorio; y
- confirmación de que la simulación no llegará a números reales. Si no existe ese aislamiento, las
  pruebas conversacionales quedan `BLOCKED`; no se improvisa un envío.

No cambies secretos, plantillas Meta, prompts, rúbricas ni App Settings editoriales durante la guía.

## Prueba 1 — semilla base independiente del legacy

1. En un ambiente sin catálogo persistido para el idioma, abre **Textos de conversación**.
2. Selecciona `es` y pulsa **Crear semilla base**.
3. Repite para `en`.
4. Comprueba que ambas versiones nacen en `borrador`.

Debe verse contenido completo y válido en ambos idiomas. No debe activarse ninguna versión.

La prueba automática debe incluir una configuración legacy con más de 30 frases y demostrar que esa
configuración no impide crear la semilla base.

## Prueba 2 — preview de configuración anterior

1. Usa **Revisar configuración anterior** para `es`.
2. Si contiene un grupo que excede el límite, verifica el nombre del grupo, cantidad y límite.
3. Descarga la configuración anterior como JSON y confirma que conserva todas las entradas, incluso
   las que exceden el límite; corrígela localmente si quieres reutilizarla.
4. No confirmes importación.
5. Recarga el historial.

No debe aparecer una versión nueva. Es falla si el preview guarda, recorta frases, mezcla valores
base o muestra contenido en logs técnicos.

## Prueba 3 — descargar JSON para edición masiva

1. Selecciona una semilla borrador.
2. Pulsa **Descargar JSON para edición masiva**.
3. Abre el archivo en un editor de texto.

Debe ser JSON UTF-8 indentado con `formato`, `familiaId`, `idioma`, todos los `mensajes` y todos los
grupos de `frases`. El nombre termina en `-editable.json`.

No debe contener secretos, plantillas físicas Meta ni datos de participantes.

## Prueba 4 — editar y cargar masivamente

1. Cambia al menos dos mensajes.
2. Agrega, modifica y retira frases en dos grupos diferentes, sin cambiar las claves.
3. Guarda el archivo.
4. Selecciona **Cargar JSON editado**.
5. Revisa el resumen y pulsa **Importar como nuevo borrador**.

Debe mostrarse el idioma, conteos y cero errores. Se crea una versión nueva en borrador con todos los
cambios y queda seleccionada. La versión de origen y la activa permanecen intactas.

Es falla si hay que compilar/desplegar, si se sobrescribe el borrador anterior o si la carga activa.

## Prueba 5 — errores completos y cero escritura

En copias separadas del JSON, provoca:

- mensaje vacío;
- clave desconocida;
- placeholder inventado;
- frase duplicada después de normalizar;
- grupo por encima del límite operativo;
- idioma distinto al seleccionado; y
- `formato` desconocido.

Prevalida cada archivo. Deben aparecer todos los errores detectables con campo/motivo y sin crear
versiones. La versión activa no cambia.

Corrige un archivo y vuelve a seleccionar exactamente el mismo nombre. El portal debe leer la versión
corregida; el input no puede quedarse reteniendo el intento anterior.

## Prueba 6 — permisos y auditoría

1. Como `visor`, descarga y previsualiza.
2. Intenta crear semilla o importar.
3. Como `admin`, repite con CSRF válido.

El visor no puede mutar. La auditoría del admin contiene acción, idioma, tamaño, conteos, resultado,
versión/huella y correlationId, pero no el JSON ni textos/frases.

## Prueba 7 — readiness real

1. Consulta readiness antes de activar catálogos.
2. Activa explícitamente `es`, consulta de nuevo.
3. Activa explícitamente `en`, consulta de nuevo.
4. Compara con `GET .../efectivo` y con el estado real del gate.

Readiness debe distinguir borrador, activo, emergencia y gate. El preview efectivo no puede reportar
por sí solo que el gate está ON.

## Prueba 8 — campaña bilingüe protegida

1. Deja uno de los idiomas sin catálogo activo.
2. Intenta activar una campaña `es/en` con localizaciones completas.
3. Activa y aprueba el catálogo faltante.
4. Repite la activación de campaña.

El primer intento falla indicando `catalogosTextos.{idioma}: activo_requerido`; el segundo puede
continuar si las demás dependencias están completas. No debe caer silenciosamente a español.

## Prueba 9 — regresión y corrida P-32

1. Con gate OFF, ejecuta la regresión legacy aplicable de `QAS/16`.
2. En ventana aislada autorizada, reinicia la API con gate ON.
3. Ejecuta `QAS/17_Prompt_Ejecutar_Validacion_Completa_P32.md` completo.
4. Verifica expresamente que el aporte, idea consolidada, Resultados y Markdown del hilo inglés
   permanezcan en inglés.
5. Completa D5, UAT, Meta, costo/latencia y rollback, o marca un prerequisito externo `BLOCKED`.

Cualquier FAIL o BLOCKED mantiene el gate OFF y evita iniciar `DT-I20-02`.

## Evidencia mínima

- ambiente, fecha y ejecutor;
- límites efectivos, sin App Settings sensibles;
- ids técnicos de versiones creadas y estados;
- JSON de QA sin secretos, o su huella si no debe conservarse;
- conteos/errores de cada prevalidación;
- readiness antes/después;
- resultado de campaña incompleta;
- resultado completo de `QAS/17`; y
- decisión final `GREEN` o `NO GREEN`.

## Resultado final

`DT-P32-02` solo queda green si las nueve pruebas pasan, la validación automática está verde, no hubo
activación/importación accidental, el gate termina en el estado acordado y la corrida P-32 no mezcla
ni traduce el idioma del aporte. Solo entonces el handoff puede volver a `DT-I20-02`.
