import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AdminApiService } from '../../core/admin-api.service';
import {
  Campania,
  ConfigLlm,
  ParticipanteCampania,
  ParticipantePreview,
  PromptConfig,
  Rubrica,
} from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { formatApiError } from '../../shared-error';

@Component({
  selector: 'app-campanias-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
          <p class="eyebrow">REQ 11, 14, 15, 16</p>
          <h2>Campanias</h2>
        </div>
        <button type="button" class="ghost-button" (click)="load()">Actualizar</button>
      </div>

      @if (error()) {
        <p class="form-error">{{ error() }}</p>
      }

      <div class="two-column">
        <section class="panel">
          <div class="panel-heading">
            <h3>Lista</h3>
          </div>
          <form class="filters-grid" (ngSubmit)="load()">
            <label>
              Estado
              <select name="estadoFiltro" [(ngModel)]="filtroEstado">
                <option value="">Todos</option>
                <option value="borrador">Borrador</option>
                <option value="activa">Activa</option>
                <option value="cerrada">Cerrada</option>
                <option value="archivada">Archivada</option>
              </select>
            </label>
            <label>
              Busqueda
              <input name="busquedaFiltro" [(ngModel)]="filtroBusqueda" placeholder="Nombre" />
            </label>
            <button class="ghost-button" type="submit">Buscar</button>
          </form>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Estado</th>
                  <th>Objetivo</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (campania of campanias(); track campania.id) {
                  <tr [class.selected-row]="selected()?.id === campania.id">
                    <td>{{ campania.nombre }}</td>
                    <td>
                      <span class="status-badge">{{ campania.estado }}</span>
                    </td>
                    <td>{{ campania.objetivo }}</td>
                    <td>
                      <button type="button" class="table-button" (click)="select(campania.id)">
                        Abrir
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="4" class="empty-cell">No hay campanias registradas.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="panel">
          <div class="panel-heading">
            <h3>Crear campania</h3>
          </div>
          <form class="form-grid" (ngSubmit)="crearCampania()">
            <label>Nombre <input name="nombre" [(ngModel)]="nueva.nombre" /></label>
            <label>Descripcion <input name="descripcion" [(ngModel)]="nueva.descripcion" /></label>
            <label
              >Objetivo <textarea name="objetivo" rows="3" [(ngModel)]="nueva.objetivo"></textarea>
            </label>
            <label>
              Rubrica
              <select name="rubricaRef" [(ngModel)]="nueva.rubricaRef" required>
                <option value="" disabled>Selecciona una rubrica</option>
                @for (rubrica of rubricas(); track rubrica.id) {
                  <option [value]="rubrica.id">{{ rubrica.nombre }}</option>
                }
              </select>
            </label>
            @if (rubricas().length === 0) {
              <p class="muted">No hay rubricas activas. Crea una en la seccion Rubricas.</p>
            }
            <label>
              Config LLM
              <select name="configLlmRef" [(ngModel)]="nueva.configLlmRef" required>
                <option value="" disabled>Selecciona una configuracion LLM</option>
                @for (config of configsLlm(); track config.id) {
                  <option [value]="config.id">{{ config.nombre }}</option>
                }
              </select>
            </label>
            @if (configsLlm().length === 0) {
              <p class="muted">No hay configuraciones LLM. Crea una en la seccion Config LLM.</p>
            }
            <label>
              Prompt de evaluacion
              <select name="promptEvaluarRef" [(ngModel)]="nueva.promptEvaluarRef">
                <option value="">Sin prompt (configurar por pregunta o luego)</option>
                @for (prompt of prompts(); track prompt.id) {
                  <option [value]="prompt.id">{{ prompt.nombre }} ({{ prompt.tipoPrompt }})</option>
                }
              </select>
            </label>
            <button class="primary-button" type="submit" [disabled]="!auth.isAdmin()">
              Guardar campania
            </button>
          </form>
        </section>
      </div>

      @if (selected(); as campania) {
        <section class="panel">
          <div class="panel-heading">
            <h3>{{ campania.nombre }}</h3>
            <div class="actions-row">
              <button
                type="button"
                class="ghost-button"
                [routerLink]="['/campanias', campania.id, 'envios']"
              >
                Envios
              </button>
              @if (auth.isAdmin()) {
                <button
                  type="button"
                  class="ghost-button"
                  (click)="cambiarEstado(campania, 'activa')"
                >
                  Activar
                </button>
                <button
                  type="button"
                  class="ghost-button"
                  (click)="cambiarEstado(campania, 'cerrada')"
                >
                  Cerrar
                </button>
              }
            </div>
          </div>

          <div class="tabs-layout">
            <article>
              <h4>Mensajes iniciales</h4>
              <form class="form-grid" (ngSubmit)="crearMensaje(campania.id)">
                <input
                  name="miNombre"
                  [(ngModel)]="mensaje.nombreInterno"
                  placeholder="Nombre interno"
                />
                <textarea
                  name="miTexto"
                  rows="3"
                  [(ngModel)]="mensaje.texto"
                  placeholder="Texto"
                ></textarea>
                <button class="primary-button" type="submit" [disabled]="!auth.isAdmin()">
                  Agregar mensaje
                </button>
              </form>
              <ul class="compact-list">
                @for (item of campania.mensajesIniciales ?? []; track item.id) {
                  <li>
                    <strong>{{ item.nombreInterno }}</strong
                    ><span>{{ item.texto }}</span>
                  </li>
                } @empty {
                  <li class="muted">Sin mensajes.</li>
                }
              </ul>
            </article>

            <article>
              <h4>Preguntas</h4>
              <form class="form-grid" (ngSubmit)="crearPregunta(campania.id)">
                <input
                  name="preguntaCategoria"
                  [(ngModel)]="pregunta.categoria"
                  placeholder="Categoria"
                />
                <textarea
                  name="preguntaTexto"
                  rows="3"
                  [(ngModel)]="pregunta.texto"
                  placeholder="Pregunta"
                ></textarea>
                <label>
                  Rubrica (opcional, sobreescribe la campania)
                  <select name="preguntaRubricaRef" [(ngModel)]="pregunta.rubricaRef">
                    <option value="">Heredar de la campania</option>
                    @for (rubrica of rubricas(); track rubrica.id) {
                      <option [value]="rubrica.id">{{ rubrica.nombre }}</option>
                    }
                  </select>
                </label>
                <label>
                  Prompt de evaluacion (opcional, sobreescribe la campania)
                  <select name="preguntaPromptRef" [(ngModel)]="pregunta.promptEvaluarRef">
                    <option value="">Heredar de la campania</option>
                    @for (prompt of prompts(); track prompt.id) {
                      <option [value]="prompt.id">
                        {{ prompt.nombre }} ({{ prompt.tipoPrompt }})
                      </option>
                    }
                  </select>
                </label>
                <button class="primary-button" type="submit" [disabled]="!auth.isAdmin()">
                  Agregar pregunta
                </button>
              </form>
              <ul class="compact-list">
                @for (item of campania.preguntas ?? []; track item.id) {
                  <li>
                    <strong>{{ item.categoria }}</strong
                    ><span>{{ item.texto }}</span>
                  </li>
                } @empty {
                  <li class="muted">Sin preguntas.</li>
                }
              </ul>
            </article>

            <article>
              <h4>Participantes</h4>
              <form class="form-grid" (ngSubmit)="preview(campania.id)">
                <label>
                  Area
                  <select name="previewArea" [(ngModel)]="filtroParticipantes.area">
                    <option value="">Todas</option>
                    @for (area of areasDisponibles(); track area) {
                      <option [value]="area">{{ area }}</option>
                    }
                  </select>
                </label>
                <label>
                  Empresa
                  <select name="previewEmpresa" [(ngModel)]="filtroParticipantes.empresa">
                    <option value="">Todas</option>
                    @for (empresa of empresasDisponibles(); track empresa) {
                      <option [value]="empresa">{{ empresa }}</option>
                    }
                  </select>
                </label>
                <button class="ghost-button" type="submit">Preview</button>
              </form>
              @if (previewUsuarios().length > 0) {
                <p class="muted">
                  Preview: {{ previewUsuarios().length }} elegibles,
                  {{ previewSeleccion().size }} seleccionados
                </p>
                <ul class="compact-list">
                  @for (usuario of previewUsuarios(); track usuario.usuarioId) {
                    <li>
                      <label class="check-inline">
                        <input
                          type="checkbox"
                          [checked]="previewSeleccion().has(usuario.usuarioId)"
                          (change)="togglePreview(usuario.usuarioId)"
                        />
                        <strong>{{ usuario.nombre }}</strong>
                        <span>{{ usuario.area }} / {{ usuario.empresa }}</span>
                      </label>
                    </li>
                  }
                </ul>
                @if (auth.isAdmin()) {
                  <button
                    type="button"
                    class="primary-button"
                    [disabled]="previewSeleccion().size === 0"
                    (click)="asociarPreview(campania.id)"
                  >
                    Asociar seleccionados ({{ previewSeleccion().size }})
                  </button>
                }
              }
              <h5 class="subhead">Asociados</h5>
              <div class="table-wrap small-table">
                <table>
                  <tbody>
                    @for (participante of participantes(); track participante.id) {
                      <tr>
                        <td>{{ participante.usuarioId }}</td>
                        <td>{{ participante.estadoEnvio }}</td>
                        <td>{{ participante.estadoRespuesta }}</td>
                      </tr>
                    } @empty {
                      <tr>
                        <td class="empty-cell">Sin participantes asociados.</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </article>
          </div>
        </section>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaniasPage {
  private readonly api = inject(AdminApiService);
  protected readonly auth = inject(AuthService);
  protected readonly campanias = signal<Campania[]>([]);
  protected readonly selected = signal<Campania | null>(null);
  protected readonly participantes = signal<ParticipanteCampania[]>([]);
  protected readonly previewUsuarios = signal<ParticipantePreview[]>([]);
  protected readonly previewSeleccion = signal<Set<string>>(new Set<string>());
  protected readonly rubricas = signal<Rubrica[]>([]);
  protected readonly configsLlm = signal<ConfigLlm[]>([]);
  protected readonly prompts = signal<PromptConfig[]>([]);
  protected readonly areasDisponibles = signal<string[]>([]);
  protected readonly empresasDisponibles = signal<string[]>([]);
  protected readonly error = signal('');

  protected filtroEstado = '';
  protected filtroBusqueda = '';
  protected nueva = {
    nombre: '',
    descripcion: '',
    objetivo: '',
    rubricaRef: '',
    configLlmRef: '',
    promptEvaluarRef: '',
  };
  protected mensaje = { nombreInterno: '', texto: '' };
  protected pregunta = { categoria: '', texto: '', rubricaRef: '', promptEvaluarRef: '' };
  protected filtroParticipantes = { area: '', empresa: '' };

  constructor() {
    this.load();
    this.loadCatalogos();
  }

  load() {
    this.api
      .campanias({ estado: this.filtroEstado, q: this.filtroBusqueda, pageSize: 50 })
      .subscribe({
        next: (page) => {
          this.campanias.set(page.items);
          this.error.set('');
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  private loadCatalogos() {
    this.api.rubricas({ estado: 'activa', pageSize: 100 }).subscribe({
      next: (page) => this.rubricas.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
    this.api.configsLlm({ pageSize: 100 }).subscribe({
      next: (page) => this.configsLlm.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
    this.api.prompts({ pageSize: 100 }).subscribe({
      next: (page) => this.prompts.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
    this.api.usuarios({ rol: 'participante', pageSize: 100 }).subscribe({
      next: (page) => {
        this.areasDisponibles.set(this.distinct(page.items.map((usuario) => usuario.area)));
        this.empresasDisponibles.set(this.distinct(page.items.map((usuario) => usuario.empresa)));
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  private distinct(valores: string[]): string[] {
    return Array.from(
      new Set(valores.filter((valor) => !!valor && valor.trim().length > 0)),
    ).sort();
  }

  select(id: string) {
    this.api.campania(id).subscribe({
      next: (campania) => {
        this.selected.set(campania);
        this.loadParticipantes(campania.id);
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  crearCampania() {
    const { promptEvaluarRef, ...datos } = this.nueva;
    this.api
      .crearCampania({
        ...datos,
        promptRefs: promptEvaluarRef ? { evaluar: promptEvaluarRef } : {},
        configMarkdown: { tipoArtefacto: 'respuesta' },
        configConversacional: {
          maxRepreguntas: 1,
          mensajeCierre: 'Gracias. Tu aporte quedo registrado.',
        },
        configSeguridad: {
          maxCaracteresMensaje: 1500,
          maxMensajesPorUsuario: 10,
          maxLlamadasLlmPorUsuario: 2,
        },
      })
      .subscribe({
        next: (campania) => {
          this.nueva = {
            nombre: '',
            descripcion: '',
            objetivo: '',
            rubricaRef: '',
            configLlmRef: '',
            promptEvaluarRef: '',
          };
          this.load();
          this.select(campania.id);
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  cambiarEstado(campania: Campania, estado: string) {
    this.api.cambiarEstadoCampania(campania.id, estado).subscribe({
      next: (actualizada) => {
        this.selected.set(actualizada);
        this.load();
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  crearMensaje(campaniaId: string) {
    this.api
      .crearMensajeInicial(campaniaId, {
        ...this.mensaje,
        orden: 1,
        variablesDinamicas: ['nombre'],
        estado: 'activo',
      })
      .subscribe({
        next: () => {
          this.mensaje = { nombreInterno: '', texto: '' };
          this.select(campaniaId);
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  crearPregunta(campaniaId: string) {
    this.api
      .crearPregunta(campaniaId, {
        categoria: this.pregunta.categoria,
        texto: this.pregunta.texto,
        instruccion: this.pregunta.texto,
        orden: 1,
        estado: 'activo',
        maxRepreguntas: 1,
        limitesSeguridad: { maxCaracteresMensaje: 1500, maxLlamadasLlm: 2 },
        configMarkdown: { tipoArtefacto: 'respuesta' },
        ...(this.pregunta.rubricaRef ? { rubricaRef: this.pregunta.rubricaRef } : {}),
        ...(this.pregunta.promptEvaluarRef
          ? { promptRefs: { evaluar: this.pregunta.promptEvaluarRef } }
          : {}),
      })
      .subscribe({
        next: () => {
          this.pregunta = { categoria: '', texto: '', rubricaRef: '', promptEvaluarRef: '' };
          this.select(campaniaId);
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  preview(campaniaId: string) {
    this.api.previewParticipantes(campaniaId, this.filtroParticipantes).subscribe({
      next: (response) => {
        this.previewUsuarios.set(response.items);
        this.previewSeleccion.set(new Set(response.items.map((usuario) => usuario.usuarioId)));
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  togglePreview(usuarioId: string) {
    const seleccion = new Set(this.previewSeleccion());
    if (seleccion.has(usuarioId)) {
      seleccion.delete(usuarioId);
    } else {
      seleccion.add(usuarioId);
    }
    this.previewSeleccion.set(seleccion);
  }

  asociarPreview(campaniaId: string) {
    this.api.asociarParticipantes(campaniaId, Array.from(this.previewSeleccion())).subscribe({
      next: () => {
        this.loadParticipantes(campaniaId);
        this.previewUsuarios.set([]);
        this.previewSeleccion.set(new Set<string>());
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  private loadParticipantes(campaniaId: string) {
    this.api.participantes(campaniaId).subscribe({
      next: (items) => this.participantes.set(items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }
}
