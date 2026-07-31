# REQ-001 · Autenticación de administrador por OTP de WhatsApp

| | |
|---|---|
| **Tipo** | Nuevo |
| **Prioridad** | Alta |
| **Estado** | Entregado |
| **Solicitado por / Fecha** | GHT (alcance base MVP) · 2026-06-12 |
| **Estimación** | Alcance base del MVP (no facturable por T&M) |

## Qué se necesita
Login del portal admin sin contraseñas: el administrador ingresa su número y recibe un código OTP por WhatsApp para acceder.

## Alcance
- Incluye: normalización de número, generación/validación de OTP, sesión con rol admin, mensajes neutrales ante código inválido o vencido.
- No incluye: Entra ID / SSO (post-MVP).

## Criterios de aceptación
- [x] El login muestra instrucciones de normalización del número.
- [x] El admin recibe el código por WhatsApp y accede con uno válido.
- [x] Un código inválido o vencido se rechaza con mensaje neutral.

## Aprobación
Entregado como parte del MVP base · Ref. spec: `Especificaciones/base/06`, `04 §4`
