# P-19 — Anunciar errores y confirmaciones dinámicos

**Estado:** TODO — después de P-18  
**Origen:** `UXA11Y-002` de la auditoría técnica del 2026-07-24.  
**Dependencias:** P-18 para que los errores de formulario puedan asociarse a controles con nombre.

## 1. Propósito

Hacer perceptibles para tecnologías de asistencia los errores, confirmaciones y mensajes de carga que aparecen después de una acción. Un mensaje visible que se inserta o cambia sin región viva puede no ser anunciado, en especial en inicio de sesión y en formularios fuera del contenedor principal de notificaciones.

## 2. Alcance confirmado

Se definirá un patrón reutilizable para estados dinámicos en las pantallas del portal, empezando por `LoginPage` y los formularios administrativos que actualmente muestran `form-error` u otro texto local sin semántica de anuncio.

La iniciativa reutilizará `NotificacionesComponent` donde ya esté disponible y añadirá un componente compartido de estado accesible para los casos locales o previos a la sesión. No cambia los textos de negocio, la validación, las API ni la duración de notificaciones salvo que sea necesaria para el anuncio.

## 3. Diseño de implementación

- Crear un componente compartido, por ejemplo `EstadoAccesibleComponent`, con mensajes tipados `error`, `exito` e `informacion`.
- Los errores usarán una región asertiva (`role="alert"` o `aria-live="assertive"`) y los éxitos/información una región educada (`role="status"` o `aria-live="polite"`), con `aria-atomic="true"`.
- Renderizar la región desde el inicio de la vista y actualizar su contenido, en lugar de crear un contenedor nuevo solo cuando ocurra el error.
- Cuando un error pertenece a un campo, conservar el mensaje visible y asociarlo mediante `aria-invalid` y `aria-describedby`; no se anunciará dos veces el mismo contenido desde regiones superpuestas.
- Mantener el foco donde la persona estaba salvo que la validación impida continuar. Para errores de formulario bloqueantes, llevar el foco al resumen o al primer control inválido mediante una regla consistente y comprobable.

## 4. Criterios de aceptación y pruebas

- Un error de inicio de sesión se anuncia sin depender del contenedor autenticado de notificaciones.
- Un éxito de guardado se anuncia una sola vez y no interrumpe innecesariamente la lectura en curso.
- Los errores de campo señalan el control inválido y permiten leer la explicación asociada.
- Las pruebas verifican rol, `aria-live`, actualización del mensaje y atributos de relación para un caso de error y uno de éxito.
- La prueba manual con NVDA, Narrador u otro lector disponible confirma el anuncio de error y de confirmación en al menos inicio de sesión y un formulario administrativo.

## 5. Cómo probarlo

1. En la pantalla de ingreso, enviar una credencial inválida sin mover el foco.
2. Confirmar que el error se escucha y sigue visible.
3. En un formulario administrativo, provocar una validación y después guardar correctamente un cambio permitido.
4. Confirmar que el error identifica el campo y que el éxito se anuncia una vez.
5. Es un fallo si solo se ve el texto, si se anuncia repetidamente o si el foco desaparece sin explicación.

## 6. Riesgo y reversión

El principal riesgo es el anuncio duplicado. La revisión se hará por flujo completo con lector de pantalla y pruebas de componente. La reutilización de un componente común reduce divergencias y permite revertir su adopción por pantalla si fuera necesario.
