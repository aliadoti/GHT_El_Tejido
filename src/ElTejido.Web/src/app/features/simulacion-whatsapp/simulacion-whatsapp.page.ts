import { HttpClient, HttpHeaders } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { BrandSignatureComponent } from '../../layout/brand-signature.component';
import { formatApiError } from '../../shared-error';

interface AdminInicialResponse {
  id: string;
  nombre: string;
  whatsappNormalizado: string;
}

interface OtpResponse {
  numero: string;
  codigo: string;
  expiracion: string;
  intentos: number;
}

@Component({
  selector: 'app-simulacion-whatsapp-page',
  standalone: true,
  imports: [FormsModule, RouterLink, BrandSignatureComponent],
  template: `
    <section class="login-shell simulator-shell">
      <div class="simulator-page">
        <header class="section-header">
          <div>
            <p class="eyebrow">Development</p>
            <h1>Simulacion WhatsApp</h1>
          </div>
          <div class="actions-row">
            <a class="ghost-button" routerLink="/login">Login</a>
            <a class="ghost-button" routerLink="/">Portal</a>
          </div>
        </header>

        @if (error()) {
          <p class="form-error">{{ error() }}</p>
        }
        @if (notice()) {
          <p class="notice">{{ notice() }}</p>
        }

        <section class="panel">
          <div class="panel-heading">
            <h2>Clave de diagnostico</h2>
          </div>
          <p class="subhead">
            Solo para probar contra el despliegue (Azure). En local dejala vacia. Debe coincidir con
            la clave configurada en el servidor (Diagnostico:Clave o el secreto diag-key).
          </p>
          <form class="form-grid">
            <label>
              X-Diag-Key
              <input name="diagKey" [(ngModel)]="diagKey" type="password" autocomplete="off" />
            </label>
          </form>
        </section>

        <div class="two-column">
          <section class="panel">
            <div class="panel-heading">
              <h2>Acceso administrador</h2>
              @if (otp(); as currentOtp) {
                <span class="status-badge">{{ currentOtp.codigo }}</span>
              }
            </div>

            <form class="form-grid" (ngSubmit)="crearAdmin()">
              <label>
                Numero admin
                <input name="adminNumero" [(ngModel)]="adminNumero" placeholder="573001119999" />
              </label>
              <label>
                Nombre
                <input name="adminNombre" [(ngModel)]="adminNombre" />
              </label>
              <button class="primary-button" type="submit">Crear admin inicial</button>
            </form>

            <form class="form-grid" (ngSubmit)="emitirOtp()">
              <label>
                Codigo OTP
                <input name="codigoOtp" [(ngModel)]="codigoOtp" maxlength="6" />
              </label>
              <button class="ghost-button" type="submit">Emitir OTP de prueba</button>
            </form>

            @if (admin(); as currentAdmin) {
              <dl class="sim-kv">
                <div>
                  <dt>ID</dt>
                  <dd>{{ currentAdmin.id }}</dd>
                </div>
                <div>
                  <dt>Numero</dt>
                  <dd>{{ currentAdmin.whatsappNormalizado }}</dd>
                </div>
              </dl>
            }
          </section>

          <section class="panel">
            <div class="panel-heading">
              <h2>Mensaje entrante</h2>
              <span class="status-badge">{{ lastStatus() || 'listo' }}</span>
            </div>

            <form class="form-grid" (ngSubmit)="enviarWebhook()">
              <label>
                App secret
                <input
                  name="appSecret"
                  [(ngModel)]="appSecret"
                  type="password"
                  autocomplete="off"
                />
              </label>
              <label>
                Numero participante
                <input name="participanteNumero" [(ngModel)]="participanteNumero" />
              </label>
              <label>
                WhatsApp message ID
                <input name="wamid" [(ngModel)]="wamid" />
              </label>
              <label>
                Texto
                <textarea name="texto" [(ngModel)]="texto" rows="5"></textarea>
              </label>
              <button class="primary-button" type="submit">Enviar webhook firmado</button>
            </form>
          </section>
        </div>

        <section class="panel">
          <div class="panel-heading">
            <h2>Payload</h2>
            <button type="button" class="ghost-button" (click)="refrescarWamid()">Nuevo ID</button>
          </div>
          <pre class="markdown-preview">{{ payloadPreview() }}</pre>
        </section>

        <app-brand-signature />
      </div>
    </section>
  `,
  styles: [
    `
      .simulator-shell {
        place-items: stretch;
      }

      .simulator-page {
        display: grid;
        gap: 18px;
        width: min(1180px, 100%);
        margin: 0 auto;
      }

      .sim-kv {
        display: grid;
        gap: 8px;
        margin: 0;
      }

      .sim-kv div {
        display: grid;
        grid-template-columns: 120px 1fr;
        gap: 10px;
        border-top: 1px solid var(--ght-borde);
        padding-top: 8px;
      }

      .sim-kv dt {
        color: var(--ght-texto-secundario);
        font-weight: 800;
      }

      .sim-kv dd {
        margin: 0;
        overflow-wrap: anywhere;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SimulacionWhatsappPage {
  private readonly http = inject(HttpClient);

  protected diagKey = '';
  protected adminNumero = '573001119999';
  protected adminNombre = 'Administrador prueba';
  protected codigoOtp = '123456';
  protected appSecret = 'appsec-local';
  protected participanteNumero = '573001112233';
  protected wamid = this.nuevoWamid();
  protected texto = 'Mi idea es reducir desperdicio operativo.';

  protected readonly admin = signal<AdminInicialResponse | null>(null);
  protected readonly otp = signal<OtpResponse | null>(null);
  protected readonly notice = signal('');
  protected readonly error = signal('');
  protected readonly lastStatus = signal('');

  crearAdmin() {
    this.http
      .post<AdminInicialResponse>(
        '/diagnostico/simulacion/admin-inicial',
        {
          numero: this.adminNumero,
          nombre: this.adminNombre,
          area: 'Administracion',
          empresa: 'GHT',
        },
        { headers: this.diagHeaders() },
      )
      .subscribe({
        next: (admin) => {
          this.admin.set(admin);
          this.notice.set(`Admin listo: ${admin.whatsappNormalizado}`);
          this.error.set('');
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  emitirOtp() {
    this.http
      .post<OtpResponse>(
        '/diagnostico/simulacion/otp-admin',
        {
          numero: this.adminNumero,
          codigo: this.codigoOtp,
        },
        { headers: this.diagHeaders() },
      )
      .subscribe({
        next: (otp) => {
          this.otp.set(otp);
          this.notice.set(`OTP emitido hasta ${otp.expiracion}`);
          this.error.set('');
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  async enviarWebhook() {
    const payload = this.payloadPreview();
    const signature = await this.hmacSha256(this.appSecret, payload);
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
      'X-Hub-Signature-256': `sha256=${signature}`,
    });

    this.http.post('/webhook/whatsapp', payload, { headers, observe: 'response' }).subscribe({
      next: (response) => {
        this.lastStatus.set(String(response.status));
        this.notice.set('Webhook aceptado');
        this.error.set('');
        this.refrescarWamid();
      },
      error: (err: unknown) => {
        this.lastStatus.set('error');
        this.error.set(formatApiError(err));
      },
    });
  }

  refrescarWamid() {
    this.wamid = this.nuevoWamid();
  }

  payloadPreview() {
    return JSON.stringify(
      {
        entry: [
          {
            changes: [
              {
                value: {
                  messages: [
                    {
                      from: this.participanteNumero,
                      id: this.wamid,
                      timestamp: String(Math.floor(Date.now() / 1000)),
                      type: 'text',
                      text: { body: this.texto },
                    },
                  ],
                },
              },
            ],
          },
        ],
      },
      null,
      2,
    );
  }

  private diagHeaders() {
    const clave = this.diagKey.trim();
    return clave ? new HttpHeaders({ 'X-Diag-Key': clave }) : new HttpHeaders();
  }

  private async hmacSha256(secret: string, value: string) {
    const encoder = new TextEncoder();
    const key = await crypto.subtle.importKey(
      'raw',
      encoder.encode(secret),
      { name: 'HMAC', hash: 'SHA-256' },
      false,
      ['sign'],
    );
    const signature = await crypto.subtle.sign('HMAC', key, encoder.encode(value));
    return Array.from(new Uint8Array(signature))
      .map((byte) => byte.toString(16).padStart(2, '0'))
      .join('');
  }

  private nuevoWamid() {
    return `wamid.sim.${Date.now()}`;
  }
}
