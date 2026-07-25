import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnChanges,
  inject,
  input,
  output,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import {
  Campania,
  ConfigLlm,
  ParticipanteCampania,
  ParticipantePreview,
  Pregunta,
  PromptConfig,
  Rubrica,
} from '../../core/api-models';

export interface CampaniasFiltro {
  estado: string;
  busqueda: string;
}
export interface CampaniaCrearForm {
  nombre: string;
  descripcion: string;
  objetivo: string;
  rubricaRef: string;
  configLlmRef: string;
  promptEvaluarRef: string;
}
export interface CampaniaEdicionForm extends CampaniaCrearForm {
  presupuestoTokensCampania: number;
  segmentacionIdeas: boolean;
  parafraseo: boolean;
  umbralCierreAnticipado: number | null;
  minutosInactividadSesion: number | null;
}
export type TabCampania = 'config' | 'mensajes' | 'preguntas' | 'participantes';
export interface MensajeInicialForm {
  nombreInterno: string;
  texto: string;
  plantillaNombre: string;
  plantillaIdioma: string;
  plantillaComponentes: string;
}
export interface PreguntaForm {
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
  umbralCierreAnticipado: number | null;
}
export interface ParticipantesFiltro {
  area: string;
  empresa: string;
}
export interface PreguntaActualizada {
  id: string;
  form: PreguntaForm;
}

export function crearFormularioVacio(): CampaniaCrearForm {
  return {
    nombre: '',
    descripcion: '',
    objetivo: '',
    rubricaRef: '',
    configLlmRef: '',
    promptEvaluarRef: '',
  };
}

export function preguntaVacia(): PreguntaForm {
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
    umbralCierreAnticipado: null,
  };
}

export function formularioDesdePregunta(pregunta: Pregunta): PreguntaForm {
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
    umbralCierreAnticipado: pregunta.umbralCierreAnticipado ?? null,
  };
}

export function formularioDesdeCampania(campania: Campania): CampaniaEdicionForm {
  return {
    nombre: campania.nombre,
    descripcion: campania.descripcion,
    objetivo: campania.objetivo,
    rubricaRef: campania.rubricaRef ?? '',
    configLlmRef: campania.configLLMRef ?? '',
    promptEvaluarRef: campania.promptRefs?.['evaluar'] ?? '',
    presupuestoTokensCampania: campania.configSeguridad?.presupuestoTokensCampania ?? 0,
    segmentacionIdeas: campania.configConversacional?.segmentacionIdeas ?? false,
    parafraseo: campania.configConversacional?.parafraseo ?? false,
    umbralCierreAnticipado: campania.configConversacional?.umbralCierreAnticipado ?? null,
    minutosInactividadSesion: campania.configConversacional?.minutosInactividadSesion ?? null,
  };
}

