import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import { AdminApiService } from '../../core/admin-api.service';
import { ReportePurgaCampanias } from '../../core/api-models';
import { NotificacionesService } from '../../core/notificaciones.service';
import { formatApiError } from '../../shared-error';

const PALABRA_CONFIRMACION = 'ELIMINAR';

@Component({
  selector: 'app-mantenimiento-page',
  standalone: true,
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <p class="eyebrow">Mantenimiento</p>
          <h2>Purga de datos para pruebas en frío</h2>
        </div>
      </div>

      <section class="panel danger-zone">
        <div class="panel-heading">
          <h3>Eliminar todas las campañas</h3>
        </div>

        <p>
          Esta acción borra <strong>de forma permanente e irreversible</strong> toda la actividad de
          campañas para poder empezar a probar desde cero.
        </p>

        <div class="purga-detalle">
          <div>
            <h4>Se elimina</h4>
            <ul>
              <li>Todas las campañas (con sus preguntas y mensajes)</li>
              <li>Conversaciones y mensajes de WhatsApp</li>
              <li>Respuestas, evaluaciones y artefactos Markdown (y sus blobs)</li>
              <li>Participantes y registros de envío</li>
              <li>Usuarios <strong>no administrativos</strong> (rol Participante)</li>
            </ul>
          </div>
          <div>
            <h4>Se conserva</h4>
            <ul>
              <li>Usuarios administrativos (Admin y Visor)</li>
              <li>Configuraciones LLM</li>
              <li>Rúbricas</li>
              <li>Prompts</li>
              <li>Tags</li>
            </ul>
          </div>
        </div>

        @if (error()) {
          <p class="form-error">{{ error() }}</p>
        }

        <div class="field">
          <label for="confirmacion">
            Escribe <strong>{{ palabra }}</strong> para habilitar el botón
          </label>
          <input
            id="confirmacion"
            type="text"
            autocomplete="off"
            [value]="confirmacion()"
            (input)="confirmacion.set($any($event.target).value)"
            [disabled]="cargando()"
            placeholder="{{ palabra }}"
          />
        </div>

        <button
          type="button"
          class="danger-button"
          [disabled]="!puedeEliminar() || cargando()"
          (click)="purgar()"
        >
          {{ cargando() ? 'Eliminando…' : 'Eliminar todas las campañas' }}
        </button>

        @if (reporte(); as r) {
          <div class="purga-reporte">
            <h4>Purga completada</h4>
            <ul>
              <li>Campañas: {{ r.campanias }}</li>
              <li>Conversaciones: {{ r.conversaciones }} · Mensajes: {{ r.mensajes }}</li>
              <li>
                Respuestas: {{ r.respuestas }} · Evaluaciones: {{ r.evaluaciones }} · Artefactos:
                {{ r.artefactos }}
              </li>
              <li>Blobs borrados: {{ r.blobsBorrados }} · fallidos: {{ r.blobsFallidos }}</li>
              <li>Participantes: {{ r.participantes }}</li>
              <li>Usuarios borrados: {{ r.usuariosBorrados }}</li>
            </ul>
          </div>
        }
      </section>
    </section>
  `,
  styles: [
    `
      .danger-zone {
        border: 1px solid rgba(220, 38, 38, 0.4);
      }
      .purga-detalle {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
        gap: 1.5rem;
        margin: 1rem 0;
      }
      .purga-detalle h4 {
        margin: 0 0 0.5rem;
      }
      .field {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        max-width: 320px;
        margin-bottom: 1rem;
      }
      .danger-button {
        background: #dc2626;
        color: #fff;
        border: none;
        border-radius: 6px;
        padding: 0.6rem 1.1rem;
        cursor: pointer;
      }
      .danger-button:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
      .purga-reporte {
        margin-top: 1.25rem;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MantenimientoPage {
  private readonly api = inject(AdminApiService);
  private readonly notificaciones = inject(NotificacionesService);

  protected readonly palabra = PALABRA_CONFIRMACION;
  protected readonly confirmacion = signal('');
  protected readonly cargando = signal(false);
  protected readonly error = signal('');
  protected readonly reporte = signal<ReportePurgaCampanias | null>(null);
  protected readonly puedeEliminar = computed(
    () => this.confirmacion().trim() === PALABRA_CONFIRMACION,
  );

  purgar() {
    if (!this.puedeEliminar() || this.cargando()) {
      return;
    }

    const ok = window.confirm(
      'Vas a eliminar TODAS las campañas y datos asociados de forma permanente. ¿Continuar?',
    );
    if (!ok) {
      return;
    }

    this.cargando.set(true);
    this.error.set('');
    this.reporte.set(null);
    this.api.purgarCampanias(this.confirmacion().trim()).subscribe({
      next: (reporte) => {
        this.reporte.set(reporte);
        this.confirmacion.set('');
        this.cargando.set(false);
        this.notificaciones.exito(
          `Purga completada: ${reporte.campanias} campañas y ${reporte.usuariosBorrados} usuarios eliminados.`,
        );
      },
      error: (err: unknown) => {
        this.cargando.set(false);
        this.error.set(formatApiError(err));
      },
    });
  }
}
