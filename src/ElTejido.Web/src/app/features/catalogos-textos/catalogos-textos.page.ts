import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  AdminApiService,
  CatalogoTextos,
  CatalogoTextosEfectivo,
  ContenidoCatalogoTextos,
  MapeoPlantillaMeta,
  PrevalidacionCatalogoTextos,
  ReadinessCatalogosTextos,
  ReadinessIdiomaCatalogo,
} from '../../core/admin-api.service';
import { EstadoAccesibleComponent } from '../../shared/estado-accesible.component';
import { formatApiError } from '../../shared-error';

interface DiferenciaMensaje {
  clave: string;
  activa: string;
  borrador: string;
}

interface DiferenciaGrupo {
  clave: string;
  activa: number;
  borrador: number;
}

/**
 * P-32 + DT-P32-02: administración de contenido global. El portal nunca activa nada
 * automáticamente, no repara JSON y no recorta listas: solo muestra lo que el servidor decidió.
 */
@Component({
  selector: 'app-catalogos-textos-page',
  standalone: true,
  imports: [FormsModule, EstadoAccesibleComponent],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <h2>Textos de conversación</h2>
          <p class="subhead">
            Edita borradores por idioma. Activar afecta solo conversaciones nuevas.
          </p>
        </div>
        <button type="button" class="ghost-button" (click)="cargar()">Actualizar</button>
      </div>

      <app-estado-accesible tipo="error" [mensaje]="error()" />
      <app-estado-accesible tipo="informacion" [mensaje]="aviso()" />

      <section class="panel form-grid">
        <label>
          Idioma
          <select name="idioma" [(ngModel)]="idioma" (ngModelChange)="cargar()">
            <option value="es">Español</option>
            <option value="en">English</option>
          </select>
        </label>
        <p class="subhead">Vista efectiva: {{ efectivo()?.origen ?? 'sin consultar' }}.</p>
      </section>

      <!-- DT-P32-02 §4.1: readiness real; la vista efectiva no prueba que el gate esté encendido. -->
      <section class="panel form-grid" aria-labelledby="preparacion-titulo">
        <div class="panel-heading">
          <h3 id="preparacion-titulo">Preparación</h3>
        </div>
        @if (readiness(); as estado) {
          <p class="subhead">
            Los textos del catálogo
            {{ estado.gateHabilitado ? 'ya se usan' : 'todavía no se usan' }} en las conversaciones.
            Máximo de frases por grupo: {{ estado.limites.maxFrasesPorGrupo }}. Tamaño máximo del
            archivo: {{ maximoEnKiB(estado) }} KiB.
          </p>
          <ul class="lista-simple">
            @for (item of estado.idiomas; track item.idioma) {
              <li>
                <strong>{{ nombreIdioma(item.idioma) }}:</strong>
                @if (item.listo) {
                  activo en la versión {{ item.versionActiva }}.
                } @else if (item.tieneActivo) {
                  hay una versión activa que ya no es válida; revísala antes de usarla.
                } @else if (item.tieneBorrador) {
                  hay un borrador sin activar.
                } @else {
                  todavía no hay contenido para este idioma.
                }
                @if (item.campaniasBloqueadas.length > 0) {
                  <span class="form-error">
                    Campañas en espera de este idioma:
                    {{ nombresBloqueadas(item) }}.
                  </span>
                }
              </li>
            }
          </ul>

          <!--
            DT-P32-03 §3.2: catálogos y plantillas son dos comprobaciones distintas. Un catálogo
            listo no basta: sin el mapeo Meta el primer envío falla para todos los participantes.
          -->
          <h4>Plantillas de WhatsApp</h4>
          <p class="subhead" id="ayuda-plantillas">
            Esta revisión solo comprueba que la plantilla esté configurada en el servidor. No puede
            confirmar que Meta la haya aprobado ni que sus variables coincidan: eso se verifica a
            mano.
          </p>
          @if (estado.mapeosMeta.length === 0) {
            <p class="subhead">
              Ninguna campaña activa o en borrador pide una plantilla para estos idiomas.
            </p>
          } @else {
            <ul class="lista-simple" aria-describedby="ayuda-plantillas">
              @for (mapeo of estado.mapeosMeta; track $index) {
                <li>
                  <strong
                    >{{ mapeo.plantillaRef ?? 'sin nombre corto' }} ({{
                      nombreIdioma(mapeo.idioma)
                    }}):</strong
                  >
                  @if (mapeo.problemas.length === 0) {
                    configurada{{
                      mapeo.componentes.length > 0
                        ? ' con los datos: ' + mapeo.componentes.join(', ')
                        : ' sin datos variables'
                    }}.
                  } @else {
                    <span class="form-error">
                      {{ describirMapeo(mapeo) }}
                    </span>
                  }
                  <span class="subhead"> La piden: {{ nombresRequirentes(mapeo) }}. </span>
                </li>
              }
            </ul>
          }

          <p [class]="estado.listoParaGateOn ? 'subhead' : 'form-error'" role="status">
            @if (estado.listoParaGateOn) {
              Todo lo que se revisa aquí está listo para empezar a usar estos textos.
            } @else if (estado.listo) {
              Los textos están listos, pero todavía falta configurar plantillas: no empieces a
              usarlos aún.
            } @else {
              Todavía falta preparación: no empieces a usar estos textos.
            }
          </p>
        } @else {
          <p class="subhead">Sin datos de preparación.</p>
        }
      </section>

      <!-- DT-P32-02 §2.1: base curada y configuración anterior son acciones distintas. -->
      <section class="panel form-grid" aria-labelledby="inicializar-titulo">
        <div class="panel-heading">
          <h3 id="inicializar-titulo">Empezar el contenido de este idioma</h3>
        </div>
        <p class="subhead" id="ayuda-inicializar">
          La semilla base es contenido revisado que siempre funciona. La configuración anterior es
          una foto de lo que hay hoy en el servidor y puede tener errores; revísala antes de usarla.
          Ninguna de las dos activa nada.
        </p>
        <div class="acciones">
          <button
            type="button"
            class="primary-button"
            aria-describedby="ayuda-inicializar"
            (click)="crearSemillaBase()"
          >
            Crear semilla base
          </button>
          <button type="button" class="ghost-button" (click)="revisarLegacy()">
            Revisar configuración anterior
          </button>
          <button type="button" class="ghost-button" (click)="descargarLegacy()">
            Descargar configuración anterior como JSON
          </button>
        </div>
        @if (legacy(); as revision) {
          <div class="notice" role="status" aria-live="polite">
            @if (revision.valido) {
              <p>
                La configuración anterior es válida: {{ revision.conteos.mensajes }} mensajes,
                {{ revision.conteos.gruposFrases }} grupos y {{ revision.conteos.frases }} frases.
              </p>
              <button type="button" class="primary-button" (click)="importarLegacy()">
                Importar configuración anterior como borrador
              </button>
            } @else {
              <p>
                La configuración anterior no se puede usar tal como está ({{
                  revision.errores.length
                }}
                {{ revision.errores.length === 1 ? 'problema' : 'problemas' }}). Descárgala,
                corrígela y cárgala por edición masiva.
              </p>
              <ul class="lista-simple">
                @for (problema of revision.errores; track $index) {
                  <li>{{ describir(problema) }}</li>
                }
              </ul>
            }
          </div>
        }
      </section>

      <div class="two-column">
        <section class="panel">
          <div class="panel-heading"><h3>Versiones</h3></div>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Versión</th>
                  <th>Estado</th>
                  <th>Huella</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (catalogo of catalogos(); track catalogo.version) {
                  <tr [class.row-selected]="seleccionado()?.version === catalogo.version">
                    <td>v{{ catalogo.version }}</td>
                    <td>
                      <span class="status-badge">{{ catalogo.estado }}</span>
                    </td>
                    <td>{{ catalogo.huella }}</td>
                    <td>
                      <button type="button" class="link-button" (click)="seleccionar(catalogo)">
                        Ver
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="4" class="empty-cell">No hay versiones para este idioma.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="panel">
          <div class="panel-heading">
            <h3>{{ seleccionado() ? 'Contenido de la versión' : 'Seleccione una versión' }}</h3>
            @if (seleccionado()?.estado === 'borrador') {
              <div>
                <button type="button" class="ghost-button" (click)="guardarBorrador()">
                  Guardar borrador
                </button>
                <button type="button" class="primary-button" (click)="activar()">Activar</button>
              </div>
            }
            @if (seleccionado()?.estado === 'inactivo') {
              <button type="button" class="primary-button" (click)="activar()">
                Reactivar esta versión
              </button>
            }
            @if (seleccionado()) {
              <button type="button" class="ghost-button" (click)="exportar()">
                Descargar JSON para edición masiva
              </button>
            }
          </div>
          @if (seleccionado()) {
            <form class="form-grid" (ngSubmit)="guardarBorrador()">
              <h4>Mensajes</h4>
              @for (entrada of mensajes(); track entrada.clave) {
                <label>
                  {{ entrada.clave }} ({{ entrada.valor.length }} caracteres)
                  <textarea
                    [name]="'mensaje_' + entrada.clave"
                    [ngModel]="entrada.valor"
                    (ngModelChange)="cambiarMensaje(entrada.clave, $event)"
                    [readonly]="seleccionado()?.estado !== 'borrador'"
                    rows="3"
                  ></textarea>
                </label>
              }
              <h4>Frases de intención</h4>
              @for (entrada of frases(); track entrada.clave) {
                <label>
                  {{ entrada.clave }} (una frase por línea)
                  <textarea
                    [name]="'frase_' + entrada.clave"
                    [ngModel]="frasesTexto(entrada.valor)"
                    (ngModelChange)="cambiarFrases(entrada.clave, $event)"
                    [readonly]="seleccionado()?.estado !== 'borrador'"
                    rows="4"
                  ></textarea>
                </label>
              }
            </form>
          }
        </section>
      </div>

      <!-- DT-P32-02 §3.1: descargar → editar → revisar → confirmar. Cargar nunca publica. -->
      <section class="panel form-grid" aria-labelledby="masiva-titulo">
        <div class="panel-heading">
          <h3 id="masiva-titulo">Cargar JSON editado</h3>
        </div>
        <p class="subhead" id="ayuda-masiva">
          Descarga el JSON de una versión, cambia solo los textos de <code>mensajes</code> y las
          listas de <code>frases</code>, y vuelve a cargarlo. Se revisa antes de guardar y, al
          confirmar, se crea una versión nueva en borrador; nunca reemplaza la versión activa.
        </p>
        <label for="archivo-catalogo">Archivo JSON editado</label>
        <input
          id="archivo-catalogo"
          #archivoInput
          type="file"
          accept="application/json,.json"
          aria-describedby="ayuda-masiva"
          (change)="revisarArchivo($event, archivoInput)"
        />
        @if (prevalidacion(); as revision) {
          <div class="notice" role="status" aria-live="polite">
            <p>
              Archivo <strong>{{ nombreArchivo() }}</strong> · idioma
              {{ nombreIdioma(revision.idioma) }} · {{ revision.conteos.mensajes }} mensajes ·
              {{ revision.conteos.gruposFrases }} grupos · {{ revision.conteos.frases }} frases.
            </p>
            @if (revision.valido) {
              <p>Sin errores. Puedes crear una versión nueva en borrador con este contenido.</p>
              <div class="acciones">
                <button type="button" class="primary-button" (click)="confirmarImportacion()">
                  Importar como nuevo borrador
                </button>
                <button type="button" class="ghost-button" (click)="cancelarImportacion()">
                  Cancelar
                </button>
              </div>
            } @else {
              <p class="form-error">
                No se cargó nada. Corrige {{ revision.errores.length }}
                {{ revision.errores.length === 1 ? 'problema' : 'problemas' }} y vuelve a
                seleccionar el archivo.
              </p>
              <ul class="lista-simple">
                @for (problema of revision.errores; track $index) {
                  <li>{{ describir(problema) }}</li>
                }
              </ul>
              <button type="button" class="ghost-button" (click)="cancelarImportacion()">
                Cancelar
              </button>
            }
          </div>
        }
      </section>

      @if (diferenciasMensajes().length > 0 || diferenciasGrupos().length > 0) {
        <section class="panel" aria-labelledby="comparacion-titulo">
          <div class="panel-heading">
            <h3 id="comparacion-titulo">Diferencias con la versión activa</h3>
          </div>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Clave</th>
                  <th>Versión activa</th>
                  <th>Esta versión</th>
                </tr>
              </thead>
              <tbody>
                @for (fila of diferenciasMensajes(); track fila.clave) {
                  <tr>
                    <td>{{ fila.clave }}</td>
                    <td>{{ fila.activa || '(sin texto)' }}</td>
                    <td>{{ fila.borrador || '(sin texto)' }}</td>
                  </tr>
                }
                @for (fila of diferenciasGrupos(); track fila.clave) {
                  <tr>
                    <td>{{ fila.clave }}</td>
                    <td>{{ fila.activa }} frases</td>
                    <td>{{ fila.borrador }} frases</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogosTextosPage {
  private readonly api = inject(AdminApiService);
  protected idioma: 'es' | 'en' = 'es';
  protected readonly catalogos = signal<CatalogoTextos[]>([]);
  protected readonly seleccionado = signal<CatalogoTextos | null>(null);
  protected readonly efectivo = signal<CatalogoTextosEfectivo | null>(null);
  protected readonly readiness = signal<ReadinessCatalogosTextos | null>(null);
  protected readonly legacy = signal<PrevalidacionCatalogoTextos | null>(null);
  protected readonly prevalidacion = signal<PrevalidacionCatalogoTextos | null>(null);
  protected readonly nombreArchivo = signal('');
  protected readonly error = signal('');
  protected readonly aviso = signal('');
  private contenido: ContenidoCatalogoTextos = { mensajes: {}, frases: {} };
  /** Contenido del archivo tal cual se leyó: se envía sin tocar para que el servidor decida. */
  private archivoPendiente: unknown = null;

  constructor() {
    this.cargar();
  }

  protected cargar(): void {
    this.api.catalogosTextos({ idioma: this.idioma }).subscribe({
      next: (catalogos) => {
        this.catalogos.set(catalogos);
        this.seleccionar(catalogos[0] ?? null);
        this.error.set('');
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
    this.api.catalogoTextosEfectivo(this.idioma).subscribe({
      next: (efectivo) => this.efectivo.set(efectivo),
      error: () => this.efectivo.set(null),
    });
    this.api.readinessCatalogosTextos().subscribe({
      next: (readiness) => this.readiness.set(readiness),
      error: () => this.readiness.set(null),
    });
    this.legacy.set(null);
  }

  protected seleccionar(catalogo: CatalogoTextos | null): void {
    this.seleccionado.set(catalogo);
    this.contenido = this.copiar(catalogo ?? undefined);
  }

  protected mensajes() {
    return Object.entries(this.contenido.mensajes).map(([clave, valor]) => ({ clave, valor }));
  }

  protected frases() {
    return Object.entries(this.contenido.frases).map(([clave, valor]) => ({ clave, valor }));
  }

  protected frasesTexto(frases: readonly string[]): string {
    return frases.join('\n');
  }

  protected cambiarMensaje(clave: string, valor: string): void {
    this.contenido.mensajes[clave] = valor;
  }

  protected cambiarFrases(clave: string, valor: string): void {
    this.contenido.frases[clave] = valor
      .split('\n')
      .map((frase) => frase.trim())
      .filter(Boolean);
  }

  protected nombreIdioma(idioma: string): string {
    return idioma === 'en' ? 'inglés' : 'español';
  }

  protected maximoEnKiB(readiness: ReadinessCatalogosTextos): number {
    return Math.round(readiness.limites.maxBytesImportacionJson / 1024);
  }

  protected nombresBloqueadas(item: ReadinessIdiomaCatalogo): string {
    return item.campaniasBloqueadas.map((campania) => campania.nombre).join(', ');
  }

  protected nombresRequirentes(mapeo: MapeoPlantillaMeta): string {
    return mapeo.campanias.map((campania) => `${campania.nombre} (${campania.estado})`).join(', ');
  }

  /** DT-P32-03 §3.2: cada problema estructural dice qué falta configurar, sin jerga del servidor. */
  protected describirMapeo(mapeo: MapeoPlantillaMeta): string {
    const partes = mapeo.problemas.map((problema) => {
      if (problema === 'plantilla_ref_faltante') {
        return 'el mensaje inicial no dice qué plantilla usar en este idioma';
      }
      if (problema === 'nombre_faltante') {
        return 'falta el nombre de la plantilla aprobada en Meta';
      }
      if (problema === 'idioma_meta_faltante') {
        return 'falta el código de idioma de Meta';
      }
      if (problema === 'componente_vacio') {
        return 'hay un dato variable en blanco';
      }
      if (problema === 'componente_duplicado') {
        return 'hay un dato variable repetido';
      }
      return problema;
    });
    return `${partes.join('; ')}.`;
  }

  /** Traduce el motivo técnico del servidor a algo que un administrador pueda accionar. */
  protected describir(problema: { field: string | null; issue: string }): string {
    const campo = problema.field ?? 'el archivo';
    const issue = problema.issue;
    if (issue === 'no_coincide_con_seleccion') {
      return `${campo}: el archivo no corresponde al idioma o al catálogo seleccionado.`;
    }
    if (issue === 'no_soportado') {
      return `${campo}: el formato del archivo no es el que genera esta pantalla.`;
    }
    if (issue === 'obligatorio') {
      return `${campo}: falta y es obligatorio.`;
    }
    if (issue === 'vacio') {
      return `${campo}: quedó sin texto.`;
    }
    if (issue === 'clave_desconocida') {
      return `${campo}: no es una clave que el sistema sepa usar; no se pueden inventar claves.`;
    }
    if (issue === 'frase_duplicada') {
      return `${campo}: hay frases repetidas dentro del grupo.`;
    }
    if (issue === 'html_no_permitido') {
      return `${campo}: no se admiten etiquetas HTML.`;
    }
    if (issue.startsWith('placeholder_no_permitido:')) {
      return `${campo}: usa un dato entre llaves que no está permitido (${issue.split(':')[1]}).`;
    }
    if (issue.startsWith('debe_tener_entre_')) {
      return `${campo}: la cantidad de frases está fuera del límite permitido.`;
    }
    if (issue.startsWith('frase_vacia_o_excede_')) {
      return `${campo}: hay una frase vacía o demasiado larga.`;
    }
    if (issue.startsWith('excede_') && issue.endsWith('_bytes')) {
      return 'El archivo pesa más de lo permitido.';
    }
    if (issue.startsWith('excede_')) {
      return `${campo}: el texto es demasiado largo.`;
    }
    if (issue === 'tipo_invalido' || issue === 'elemento_no_texto') {
      return `${campo}: el contenido no tiene la forma esperada.`;
    }
    if (issue === 'json_invalido') {
      return 'El archivo no es un JSON válido.';
    }
    return `${campo}: ${issue}`;
  }

  protected crearSemillaBase(): void {
    this.api.crearSemillaBaseCatalogoTextos(this.idioma).subscribe({
      next: () => {
        this.aviso.set('Semilla base creada como borrador. Revísala antes de activarla.');
        this.cargar();
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  protected revisarLegacy(): void {
    this.api.prevalidarSemillaLegacy(this.idioma).subscribe({
      next: (revision) => {
        this.legacy.set(revision);
        this.aviso.set('Revisión hecha. No se guardó nada.');
        this.error.set('');
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  protected descargarLegacy(): void {
    this.api.exportarSemillaLegacy(this.idioma).subscribe({
      next: (archivo) =>
        this.descargar(archivo, `catalogo-${this.idioma}-configuracion-anterior.json`),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  protected importarLegacy(): void {
    this.api.importarSemillaLegacy(this.idioma).subscribe({
      next: () => {
        this.aviso.set('Configuración anterior importada como borrador.');
        this.legacy.set(null);
        this.cargar();
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  protected guardarBorrador(): void {
    const catalogo = this.seleccionado();
    if (!catalogo || catalogo.estado !== 'borrador') return;
    this.api.actualizarCatalogoTextos(catalogo, this.contenido).subscribe({
      next: (actualizado) => {
        this.seleccionar(actualizado);
        this.aviso.set('Borrador guardado.');
        this.cargar();
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  protected activar(): void {
    const catalogo = this.seleccionado();
    if (!catalogo || !confirm(`¿Activar la versión ${catalogo.version} para ${catalogo.idioma}?`))
      return;
    this.api.activarCatalogoTextos(catalogo).subscribe({
      next: () => {
        this.aviso.set('Versión activada. El cambio se aplicará a conversaciones nuevas.');
        this.cargar();
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  protected exportar(): void {
    const catalogo = this.seleccionado();
    if (!catalogo) return;
    this.api.exportarCatalogoTextos(catalogo).subscribe({
      next: (archivo) =>
        this.descargar(
          archivo,
          `catalogo-${catalogo.familiaId}-${catalogo.idioma}-v${catalogo.version}-editable.json`,
        ),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  /**
   * Lee el archivo y pide la revisión al servidor. No escribe nada: hasta que el administrador
   * confirme, la versión activa y el borrador seleccionado quedan intactos.
   */
  protected revisarArchivo(evento: Event, input: HTMLInputElement): void {
    const archivo = (evento.target as HTMLInputElement).files?.[0];
    // El input se limpia siempre para poder volver a elegir el mismo archivo ya corregido.
    input.value = '';
    if (!archivo) return;
    this.prevalidacion.set(null);
    this.archivoPendiente = null;
    this.nombreArchivo.set(archivo.name);
    const maximo = this.readiness()?.limites.maxBytesImportacionJson;
    if (maximo && archivo.size > maximo) {
      this.error.set(
        `El archivo pesa más de lo permitido (máximo ${Math.round(maximo / 1024)} KiB).`,
      );
      return;
    }

    archivo
      .text()
      .then((texto) => {
        const contenido: unknown = JSON.parse(texto);
        this.archivoPendiente = contenido;
        this.error.set('');
        this.api.prevalidarImportacionCatalogoTextos(contenido, this.idioma).subscribe({
          next: (revision) => {
            this.prevalidacion.set(revision);
            this.aviso.set(
              revision.valido
                ? 'Archivo revisado. Confirma para crear el borrador.'
                : 'El archivo tiene errores. No se guardó nada.',
            );
          },
          error: (err: unknown) => {
            this.archivoPendiente = null;
            this.error.set(formatApiError(err));
          },
        });
      })
      .catch(() => {
        this.archivoPendiente = null;
        this.error.set('El archivo no contiene JSON válido.');
      });
  }

  protected confirmarImportacion(): void {
    const archivo = this.archivoPendiente;
    if (archivo === null || !this.prevalidacion()?.valido) return;
    this.api.importarCatalogoTextos(archivo, this.idioma).subscribe({
      next: (creado) => {
        this.aviso.set(`Se creó la versión ${creado.version} en borrador con tus cambios.`);
        this.cancelarImportacion();
        this.api.catalogosTextos({ idioma: this.idioma }).subscribe({
          next: (catalogos) => {
            this.catalogos.set(catalogos);
            // Deja seleccionado el borrador nuevo para poder compararlo con la activa.
            this.seleccionar(
              catalogos.find((catalogo) => catalogo.version === creado.version) ?? creado,
            );
          },
          error: (err: unknown) => this.error.set(formatApiError(err)),
        });
        this.api.readinessCatalogosTextos().subscribe({
          next: (readiness) => this.readiness.set(readiness),
          error: () => this.readiness.set(null),
        });
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  protected cancelarImportacion(): void {
    this.prevalidacion.set(null);
    this.archivoPendiente = null;
    this.nombreArchivo.set('');
  }

  protected diferenciasMensajes(): DiferenciaMensaje[] {
    const activa = this.activa();
    const seleccionado = this.seleccionado();
    if (!activa || !seleccionado || activa.version === seleccionado.version) return [];
    return Object.keys(seleccionado.mensajes)
      .filter((clave) => (activa.mensajes[clave] ?? '') !== seleccionado.mensajes[clave])
      .map((clave) => ({
        clave,
        activa: activa.mensajes[clave] ?? '',
        borrador: seleccionado.mensajes[clave],
      }));
  }

  protected diferenciasGrupos(): DiferenciaGrupo[] {
    const activa = this.activa();
    const seleccionado = this.seleccionado();
    if (!activa || !seleccionado || activa.version === seleccionado.version) return [];
    return Object.keys(seleccionado.frases)
      .filter(
        (clave) => (activa.frases[clave] ?? []).join('') !== seleccionado.frases[clave].join(''),
      )
      .map((clave) => ({
        clave,
        activa: (activa.frases[clave] ?? []).length,
        borrador: seleccionado.frases[clave].length,
      }));
  }

  private activa(): CatalogoTextos | null {
    return this.catalogos().find((catalogo) => catalogo.estado === 'activo') ?? null;
  }

  private descargar(archivo: Blob, nombre: string): void {
    const enlace = document.createElement('a');
    enlace.href = URL.createObjectURL(archivo);
    enlace.download = nombre;
    enlace.click();
    URL.revokeObjectURL(enlace.href);
  }

  private copiar(catalogo: CatalogoTextos | undefined): ContenidoCatalogoTextos {
    return {
      mensajes: { ...(catalogo?.mensajes ?? {}) },
      frases: Object.fromEntries(
        Object.entries(catalogo?.frases ?? {}).map(([clave, valor]) => [clave, [...valor]]),
      ),
    };
  }
}
