import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { throwError } from 'rxjs';

import { AuthService } from '../core/auth.service';
import { NotificacionesService } from '../core/notificaciones.service';
import { LoginPage } from '../features/auth/login.page';
import { EstadoAccesibleComponent } from '../shared/estado-accesible.component';
import { NotificacionesComponent } from './notificaciones.component';

describe('estados accesibles', () => {
  it('actualiza una región viva con la prioridad correcta para error y éxito', () => {
    const fixture = TestBed.createComponent(EstadoAccesibleComponent);
    fixture.componentRef.setInput('mensaje', 'No fue posible guardar');
    fixture.componentRef.setInput('tipo', 'error');
    fixture.detectChanges();

    const estado = fixture.nativeElement.querySelector('p') as HTMLParagraphElement;
    expect(estado.getAttribute('role')).toBe('alert');
    expect(estado.getAttribute('aria-live')).toBe('assertive');
    expect(estado.getAttribute('aria-atomic')).toBe('true');
    expect(estado.textContent?.trim()).toBe('No fue posible guardar');

    fixture.componentRef.setInput('mensaje', 'Cambios guardados');
    fixture.componentRef.setInput('tipo', 'exito');
    fixture.detectChanges();

    expect(estado.getAttribute('role')).toBe('status');
    expect(estado.getAttribute('aria-live')).toBe('polite');
    expect(estado.textContent?.trim()).toBe('Cambios guardados');
  });

  it('anuncia cada aviso del portal desde una sola región con su prioridad', () => {
    const avisos = signal([
      { id: 1, tipo: 'exito' as const, texto: 'Cambios guardados' },
      { id: 2, tipo: 'error' as const, texto: 'No fue posible guardar' },
    ]);
    TestBed.configureTestingModule({
      imports: [NotificacionesComponent],
      providers: [
        {
          provide: NotificacionesService,
          useValue: { notificaciones: avisos, descartar: () => {} },
        },
      ],
    });

    const fixture = TestBed.createComponent(NotificacionesComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const toasts = element.querySelectorAll<HTMLElement>('.toast');

    expect(toasts).toHaveLength(2);
    expect(toasts[0].getAttribute('role')).toBe('status');
    expect(toasts[0].getAttribute('aria-live')).toBe('polite');
    expect(toasts[1].getAttribute('role')).toBe('alert');
    expect(toasts[1].getAttribute('aria-live')).toBe('assertive');
    expect(element.querySelector('.toast-stack')?.hasAttribute('aria-live')).toBe(false);
  });

  it('asocia el error de ingreso con el campo activo sin mover el foco', () => {
    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        {
          provide: AuthService,
          useValue: {
            requestCode: () => throwError(() => ({ error: { message: 'Número no válido' } })),
          },
        },
        { provide: Router, useValue: { navigateByUrl: () => Promise.resolve(true) } },
      ],
    });

    const fixture = TestBed.createComponent(LoginPage);
    const component = fixture.componentInstance as unknown as {
      numero: string;
      requestCode(): void;
    };
    component.numero = '573001119999';
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    const numero = element.querySelector<HTMLInputElement>('input[name="numero"]');
    if (!numero) {
      throw new Error('No se encontró el campo de número del ingreso.');
    }
    numero.focus();
    component.requestCode();
    fixture.detectChanges();

    const error = element.querySelector<HTMLElement>('#login-error');
    if (!error) {
      throw new Error('No se encontró la región de error del ingreso.');
    }
    expect(numero).toBe(document.activeElement);
    expect(numero.getAttribute('aria-invalid')).toBe('true');
    expect(numero.getAttribute('aria-describedby')).toBe('login-error');
    expect(error.getAttribute('role')).toBe('alert');
    expect(error.textContent?.trim()).not.toBe('');
  });
});
