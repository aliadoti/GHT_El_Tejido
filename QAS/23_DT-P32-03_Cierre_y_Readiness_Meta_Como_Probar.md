# QAS 23 — DT-P32-03/03-01: cierre localizado y readiness Meta

> **Estado (2026-08-15):** los dos cortes de DT-P32-03 están **desplegados** (`a9f4a6f`; CI y Deploy
> en success). El smoke registró PASS en pruebas 1–4 y 6, y BLOCKED en la 5: el defecto de cierre está
> cerrado, pero los borradores incompletos bloquean indebidamente `listoParaGateOn`. El microajuste
> DT-P32-03-01 está **implementado 1/1 en local (2026-08-15) y pendiente de despliegue autorizado**;
> apenas se despliegue se repiten únicamente las pruebas 4–6. Hasta entonces, el smoke permanece
> NO GREEN.
>
> El reporte confirmó el gate OFF al terminar. Las pruebas 1 a 3 exigían encenderlo solo en el
> ambiente autorizado y ya pasaron; no deben repetirse para este microajuste. Readiness de pruebas 4
> y 5 no depende del gate. La guarda de activación añadida a la prueba 6 sí requiere una ventana ON
> controlada y retorno a OFF. Las pruebas 1 a 3 tienen su
> equivalente automatizado en la suite backend (matriz por ruta con gate OFF/ON, hilo `es`/`en` y
> localización ausente, más una prueba que impide nuevas lecturas directas del cierre). Las pruebas 4
> a 7 tienen el agregado de mapeos, `listoParaGateOn` y el panel **Preparación**
> existen, con unitarias del validador, integración de `GET /catalogos-textos/readiness` y pruebas del
> portal. La ejecución manual sigue siendo obligatoria porque readiness **no** consulta Graph API: que
> un par aparezca como configurado no prueba que Meta haya aprobado la plantilla ni que sus variables
> coincidan; eso se verifica a mano en el administrador de WhatsApp.

## Precondiciones

- Ambiente de pruebas autorizado y gate inicialmente OFF.
- Catálogos `es/en` activos y válidos.
- Campaña de prueba completa en `es/en` con `plantillaRef` real.
- Plantillas Meta aprobadas y teléfonos de prueba si se ejecutará envío real.

## Prueba 1 — regresión gate OFF

Ejecuta las rutas de cierre normal, salida explícita, tope/cupo, fallback e inactividad con el gate
OFF. Todas deben conservar el cierre legacy exacto.

## Prueba 2 — matriz de cierres bilingües

Con gate ON, ejecuta cada ruta de cierre con un hilo `es` y otro `en`. El cierre debe coincidir con
`localizaciones.{idioma}.mensajeCierre`; ninguna salida inglesa puede contener el cierre español.

## Prueba 3 — localización inconsistente

En una prueba automatizada o fixture aislado, simula una campaña histórica activa sin cierre del
idioma del hilo. Debe aparecer el fallo tipificado y cero fallback a otro idioma; no debe quedar una
transición parcial ni duplicarse al reintentar.

## Prueba 4 — readiness sin mapeo: activa frente a borrador

Comprueba por separado, en fixture/local autorizado:

- una campaña **activa** que requiera un par ausente: el par aparece con `bloqueaGateOn=true` y
  `listoParaGateOn=false`;
- una campaña **borrador** que requiera un par ausente: el par y sus problemas siguen visibles con
  `bloqueaGateOn=false`, pero no alteran una señal que por lo demás está lista;
- un mismo par requerido por activa y borrador: se deduplica, lista ambas campañas y bloquea por la
  consumidora activa.

No cambies una configuración compartida sin autorización.

## Prueba 5 — readiness estructural completo

Configura `Nombre`, `Idioma` y los `Componentes` exactos de las plantillas aprobadas. Tras reiniciar la
API, readiness debe mostrar los pares activos configurados y `listoParaGateOn=true` si los catálogos
también están listos, aunque existan borradores incompletos.

La API confirma estructura, no devuelve ni certifica el nombre físico. Para cerrar esta prueba se
acepta como evidencia externa la verificación del operador en Azure/Meta del nombre, código y orden de
componentes; no se requiere ampliar el endpoint. Registrar la referencia sin secretos.

## Prueba 6 — componentes y límite de la comprobación

- Un componente vacío o duplicado se reporta como inválido.
- `Componentes=[]` es válido únicamente si la plantilla aprobada no tiene variables de cuerpo.
- Verifica manualmente en Meta el número, orden y significado de variables. Readiness no puede afirmar
  que la plantilla está aprobada ni que coincide con Meta.
- Con gate ON, intenta activar un borrador con uno de sus mapeos incompleto: debe responder
  `400 VALIDATION_ERROR` y conservar `borrador`.
- Con gate ON, completa sus propios mapeos y repite: debe poder activarse aunque otro borrador siga
  incompleto. Con gate OFF, la transición conserva la conducta previa.

## Prueba 7 — lote mixto

Con autorización de tráfico real, envía a un participante `es` y uno `en`. Ambos deben usar nombre,
código Meta y valores de cuerpo de su mapeo. Un fallo selectivo no debe detener el otro envío.

## Evidencia y salida

Registrar por prueba `PASS|FAIL|BLOCKED`, IDs sin teléfonos completos, estado final del gate, pares
requeridos, cantidad/orden de componentes y referencia verificable a la plantilla aprobada. No
registrar tokens ni secretos.

El smoke acotado habilita el inicio de DT-I20-02 cuando 1–6 estén en PASS y la evidencia humana Meta
esté aceptada. La corrida P-32 completa de QAS/17 se ejecutará después de DT-I20-02, según el orden
acordado; la prueba 7 puede quedar BLOCKED únicamente por una restricción externa explícita aceptada.

## Revalidación acotada de DT-P32-03-01

Después de desplegar el microajuste con autorización:

1. repetir pruebas 4, 5 y 6;
2. conservar como evidencia los PASS ya obtenidos en pruebas 1–3, sin reabrir el gate solo para
   repetir cierres;
3. guardar el resultado en
   `QAS/resultados/Resultados_P32_Smoke_DT-P32-03-01_2026-08-15.md` (o la fecha real);
4. declarar P-32 smoke green únicamente si 4–6 están PASS y la evidencia humana Meta está aceptada;
5. dejar gate OFF y `Simulacion__Habilitada=false`, y retirar `GHT_DIAG_KEY` al cerrar.

Para llamadas repetidas al webhook simulado, enviar un `whatsappMessageId` explícito y único por
intento. Repetir el mismo número y texto en la misma fecha UTC puede ser deduplicado y devolver 200 sin
crear una interacción nueva.