@Component({
  selector: 'app-campanias-lista-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="panel">
    <div class="panel-heading"><h3>Lista</h3></div>
    <form class="filters-grid" (ngSubmit)="buscar.emit(filtro)">
      <label
        >Estado<select name="estadoFiltro" [(ngModel)]="filtro.estado">
          <option value="">Todos</option>
          <option value="borrador">Borrador</option>
          <option value="activa">Activa</option>
          <option value="cerrada">Cerrada</option>
          <option value="archivada">Archivada</option>
        </select></label
      ><label
        >Busqueda<input
          name="busquedaFiltro"
          [(ngModel)]="filtro.busqueda"
          placeholder="Nombre" /></label
      ><button class="ghost-button" type="submit">Buscar</button>
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
            <tr [class.selected-row]="seleccionadaId() === campania.id">
              <td>{{ campania.nombre }}</td>
              <td>
                <span class="status-badge">{{ campania.estado }}</span>
              </td>
              <td>{{ campania.objetivo }}</td>
              <td>
                <button type="button" class="table-button" (click)="abrir.emit(campania.id)">
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
  </section>`,
})
export class CampaniasListaPanel {
  readonly campanias = input.required<readonly Campania[]>();
  readonly seleccionadaId = input<string | null>(null);
  readonly buscar = output<CampaniasFiltro>();
  readonly abrir = output<string>();
  protected filtro: CampaniasFiltro = { estado: '', busqueda: '' };
}

@Component({
  selector: 'app-campania-creacion-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="panel">
    <div class="panel-heading"><h3>Crear campania</h3></div>
    <form class="form-grid" (ngSubmit)="guardar.emit(formulario)">
      <label>Nombre <input name="nombre" [(ngModel)]="formulario.nombre" /></label
      ><label>Descripcion <input name="descripcion" [(ngModel)]="formulario.descripcion" /></label
      ><label
        >Objetivo
        <textarea name="objetivo" rows="3" [(ngModel)]="formulario.objetivo"></textarea></label
      ><label
        >Rubrica<select name="rubricaRef" [(ngModel)]="formulario.rubricaRef" required>
          <option value="" disabled>Selecciona una rubrica</option>
          @for (rubrica of rubricas(); track rubrica.id) {
            <option [value]="rubrica.id">{{ rubrica.nombre }}</option>
          }
        </select></label
      >
      @if (rubricas().length === 0) {
        <p class="muted">No hay rubricas activas. Crea una en la seccion Rubricas.</p>
      }
      <label
        >Config LLM<select name="configLlmRef" [(ngModel)]="formulario.configLlmRef" required>
          <option value="" disabled>Selecciona una configuracion LLM</option>
          @for (config of configsLlm(); track config.id) {
            <option [value]="config.id">{{ config.nombre }}</option>
          }
        </select></label
      >
      @if (configsLlm().length === 0) {
        <p class="muted">No hay configuraciones LLM. Crea una en la seccion Config LLM.</p>
      }
      <label
        >Prompt de evaluacion<select
          name="promptEvaluarRef"
          [(ngModel)]="formulario.promptEvaluarRef"
        >
          <option value="">Sin prompt (configurar por pregunta o luego)</option>
          @for (prompt of prompts(); track prompt.id) {
            <option [value]="prompt.id">{{ prompt.nombre }} ({{ prompt.tipoPrompt }})</option>
          }
        </select></label
      ><button class="primary-button" type="submit" [disabled]="!esAdmin()">
        Guardar campania
      </button>
    </form>
  </section>`,
})
export class CampaniaCreacionPanel implements OnChanges {
  readonly rubricas = input.required<readonly Rubrica[]>();
  readonly configsLlm = input.required<readonly ConfigLlm[]>();
  readonly prompts = input.required<readonly PromptConfig[]>();
  readonly esAdmin = input.required<boolean>();
  readonly reiniciar = input(0);
  readonly guardar = output<CampaniaCrearForm>();
  protected formulario = crearFormularioVacio();
  ngOnChanges(): void {
    if (this.reiniciar()) this.formulario = crearFormularioVacio();
  }
}

