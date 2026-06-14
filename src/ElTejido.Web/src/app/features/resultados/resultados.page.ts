import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { AdminApiService } from '../../core/admin-api.service';
import {
  ArtefactoMarkdown,
  Campania,
  Conversacion,
  Evaluacion,
  Respuesta,
} from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { formatApiError } from '../../shared-error';

@Component({
  selector: 'app-resultados-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <p class="eyebrow">REQ 27.3</p>
          <h2>Resultados, evaluaciones y Markdown</h2>
        </div>
        <button type="button" class="ghost-button" (click)="loadAll()">Actualizar</button>
      </div>

      @if (error()) {
        <p class="form-error">{{ error() }}</p>
      }

      <section class="panel">
        <form class="filters-grid" (ngSubmit)="loadAll()">
          <label>
            Campania
            <select name="campaniaId" [(ngModel)]="campaniaId">
              <option value="" disabled>Selecciona una campania</option>
              @for (campania of campanias(); track campania.id) {
                <option [value]="campania.id">{{ campania.nombre }}</option>
              }
            </select>
          </label>
          <button class="primary-button" type="submit">Consultar resultados</button>
        </form>
      </section>

      <div class="three-column">
        <section class="panel">
          <div class="panel-heading">
            <h3>Conversaciones</h3>
            <span class="muted">{{ conversaciones().length }}</span>
          </div>
          <ul class="compact-list">
            @for (conv of conversaciones(); track conv.id) {
              <li>
                <strong>{{ conv.usuarioId }}</strong>
                <span>{{ conv.estado }} / {{ conv.estadoMaquina }}</span>
              </li>
            } @empty {
              <li class="muted">Sin conversaciones.</li>
            }
          </ul>
        </section>

        <section class="panel">
          <div class="panel-heading">
            <h3>Respuestas</h3>
            <span class="muted">{{ respuestas().length }}</span>
          </div>
          <ul class="compact-list">
            @for (respuesta of respuestas(); track respuesta.id) {
              <li>
                <button type="button" class="link-button" (click)="abrirRespuesta(respuesta.id)">
                  {{ respuesta.usuarioId }}
                </button>
                <span>{{ respuesta.texto }}</span>
              </li>
            } @empty {
              <li class="muted">Sin respuestas.</li>
            }
          </ul>
        </section>

        <section class="panel">
          <div class="panel-heading">
            <h3>Markdown</h3>
            <span class="muted">{{ artefactos().length }}</span>
          </div>
          <ul class="compact-list">
            @for (artefacto of artefactos(); track artefacto.id) {
              <li>
                <button type="button" class="link-button" (click)="abrirMarkdown(artefacto.id)">
                  {{ artefacto.id }}
                </button>
                <span>v{{ artefacto.version }} - {{ artefacto.estado }}</span>
              </li>
            } @empty {
              <li class="muted">Sin artefactos.</li>
            }
          </ul>
        </section>
      </div>

      @if (evaluacion(); as detalle) {
        <section class="panel">
          <div class="panel-heading">
            <h3>Evaluacion seleccionada</h3>
            <span class="status-badge">{{ detalle.recomendacion }}</span>
          </div>
          <div class="detail-grid">
            <div>
              <span class="muted">Calificacion</span>
              <strong class="score">{{ detalle.calificacionTotal }}</strong>
            </div>
            <div>
              <span class="muted">Temas</span>
              <p>{{ detalle.temas.join(', ') || '-' }}</p>
            </div>
            <div class="wide">
              <span class="muted">Explicacion</span>
              <p>{{ detalle.explicacion }}</p>
            </div>
          </div>
        </section>
      }

      @if (markdown(); as selectedMarkdown) {
        <section class="panel">
          <div class="panel-heading">
            <h3>Markdown generado</h3>
            <div class="actions-row">
              <button
                type="button"
                class="ghost-button"
                [disabled]="!auth.isAdmin()"
                (click)="regenerar(selectedMarkdown.id)"
              >
                Regenerar
              </button>
              <button type="button" class="ghost-button" (click)="descargar(selectedMarkdown)">
                Descargar .md
              </button>
            </div>
          </div>
          <pre class="markdown-preview">{{ selectedMarkdown.contenidoMarkdown }}</pre>
        </section>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResultadosPage {
  private readonly api = inject(AdminApiService);
  protected readonly auth = inject(AuthService);
  protected readonly conversaciones = signal<Conversacion[]>([]);
  protected readonly respuestas = signal<Respuesta[]>([]);
  protected readonly artefactos = signal<ArtefactoMarkdown[]>([]);
  protected readonly evaluacion = signal<Evaluacion | null>(null);
  protected readonly markdown = signal<ArtefactoMarkdown | null>(null);
  protected readonly campanias = signal<Campania[]>([]);
  protected readonly error = signal('');
  protected campaniaId = '';

  constructor() {
    this.api.campanias({ pageSize: 100 }).subscribe({
      next: (page) => this.campanias.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  loadAll() {
    if (!this.campaniaId) {
      this.error.set('Ingresa campaniaId para consultar resultados.');
      return;
    }

    this.api.conversaciones(this.campaniaId).subscribe({
      next: (page) => this.conversaciones.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
    this.api.respuestas(this.campaniaId).subscribe({
      next: (page) => this.respuestas.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
    this.api.markdown(this.campaniaId).subscribe({
      next: (page) => this.artefactos.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  abrirRespuesta(id: string) {
    this.api.respuesta(this.campaniaId, id).subscribe({
      next: (detalle) => this.evaluacion.set(detalle.evaluacion),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  abrirMarkdown(id: string) {
    this.api.markdownDetalle(this.campaniaId, id).subscribe({
      next: (detalle) => this.markdown.set(detalle),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  regenerar(id: string) {
    this.api.regenerarMarkdown(this.campaniaId, id).subscribe({
      next: (detalle) => {
        this.markdown.set(detalle);
        this.loadAll();
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  descargar(artefacto: ArtefactoMarkdown) {
    const blob = new Blob([artefacto.contenidoMarkdown ?? ''], {
      type: 'text/markdown;charset=utf-8',
    });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${artefacto.id}.md`;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
