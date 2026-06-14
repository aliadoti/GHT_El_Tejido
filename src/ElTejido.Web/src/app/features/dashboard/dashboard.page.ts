import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AdminApiService } from '../../core/admin-api.service';
import { Campania } from '../../core/api-models';
import { formatApiError } from '../../shared-error';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <p class="eyebrow">Resumen operativo</p>
          <h2>Campanias y actividad del MVP</h2>
        </div>
        <a class="primary-button" routerLink="/campanias">Nueva campania</a>
      </div>

      @if (error()) {
        <p class="form-error">{{ error() }}</p>
      }

      <div class="metrics-grid">
        <article class="metric-card">
          <span>Total campanias</span>
          <strong>{{ campanias().length }}</strong>
        </article>
        <article class="metric-card">
          <span>Activas</span>
          <strong>{{ countByEstado('activa') }}</strong>
        </article>
        <article class="metric-card">
          <span>Borradores</span>
          <strong>{{ countByEstado('borrador') }}</strong>
        </article>
        <article class="metric-card danger">
          <span>Cerradas</span>
          <strong>{{ countByEstado('cerrada') }}</strong>
        </article>
      </div>

      <section class="panel">
        <div class="panel-heading">
          <h3>Campanias recientes</h3>
          <button type="button" class="ghost-button" (click)="load()">Actualizar</button>
        </div>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Nombre</th>
                <th>Estado</th>
                <th>Objetivo</th>
                <th>Actualizada</th>
              </tr>
            </thead>
            <tbody>
              @for (campania of campanias(); track campania.id) {
                <tr>
                  <td>{{ campania.nombre }}</td>
                  <td>
                    <span class="status-badge">{{ campania.estado }}</span>
                  </td>
                  <td>{{ campania.objetivo }}</td>
                  <td>{{ shortDate(campania.actualizadoEn) }}</td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="4" class="empty-cell">No hay campanias registradas.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  private readonly api = inject(AdminApiService);
  protected readonly campanias = signal<Campania[]>([]);
  protected readonly error = signal('');

  constructor() {
    this.load();
  }

  load() {
    this.api.campanias({ pageSize: 10 }).subscribe({
      next: (page) => {
        this.campanias.set(page.items);
        this.error.set('');
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  countByEstado(estado: string) {
    return this.campanias().filter((campania) => campania.estado === estado).length;
  }

  shortDate(value: string) {
    return value.slice(0, 10);
  }
}