@Component({
  selector: 'app-campania-configuracion-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<article>
    <h4>Configuracion</h4>
    <form class="form-grid" (ngSubmit)="guardar.emit(formulario)">
      <label>Nombre <input name="editarNombre" [(ngModel)]="formulario.nombre" /></label
      ><label
        >Descripcion<input name="editarDescripcion" [(ngModel)]="formulario.descripcion" /></label
      ><label
        >Objetivo<textarea
          name="editarObjetivo"
          rows="3"
          [(ngModel)]="formulario.objetivo"
        ></textarea></label
      ><label
        >Rubrica<select name="editarRubricaRef" [(ngModel)]="formulario.rubricaRef" required>
          @for (rubrica of rubricas(); track rubrica.id) {
            <option [value]="rubrica.id">{{ rubrica.nombre }}</option>
          }
        </select></label
      ><label
        >Config LLM<select name="editarConfigLlmRef" [(ngModel)]="formulario.configLlmRef" required>
          @for (config of configsLlm(); track config.id) {
            <option [value]="config.id">{{ config.nombre }}</option>
          }
        </select></label
      ><label
        >Presupuesto de tokens LLM (0 = sin limite)<input
          type="number"
          min="0"
          name="editarPresupuestoTokens"
          [(ngModel)]="formulario.presupuestoTokensCampania" /></label
      ><label class="checkbox-label"
        ><input
          type="checkbox"
          name="editarSegmentacionIdeas"
          [(ngModel)]="formulario.segmentacionIdeas"
        />Separar varias ideas de un mismo mensaje</label
      ><label class="checkbox-label"
        ><input
          type="checkbox"
          name="editarParafraseo"
          [(ngModel)]="formulario.parafraseo"
        />Devolver parafrasis ("esto es lo que entendi") en respuestas maduras</label
      ><label
        >Umbral de madurez / cierre (0 a 1; vacio = heredar global; 0 = apagar cierre)<input
          type="number"
          min="0"
          max="1"
          step="0.01"
          name="editarUmbralCierreAnticipado"
          [(ngModel)]="formulario.umbralCierreAnticipado"
        /><small class="muted"
          >Umbral unico: decide que respuestas quedan maduras y, si el cierre esta habilitado,
          cuales cierran la conversacion.</small
        ></label
      ><label
        >Cierre por inactividad (minutos; vacio = heredar global; 0 = apagar)<input
          type="number"
          min="0"
          step="1"
          name="editarMinutosInactividadSesion"
          [(ngModel)]="formulario.minutosInactividadSesion" /></label
      ><label
        >Prompt de evaluacion<select
          name="editarPromptEvaluarRef"
          [(ngModel)]="formulario.promptEvaluarRef"
        >
          <option value="">Sin prompt por defecto</option>
          @for (prompt of prompts(); track prompt.id) {
            <option [value]="prompt.id">{{ prompt.nombre }} ({{ prompt.tipoPrompt }})</option>
          }
        </select></label
      ><button class="primary-button" type="submit" [disabled]="!esAdmin()">Guardar cambios</button>
    </form>
  </article>`,
})
export class CampaniaConfiguracionPanel implements OnChanges {
  readonly campania = input.required<Campania>();
  readonly rubricas = input.required<readonly Rubrica[]>();
  readonly configsLlm = input.required<readonly ConfigLlm[]>();
  readonly prompts = input.required<readonly PromptConfig[]>();
  readonly esAdmin = input.required<boolean>();
  readonly guardar = output<CampaniaEdicionForm>();
  protected formulario = crearFormularioVacio() as CampaniaEdicionForm;
  ngOnChanges(): void {
    this.formulario = formularioDesdeCampania(this.campania());
  }
}

@Component({
  selector: 'app-mensajes-iniciales-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<article>
    <h4>Mensajes iniciales</h4>
    <form class="form-grid" (ngSubmit)="guardar.emit(formulario)">
      <input
        name="miNombre"
        [(ngModel)]="formulario.nombreInterno"
        placeholder="Nombre interno"
      /><textarea
        name="miTexto"
        rows="3"
        [(ngModel)]="formulario.texto"
        placeholder="Texto"
      ></textarea>
      <p class="subhead">Plantilla WhatsApp (requerida para el envio inicial proactivo)</p>
      <input
        name="miPlantillaNombre"
        [(ngModel)]="formulario.plantillaNombre"
        placeholder="Plantilla aprobada (ej: el_tejido_saludo)"
      /><input
        name="miPlantillaIdioma"
        [(ngModel)]="formulario.plantillaIdioma"
        placeholder="Idioma (ej: es)"
      /><input
        name="miPlantillaComponentes"
        [(ngModel)]="formulario.plantillaComponentes"
        placeholder="Variables en orden, coma-separadas (ej: nombre, campania)"
      /><button class="primary-button" type="submit" [disabled]="!esAdmin()">
        Agregar mensaje
      </button>
    </form>
    <ul class="compact-list">
      @for (item of mensajes(); track item.id) {
        <li>
          <strong>{{ item.nombreInterno }}</strong
          ><span>{{ item.texto }}</span>
        </li>
      } @empty {
        <li class="muted">Sin mensajes.</li>
      }
    </ul>
  </article>`,
})
export class MensajesInicialesPanel implements OnChanges {
  readonly mensajes =
    input.required<readonly NonNullable<Campania['mensajesIniciales']>[number][]>();
  readonly esAdmin = input.required<boolean>();
  readonly guardar = output<MensajeInicialForm>();
  protected formulario: MensajeInicialForm = {
    nombreInterno: '',
    texto: '',
    plantillaNombre: '',
    plantillaIdioma: 'es',
    plantillaComponentes: '',
  };
  ngOnChanges(): void {
    this.formulario = {
      nombreInterno: '',
      texto: '',
      plantillaNombre: '',
      plantillaIdioma: 'es',
      plantillaComponentes: '',
    };
  }
}

