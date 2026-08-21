import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';

import { AdminApiService, FiltrosIdeas } from '../../core/admin-api.service';
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
import { EventoIdea, construirLineaTiempo } from './linea-tiempo-idea';
import {
  COLUMNAS_RESULTADOS,
  ColumnaResultados,
  ResultadosSesionService,
  VistaResultados,
} from './resultados-sesion.service';

/** P-34 (04 §5.8): filtros que el servidor entiende y que el portal serializa en la URL. */
const LLAVES_FILTRO = [
  'q',
  'estadoResultado',
  'desde',
  'hasta',
  'area',
  'empresa',
  'sede',
  'usuarioId',
  'preguntaId',
  'estadoFlujo',
  'estadoCuraduria',
  'confirmada',
  'calificacionMin',
  'calificacionMax',
] as const satisfies readonly (keyof FiltrosIdeas)[];

/** Los del panel desplegable (nivel 2), que alimentan el contador de «Más filtros». */
const LLAVES_AVANZADAS = [
  'area',
  'empresa',
  'sede',
  'usuarioId',
  'preguntaId',
  'estadoFlujo',
  'estadoCuraduria',
  'confirmada',
  'calificacionMin',
  'calificacionMax',
] as const satisfies readonly (keyof FiltrosIdeas)[];

const ETIQUETAS_FILTRO: Record<(typeof LLAVES_FILTRO)[number], string> = {
  q: 'Búsqueda',
  estadoResultado: 'Estado',
  desde: 'Desde',
  hasta: 'Hasta',
  area: 'Área',
  empresa: 'Empresa',
  sede: 'Sede',
  usuarioId: 'Participante',
  preguntaId: 'Pregunta',
  estadoFlujo: 'Flujo',
  estadoCuraduria: 'Curaduría',
  confirmada: 'Confirmada',
  calificacionMin: 'Calificación mín.',
  calificacionMax: 'Calificación máx.',
};

