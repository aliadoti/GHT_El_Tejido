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
  LocalizacionCampania,
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
  coachingSecuencialIdeas: boolean;
  minutosCoachingPorIdea: number | null;
  parafraseo: boolean;
  umbralCierreAnticipado: number | null;
  minutosInactividadSesion: number | null;
  numeroWhatsAppSaliente: string;
  participacionContinua: boolean;
  clasificacionIntencionControl: boolean;
  consultaIdea: boolean;
  mostrarIdeaAlCerrar: boolean;
}
export type TabCampania = 'config' | 'mensajes' | 'preguntas' | 'localizaciones' | 'participantes';
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
    coachingSecuencialIdeas: campania.configConversacional?.coachingSecuencialIdeas ?? false,
    minutosCoachingPorIdea: campania.configConversacional?.minutosCoachingPorIdea ?? null,
    parafraseo: campania.configConversacional?.parafraseo ?? false,
    umbralCierreAnticipado: campania.configConversacional?.umbralCierreAnticipado ?? null,
    minutosInactividadSesion: campania.configConversacional?.minutosInactividadSesion ?? null,
    numeroWhatsAppSaliente: campania.configConversacional?.numeroWhatsAppSaliente ?? '',
    participacionContinua: campania.configConversacional?.participacionContinua ?? false,
    clasificacionIntencionControl:
      campania.configConversacional?.clasificacionIntencionControl ?? false,
    consultaIdea: campania.configConversacional?.consultaIdea ?? true,
    mostrarIdeaAlCerrar: campania.configConversacional?.mostrarIdeaAlCerrar ?? true,
  };
}

