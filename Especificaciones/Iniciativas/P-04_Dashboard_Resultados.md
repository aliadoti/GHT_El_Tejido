# P-04 — Dashboard/analítica de resultados (rama deseable / post-Hito)

> **Origen:** hoja `Iniciativas`. **Tipo:** Desarrollo · **Prioridad:** Media-Alta ·
> **Ventana:** rama deseable / post-convención · **Dependencia:** I-06, I-09.
> No bloquea el 10-ago; se desarrolla en branch sin tocar producción.

## 1. Alcance
Sobre la pantalla de Resultados actual: **ranking de ideas por score**, filtros por tag/eje de
rúbrica, **cobertura de seed thoughts** (qué ejes recibieron aportes) y **exportación** (CSV).

## 2. Diseño (borrador para refinar al retomar)
- Backend: extender las consultas de `04 §5.8` con los filtros hoy diferidos (tag, calificación
  min/max, fecha; ver `SUPUESTOS.md#fase8-consultas-resultados`) — aditivo, dentro de la partición
  `campaniaId`. Endpoint de export CSV con el guard admin.
- Frontend: vista de ranking (ordenar por `calificacionTotal`), filtros combinables, tarjetas por
  eje de rúbrica. Reutiliza `AdminApiService` y los patrones de tabs/toasts existentes.
- La "cobertura de seed thoughts" requiere que I-12 esté en producción (mapear temas de
  `Evaluacion.Temas` contra los ejes del material semilla).

## 3. Aceptación (al retomar)
Ranking/filtros/export funcionan acotados por campaña; specs `04 §2/§5.8` actualizadas en commit
aparte; sin PII fuera del guard admin.