@Component({
  selector: 'app-preguntas-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<article>
    <h4>Preguntas</h4>
    <form class="form-grid" (ngSubmit)="crear.emit(nueva)">
      <label
        >Categoria<input
          name="preguntaCategoria"
          [(ngModel)]="nueva.categoria"
          placeholder="Categoria" /></label
      ><label
        >Pregunta<textarea
          name="preguntaTexto"
          rows="3"
          [(ngModel)]="nueva.texto"
          placeholder="Texto que recibira el participante"
        ></textarea></label
      ><label
        >Instruccion de evaluacion<textarea
          name="preguntaInstruccion"
          rows="2"
          [(ngModel)]="nueva.instruccion"
          placeholder="Criterio operativo para evaluar la respuesta"
        ></textarea>
      </label>
      <div class="inline-form">
        <label
          >Orden<input
            type="number"
            min="1"
            name="preguntaOrden"
            [(ngModel)]="nueva.orden" /></label
        ><label
          >Revisiones<input
            type="number"
            min="0"
            name="preguntaMaxRepreguntas"
            [(ngModel)]="nueva.maxRepreguntas" /></label
        ><label
          >Umbral (opcional)<input
            type="number"
            min="0"
            max="1"
            step="0.01"
            name="preguntaUmbral"
            placeholder="hereda campania"
            [(ngModel)]="nueva.umbralCierreAnticipado"
        /></label>
      </div>
      <label
        >Rubrica (opcional, sobreescribe la campania)<select
          name="preguntaRubricaRef"
          [(ngModel)]="nueva.rubricaRef"
        >
          <option value="">Heredar de la campania</option>
          @for (rubrica of rubricas(); track rubrica.id) {
            <option [value]="rubrica.id">{{ rubrica.nombre }}</option>
          }
        </select></label
      ><label
        >Prompt de evaluacion (opcional, sobreescribe la campania)<select
          name="preguntaPromptRef"
          [(ngModel)]="nueva.promptEvaluarRef"
        >
          <option value="">Heredar de la campania</option>
          @for (prompt of prompts(); track prompt.id) {
            <option [value]="prompt.id">{{ prompt.nombre }} ({{ prompt.tipoPrompt }})</option>
          }
        </select></label
      ><button class="primary-button" type="submit" [disabled]="!esAdmin()">
        Agregar pregunta
      </button>
    </form>
    <ul class="compact-list">
      @for (item of preguntas(); track item.id) {
        <li>
          <strong>{{ item.categoria }}</strong
          ><span>{{ item.texto }}</span
          ><span
            >Orden {{ item.orden }} · {{ item.estado }} · Revisiones: {{ item.maxRepreguntas }}
            @if (item.umbralCierreAnticipado != null) {
              · Umbral: {{ item.umbralCierreAnticipado }}
            }
          </span>
          @if (esAdmin()) {
            <button type="button" class="table-button" (click)="editar(item)">Editar</button>
          }
        </li>
      } @empty {
        <li class="muted">Sin preguntas.</li>
      }
    </ul>
    @if (editandoId) {
      <div class="edit-block">
        <div class="panel-heading">
          <h5 class="subhead">Editar pregunta</h5>
          <button type="button" class="ghost-button" (click)="cancelar()">Cancelar</button>
        </div>
        <form class="form-grid" (ngSubmit)="guardarEdicion()">
          <label
            >Categoria<input
              name="editarPreguntaCategoria"
              [(ngModel)]="edicion.categoria" /></label
          ><label
            >Pregunta<textarea
              name="editarPreguntaTexto"
              rows="3"
              [(ngModel)]="edicion.texto"
            ></textarea></label
          ><label
            >Instruccion de evaluacion<textarea
              name="editarPreguntaInstruccion"
              rows="2"
              [(ngModel)]="edicion.instruccion"
            ></textarea>
          </label>
          <div class="inline-form">
            <label
              >Orden<input
                type="number"
                min="1"
                name="editarPreguntaOrden"
                [(ngModel)]="edicion.orden" /></label
            ><label
              >Revisiones<input
                type="number"
                min="0"
                name="editarPreguntaMaxRepreguntas"
                [(ngModel)]="edicion.maxRepreguntas" /></label
            ><label
              >Umbral (opcional)<input
                type="number"
                min="0"
                max="1"
                step="0.01"
                name="editarPreguntaUmbral"
                [(ngModel)]="edicion.umbralCierreAnticipado"
            /></label>
            <label
              >Estado<select name="editarPreguntaEstado" [(ngModel)]="edicion.estado">
                <option value="activo">Activo</option>
                <option value="inactivo">Inactivo</option>
              </select></label
            >
          </div>
          <label
            >Rubrica<select name="editarPreguntaRubricaRef" [(ngModel)]="edicion.rubricaRef">
              <option value="">Heredar de la campania</option>
              @for (rubrica of rubricas(); track rubrica.id) {
                <option [value]="rubrica.id">{{ rubrica.nombre }}</option>
              }
            </select></label
          ><label
            >Prompt de evaluacion<select
              name="editarPreguntaPromptRef"
              [(ngModel)]="edicion.promptEvaluarRef"
            >
              <option value="">Heredar de la campania</option>
              @for (prompt of prompts(); track prompt.id) {
                <option [value]="prompt.id">{{ prompt.nombre }} ({{ prompt.tipoPrompt }})</option>
              }
            </select></label
          ><button class="primary-button" type="submit" [disabled]="!esAdmin()">
            Guardar pregunta
          </button>
        </form>
      </div>
    }
  </article>`,
})
export class PreguntasPanel implements OnChanges {
  readonly preguntas = input.required<readonly Pregunta[]>();
  readonly rubricas = input.required<readonly Rubrica[]>();
  readonly prompts = input.required<readonly PromptConfig[]>();
  readonly esAdmin = input.required<boolean>();
  readonly crear = output<PreguntaForm>();
  readonly actualizar = output<PreguntaActualizada>();
  protected nueva = preguntaVacia();
  protected edicion = preguntaVacia();
  protected editandoId: string | null = null;
  ngOnChanges(): void {
    this.nueva = preguntaVacia();
    this.cancelar();
  }
  protected editar(pregunta: Pregunta): void {
    this.editandoId = pregunta.id;
    this.edicion = formularioDesdePregunta(pregunta);
  }
  protected cancelar(): void {
    this.editandoId = null;
    this.edicion = preguntaVacia();
  }
  protected guardarEdicion(): void {
    if (this.editandoId) this.actualizar.emit({ id: this.editandoId, form: this.edicion });
  }
}

@Component({
  selector: 'app-participantes-campania-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<article>
    <h4>Participantes</h4>
    <form class="form-grid" (ngSubmit)="previsualizar.emit(filtro)">
      <label
        >Area<select name="filtroArea" [(ngModel)]="filtro.area">
          <option value="">Todas</option>
          @for (area of areas(); track area) {
            <option [value]="area">{{ area }}</option>
          }
        </select></label
      ><label
        >Empresa<select name="filtroEmpresa" [(ngModel)]="filtro.empresa">
          <option value="">Todas</option>
          @for (empresa of empresas(); track empresa) {
            <option [value]="empresa">{{ empresa }}</option>
          }
        </select></label
      ><button class="ghost-button" type="submit">Preview</button>
    </form>
    @if (preview().length > 0) {
      <p class="muted">
        Preview: {{ preview().length }} elegibles, {{ seleccion.size }} seleccionados
      </p>
      <ul class="compact-list">
        @for (usuario of preview(); track usuario.usuarioId) {
          <li>
            <label class="check-inline"
              ><input
                type="checkbox"
                [checked]="seleccion.has(usuario.usuarioId)"
                (change)="alternar(usuario.usuarioId)"
              /><strong>{{ usuario.nombre }}</strong
              ><span>{{ usuario.area }} / {{ usuario.empresa }}</span></label
            >
          </li>
        }
      </ul>
      @if (esAdmin()) {
        <button
          type="button"
          class="primary-button"
          [disabled]="seleccion.size === 0"
          (click)="asociar.emit(idsSeleccionados())"
        >
          Asociar seleccionados ({{ seleccion.size }})
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
              @if (esAdmin()) {
                <td>
                  <button
                    type="button"
                    class="ghost-button"
                    (click)="reiniciarParticipante.emit(participante)"
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
    @if (esAdmin() && participantes().length > 0) {
      <p class="muted">
        Reinicio de datos de prueba: borra conversaciones, respuestas, evaluaciones y Markdown;
        conserva la campania, su configuracion y los usuarios.
      </p>
      <button type="button" class="ghost-button danger" (click)="reiniciarCampania.emit()">
        Reiniciar datos de prueba (toda la campania)
      </button>
    }
  </article>`,
})
export class ParticipantesCampaniaPanel implements OnChanges {
  readonly participantes = input.required<readonly ParticipanteCampania[]>();
  readonly preview = input.required<readonly ParticipantePreview[]>();
  readonly areas = input.required<readonly string[]>();
  readonly empresas = input.required<readonly string[]>();
  readonly nombres = input.required<ReadonlyMap<string, string>>();
  readonly esAdmin = input.required<boolean>();
  readonly previsualizar = output<ParticipantesFiltro>();
  readonly asociar = output<readonly string[]>();
  readonly reiniciarParticipante = output<ParticipanteCampania>();
  readonly reiniciarCampania = output<void>();
  protected filtro: ParticipantesFiltro = { area: '', empresa: '' };
  protected seleccion = new Set<string>();
  private previewAnterior: readonly ParticipantePreview[] | null = null;
  ngOnChanges(): void {
    if (this.previewAnterior !== this.preview()) {
      this.previewAnterior = this.preview();
      this.seleccion = new Set(this.preview().map((usuario) => usuario.usuarioId));
    }
  }
  protected alternar(usuarioId: string): void {
    const siguiente = new Set(this.seleccion);
    siguiente.has(usuarioId) ? siguiente.delete(usuarioId) : siguiente.add(usuarioId);
    this.seleccion = siguiente;
  }
  protected idsSeleccionados(): readonly string[] {
    return [...this.seleccion];
  }
  protected nombreUsuario(usuarioId: string): string {
    return this.nombres().get(usuarioId) ?? usuarioId;
  }
}

