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
            <label>
              Preset de proveedor
              <select
                name="presetProveedor"
                [(ngModel)]="form.preset"
                (ngModelChange)="aplicarPreset($event)"
              >
                @for (preset of presets; track preset.id) {
                  <option [value]="preset.id">{{ preset.label }}</option>
                }
              </select>
            </label>
            <label>Proveedor <input name="proveedor" [(ngModel)]="form.proveedor" /></label>
            <label>Modelo <input name="modelo" [(ngModel)]="form.modelo" /></label>
            <p class="subhead">{{ modeloHint() }}</p>
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
  protected readonly presets = [
    {
      id: 'AzureOpenAI',
      label: 'Azure OpenAI',
      proveedor: 'AzureOpenAI',
      endpoint: 'https://<recurso>.openai.azure.com',
      modelo: 'gpt-4o-mini',
      hint: 'Usa el nombre del deployment de Azure OpenAI.',
    },
    {
      id: 'OpenAI',
      label: 'OpenAI',
      proveedor: 'OpenAI',
      endpoint: 'https://api.openai.com/v1',
      modelo: 'gpt-4o-mini',
      hint: 'Usa el id publico del modelo de OpenAI.',
    },
    {
      id: 'OpenRouter',
      label: 'OpenRouter',
      proveedor: 'OpenRouter',
      endpoint: 'https://openrouter.ai/api/v1',
      modelo: 'openai/gpt-4o-mini',
      hint: 'Usa el formato proveedor/modelo de OpenRouter.',
    },
    {
      id: 'AnthropicOpenRouter',
      label: 'Anthropic via OpenRouter',
      proveedor: 'Anthropic-via-OpenRouter',
      endpoint: 'https://openrouter.ai/api/v1',
      modelo: 'anthropic/claude-3.5-sonnet',
      hint: 'Usa un modelo Anthropic publicado en OpenRouter.',
    },
    {
      id: 'Anthropic',
      label: 'Anthropic nativo',
      proveedor: 'Anthropic',
      endpoint: 'https://api.anthropic.com',
      modelo: 'claude-3-5-sonnet-latest',
      hint: 'Usa el id de modelo de Anthropic; el backend llama /v1/messages.',
    },
    {
      id: 'Otro',
      label: 'Otro compatible OpenAI',
      proveedor: 'Otro',
      endpoint: 'https://api.example.com/v1',
      modelo: 'modelo',
      hint: 'Debe exponer /chat/completions compatible con OpenAI.',
    },
  ] as const;
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
        nombre: this.form.nombre,
        proveedor: this.form.proveedor,
        modelo: this.form.modelo,
        endpoint: this.form.endpoint,
        apiKeyRef: this.form.apiKeyRef,
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

  aplicarPreset(presetId: string) {
    const preset = this.presets.find((item) => item.id === presetId);
    if (!preset) {
      return;
    }

    this.form.proveedor = preset.proveedor;
    this.form.endpoint = preset.endpoint;
    this.form.modelo = preset.modelo;
    this.form.nombre = preset.label;
  }

  modeloHint() {
    return this.presets.find((preset) => preset.id === this.form.preset)?.hint ?? '';
  }

  private emptyForm() {
    return {
      preset: 'AzureOpenAI',
      nombre: 'Azure OpenAI',
      proveedor: 'AzureOpenAI',
      modelo: 'gpt-4o-mini',
      endpoint: 'https://<recurso>.openai.azure.com',
      apiKeyRef: 'llm-key',
    };
  }
}
