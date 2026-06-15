import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';

import { AuthService } from '../../core/auth.service';
import { BrandSignatureComponent } from '../../layout/brand-signature.component';
import { formatApiError } from '../../shared-error';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [FormsModule, BrandSignatureComponent],
  template: `
    <main class="login-shell">
      <section class="login-panel" aria-labelledby="login-title">
        <div class="login-brand">
          <span class="brand-mark">GHT</span>
          <span>
            <strong>El Tejido</strong>
            <small>Portal administrativo</small>
          </span>
        </div>

        <div class="thread-map" aria-hidden="true">
          <span></span><span></span><span></span><span></span>
        </div>

        <h1 id="login-title">Ingreso con OTP de WhatsApp</h1>
        <p>
          Usa el numero normalizado con prefijo de pais, sin espacios ni simbolos. Ejemplo:
          <strong>573001119999</strong>.
        </p>

        <form class="form-grid" (ngSubmit)="step() === 'numero' ? requestCode() : verifyCode()">
          <label>
            Numero WhatsApp
            <input
              name="numero"
              inputmode="numeric"
              autocomplete="tel"
              [(ngModel)]="numero"
              placeholder="573001119999"
              required
            />
          </label>

          @if (step() === 'codigo') {
            <label>
              Codigo OTP
              <input
                name="codigo"
                inputmode="numeric"
                autocomplete="one-time-code"
                [(ngModel)]="codigo"
                placeholder="482913"
                required
              />
            </label>
          }

          @if (message()) {
            <p class="notice">{{ message() }}</p>
          }
          @if (error()) {
            <p class="form-error">{{ error() }}</p>
          }

          <button class="primary-button" type="submit" [disabled]="loading()">
            {{ step() === 'numero' ? 'Enviar codigo' : 'Verificar codigo' }}
          </button>
          @if (step() === 'codigo') {
            <button class="ghost-button" type="button" (click)="step.set('numero')">
              Cambiar numero
            </button>
          }
        </form>

        <app-brand-signature class="login-signature" />
      </section>
    </main>
  `,
  styles: [
    `
      .login-signature {
        border-top: 1px solid var(--ght-borde);
        padding-top: 4px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected numero = '';
  protected codigo = '';
  protected readonly step = signal<'numero' | 'codigo'>('numero');
  protected readonly loading = signal(false);
  protected readonly message = signal('');
  protected readonly error = signal('');

  requestCode() {
    this.error.set('');
    this.loading.set(true);
    this.auth
      .requestCode(this.numero)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response) => {
          this.message.set(response.message);
          this.step.set('codigo');
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  verifyCode() {
    this.error.set('');
    this.loading.set(true);
    this.auth
      .verifyCode(this.numero, this.codigo)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => void this.router.navigateByUrl('/'),
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }
}