@Component({
  selector: 'app-campania-detalle-panel',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="panel">
    <div class="panel-heading">
      <h3>{{ campania().nombre }}</h3>
      <div class="actions-row">
        <button
          type="button"
          class="ghost-button"
          [routerLink]="['/campanias', campania().id, 'envios']"
        >
          Envios
        </button>
        @if (esAdmin()) {
          <button type="button" class="ghost-button" (click)="cambiarEstado.emit('activa')">
            Activar</button
          ><button type="button" class="ghost-button" (click)="cambiarEstado.emit('cerrada')">
            Cerrar
          </button>
        }
      </div>
    </div>
    <nav class="tab-nav" role="tablist" aria-label="Secciones de la campaña">
      @for (item of tabs; track item.id) {
        <button
          type="button"
          class="tab-button"
          role="tab"
          [id]="idPestana(item.id)"
          [class.active]="activa() === item.id"
          [attr.aria-selected]="activa() === item.id"
          [attr.aria-controls]="idPanel(item.id)"
          [tabindex]="activa() === item.id ? 0 : -1"
          [attr.data-tab]="item.id"
          (click)="seleccionar(item.id)"
          (keydown)="navegarPestanas($event, item.id)"
        >
          {{ item.nombre }}
        </button>
      }
    </nav>
    <div
      class="tab-panels"
      role="tabpanel"
      [id]="idPanel(activa())"
      [attr.aria-labelledby]="idPestana(activa())"
      tabindex="0"
    >
      <ng-content />
    </div>
  </section>`,
})
export class CampaniaDetallePanel {
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly campania = input.required<Campania>();
  readonly esAdmin = input.required<boolean>();
  readonly cambiarEstado = output<string>();
  readonly activa = input<TabCampania>('config');
  readonly tabCambiada = output<TabCampania>();
  protected readonly tabs = [
    { id: 'config' as const, nombre: 'Configuracion' },
    { id: 'mensajes' as const, nombre: 'Mensajes iniciales' },
    { id: 'preguntas' as const, nombre: 'Preguntas' },
    { id: 'participantes' as const, nombre: 'Participantes' },
  ];
  protected idPestana(tab: TabCampania): string {
    return `campania-${this.campania().id}-tab-${tab}`;
  }
  protected idPanel(tab: TabCampania): string {
    return `campania-${this.campania().id}-panel-${tab}`;
  }
  protected seleccionar(tab: TabCampania): void {
    this.tabCambiada.emit(tab);
  }
  protected navegarPestanas(evento: KeyboardEvent, actual: TabCampania): void {
    const indice = this.tabs.findIndex((tab) => tab.id === actual);
    let siguiente: TabCampania | null = null;
    if (evento.key === 'ArrowRight') siguiente = this.tabs[(indice + 1) % this.tabs.length].id;
    if (evento.key === 'ArrowLeft')
      siguiente = this.tabs[(indice - 1 + this.tabs.length) % this.tabs.length].id;
    if (evento.key === 'Home') siguiente = this.tabs[0].id;
    if (evento.key === 'End') siguiente = this.tabs[this.tabs.length - 1].id;
    if (!siguiente) return;
    evento.preventDefault();
    this.seleccionar(siguiente);
    const host = this.host.nativeElement as HTMLElement;
    host.querySelector<HTMLButtonElement>(`[data-tab="${siguiente}"]`)?.focus();
  }
}
