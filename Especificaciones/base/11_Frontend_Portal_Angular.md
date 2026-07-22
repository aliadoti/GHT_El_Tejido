# 11 — Frontend: Portal Administrativo (Angular 22)

**Proyecto:** `ElTejido.Web/`.
**Implementa:** `REQ §27, §32, §33.1`; `ARQ §3`.
**Depende de:** `04` (contrato API). El portal **solo** consume `/api/*`; no accede a Cosmos/Blob directamente.

---

## 1. Stack y convenciones (de `01 §4.2`, `02 §2`)
- **Angular 22** (última estable, jun-2026): standalone components, signals, `OnPush`, zoneless donde aplique.
- TypeScript estricto. Acceso a API por servicios tipados que reflejan `04`. Sin lógica de negocio en componentes.
- Build de producción → `dist/`, publicado como estático servido por `ElTejido.Api/wwwroot` (`02 §3`).
- En dev: `ng serve` + `proxy.conf.json` apuntando a la API local.

---

## 2. Estructura de la app
```
ElTejido.Web/src/app/
├─ core/            # interceptores (auth/CSRF/errores), guards, servicios singleton, modelos (DTOs de 04)
├─ shared/          # componentes UI reutilizables (tabla, filtros, formularios, badges de estado)
├─ layout/          # shell autenticado (nav lateral marca GHT, header)
├─ features/
│  ├─ auth/         # login OTP (request-code, verify-code)
│  ├─ usuarios/     # CRUD usuarios + tags
│  ├─ campanias/    # CRUD campañas, mensajes iniciales, preguntas, asociación participantes
│  ├─ envios/       # disparo y monitoreo de envíos/reenvíos
│  ├─ rubricas/     # carga/edición/versionado de rúbricas Markdown
│  ├─ prompts/      # edición, versionado y aprobación de prompts
│  ├─ config-llm/   # configuración LLM + API key (write-only, enmascarada)
│  └─ resultados/   # consulta de conversaciones, respuestas, evaluaciones, Markdown
└─ styles/          # tokens de marca GHT (5)
```

---

## 3. Rutas y guards
| Ruta | Componente | Guard |
|---|---|---|
| `/login` | Login OTP | Pública. Redirige a `/` si ya hay sesión. |
| `/` | Dashboard simple (resumen de campañas) | `authGuard` |
| `/usuarios`, `/usuarios/:id` | Usuarios/Tags | `authGuard` |
| `/campanias`, `/campanias/:id` | Campañas (detalle con tabs: datos, mensajes, preguntas, participantes) | `authGuard` |
| `/campanias/:id/envios` | Envíos | `authGuard` (rol admin para disparar) |
| `/rubricas`, `/prompts`, `/config-llm` | Configuración | `authGuard` (rol admin) |
| `/resultados` | Consulta y filtros | `authGuard` (admin o visor) |
| `/resultados/markdown/:id` | Detalle Markdown | `authGuard` |

- `authGuard` verifica sesión vía `GET /api/auth/me`; si `401`, redirige a `/login`.
- Botones de mutación se ocultan/deshabilitan para rol `visor` (la API es la autoridad final).

---

## 4. Servicios y acceso a API (core)
- `AuthService`: `requestCode(numero)`, `verifyCode(numero, codigo)`, `me()`, `logout()`. Guarda el `csrfToken` en memoria (no en localStorage; ver restricción de almacenamiento en `01 §11` y nota abajo).
- `HttpInterceptor`:
  - Adjunta `X-CSRF-Token` a mutaciones.
  - Envía credenciales (cookies) con `withCredentials: true`.
  - Traduce el modelo de error de `04 §3` a notificaciones de UI; en `401` redirige a login.
- Un servicio por feature (`UsuariosService`, `CampaniasService`, `EnviosService`, `RubricasService`, `PromptsService`, `ConfigLlmService`, `ResultadosService`) tipado contra `04`.

> Nota de almacenamiento: el `csrfToken` y el estado de sesión se mantienen en memoria (signals/servicio). La sesión persiste vía cookie `httpOnly` del backend; al recargar, el SPA llama `me()` para rehidratar. No usar `localStorage` para datos de sesión.

