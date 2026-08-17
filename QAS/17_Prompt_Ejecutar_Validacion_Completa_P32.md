# 17 — Prompt para ejecutar la validación completa de P-32

> Copia el bloque entre INICIO y FIN en el agente que ejecutará la prueba. Sirve para Codex, Claude
> Code u otro agente con acceso autorizado al ambiente de pruebas. No presupone acceso a Azure ni
> autoriza cambios remotos.
>
> **Estado 2026-08-16:** actualizado para validar `DT-P32-04` corte 3/3. La implementación está local;
> la parte remota solo aplica cuando un humano confirme que ese artefacto fue desplegado en el ambiente
> aislado y autorice la ventana.

## Antes de enviarlo

El responsable humano debe confirmar: ambiente aislado, URL, acceso administrativo, plantillas Meta
inglesas aprobadas si se probará WhatsApp real, presupuesto D5 y la ventana autorizada para encender
temporalmente el catálogo. El agente crea en cada ejecución los usuarios y campañas de prueba; no
reutiliza datos de una corrida anterior. Si falta algo, debe dejarlo como BLOCKED.

### Acceso seguro para simulación en Azure

El agente **no obtiene secretos**. Si la prueba se hará contra Azure, un operador autorizado debe:

1. habilitar temporalmente `Simulacion__Habilitada=true` en el ambiente de pruebas;
2. entregar la clave de diagnóstico al proceso del agente como variable de entorno secreta
   `GHT_DIAG_KEY`, sin pegarla en chat, prompt, archivo, Git ni reporte; y
3. al cierre, apagar `Simulacion__Habilitada` y retirar `GHT_DIAG_KEY` de la sesión.

La clave es la que resuelve el servidor desde `Diagnostico:ClaveSecretName` (normalmente el secreto
`diag-key`) o `Diagnostico:Clave`. No es el App Secret de Meta ni la clave del LLM. Si el agente no
recibe `GHT_DIAG_KEY`, puede ejecutar pruebas locales, pero debe marcar como **BLOCKED** las llamadas
de simulación contra Azure.

### Invocación corta para el agente

Con el agente iniciado desde la misma sesión que contiene `GHT_DIAG_KEY`, basta pedirle:

```text
Valida DT-P32-04 siguiendo estrictamente QAS/17_Prompt_Ejecutar_Validacion_Completa_P32.md. Ejecuta solo lo autorizado, no corrijas ni despliegues, y reporta cada caso como PASS, FAIL o BLOCKED con evidencia sin secretos ni datos sensibles.
```

No copies la clave ni el contenido del documento al chat. El archivo contiene el procedimiento
completo y el agente debe detenerse con `BLOCKED` si no puede cumplir una precondición.

## ▼ INICIO DEL PROMPT ▼

Actúa como SDET/QA senior para El Tejido. Ejecuta y documenta la **validación completa de P-32:
conversación español/inglés y catálogo de textos** en el ambiente de pruebas autorizado.

Primero lee `QAS/22_DT-P32-02_Semillas_JSON_y_Readiness_Como_Probar.md`,
`QAS/23_DT-P32-03_Cierre_y_Readiness_Meta_Como_Probar.md`,
`QAS/16_P32_Multidioma_Catalogo_Textos_Como_Probar.md`,
`QAS/18_Runbook_Humano_Lanzar_Prueba_P32.md`,
`Especificaciones/Iniciativas/DT-P32-04_Nucleo_Transversal_Multidioma.md`,
`Especificaciones/planes/DT-P32-04_Plan_Refactor_Multidioma.md`,
`Especificaciones/Iniciativas/DT-P32-03-01_Readiness_Gate_Solo_Campanias_Activas.md`,
`Especificaciones/Iniciativas/DT-P32-02_Semillas_Edicion_Masiva_y_Readiness_Catalogo_Textos.md`,
`Especificaciones/Iniciativas/P-32_Conversacion_Multidioma_y_Catalogo_Textos.md` §§10, 12, 14 y 15,
`tests/Calibracion/README.md` y `QAS/06_Criterios_Aceptacion_LLM.md`.

Reglas obligatorias:

1. Antes de hacer nada, informa el ambiente, el artefacto o revisión desplegada, la autorización
   disponible, los datos de prueba que usarás y un plan corto. Si no puedes confirmar que el ambiente
   contiene DT-P32-04 corte 3/3, marca la validación remota como BLOCKED. Si no hay autorización
   explícita para desplegar, activar temporalmente el catálogo o ejecutar D5 real, no hagas ese cambio:
   marca el caso BLOCKED y continúa solo con lo permitido.
2. No hagas push, despliegue, cambio de secretos, modificación de rúbricas/prompts/configuraciones LLM
   existentes ni carga de datos reales. Sí estás autorizado a crear los usuarios, catálogos borrador y
   campañas de prueba definidos abajo. No uses el App Secret de Meta. No inventes URLs, credenciales,
   plantillas, traducciones ni resultados.
3. Para las llamadas de simulación contra Azure, lee exclusivamente `GHT_DIAG_KEY` desde el entorno de
   ejecución y envíala solo como header `X-Diag-Key`. Nunca muestres su valor, lo escribas en el
   reporte, lo incluyas en comandos visibles, lo guardes en archivos ni intentes obtenerlo de Key Vault,
   App Settings o el navegador. Si la variable no existe o la respuesta es `404`, marca la simulación
   como BLOCKED y explica la condición sin adivinar si la clave, el gate o la ruta son la causa.
4. Ejecuta primero la regresión con `Conversacion:CatalogoTextosHabilitado=false`. Luego, solo si la
   ventana ya fue preparada por un humano autorizado, valida con el gate temporalmente ON. Al terminar,
   confirma que quedó OFF, salvo que exista una aprobación formal de activación productiva.
   Antes de cualquier conversación confirma que el ambiente saliente está aislado o que todos los
   números son de prueba autorizados: la simulación entrante no desactiva automáticamente el emisor
   real. Sin esa garantía, marca el recorrido `BLOCKED` y no envíes mensajes.
5. Antes de la prueba remota, ejecuta secuencialmente el gate local del checkout:
   `dotnet build -c Release -warnaserror`,
   `dotnet test -c Release --no-build --filter "Category!=Calibracion"`,
   `dotnet format --verify-no-changes --no-restore` y `git diff --check`. Registra conteos y resultado;
   no modifiques código para corregir fallos durante esta corrida. Un fallo local detiene la validación
   remota como FAIL.
6. Prepara una corrida nueva antes de puntuar. Conserva los datos para auditoría; no borres campañas,
   usuarios ni evidencia al terminar.

   a. Crea o entra con el administrador de diagnóstico y autentícate. Contra Azure usa
      `GHT_DIAG_KEY` para los endpoints de simulación; en Development no hace falta clave.

   b. Genera un identificador único de corrida, por ejemplo `P32-AAAAMMDD-HHMM`, y consulta primero
      que los teléfonos elegidos no existan. Crea tres usuarios activos, con nombres que empiecen por
      ese identificador: uno `es`, uno `en` para la campaña completa y uno `en` reservado para la
      campaña incompleta. Usa teléfonos de prueba nuevos, nunca los de la convención ni los rangos de
      `QAS/datos/`. Anota solo los IDs y últimos cuatro dígitos en el resultado, no números completos.

   c. Busca los recursos **activos** ya existentes y selecciónalos por nombre exacto. No los crees ni
      edites. Deben ser: rúbrica `rúbrica OpenBrain v3.4`, prompt `Evaluación con rubrica OpenBrain
      Thought-Scoring` y configuración LLM `OpenRouter-Terra`. Si falta alguno, está inactivo o aparece
      más de uno con el mismo nombre, detén las pruebas que dependan del LLM como BLOCKED y reporta el
      identificador encontrado; jamás solicites ni manipules la key de OpenRouter.

   d. Ejecuta primero las Pruebas 1 a 8 de `QAS/22`. En el portal entra a **Textos de conversación**
      y revisa primero el panel **Preparación**. Para cada idioma `es` y `en` que no tenga catálogo
      global activo y válido: selecciona el idioma, pulsa **Crear semilla base** y confirma que nace
      como borrador; descarga su **JSON para edición masiva**, modifica contenido de prueba, cárgalo,
      ejecuta la prevalidación sin escritura y confirma **Importar como nuevo borrador**. Revisa el
      borrador resultante y actívalo explícitamente con ETag. Al terminar, vuelve al panel
      **Preparación** y comprueba que ambos idiomas aparecen activos y válidos. Anota versión/huella.
      No uses una fotografía legacy inválida, no sobrescribas una activa y no confundas activar una
      versión editorial con encender el gate del proceso.

   e. Crea una campaña nueva llamada `CAMP-<identificador>-COMPLETA`, con esos tres recursos, una
      pregunta activa, un mensaje inicial y textos/localizaciones completos para `es` y `en` (nombre,
      descripción, objetivo, cierre, mensaje, pregunta e instrucción). Asocia solo los dos usuarios
      principales y actívala. Para pruebas por simulación no envíes WhatsApp real; usa el webhook
      simulado. Revisa en **Preparación** `listoParaGateOn` y los pares `plantillaRef+idioma`. El
      agente no crea ni modifica App Settings: si el operador ya configuró plantillas aprobadas y
      existe autorización explícita, verifica `Nombre`, `Idioma`, orden de `Componentes` y ejecuta
      el lote mixto real; de lo contrario márcalo BLOCKED.
      Los pares requeridos solo por campañas borrador deben seguir visibles, pero con
      `bloqueaGateOn=false`; no deben impedir `listoParaGateOn=true` si catálogos y campañas activas
      están listos. Con el gate ON, comprueba que un borrador no pueda activarse sin sus propios
      mapeos.

   f. Crea una segunda campaña nueva llamada `CAMP-<identificador>-INCOMPLETA`, habilita `es/en` y
      deja deliberadamente vacío el contenido `en`. No la completes ni intentes eludir sus controles:
      úsala exclusivamente para la Prueba 6, que debe demostrar el rechazo al activar y asociar el
      tercer usuario.

