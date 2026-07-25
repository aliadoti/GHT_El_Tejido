import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type TipoEstadoAccesible = 'error' | 'exito' | 'informacion';

/** Región viva persistente para mensajes que pertenecen a una pantalla o formulario. */
@Component({
  selector: 'app-estado-accesible',
  standalone: true,
  template: `
    <p
      [id]="estadoId()"
      class="estado-accesible"
      [class.form-error]="tipo() === 'error'"
      [class.notice]="tipo() !== 'error'"
      [attr.role]="tipo() === 'error' ? 'alert' : 'status'"
      [attr.aria-live]="tipo() === 'error' ? 'assertive' : 'polite'"
      aria-atomic="true"
    >
      {{ mensaje() }}
    </p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EstadoAccesibleComponent {
  readonly mensaje = input('');
  readonly tipo = input<TipoEstadoAccesible>('informacion');
  readonly estadoId = input<string | undefined>(undefined);
}
