# P-16 — Descomponer la página administrativa de campañas

**Estado:** TODO — después de P-15  
**Origen:** `CAL-002` de la auditoría técnica del 2026-07-24.  
**Dependencias:** P-15 finalizada para mantener una sola iniciativa activa; P-20 depende de esta descomposición.

## 1. Propósito

Separar los cinco flujos administrativos que hoy conviven en `CampaniasPage` y en su plantilla en línea. La finalidad es que cada flujo pueda evolucionar y probarse sin tocar el resto, conservando la ruta, permisos y comportamiento actual del portal.

## 2. Alcance confirmado

Se divide `src/ElTejido.Web/src/app/features/campanias/campanias.page.ts` y su plantilla asociada en componentes de presentación y formularios con responsabilidades claras:

1. listado y selección de campañas;
2. alta y edición de campaña;
3. mensajes iniciales;
4. preguntas y su orden;
5. participantes, vista previa y reinicio de conversación;
6. configuración propia de la campaña y coordinación del detalle.

El componente contenedor conservará la carga inicial, la campaña seleccionada, la coordinación de refrescos y los límites de acceso. Los hijos recibirán datos tipados mediante `input` y emitirán acciones mediante `output`; no podrán cambiar permisos ni llamar a contratos distintos a los vigentes.

Quedan fuera los cambios visuales de accesibilidad de pestañas, que son responsabilidad específica de P-20, y cualquier cambio de DTO, URL o regla de negocio.

## 3. Diseño de implementación

- Crear componentes standalone dentro de `features/campanias/`, con nombres que describan el flujo, por ejemplo `CampaniasListaPanel`, `CampaniaFormulario`, `MensajesInicialesPanel`, `PreguntasPanel` y `ParticipantesCampaniaPanel`.
- Extraer primero secciones puramente visuales; después mover el estado local que les pertenece. El contenedor seguirá siendo la única fuente de verdad de la campaña seleccionada.
- Declarar interfaces de entrada y eventos para cada panel. Los eventos usarán intención de negocio (`guardar`, `eliminar`, `reiniciar`, `refrescar`) y no expondrán referencias mutables del formulario padre.
- Mantener los mismos servicios, rutas, permisos de administrador y mensajes actuales. Si un componente hijo necesita una llamada propia, se inyectará el servicio existente sin duplicar el cliente HTTP.
- Extraer las pestañas y los paneles con identificadores estables para que P-20 pueda completar su semántica ARIA sobre una estructura ya separada.
- No se publicará la extracción hasta que todas las secciones se rendericen en el detalle de campaña con los mismos datos y acciones actuales.

## 4. Contratos y compatibilidad

| Superficie | Regla de compatibilidad |
|---|---|
| Ruta y navegación | Se conserva `/campanias` y la campaña seleccionada al interactuar. |
| APIs y DTO | Sin cambios de URL, cuerpo ni versión. |
| Autorización | Las operaciones de administración permanecen restringidas como hoy. |
| Formularios | Se preservan validaciones, confirmaciones y recargas existentes. |
| Pestañas | Se mantiene el contenido actual; P-20 completará el patrón ARIA y teclado. |

## 5. Criterios de aceptación y pruebas

- Cada uno de los seis flujos se encuentra en un componente o panel con una responsabilidad única y una API de entradas/salidas explícita.
- Un administrador puede crear/editar una campaña, administrar mensajes iniciales y preguntas, gestionar participantes, previsualizar, reiniciar y ajustar configuración igual que antes.
- No hay solicitudes HTTP duplicadas al seleccionar, guardar o refrescar una campaña.
- Las pruebas de componentes cubren al menos la emisión de acciones, el refresco tras éxito y la preservación de la campaña seleccionada.
- El frontend compila con el Node temporal requerido en el equipo y con los ejecutables locales de Angular/TypeScript cuando los envoltorios de npm no acepten argumentos.

## 6. Cómo probarlo

1. Entrar al portal como administrador y abrir **Campañas**.
2. Elegir una campaña y recorrer cada sección: datos, mensajes iniciales, preguntas, participantes/vista previa/reinicio y configuración.
3. Guardar un cambio no sensible en cada flujo y confirmar que solo se actualiza la información correspondiente.
4. Es un fallo si desaparece una sección, se pierde la campaña elegida, se habilita una acción a un rol no autorizado o se envía la misma operación dos veces.

## 7. Riesgo, reversión y siguiente paso

El riesgo es fragmentar estado compartido y producir refrescos inconsistentes. La extracción gradual, los eventos explícitos y el contenedor como fuente de verdad permiten revertir un panel sin afectar los demás. P-20 se ejecutará después para corregir el patrón de pestañas sobre esta estructura.
