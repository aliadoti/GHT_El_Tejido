# P-03 · Reinicio de datos (participante/campaña)

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT · — |
| **Estimación** | Por estimar (T&M) |

## Qué se necesita
Herramienta interna para reiniciar datos por participante o por campaña completa, conservando campaña, configuración y usuarios.

## Alcance
- Incluye: reinicio por participante; reinicio por campaña (conserva config/usuarios)
- No incluye: Protegido por `Seguridad:PermitirReinicioDatos`, apagado en producción/freeze. Herramienta interna, no de cara al usuario.

## Criterios de aceptación
- [x] Reinicia conversaciones/respuestas/Markdown sin borrar la campaña ni los usuarios.
- [x] La capacidad se apaga por flag en el freeze.

## Aprobación / Referencia
Estado de desarrollo: DONE 2026-07-13/14; flag se apaga en el freeze
Spec detallada: `Especificaciones/Iniciativas/P-03_Reiniciar_Conversacion.md`
