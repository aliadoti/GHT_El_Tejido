import { Injectable, signal } from '@angular/core';

export type TipoNotificacion = 'exito' | 'error' | 'info';

export interface Notificacion {
  id: number;
  tipo: TipoNotificacion;
  texto: string;
}

/**
 * Avisos efímeros para el administrador (confirmaciones de acciones, errores).
 * Señal global consumida por el toast del shell; cada aviso se descarta solo tras unos segundos.
 */
@Injectable({ providedIn: 'root' })
export class NotificacionesService {
  private static readonly DuracionMs = 4500;
  private contador = 0;
  readonly notificaciones = signal<Notificacion[]>([]);

  exito(texto: string): void {
    this.emitir('exito', texto);
  }

  error(texto: string): void {
    this.emitir('error', texto);
  }

  info(texto: string): void {
    this.emitir('info', texto);
  }

  descartar(id: number): void {
    this.notificaciones.update((items) => items.filter((item) => item.id !== id));
  }

  private emitir(tipo: TipoNotificacion, texto: string): void {
    const id = ++this.contador;
    this.notificaciones.update((items) => [...items, { id, tipo, texto }]);
    setTimeout(() => this.descartar(id), NotificacionesService.DuracionMs);
  }
}
