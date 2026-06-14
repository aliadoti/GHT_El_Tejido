import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

import { AdminApiService } from '../../core/admin-api.service';
import { EnvioEstado, JobEnvio } from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { formatApiError } from '../../shared-error';

@Component({
  selector: 'app-envios-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <p class="eyebrow">REQ 15, 27.2</p>
          <h2>Envios de campania</h2>
        </div>
        <button type="button" class="ghost-button" (click)="load()">Actualizar</button>
      </div>

      @if (error()) {
        <p class="form-error">{{ error() }}</p>
      }

      <section class="panel">
        <form class="filters-grid" (ngSubmit)="load()">
          <label>
            Campania ID
            <input name="campaniaId" [(ngModel)]="campaniaId" placeholder="camp_..." />
          </label>
          <label>
            Mensaje inicial ID
            <input name="mensajeInicialId" [(ngModel)]="mensajeInicialId" placeholder="mi_..." />
          </label>
          <button class="primary-button" type="submit">Consultar</button>
        </form>
      </section>

      <section class="panel">
        <div class="panel-heading">
          <h3>Acciones</h3>
          @if (job(); as currentJob) {
            <span class="status-badge"
              >{{ currentJob.estado }} - {{ currentJob.encolados }} encolados</span
            >
          }
        </div>
        <div class="actions-row">
          <button
            class="primary-button"
            type="button"
            [disabled]="!auth.isAdmin()"
            (click)="enviarSeleccionados()"
          >
            Enviar seleccionados
          </button>
          <button
            class="ghost-button"
            type="button"
            [disabled]="!auth.isAdmin()"
            (click)="reenviar()"
          >
            Reenviar sin respuesta
          </button>
          <button
            class="ghost-button"
            type="button"
            [disabled]="!auth.isAdmin()"
            (click)="reintentar()"
          >
            Reintentar errores
          </button>
        </div>
      </section>

      <section class="panel">
        <div class="panel-heading">
          <h3>Estado por participante</h3>
          <span class="muted">{{ estados().length }} filas</span>
        </div>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th><input type="checkbox" [checked]="allSelected()" (change)="toggleAll()" /></th>
                <th>Usuario</th>
                <th>Numero</th>
                <th>Envio</th>
                <th>Respuesta</th>
                <th>Error</th>
              </tr>
            </thead>
            <tbody>
              @for (estado of estados(); track estado.usuarioId) {
                <tr>
                  <td>
                    <input
                      type="checkbox"
                      [checked]="seleccion().includes(estado.usuarioId)"
                      (change)="toggle(estado.usuarioId)"
                    />
                  </td>
                  <td>{{ estado.usuarioId }}</td>
                  <td>{{ estado.numero }}</td>
                  <td>
                    <span class="status-badge">{{ estado.estadoEnvio }}</span>
                  </td>
                  <td>{{ estado.estadoRespuesta }}</td>
                  <td>{{ estado.error ?? '-' }}</td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="6" class="empty-cell">Sin estado de envio para esta campania.</td>
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
export class EnviosPage {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  protected readonly auth = inject(AuthService);
  protected readonly estados = signal<EnvioEstado[]>([]);
  protected readonly seleccion = signal<string[]>([]);
  protected readonly job = signal<JobEnvio | null>(null);
  protected readonly error = signal('');
  protected campaniaId =
    this.route.snapshot.paramMap.get('id') === '_'
      ? ''
      : (this.route.snapshot.paramMap.get('id') ?? '');
  protected mensajeInicialId = '';

  constructor() {
    if (this.campaniaId) {
      this.load();
    }
  }

  load() {
    if (!this.campaniaId) {
      this.error.set('Ingresa un campaniaId para consultar envios.');
      return;
    }

    this.api.envios(this.campaniaId).subscribe({
      next: (items) => {
        this.estados.set(items);
        this.error.set('');
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  enviarSeleccionados() {
    this.api
      .enviar(this.campaniaId, this.seleccion(), this.mensajeInicialId || undefined)
      .subscribe(this.jobObserver());
  }

  reenviar() {
    this.api
      .reenviar(this.campaniaId, this.mensajeInicialId || undefined)
      .subscribe(this.jobObserver());
  }

  reintentar() {
    this.api
      .reintentar(this.campaniaId, this.mensajeInicialId || undefined)
      .subscribe(this.jobObserver());
  }

  toggle(usuarioId: string) {
    const current = this.seleccion();
    this.seleccion.set(
      current.includes(usuarioId)
        ? current.filter((id) => id !== usuarioId)
        : [...current, usuarioId],
    );
  }

  toggleAll() {
    this.seleccion.set(this.allSelected() ? [] : this.estados().map((estado) => estado.usuarioId));
  }

  allSelected() {
    return this.estados().length > 0 && this.seleccion().length === this.estados().length;
  }

  private jobObserver() {
    return {
      next: (job: JobEnvio) => {
        this.job.set(job);
        this.load();
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    };
  }
}