7. Ejecuta primero `QAS/23` completo, incluida la revalidación DT-P32-04 y el readiness compuesto, y
   después evidencia las pruebas 0 a 8 de `QAS/16`: snapshot, recorrido completo es/en, menú y
   comandos, lote mixto, edición de borrador, activación, rollback, campaña incompleta, D5 real y UAT.
   Para D5 compara pares equivalentes es/en: idea fuerte, débil, inyección y salida. El modelo puede
   redactar distinto, pero no puede cambiar estados, revelar información protegida ni mezclar idiomas.
8. No marques PASS sin evidencia. Si una precondición falta, usa BLOCKED; si el resultado observado
   contradice el esperado, usa FAIL, describe qué ocurrió y conserva identificadores/capturas/reportes.
   No intentes corregir el sistema durante la ejecución.

   Para repetir un mismo texto de control en el webhook simulado durante el mismo día UTC, envía un
   `whatsappMessageId` explícito y único. La combinación repetida de número, texto y fecha puede ser
   deduplicada con respuesta 200 y no constituye evidencia de una interacción nueva.

Al finalizar crea `QAS/resultados/Resultados_P32_Multidioma_<AAAA-MM-DD>.md` con:

- ambiente, fecha, ejecutor y autorización;
- identificador de corrida, IDs de usuarios/campañas y últimos cuatro dígitos de cada teléfono;
- confirmación de que reutilizaste, sin editar, `rúbrica OpenBrain v3.4`, `Evaluación con rubrica
  OpenBrain Thought-Scoring` y `OpenRouter-Terra`, o el bloqueo concreto si alguno no estaba disponible;
- versiones/huellas de catálogo y plantillas Meta usadas, sin secretos;
- resultado de semilla base, edición masiva JSON, prevalidación y readiness de `QAS/22`;
- resultado de la matriz de cierres, mapeos Meta y readiness compuesto de `QAS/23`;
- tabla `Prueba | es | en | Estado | Evidencia | Observación`;
- resultado del lote mixto, activación/rollback y campaña incompleta;
- reporte D5, costo/tokens/latencia observados y comparación de equivalencia;
- decisión UAT de GHT (aceptado, observaciones, rechazo o pendiente);
- recomendación final: `LISTO PARA ACTA DE ACTIVACIÓN`, `NO ACTIVAR` o `BLOCKED`, con razones;
- confirmación del estado final del gate.

Entrega además un resumen de máximo diez líneas que separe hechos verificados, bloqueos externos y
acciones que requieren decisión humana. No declares listo para producción si D5, UAT, plantillas Meta,
costo/latencia o acta de cambio siguen pendientes.

## ▲ FIN DEL PROMPT ▲
