# `I-08 v2` — Cómo probar la carga masiva de participantes

Guía ejecutable para un agente (Codex / Claude Code) o una persona. Cubre `ADM-08`, `ADM-08a`,
`ADM-08b`, `ADM-08c`, `ADM-09` y `ADM-09a` de `02_Casos_de_Prueba_E2E.md`, y `E15`/`E15b` de
`10_Guia_E2E_Ejecutable_Agente_o_Humano.md`.

> **No cargues la lista real de GHT.** Esta guía usa los archivos de `QAS/datos/`, con números que no
> corresponden a personas reales. La carga real es un paso aparte del freeze y espera a que GHT
> entregue el archivo con la columna `Telefono` diligenciada (`I-08 §9`).

---

## 0. Precondiciones

- El contenedor `users` fue creado con **unique key `/claveUnicidad`** y sembrado con el admin
  (`Guia_Azure_Portal §2.1 paso 6`). Sin esto la reasignación **no puede funcionar**: la unique key
  vieja (`/whatsappNormalizado`) rechaza dos documentos con el mismo número.
- Sesión de admin en el portal, o `X-Diag-Key` si vas por API.
- Nada que reiniciar entre casos salvo lo que diga cada paso: la carga es idempotente a propósito.

**Variables para los ejemplos:**

```powershell
$base = "https://app-eltejido-mvp-evd8ffcgd3fthshw.eastus-01.azurewebsites.net"
$datos = "QAS/datos"
```

---

## 1. Lote con datos sucios (ADM-08)

**Archivo:** `QAS/datos/participantes_QA.csv` (8 filas, ver `04_* §5` para el detalle de cada una).

**Portal:** Usuarios → Carga masiva → modo **Crear y actualizar** → subir el archivo.

**Qué debe pasar, fila por fila:**

| Fila | Esperado |
|---|---|
| Ana Pérez | `creado`, antigüedad `16.391666` **sin redondear** |
| Beto Ríos | `creado`; el email pierde el espacio final, `idioma = es` por defecto |
| Carla Díaz | `creado`; el número local `3001112203` se normaliza a E.164 |
| Diego Luna | `rechazado` — el teléfono no es válido |
| Elsa Mora | `creado` — solo `Nombre` y `Telefono` son obligatorios |
| Ana Pérez (2ª) | `rechazado` — teléfono repetido en el archivo; **el primero gana** |
| Fabio Sanz | `rechazado` — ese correo ya lo tiene Ana, que está activa |
| Gina Ruiz | `rechazado` — `Idioma = pt` no está en el catálogo |

**Además:** cada creado recibe un `codigoUsuario` consecutivo; se crea la tag `t_emp_ac`; el lote **no**
se aborta por las filas malas.

**Algo salió mal si:** una fila mala detiene el lote, la antigüedad aparece redondeada, o el reporte o
los logs muestran números de teléfono.

---

## 2. Cabecera inválida aborta el lote (ADM-08a)

Sube un archivo con la cabecera vieja (`Nombre,WhatsApp,Area,Empresa,Tags`) o con las columnas en otro
orden.

**Esperado:** error `400`, **ninguna** fila procesada, el maestro idéntico al estado previo.

La cabecera tolera mayúsculas, espacios de sobra y tildes; **no** tolera cambio de orden ni columnas
faltantes.

---

## 3. Reprocesar no duplica (ADM-09)

Vuelve a subir **el mismo** `participantes_QA.csv`.

**Esperado:** los que antes fueron `creado` ahora son `actualizado`; **cero** usuarios nuevos; el
`codigoUsuario` de cada uno **no cambia**; `usuarioWhatsapp`, `rol`, `estado` y las tags puestas a mano
se conservan. Un campo opcional vacío en el archivo **no borra** el valor que ya estaba.

---

## 4. Modo solo actualizar (ADM-09a)

**Archivo:** `QAS/datos/participantes_QA_solo_actualizar.csv`, modo **Solo actualizar los que ya
existen**.

**Esperado:** Ana → `actualizado` (queda con cargo `Directora` e `idioma = en`); el teléfono
`573009999999` → `rechazado`, con el motivo de que no existe nadie con ese teléfono. **`creados = 0`**,
incluso si hubiera un inactivo con ese número.

---

## 5. Conflicto de titular y reasignación (ADM-08b) — el caso importante

**Archivo:** `QAS/datos/participantes_QA_conflicto.csv`.

