# 17 — Prompt para ejecutar la validación completa de P-32

> Copia el bloque entre INICIO y FIN en el agente que ejecutará la prueba. Sirve para Codex, Claude
> Code u otro agente con acceso autorizado al ambiente de pruebas. No presupone acceso a Azure ni
> autoriza cambios remotos.

## Antes de enviarlo

El responsable humano debe confirmar: ambiente aislado, URL, acceso administrativo, participantes de
prueba `es/en`, campaña bilingüe completa, plantillas Meta inglesas aprobadas, presupuesto D5 y la
ventana autorizada para encender temporalmente el catálogo. Si algo falta, el ejecutor debe dejarlo
como BLOCKED.

## ▼ INICIO DEL PROMPT ▼

Actúa como SDET/QA senior para El Tejido. Ejecuta y documenta la **validación completa de P-32:
conversación español/inglés y catálogo de textos** en el ambiente de pruebas autorizado.

Primero lee `QAS/16_P32_Multidioma_Catalogo_Textos_Como_Probar.md`,
`Especificaciones/Iniciativas/P-32_Conversacion_Multidioma_y_Catalogo_Textos.md` §§10, 12, 14 y 15,
`tests/Calibracion/README.md` y `QAS/06_Criterios_Aceptacion_LLM.md`.

Reglas obligatorias:

1. Antes de hacer nada, informa el ambiente, la autorización disponible, los datos de prueba que usarás
   y un plan corto. Si no hay autorización explícita para activar temporalmente el catálogo o para D5
   real, no hagas ese cambio: marca el caso BLOCKED y continúa solo con lo permitido.
2. No hagas push, despliegue, cambio de secretos, modificación de rúbricas/prompts ni carga de datos
   reales. No uses el App Secret de Meta. No inventes URLs, credenciales, plantillas, traducciones ni
   resultados.
3. Ejecuta primero la regresión con `Conversacion:CatalogoTextosHabilitado=false`. Luego, solo si la
   ventana ya fue preparada por un humano autorizado, valida con el gate temporalmente ON. Al terminar,
   confirma que quedó OFF, salvo que exista una aprobación formal de activación productiva.
4. Ejecuta y evidencia las pruebas 0 a 8 de `QAS/16`: snapshot, recorrido completo es/en, menú y
   comandos, lote mixto, edición de borrador, activación, rollback, campaña incompleta, D5 real y UAT.
   Para D5 compara pares equivalentes es/en: idea fuerte, débil, inyección y salida. El modelo puede
   redactar distinto, pero no puede cambiar estados, revelar información protegida ni mezclar idiomas.
5. No marques PASS sin evidencia. Si una precondición falta, usa BLOCKED; si el resultado observado
   contradice el esperado, usa FAIL, describe qué ocurrió y conserva identificadores/capturas/reportes.
   No intentes corregir el sistema durante la ejecución.

Al finalizar crea `QAS/resultados/Resultados_P32_Multidioma_<AAAA-MM-DD>.md` con:

- ambiente, fecha, ejecutor y autorización;
- versiones/huellas de catálogo y plantillas Meta usadas, sin secretos;
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
