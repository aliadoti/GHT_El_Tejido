# Acciones operativas — plantillas Meta bilingües P-32

Estas actividades no requieren código y las ejecuta un operador autorizado en el ambiente de pruebas.

## Antes de configurar

1. Confirmar en Meta el nombre exacto y código de idioma de las plantillas aprobadas para español e inglés.
2. Confirmar el alias lógico usado por la campaña, por ejemplo `inicio_campania`.
3. Revisar el body de cada plantilla y anotar el número, orden y significado de sus variables.

## App Settings por cada idioma

Configurar, sin inventar valores:

```text
WhatsApp__PlantillaEnvioInicial__Mapeos__{alias}__es__Nombre
WhatsApp__PlantillaEnvioInicial__Mapeos__{alias}__es__Idioma
WhatsApp__PlantillaEnvioInicial__Mapeos__{alias}__en__Nombre
WhatsApp__PlantillaEnvioInicial__Mapeos__{alias}__en__Idioma
```

Si el body tiene variables, agregar en orden cero-basado:

```text
WhatsApp__PlantillaEnvioInicial__Mapeos__{alias}__es__Componentes__0
WhatsApp__PlantillaEnvioInicial__Mapeos__{alias}__es__Componentes__1
WhatsApp__PlantillaEnvioInicial__Mapeos__{alias}__en__Componentes__0
WhatsApp__PlantillaEnvioInicial__Mapeos__{alias}__en__Componentes__1
```

Valores disponibles: `nombre`, `area`, `empresa`, `campania`/`campaña` y propiedades dinámicas del
usuario. No agregar `Componentes` si la plantilla no tiene variables de body. La posición debe
coincidir exactamente con Meta; no basta con que existan los mismos nombres.

## Verificación

1. Guardar la configuración y esperar el reinicio de la API.
2. Con el gate OFF, abrir **Textos de conversación → Preparación**.
3. Tras implementar DT-P32-03, comprobar `listoParaGateOn=true` y ambos pares del alias.
4. Comparar manualmente los componentes mostrados con Meta; readiness no verifica la aprobación.
5. Solo entonces abrir la ventana gate ON y ejecutar QAS/23 + QAS/17.
6. Al terminar, volver el gate a OFF salvo acta formal; apagar simulación y retirar la clave diagnóstica.

## Pendientes que no resuelve el código

- aprobación efectiva de las plantillas en Meta;
- contenido/traducción y revisión editorial de las plantillas;
- orden contractual de variables del body;
- autorización de tráfico real, D5, UAT, presupuesto/costo y acta de activación;
- limpieza o conservación de campañas y datos de prueba.
