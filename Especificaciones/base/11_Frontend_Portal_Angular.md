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

**Campañas** (`REQ §33.1.7–9`): lista por estado y acción **"+ Nueva campaña"**; el formulario se muestra solo al solicitarlo. El detalle guía el ciclo como pasos (Configuración, Mensajes iniciales, Preguntas, Participantes), indica qué falta, ofrece acceso contextual a **"Ver envíos"** de la campaña elegida y conserva asociación manual o por filtro con **preview de destinatarios** y cambio de estado. Configuración agrupa Evaluación, Conversación y Seguridad/costo; `segmentacionIdeas` sigue siendo un checkbox por campaña, apagado por defecto. I-18 añade, dentro de Conversación y solo para admin, **“Afinar ideas una por una”** (`coachingSecuencialIdeas`, default OFF) y **“Minutos por idea”** (vacío = hereda, 0 = apagado), con ayuda visible que explica su dependencia de multi-idea y el valor efectivo; el visor solo los lee. P-26 añade en creación y edición **“Permitir nuevas ideas después de finalizar”** (`participacionContinua`, default OFF), separado visualmente del estado de la campaña. La ayuda explica que solo funciona mientras la campaña esté activa, que cada idea nueva conserva historial independiente y que apagarlo deja terminar la idea abierta pero bloquea la siguiente. P-27 añade **“Interpretar solicitudes de parar o avanzar escritas libremente”** (`clasificacionIntencionControl`, default OFF), con ayuda que advierte una llamada LLM adicional y aclara que el servidor conserva la decisión; admin edita y visor solo lee.

**Envíos** (`REQ §33.1.10–11`, `§27.2`): seleccionar campaña/participantes; ver cantidad de destinatarios; botón de envío; tabla de estado por participante (enviado/error/pendiente); reintentar fallidos; reenviar a sin respuesta. Monitorea el `jobId`.

**Rúbricas** (`REQ §33.1.13`): editor/carga de Markdown; vista de criterios/pesos/escala parseados; versiones.

**Prompts** (`REQ §33.1.14`): edición por tipo; versionado; **botón de aprobación humana**; indicador de "no aprobado / no usable".

**Config LLM** (`REQ §33.1.15–16`): proveedor/modelo/endpoint/parámetros; campo de API key **write-only** que muestra `••••1234`; nunca solicita ni muestra la key completa.

**Resultados** (`REQ §33.1.17–21`, `§27.3`): la campaña recordada en la sesión (o la primera disponible) se carga sin exigir una consulta manual. Con I-19, el patrón maestro-detalle presenta **una fila por idea consolidada**, no por aporte: participante, `Madura|Pendiente|Rechazada`, estado del flujo/curaduría y extracto de la versión vigente. Al elegirla se ven calificación, explicación, Markdown canónico y un historial expandible de aportes, versiones, confirmaciones y evaluaciones; descarga `.md` y regeneración siguen solo para admin. Los resultados legacy sin `ideaId` permanecen visibles como históricos. Incluye resumen, leyenda visible de estados y actividad de conversaciones como acceso secundario. Los filtros adicionales requieren soporte de contrato; no se inventan desde esta vista.

---

## 7. Accesibilidad y UX
- Formularios con validación inline y mensajes claros. Cada control tiene un nombre accesible: `label` asociado cuando sea posible, o un nombre ARIA contextual cuando la tabla no permita texto visible; el placeholder nunca es la única etiqueta. Las listas generan identificadores únicos y los selectores de archivo explican el archivo esperado.
- Estados de carga/skeletons; manejo visible y anunciado de errores/confirmaciones. Los errores usan una región asertiva y los éxitos/información una región educada; los mensajes de campo se asocian con `aria-invalid` y `aria-describedby` sin anunciar el mismo contenido dos veces. Este patrón también cubre el login, que puede estar fuera del shell de toasts.
- Los selectores de secciones de campaña siguen el patrón ARIA de pestañas: `tablist` con nombre, activadores `tab`, paneles `tabpanel`, relación `aria-controls`/`aria-labelledby`, foco móvil y teclado Flecha, Inicio y Fin.
- Las pestañas de Campañas se presentan como pasos numerados. Mensajes, Preguntas y Participantes anuncian si están completos o pendientes a partir de los datos ya cargados; esto orienta sin bloquear la navegación. Cuando no hay selección o una lista está vacía, la pantalla explica el siguiente paso.
- Resultados usa una lista maestra con `aria-current` para la idea activa, detalle asociado y leyenda
  visible. El historial identifica sin depender solo del color qué versión fue propuesta, confirmada,
  descartada o quedó pendiente. La ausencia de campañas, resultados o detalle se explica en lenguaje
  humano; carga usa esqueletos y error/confirmación usan las regiones persistentes según su prioridad.
- Tablas con paginación servidor (`04 §2`).
- Responsive razonable (uso principal en desktop).

---

## 8. Criterios de aceptación (resumen; ver `13`)
- Un admin completa todo el ciclo desde el portal: login OTP → crear usuarios/campaña → asociar → enviar → consultar resultados y Markdown.
- El rol `visor` solo ve (sin botones de mutación).
- La API key nunca se muestra completa.
- La marca GHT se aplica por tokens; sin colores hardcodeados.
- Con lector de pantalla y teclado, los controles de envío/tags/CSV anuncian su propósito; login y formularios anuncian error o éxito una sola vez; las pestañas de campaña anuncian nombre y estado y se recorren con teclado.
- I-19 no duplica revisiones en la lista: una idea abre su historial y una madura muestra
  explícitamente `Pendiente de curaduría`, sin acciones de aprobación todavía.
- Un admin puede crear/editar `participacionContinua`; el visor solo la lee, el valor hace round-trip
  por API y su ayuda no confunde continuidad con estado `activa`.
- Un admin puede crear/editar `clasificacionIntencionControl`; el visor solo la lee, el valor hace
  round-trip y la ayuda explica costo adicional, dependencia de ConfigLLM y decisión server-side.

*Fin del documento.*
