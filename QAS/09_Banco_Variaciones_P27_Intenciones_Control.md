# Banco de variaciones P-27 — Intenciones de control

> Uso: antes de activar P-27 en una campaña de prueba. Ejecutar cada frase durante una mejora activa,
> con ambos opt-ins activos. La etiqueta esperada es una propuesta de prueba; el servidor conserva la
> decisión y el fallback seguro.

| Grupo | Frase | Resultado esperado |
|---|---|---|
| Español, salida de idea | `quiero parar aquí` | Finaliza solo la idea activa; no se evalúa la frase. |
| Español, salida de participación | `no quiero continuar` | Finaliza la participación actual; no abre otra pregunta. |
| Inglés, salida de participación | `I think I should stop for today` | `finalizarParticipacion` o aclaración 1/2/3; nunca contenido evaluado. |
| Inglés, contenido | `we need to stop the conveyor for maintenance` | `aportar`; continúa el coaching. |
| Mixto, salida incierta | `ya no sé si seguir / I need a break` | Aclaración 1/2/3 o salida validada; no cierra campaña. |
| Mixto, cambio de contenido | `paremos el proceso manual y usemos scanner` | `aportar`; continúa el coaching. |
| Falso positivo | `stop losses reduce risk in the proposal` | `aportar`; no se clasifica el primer aporte. |
| Prioridad | `no lo guardes` | Rechazo de guardado vigente; P-27 no interviene. |
| Límite | Texto de más de 160 caracteres con “parar” | No invoca clasificador; sigue el flujo seguro. |
| Cupo agotado | Frase libre con cupo/presupuesto agotado | `omitida` en telemetría, sin llamada LLM ni cierre decidido por modelo. |

Para cada fila registrar: fecha, campaña de prueba, flags usados, resultado observado, si apareció
aclaración, y si hubo fallback. Nunca copiar el número de teléfono ni el texto completo del participante
en telemetría técnica.