---

## 5. Marca GHT (`REQ §32`)
Tokens centralizados en `styles/` (CSS custom properties); **prohibido** hardcodear colores fuera de aquí.
```css
:root {
  --ght-verde: #20431D;        /* verde GHT */
  --ght-verde-claro: #508D5E;  /* verde claro GHT */
  --ght-rojo: #DB2B09;         /* rojo GHT (acento sobrio) */
  --ght-superficie: #F7F8F6;   /* superficie clara/neutra */
  --ght-texto: #1A1A1A;
  /* tipografía: Avenir Next o fallback del sistema (sin exponer archivos de fuente) */
  --ght-font: "Avenir Next", "Segoe UI", system-ui, -apple-system, sans-serif;
}
```
Reglas (`REQ §32.2`): no exponer archivos de fuente; usar fallback del sistema si no hay licencia web; consistencia con material ejecutivo; interfaz sobria, claridad operativa sobre animaciones; metáfora visual de red/tejido/nodos discreta (p. ej. en el login o el header).

---

## 6. Pantallas clave (resumen funcional)

**Login OTP** (`REQ §10`, `§33.1.1–4`): campo de número con **instrucciones de normalización** y ejemplos; botón "Enviar código"; pantalla de ingreso de código; mensajes neutrales (no revelan existencia). Llama `request-code` y `verify-code`.

**Usuarios/Tags** (`REQ §33.1.5–6`): tabla con filtros (rol, estado, área, empresa, tag, búsqueda); alta/edición con validación de número; asignación de área/empresa/tags; activar/inactivar. CRUD de tags. **Carga masiva CSV (`I-08`, `REQ §12/§26.3`):** panel solo-admin en la misma pantalla — sube un `.csv` (columnas `Nombre,WhatsApp,Area,Empresa,Tags`), asocia opcionalmente a una campaña, y muestra el reporte por fila (`creado|actualizado|rechazado`+motivo) sin PII, reusando `POST /api/admin/usuarios/carga-masiva` (`04 §5.1`) sin alterar su contrato.

**Campañas** (`REQ §33.1.7–9`): lista por estado; detalle con tabs (datos, mensajes iniciales, preguntas, participantes); asociación de participantes manual o por filtro con **preview de destinatarios**; cambio de estado. Para I-06, la pestaña Configuración expone `segmentacionIdeas` como checkbox de campaña, apagado por defecto.

**Envíos** (`REQ §33.1.10–11`, `§27.2`): seleccionar campaña/participantes; ver cantidad de destinatarios; botón de envío; tabla de estado por participante (enviado/error/pendiente); reintentar fallidos; reenviar a sin respuesta. Monitorea el `jobId`.

**Rúbricas** (`REQ §33.1.13`): editor/carga de Markdown; vista de criterios/pesos/escala parseados; versiones.

**Prompts** (`REQ §33.1.14`): edición por tipo; versionado; **botón de aprobación humana**; indicador de "no aprobado / no usable".

**Config LLM** (`REQ §33.1.15–16`): proveedor/modelo/endpoint/parámetros; campo de API key **write-only** que muestra `••••1234`; nunca solicita ni muestra la key completa.

**Resultados** (`REQ §33.1.17–21`, `§27.3`): filtros completos (campaña, usuario, número, área, empresa, tag, pregunta, categoría, estado, calificación, fecha, estado envío/respuesta, tema, entidad); ver respuestas, calificaciones, explicaciones y Markdown generado; descarga `.md`; regenerar artefacto.

---

## 7. Accesibilidad y UX
- Formularios con validación inline y mensajes claros.
- Estados de carga/skeletons; manejo visible de errores (toasts).
- Tablas con paginación servidor (`04 §2`).
- Responsive razonable (uso principal en desktop).

---

## 8. Criterios de aceptación (resumen; ver `13`)
- Un admin completa todo el ciclo desde el portal: login OTP → crear usuarios/campaña → asociar → enviar → consultar resultados y Markdown.
- El rol `visor` solo ve (sin botones de mutación).
- La API key nunca se muestra completa.
- La marca GHT se aplica por tokens; sin colores hardcodeados.

*Fin del documento.*
