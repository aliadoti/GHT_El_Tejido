import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AdminApiService } from '../../core/admin-api.service';
import { Rubrica } from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { formatApiError } from '../../shared-error';

type ModoRubrica = 'crear' | 'editar' | 'version';

@Component({
  selector: 'app-rubricas-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <h2>Rubricas Markdown</h2>
        </div>
        <button type="button" class="ghost-button" (click)="load()">Actualizar</button>
      </div>

      @if (error()) {
        <p class="form-error">{{ error() }}</p>
      }

      <div class="two-column">
        <section class="panel">
          <div class="panel-heading"><h3>Versiones activas</h3></div>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Nombre</th>
                  <th>Version</th>
                  <th>Estado</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (rubrica of rubricas(); track rubrica.id + rubrica.version) {
                  <tr>
                    <td>{{ rubrica.id }}</td>
                    <td>{{ rubrica.nombre }}</td>
                    <td>v{{ rubrica.version }}</td>
                    <td>
                      <span class="status-badge">{{ rubrica.estado }}</span>
                    </td>
                    <td>
                      <button type="button" class="table-button" (click)="ver(rubrica)">Ver</button>
                      @if (auth.isAdmin()) {
                        <button type="button" class="table-button" (click)="editar(rubrica)">
                          {{ rubrica.estado === 'borrador' ? 'Editar' : 'Nueva version' }}
                        </button>
                        @if (rubrica.estado !== 'activa') {
                          <button
                            type="button"
                            class="table-button"
                            (click)="cambiarEstado(rubrica, 'activa')"
                          >
                            Activar
                          </button>
                        }
                        @if (rubrica.estado === 'activa') {
                          <button
                            type="button"
                            class="table-button"
                            (click)="cambiarEstado(rubrica, 'archivada')"
                          >
                            Archivar
                          </button>
                        }
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5" class="empty-cell">No hay rubricas.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        @if (vista(); as rubrica) {
          <section class="panel" aria-label="Vista de solo lectura de rubrica">
            <div class="panel-heading">
              <h3>Vista de rubrica</h3>
              <button type="button" class="ghost-button" (click)="cerrarVista()">Cerrar</button>
            </div>
            <dl class="detail-grid">
              <div>
                <dt>Nombre</dt>
                <dd>{{ rubrica.nombre }}</dd>
              </div>
              <div>
                <dt>Version</dt>
                <dd>v{{ rubrica.version }}</dd>
              </div>
              <div>
                <dt>Estado</dt>
                <dd>{{ rubrica.estado }}</dd>
              </div>
              <div>
                <dt>Escala</dt>
                <dd>{{ rubrica.escala.min }}–{{ rubrica.escala.max }}</dd>
              </div>
            </dl>
            <h4>Criterios</h4>
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Nombre</th>
                    <th>Peso</th>
                  </tr>
                </thead>
                <tbody>
                  @for (criterio of rubrica.criterios; track criterio.nombre) {
                    <tr>
                      <td>{{ criterio.nombre }}</td>
                      <td>{{ criterio.peso }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <h4>Contenido Markdown</h4>
            <pre class="markdown-preview">{{ rubrica.contenidoMarkdown }}</pre>
          </section>
        } @else if (auth.isAdmin()) {
          <section class="panel">
            <div class="panel-heading">
              <h3>{{ tituloFormulario() }}</h3>
            </div>
            <form class="form-grid" (ngSubmit)="guardar()">
              <label
                >ID familia
                <input name="id" [(ngModel)]="form.id" [disabled]="modo() !== 'crear'" />
              </label>
              <label>Nombre <input name="nombre" [(ngModel)]="form.nombre" /></label>
              <label>Descripcion <input name="descripcion" [(ngModel)]="form.descripcion" /></label>
              <label>
                Contenido Markdown
                <textarea name="contenido" rows="9" [(ngModel)]="form.contenidoMarkdown"></textarea>
              </label>
              <div class="form-actions">
                <button class="primary-button" type="submit">{{ textoBoton() }}</button>
                @if (modo() !== 'crear') {
                  <button type="button" class="ghost-button" (click)="cancelar()">Cancelar</button>
                }
              </div>
            </form>
          </section>
        }
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RubricasPage {
  private readonly api = inject(AdminApiService);
  protected readonly auth = inject(AuthService);
  protected readonly rubricas = signal<Rubrica[]>([]);
  protected readonly vista = signal<Rubrica | null>(null);
  protected readonly error = signal('');
  protected readonly modo = signal<ModoRubrica>('crear');
  protected form = this.emptyForm();

  constructor() {
    this.load();
  }

  load() {
    this.api.rubricas({ pageSize: 50 }).subscribe({
      next: (page) => this.rubricas.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  tituloFormulario() {
    switch (this.modo()) {
      case 'editar':
        return 'Editar borrador';
      case 'version':
        return 'Nueva version';
      default:
        return 'Nueva rubrica';
    }
  }

  textoBoton() {
    switch (this.modo()) {
      case 'editar':
        return 'Guardar cambios';
      case 'version':
        return 'Crear version';
      default:
        return 'Crear v1';
    }
  }

  editar(rubrica: Rubrica) {
    this.error.set('');
    this.form = {
      id: rubrica.id,
      nombre: rubrica.nombre,
      descripcion: rubrica.descripcion,
      contenidoMarkdown: rubrica.contenidoMarkdown,
    };
    // Borrador: edicion en sitio (misma version). Activa/archivada: nueva version (conserva snapshots).
    this.modo.set(rubrica.estado === 'borrador' ? 'editar' : 'version');
  }

  ver(rubrica: Rubrica) {
    this.vista.set(rubrica);
  }

  cerrarVista() {
    this.vista.set(null);
  }

  guardar() {
    const payload = {
      ...this.form,
      escala: { min: 1, max: 5 },
      criterios: [{ nombre: 'Impacto', peso: 1 }],
    };

    const peticion =
      this.modo() === 'editar'
        ? this.api.actualizarRubrica(this.form.id, { ...payload, estado: 'borrador' })
        : this.modo() === 'version'
          ? this.api.crearVersionRubrica(this.form.id, { ...payload, estado: 'activa' })
          : this.api.crearRubrica({ ...payload, estado: 'borrador' });

    peticion.subscribe({
      next: () => this.cancelar(),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  cambiarEstado(rubrica: Rubrica, estado: string) {
    this.api.cambiarEstadoRubrica(rubrica.id, estado).subscribe({
      next: () => this.load(),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  cancelar() {
    this.form = this.emptyForm();
    this.modo.set('crear');
    this.load();
  }

  private emptyForm() {
    return {
      id: '',
      nombre: '',
      descripcion: '',
      contenidoMarkdown: '# Rubrica\n\n- Impacto: 100%',
    };
  }
}
