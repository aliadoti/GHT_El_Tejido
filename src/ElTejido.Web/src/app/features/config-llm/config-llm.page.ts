import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AdminApiService } from '../../core/admin-api.service';
import { ConfigLlm } from '../../core/api-models';
import { formatApiError } from '../../shared-error';

@Component({
  selector: 'app-config-llm-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <p class="eyebrow">REQ 19</p>
          <h2>Configuracion LLM</h2>
        </div>
        <button type="button" class="ghost-button" (click)="load()">Actualizar</button>
      </div>

      @if (error()) {
        <p class="form-error">{{ error() }}</p>
      }

      <div class="two-column">
        <section class="panel">
          <div class="panel-heading"><h3>Configuraciones</h3></div>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Proveedor</th>
                  <th>Modelo</th>
                  <th>Key</th>
                  <th>Estado</th>
                </tr>
              </thead>
              <tbody>
                @for (config of configs(); track config.id) {
                  <tr>
                    <td>{{ config.nombre }}</td>
                    <td>{{ config.proveedor }}</td>
                    <td>{{ config.modelo }}</td>
                    <td>{{ config.apiKeyMascara }}</td>
                    <td>
                      <span class="status-badge">{{ config.estado }}</span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5" class="empty-cell">No hay configs LLM.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="panel">
          <div class="panel-heading"><h3>Nueva configuracion</h3></div>
          <form class="form-grid" (ngSubmit)="crear()">
            <label>Nombre <input name="nombre" [(ngModel)]="form.nombre" /></label>
            <label>Proveedor <input name="proveedor" [(ngModel)]="form.proveedor" /></label>
            <label>Modelo <input name="modelo" [(ngModel)]="form.modelo" /></label>
            <label>Endpoint <input name="endpoint" [(ngModel)]="form.endpoint" /></label>
            <label>
              Nombre del secreto (apiKeyRef)
              <input name="apiKeyRef" [(ngModel)]="form.apiKeyRef" placeholder="llm-key" />
            </label>
            <p class="subhead">
              La API key NO se ingresa aqui. Carga la API key real en Key Vault como un secreto y
              escribe aqui su nombre. El secreto debe existir antes de guardar; si no, veras un
              error.
            </p>
            <button class="primary-button" type="submit">Guardar configuracion</button>
          </form>
        </section>
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfigLlmPage {
  private readonly api = inject(AdminApiService);
  protected readonly configs = signal<ConfigLlm[]>([]);
  protected readonly error = signal('');
  protected form = this.emptyForm();

  constructor() {
    this.load();
  }

  load() {
    this.api.configsLlm({ pageSize: 50 }).subscribe({
      next: (page) => this.configs.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  crear() {
    this.api
      .crearConfigLlm({
        ...this.form,
        parametros: { temperature: 0.2 },
        limitesTokens: { maxPrompt: 6000, maxCompletion: 800 },
        timeoutSegundos: 30,
        maxReintentos: 2,
        estado: 'activo',
      })
      .subscribe({
        next: () => {
          this.form = this.emptyForm();
          this.load();
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  private emptyForm() {
    return {
      nombre: 'Azure OpenAI',
      proveedor: 'AzureOpenAI',
      modelo: 'gpt-4o-mini',
      endpoint: 'https://example.openai.azure.com',
      apiKeyRef: 'llm-key',
    };
  }
}
