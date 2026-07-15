# Banco de calibración de prompts — D5 (runbook)

Convierte la calibración del LLM en **datos comparables versión-a-versión** antes de tocar
prompts / rúbrica / umbral (I-01/I-03/I-04/I-05). Ver spec `Especificaciones/Iniciativas/D5_Banco_Calibracion.md`.

## Piezas

- **Golden set** (dato versionado): [`golden-set.json`](golden-set.json). 20–30 respuestas
  anonimizadas + sintéticas con los casos límite (multi-idea, texto corto, hostil con instrucciones
  embebidas, "no quiero seguir", respuesta fuerte, vacío/ruido). **Sin PII.** Se amplía sin tocar código.
- **Lógica pura** (proyecto `src/ElTejido.Calibracion`, probada **mockeada y verde en CI**):
  - `CargadorGoldenSet` — carga + valida el set.
  - `CorredorCalibracion` + `AgregadorCalibracion` — corren el set N veces y agregan: distribución de
    scores por eje y total (min/max/media/desv), decisión cerrar vs repreguntar, **% de salida inválida**
    (tasa de fallback con motivo), ideas por entrada y **tokens/costo** (metering de P-10).
  - `EscritorReporteJson` / `EscritorReporteMarkdown` — reporte determinista (JSON + MD legible).
  - `ComparadorRegresion` — compara un corrido contra el **baseline** congelado y marca deltas que
    excedan `Tolerancias` (Δmedia por eje/total, Δ% inválido, Δproporción de decisión).
- **Runner opt-in** (test `[Trait("Category","Calibracion")]` en `ElTejido.IntegrationTests`): llama al
  **LLM real** de staging. **Cuesta dinero → EXCLUIDO del CI** (`--filter "Category!=Calibracion"`) y
  además **no-op** si faltan las variables de entorno.

## Cómo dispararlo contra staging

1. Copia la plantilla y ajústala a la campaña de staging (rúbrica + prompt + ConfigLLM versionados):

   ```bash
   cp tests/Calibracion/staging-triplet.example.json /ruta/segura/staging-triplet.json
   # edita proveedor/endpoint/modelo/rúbrica/prompt/N/precio
   ```

2. Exporta la configuración. **La API key va solo por variable de entorno** (equivalente local de
   Key Vault); nunca en el repo ni en el JSON:

   ```bash
   export CALIBRACION_CONFIG=/ruta/segura/staging-triplet.json
   export CALIBRACION_API_KEY=****          # secreto; no se persiste en disco
   # opcional: export CALIBRACION_OUT=/ruta/salida
   ```

3. Corre solo el banco (fuera del `dotnet test` normal):

   ```bash
   dotnet test tests/ElTejido.IntegrationTests -c Release --filter "Category=Calibracion"
   ```

   Escribe `reporte-<timestamp>.json` y `.md` en `tests/Calibracion/salida/` (gitignoreado).

## Congelar / actualizar el baseline

```bash
cp tests/Calibracion/salida/reporte-<timestamp>.json tests/Calibracion/baseline.json
git add tests/Calibracion/baseline.json   # el baseline SÍ se versiona
```

Con `baseline.json` presente, cada corrido lo compara y **falla si hay regresión** sobre las
tolerancias. Al cambiar prompt/rúbrica/umbral a propósito, corre el banco, revisa el reporte y
recongela el baseline en un commit aparte.

## Seguridad

- El texto de cada entrada es **dato no confiable** (incluye un caso de prompt-injection): las
  salvaguardas son deterministas y server-side (R-01). El reporte no contiene secretos ni PII.
- La API key solo vive en Key Vault / variable de entorno; nunca en el golden set, el config, los
  reportes ni los logs.
