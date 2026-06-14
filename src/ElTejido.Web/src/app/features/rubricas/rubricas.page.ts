import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AdminApiService } from '../../core/admin-api.service';
import { Rubrica } from '../../core/api-models';
import { formatApiError } from '../../shared-error';

@Component({
  selector: 'app-rubricas-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <p class="eyebrow">REQ 17</p>
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
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="4" class="empty-cell">No hay rubricas.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="panel">
          <div class="panel-heading"><h3>Nueva rubrica</h3></div>
          <form class="form-grid" (ngSubmit)="crear()">
            <label>ID familia <input name="id" [(ngModel)]="form.id" /></label>
            <label>Nombre <input name="nombre" [(ngModel)]="form.nombre" /></label>
            <label>Descripcion <input name="descripcion" [(ngModel)]="form.descripcion" /></label>
            <label>
              Contenido Markdown
              <textarea name="contenido" rows="9" [(ngModel)]="form.contenidoMarkdown"></textarea>
            </label>
            <button class="primary-button" type="submit">Crear v1</button>
          </form>
        </section>
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RubricasPage {
  private readonly api = inject(AdminApiService);
  protected readonly rubricas = signal<Rubrica[]>([]);
  protected readonly error = signal('');
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

  crear() {
    this.api
      .crearRubrica({
        ...this.form,
        escala: { min: 1, max: 5 },
        criterios: [{ nombre: 'Impacto', peso: 1 }],
        estado: 'activa',
      })
      .subscribe({
        next: () => {
          this.form = this.emptyForm();
          this.load();
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
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
