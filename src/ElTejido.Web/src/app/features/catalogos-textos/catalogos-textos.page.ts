import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  AdminApiService,
  CatalogoTextos,
  CatalogoTextosEfectivo,
  ContenidoCatalogoTextos,
} from '../../core/admin-api.service';
import { EstadoAccesibleComponent } from '../../shared/estado-accesible.component';
import { formatApiError } from '../../shared-error';

/** P-32: administración de contenido global. El portal no activa nada automáticamente. */
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
        @if (!seleccionado()) {
          <button type="button" class="primary-button" (click)="crearSemilla()">
            Crear borrador desde semilla
          </button>
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
              <button type="button" class="ghost-button" (click)="exportar()">Exportar JSON</button>
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
      <section class="panel form-grid">
        <h3>Importar borrador JSON</h3>
        <p class="subhead">La importación crea un borrador; nunca activa contenido.</p>
        <input type="file" accept="application/json" (change)="importarArchivo($event)" />
      </section>
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
  protected readonly error = signal('');
  protected readonly aviso = signal('');
  private contenido: ContenidoCatalogoTextos = { mensajes: {}, frases: {} };

  constructor() {
    this.cargar();
  }

  protected cargar(): void {
    this.api.catalogosTextos({ idioma: this.idioma }).subscribe({
      next: (catalogos) => {
        this.catalogos.set(catalogos);
        this.seleccionado.set(catalogos[0] ?? null);
        this.contenido = this.copiar(catalogos[0]);
        this.error.set('');
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
    this.api.catalogoTextosEfectivo(this.idioma).subscribe({
      next: (efectivo) => this.efectivo.set(efectivo),
      error: () => this.efectivo.set(null),
    });
  }

  protected seleccionar(catalogo: CatalogoTextos): void {
    this.seleccionado.set(catalogo);
    this.contenido = this.copiar(catalogo);
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

  protected crearSemilla(): void {
    this.api.crearSemillaCatalogoTextos(this.idioma).subscribe({
      next: () => {
        this.aviso.set('Borrador creado. Revíselo antes de activarlo.');
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
      next: (archivo) => {
        const enlace = document.createElement('a');
        enlace.href = URL.createObjectURL(archivo);
        enlace.download = `catalogo-${catalogo.familiaId}-${catalogo.idioma}-v${catalogo.version}.json`;
        enlace.click();
        URL.revokeObjectURL(enlace.href);
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  protected importarArchivo(evento: Event): void {
    const archivo = (evento.target as HTMLInputElement).files?.[0];
    if (!archivo) return;
    archivo
      .text()
      .then((texto) => {
        const importado = JSON.parse(texto) as ContenidoCatalogoTextos & {
          familiaId?: string;
          idioma?: string;
        };
        this.api
          .importarCatalogoTextos({
            familiaId: importado.familiaId ?? 'conversacion-global',
            idioma: importado.idioma ?? this.idioma,
            mensajes: importado.mensajes,
            frases: importado.frases,
          })
          .subscribe({
            next: () => {
              this.aviso.set('JSON importado como borrador.');
              this.cargar();
            },
            error: (err: unknown) => this.error.set(formatApiError(err)),
          });
      })
      .catch(() => this.error.set('El archivo no contiene JSON válido.'));
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