### 5.1 Primera pasada

Sube el archivo en modo **Crear y actualizar**.

**Esperado:**
- `ANA PEREZ` frente a `Ana Pérez`: solo cambian tildes y mayúsculas → se trata como un **typo** →
  `actualizado`, **sin** conflicto, conservando `id` y `codigoUsuario`.
- `RODRIGO NUEVO` sobre el teléfono de Beto: nombre claramente distinto → **`rechazado`** con el
  motivo "El teléfono ya es de otra persona", y **nada se escribe** para esa fila.
- Aparece la tabla **"Teléfonos que ya son de otra persona"**, con *quién está registrado* frente a
  *quién trae el archivo* y el código del titular actual.

**Algo salió mal si** la carga inactiva a Beto por su cuenta, o si acepta a Rodrigo en silencio.

### 5.2 Resolver

En esa tabla, elige **"Es otra persona: reasignar el teléfono"** para la fila de Rodrigo, vuelve a
seleccionar **el mismo archivo** y pulsa **Aplicar decisiones y volver a cargar**.

**Esperado:**
- Beto queda **inactivo conservando su número** y todo su historial.
- Rodrigo queda **activo**, con **nuevo `id` y nuevo `codigoUsuario`**, sin heredar rol ni tags.
- El resultado de esa fila es `reasignado`.

### 5.3 Verificar la trazabilidad

Abre **Ver ficha** en Rodrigo: el historial de ese teléfono debe mostrar **a los dos**, ordenados por
fecha, con Beto en `inactivo`.

Si Beto tenía conversaciones o evaluaciones previas, deben seguir colgando de **su** `id` y **no**
aparecer bajo Rodrigo. Eso es lo que hace que la reasignación sea segura.

Por API:

```powershell
Invoke-RestMethod -Uri "$base/api/admin/usuarios/por-numero/573001112202"
```

### 5.4 Las otras dos decisiones

Repite 5.1 y elige las otras opciones para comprobar que hacen lo que dicen:
- **"Es la misma persona: corregir el nombre"** → mismo `id` y mismo `codigoUsuario`, solo cambia el
  nombre.
- **"Dejarla sin cargar"** → la fila sigue rechazada y no se toca nada.

---

## 6. Un solo activo por teléfono (ADM-08c) — el guardarraíl

Tres comprobaciones, la primera solo es concluyente **contra Cosmos real**:

1. **En Data Explorer**, inserta a mano un documento en `users` con `estado: "activo"` y la misma
   `claveUnicidad` que un usuario activo existente (`wa|<número>`). Debe fallar con **`409`**. Si lo
   guarda, la unique key quedó mal y hay que recrear el contenedor.
2. **Reactivar un inactivo** cuyo número ya tiene titular activo (en la ficha, botón *Activar*): debe
   responder `409` con un mensaje que sugiere reasignar en vez de reactivar.
3. **Mensaje entrante desde un número cuyo único registro está inactivo**: no debe resolver
   participante ni atribuirse al titular anterior; cae en el rechazo de participación de siempre.

```powershell
Invoke-RestMethod -Method Post -Uri "$base/diagnostico/simulacion/webhook-entrante" `
  -Headers @{ "X-Diag-Key" = $clave } -ContentType "application/json" `
  -Body '{"numero":"573001112202","texto":"Hola"}'
```

---

## 7. Plantilla y ficha

- **Descargar plantilla vacía** (Usuarios → botón arriba a la derecha): baja un `.xlsx` con las 9
  columnas oficiales, la fila de cabecera en negrita y la columna `Telefono` en formato texto. Debe
  poder subirse tal cual: se acepta y reporta 0 filas.
- **Ver ficha** de cualquier usuario: muestra código, teléfono, empresa, sede, cargo, correo,
  antigüedad, idioma y usuario de WhatsApp, más el historial del número.

---

## 8. Qué NO debe pasar nunca

- Dos usuarios **activos** con el mismo teléfono.
- Que la carga inactive a alguien sin que un admin lo haya decidido explícitamente por esa fila.
- Que un `codigoUsuario` cambie al actualizar, o que se repita entre dos personas.
- Que aparezca un número de teléfono o un correo en los logs o en la auditoría (el reporte al admin sí
  muestra nombres: los necesita para decidir el conflicto).
- Que un lote quede a medias: o la fila se procesó, o quedó reportada con su motivo.
