# QAS 23 — DT-P32-03/03-01: cierre localizado y readiness Meta

> **Revalidación vigente (2026-08-16):** `DT-P32-04` corte 3/3 centralizó la resolución de contenido,
> catálogo, plantillas Meta e idioma LLM. Por ese cambio deben repetirse las pruebas 1–7 de esta guía
> sobre un despliegue autorizado del corte 3/3. Los PASS de 2026-08-15 son baseline, no evidencia del
> nuevo artefacto. La prueba 7 puede quedar `BLOCKED` si no existe autorización de tráfico real; no se
> reemplaza con un PASS histórico.

> **Baseline histórico (2026-08-15): P-32 SMOKE GREEN.** DT-P32-03 y DT-P32-03-01 están desplegadas
> (`a9f4a6f` y `60b520d`). Las pruebas 1–3 conservan PASS del primer smoke y la revalidación Azure
> dejó 4–6 PASS, incluida la guarda con ventana ON y retorno verificado a OFF. La evidencia humana de
> nombre, idioma y componentes Meta fue aceptada. Reporte:
> `resultados/Resultados_P32_Smoke_DT-P32-03-01_2026-08-15.md`. Este green habilita DT-I20-02; no
> cierra P-32, cuya QAS/17 completa y prueba 7 siguen para después de DT-I20-02.
>
> El reporte confirmó el gate OFF al terminar. Para el microajuste DT-P32-03-01, las pruebas 1 a 3
> ya habían pasado y no debían repetirse. Esa instrucción histórica no aplica a DT-P32-04: la
> revalidación vigente de arriba exige repetir 1–7. Readiness de pruebas 4
> y 5 no depende del gate. La guarda de activación añadida a la prueba 6 sí requiere una ventana ON
> controlada y retorno a OFF. Las pruebas 1 a 3 tienen su
> equivalente automatizado en la suite backend (matriz por ruta con gate OFF/ON, hilo `es`/`en` y
> localización ausente, más una prueba que impide nuevas lecturas directas del cierre). Las pruebas 4
> a 7 cuentan con el agregado de mapeos, `listoParaGateOn` y el panel **Preparación**, además de
> unitarias del validador, integración de `GET /catalogos-textos/readiness` y pruebas del
> portal. La ejecución manual sigue siendo obligatoria porque readiness **no** consulta Graph API: que
> un par aparezca como configurado no prueba que Meta haya aprobado la plantilla ni que sus variables
> coincidan; eso se verifica a mano en el administrador de WhatsApp.

## Precondiciones

- Ambiente de pruebas autorizado y gate inicialmente OFF.
- Catálogos `es/en` activos y válidos.
- Campaña de prueba completa en `es/en` con `plantillaRef` real.
- Plantillas Meta aprobadas y teléfonos de prueba si se ejecutará envío real.
- Artefacto desplegado de DT-P32-04 corte 3/3 identificado por el operador; sin esa confirmación, la
  regresión remota queda `BLOCKED` y el agente no despliega por cuenta propia.

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

Además, comprueba el readiness compuesto de DT-P32-04 con campañas activas: cada idioma solo puede
quedar listo si están disponibles su catálogo global, contenido efectivo de campaña, mapeo Meta y
política de idioma LLM. En un fixture o campaña aislada, retirar uno de los tres primeros requisitos
debe cambiar la señal a no lista con causa concreta. Restaura los datos antes de seguir.

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

Para cerrar DT-P32-04, las pruebas 1–6 y el readiness compuesto deben quedar en PASS sobre el nuevo
artefacto; después se ejecuta QAS/17. La prueba 7 puede quedar BLOCKED únicamente por una restricción
externa explícita aceptada, pero ese bloqueo impide declarar P-32 lista para activación productiva.

## Revalidación acotada de DT-P32-03-01

> **Ejecutada 2026-08-15:** pruebas 4, 5 y 6 PASS. Gate final OFF, simulación apagada posteriormente
> por el operador y clave diagnóstica retirada. No repetir salvo una nueva regresión o cambio de estos
> contratos.

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
