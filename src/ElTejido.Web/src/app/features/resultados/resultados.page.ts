import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

import { AdminApiService } from '../../core/admin-api.service';
import {
  ArtefactoMarkdown,
  Campania,
  Conversacion,
  DetalleIdea,
  Evaluacion,
  IdeaConsolidada,
  Respuesta,
  UsuarioAdmin,
} from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { EstadoAccesibleComponent } from '../../shared/estado-accesible.component';
import { formatApiError } from '../../shared-error';
import { ResultadosSesionService } from './resultados-sesion.service';

@Component({
  selector: 'app-resultados-page',
  standalone: true,
  imports: [FormsModule, EstadoAccesibleComponent],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <h2>Resultados, evaluaciones y Markdown</h2>
          <p class="muted">Elige una campaña para revisar sus ideas, evaluaciones y documentos.</p>
        </div>
        <button type="button" class="ghost-button" (click)="loadAll()">Actualizar</button>
      </div>

      <app-estado-accesible tipo="error" [mensaje]="error()" />
      <app-estado-accesible tipo="informacion" [mensaje]="informacion()" />

      <section class="panel">
        <div class="filters-grid">
          <label>
            Campaña
            <select name="campaniaId" [(ngModel)]="campaniaId" (ngModelChange)="cambiarCampania()">
              <option value="" disabled>Selecciona una campaña</option>
              @for (campania of campanias(); track campania.id) {
                <option [value]="campania.id">{{ campania.nombre }}</option>
              }
            </select>
          </label>
          <label>
            Estado de la idea
            <select name="estadoIdea" [(ngModel)]="estadoIdeaFiltro" (ngModelChange)="loadAll()">
              <option value="">Todas</option>
              <option value="madura">Maduras</option>
              <option value="pendiente">Pendientes</option>
              <option value="rechazada">Rechazadas</option>
            </select>
          </label>
          <div class="resultados-resumen" aria-label="Resumen de ideas">
            <strong>{{ ideas().length }} ideas</strong>
            <span>
              {{ conteoIdeas('madura') }} maduras · {{ conteoIdeas('pendiente') }} pendientes ·
              {{ conteoIdeas('rechazada') }} rechazadas
            </span>
          </div>
          <div class="resultados-leyenda" aria-label="Leyenda de estados">
            <span class="status-badge badge-ok">Maduras</span>
            <span class="status-badge">Pendientes</span>
            <span class="status-badge badge-warn">Rechazadas</span>
            <span class="status-badge">En curso</span>
          </div>
        </div>
      </section>

      @if (!campaniaId && !cargando()) {
        <section class="panel empty-state">
          <h3>Elige una campaña</h3>
          <p>
            Cuando haya una campaña disponible, podrás revisar aquí sus respuestas y documentos.
          </p>
        </section>
      } @else {
        <div class="resultados-master-detail">
          <section class="panel">
            <div class="panel-heading">
              <h3>Ideas</h3>
              <span class="muted">{{ ideas().length }}</span>
            </div>
            @if (cargando()) {
              <div class="resultados-skeleton" aria-label="Cargando ideas">
                <span></span><span></span><span></span>
              </div>
            } @else {
              <ul class="compact-list resultados-lista-maestra">
                @for (idea of ideas(); track idea.id) {
                  <li [class.selected-row]="ideaSeleccionada()?.id === idea.id">
                    <button
                      type="button"
                      class="resultados-idea"
                      [attr.aria-current]="ideaSeleccionada()?.id === idea.id ? 'true' : null"
                      (click)="abrirIdea(idea.id)"
                    >
                      <span class="resultados-respuesta-titulo">
                        <strong>{{ nombreUsuario(idea.usuarioId) }}</strong>
                        <span
                          class="status-badge"
                          [class.badge-ok]="idea.estadoResultado === 'madura'"
                          [class.badge-warn]="idea.estadoResultado === 'rechazada'"
                          [title]="tituloEstadoIdea(idea)"
                        >
                          {{ etiquetaEstadoIdea(idea) }}
                        </span>
                        @if (marcaFlujoIdea(idea); as marca) {
                          <span class="status-badge">{{ marca }}</span>
                        }
                        @if (idea.estadoCuraduria === 'pendiente') {
                          <span
                            class="status-badge"
                            title="Ninguna idea pasa automáticamente: queda pendiente de curaduría"
                          >
                            pendiente de curaduría
                          </span>
                        }
                      </span>
                      <span class="resultados-extracto">{{ extracto(idea.texto ?? '') }}</span>
                    </button>
                  </li>
                } @empty {
                  <li class="muted">
                    Esta campaña aún no tiene ideas con ese filtro. Cambia el estado o revisa que la
                    campaña haya recibido mensajes.
                  </li>
                }
              </ul>
            }
          </section>

          <section class="panel resultados-detalle">
            @if (cargandoDetalle()) {
              <div class="resultados-skeleton" aria-label="Cargando detalle">
                <span></span><span></span>
              </div>
            } @else if (detalleIdea(); as detalle) {
              <div class="panel-heading">
                <h3>Detalle de {{ nombreUsuario(detalle.idea.usuarioId) }}</h3>
                <span
                  class="status-badge"
                  [class.badge-ok]="detalle.idea.estadoResultado === 'madura'"
                >
                  {{ etiquetaEstadoIdea(detalle.idea) }}
                </span>
              </div>

              <section aria-labelledby="idea-consolidada">
                <h4 id="idea-consolidada">Idea consolidada</h4>
                @if (!detalle.idea.confirmada) {
                  <p class="muted">
                    Esta versión todavía no fue confirmada por el participante, así que no puede
                    contar como madura.
                  </p>
                }
                <p>{{ detalle.idea.texto ?? 'Sin versión consolidada todavía.' }}</p>
                <div class="detail-grid">
                  <div>
                    <span class="muted">Estado</span>
                    <p>{{ etiquetaEstadoIdea(detalle.idea) }}</p>
                  </div>
                  <div>
                    <span class="muted">Motivo de cierre</span>
                    <p>{{ detalle.idea.motivoCierre ?? '-' }}</p>
                  </div>
                  <div>
                    <span class="muted">Curaduría</span>
                    <p>
                      {{
                        detalle.idea.estadoCuraduria === 'pendiente'
                          ? 'Pendiente de curaduría'
                          : 'No aplica'
                      }}
                    </p>
                  </div>
                </div>
              </section>

              <section aria-labelledby="evaluacion-idea">
                <h4 id="evaluacion-idea">Evaluación de la versión vigente</h4>
                @if (detalle.evaluacion; as evaluacionIdea) {
                  <div class="detail-grid">
                    <div>
                      <span class="muted">Calificación</span>
                      <strong class="score">{{ evaluacionIdea.calificacionTotal }}</strong>
                    </div>
                    <div>
                      <span class="muted">Temas</span>
                      <p>{{ evaluacionIdea.temas.join(', ') || '-' }}</p>
                    </div>
                    <div class="wide">
                      <span class="muted">Retroalimentación enviada</span>
                      <p>{{ evaluacionIdea.retroalimentacionEnviada }}</p>
                    </div>
                    <div class="wide">
                      <span class="muted">Explicación</span>
                      <p>{{ evaluacionIdea.explicacion }}</p>
                    </div>
                  </div>
                } @else {
                  <p class="muted">
                    Esta idea todavía no tiene una evaluación de su versión vigente.
                  </p>
                }
              </section>

              <details class="resultados-historial">
                <summary>
                  Historial de la idea ({{ detalle.aportes.length }} aportes ·
                  {{ detalle.versiones.length }} versiones)
                </summary>
                <h5>Aportes originales</h5>
                <ul class="compact-list">
                  @for (aporte of detalle.aportes; track aporte.id) {
                    <li>
                      <span class="status-badge">{{ aporte.tipoAporte ?? 'aporte' }}</span>
                      <span>{{ aporte.texto }}</span>
                    </li>
                  } @empty {
                    <li class="muted">Sin aportes registrados.</li>
                  }
                </ul>
                <h5>Versiones</h5>
                <ul class="compact-list">
                  @for (version of detalle.versiones; track version.id) {
                    <li>
                      <span class="status-badge">v{{ version.numeroVersion }}</span>
                      <span class="status-badge">{{ version.estadoConfirmacion }}</span>
                      <span>{{ extracto(version.texto) }}</span>
                    </li>
                  } @empty {
                    <li class="muted">Sin versiones registradas.</li>
                  }
                </ul>
              </details>

              <section aria-labelledby="markdown-idea">
                <div class="panel-heading">
                  <h4 id="markdown-idea">Documento Markdown</h4>
                  @if (markdown(); as selectedMarkdown) {
                    <div class="actions-row">
                      @if (auth.isAdmin()) {
                        <button
                          type="button"
                          class="ghost-button"
                          (click)="regenerar(selectedMarkdown.id)"
                        >
                          Regenerar documento
                        </button>
                      }
                      <button
                        type="button"
                        class="ghost-button"
                        (click)="descargar(selectedMarkdown)"
                      >
                        Descargar .md
                      </button>
                    </div>
                  }
                </div>
                @if (markdown(); as selectedMarkdown) {
                  <pre class="markdown-preview">{{ selectedMarkdown.contenidoMarkdown }}</pre>
                } @else {
                  <p class="muted">Esta idea aún no tiene un documento Markdown disponible.</p>
                }
              </section>
            } @else if (respuestaSeleccionada(); as resp) {
              <div class="panel-heading">
                <h3>Detalle de {{ nombreUsuario(resp.usuarioId) }}</h3>
                @if (evaluacion(); as detalle) {
                  <span class="status-badge">{{ detalle.recomendacion }}</span>
                }
              </div>

              <section aria-labelledby="evaluacion-seleccionada">
                <h4 id="evaluacion-seleccionada">Evaluación</h4>
                @if (!evaluacion()) {
                  <p class="muted">Esta respuesta aún no tiene una evaluación asociada.</p>
                } @else if (esFallback()) {
                  <p class="form-error">
                    La evaluación no se completó. Revisa la configuración y vuelve a enviar la
                    respuesta.
                  </p>
                  <div class="detail-grid">
                    <div class="wide">
                      <span class="muted">Respuesta del participante</span>
                      <p>{{ resp.texto }}</p>
                    </div>
                    <div class="wide">
                      <span class="muted">Detalle para el equipo técnico</span>
                      <p>{{ evaluacion()!.explicacion }}</p>
                    </div>
                  </div>
                } @else {
                  <div class="detail-grid">
                    <div>
                      <span class="muted">Calificación</span>
                      <strong class="score">{{ evaluacion()!.calificacionTotal }}</strong>
                    </div>
                    <div>
                      <span class="muted">Temas</span>
                      <p>{{ evaluacion()!.temas.join(', ') || '-' }}</p>
                    </div>
                    <div class="wide">
                      <span class="muted">Respuesta del participante</span>
                      <p>{{ resp.texto }}</p>
                    </div>
                    <div class="wide">
                      <span class="muted">Retroalimentación enviada</span>
                      <p>{{ evaluacion()!.retroalimentacionEnviada }}</p>
                    </div>
                    <div class="wide">
                      <span class="muted">Explicación</span>
                      <p>{{ evaluacion()!.explicacion }}</p>
                    </div>
                  </div>
                }
              </section>

              <section aria-labelledby="markdown-seleccionado">
                <div class="panel-heading">
                  <h4 id="markdown-seleccionado">Documento Markdown</h4>
                  @if (markdown(); as selectedMarkdown) {
                    <div class="actions-row">
                      @if (auth.isAdmin()) {
                        <button
                          type="button"
                          class="ghost-button"
                          (click)="regenerar(selectedMarkdown.id)"
                        >
                          Regenerar documento
                        </button>
                      }
                      <button
                        type="button"
                        class="ghost-button"
                        (click)="descargar(selectedMarkdown)"
                      >
                        Descargar .md
                      </button>
                    </div>
                  }
                </div>
                @if (markdown(); as selectedMarkdown) {
                  <pre class="markdown-preview">{{ selectedMarkdown.contenidoMarkdown }}</pre>
                } @else {
                  <p class="muted">Esta respuesta aún no tiene un documento Markdown disponible.</p>
                }
              </section>
            } @else {
              <div class="empty-state">
                <h3>Selecciona una idea</h3>
                <p>Elige una idea de la izquierda para ver su evaluación y su documento.</p>
              </div>
            }
          </section>
        </div>

        @if (respuestasHistoricas().length) {
          <details class="panel resultados-historicos">
            <summary>
              Resultados históricos ({{ respuestasHistoricas().length }} respuestas sin idea)
            </summary>
            <p class="muted">
              Respuestas anteriores a la consolidación por idea. Se conservan tal cual para
              auditoría; no se migran ni se mezclan con las ideas de arriba.
            </p>
            <ul class="compact-list">
              @for (respuesta of respuestasHistoricas(); track respuesta.id) {
                <li [class.selected-row]="respuestaSeleccionada()?.id === respuesta.id">
                  <button
                    type="button"
                    class="resultados-respuesta"
                    [attr.aria-current]="
                      respuestaSeleccionada()?.id === respuesta.id ? 'true' : null
                    "
                    (click)="abrirRespuesta(respuesta.id)"
                  >
                    <span class="resultados-respuesta-titulo">
                      <strong>{{ nombreUsuario(respuesta.usuarioId) }}</strong>
                      <span class="status-badge" [class.badge-ok]="esMadura(respuesta)">
                        {{ esMadura(respuesta) ? 'madura' : 'incubación' }}
                      </span>
                      <span class="status-badge">resultado histórico</span>
                    </span>
                    <span class="resultados-extracto">{{ extracto(respuesta.texto) }}</span>
                  </button>
                </li>
              }
            </ul>
          </details>
        }

        <details class="panel resultados-actividad">
          <summary>Actividad de la campaña ({{ conversaciones().length }} conversaciones)</summary>
          <ul class="compact-list">
            @for (conv of conversaciones(); track conv.id) {
              <li>
                <strong>{{ nombreUsuario(conv.usuarioId) }}</strong>
                <span>{{ estadoConversacion(conv) }}</span>
              </li>
            } @empty {
              <li class="muted">Sin conversaciones.</li>
            }
          </ul>
        </details>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResultadosPage {
  private readonly api = inject(AdminApiService);
  private readonly sesion = inject(ResultadosSesionService);
  protected readonly auth = inject(AuthService);
  protected readonly conversaciones = signal<Conversacion[]>([]);
  protected readonly ideas = signal<IdeaConsolidada[]>([]);
  protected readonly ideaSeleccionada = signal<IdeaConsolidada | null>(null);
  protected readonly detalleIdea = signal<DetalleIdea | null>(null);
  protected readonly respuestas = signal<Respuesta[]>([]);
  protected readonly artefactos = signal<ArtefactoMarkdown[]>([]);
  protected readonly evaluacion = signal<Evaluacion | null>(null);
  protected readonly respuestaSeleccionada = signal<Respuesta | null>(null);
  protected readonly markdown = signal<ArtefactoMarkdown | null>(null);
  protected readonly campanias = signal<Campania[]>([]);
  protected readonly usuarios = signal<Map<string, UsuarioAdmin>>(new Map());
  protected readonly error = signal('');
  protected readonly informacion = signal('');
  protected readonly cargando = signal(false);
  protected readonly cargandoDetalle = signal(false);
  protected campaniaId = '';
  protected estadoIdeaFiltro = '';
  protected nivelMadurezFiltro = '';
  private cargasPendientes = 0;

  constructor() {
    this.api.campanias({ pageSize: 100 }).subscribe({
      next: (page) => {
        this.campanias.set(page.items);
        this.campaniaId = this.campaniaDisponible(page.items);
        if (this.campaniaId) {
          this.loadAll();
        } else {
          this.informacion.set('Aún no hay campañas disponibles para consultar resultados.');
        }
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
    this.api.usuarios({ pageSize: 500 }).subscribe({
      next: (page) => this.usuarios.set(new Map(page.items.map((u) => [u.id, u]))),
      error: () => {
        /* el id técnico sigue siendo el fallback; no bloquea la consulta de resultados */
      },
    });
  }

  nombreUsuario(usuarioId: string): string {
    const usuario = this.usuarios().get(usuarioId);
    if (!usuario) return usuarioId;
    return usuario.area ? `${usuario.nombre} (${usuario.area})` : usuario.nombre;
  }

  esMadura(respuesta: Respuesta): boolean {
    return respuesta.nivelMadurez === 'maduro';
  }

  /** I-19: respuestas anteriores a la consolidación; se conservan visibles sin migración. */
  respuestasHistoricas(): Respuesta[] {
    return this.respuestas().filter((respuesta) => !respuesta.ideaId);
  }

  conteoIdeas(estado: string): number {
    return this.ideas().filter((idea) => idea.estadoResultado === estado).length;
  }

  /** Estado visible de la idea: su resultado si ya cerró, o el hecho de que sigue en curso. */
  etiquetaEstadoIdea(idea: IdeaConsolidada): string {
    switch (idea.estadoResultado) {
      case 'madura':
        return 'madura';
      case 'pendiente':
        return 'pendiente';
      case 'rechazada':
        return 'rechazada';
      default:
        return 'en curso';
    }
  }

  tituloEstadoIdea(idea: IdeaConsolidada): string {
    switch (idea.estadoResultado) {
      case 'madura':
        return 'Madura: la versión confirmada superó el umbral de la rúbrica';
      case 'pendiente':
        return 'Pendiente: se conserva la última versión confirmada, sin alcanzar el umbral';
      case 'rechazada':
        return 'Rechazada: el participante pidió no guardarla; se conserva solo para auditoría';
      default:
        return 'En curso: el participante todavía está trabajando esta idea';
    }
  }

  /** Marca adicional del flujo cuando la idea sigue abierta (§9.2). */
  marcaFlujoIdea(idea: IdeaConsolidada): string {
    if (idea.estadoFlujo === 'enRevision') return 'en revisión';
    if (idea.estadoFlujo === 'pendienteConfirmacion') return 'pendiente de confirmación';
    return '';
  }

  extracto(texto: string): string {
    const limite = 140;
    return texto.length > limite ? `${texto.slice(0, limite - 1).trimEnd()}…` : texto;
  }

  estadoConversacion(conversacion: Conversacion): string {
    return conversacion.estado === 'cerrada' ? 'Cerrada' : 'En curso';
  }

  esFallback(): boolean {
    const respuesta = this.respuestaSeleccionada();
    const evaluacion = this.evaluacion();
    return (
      respuesta?.estado === 'evaluacionPendiente' ||
      (evaluacion?.explicacion?.startsWith('Evaluacion en fallback') ?? false)
    );
  }

  cambiarCampania() {
    this.loadAll();
  }

  loadAll() {
    if (!this.campaniaId) {
      this.error.set('');
      this.informacion.set('Elige una campaña para consultar sus resultados.');
      return;
    }

    this.sesion.campaniaId = this.campaniaId;
    this.error.set('');
    this.informacion.set('');
    this.cargando.set(true);
    this.cargasPendientes = 4;
    this.respuestaSeleccionada.set(null);
    this.ideaSeleccionada.set(null);
    this.detalleIdea.set(null);
    this.evaluacion.set(null);
    this.markdown.set(null);

    this.api
      .ideas(this.campaniaId, this.estadoIdeaFiltro)
      .pipe(finalize(() => this.finalizarCarga()))
      .subscribe({
        next: (page) => this.ideas.set(page.items),
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
    this.api
      .conversaciones(this.campaniaId)
      .pipe(finalize(() => this.finalizarCarga()))
      .subscribe({
        next: (page) => this.conversaciones.set(page.items),
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
    this.api
      .respuestas(this.campaniaId, this.nivelMadurezFiltro)
      .pipe(finalize(() => this.finalizarCarga()))
      .subscribe({
        next: (page) => this.respuestas.set(page.items),
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
    this.api
      .markdown(this.campaniaId)
      .pipe(finalize(() => this.finalizarCarga()))
      .subscribe({
        next: (page) => {
          this.artefactos.set(page.items);
          this.cargarMarkdownRelacionado();
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  /** I-19 §9.2: la unidad del detalle es la idea, con su historial auditable. */
  abrirIdea(id: string) {
    this.cargandoDetalle.set(true);
    this.error.set('');
    this.markdown.set(null);
    this.respuestaSeleccionada.set(null);
    this.evaluacion.set(null);
    const ideaDeLista = this.ideas().find((idea) => idea.id === id);
    if (ideaDeLista) this.ideaSeleccionada.set(ideaDeLista);
    this.api.idea(this.campaniaId, id).subscribe({
      next: (detalle) => {
        this.ideaSeleccionada.set(detalle.idea);
        this.detalleIdea.set(detalle);
        this.cargarMarkdownDeIdea(id);
      },
      error: (err: unknown) => {
        this.error.set(formatApiError(err));
        this.cargandoDetalle.set(false);
      },
    });
  }

  abrirRespuesta(id: string) {
    this.cargandoDetalle.set(true);
    this.error.set('');
    this.markdown.set(null);
    this.ideaSeleccionada.set(null);
    this.detalleIdea.set(null);
    const respuestaDeLista = this.respuestas().find((respuesta) => respuesta.id === id);
    if (respuestaDeLista) this.respuestaSeleccionada.set(respuestaDeLista);
    this.api.respuesta(this.campaniaId, id).subscribe({
      next: (detalle) => {
        this.respuestaSeleccionada.set(detalle.respuesta);
        this.evaluacion.set(detalle.evaluacion);
        this.cargarMarkdownRelacionado();
      },
      error: (err: unknown) => {
        this.error.set(formatApiError(err));
        this.cargandoDetalle.set(false);
      },
    });
  }

  abrirMarkdown(id: string, controlaCarga = true) {
    if (controlaCarga) this.cargandoDetalle.set(true);
    this.api.markdownDetalle(this.campaniaId, id).subscribe({
      next: (detalle) => {
        this.markdown.set(detalle);
        this.cargandoDetalle.set(false);
      },
      error: (err: unknown) => {
        this.error.set(formatApiError(err));
        this.cargandoDetalle.set(false);
      },
    });
  }

  regenerar(id: string) {
    this.api.regenerarMarkdown(this.campaniaId, id).subscribe({
      next: (detalle) => {
        this.markdown.set(detalle);
        this.loadAll();
        this.informacion.set('Documento regenerado.');
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
    this.informacion.set('Descarga iniciada.');
  }

  private campaniaDisponible(campanias: Campania[]): string {
    const recordada = this.sesion.campaniaId;
    return campanias.some((campania) => campania.id === recordada)
      ? recordada
      : (campanias[0]?.id ?? '');
  }

  private finalizarCarga() {
    this.cargasPendientes -= 1;
    if (this.cargasPendientes <= 0) {
      this.cargasPendientes = 0;
      this.cargando.set(false);
    }
  }

  /** El artefacto canónico de una idea se localiza por `ideaRef` (I-19 §10). */
  private cargarMarkdownDeIdea(ideaId: string) {
    const artefacto = this.artefactos().find((item) => item.ideaRef === ideaId);
    if (artefacto) {
      this.abrirMarkdown(artefacto.id, false);
    } else {
      this.cargandoDetalle.set(false);
    }
  }

  private cargarMarkdownRelacionado() {
    const respuesta = this.respuestaSeleccionada();
    const artefacto = respuesta
      ? this.artefactos().find((item) => item.respuestaRef === respuesta.id)
      : undefined;
    if (artefacto && !this.markdown()) {
      this.cargandoDetalle.set(true);
      this.abrirMarkdown(artefacto.id, false);
    } else if (!artefacto) {
      this.cargandoDetalle.set(false);
    }
  }
}
