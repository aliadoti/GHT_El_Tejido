import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AdminApiService } from '../../core/admin-api.service';
import { Campania, ParticipanteCampania, UsuarioAdmin } from '../../core/api-models';
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
            <label>Rubrica ref <input name="rubricaRef" [(ngModel)]="nueva.rubricaRef" /></label>
            <label
              >Config LLM ref <input name="configLlmRef" [(ngModel)]="nueva.configLlmRef"
            /></label>
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
                <input
                  name="previewArea"
                  [(ngModel)]="filtroParticipantes.area"
                  placeholder="Area"
                />
                <input
                  name="previewEmpresa"
                  [(ngModel)]="filtroParticipantes.empresa"
                  placeholder="Empresa"
                />
                <button class="ghost-button" type="submit">Preview</button>
              </form>
              <p class="muted">Preview: {{ previewUsuarios().length }} usuarios</p>
              @if (auth.isAdmin() && previewUsuarios().length > 0) {
                <button type="button" class="primary-button" (click)="asociarPreview(campania.id)">
                  Asociar preview
                </button>
              }
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
  protected readonly previewUsuarios = signal<UsuarioAdmin[]>([]);
  protected readonly error = signal('');

  protected nueva = {
    nombre: '',
    descripcion: '',
    objetivo: '',
    rubricaRef: 'rubrica_default',
    configLlmRef: 'config_default',
  };
  protected mensaje = { nombreInterno: '', texto: '' };
  protected pregunta = { categoria: '', texto: '' };
  protected filtroParticipantes = { area: '', empresa: '' };

  constructor() {
    this.load();
  }

  load() {
    this.api.campanias({ pageSize: 50 }).subscribe({
      next: (page) => {
        this.campanias.set(page.items);
        this.error.set('');
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
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
    this.api
      .crearCampania({
        ...this.nueva,
        promptRefs: {},
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
            rubricaRef: 'rubrica_default',
            configLlmRef: 'config_default',
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
        ...this.pregunta,
        instruccion: this.pregunta.texto,
        orden: 1,
        estado: 'activo',
        maxRepreguntas: 1,
        limitesSeguridad: { maxCaracteresMensaje: 1500, maxLlamadasLlm: 2 },
        configMarkdown: { tipoArtefacto: 'respuesta' },
      })
      .subscribe({
        next: () => {
          this.pregunta = { categoria: '', texto: '' };
          this.select(campaniaId);
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }

  preview(campaniaId: string) {
    this.api.previewParticipantes(campaniaId, this.filtroParticipantes).subscribe({
      next: (response) => this.previewUsuarios.set(response.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }

  asociarPreview(campaniaId: string) {
    this.api
      .asociarParticipantes(
        campaniaId,
        this.previewUsuarios().map((usuario) => usuario.id),
      )
      .subscribe({
        next: () => this.loadParticipantes(campaniaId),
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
