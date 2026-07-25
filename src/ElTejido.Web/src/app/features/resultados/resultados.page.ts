import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

import { AdminApiService } from '../../core/admin-api.service';
import {
  ArtefactoMarkdown,
  Campania,
  Conversacion,
  Evaluacion,
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
          <p class="muted">
            Elige una campaña para revisar sus respuestas, evaluaciones y documentos.
          </p>
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
            Nivel de madurez
            <select
              name="nivelMadurez"
              [(ngModel)]="nivelMadurezFiltro"
              (ngModelChange)="loadAll()"
            >
              <option value="">Todas</option>
              <option value="maduro">Maduras</option>
              <option value="incubacion">Incubación</option>
            </select>
          </label>
          <div class="resultados-resumen" aria-label="Resumen de respuestas">
            <strong>{{ respuestas().length }} respuestas</strong>
            <span>{{ conteoMaduras() }} maduras · {{ conteoIncubacion() }} en incubación</span>
          </div>
          <div class="resultados-leyenda" aria-label="Leyenda de estados">
            <span class="status-badge badge-ok">Maduras</span>
            <span class="status-badge">En incubación</span>
            <span class="status-badge">Evaluadas</span>
            <span class="status-badge badge-warn">Sin evaluar</span>
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
              <h3>Respuestas</h3>
              <span class="muted">{{ respuestas().length }}</span>
            </div>
            @if (cargando()) {
              <div class="resultados-skeleton" aria-label="Cargando respuestas">
                <span></span><span></span><span></span>
              </div>
            } @else {
              <ul class="compact-list resultados-lista-maestra">
                @for (respuesta of respuestas(); track respuesta.id) {
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
                        <span
                          class="status-badge"
                          [class.badge-warn]="respuesta.estado === 'evaluacionPendiente'"
                        >
                          {{
                            respuesta.estado === 'evaluacionPendiente' ? 'sin evaluar' : 'evaluada'
                          }}
                        </span>
                        <span
                          class="status-badge"
                          [class.badge-ok]="esMadura(respuesta)"
                          [title]="
                            esMadura(respuesta)
                              ? 'Idea madura: superó el umbral de la rúbrica'
                              : 'En incubación: no alcanzó el umbral (material para coaching)'
                          "
                        >
                          {{ esMadura(respuesta) ? 'madura' : 'incubación' }}
                        </span>
                      </span>
                      <span class="resultados-extracto">{{ extracto(respuesta.texto) }}</span>
                    </button>
                  </li>
                } @empty {
                  <li class="muted">
                    Esta campaña aún no tiene respuestas con ese filtro. Cambia el nivel de madurez
                    o revisa que la campaña haya recibido mensajes.
                  </li>
                }
              </ul>
            }
          </section>

          <section class="panel resultados-detalle">
            @if (cargandoDetalle()) {
              <div class="resultados-skeleton" aria-label="Cargando detalle de la respuesta">
                <span></span><span></span>
              </div>
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
                <h3>Selecciona una respuesta</h3>
                <p>Elige una respuesta de la izquierda para ver su evaluación y su documento.</p>
              </div>
            }
          </section>
        </div>

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

  conteoMaduras(): number {
    return this.respuestas().filter((respuesta) => this.esMadura(respuesta)).length;
  }

  conteoIncubacion(): number {
    return this.respuestas().filter((respuesta) => !this.esMadura(respuesta)).length;
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
    this.cargasPendientes = 3;
    this.respuestaSeleccionada.set(null);
    this.evaluacion.set(null);
    this.markdown.set(null);

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

  abrirRespuesta(id: string) {
    this.cargandoDetalle.set(true);
    this.error.set('');
    this.markdown.set(null);
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
