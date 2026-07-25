import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';

import { AdminApiService } from '../../core/admin-api.service';
import {
  Campania,
  ConfigLlm,
  ParticipanteCampania,
  ParticipantePreview,
  PromptConfig,
  Rubrica,
  UsuarioAdmin,
} from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { NotificacionesService } from '../../core/notificaciones.service';
import { EstadoAccesibleComponent } from '../../shared/estado-accesible.component';
import { formatApiError } from '../../shared-error';
import {
  CampaniaConfiguracionPanel,
  CampaniaCreacionPanel,
  CampaniaDetallePanel,
  CampaniaEdicionForm,
  CampaniaCrearForm,
  CampaniasFiltro,
  CampaniasListaPanel,
  formularioDesdePregunta,
  MensajeInicialForm,
  MensajesInicialesPanel,
  ParticipantesCampaniaPanel,
  ParticipantesFiltro,
  PreguntaActualizada,
  PreguntaForm,
  PreguntasPanel,
} from './campanias.panels';

@Component({
  selector: 'app-campanias-page',
  standalone: true,
  imports: [
    EstadoAccesibleComponent,
    CampaniasListaPanel,
    CampaniaCreacionPanel,
    CampaniaDetallePanel,
    CampaniaConfiguracionPanel,
    MensajesInicialesPanel,
    PreguntasPanel,
    ParticipantesCampaniaPanel,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="page-grid">
    <div class="section-header">
      <div><h2>Campanias</h2></div>
      <button type="button" class="ghost-button" (click)="load()">Actualizar</button>
    </div>
    <app-estado-accesible tipo="error" [mensaje]="error()" />
    <div class="two-column">
      <app-campanias-lista-panel
        [campanias]="campanias()"
        [seleccionadaId]="selected()?.id ?? null"
        (buscar)="aplicarFiltro($event)"
        (abrir)="select($event)"
      /><app-campania-creacion-panel
        [rubricas]="rubricas()"
        [configsLlm]="configsLlm()"
        [prompts]="prompts()"
        [esAdmin]="auth.isAdmin()"
        [reiniciar]="creacionVersion()"
        (guardar)="crearCampania($event)"
      />
    </div>
    @if (selected(); as campania) {
      <app-campania-detalle-panel
        [campania]="campania"
        [esAdmin]="auth.isAdmin()"
        [activa]="detalleTab()"
        (cambiarEstado)="cambiarEstado(campania, $event)"
        (tabCambiada)="detalleTab.set($event)"
      >
        @switch (detalleTab()) {
          @case ('config') {
            <app-campania-configuracion-panel
              [campania]="campania"
              [rubricas]="rubricas()"
              [configsLlm]="configsLlm()"
              [prompts]="prompts()"
              [esAdmin]="auth.isAdmin()"
              (guardar)="actualizarCampania(campania.id, $event)"
            />
          }
          @case ('mensajes') {
            <app-mensajes-iniciales-panel
              [mensajes]="campania.mensajesIniciales ?? []"
              [esAdmin]="auth.isAdmin()"
              (guardar)="crearMensaje(campania.id, $event)"
            />
          }
          @case ('preguntas') {
            <app-preguntas-panel
              [preguntas]="campania.preguntas ?? []"
              [rubricas]="rubricas()"
              [prompts]="prompts()"
              [esAdmin]="auth.isAdmin()"
              (crear)="crearPregunta(campania.id, $event)"
              (actualizar)="actualizarPregunta(campania.id, $event)"
            />
          }
          @case ('participantes') {
            <app-participantes-campania-panel
              [participantes]="participantes()"
              [preview]="previewUsuarios()"
              [areas]="areasDisponibles()"
              [empresas]="empresasDisponibles()"
              [nombres]="nombresUsuarios()"
              [esAdmin]="auth.isAdmin()"
              (previsualizar)="preview(campania.id, $event)"
              (asociar)="asociarPreview(campania.id, $event)"
              (reiniciarParticipante)="reiniciarParticipante(campania.id, $event)"
              (reiniciarCampania)="reiniciarDatosCampania(campania)"
            />
          }
        }
      </app-campania-detalle-panel>
    }
  </section>`,
})
export class CampaniasPage {
  private readonly api = inject(AdminApiService);
  private readonly notificaciones = inject(NotificacionesService);
  protected readonly auth = inject(AuthService);
  protected readonly campanias = signal<Campania[]>([]);
  protected readonly selected = signal<Campania | null>(null);
  protected readonly participantes = signal<ParticipanteCampania[]>([]);
  protected readonly previewUsuarios = signal<ParticipantePreview[]>([]);
  protected readonly rubricas = signal<Rubrica[]>([]);
  protected readonly configsLlm = signal<ConfigLlm[]>([]);
  protected readonly prompts = signal<PromptConfig[]>([]);
  protected readonly areasDisponibles = signal<string[]>([]);
  protected readonly empresasDisponibles = signal<string[]>([]);
  protected readonly nombresUsuarios = signal<ReadonlyMap<string, string>>(new Map());
  protected readonly error = signal('');
  protected readonly creacionVersion = signal(0);
  protected readonly detalleTab = signal<'config' | 'mensajes' | 'preguntas' | 'participantes'>(
    'config',
  );
  private filtro: CampaniasFiltro = { estado: '', busqueda: '' };

  constructor() {
    this.load();
    this.loadCatalogos();
  }
  protected aplicarFiltro(filtro: CampaniasFiltro): void {
    this.filtro = filtro;
    this.load();
  }
  protected load(): void {
    this.api
      .campanias({ estado: this.filtro.estado, q: this.filtro.busqueda, pageSize: 50 })
      .subscribe({
        next: (page) => {
          this.campanias.set(page.items);
          this.error.set('');
        },
        error: (err: unknown) => this.error.set(formatApiError(err)),
      });
  }
  protected select(id: string): void {
    this.api.campania(id).subscribe({
      next: (campania) => {
        this.selected.set(campania);
        this.detalleTab.set('config');
        this.loadParticipantes(campania.id);
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }
  protected crearCampania(formulario: CampaniaCrearForm): void {
    const { promptEvaluarRef, ...datos } = formulario;
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
          this.creacionVersion.update((version) => version + 1);
          this.load();
          this.select(campania.id);
          this.notificaciones.exito('Campania creada.');
        },
        error: (err: unknown) => this.reportarError(err),
      });
  }
  protected actualizarCampania(id: string, formulario: CampaniaEdicionForm): void {
    const actual = this.selected();
    const promptRefs = formulario.promptEvaluarRef ? { evaluar: formulario.promptEvaluarRef } : {};
    this.api
      .actualizarCampania(id, {
        nombre: formulario.nombre,
        descripcion: formulario.descripcion,
        objetivo: formulario.objetivo,
        rubricaRef: formulario.rubricaRef,
        promptRefs,
        configLLMRef: formulario.configLlmRef,
        configConversacional: {
          maxRepreguntas: actual?.configConversacional?.maxRepreguntas ?? 1,
          mensajeCierre:
            actual?.configConversacional?.mensajeCierre ??
            'Gracias. Tu aporte quedo registrado correctamente.',
          segmentacionIdeas: Boolean(formulario.segmentacionIdeas),
          tejidoColectivo: Boolean(actual?.configConversacional?.tejidoColectivo),
          parafraseo: Boolean(formulario.parafraseo),
          umbralCierreAnticipado:
            formulario.umbralCierreAnticipado === null
              ? null
              : Math.min(1, Math.max(0, Number(formulario.umbralCierreAnticipado) || 0)),
          minutosInactividadSesion:
            formulario.minutosInactividadSesion === null
              ? null
              : Math.max(0, Math.trunc(Number(formulario.minutosInactividadSesion) || 0)),
          numeroWhatsAppSaliente: formulario.numeroWhatsAppSaliente.trim() || null,
        },
        configSeguridad: {
          maxCaracteresMensaje: actual?.configSeguridad?.maxCaracteresMensaje ?? 1500,
          maxMensajesPorUsuario: actual?.configSeguridad?.maxMensajesPorUsuario ?? 10,
          maxLlamadasLlmPorUsuario: actual?.configSeguridad?.maxLlamadasLlmPorUsuario ?? 2,
          presupuestoTokensCampania: Math.max(0, Number(formulario.presupuestoTokensCampania) || 0),
        },
      })
      .subscribe({
        next: (campania) => {
          this.selected.set(campania);
          this.load();
          this.notificaciones.exito('Campania actualizada.');
        },
        error: (err: unknown) => this.reportarError(err),
      });
  }
  protected cambiarEstado(campania: Campania, estado: string): void {
    this.api.cambiarEstadoCampania(campania.id, estado).subscribe({
      next: (actualizada) => {
        this.selected.set(actualizada);
        this.load();
        this.notificaciones.exito(`Campania ${estado}.`);
      },
      error: (err: unknown) => this.reportarError(err),
    });
  }
  protected crearMensaje(campaniaId: string, formulario: MensajeInicialForm): void {
    const plantillaNombre = formulario.plantillaNombre.trim();
    const componentes = formulario.plantillaComponentes
      .split(',')
      .map((componente) => componente.trim())
      .filter((componente) => componente.length > 0);
    const plantillaWhatsApp = plantillaNombre
      ? { nombre: plantillaNombre, idioma: formulario.plantillaIdioma.trim() || 'es', componentes }
      : undefined;
    this.api
      .crearMensajeInicial(campaniaId, {
        nombreInterno: formulario.nombreInterno,
        texto: formulario.texto,
        orden: 1,
        variablesDinamicas: ['nombre'],
        estado: 'activo',
        ...(plantillaWhatsApp ? { plantillaWhatsApp } : {}),
      })
      .subscribe({
        next: () => {
          this.select(campaniaId);
          this.notificaciones.exito('Mensaje inicial agregado.');
        },
        error: (err: unknown) => this.reportarError(err),
      });
  }
  protected crearPregunta(campaniaId: string, formulario: PreguntaForm): void {
    this.api.crearPregunta(campaniaId, this.preguntaPayload(formulario)).subscribe({
      next: () => {
        this.select(campaniaId);
        this.notificaciones.exito('Pregunta agregada.');
      },
      error: (err: unknown) => this.reportarError(err),
    });
  }
  protected actualizarPregunta(campaniaId: string, actualizacion: PreguntaActualizada): void {
    this.api
      .actualizarPregunta(campaniaId, actualizacion.id, this.preguntaPayload(actualizacion.form))
      .subscribe({
        next: () => {
          this.select(campaniaId);
          this.notificaciones.exito('Pregunta actualizada.');
        },
        error: (err: unknown) => this.reportarError(err),
      });
  }
  protected preview(campaniaId: string, filtro: ParticipantesFiltro): void {
    this.api.previewParticipantes(campaniaId, { ...filtro }).subscribe({
      next: (response) => this.previewUsuarios.set(response.items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }
  protected asociarPreview(campaniaId: string, usuarioIds: readonly string[]): void {
    this.api.asociarParticipantes(campaniaId, [...usuarioIds]).subscribe({
      next: () => {
        this.loadParticipantes(campaniaId);
        this.previewUsuarios.set([]);
        this.notificaciones.exito('Participantes asociados a la campania.');
      },
      error: (err: unknown) => this.reportarError(err),
    });
  }
  protected reiniciarParticipante(campaniaId: string, participante: ParticipanteCampania): void {
    const nombre = this.nombresUsuarios().get(participante.usuarioId) ?? participante.usuarioId;
    if (!window.confirm(`Reiniciar la conversacion de ${nombre}? Se borraran sus datos del flujo.`))
      return;
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
  protected reiniciarDatosCampania(campania: Campania): void {
    const escrito = window.prompt(
      `Esto borrara los datos de prueba de TODOS los participantes de "${campania.nombre}". Escribe el nombre de la campania para confirmar:`,
    );
    if (escrito === null) return;
    if (escrito.trim() !== campania.nombre) {
      this.notificaciones.error('El nombre no coincide; no se reinicio nada.');
      return;
    }
    this.api.reiniciarDatosCampania(campania.id, { reiniciarEnvios: false }).subscribe({
      next: (reporte) => {
        this.loadParticipantes(campania.id);
        this.notificaciones.exito(
          `Campania reiniciada: ${reporte.respuestas} respuestas, ${reporte.conversaciones} conversaciones, ${reporte.participantesReseteados} participantes reseteados.`,
        );
      },
      error: (err: unknown) => this.reportarError(err),
    });
  }
  private loadCatalogos(): void {
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
        this.nombresUsuarios.set(
          new Map(
            page.items.map((usuario: UsuarioAdmin) => [
              usuario.id,
              usuario.area ? `${usuario.nombre} (${usuario.area})` : usuario.nombre,
            ]),
          ),
        );
      },
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }
  private loadParticipantes(campaniaId: string): void {
    this.api.participantes(campaniaId).subscribe({
      next: (items) => this.participantes.set(items),
      error: (err: unknown) => this.error.set(formatApiError(err)),
    });
  }
  private distinct(valores: string[]): string[] {
    return Array.from(
      new Set(valores.filter((valor) => !!valor && valor.trim().length > 0)),
    ).sort();
  }
  private preguntaPayload(formulario: PreguntaForm) {
    return {
      categoria: formulario.categoria,
      texto: formulario.texto,
      instruccion: formulario.instruccion || formulario.texto,
      orden: Number(formulario.orden) || 1,
      estado: formulario.estado,
      rubricaRef: formulario.rubricaRef,
      promptRefs: formulario.promptEvaluarRef ? { evaluar: formulario.promptEvaluarRef } : {},
      maxRepreguntas: Math.max(0, Number(formulario.maxRepreguntas) || 0),
      limitesSeguridad: {
        maxCaracteresMensaje: Math.max(1, Number(formulario.maxCaracteresMensaje) || 1500),
        maxLlamadasLlm: Math.max(1, Number(formulario.maxLlamadasLlm) || 2),
      },
      configMarkdown: { tipoArtefacto: 'respuesta' },
      umbralCierreAnticipado:
        formulario.umbralCierreAnticipado === null ||
        formulario.umbralCierreAnticipado === undefined
          ? null
          : Math.min(1, Math.max(0, Number(formulario.umbralCierreAnticipado) || 0)),
    };
  }
  private reportarError(err: unknown): void {
    const mensaje = formatApiError(err);
    this.error.set(mensaje);
    this.notificaciones.error(mensaje);
  }
}
