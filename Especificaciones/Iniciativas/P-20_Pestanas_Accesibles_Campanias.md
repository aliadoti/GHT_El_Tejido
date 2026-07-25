# P-20 — Completar el patrón ARIA de pestañas en campañas

**Estado:** TODO — después de P-19  
**Origen:** `UXA11Y-003` de la auditoría técnica del 2026-07-24.  
**Dependencias:** P-16 finalizada; la estructura de paneles debe estar separada antes de aplicar la semántica de pestañas.

## 1. Propósito

Corregir el detalle de campaña, que declara un `tablist` sin declarar pestañas, relación con paneles ni navegación de teclado. El resultado debe cumplir el patrón ARIA de pestañas sin cambiar las secciones administrativas disponibles.

## 2. Alcance confirmado

La corrección se aplica al selector de secciones del detalle de campaña y a los paneles extraídos por P-16. Incluye rol, estado, identificadores, relación pestaña-panel, foco y teclado. No modifica los permisos de cada sección ni agrega rutas o pestañas nuevas.

## 3. Diseño de implementación

- Cada activador declara `role="tab"`, un `id` único y estable, `aria-selected`, `aria-controls` y `tabindex` con foco móvil: `0` solo en la pestaña activa y `-1` en las demás.
- El contenedor conserva `role="tablist"` y un nombre accesible que describa las secciones de la campaña.
- Cada contenido declara `role="tabpanel"`, `aria-labelledby` de su pestaña y un identificador correspondiente. Los paneles inactivos no quedan disponibles para navegación ni lectores de pantalla.
- Las teclas Flecha izquierda/derecha, Inicio y Fin mueven foco y activan la pestaña correspondiente; Tab sale del tablist hacia el panel activo. Se respetará la dirección de interfaz si se añade soporte RTL en el futuro.
- La selección por mouse, teclado y estado inicial de campaña usan una única fuente de verdad para evitar diferencias entre `aria-selected`, contenido visible y foco.

## 4. Criterios de aceptación y pruebas

- El árbol de accesibilidad muestra `tablist`, pestañas con nombre y un único elemento seleccionado, y `tabpanel` relacionado.
- Con teclado, Flecha izquierda/derecha, Inicio y Fin recorren las pestañas en el orden visual; Tab llega al contenido activo.
- Al cambiar de pestaña, se actualizan de manera consistente contenido visible, `aria-selected`, `tabindex` y relación `aria-controls`/`aria-labelledby`.
- Las pruebas de componente cubren las teclas, el estado inicial, el cambio por click y los identificadores únicos.
- Se verifica manualmente en navegador y con lector de pantalla que el nombre de la pestaña y su estado se anuncian.

## 5. Cómo probarlo

1. Abrir el detalle de una campaña y poner el foco en la primera sección.
2. Usar Flecha derecha, Flecha izquierda, Inicio y Fin; confirmar que cambian foco y contenido juntos.
3. Pulsar Tab desde la pestaña activa y confirmar que se llega a su contenido.
4. Activar una sección con el mouse y volver a revisar su estado con lector de pantalla.
5. Es un fallo si el foco queda en una pestaña distinta del panel mostrado, si un panel no tiene nombre o si las flechas desplazan la página en vez de recorrer las secciones.

## 6. Riesgo y reversión

El riesgo es cambiar una convención de teclado sin conservar el flujo de foco. Las pruebas automatizadas y manuales cubren ambos medios de activación. El cambio se limita al detalle de campañas y es reversible sin afectar datos ni API.
