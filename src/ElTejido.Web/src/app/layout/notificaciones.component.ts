import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { NotificacionesService } from '../core/notificaciones.service';

/** Toast de avisos del portal (confirmaciones/errores). Se monta una vez en el shell. */
@Component({
  selector: 'app-notificaciones',
  standalone: true,
  template: `
    <div class="toast-stack" aria-live="polite" aria-atomic="false">
      @for (n of servicio.notificaciones(); track n.id) {
        <div
          class="toast"
          [class.toast-exito]="n.tipo === 'exito'"
          [class.toast-error]="n.tipo === 'error'"
        >
          <span>{{ n.texto }}</span>
          <button
            type="button"
            class="toast-close"
            aria-label="Descartar"
            (click)="servicio.descartar(n.id)"
          >
            ×
          </button>
        </div>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificacionesComponent {
  protected readonly servicio = inject(NotificacionesService);
}
