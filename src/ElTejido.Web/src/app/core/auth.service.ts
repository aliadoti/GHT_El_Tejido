import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, map, of, tap } from 'rxjs';

import { ApiClient } from './api-client.service';
import { MeResponse, SesionResponse, UsuarioSesion } from './api-models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  private readonly usuarioSignal = signal<UsuarioSesion | null>(null);
  private readonly csrfSignal = signal<string | null>(null);
  private readonly checkedSignal = signal(false);

  readonly usuario = this.usuarioSignal.asReadonly();
  readonly csrfToken = this.csrfSignal.asReadonly();
  readonly checked = this.checkedSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.usuarioSignal() !== null);
  readonly isAdmin = computed(() => this.usuarioSignal()?.rol === 'admin');

  requestCode(numero: string) {
    return this.api.post<{ message: string }>('/api/auth/request-code', { numero });
  }

  verifyCode(numero: string, codigo: string) {
    return this.api.post<SesionResponse>('/api/auth/verify-code', { numero, codigo }).pipe(
      tap((sesion) => {
        this.usuarioSignal.set(sesion.usuario);
        this.csrfSignal.set(sesion.csrfToken);
        this.checkedSignal.set(true);
      }),
    );
  }

  me() {
    return this.api.get<MeResponse>('/api/auth/me').pipe(
      tap((response) => {
        this.usuarioSignal.set(response.usuario);
        this.checkedSignal.set(true);
      }),
      map(() => true),
      catchError(() => {
        this.clearSession();
        this.checkedSignal.set(true);
        return of(false);
      }),
    );
  }

  logout() {
    return this.api.post<void>('/api/auth/logout').pipe(
      tap(() => {
        this.clearSession();
        void this.router.navigateByUrl('/login');
      }),
    );
  }

  clearSession() {
    this.usuarioSignal.set(null);
    this.csrfSignal.set(null);
  }
}
