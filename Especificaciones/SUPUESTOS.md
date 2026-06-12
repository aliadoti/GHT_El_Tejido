# SUPUESTOS - El Tejido MVP

> Registro de decisiones tomadas ante ambiguedad de las specs (ver `01_Convenciones_para_Agentes.md` seccion 9).
> Cada vez que una spec no resuelva un caso y debas decidir, anade una entrada aqui en lugar de dejar la decision oculta en el codigo.

## Formato de cada entrada

```markdown
### <id corto> - <titulo>
- Fecha: <YYYY-MM-DD> - Agente/Rol: <Codex/Claude Code/opencode - rol> - Commit: <hash o PR>
- Contexto: <que spec/caso no estaba definido> - REQ/ARQ: <referencias>
- Decision: <que se eligio>
- Alternativa(s) descartada(s): <que y por que>
- Impacto / reversibilidad: <a que afecta, si cierra o no fronteras post-MVP>
```

---

## Supuestos registrados

### fase0-frontend-lint - Lint inicial del portal Angular
- Fecha: 2026-06-12 - Agente/Rol: Codex - SDET/Frontend - Commit: n/a (sin repositorio Git)
- Contexto: Angular CLI 22 no genero un target `lint`/ESLint por defecto, pero `12_CICD_GitHub_Actions.md` seccion 3.1 exige `npm run lint`.
- Decision: definir `npm run lint` como `prettier --check "src/**/*.{ts,html,scss}"` durante Fase 0.
- Alternativa(s) descartada(s): agregar ESLint en Fase 0 sin reglas de portal reales; aumenta dependencias y no aporta validacion funcional todavia.
- Impacto / reversibilidad: afecta solo el scaffold frontend; es reversible agregando ESLint y cambiando el script sin romper contratos API o de datos.

### fase1-normalizacion-e164 - Validacion plausible de prefijo E.164
- Fecha: 2026-06-12 - Agente/Rol: Codex - Backend/AppSec - Commit: n/a (sin repositorio Git)
- Contexto: `06_Backend_Identidad_y_Autenticacion.md` seccion 2 exige rechazar si el resultado no es E.164 plausible por longitud y prefijo de pais valido, pero las specs no incluyen un catalogo de prefijos permitido. REQ 10.2, REQ 12.2.2 / ARQ 16.
- Decision: validar formato E.164 plausible sin simbolos: solo digitos ASCII, longitud 8-15 y primer digito distinto de 0. Se eliminan simbolos comunes antes de validar.
- Alternativa(s) descartada(s): incluir un catalogo completo de prefijos de pais en dominio; aumenta mantenimiento y puede bloquear numeros validos si queda incompleto.
- Impacto / reversibilidad: afecta solo el value object de dominio; se puede endurecer luego agregando un catalogo/configuracion de prefijos sin cambiar contratos de API o Cosmos.

### fase1-puertos-persistencia-application - Ubicacion de puertos de repositorio
- Fecha: 2026-06-12 - Agente/Rol: Codex - Arquitecto/Backend - Commit: a36bd2f
- Contexto: `01_Convenciones_para_Agentes.md` seccion 2 menciona interfaces en Domain para algunos modulos, mientras `02_Arquitectura_y_Stack.md` seccion 3 define que Application expone puertos e Infrastructure los implementa. REQ 31.8 / ARQ 1.1, 8, 9.
- Decision: ubicar los puertos de persistencia en `ElTejido.Application`, manteniendo `ElTejido.Domain` como capa pura de entidades/value objects sin contratos de I/O.
- Alternativa(s) descartada(s): poner repositorios en `ElTejido.Domain`; acopla el dominio a necesidades de aplicacion/persistencia y complica mantenerlo como nucleo puro.
- Impacto / reversibilidad: afecta solo la organizacion de interfaces internas; no cambia contratos API ni Cosmos y permite mover interfaces despues si el equipo estandariza otra frontera.
