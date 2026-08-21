# Datos de prueba de carga masiva (`I-08 v2`)

## Catálogos de conversación — evidencia DT-P33-01

| Archivo | Estado de origen | Uso |
|---|---|---|
| `catalogo-catalogo_conversacion-en-v2-editable.json` | Descargado de inglés v2 activo; editado, importado y activado en portal como v3 | Contiene los siete alias de conformidad del hotfix en `continuar`, `confirmar` y `acuseConsultaIdea`. La copia local conserva `metadatos.version=2` porque los metadatos de descarga son informativos y el importador crea siempre una versión nueva. |
| `catalogo-catalogo_conversacion-es-v3-editable.json` | Descargado de español v3 activo, sin modificaciones | Double check de las 29 claves de mensajes y 16 grupos de frases; no se debe reimportar para este hotfix. |

El inglés v3 remoto quedó activo después de desplegar `v1.0.3-convencion`. Su validación dirigida forma
parte de la corrida integral de `QAS/25` al terminar el fix completo.

---

Archivos listos para ejecutar `ADM-08`, `ADM-08a`, `ADM-08b`, `ADM-08c`, `ADM-09` y `ADM-09a`
(`02_Casos_de_Prueba_E2E.md`). El detalle de qué debe pasar con cada fila está en
`04_Datos_de_Prueba_y_Reinicio.md §5`.

| Archivo | Para qué | Caso |
|---|---|---|
| `participantes_QA.csv` | Lote con casos sucios a propósito: válidos, número inválido, opcionales vacíos, duplicado interno, email repetido, idioma fuera de catálogo | ADM-08, ADM-09 |
| `participantes_QA_conflicto.csv` | Mismo teléfono con nombre distinto (cambio de titular) y con typo (misma persona) | ADM-08b |
| `participantes_QA_solo_actualizar.csv` | Un teléfono existente y uno inexistente | ADM-09a |

## Sobre el `.xlsx`

Los casos de QAS mencionan `participantes_QA.xlsx`. **No se versiona el binario**: se obtiene en un
minuto y evita que un archivo opaco se desincronice de la cabecera oficial.

Dos formas de conseguirlo, cualquiera sirve:

1. **Desde el portal** (recomendado, valida el camino real): Usuarios → **Descargar plantilla vacía**
   → pega debajo las filas del `.csv` correspondiente → guarda como `.xlsx`.
2. Abre el `.csv` en Excel y usa *Guardar como → Libro de Excel*. Si lo haces así, deja la columna
   `Telefono` con formato **Texto** antes de guardar, o Excel se come los ceros a la izquierda y
   convierte los números largos a notación científica.

**Por qué el `.csv` basta para la mayoría de los casos:** los dos lectores comparten la definición de
columnas y las conversiones (`PlantillaParticipantes`), y hay una prueba unitaria que carga el mismo
contenido por ambos caminos y exige que produzcan **filas idénticas**
(`LectorXlsxParticipantesTests.Leer_ProduceLasMismasFilasQueElLectorCsv`). El camino `.xlsx` en sí
—celdas numéricas, formato de presentación que redondea a la vista, archivo corrupto— está cubierto
por sus propias pruebas unitarias y por una de integración que sube un `.xlsx` real al endpoint.

Lo que **sí** conviene probar con un `.xlsx` de verdad en el entorno desplegado es el recorrido
completo del admin (ADM-08 en Azure): descargar la plantilla, diligenciarla en Excel y subirla, porque
ahí es donde aparecen los problemas de formato de celda que un `.csv` nunca reproduce.

## Números usados

Todos los teléfonos de prueba están en el rango `5730011122xx` y `573009999999`, que **no**
corresponden a personas reales. No metas números reales en estos archivos: el reporte y los logs no
llevan PII, pero el maestro sí guardaría el número.
