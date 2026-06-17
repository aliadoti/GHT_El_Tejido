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
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (config of configs(); track config.id) {
                  <tr [class.row-selected]="editandoId() === config.id">
                    <td>{{ config.nombre }}</td>
                    <td>{{ config.proveedor }}</td>
                    <td>{{ config.modelo }}</td>
                    <td>{{ config.apiKeyMascara }}</td>
                    <td>
                      <span class="status-badge">{{ config.estado }}</span>
                    </td>
                    <td>
                      <button type="button" class="link-button" (click)="editar(config)">
                        Editar
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6" class="empty-cell">No hay configs LLM.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="panel">
          <div class="panel-heading">
            <h3>{{ editandoId() ? 'Editar configuracion' : 'Nueva configuracion' }}</h3>
            @if (editandoId()) {
              <button type="button" class="ghost-button" (click)="cancelarEdicion()">
                Cancelar
              </button>
            }
          </div>
          <form class="form-grid" (ngSubmit)="guardar()">
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
            <label>
              Estado
              <select name="estado" [(ngModel)]="form.estado">
                <option value="activo">activo</option>
                <option value="inactivo">inactivo</option>
              </select>
            </label>
            <p class="subhead">
              La API key NO se ingresa aqui. Carga la API key real en Key Vault como un secreto y
              escribe aqui su nombre. El secreto debe existir antes de guardar; si no, veras un
              error.
            </p>
            <button class="primary-button" type="submit">
              {{ editandoId() ? 'Guardar cambios' : 'Guardar configuracion' }}
            </button>
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
  protected readonly editandoId = signal<string | null>(null);
  private editandoOriginal: ConfigLlm | null = null;
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

  guardar() {
    if (this.editandoId()) {
      this.actualizar();
    } else {
      this.crear();
    }
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
        estado: this.form.estado,
      })
      .subscribe({
        next: () => {
          this.resetForm();
          this.load();
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  actualizar() {
    const id = this.editandoId();
    if (!id) {
      return;
    }

    // Los campos avanzados (parametros, limites, timeout, reintentos) no estan en el formulario:
    // se preservan los de la config original para no perderlos al editar lo visible.
    const original = this.editandoOriginal;
    this.api
      .actualizarConfigLlm(id, {
        nombre: this.form.nombre,
        proveedor: this.form.proveedor,
        modelo: this.form.modelo,
        endpoint: this.form.endpoint,
        apiKeyRef: this.form.apiKeyRef,
        parametros: original?.parametros ?? { temperature: 0.2 },
        limitesTokens: original?.limitesTokens ?? { maxPrompt: 6000, maxCompletion: 800 },
        timeoutSegundos: original?.timeoutSegundos ?? 30,
        maxReintentos: original?.maxReintentos ?? 2,
        estado: this.form.estado,
      })
      .subscribe({
        next: () => {
          this.resetForm();
          this.load();
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  editar(config: ConfigLlm) {
    this.editandoId.set(config.id);
    this.editandoOriginal = config;
    this.form = {
      preset: 'Otro',
      nombre: config.nombre,
      proveedor: config.proveedor,
      modelo: config.modelo,
      endpoint: config.endpoint,
      apiKeyRef: config.apiKeyRef,
      estado: config.estado === 'inactivo' ? 'inactivo' : 'activo',
    };
    this.error.set('');
  }

  cancelarEdicion() {
    this.resetForm();
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

  private resetForm() {
    this.editandoId.set(null);
    this.editandoOriginal = null;
    this.form = this.emptyForm();
  }

  private emptyForm() {
    return {
      preset: 'AzureOpenAI',
      nombre: 'Azure OpenAI',
      proveedor: 'AzureOpenAI',
      modelo: 'gpt-4o-mini',
      endpoint: 'https://<recurso>.openai.azure.com',
      apiKeyRef: 'llm-key',
      estado: 'activo',
    };
  }
}