@Component({
  selector: 'app-campanias-lista-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="panel">
    <div class="panel-heading">
      <h3>Lista de campanias</h3>
      @if (esAdmin()) {
        <button type="button" class="primary-button" (click)="nueva.emit()">
          + Nueva campania
        </button>
      }
    </div>
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
              <td colspan="4" class="empty-cell">
                No hay campanias registradas. Crea la primera para empezar.
              </td>
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
  readonly esAdmin = input.required<boolean>();
  readonly buscar = output<CampaniasFiltro>();
  readonly abrir = output<string>();
  readonly nueva = output<void>();
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
      <fieldset class="form-fieldset">
        <legend>Evaluacion</legend>
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
        >
      </fieldset>
      <div class="actions-row">
        <button class="primary-button" type="submit" [disabled]="!esAdmin()">
          Guardar campania
        </button>
      </div>
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
      <fieldset class="form-fieldset">
        <legend>Evaluacion</legend>
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
          >Config LLM<select
            name="editarConfigLlmRef"
            [(ngModel)]="formulario.configLlmRef"
            required
          >
            @for (config of configsLlm(); track config.id) {
              <option [value]="config.id">{{ config.nombre }}</option>
            }
          </select></label
        >
      </fieldset>
      <fieldset class="form-fieldset">
        <legend>Seguridad y costo</legend>
        <label
          >Presupuesto de tokens LLM<input
            aria-describedby="ayuda-presupuesto"
            type="number"
            min="0"
            name="editarPresupuestoTokens"
            [(ngModel)]="formulario.presupuestoTokensCampania"
          /><small id="ayuda-presupuesto" class="muted">0 = sin limite.</small></label
        >
      </fieldset>
      <fieldset class="form-fieldset">
        <legend>Conversacion</legend>
        <label class="checkbox-label"
          ><input
            type="checkbox"
            name="editarSegmentacionIdeas"
            [(ngModel)]="formulario.segmentacionIdeas"
          />Separar varias ideas de un mismo mensaje</label
        ><label class="checkbox-label"
          ><input
            aria-describedby="ayuda-coaching-ideas"
            type="checkbox"
            name="editarCoachingSecuencialIdeas"
            [(ngModel)]="formulario.coachingSecuencialIdeas"
          />Afinar ideas una por una</label
        ><small id="ayuda-coaching-ideas" class="muted"
          >Requiere separar varias ideas. El coach trabaja una idea y luego la siguiente; nace
          apagado.</small
        ><label
          >Minutos por idea<input
            aria-describedby="ayuda-minutos-coaching"
            type="number"
            min="0"
            step="1"
            name="editarMinutosCoachingPorIdea"
            [(ngModel)]="formulario.minutosCoachingPorIdea"
          /><small id="ayuda-minutos-coaching" class="muted"
            >Vacio hereda el valor global; 0 apaga el tiempo por idea.</small
          ></label
        ><label class="checkbox-label"
          ><input
            type="checkbox"
            name="editarParafraseo"
            [(ngModel)]="formulario.parafraseo"
          />Devolver parafrasis ("esto es lo que entendi") en respuestas maduras</label
        ><label
          >Umbral de madurez / cierre<input
            aria-describedby="ayuda-umbral"
            type="number"
            min="0"
            max="1"
            step="0.01"
            name="editarUmbralCierreAnticipado"
            [(ngModel)]="formulario.umbralCierreAnticipado"
          /><small id="ayuda-umbral" class="muted"
            >0 desactiva el cierre. Entre 0 y 1 indica la fraccion de la rubrica para cerrar antes,
            por ejemplo 0.6.</small
          ></label
        ><label
          >Cierre por inactividad<input
            aria-describedby="ayuda-inactividad"
            type="number"
            min="0"
            step="1"
            name="editarMinutosInactividadSesion"
            [(ngModel)]="formulario.minutosInactividadSesion"
          /><small id="ayuda-inactividad" class="muted"
            >Minutos sin respuesta antes de cerrar el hilo. Vacio usa el valor global.</small
          ></label
        ><label
          >Alias del numero de envio (opcional)<input
            name="editarNumeroWhatsAppSaliente"
            placeholder="usar predeterminado"
            [(ngModel)]="formulario.numeroWhatsAppSaliente"
          /><small class="muted"
            >Dejalo vacio para usar el numero predeterminado o escribe un alias configurado.</small
          ></label
        >
      </fieldset>
      <fieldset class="form-fieldset">
        <legend>Participacion continua</legend>
        <label class="checkbox-label"
          ><input
            aria-describedby="ayuda-participacion-continua"
            type="checkbox"
            name="editarParticipacionContinua"
            [(ngModel)]="formulario.participacionContinua"
          />Permitir nuevas ideas despues de finalizar</label
        ><small id="ayuda-participacion-continua" class="muted"
          >Mientras la campania este activa, cada participante podra volver y comenzar ideas nuevas.
          Sus ideas anteriores no se mezclaran. Es distinto del estado de la campania: una campania
          cerrada no recibe aportes aunque esto este encendido.</small
        >
        @if (avisoApagado()) {
          <p class="muted" role="status">
            Las ideas que ya estan en conversacion podran terminar; no se abriran ideas nuevas.
          </p>
        }
      </fieldset>
      <fieldset class="form-fieldset">
        <legend>Intenciones de control</legend>
        <label class="checkbox-label"
          ><input
            aria-describedby="ayuda-clasificacion-intencion"
            type="checkbox"
            name="editarClasificacionIntencionControl"
            [(ngModel)]="formulario.clasificacionIntencionControl"
          />Interpretar expresiones flexibles para salir del coaching</label
        ><small id="ayuda-clasificacion-intencion" class="muted"
          >Solo se usa durante una mejora o confirmación y requiere que el interruptor global esté
          habilitado. Las frases inequívocas siguen funcionando aunque esta opción esté
          apagada.</small
        >
        @if (avisoClasificacionApagada()) {
          <p class="muted" role="status">
            Las aclaraciones de salida pendientes volverán a la conversación normal; no se perderá
            la idea activa.
          </p>
        }
      </fieldset>
      <fieldset class="form-fieldset">
        <legend>Visibilidad de la idea</legend>
        <label class="checkbox-label"
          ><input type="checkbox" name="editarConsultaIdea" [(ngModel)]="formulario.consultaIdea" />
          Permitir que la persona consulte su última idea</label
        ><small class="muted"
          >Solo funciona cuando el interruptor global está habilitado y nunca muestra ideas de otra persona.</small
        >
        <label class="checkbox-label"
          ><input type="checkbox" name="editarMostrarIdeaAlCerrar" [(ngModel)]="formulario.mostrarIdeaAlCerrar" />
          Mostrar la versión final al cerrar</label
        ><small class="muted">No se muestra una idea rechazada.</small>
      </fieldset>
      <div class="actions-row">
        <button class="primary-button" type="submit" [disabled]="!esAdmin()">
          Guardar cambios
        </button>
      </div>
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

  /**
   * P-26 §8.2: al apagar la participacion continua se avisa que las ideas en curso podran terminar
   * pero no se abriran nuevas. Solo aplica mientras el cambio esta sin guardar sobre una campania
   * que hoy la tiene encendida.
   */
  protected avisoApagado(): boolean {
    return (
      (this.campania().configConversacional?.participacionContinua ?? false) &&
      !this.formulario.participacionContinua
    );
  }

  protected avisoClasificacionApagada(): boolean {
    return (
      (this.campania().configConversacional?.clasificacionIntencionControl ?? false) &&
      !this.formulario.clasificacionIntencionControl
    );
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
      <label>Nombre interno<input name="miNombre" [(ngModel)]="formulario.nombreInterno" /></label>
      <label
        >Texto del mensaje<textarea
          name="miTexto"
          rows="3"
          [(ngModel)]="formulario.texto"
        ></textarea>
      </label>
      <p class="subhead">Plantilla WhatsApp (requerida para el envio inicial proactivo)</p>
      <label
        >Plantilla aprobada<input
          name="miPlantillaNombre"
          [(ngModel)]="formulario.plantillaNombre"
          placeholder="ej: el_tejido_saludo"
      /></label>
      <label
        >Idioma<input
          name="miPlantillaIdioma"
          [(ngModel)]="formulario.plantillaIdioma"
          placeholder="ej: es"
      /></label>
      <label
        >Variables en orden<input
          name="miPlantillaComponentes"
          [(ngModel)]="formulario.plantillaComponentes"
          placeholder="ej: nombre, campania"
      /></label>
      <div class="actions-row">
        <button class="primary-button" type="submit" [disabled]="!esAdmin()">
          Agregar mensaje
        </button>
      </div>
    </form>
    <ul class="compact-list">
      @for (item of mensajes(); track item.id) {
        <li>
          <strong>{{ item.nombreInterno }}</strong
          ><span>{{ item.texto }}</span>
        </li>
      } @empty {
        <li class="muted">
          Esta campania aun no tiene mensajes iniciales. Agrega el primero para saludar a las
          personas.
        </li>
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
  selector: 'app-localizaciones-campania-panel',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<article>
    <h4>Textos por idioma</h4>
    <p class="muted">
      Los IDs técnicos se comparten. Edita el contenido visible por idioma; el alias de plantilla se
      mapea por ambiente y no lleva secretos.
    </p>
    <form class="form-grid" (ngSubmit)="guardarCambios()">
      <label
        >Idioma<select
          name="idiomaLocalizacion"
          [(ngModel)]="idioma"
          (ngModelChange)="cargarIdioma()"
        >
          <option value="es">Español</option>
          <option value="en">English</option>
        </select></label
      >
      <label class="checkbox-field"
        ><input type="checkbox" name="inglesHabilitado" [(ngModel)]="inglesHabilitado" /> Habilitar
        inglés para esta campaña</label
      >
      <label>Nombre visible<input name="localizacionNombre" [(ngModel)]="actual.nombre" /></label>
      <label
        >Descripción<textarea
          name="localizacionDescripcion"
          rows="2"
          [(ngModel)]="actual.descripcion"
        ></textarea>
      </label>
      <label
        >Objetivo<textarea
          name="localizacionObjetivo"
          rows="2"
          [(ngModel)]="actual.objetivo"
        ></textarea>
      </label>
      <label
        >Mensaje de cierre<textarea
          name="localizacionCierre"
          rows="2"
          [(ngModel)]="actual.mensajeCierre"
        ></textarea>
      </label>
      <h5 class="subhead">Mensajes iniciales</h5>
      @for (mensaje of campania().mensajesIniciales ?? []; track mensaje.id) {
        <div class="edit-block">
          <strong>{{ mensaje.nombreInterno }}</strong>
          <label
            >Texto<textarea
              [name]="'texto-' + mensaje.id"
              rows="2"
              [ngModel]="textoMensaje(mensaje.id)"
              (ngModelChange)="cambiarTextoMensaje(mensaje.id, $event)"
            ></textarea>
          </label>
          <label
            >Alias de plantilla Meta<input
              [name]="'plantilla-' + mensaje.id"
              [ngModel]="plantillaRef(mensaje.id)"
              (ngModelChange)="cambiarPlantillaRef(mensaje.id, $event)"
              placeholder="ej: inicio_campania"
          /></label>
        </div>
      }
      <h5 class="subhead">Preguntas</h5>
      @for (pregunta of campania().preguntas ?? []; track pregunta.id) {
        <div class="edit-block">
          <strong>{{ pregunta.categoria }}</strong>
          <label
            >Texto<textarea
              [name]="'pregunta-' + pregunta.id"
              rows="2"
              [ngModel]="textoPregunta(pregunta.id)"
              (ngModelChange)="cambiarTextoPregunta(pregunta.id, $event)"
            ></textarea>
          </label>
          <label
            >Instrucción<textarea
              [name]="'instruccion-' + pregunta.id"
              rows="2"
              [ngModel]="instruccionPregunta(pregunta.id)"
              (ngModelChange)="cambiarInstruccionPregunta(pregunta.id, $event)"
            ></textarea>
          </label>
        </div>
      }
      <button class="primary-button" type="submit" [disabled]="!esAdmin()">
        Guardar textos por idioma
      </button>
    </form>
  </article>`,
})
export class LocalizacionesCampaniaPanel implements OnChanges {
  readonly campania = input.required<Campania>();
  readonly esAdmin = input.required<boolean>();
  readonly guardar = output<{
    idiomasHabilitados: string[];
    localizaciones: Record<string, LocalizacionCampania>;
  }>();
  protected idioma = 'es';
  protected inglesHabilitado = false;
  protected actual: LocalizacionCampania = {};
  private localizaciones: Record<string, LocalizacionCampania> = {};

  ngOnChanges(): void {
    this.inglesHabilitado = (this.campania().idiomasHabilitados ?? ['es']).includes('en');
    this.localizaciones = structuredClone(this.campania().localizaciones ?? {});
    this.cargarIdioma();
  }

  protected cargarIdioma(): void {
    const existente = this.localizaciones[this.idioma];
    if (existente) {
      this.actual = existente;
      return;
    }
    this.actual = this.desdeLegado(this.idioma);
    this.localizaciones[this.idioma] = this.actual;
  }

  protected textoMensaje(id: string): string {
    return this.actual.mensajesIniciales?.[id]?.texto ?? '';
  }
  protected plantillaRef(id: string): string {
    return this.actual.mensajesIniciales?.[id]?.plantillaRef ?? '';
  }
  protected textoPregunta(id: string): string {
    return this.actual.preguntas?.[id]?.texto ?? '';
  }
  protected instruccionPregunta(id: string): string {
    return this.actual.preguntas?.[id]?.instruccion ?? '';
  }
  protected cambiarTextoMensaje(id: string, texto: string): void {
    this.mensaje(id).texto = texto;
  }
  protected cambiarPlantillaRef(id: string, plantillaRef: string): void {
    this.mensaje(id).plantillaRef = plantillaRef;
  }
  protected cambiarTextoPregunta(id: string, texto: string): void {
    this.pregunta(id).texto = texto;
  }
  protected cambiarInstruccionPregunta(id: string, instruccion: string): void {
    this.pregunta(id).instruccion = instruccion;
  }

  protected guardarCambios(): void {
    this.localizaciones[this.idioma] = this.actual;
    this.guardar.emit({
      idiomasHabilitados: this.inglesHabilitado ? ['es', 'en'] : ['es'],
      localizaciones: this.localizaciones,
    });
  }

  private mensaje(id: string) {
    this.actual.mensajesIniciales ??= {};
    return (this.actual.mensajesIniciales[id] ??= {});
  }
  private pregunta(id: string) {
    this.actual.preguntas ??= {};
    return (this.actual.preguntas[id] ??= {});
  }
  private desdeLegado(idioma: string): LocalizacionCampania {
    const campania = this.campania();
    return {
      nombre: idioma === 'es' ? campania.nombre : '',
      descripcion: idioma === 'es' ? campania.descripcion : '',
      objetivo: idioma === 'es' ? campania.objetivo : '',
      mensajeCierre: idioma === 'es' ? (campania.configConversacional?.mensajeCierre ?? '') : '',
      mensajesIniciales: Object.fromEntries(
        (campania.mensajesIniciales ?? []).map((mensaje) => [
          mensaje.id,
          { texto: idioma === 'es' ? mensaje.texto : '', plantillaRef: '' },
        ]),
      ),
      preguntas: Object.fromEntries(
        (campania.preguntas ?? []).map((pregunta) => [
          pregunta.id,
          {
            texto: idioma === 'es' ? pregunta.texto : '',
            instruccion: idioma === 'es' ? pregunta.instruccion : '',
          },
        ]),
      ),
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
        <li class="muted">
          Esta campania aun no tiene preguntas. Agrega la primera para poder evaluar respuestas.
        </li>
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
              <td class="empty-cell">
                Esta campania aun no tiene participantes asociados. Usa la vista previa para agregar
                los primeros.
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    @if (esAdmin() && participantes().length > 0) {
      <p class="muted">
        Reinicio de datos de prueba: borra conversaciones, respuestas, evaluaciones y Markdown;
        conserva la campania, su configuracion y los usuarios, y deja los envios pendientes para
        volver a enviar la campania.
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
          Ver envios
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
          [attr.aria-label]="nombreAccesible(item.id)"
          [tabindex]="activa() === item.id ? 0 : -1"
          [attr.data-tab]="item.id"
          (click)="seleccionar(item.id)"
          (keydown)="navegarPestanas($event, item.id)"
        >
          {{ item.numero }} · {{ item.nombre }}
          @if (estadoPaso(item.id); as estado) {
            <span aria-hidden="true">{{ estado.simbolo }}</span>
          }
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
      <p class="muted">{{ siguientePaso() }}</p>
    </div>
  </section>`,
})
export class CampaniaDetallePanel {
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly campania = input.required<Campania>();
  readonly participantes = input.required<readonly ParticipanteCampania[]>();
  readonly esAdmin = input.required<boolean>();
  readonly cambiarEstado = output<string>();
  readonly activa = input<TabCampania>('config');
  readonly tabCambiada = output<TabCampania>();
  protected readonly tabs = [
    { id: 'config' as const, numero: 1, nombre: 'Configuracion' },
    { id: 'mensajes' as const, numero: 2, nombre: 'Mensajes iniciales' },
    { id: 'preguntas' as const, numero: 3, nombre: 'Preguntas' },
    { id: 'localizaciones' as const, numero: 4, nombre: 'Textos por idioma' },
    { id: 'participantes' as const, numero: 5, nombre: 'Participantes' },
  ];
  protected estadoPaso(tab: TabCampania): { simbolo: string; texto: string } | null {
    if (tab === 'config') return null;
    const completo =
      tab === 'mensajes'
        ? (this.campania().mensajesIniciales?.some((mensaje) => mensaje.estado === 'activo') ??
          false)
        : tab === 'preguntas'
          ? (this.campania().preguntas?.some((pregunta) => pregunta.estado === 'activo') ?? false)
          : this.participantes().length > 0;
    return completo ? { simbolo: '✓', texto: 'completo' } : { simbolo: '⚠', texto: 'pendiente' };
  }
  protected nombreAccesible(tab: TabCampania): string {
    const item = this.tabs.find((opcion) => opcion.id === tab)!;
    const estado = this.estadoPaso(tab);
    return `Paso ${item.numero}, ${item.nombre}${estado ? `, ${estado.texto}` : ''}`;
  }
  protected siguientePaso(): string {
    if (this.activa() === 'config')
      return 'Cuando termines la configuracion, sigue con el mensaje inicial en el paso 2.';
    if (this.activa() === 'mensajes')
      return 'Cuando tengas el mensaje inicial, sigue con las preguntas en el paso 3.';
    if (this.activa() === 'preguntas')
      return 'Revisa los textos por idioma antes de agregar participantes.';
    if (this.activa() === 'localizaciones')
      return 'Con los textos completos, agrega participantes en el paso 5.';
    return 'Cuando tengas participantes, ya puedes activar la campania y ver sus envios.';
  }
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
