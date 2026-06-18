import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

import { AdminApiService } from '../../core/admin-api.service';
import { Campania, EnvioEstado, JobEnvio, MensajeInicial } from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { NotificacionesService } from '../../core/notificaciones.service';
import { formatApiError } from '../../shared-error';

@Component({
  selector: 'app-envios-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
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
            Campania
            <select name="campaniaId" [(ngModel)]="campaniaId" (ngModelChange)="onCampaniaChange()">
              <option value="" disabled>Selecciona una campania</option>
              @for (campania of campanias(); track campania.id) {
                <option [value]="campania.id">{{ campania.nombre }}</option>
              }
            </select>
          </label>
          <label>
            Mensaje inicial
            <select name="mensajeInicialId" [(ngModel)]="mensajeInicialId">
              <option value="">Por defecto (primero activo)</option>
              @for (mensaje of mensajes(); track mensaje.id) {
                <option [value]="mensaje.id">{{ mensaje.nombreInterno }}</option>
              }
            </select>
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
  private readonly notificaciones = inject(NotificacionesService);
  protected readonly auth = inject(AuthService);
  protected readonly estados = signal<EnvioEstado[]>([]);
  protected readonly seleccion = signal<string[]>([]);
  protected readonly job = signal<JobEnvio | null>(null);
  protected readonly campanias = signal<Campania[]>([]);
  protected readonly mensajes = signal<MensajeInicial[]>([]);
  protected readonly error = signal('');
  protected campaniaId =
    this.route.snapshot.paramMap.get('id') === '_'
      ? ''
      : (this.route.snapshot.paramMap.get('id') ?? '');
  protected mensajeInicialId = '';

  constructor() {
    this.api.campanias({ pageSize: 100 }).subscribe({
      next: (page) => this.campanias.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });

    if (this.campaniaId) {
      this.load();
      this.loadMensajes();
    }
  }

  onCampaniaChange() {
    this.mensajeInicialId = '';
    this.mensajes.set([]);
    this.loadMensajes();
  }

  private loadMensajes() {
    if (!this.campaniaId) {
      return;
    }

    this.api.campania(this.campaniaId).subscribe({
      next: (campania) => this.mensajes.set(campania.mensajesIniciales ?? []),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  load() {
    if (!this.campaniaId) {
      const mensaje = 'Selecciona una campania para consultar envios.';
      this.error.set(mensaje);
      this.notificaciones.info(mensaje);
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
    if (!this.campaniaId) {
      this.notificaciones.info('Selecciona una campania antes de enviar.');
      return;
    }

    if (this.seleccion().length === 0) {
      this.notificaciones.info('Selecciona al menos un participante para enviar la campania.');
      return;
    }

    this.api
      .enviar(this.campaniaId, this.seleccion(), this.mensajeInicialId || undefined)
      .subscribe(this.jobObserver('Envio de campania encolado'));
  }

  reenviar() {
    if (!this.campaniaId) {
      this.notificaciones.info('Selecciona una campania antes de reenviar.');
      return;
    }

    this.api
      .reenviar(this.campaniaId, this.mensajeInicialId || undefined)
      .subscribe(this.jobObserver('Reenvio encolado'));
  }

  reintentar() {
    if (!this.campaniaId) {
      this.notificaciones.info('Selecciona una campania antes de reintentar.');
      return;
    }

    this.api
      .reintentar(this.campaniaId, this.mensajeInicialId || undefined)
      .subscribe(this.jobObserver('Reintento encolado'));
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

  private jobObserver(mensajeBase: string) {
    return {
      next: (job: JobEnvio) => {
        this.job.set(job);
        this.load();
        this.notificaciones.exito(`${mensajeBase}: ${job.encolados} participantes.`);
      },
      error: (err: unknown) => {
        const mensaje = formatApiError(err);
        this.error.set(mensaje);
        this.notificaciones.error(mensaje);
      },
    };
  }
}
