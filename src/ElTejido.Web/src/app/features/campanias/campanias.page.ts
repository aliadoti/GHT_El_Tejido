import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AdminApiService } from '../../core/admin-api.service';
import {
  Campania,
  ConfigLlm,
  ParticipanteCampania,
  ParticipantePreview,
  Pregunta,
  PromptConfig,
  Rubrica,
  UsuarioAdmin,
} from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { NotificacionesService } from '../../core/notificaciones.service';
import { formatApiError } from '../../shared-error';

interface PreguntaForm {
  categoria: string;
  texto: string;
  instruccion: string;
  orden: number;
  estado: string;
  rubricaRef: string;
  promptEvaluarRef: string;
  maxRepreguntas: number;
  maxCaracteresMensaje: number;
  maxLlamadasLlm: number;
}

@Component({
  selector: 'app-campanias-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <section class="page-grid">
      <div class="section-header">
        <div>
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

          <nav class="tab-nav" role="tablist">
            <button
              type="button"
              class="tab-button"
              [class.active]="tabActiva() === 'config'"
              (click)="tabActiva.set('config')"
            >
              Configuracion
            </button>
            <button
              type="button"
              class="tab-button"
              [class.active]="tabActiva() === 'mensajes'"
              (click)="tabActiva.set('mensajes')"
            >
              Mensajes iniciales
            </button>
            <button
              type="button"
              class="tab-button"
              [class.active]="tabActiva() === 'preguntas'"
              (click)="tabActiva.set('preguntas')"
            >
              Preguntas
            </button>
            <button
              type="button"
              class="tab-button"
              [class.active]="tabActiva() === 'participantes'"
              (click)="tabActiva.set('participantes')"
            >
              Participantes
            </button>
          </nav>

          <div class="tab-panels">
            @if (tabActiva() === 'config') {
              <article>
                <h4>Configuracion</h4>
                <form class="form-grid" (ngSubmit)="actualizarCampania(campania.id)">
                  <label>Nombre <input name="editarNombre" [(ngModel)]="edicion.nombre" /></label>
                  <label>
                    Descripcion
                    <input name="editarDescripcion" [(ngModel)]="edicion.descripcion" />
                  </label>
                  <label>
                    Objetivo
                    <textarea
                      name="editarObjetivo"
                      rows="3"
                      [(ngModel)]="edicion.objetivo"
                    ></textarea>
                  </label>
                  <label>
                    Rubrica
                    <select name="editarRubricaRef" [(ngModel)]="edicion.rubricaRef" required>
                      @for (rubrica of rubricas(); track rubrica.id) {
                        <option [value]="rubrica.id">{{ rubrica.nombre }}</option>
                      }
                    </select>
                  </label>
                  <label>
                    Config LLM
                    <select name="editarConfigLlmRef" [(ngModel)]="edicion.configLlmRef" required>
                      @for (config of configsLlm(); track config.id) {
                        <option [value]="config.id">{{ config.nombre }}</option>
                      }
                    </select>
                  </label>
                  <label>
                    Presupuesto de tokens LLM (0 = sin límite)
                    <input
                      type="number"
                      min="0"
                      name="editarPresupuestoTokens"
                      [(ngModel)]="edicion.presupuestoTokensCampania"
                    />
                  </label>
                  <label class="checkbox-label">
                    <input
                      type="checkbox"
                      name="editarSegmentacionIdeas"
                      [(ngModel)]="edicion.segmentacionIdeas"
                    />
                    Separar varias ideas de un mismo mensaje
                  </label>
                  <label>
                    Umbral de cierre anticipado (vacío = heredar global; 0 = apagar)
                    <input
                      type="number"
                      min="0"
                      max="1"
                      step="0.01"
                      name="editarUmbralCierreAnticipado"
                      [(ngModel)]="edicion.umbralCierreAnticipado"
                    />
                  </label>
                  <label>
                    Prompt de evaluacion
                    <select name="editarPromptEvaluarRef" [(ngModel)]="edicion.promptEvaluarRef">
                      <option value="">Sin prompt por defecto</option>
                      @for (prompt of prompts(); track prompt.id) {
                        <option [value]="prompt.id">
                          {{ prompt.nombre }} ({{ prompt.tipoPrompt }})
                        </option>
                      }
                    </select>
                  </label>
                  <button class="primary-button" type="submit" [disabled]="!auth.isAdmin()">
                    Guardar cambios
                  </button>
                </form>
              </article>
            }

            @if (tabActiva() === 'mensajes') {
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
                  <p class="subhead">
                    Plantilla WhatsApp (requerida para el envio inicial proactivo)
                  </p>
                  <input
                    name="miPlantillaNombre"
                    [(ngModel)]="mensaje.plantillaNombre"
                    placeholder="Plantilla aprobada (ej: el_tejido_saludo)"
                  />
                  <input
                    name="miPlantillaIdioma"
                    [(ngModel)]="mensaje.plantillaIdioma"
                    placeholder="Idioma (ej: es)"
                  />
                  <input
                    name="miPlantillaComponentes"
                    [(ngModel)]="mensaje.plantillaComponentes"
                    placeholder="Variables en orden, coma-separadas (ej: nombre, campania)"
                  />
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
            }

            @if (tabActiva() === 'preguntas') {
              <article>
                <h4>Preguntas</h4>
                <form class="form-grid" (ngSubmit)="crearPregunta(campania.id)">
                  <label>
                    Categoria
                    <input
                      name="preguntaCategoria"
                      [(ngModel)]="pregunta.categoria"
                      placeholder="Categoria"
                    />
                  </label>
                  <label>
                    Pregunta
                    <textarea
                      name="preguntaTexto"
                      rows="3"
                      [(ngModel)]="pregunta.texto"
                      placeholder="Texto que recibira el participante"
                    ></textarea>
                  </label>
                  <label>
                    Instruccion de evaluacion
                    <textarea
                      name="preguntaInstruccion"
                      rows="2"
                      [(ngModel)]="pregunta.instruccion"
                      placeholder="Criterio operativo para evaluar la respuesta"
                    ></textarea>
                  </label>
                  <div class="inline-form">
                    <label>
                      Orden
                      <input
                        type="number"
                        min="1"
                        name="preguntaOrden"
                        [(ngModel)]="pregunta.orden"
                      />
                    </label>
                    <label>
                      Revisiones
                      <input
                        type="number"
                        min="0"
                        name="preguntaMaxRepreguntas"
                        [(ngModel)]="pregunta.maxRepreguntas"
                      />
                    </label>
                  </div>
                  <input type="hidden" name="preguntaEstado" [(ngModel)]="pregunta.estado" />
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
                      <span
                        >Orden {{ item.orden }} · {{ item.estado }} · Revisiones:
                        {{ item.maxRepreguntas }}</span
                      >
                      @if (auth.isAdmin()) {
                        <button type="button" class="table-button" (click)="editarPregunta(item)">
                          Editar
                        </button>
                      }
                    </li>
                  } @empty {
                    <li class="muted">Sin preguntas.</li>
                  }
                </ul>
                @if (preguntaEditandoId()) {
                  <div class="edit-block">
                    <div class="panel-heading">
                      <h5 class="subhead">Editar pregunta</h5>
                      <button
                        type="button"
                        class="ghost-button"
                        (click)="cancelarEdicionPregunta()"
                      >
                        Cancelar
                      </button>
                    </div>
                    <form class="form-grid" (ngSubmit)="actualizarPregunta(campania.id)">
                      <label>
                        Categoria
                        <input
                          name="editarPreguntaCategoria"
                          [(ngModel)]="preguntaEdicion.categoria"
                        />
                      </label>
                      <label>
                        Pregunta
                        <textarea
                          name="editarPreguntaTexto"
                          rows="3"
                          [(ngModel)]="preguntaEdicion.texto"
                        ></textarea>
                      </label>
                      <label>
                        Instruccion de evaluacion
                        <textarea
                          name="editarPreguntaInstruccion"
                          rows="2"
                          [(ngModel)]="preguntaEdicion.instruccion"
                        ></textarea>
                      </label>
                      <div class="inline-form">
                        <label>
                          Orden
                          <input
                            type="number"
                            min="1"
                            name="editarPreguntaOrden"
                            [(ngModel)]="preguntaEdicion.orden"
                          />
                        </label>
                        <label>
                          Revisiones
                          <input
                            type="number"
                            min="0"
                            name="editarPreguntaMaxRepreguntas"
                            [(ngModel)]="preguntaEdicion.maxRepreguntas"
                          />
                        </label>
                        <label>
                          Estado
                          <select name="editarPreguntaEstado" [(ngModel)]="preguntaEdicion.estado">
                            <option value="activo">Activo</option>
                            <option value="inactivo">Inactivo</option>
                          </select>
                        </label>
                      </div>
                      <label>
                        Rubrica
                        <select
                          name="editarPreguntaRubricaRef"
                          [(ngModel)]="preguntaEdicion.rubricaRef"
                        >
                          <option value="">Heredar de la campania</option>
                          @for (rubrica of rubricas(); track rubrica.id) {
                            <option [value]="rubrica.id">{{ rubrica.nombre }}</option>
                          }
                        </select>
                      </label>
                      <label>
                        Prompt de evaluacion
                        <select
                          name="editarPreguntaPromptRef"
                          [(ngModel)]="preguntaEdicion.promptEvaluarRef"
                        >
                          <option value="">Heredar de la campania</option>
                          @for (prompt of prompts(); track prompt.id) {
                            <option [value]="prompt.id">
                              {{ prompt.nombre }} ({{ prompt.tipoPrompt }})
                            </option>
                          }
                        </select>
                      </label>
                      <button class="primary-button" type="submit" [disabled]="!auth.isAdmin()">
                        Guardar pregunta
                      </button>
                    </form>
                  </div>
                }
              </article>
            }

            @if (tabActiva() === 'participantes') {
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
                          <td>{{ nombreUsuario(participante.usuarioId) }}</td>
                          <td>{{ participante.estadoEnvio }}</td>
                          <td>{{ participante.estadoRespuesta }}</td>
                          @if (auth.isAdmin()) {
                            <td>
                              <button
                                type="button"
                                class="ghost-button"
                                (click)="reiniciarParticipante(campania.id, participante)"
                              >
                                Reiniciar conversacion
                              </button>
                            </td>
                          }
                        </tr>
                      } @empty {
                        <tr>
                          <td class="empty-cell">Sin participantes asociados.</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
                @if (auth.isAdmin() && participantes().length > 0) {
                  <p class="muted">
                    Reinicio de datos de prueba: borra conversaciones, respuestas, evaluaciones y
                    Markdown; conserva la campania, su configuracion y los usuarios.
                  </p>
                  <button
                    type="button"
                    class="ghost-button danger"
                    (click)="reiniciarDatosCampania(campania)"
                  >
                    Reiniciar datos de prueba (toda la campania)
                  </button>
                }
              </article>
            }
          </div>
        </section>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaniasPage {
  private readonly api = inject(AdminApiService);
  private readonly notificaciones = inject(NotificacionesService);
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
  protected readonly preguntaEditandoId = signal<string | null>(null);
  protected readonly usuariosMap = signal<Map<string, UsuarioAdmin>>(new Map());
  protected readonly tabActiva = signal<'config' | 'mensajes' | 'preguntas' | 'participantes'>(
    'config',
  );

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
  protected edicion = this.emptyCampaniaForm();
  protected mensaje = {
    nombreInterno: '',
    texto: '',
    plantillaNombre: '',
    plantillaIdioma: 'es',
    plantillaComponentes: '',
  };
  protected pregunta = this.emptyPreguntaForm();
  protected preguntaEdicion = this.emptyPreguntaForm();
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
    this.api.configsLlm({ estado: 'activo', pageSize: 100 }).subscribe({
      next: (page) => this.configsLlm.set(page.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
    this.api.prompts({ tipoPrompt: 'evaluar', estado: 'activo', pageSize: 100 }).subscribe({
      next: (page) =>
        this.prompts.set(
          page.items.filter((prompt) => !!prompt.aprobadoPor && !!prompt.fechaAprobacion),
        ),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
    this.api.usuarios({ rol: 'participante', pageSize: 500 }).subscribe({
      next: (page) => {
        this.areasDisponibles.set(this.distinct(page.items.map((usuario) => usuario.area)));
        this.empresasDisponibles.set(this.distinct(page.items.map((usuario) => usuario.empresa)));
        // Mapa id -> usuario para mostrar nombre/area en vez del id tecnico en Asociados.
        this.usuariosMap.set(new Map(page.items.map((usuario) => [usuario.id, usuario])));
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  /** Nombre legible del participante (con area si esta disponible); cae al id si no se encontro. */
  nombreUsuario(usuarioId: string): string {
    const usuario = this.usuariosMap().get(usuarioId);
    if (!usuario) {
      return usuarioId;
    }
    return usuario.area ? `${usuario.nombre} (${usuario.area})` : usuario.nombre;
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
        this.edicion = this.formFromCampania(campania);
        this.tabActiva.set('config');
        this.cancelarEdicionPregunta();
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
          segmentacionIdeas: false,
          umbralCierreAnticipado: null,
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
            ...this.emptyCampaniaForm(),
          };
          this.load();
          this.select(campania.id);
          this.notificaciones.exito('Campania creada.');
        },
        error: (err: unknown) => this.reportarError(err),
      });
  }

  actualizarCampania(id: string) {
    const promptRefs = this.edicion.promptEvaluarRef
      ? { evaluar: this.edicion.promptEvaluarRef }
      : {};
    this.api
      .actualizarCampania(id, {
        nombre: this.edicion.nombre,
        descripcion: this.edicion.descripcion,
        objetivo: this.edicion.objetivo,
        rubricaRef: this.edicion.rubricaRef,
        promptRefs,
        configLLMRef: this.edicion.configLlmRef,
        configConversacional: {
          maxRepreguntas: this.selected()?.configConversacional?.maxRepreguntas ?? 1,
          mensajeCierre:
            this.selected()?.configConversacional?.mensajeCierre ??
            'Gracias. Tu aporte quedo registrado correctamente.',
          segmentacionIdeas: Boolean(this.edicion.segmentacionIdeas),
          // I-09: preserva el valor actual (la UI de activación llega con I-10) para no
          // reiniciarlo a false en cada edición de la campaña.
          tejidoColectivo: Boolean(this.selected()?.configConversacional?.tejidoColectivo),
          // I-05: preserva el flag por campaña al editar otra configuración.
          parafraseo: Boolean(this.selected()?.configConversacional?.parafraseo),
          umbralCierreAnticipado:
            this.edicion.umbralCierreAnticipado === null
              ? null
              : Math.min(1, Math.max(0, Number(this.edicion.umbralCierreAnticipado) || 0)),
        },
        // P-10: conserva los cupos actuales y actualiza el presupuesto de tokens de la campaña.
        configSeguridad: {
          maxCaracteresMensaje: this.selected()?.configSeguridad?.maxCaracteresMensaje ?? 1500,
          maxMensajesPorUsuario: this.selected()?.configSeguridad?.maxMensajesPorUsuario ?? 10,
          maxLlamadasLlmPorUsuario: this.selected()?.configSeguridad?.maxLlamadasLlmPorUsuario ?? 2,
          presupuestoTokensCampania: Math.max(
            0,
            Number(this.edicion.presupuestoTokensCampania) || 0,
          ),
        },
      })
      .subscribe({
        next: (campania) => {
          this.selected.set(campania);
          this.edicion = this.formFromCampania(campania);
          this.load();
          this.notificaciones.exito('Campania actualizada.');
        },
        error: (err: unknown) => this.reportarError(err),
      });
  }

  cambiarEstado(campania: Campania, estado: string) {
    this.api.cambiarEstadoCampania(campania.id, estado).subscribe({
      next: (actualizada) => {
        this.selected.set(actualizada);
        this.load();
        this.notificaciones.exito(`Campania ${estado}.`);
      },
      error: (err: unknown) => this.reportarError(err),
    });
  }

  crearMensaje(campaniaId: string) {
    const plantillaNombre = this.mensaje.plantillaNombre.trim();
    const componentes = this.mensaje.plantillaComponentes
      .split(',')
      .map((componente) => componente.trim())
      .filter((componente) => componente.length > 0);
    const plantillaWhatsApp = plantillaNombre
      ? {
          nombre: plantillaNombre,
          idioma: this.mensaje.plantillaIdioma.trim() || 'es',
          componentes,
        }
      : undefined;

    this.api
      .crearMensajeInicial(campaniaId, {
        nombreInterno: this.mensaje.nombreInterno,
        texto: this.mensaje.texto,
        orden: 1,
        variablesDinamicas: ['nombre'],
        estado: 'activo',
        ...(plantillaWhatsApp ? { plantillaWhatsApp } : {}),
      })
      .subscribe({
        next: () => {
          this.mensaje = {
            nombreInterno: '',
            texto: '',
            plantillaNombre: '',
            plantillaIdioma: 'es',
            plantillaComponentes: '',
          };
          this.select(campaniaId);
          this.notificaciones.exito('Mensaje inicial agregado.');
        },
        error: (err: unknown) => this.reportarError(err),
      });
  }

  crearPregunta(campaniaId: string) {
    this.api.crearPregunta(campaniaId, this.preguntaPayload(this.pregunta)).subscribe({
      next: () => {
        this.pregunta = this.emptyPreguntaForm();
        this.select(campaniaId);
        this.notificaciones.exito('Pregunta agregada.');
      },
      error: (err: unknown) => this.reportarError(err),
    });
  }

  editarPregunta(pregunta: Pregunta) {
    this.preguntaEditandoId.set(pregunta.id);
    this.preguntaEdicion = this.formFromPregunta(pregunta);
  }

  cancelarEdicionPregunta() {
    this.preguntaEditandoId.set(null);
    this.preguntaEdicion = this.emptyPreguntaForm();
  }

  actualizarPregunta(campaniaId: string) {
    const preguntaId = this.preguntaEditandoId();
    if (!preguntaId) {
      return;
    }

    this.api
      .actualizarPregunta(campaniaId, preguntaId, this.preguntaPayload(this.preguntaEdicion))
      .subscribe({
        next: () => {
          this.cancelarEdicionPregunta();
          this.select(campaniaId);
          this.notificaciones.exito('Pregunta actualizada.');
        },
        error: (err: unknown) => this.reportarError(err),
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
        this.notificaciones.exito('Participantes asociados a la campania.');
      },
      error: (err: unknown) => this.reportarError(err),
    });
  }

  private loadParticipantes(campaniaId: string) {
    this.api.participantes(campaniaId).subscribe({
      next: (items) => this.participantes.set(items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  // P-03: reinicio por participante. Confirmacion simple (destruye datos del flujo de ese usuario).
  reiniciarParticipante(campaniaId: string, participante: ParticipanteCampania) {
    const nombre = this.nombreUsuario(participante.usuarioId);
    if (
      !window.confirm(`Reiniciar la conversacion de ${nombre}? Se borraran sus datos del flujo.`)
    ) {
      return;
    }
    this.api.reiniciarParticipante(campaniaId, participante.usuarioId, false).subscribe({
      next: (reporte) => {
        this.loadParticipantes(campaniaId);
        this.notificaciones.exito(
          `Reiniciado ${nombre}: ${reporte.respuestas} respuestas, ${reporte.conversaciones} conversaciones borradas.`,
        );
      },
      error: (err: unknown) => this.reportarError(err),
    });
  }

  // P-03: reinicio masivo. Confirmacion fuerte: exige escribir el nombre exacto de la campania.
  reiniciarDatosCampania(campania: Campania) {
    const escrito = window.prompt(
      `Esto borrara los datos de prueba de TODOS los participantes de "${campania.nombre}". ` +
        `Escribe el nombre de la campania para confirmar:`,
    );
    if (escrito === null) {
      return;
    }
    if (escrito.trim() !== campania.nombre) {
      this.notificaciones.error('El nombre no coincide; no se reinicio nada.');
      return;
    }
    this.api.reiniciarDatosCampania(campania.id, { reiniciarEnvios: false }).subscribe({
      next: (reporte) => {
        this.loadParticipantes(campania.id);
        this.notificaciones.exito(
          `Campania reiniciada: ${reporte.respuestas} respuestas, ${reporte.conversaciones} conversaciones, ` +
            `${reporte.participantesReseteados} participantes reseteados.`,
        );
      },
      error: (err: unknown) => this.reportarError(err),
    });
  }

  private emptyCampaniaForm() {
    return {
      nombre: '',
      descripcion: '',
      objetivo: '',
      rubricaRef: '',
      configLlmRef: '',
      promptEvaluarRef: '',
      presupuestoTokensCampania: 0,
      segmentacionIdeas: false,
      umbralCierreAnticipado: null as number | null,
    };
  }

  private emptyPreguntaForm(): PreguntaForm {
    return {
      categoria: '',
      texto: '',
      instruccion: '',
      orden: 1,
      estado: 'activo',
      rubricaRef: '',
      promptEvaluarRef: '',
      maxRepreguntas: 1,
      maxCaracteresMensaje: 1500,
      maxLlamadasLlm: 2,
    };
  }

  private formFromCampania(campania: Campania) {
    return {
      nombre: campania.nombre,
      descripcion: campania.descripcion,
      objetivo: campania.objetivo,
      rubricaRef: campania.rubricaRef ?? '',
      configLlmRef: campania.configLLMRef ?? '',
      promptEvaluarRef: campania.promptRefs?.['evaluar'] ?? '',
      presupuestoTokensCampania: campania.configSeguridad?.presupuestoTokensCampania ?? 0,
      segmentacionIdeas: campania.configConversacional?.segmentacionIdeas ?? false,
      umbralCierreAnticipado: campania.configConversacional?.umbralCierreAnticipado ?? null,
    };
  }

  private formFromPregunta(pregunta: Pregunta): PreguntaForm {
    return {
      categoria: pregunta.categoria,
      texto: pregunta.texto,
      instruccion: pregunta.instruccion,
      orden: pregunta.orden,
      estado: pregunta.estado,
      rubricaRef: pregunta.rubricaRef ?? '',
      promptEvaluarRef: pregunta.promptRefs?.['evaluar'] ?? '',
      maxRepreguntas: pregunta.maxRepreguntas ?? 1,
      maxCaracteresMensaje: pregunta.limitesSeguridad?.maxCaracteresMensaje ?? 1500,
      maxLlamadasLlm: pregunta.limitesSeguridad?.maxLlamadasLlm ?? 2,
    };
  }

  private preguntaPayload(form: PreguntaForm) {
    return {
      categoria: form.categoria,
      texto: form.texto,
      instruccion: form.instruccion || form.texto,
      orden: Number(form.orden) || 1,
      estado: form.estado,
      rubricaRef: form.rubricaRef,
      promptRefs: form.promptEvaluarRef ? { evaluar: form.promptEvaluarRef } : {},
      maxRepreguntas: Math.max(0, Number(form.maxRepreguntas) || 0),
      limitesSeguridad: {
        maxCaracteresMensaje: Math.max(1, Number(form.maxCaracteresMensaje) || 1500),
        maxLlamadasLlm: Math.max(1, Number(form.maxLlamadasLlm) || 2),
      },
      configMarkdown: { tipoArtefacto: 'respuesta' },
    };
  }

  private reportarError(err: unknown) {
    const mensaje = formatApiError(err);
    this.error.set(mensaje);
    this.notificaciones.error(mensaje);
  }
}
