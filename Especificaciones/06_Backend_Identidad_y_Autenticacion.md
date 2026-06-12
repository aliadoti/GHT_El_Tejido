# 06 — Backend: Identidad, Matrícula y Autenticación Admin

**Módulos:** `Application/Identidad/` y `Application/Auth/`.
**Implementa:** `REQ §10, §12, §26.3`; `ARQ §5`.
**Depende de:** `03` (Usuario, ParticipanteCampania, CodigoAuthAdmin, LogSeguridad), `05` (Gateway para enviar OTP), `04 §4` (endpoints auth), `10` (rate limiting, hashing).

---

## 1. Responsabilidades

- **Identidad / matrícula:** resolver número → participante y validar matrícula, actividad y pertenencia a campaña activa antes de procesar cualquier respuesta entrante.
- **Autenticación admin:** login sin contraseña mediante **OTP de un solo uso, con expiración, entregado por WhatsApp**; emisión y validación de sesiones con rol.

---

## 2. Normalización de números (compartida)

Servicio `INormalizadorNumero` reutilizado por identidad, auth y alta de usuarios.

Reglas (`REQ §10.2`, `§12.2.2`):
- Salida en **E.164 sin símbolos**: solo dígitos, sin `+`, espacios, guiones ni paréntesis (p. ej. `573001112233`).
- Entrada esperada: número en formato internacional. Se eliminan caracteres no numéricos; se rechaza (`400 VALIDATION_ERROR`) si el resultado no es un E.164 plausible (longitud y prefijo de país válidos).
- La normalización es **centralizada y única** (`ARQ §16`): toda comparación (login, resolución de participante, unicidad) usa el mismo método.
- La pantalla de login muestra instrucciones y ejemplos (Colombia `573001112233`, EE. UU. `13055551234`) (`REQ §10.2`).

---

## 3. Identidad y matrícula

### 3.1 Puerto

```csharp
public interface IResolutorParticipante
{
    // Devuelve el participante autorizado para una campaña activa, o un rechazo tipado.
    Task<ResultadoResolucion> ResolverAsync(string numeroCrudo, CancellationToken ct);
}
```
`ResultadoResolucion`: `Autorizado(ParticipanteResuelto)` | `NoAutorizado(MotivoRechazo)`.
`ParticipanteResuelto`: `{ Usuario usuario; Campania campania; Pregunta preguntaVigente; }`.

### 3.2 Algoritmo (`REQ §26.3`)
```
1. Normaliza el número.
2. Busca Usuario por whatsappNormalizado (contenedor users).
   - Si no existe → NoAutorizado(NoMatriculado).
3. Verifica usuario.estado == activo → si no, NoAutorizado(Inactivo).
4. Verifica rol == participante (los admin/visor no participan por WhatsApp).
5. Resuelve la campaña activa asociada:
   - Busca ParticipanteCampania por whatsappNormalizado/usuarioId con campaña en estado 'activa'.
   - Si no hay campaña activa asociada → NoAutorizado(SinCampaniaActiva).
6. Determina la pregunta vigente del hilo (la de la conversación abierta, o la primera pendiente).
7. Devuelve Autorizado(participante, campania, pregunta).
```
> Para el MVP se asume **una** campaña activa por participante a la vez. Si hubiera varias, elegir la conversación abierta más reciente; documentar supuesto si se requiere desambiguación adicional (`01 §9`).

### 3.3 Rechazo controlado y neutral (`REQ §26.3.6`, `§10.3.10`)
Cuando el resultado es `NoAutorizado`, el sistema responde por WhatsApp un mensaje **neutro** que **no revela** si el número existe ni el motivo exacto:
> "Este número no está habilitado para esta actividad."

Se registra un `LogSeguridad` (`tipoEvento=rechazoParticipacion`, `resultado=rechazado`, motivo en `detalle` interno) sin exponer el motivo al usuario.

---

## 4. Autenticación admin por OTP de WhatsApp

### 4.1 Puertos

```csharp
public interface IAuthAdminService
{
    Task SolicitarCodigoAsync(string numeroCrudo, CancellationToken ct);      // siempre "exitoso" de cara al cliente
    Task<SesionEmitida?> VerificarCodigoAsync(string numeroCrudo, string codigo, CancellationToken ct);
}
```