@Component({
  selector: 'app-resultados-page',
  standalone: true,
  imports: [FormsModule, NgTemplateOutlet, EstadoAccesibleComponent],
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

      <!-- P-34 H-01: el fallo del directorio deja de ser silencioso y se puede reintentar. -->
      @if (errorUsuarios()) {
        <section class="panel resultados-aviso-usuarios">
          <app-estado-accesible tipo="error" [mensaje]="errorUsuarios()" />
          <div class="actions-row">
            <button
              type="button"
              class="ghost-button"
              [disabled]="cargandoUsuarios()"
              (click)="reintentarUsuarios()"
            >
              Reintentar la carga de participantes
            </button>
          </div>
        </section>
      }

      <section class="panel">
        <!-- P-34 §4.2: nivel 1 siempre visible; el resto vive en el panel desplegable. -->
        <form class="filters-grid" (ngSubmit)="aplicarFiltros()">
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
            Buscar
            <input
              type="search"
              name="q"
              [(ngModel)]="filtros.q"
              placeholder="Nombre, código o texto de la idea"
            />
          </label>
          <label>
            Estado de la idea
            <select name="estadoResultado" [(ngModel)]="filtros.estadoResultado">
              <option value="">Todas</option>
              <option value="madura">Maduras</option>
              <option value="pendiente">Pendientes</option>
              <option value="rechazada">Rechazadas</option>
            </select>
          </label>
          <label>
            Desde
            <input type="date" name="desde" [(ngModel)]="filtros.desde" />
          </label>
          <label>
            Hasta
            <input type="date" name="hasta" [(ngModel)]="filtros.hasta" />
          </label>
          <div class="actions-row">
            <button type="submit" class="ghost-button">Aplicar filtros</button>
            <button
              type="button"
              class="ghost-button"
              [attr.aria-expanded]="masFiltros()"
              (click)="alternarMasFiltros()"
            >
              Más filtros{{ conteoFiltrosAvanzados() ? ' · ' + conteoFiltrosAvanzados() : '' }}
            </button>
          </div>
        </form>

        @if (masFiltros()) {
          <form class="filters-grid resultados-filtros-avanzados" (ngSubmit)="aplicarFiltros()">
            <label>
              Área
              <input type="text" name="area" [(ngModel)]="filtros.area" />
            </label>
            <label>
              Empresa
              <input type="text" name="empresa" [(ngModel)]="filtros.empresa" />
            </label>
            <label>
              Sede
              <input type="text" name="sede" [(ngModel)]="filtros.sede" />
            </label>
            <label>
              Participante (id)
              <input type="text" name="usuarioId" [(ngModel)]="filtros.usuarioId" />
            </label>
            <label>
              Pregunta (id)
              <input type="text" name="preguntaId" [(ngModel)]="filtros.preguntaId" />
            </label>
            <label>
              Estado del flujo
              <select name="estadoFlujo" [(ngModel)]="filtros.estadoFlujo">
                <option value="">Todos</option>
                <option value="pendienteConfirmacion">Pendiente de confirmación</option>
                <option value="enMejora">En mejora</option>
                <option value="enRevision">En revisión</option>
                <option value="cerrada">Cerrada</option>
              </select>
            </label>
            <label>
              Curaduría
              <select name="estadoCuraduria" [(ngModel)]="filtros.estadoCuraduria">
                <option value="">Todas</option>
                <option value="pendiente">Pendiente de curaduría</option>
              </select>
            </label>
            <label>
              Confirmada
              <select name="confirmada" [(ngModel)]="filtros.confirmada">
                <option value="">Todas</option>
                <option value="true">Solo confirmadas</option>
                <option value="false">Sin confirmar</option>
              </select>
            </label>
            <label>
              Calificación mínima
              <input
                type="number"
                step="0.1"
                name="calificacionMin"
                [(ngModel)]="filtros.calificacionMin"
              />
            </label>
            <label>
              Calificación máxima
              <input
                type="number"
                step="0.1"
                name="calificacionMax"
                [(ngModel)]="filtros.calificacionMax"
              />
            </label>
            <div class="actions-row">
              <button type="submit" class="ghost-button">Aplicar filtros</button>
            </div>
          </form>
        }

        <!-- Chips: el usuario ve por qué la lista muestra lo que muestra y lo desarma de a uno. -->
        @if (chipsFiltros().length) {
          <div class="resultados-chips" aria-label="Filtros aplicados">
            @for (chip of chipsFiltros(); track chip.clave) {
              <button
                type="button"
                class="status-badge resultados-chip"
                (click)="quitarFiltro(chip.clave)"
              >
                {{ chip.etiqueta }}: {{ chip.valor }}
                <span aria-hidden="true">×</span>
                <span class="sr-only">Quitar filtro {{ chip.etiqueta }}</span>
              </button>
            }
            <button type="button" class="ghost-button" (click)="limpiarFiltros()">
              Limpiar todo
            </button>
          </div>
        }

        <div class="filters-grid">
          <!-- P-34 H-04: el conteo es el de la campaña completa, no el del arreglo cargado. -->
          <div class="resultados-resumen" aria-label="Resumen de ideas">
            <strong>{{ totalIdeas() }} ideas</strong>
            <span>
              {{ conteoIdeas('madura') }} maduras · {{ conteoIdeas('pendiente') }} pendientes ·
              {{ conteoIdeas('rechazada') }} rechazadas
              @if (hayIdeasSinCargar()) {
                <span class="muted">(sobre las {{ ideas().length }} primeras)</span>
              }
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
        <!-- P-34 §4.3: la tabla compara, el maestro-detalle lee. La vista elegida vive en sesión. -->
        <section class="panel resultados-barra-vistas">
          <div class="actions-row" role="group" aria-label="Vista de resultados">
            <button
              type="button"
              class="ghost-button"
              [attr.aria-pressed]="vista() === 'tabla'"
              (click)="cambiarVista('tabla')"
            >
              Vista tabla
            </button>
            <button
              type="button"
              class="ghost-button"
              [attr.aria-pressed]="vista() === 'lectura'"
              (click)="cambiarVista('lectura')"
            >
              Vista lectura
            </button>
          </div>
          @if (vista() === 'tabla') {
            <div class="actions-row">
              <label class="resultados-opcion">
                <input
                  type="checkbox"
                  name="agrupar"
                  [ngModel]="agrupado()"
                  (ngModelChange)="alternarAgrupado()"
                />
                Agrupar por participante
              </label>
              <label class="resultados-opcion">
                <input
                  type="checkbox"
                  name="densidad"
                  [ngModel]="densidadCompacta()"
                  (ngModelChange)="alternarDensidad()"
                />
                Filas compactas
              </label>
              <label class="resultados-opcion">
                Filas por página
                <select
                  name="tamanoPagina"
                  [ngModel]="tamanoPagina()"
                  (ngModelChange)="cambiarTamanoPagina($event)"
                >
                  <option [value]="25">25</option>
                  <option [value]="50">50</option>
                  <option [value]="100">100</option>
                </select>
              </label>
              <details class="resultados-columnas">
                <summary>Columnas</summary>
                <ul class="compact-list">
                  @for (columna of columnasDisponibles; track columna.clave) {
                    <li>
                      <label class="resultados-opcion">
                        <input
                          type="checkbox"
                          [name]="'columna-' + columna.clave"
                          [ngModel]="columnaVisible(columna.clave)"
                          (ngModelChange)="alternarColumna(columna.clave)"
                        />
                        {{ columna.etiqueta }}
                      </label>
                    </li>
                  }
                </ul>
              </details>
            </div>
          }
        </section>

        @if (vista() === 'tabla') {
          <section class="panel">
            <div class="panel-heading">
              <h3>Ideas</h3>
              <span class="muted">{{ resumenPaginacion() }}</span>
            </div>
            @if (cargando()) {
              <div class="resultados-skeleton" aria-label="Cargando ideas">
                <span></span><span></span><span></span>
              </div>
            } @else if (!ideas().length) {
              <p class="muted">{{ mensajeSinIdeas() }}</p>
            } @else {
              <div class="resultados-tabla-scroll">
                <table
                  class="resultados-tabla"
                  [class.compacta]="densidadCompacta()"
                  aria-label="Ideas de la campaña"
                >
                  <thead>
                    <tr>
                      <th scope="col" class="resultados-col-seleccion">
                        <span class="sr-only">Selección</span>
                      </th>
                      <th scope="col" [attr.aria-sort]="ordenDe('participante')">
                        <button
                          type="button"
                          class="resultados-orden"
                          (click)="ordenarPor('participante')"
                        >
                          Participante
                        </button>
                      </th>
                      @if (columnaVisible('area')) {
                        <th scope="col">Área</th>
                      }
                      @if (columnaVisible('pregunta')) {
                        <th scope="col" [attr.aria-sort]="ordenDe('pregunta')">
                          <button
                            type="button"
                            class="resultados-orden"
                            (click)="ordenarPor('pregunta')"
                          >
                            Pregunta
                          </button>
                        </th>
                      }
                      <th scope="col">Idea</th>
                      @if (columnaVisible('estado')) {
                        <th scope="col">Estado</th>
                      }
                      @if (columnaVisible('calificacion')) {
                        <th scope="col" [attr.aria-sort]="ordenDe('calificacion')">
                          <button
                            type="button"
                            class="resultados-orden"
                            (click)="ordenarPor('calificacion')"
                          >
                            Calificación
                          </button>
                        </th>
                      }
                      @if (columnaVisible('creada')) {
                        <th scope="col" [attr.aria-sort]="ordenDe('creada')">
                          <button
                            type="button"
                            class="resultados-orden"
                            (click)="ordenarPor('creada')"
                          >
                            Creada
                          </button>
                        </th>
                      }
                      @if (columnaVisible('actualizada')) {
                        <th scope="col" [attr.aria-sort]="ordenDe('actualizada')">
                          <button
                            type="button"
                            class="resultados-orden"
                            (click)="ordenarPor('actualizada')"
                          >
                            Actualizada
                          </button>
                        </th>
                      }
                      @if (columnaVisible('documento')) {
                        <th scope="col">Documento</th>
                      }
                    </tr>
                  </thead>
                  @for (grupo of gruposPagina(); track grupo.clave) {
                    <tbody>
                      @if (agrupado()) {
                        <tr class="resultados-grupo">
                          <th [attr.colspan]="totalColumnas()" scope="colgroup">
                            <button
                              type="button"
                              class="resultados-orden"
                              [attr.aria-expanded]="!grupoColapsado(grupo.clave)"
                              (click)="alternarGrupo(grupo.clave)"
                            >
                              {{ grupo.etiqueta }} ({{ grupo.filas.length }})
                            </button>
                          </th>
                        </tr>
                      }
                      @if (!agrupado() || !grupoColapsado(grupo.clave)) {
                        @for (idea of grupo.filas; track idea.id) {
                          <tr
                            [class.selected-row]="ideaSeleccionada()?.id === idea.id"
                            [attr.aria-current]="ideaSeleccionada()?.id === idea.id ? 'true' : null"
                          >
                            <td>
                              <label class="resultados-opcion">
                                <span class="sr-only"
                                  >Seleccionar la idea de {{ nombreParticipante(idea) }}</span
                                >
                                <input
                                  type="checkbox"
                                  [name]="'seleccion-' + idea.id"
                                  [ngModel]="estaSeleccionada(idea.id)"
                                  (ngModelChange)="alternarSeleccion(idea.id)"
                                />
                              </label>
                            </td>
                            <th scope="row">
                              <button
                                type="button"
                                class="resultados-orden"
                                (click)="abrirIdea(idea.id)"
                              >
                                {{ nombreParticipante(idea) }}
                              </button>
                              <span class="muted">{{
                                idea.participante?.codigoUsuarioLegible ?? ''
                              }}</span>
                            </th>
                            @if (columnaVisible('area')) {
                              <td>{{ idea.participante?.area ?? '-' }}</td>
                            }
                            @if (columnaVisible('pregunta')) {
                              <td>{{ idea.preguntaId }}</td>
                            }
                            <td class="resultados-col-idea">{{ extracto(idea.texto ?? '') }}</td>
                            @if (columnaVisible('estado')) {
                              <td>
                                <span
                                  class="status-badge"
                                  [class.badge-ok]="idea.estadoResultado === 'madura'"
                                  [class.badge-warn]="idea.estadoResultado === 'rechazada'"
                                  [title]="tituloEstadoIdea(idea)"
                                >
                                  {{ etiquetaEstadoIdea(idea) }}
                                </span>
                              </td>
                            }
                            @if (columnaVisible('calificacion')) {
                              <td>{{ idea.calificacionTotal ?? '-' }}</td>
                            }
                            @if (columnaVisible('creada')) {
                              <td>{{ fechaCorta(idea.creadaEn) }}</td>
                            }
                            @if (columnaVisible('actualizada')) {
                              <td>{{ fechaCorta(idea.actualizadaEn) }}</td>
                            }
                            @if (columnaVisible('documento')) {
                              <td>{{ tieneDocumento(idea.id) ? 'Sí' : 'No' }}</td>
                            }
                          </tr>
                        }
                      }
                    </tbody>
                  }
                </table>
              </div>
              <div class="actions-row">
                <button
                  type="button"
                  class="ghost-button"
                  [disabled]="pagina() === 1"
                  (click)="irAPagina(pagina() - 1)"
                >
                  Anterior
                </button>
                <span class="muted">Página {{ pagina() }} de {{ totalPaginas() }}</span>
                <button
                  type="button"
                  class="ghost-button"
                  [disabled]="pagina() >= totalPaginas()"
                  (click)="irAPagina(pagina() + 1)"
                >
                  Siguiente
                </button>
              </div>
            }
          </section>

          <ng-container [ngTemplateOutlet]="panelDetalle" />
        } @else {
          <div class="resultados-master-detail">
            <section class="panel">
              <div class="panel-heading">
                <h3>Ideas</h3>
                <span class="muted">
                  {{ hayIdeasSinCargar() ? ideas().length + ' de ' + totalIdeas() : totalIdeas() }}
                </span>
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
                          <strong>{{ nombreParticipante(idea) }}</strong>
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
                    <li class="muted">{{ mensajeSinIdeas() }}</li>
                  }
                </ul>
              }
            </section>

            <ng-container [ngTemplateOutlet]="panelDetalle" />
          </div>
        }

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

    <ng-template #panelDetalle>
      <section class="panel resultados-detalle">
        @if (cargandoDetalle()) {
          <div class="resultados-skeleton" aria-label="Cargando detalle">
            <span></span><span></span>
          </div>
        } @else if (detalleIdea(); as detalle) {
          <div class="panel-heading">
            <h3>Detalle de {{ nombreParticipante(detalle.idea) }}</h3>
            <span class="status-badge" [class.badge-ok]="detalle.idea.estadoResultado === 'madura'">
              {{ etiquetaEstadoIdea(detalle.idea) }}
            </span>
          </div>

          <section aria-labelledby="idea-consolidada">
            <h4 id="idea-consolidada">Idea consolidada</h4>
            @if (!detalle.idea.confirmada) {
              <p class="muted">
                Esta versión todavía no fue confirmada por el participante, así que no puede contar
                como madura.
              </p>
            }
            <p>{{ detalle.idea.texto ?? 'Sin versión consolidada todavía.' }}</p>
            <!-- P-34 §4.4 (H-05): la metadata que la API ya devolvía y no se pintaba. -->
            <div class="detail-grid">
              <div>
                <span class="muted">Participante</span>
                <p>{{ nombreParticipante(detalle.idea) }}</p>
              </div>
              <div>
                <span class="muted">Código</span>
                <p>{{ detalle.idea.participante?.codigoUsuarioLegible ?? '-' }}</p>
              </div>
              <div>
                <span class="muted">Empresa y sede</span>
                <p>
                  {{ detalle.idea.participante?.empresa ?? '-' }} ·
                  {{ detalle.idea.participante?.sede ?? '-' }}
                </p>
              </div>
              <div>
                <span class="muted">Pregunta</span>
                <p>{{ detalle.idea.preguntaId }}</p>
              </div>
              <div>
                <span class="muted">Idea número</span>
                <p>{{ detalle.idea.ideaIndice }}</p>
              </div>
              <div>
                <span class="muted">Estado</span>
                <p>{{ etiquetaEstadoIdea(detalle.idea) }}</p>
              </div>
              <div>
                <span class="muted">Creada</span>
                <p>{{ fechaLarga(detalle.idea.creadaEn) }}</p>
              </div>
              <div>
                <span class="muted">Actualizada</span>
                <p>{{ fechaLarga(detalle.idea.actualizadaEn) }}</p>
              </div>
              <div>
                <span class="muted">Confirmada</span>
                <p>
                  {{
                    detalle.versionConfirmada
                      ? 'Sí, versión ' + detalle.versionConfirmada.numeroVersion
                      : 'Todavía no'
                  }}
                </p>
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
              <div>
                <span class="muted">Rúbrica y modelo</span>
                <p>{{ rubricaYModelo(detalle.evaluacion) }}</p>
              </div>
              <div class="wide">
                <span class="muted">Identificador técnico</span>
                <p class="resultados-id-tecnico">
                  <code>{{ detalle.idea.id }}</code>
                  <button type="button" class="ghost-button" (click)="copiar(detalle.idea.id)">
                    Copiar
                  </button>
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
              <p class="muted">Esta idea todavía no tiene una evaluación de su versión vigente.</p>
            }
          </section>

          <!-- P-34 §4.4: una sola secuencia cronológica, como ocurrió la conversación. -->
          <details class="resultados-historial">
            <summary>
              Línea de tiempo de la idea ({{ detalle.aportes.length }} aportes ·
              {{ detalle.versiones.length }} versiones)
            </summary>
            <ol class="compact-list resultados-linea-tiempo">
              @for (evento of lineaTiempo(detalle); track evento.id) {
                <li>
                  <span class="status-badge">{{ evento.etiqueta }}</span>
                  <span class="muted">{{ fechaLarga(evento.fecha) }}</span>
                  <span>{{ extracto(evento.texto) }}</span>
                </li>
              } @empty {
                <li class="muted">Esta idea todavía no tiene aportes ni versiones registradas.</li>
              }
            </ol>
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
                  <button type="button" class="ghost-button" (click)="descargar(selectedMarkdown)">
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
                  <button type="button" class="ghost-button" (click)="descargar(selectedMarkdown)">
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
    </ng-template>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResultadosPage {
  private readonly api = inject(AdminApiService);
  private readonly sesion = inject(ResultadosSesionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
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
  protected readonly errorUsuarios = signal('');
  protected readonly informacion = signal('');
  protected readonly cargando = signal(false);
  protected readonly cargandoUsuarios = signal(false);
  protected readonly cargandoDetalle = signal(false);
  /** P-34 H-04: total declarado por el servidor para la campaña y el filtro vigentes. */
  protected readonly totalIdeas = signal(0);
  protected campaniaId = '';
  /** P-34 §4.2: los filtros del servidor, tal como viajan en la query y en la URL del portal. */
  protected filtros: FiltrosIdeas = {};
  protected readonly masFiltros = signal(false);
  /** P-34 §4.3: preferencias de la tabla; viven en sesión, no en `localStorage` (01 §11). */
  protected readonly vista = signal<VistaResultados>(this.sesion.vista);
  protected readonly agrupado = signal(this.sesion.agruparPorParticipante);
  protected readonly densidadCompacta = signal(this.sesion.densidadCompacta);
  protected readonly tamanoPagina = signal(this.sesion.tamanoPagina);
  protected readonly columnas = signal<ColumnaResultados[]>([...this.sesion.columnas]);
  protected readonly pagina = signal(1);
  protected readonly seleccion = signal<ReadonlySet<string>>(new Set());
  protected readonly gruposColapsados = signal<ReadonlySet<string>>(new Set());
  /** Total de la campaña sin filtros, para que la paginación diga la verdad completa (§4.3). */
  protected readonly totalCampania = signal(0);
  protected readonly columnasDisponibles = COLUMNAS_RESULTADOS;
  protected nivelMadurezFiltro = '';
  private cargasPendientes = 0;
  private urlAplicada = '';

  constructor() {
    // P-34 H-09: la vista vive en la URL, así que se puede compartir, recargar y volver atrás.
    this.route.queryParamMap.subscribe((parametros) => {
      const serializada = parametros.keys
        .map((clave) => `${clave}=${parametros.get(clave)}`)
        .sort()
        .join('&');
      if (serializada === this.urlAplicada) return;
      this.urlAplicada = serializada;
      this.filtros = LLAVES_FILTRO.reduce((acumulado: FiltrosIdeas, clave) => {
        const valor = parametros.get(clave);
        if (valor) acumulado[clave] = valor;
        return acumulado;
      }, {});
      this.masFiltros.set(this.conteoFiltrosAvanzados() > 0);
      const campaniaDeUrl = parametros.get('campaniaId');
      if (campaniaDeUrl && campaniaDeUrl !== this.campaniaId) {
        this.campaniaId = campaniaDeUrl;
      }
      if (this.campaniaId && this.campanias().length) this.loadAll();
    });
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
    this.cargarUsuarios();
  }

  /**
   * P-34 H-01/H-02: el directorio completo, con el fallo visible. Antes se pedía una sola página de
   * 500 —que el servidor recortaba a 100— y el error se descartaba, así que la pantalla se veía
   * normal mientras todas las filas mostraban el id técnico.
   */
  cargarUsuarios() {
    this.cargandoUsuarios.set(true);
    this.api
      .usuariosTodos()
      .pipe(finalize(() => this.cargandoUsuarios.set(false)))
      .subscribe({
        next: (page) => {
          this.usuarios.set(new Map(page.items.map((u) => [u.id, u])));
          this.errorUsuarios.set('');
        },
        error: (err: unknown) =>
          this.errorUsuarios.set(
            `No se pudo cargar la lista de participantes, así que las ideas se muestran sin nombre. ${formatApiError(err)}`,
          ),
      });
  }

  reintentarUsuarios() {
    if (this.cargandoUsuarios()) return;
    this.cargarUsuarios();
  }

  /**
   * P-34 §4.1: la identidad la resuelve el servidor. Si el backend es anterior a P-34 y no manda
   * `participante`, se cae al maestro descargado —el comportamiento previo— y, si tampoco está,
   * al texto legible con el código corto.
   */
  nombreParticipante(idea: IdeaConsolidada): string {
    const participante = idea.participante;
    if (participante?.resuelto && participante.nombre) {
      return participante.area
        ? `${participante.nombre} (${participante.area})`
        : participante.nombre;
    }
    if (participante && !participante.resuelto) {
      return `Participante no identificado · ${this.codigoCorto(participante.codigoUsuarioLegible ?? idea.usuarioId)}`;
    }
    return this.nombreUsuario(idea.usuarioId);
  }

  /** P-34 H-01: nunca un id técnico pelado; el código corto queda visible para poder rastrear. */
  nombreUsuario(usuarioId: string): string {
    const usuario = this.usuarios().get(usuarioId);
    if (!usuario) return `Participante no identificado · ${this.codigoCorto(usuarioId)}`;
    return usuario.area ? `${usuario.nombre} (${usuario.area})` : usuario.nombre;
  }

  codigoCorto(usuarioId: string): string {
    const limite = 12;
    return usuarioId.length > limite ? `${usuarioId.slice(0, limite)}…` : usuarioId;
  }

  /**
   * Desde el corte 2 el listado se recorre completo, así que esto solo es cierto si el servidor
   * declara más ideas de las que llegó a entregar; el desglose por estado lo dice en vez de mentir.
   */
  hayIdeasSinCargar(): boolean {
    return this.totalIdeas() > this.ideas().length;
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

  // ---- P-34 §4.3: vista tabla ------------------------------------------------------------------

  cambiarVista(vista: VistaResultados) {
    this.vista.set(vista);
    this.sesion.vista = vista;
  }

  alternarAgrupado() {
    const valor = !this.agrupado();
    this.agrupado.set(valor);
    this.sesion.agruparPorParticipante = valor;
    this.pagina.set(1);
  }

  alternarDensidad() {
    const valor = !this.densidadCompacta();
    this.densidadCompacta.set(valor);
    this.sesion.densidadCompacta = valor;
  }

  cambiarTamanoPagina(valor: string | number) {
    const tamano = Number(valor) || 25;
    this.tamanoPagina.set(tamano);
    this.sesion.tamanoPagina = tamano;
    this.pagina.set(1);
  }

  columnaVisible(clave: ColumnaResultados): boolean {
    return this.columnas().includes(clave);
  }

  alternarColumna(clave: ColumnaResultados) {
    const visibles = this.columnaVisible(clave)
      ? this.columnas().filter((columna) => columna !== clave)
      : [...this.columnas(), clave];
    this.columnas.set(visibles);
    this.sesion.columnas = [...visibles];
  }

  /** Columnas realmente pintadas, para el `colspan` de la fila de grupo. */
  totalColumnas(): number {
    return 3 + this.columnas().length;
  }

  /**
   * P-34 §4.3: el orden lo resuelve el servidor. Ordenar en el cliente con paginación sería un orden
   * falso —solo reordenaría lo que ya se trajo—, así que el clic viaja como `orden`/`dir`.
   */
  ordenarPor(columna: string) {
    const mismaColumna = this.filtros.orden === columna;
    this.filtros.orden = columna;
    this.filtros.dir = mismaColumna && this.filtros.dir !== 'desc' ? 'desc' : 'asc';
    if (mismaColumna && this.filtros.dir === 'asc') {
      // Tercer clic: se vuelve al orden natural de I-19 en vez de dejarlo pegado.
      delete this.filtros.orden;
      delete this.filtros.dir;
    }

    this.pagina.set(1);
    this.aplicarFiltros();
  }

  /** Valor de `aria-sort` que anuncia el orden vigente al lector de pantalla (P-18/P-19). */
  ordenDe(columna: string): 'ascending' | 'descending' | 'none' {
    if (this.filtros.orden !== columna) return 'none';
    return this.filtros.dir === 'desc' ? 'descending' : 'ascending';
  }

  estaSeleccionada(ideaId: string): boolean {
    return this.seleccion().has(ideaId);
  }

  /** D4: la curaduría se mira, no se marca. La selección existe, pero todavía no ejecuta nada. */
  alternarSeleccion(ideaId: string) {
    const seleccion = new Set(this.seleccion());
    if (!seleccion.delete(ideaId)) seleccion.add(ideaId);
    this.seleccion.set(seleccion);
  }

  grupoColapsado(clave: string): boolean {
    return this.gruposColapsados().has(clave);
  }

  alternarGrupo(clave: string) {
    const colapsados = new Set(this.gruposColapsados());
    if (!colapsados.delete(clave)) colapsados.add(clave);
    this.gruposColapsados.set(colapsados);
  }

  totalPaginas(): number {
    return Math.max(1, Math.ceil(this.ideas().length / this.tamanoPagina()));
  }

  irAPagina(pagina: number) {
    this.pagina.set(Math.min(Math.max(1, pagina), this.totalPaginas()));
  }

  /** Filas de la página actual, agrupadas por participante cuando se pide (§4.3). */
  gruposPagina(): { clave: string; etiqueta: string; filas: IdeaConsolidada[] }[] {
    const inicio = (this.pagina() - 1) * this.tamanoPagina();
    const filas = this.ideas().slice(inicio, inicio + this.tamanoPagina());
    if (!this.agrupado()) {
      return [{ clave: 'todas', etiqueta: 'Todas', filas }];
    }

    const grupos = new Map<string, { clave: string; etiqueta: string; filas: IdeaConsolidada[] }>();
    for (const idea of filas) {
      const clave = idea.usuarioId;
      const grupo = grupos.get(clave) ?? {
        clave,
        etiqueta: this.nombreParticipante(idea),
        filas: [],
      };
      grupo.filas.push(idea);
      grupos.set(clave, grupo);
    }

    return [...grupos.values()];
  }

  /**
   * P-34 §4.3 (H-04): paginación honesta. Distingue lo que se está mostrando, lo que dejó el filtro
   * y lo que tiene la campaña completa, en vez de insinuar que la página es todo lo que existe.
   */
  resumenPaginacion(): string {
    const total = this.ideas().length;
    if (!total) return 'Sin ideas para este filtro';

    const inicio = (this.pagina() - 1) * this.tamanoPagina() + 1;
    const fin = Math.min(inicio + this.tamanoPagina() - 1, total);
    const filtradas = `Mostrando ${inicio}–${fin} de ${total} filtradas`;
    const campania = this.totalCampania();
    return campania && campania !== total ? `${filtradas} · ${campania} en la campaña` : filtradas;
  }

  tieneDocumento(ideaId: string): boolean {
    return this.artefactos().some((artefacto) => artefacto.ideaRef === ideaId);
  }

  // ---- P-34 §4.4: ficha y línea de tiempo -------------------------------------------------------

  lineaTiempo(detalle: DetalleIdea): EventoIdea[] {
    return construirLineaTiempo(detalle);
  }

  /** Fecha absoluta en la zona de la operación (`America/Bogota`) más el «hace cuánto». */
  fechaLarga(fecha?: string | null): string {
    if (!fecha) return '-';
    const momento = new Date(fecha);
    if (Number.isNaN(momento.getTime())) return '-';

    const absoluta = momento.toLocaleString('es-CO', {
      timeZone: 'America/Bogota',
      dateStyle: 'medium',
      timeStyle: 'short',
    });
    return `${absoluta} (${this.relativa(momento)})`;
  }

  fechaCorta(fecha?: string | null): string {
    if (!fecha) return '-';
    const momento = new Date(fecha);
    if (Number.isNaN(momento.getTime())) return '-';
    return momento.toLocaleDateString('es-CO', { timeZone: 'America/Bogota', dateStyle: 'short' });
  }

  rubricaYModelo(evaluacion: Evaluacion | null): string {
    if (!evaluacion) return 'Sin evaluación vigente';
    const rubrica = evaluacion.rubricaRef
      ? `${evaluacion.rubricaRef} v${evaluacion.versionRubrica ?? '?'}`
      : 'Rúbrica no registrada';
    const modelo = evaluacion.configLLMSnapshot?.modelo ?? 'modelo no registrado';
    return `${rubrica} · ${modelo}`;
  }

  copiar(texto: string) {
    void navigator.clipboard?.writeText(texto);
    this.informacion.set('Identificador copiado.');
  }

  private relativa(momento: Date): string {
    const dias = Math.round((momento.getTime() - Date.now()) / 86400000);
    if (dias === 0) return 'hoy';
    const formato = new Intl.RelativeTimeFormat('es-CO', { numeric: 'auto' });
    return formato.format(dias, 'day');
  }

  cambiarCampania() {
    this.aplicarFiltros();
  }

  /** Escribe el estado en la URL; la suscripción a la query es la que dispara la recarga. */
  aplicarFiltros() {
    const queryParams: Record<string, string> = {};
    if (this.campaniaId) queryParams['campaniaId'] = this.campaniaId;
    for (const clave of LLAVES_FILTRO) {
      const valor = (this.filtros[clave] ?? '').toString().trim();
      if (valor) queryParams[clave] = valor;
    }

    const serializada = Object.entries(queryParams)
      .map(([clave, valor]) => `${clave}=${valor}`)
      .sort()
      .join('&');
    // La suscripción a la query ignora esta emisión porque el estado ya quedó registrado aquí.
    this.urlAplicada = serializada;
    this.router.navigate([], { relativeTo: this.route, queryParams, replaceUrl: true });
    this.loadAll();
  }

  alternarMasFiltros() {
    this.masFiltros.update((visible) => !visible);
  }

  /** Cuántos filtros del nivel 2 están puestos, para el contador del botón «Más filtros». */
  conteoFiltrosAvanzados(): number {
    return LLAVES_AVANZADAS.filter((clave) => (this.filtros[clave] ?? '').toString().trim()).length;
  }

  /** Un chip por filtro aplicado: se ve por qué la lista muestra lo que muestra. */
  chipsFiltros(): { clave: keyof FiltrosIdeas; etiqueta: string; valor: string }[] {
    return LLAVES_FILTRO.filter((clave) => (this.filtros[clave] ?? '').toString().trim()).map(
      (clave) => ({
        clave,
        etiqueta: ETIQUETAS_FILTRO[clave],
        valor: (this.filtros[clave] ?? '').toString(),
      }),
    );
  }

  quitarFiltro(clave: keyof FiltrosIdeas) {
    delete this.filtros[clave];
    this.aplicarFiltros();
  }

  limpiarFiltros() {
    this.filtros = {};
    this.aplicarFiltros();
  }

  /** El vacío nombra el filtro que lo produjo, en vez de sugerir que la campaña está vacía. */
  mensajeSinIdeas(): string {
    const chips = this.chipsFiltros();
    if (!chips.length) {
      return 'Esta campaña todavía no tiene ideas registradas. Revisa que haya recibido mensajes.';
    }

    const descripcion = chips.map((chip) => `${chip.etiqueta} «${chip.valor}»`).join(', ');
    return `Ninguna idea coincide con los filtros aplicados (${descripcion}). Quita alguno para ver más.`;
  }

  loadAll() {
    if (!this.campaniaId) {
      this.error.set('');
      this.informacion.set('Elige una campaña para consultar sus resultados.');
      return;
    }

    this.sesion.campaniaId = this.campaniaId;
    this.pagina.set(1);
    this.error.set('');
    this.informacion.set('');
    this.cargando.set(true);
    this.cargasPendientes = 4;
    this.respuestaSeleccionada.set(null);
    this.ideaSeleccionada.set(null);
    this.detalleIdea.set(null);
    this.evaluacion.set(null);
    this.markdown.set(null);

    // El total sin filtros es lo que permite decir «… · N en la campaña» sin inventar (§4.3).
    this.api.conteoIdeasCampania(this.campaniaId).subscribe({
      next: (page) => this.totalCampania.set(page.total ?? 0),
      error: () => this.totalCampania.set(0),
    });
    this.api
      .ideasTodas(this.campaniaId, this.filtrosParaApi())
      .pipe(finalize(() => this.finalizarCarga()))
      .subscribe({
        next: (page) => {
          this.ideas.set(page.items);
          this.totalIdeas.set(page.total ?? page.items.length);
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
    this.api
      .conversacionesTodas(this.campaniaId)
      .pipe(finalize(() => this.finalizarCarga()))
      .subscribe({
        next: (page) => this.conversaciones.set(page.items),
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
    this.api
      .respuestasTodas(this.campaniaId, this.nivelMadurezFiltro)
      .pipe(finalize(() => this.finalizarCarga()))
      .subscribe({
        next: (page) => this.respuestas.set(page.items),
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
    this.api
      .markdownTodo(this.campaniaId)
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

  /**
   * El selector de fecha da `aaaa-mm-dd`; el rango se manda como instantes para que «hasta» incluya
   * el día completo y no corte en su medianoche.
   */
  private filtrosParaApi(): FiltrosIdeas {
    const parametros: FiltrosIdeas = { ...this.filtros };
    if (parametros.desde) parametros.desde = `${parametros.desde}T00:00:00Z`;
    if (parametros.hasta) parametros.hasta = `${parametros.hasta}T23:59:59Z`;
    return parametros;
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