### 4.2 Solicitud de código — `POST /api/auth/request-code` (`ARQ §5 paso 1`)
```
1. Normaliza el número.
2. SIEMPRE responde al cliente con el mensaje neutro (no revela existencia) — REQ §10.3.10.
3. Busca Usuario admin (rol ∈ {admin, visor con acceso}) activo con ese número.
   - Si no existe / no es admin → termina silenciosamente (ya respondió neutro). Registra LogSeguridad(solicitudOtp, resultado=ignorado).
4. Si existe admin válido:
   a. Verifica límite de solicitudes por número/ventana (REQ §10.3.7). Si excede → registra rateLimit y termina (sigue respondiendo neutro).
   b. Genera código numérico de 6 dígitos con CSPRNG (RandomNumberGenerator).
   c. Calcula hash Argon2id (o bcrypt) + sal (sal global desde Key Vault: otp-salt).
   d. Crea CodigoAuthAdmin { hashCodigo, expiracion = now+5min, intentosRestantes = 5, usado=false, ttl }.
   e. Envía el código por WhatsApp (Gateway, plantilla de Autenticación, tipo=Autenticacion).
   f. Registra LogSeguridad(solicitudOtp, resultado=enviado).
```
Parámetros configurables (sección `Auth` de `02 §6`): TTL del OTP (default 5 min), longitud (6), intentos (5), límite de solicitudes por número/ventana.

### 4.3 Verificación — `POST /api/auth/verify-code` (`ARQ §5 paso 2`)
```
1. Normaliza el número; busca el CodigoAuthAdmin vigente más reciente del usuario admin.
2. Si no hay código / expirado / usado → 401 (mensaje neutro). Registra loginFallido.
3. Verifica intentosRestantes > 0; si 0 → invalida (usado=true) y 429/401. Registra.
4. Compara hash(codigoIngresado) con hashCodigo:
   - Inválido → intentosRestantes--, persiste, 401 (neutro), LogSeguridad(loginFallido).
   - Válido y no expirado y no usado:
       a. Marca usado=true.
       b. Emite sesión: cookie httpOnly/Secure/SameSite=Strict + CSRF token.
          - Implementación: token de sesión firmado (JWT corto, clave de firma jwt-sign de Key Vault)
            o registro de sesión en Cosmos; incluye usuarioId, rol, expiración (default 60 min).
       c. Registra LogSeguridad(loginExitoso).
       d. Devuelve 200 con { usuario, csrfToken, expiraEn } (04 §4.2).
```

### 4.4 Sesiones y autorización
- Cookie de sesión `httpOnly`, `Secure`, `SameSite=Strict`, expiración configurable.
- Middleware de autorización: toda `/api/admin/*` exige sesión válida; mutaciones exigen rol `admin` y header `X-CSRF-Token` correcto; GET admiten `admin` o `visor`.
- `GET /api/auth/me` restaura el estado del SPA; `POST /api/auth/logout` invalida la sesión.
- Los participantes **nunca** acceden al portal (`REQ §8.1`, `§27.4`).

### 4.5 Registro de seguridad (`ARQ §5`, `REQ §10.3.9`)
Cada solicitud y verificación (éxito/fallo) genera un `LogSeguridad` con número normalizado, resultado y timestamp, **sin** almacenar el código en claro ni el hash en el log.

---

## 5. Reglas de seguridad específicas (checklist)
- Código OTP: un solo uso, expira, límite de intentos y de solicitudes (`REQ §10.3.4–7`).
- Hash con sal, nunca texto plano (`REQ §10.3.8`).
- Respuestas neutrales que no revelan existencia de números (`REQ §10.3.10`).
- Solo roles administrativos reciben código (`REQ §10.3.3`).
- Comparación siempre contra número normalizado (`REQ §10.3.2`).

---

## 6. Criterios de aceptación del módulo (resumen; ver `13`)
- Un admin existente recibe el código por WhatsApp e inicia sesión con un código válido.
- Un código vencido/usado/erróneo es rechazado con mensaje neutral.
- Un número no registrado recibe la misma respuesta neutral (no se revela inexistencia).
- Un participante matriculado/activo/asociado es resuelto y autorizado.
- Un no matriculado, inactivo o sin campaña activa recibe rechazo neutral y se registra el evento.
- Tras N intentos/solicitudes excedidos, se aplica el límite y se registra.

*Fin del documento.*
