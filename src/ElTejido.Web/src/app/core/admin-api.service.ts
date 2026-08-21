import { Injectable, inject } from '@angular/core';
import { Observable, of, switchMap } from 'rxjs';

import { ApiClient } from './api-client.service';
import {
  ArtefactoMarkdown,
  Campania,
  ConfigLlm,
  Conversacion,
  DetalleIdea,
  EnvioEstado,
  Evaluacion,
  IdeaConsolidada,
  JobEnvio,
  ModoCargaMasiva,
  PagedResult,
  ParticipanteCampania,
  ParticipantePreview,
  Pregunta,
  PrevalidacionRubrica,
  PromptConfig,
  ReporteCargaMasiva,
  ReportePurgaCampanias,
  ReporteReinicioDatos,
  ResolucionConflictoTitular,
  Respuesta,
  ResultadoReasignacionNumero,
  ResumenCampania,
  Rubrica,
  TagAdmin,
  UsuarioAdmin,
} from './api-models';

export interface ContenidoCatalogoTextos {
  mensajes: Record<string, string>;
  frases: Record<string, string[]>;
}

export interface CatalogoTextos extends ContenidoCatalogoTextos {
  familiaId: string;
  idioma: 'es' | 'en';
  version: number;
  estado: 'borrador' | 'activo' | 'inactivo';
  etag: string;
  huella: string;
}

export interface CatalogoTextosEfectivo {
  origen: string;
  catalogo: CatalogoTextos | null;
}

/** DT-P32-02 §3.3: revisión previa a escribir; nunca devuelve los textos revisados. */
export interface PrevalidacionCatalogoTextos {
  valido: boolean;
  familiaId: string;
  idioma: string;
  conteos: { mensajes: number; gruposFrases: number; frases: number };
  errores: { field: string | null; issue: string }[];
}

export interface CampaniaBloqueadaCatalogo {
  campaniaId: string;
  nombre: string;
  estado: string;
  motivo: string;
}

export interface ReadinessIdiomaCatalogo {
  idioma: 'es' | 'en';
  listo: boolean;
  tieneActivo: boolean;
  versionActiva: number | null;
  huellaActiva: string | null;
  activaValida: boolean;
  problemasActiva: { field: string | null; issue: string }[];
  tieneBorrador: boolean;
  totalVersiones: number;
  semillaBaseDisponible: boolean;
  legacyValido: boolean;
  conteosLegacy: { mensajes: number; gruposFrases: number; frases: number };
  problemasLegacy: { field: string | null; issue: string }[];
  campaniasBloqueadas: CampaniaBloqueadaCatalogo[];
}

export interface CampaniaRequierePlantillaMeta {
  campaniaId: string;
  nombre: string;
  estado: string;
  mensajeInicialId: string;
}

/**
 * DT-P32-03 §3.2: revisión estructural del par `plantillaRef + idioma`. No certifica la aprobación
 * en Meta ni la correspondencia de variables: esa comprobación sigue siendo manual.
 */
export interface MapeoPlantillaMeta {
  plantillaRef: string | null;
  idioma: 'es' | 'en';
  configurado: boolean;
  nombreConfigurado: boolean;
  idiomaMetaConfigurado: boolean;
  /** DT-P32-03-01: solo los pares que exige alguna campaña activa condicionan el gate. */
  bloqueaGateOn: boolean;
  componentes: string[];
  problemas: string[];
  campanias: CampaniaRequierePlantillaMeta[];
}

/** DT-P32-02 §4.1: estado real de preparación, incluido el gate del proceso. */
export interface ReadinessCatalogosTextos {
  gateHabilitado: boolean;
  limites: { maxFrasesPorGrupo: number; maxBytesImportacionJson: number };
  /** Disponibilidad editorial de los catálogos; DT-P32-03 conserva su significado. */
  listo: boolean;
  /** DT-P32-03: catálogos válidos **y** mapeos Meta configurados. */
  listoParaGateOn: boolean;
  idiomas: ReadinessIdiomaCatalogo[];
  mapeosMeta: MapeoPlantillaMeta[];
}

/** El servidor recorta cualquier `pageSize` mayor a este tope (04 §5.8). */
const TAMANO_PAGINA_MAXIMO = 100;

/**
 * P-34 (04 §5.8): filtros y orden del listado de ideas. Todos son opcionales y el servidor los aplica
 * antes de paginar, así que el `total` que devuelve corresponde siempre al filtro pedido.
 */
export interface FiltrosIdeas {
  q?: string;
  estadoResultado?: string;
  estadoFlujo?: string;
  estadoCuraduria?: string;
  usuarioId?: string;
  preguntaId?: string;
  area?: string;
  empresa?: string;
  sede?: string;
  desde?: string;
  hasta?: string;
  calificacionMin?: string;
  calificacionMax?: string;
  confirmada?: string;
  orden?: string;
  dir?: string;
}

/** Un filtro vacío no viaja: el servidor distingue «sin filtro» de «filtro con valor vacío». */
function limpiarFiltros(filtros: FiltrosIdeas): Record<string, string> {
  return Object.fromEntries(
    Object.entries(filtros).filter(([, valor]) => (valor ?? '').toString().trim() !== ''),
  ) as Record<string, string>;
}

/** Red de seguridad: 100 páginas son 10.000 filas, muy por encima de la escala prevista (P-34 §6). */
const MAXIMO_PAGINAS = 100;

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly api = inject(ApiClient);

  usuarios(query?: Record<string, string | number | undefined>) {
    return this.api.get<PagedResult<UsuarioAdmin>>('/api/admin/usuarios', query);
  }

  /**
   * P-34 H-02: el maestro completo. Pedir `pageSize: 500` no servía —el servidor recorta a 100 sin
   * avisar—, así que se piden páginas sucesivas hasta agotar el `total` que la misma respuesta trae.
   */
  usuariosTodos(query?: Record<string, string | number | undefined>) {
    return this.paginarTodo<UsuarioAdmin>((page) =>
      this.api.get<PagedResult<UsuarioAdmin>>('/api/admin/usuarios', {
        ...query,
        page,
        pageSize: TAMANO_PAGINA_MAXIMO,
      }),
    );
  }

  crearUsuario(body: Partial<UsuarioAdmin> & { numero?: string }) {
    return this.api.post<UsuarioAdmin>('/api/admin/usuarios', body);
  }

  actualizarUsuario(id: string, body: Partial<UsuarioAdmin> & { numero?: string }) {
    return this.api.put<UsuarioAdmin>(`/api/admin/usuarios/${id}`, body);
  }

  cambiarEstadoUsuario(id: string, estado: string) {
    return this.api.patch<UsuarioAdmin>(`/api/admin/usuarios/${id}/estado`, { estado });
  }

  nombresSaludoPendientes() {
    return this.api.get<{ pendientes: number }>('/api/admin/usuarios/nombres-saludo/pendientes');
  }

  completarNombresSaludo() {
    return this.api.post<{ completados: number }>(
      '/api/admin/usuarios/nombres-saludo/completar',
      {},
    );
  }

  // I-08 v2 (04 §5.1): historico de titulares de un numero (activo + inactivos).
  usuariosPorNumero(numero: string) {
    return this.api.get<UsuarioAdmin[]>(
      `/api/admin/usuarios/por-numero/${encodeURIComponent(numero)}`,
    );
  }

  // I-08 v2 §4.4: reasignacion manual; inactiva al titular y crea uno nuevo con el mismo numero.
  reasignarNumero(
    id: string,
    body: {
      nombre: string;
      email?: string | null;
      empresaId?: string | null;
      sede?: string | null;
      cargo?: string | null;
    },
  ) {
    return this.api.post<ResultadoReasignacionNumero>(
      `/api/admin/usuarios/${id}/reasignar-numero`,
      body,
    );
  }

  /**
   * I-08 v2 (04 §5.1): sube el roster (.xlsx o .csv) y devuelve el reporte por fila.
   * `resoluciones` solo viaja en la segunda pasada, cuando el admin ya decidio que hacer con las
   * filas que quedaron en conflicto de titular; se reenvia el mismo archivo.
   */
  cargaMasivaUsuarios(
    archivo: File,
    opciones?: {
      campaniaId?: string;
      modo?: ModoCargaMasiva;
      resoluciones?: ResolucionConflictoTitular[];
    },
  ) {
    const formulario = new FormData();
    formulario.append('archivo', archivo, archivo.name);
    if (opciones?.modo) {
      formulario.append('modo', opciones.modo);
    }
    if (opciones?.resoluciones?.length) {
      formulario.append('reasignaciones', JSON.stringify(opciones.resoluciones));
    }

    return this.api.post<ReporteCargaMasiva>(
      '/api/admin/usuarios/carga-masiva',
      formulario,
      opciones?.campaniaId ? { campaniaId: opciones.campaniaId } : undefined,
    );
  }

  // I-08 v2: plantilla vacia con la cabecera oficial, generada por el servidor.
  descargarPlantillaCarga() {
    return this.api.getBlob('/api/admin/usuarios/plantilla-carga');
  }

  tags(query?: Record<string, string | number | undefined>) {
    return this.api.get<PagedResult<TagAdmin>>('/api/admin/tags', query);
  }

  crearTag(body: Partial<TagAdmin>) {
    return this.api.post<TagAdmin>('/api/admin/tags', body);
  }

  campanias(query?: Record<string, string | number | undefined>) {
    return this.api.get<PagedResult<Campania>>('/api/admin/campanias', query);
  }

  campania(id: string) {
    return this.api.get<Campania>(`/api/admin/campanias/${id}`);
  }

  crearCampania(body: unknown) {
    return this.api.post<Campania>('/api/admin/campanias', body);
  }

  actualizarCampania(id: string, body: unknown) {
    return this.api.put<Campania>(`/api/admin/campanias/${id}`, body);
  }

  actualizarLocalizacionesCampania(id: string, body: unknown) {
    return this.api.put<Campania>(`/api/admin/campanias/${id}/localizaciones`, body);
  }

  catalogosTextos(query?: Record<string, string | number | undefined>) {
    return this.api.get<CatalogoTextos[]>('/api/admin/catalogos-textos', query);
  }

  versionesCatalogoTextos(familiaId: string, idioma: string) {
    return this.api.get<CatalogoTextos[]>(
      `/api/admin/catalogos-textos/${encodeURIComponent(familiaId)}/${idioma}/versiones`,
    );
  }

  catalogoTextosEfectivo(idioma: string) {
    return this.api.get<CatalogoTextosEfectivo>('/api/admin/catalogos-textos/efectivo', { idioma });
  }

  // DT-P32-02 §4: base curada compilada; no depende de la configuracion del ambiente.
  crearSemillaBaseCatalogoTextos(idioma: string) {
    return this.api.post<CatalogoTextos>(`/api/admin/catalogos-textos/semillas/${idioma}/base`);
  }

  // DT-P32-02 §4: revisa la configuracion anterior del ambiente sin guardar nada.
  prevalidarSemillaLegacy(idioma: string) {
    return this.api.get<PrevalidacionCatalogoTextos>(
      `/api/admin/catalogos-textos/semillas/${idioma}/legacy/preview`,
    );
  }

  // DT-P32-02 §6: descarga completa de la configuracion anterior, aunque sea invalida.
  exportarSemillaLegacy(idioma: string) {
    return this.api.getBlob(`/api/admin/catalogos-textos/semillas/${idioma}/legacy/exportar`);
  }

  importarSemillaLegacy(idioma: string) {
    return this.api.post<CatalogoTextos>(`/api/admin/catalogos-textos/semillas/${idioma}/legacy`);
  }

  // DT-P32-02 §3.3: mismo cuerpo y mismo validador que la importacion, sin escribir.
  prevalidarImportacionCatalogoTextos(archivo: unknown, idioma: string) {
    return this.api.post<PrevalidacionCatalogoTextos>(
      '/api/admin/catalogos-textos/importar/prevalidar',
      archivo,
      { idioma },
    );
  }

  /** El archivo viaja tal cual: el servidor ignora sus metadatos y numera la version nueva. */
  importarCatalogoTextos(archivo: unknown, idioma: string) {
    return this.api.post<CatalogoTextos>('/api/admin/catalogos-textos/importar', archivo, {
      idioma,
    });
  }

  readinessCatalogosTextos(idioma?: string) {
    return this.api.get<ReadinessCatalogosTextos>(
      '/api/admin/catalogos-textos/readiness',
      idioma ? { idioma } : undefined,
    );
  }

  exportarCatalogoTextos(catalogo: CatalogoTextos) {
    return this.api.getBlob(
      `/api/admin/catalogos-textos/${encodeURIComponent(catalogo.familiaId)}/${catalogo.idioma}/versiones/${catalogo.version}/exportar`,
    );
  }

  actualizarCatalogoTextos(catalogo: CatalogoTextos, contenido: ContenidoCatalogoTextos) {
    return this.api.put<CatalogoTextos>(
      `/api/admin/catalogos-textos/${encodeURIComponent(catalogo.familiaId)}/${catalogo.idioma}/versiones/${catalogo.version}`,
      contenido,
      { 'If-Match': catalogo.etag },
    );
  }

  activarCatalogoTextos(catalogo: CatalogoTextos) {
    return this.api.post<CatalogoTextos>(
      `/api/admin/catalogos-textos/${encodeURIComponent(catalogo.familiaId)}/${catalogo.idioma}/versiones/${catalogo.version}/activar`,
      {},
      undefined,
      { 'If-Match': catalogo.etag },
    );
  }

  cambiarEstadoCampania(id: string, estado: string) {
    return this.api.patch<Campania>(`/api/admin/campanias/${id}/estado`, { estado });
  }

  crearMensajeInicial(campaniaId: string, body: unknown) {
    return this.api.post(`/api/admin/campanias/${campaniaId}/mensajes-iniciales`, body);
  }

  crearPregunta(campaniaId: string, body: unknown) {
    return this.api.post(`/api/admin/campanias/${campaniaId}/preguntas`, body);
  }

  actualizarPregunta(campaniaId: string, preguntaId: string, body: unknown) {
    return this.api.put<Pregunta>(
      `/api/admin/campanias/${campaniaId}/preguntas/${preguntaId}`,
      body,
    );
  }

  participantes(campaniaId: string) {
    return this.api.get<ParticipanteCampania[]>(`/api/admin/campanias/${campaniaId}/participantes`);
  }

  previewParticipantes(campaniaId: string, query: Record<string, string | undefined>) {
    return this.api.get<{ total: number; items: ParticipantePreview[] }>(
      `/api/admin/campanias/${campaniaId}/participantes/preview`,
      query,
    );
  }

  asociarParticipantes(campaniaId: string, usuarioIds: string[]) {
    return this.api.post<ParticipanteCampania[]>(
      `/api/admin/campanias/${campaniaId}/participantes`,
      { usuarioIds },
    );
  }

  // P-03: reinicio de datos de prueba (conserva campania/config/usuarios).
  reiniciarParticipante(campaniaId: string, usuarioId: string, reiniciarEnvios: boolean) {
    return this.api.post<ReporteReinicioDatos>(
      `/api/admin/campanias/${campaniaId}/participantes/${usuarioId}/reiniciar`,
      { reiniciarEnvios },
    );
  }

  reiniciarDatosCampania(
    campaniaId: string,
    opciones: { usuarioIds?: string[]; reiniciarEnvios?: boolean },
  ) {
    return this.api.post<ReporteReinicioDatos>(
      `/api/admin/campanias/${campaniaId}/reiniciar-datos`,
      opciones,
    );
  }

  // P-15: purga total de campañas y usuarios no administrativos (arranque en frío de pruebas).
  // Exige la palabra de confirmacion exacta y el flag Seguridad:PermitirReinicioDatos en el backend.
  purgarCampanias(confirmacion: string) {
    return this.api.post<ReportePurgaCampanias>('/api/admin/mantenimiento/purgar-campanias', {
      confirmacion,
    });
  }

  envios(campaniaId: string) {
    return this.api.get<EnvioEstado[]>(`/api/admin/campanias/${campaniaId}/envios`);
  }

  enviar(campaniaId: string, participantes: string[], mensajeInicialId?: string) {
    return this.api.post<JobEnvio>(`/api/admin/campanias/${campaniaId}/envios`, {
      participantes,
      mensajeInicialId,
    });
  }

  reenviar(campaniaId: string, mensajeInicialId?: string) {
    return this.api.post<JobEnvio>(`/api/admin/campanias/${campaniaId}/envios/reenviar`, {
      mensajeInicialId,
    });
  }

  reintentar(campaniaId: string, mensajeInicialId?: string) {
    return this.api.post<JobEnvio>(`/api/admin/campanias/${campaniaId}/envios/reintentar`, {
      mensajeInicialId,
    });
  }

  job(jobId: string) {
    return this.api.get<JobEnvio>(`/api/admin/jobs/${jobId}`);
  }

  rubricas(query?: Record<string, string | number | undefined>) {
    return this.api.get<PagedResult<Rubrica>>('/api/admin/rubricas', query);
  }

  crearRubrica(body: unknown) {
    return this.api.post<Rubrica>('/api/admin/rubricas', body);
  }

  actualizarRubrica(id: string, body: unknown) {
    return this.api.put<Rubrica>(`/api/admin/rubricas/${id}`, body);
  }

  crearVersionRubrica(id: string, body: unknown) {
    return this.api.post<Rubrica>(`/api/admin/rubricas/${id}/versiones`, body);
  }

  cambiarEstadoRubrica(id: string, estado: string) {
    return this.api.patch<Rubrica>(`/api/admin/rubricas/${id}/estado`, { estado });
  }

  /**
   * DT-RUB-01 (04 §5.5): valida y compila la misma estructura que la escritura, sin escribir. Es la
   * unica fuente del preview: el portal no mantiene un segundo compilador en TypeScript.
   */
  prevalidarRubrica(body: unknown) {
    return this.api.post<PrevalidacionRubrica>('/api/admin/rubricas/prevalidar', body);
  }

  prompts(query?: Record<string, string | number | undefined>) {
    return this.api.get<PagedResult<PromptConfig>>('/api/admin/prompts', query);
  }

  crearPrompt(body: unknown) {
    return this.api.post<PromptConfig>('/api/admin/prompts', body);
  }

  actualizarPrompt(id: string, body: unknown) {
    return this.api.put<PromptConfig>(`/api/admin/prompts/${id}`, body);
  }

  crearVersionPrompt(id: string, body: unknown) {
    return this.api.post<PromptConfig>(`/api/admin/prompts/${id}/versiones`, body);
  }

  cambiarEstadoPrompt(id: string, estado: string) {
    return this.api.patch<PromptConfig>(`/api/admin/prompts/${id}/estado`, { estado });
  }

  aprobarPrompt(id: string, aprobadoPor: string) {
    return this.api.post<PromptConfig>(`/api/admin/prompts/${id}/aprobar`, { aprobadoPor });
  }

  configsLlm(query?: Record<string, string | number | undefined>) {
    return this.api.get<PagedResult<ConfigLlm>>('/api/admin/config-llm', query);
  }

  crearConfigLlm(body: unknown) {
    return this.api.post<ConfigLlm>('/api/admin/config-llm', body);
  }

  actualizarConfigLlm(id: string, body: unknown) {
    return this.api.put<ConfigLlm>(`/api/admin/config-llm/${id}`, body);
  }

  conversaciones(campaniaId: string) {
    return this.api.get<PagedResult<Conversacion>>('/api/admin/conversaciones', { campaniaId });
  }

  /** P-34 H-04: la actividad de la campaña completa, no las 25 primeras conversaciones. */
  conversacionesTodas(campaniaId: string) {
    return this.paginarTodo<Conversacion>((page) =>
      this.api.get<PagedResult<Conversacion>>('/api/admin/conversaciones', {
        campaniaId,
        page,
        pageSize: TAMANO_PAGINA_MAXIMO,
      }),
    );
  }

  /** I-19 (04 §5.8): una fila por idea lógica; es la unidad principal de Resultados. */
  ideas(campaniaId: string, estadoResultado?: string) {
    return this.api.get<PagedResult<IdeaConsolidada>>('/api/admin/ideas', {
      campaniaId,
      estadoResultado: estadoResultado || undefined,
      pageSize: 100,
    });
  }

  /**
   * P-34 corte 2: el listado dejó de resolver la versión vigente idea por idea, así que ya se puede
   * recorrer completo sin multiplicar lecturas puntuales. Es lo que hace exactos los contadores por
   * estado de Resultados (H-04) en una campaña de 1.000 ideas.
   */
  ideasTodas(campaniaId: string, filtros: FiltrosIdeas = {}) {
    return this.paginarTodo<IdeaConsolidada>((page) =>
      this.api.get<PagedResult<IdeaConsolidada>>('/api/admin/ideas', {
        campaniaId,
        ...limpiarFiltros(filtros),
        page,
        pageSize: TAMANO_PAGINA_MAXIMO,
      }),
    );
  }

  /**
   * P-34 §4.3: solo el `total` de la campaña sin filtros, para que la paginación pueda decir
   * «… · 1.024 en la campaña». Pide una fila: es la consulta más barata que responde esa pregunta.
   */
  conteoIdeasCampania(campaniaId: string) {
    return this.api.get<PagedResult<IdeaConsolidada>>('/api/admin/ideas', {
      campaniaId,
      page: 1,
      pageSize: 1,
    });
  }

  /**
   * P-34 §4.5 (04 §5.8): el alcance lo resuelve el servidor con el mismo filtro que la pantalla, así
   * que aquí solo viajan los filtros vigentes más el recurso, el formato y el anonimizado.
   */
  exportarResultados(
    campaniaId: string,
    opciones: { recurso: string; formato: string; anonimizado: boolean },
    filtros: FiltrosIdeas = {},
  ) {
    return this.api.getArchivo(`/api/admin/campanias/${campaniaId}/exportar`, {
      ...limpiarFiltros(filtros),
      recurso: opciones.recurso,
      formato: opciones.formato,
      anonimizado: opciones.anonimizado ? 'true' : undefined,
    });
  }

  exportarDocumentos(campaniaId: string, anonimizado: boolean, filtros: FiltrosIdeas = {}) {
    return this.api.getArchivo(`/api/admin/campanias/${campaniaId}/documentos.zip`, {
      ...limpiarFiltros(filtros),
      anonimizado: anonimizado ? 'true' : undefined,
    });
  }

  /**
   * P-34 §4.6: el resumen lo calcula el servidor sobre el mismo conjunto filtrado que la tabla; con
   * 1.000 ideas, hacerlo en el navegador obligaría a descargarlas todas (D5).
   */
  resumenCampania(campaniaId: string, filtros: FiltrosIdeas = {}) {
    return this.api.get<ResumenCampania>(
      `/api/admin/campanias/${campaniaId}/resumen`,
      limpiarFiltros(filtros),
    );
  }

  /** I-19: detalle auditable con versiones, aportes y evaluación vigente. */
  idea(campaniaId: string, id: string) {
    return this.api.get<DetalleIdea>(`/api/admin/ideas/${id}`, { campaniaId });
  }

  respuestas(campaniaId: string, nivelMadurez?: string) {
    return this.api.get<PagedResult<Respuesta>>('/api/admin/respuestas', {
      campaniaId,
      nivelMadurez: nivelMadurez || undefined,
    });
  }

  /** P-34 H-04: las respuestas históricas también llegaban de 25 en 25. */
  respuestasTodas(campaniaId: string, nivelMadurez?: string) {
    return this.paginarTodo<Respuesta>((page) =>
      this.api.get<PagedResult<Respuesta>>('/api/admin/respuestas', {
        campaniaId,
        nivelMadurez: nivelMadurez || undefined,
        page,
        pageSize: TAMANO_PAGINA_MAXIMO,
      }),
    );
  }

  respuesta(campaniaId: string, id: string) {
    return this.api.get<{ respuesta: Respuesta; evaluacion: Evaluacion | null }>(
      `/api/admin/respuestas/${id}`,
      {
        campaniaId,
      },
    );
  }

  markdown(campaniaId: string) {
    return this.api.get<PagedResult<ArtefactoMarkdown>>('/api/admin/markdown', { campaniaId });
  }

  /**
   * P-34 H-03: el listado sin `pageSize` devolvía 25 artefactos y el portal concluía que la idea 26
   * en adelante no tenía documento. Se recorren todas las páginas antes de buscar por `ideaRef`.
   */
  markdownTodo(campaniaId: string) {
    return this.paginarTodo<ArtefactoMarkdown>((page) =>
      this.api.get<PagedResult<ArtefactoMarkdown>>('/api/admin/markdown', {
        campaniaId,
        page,
        pageSize: TAMANO_PAGINA_MAXIMO,
      }),
    );
  }

  markdownDetalle(campaniaId: string, id: string) {
    return this.api.get<ArtefactoMarkdown>(`/api/admin/markdown/${id}`, { campaniaId });
  }

  regenerarMarkdown(campaniaId: string, id: string) {
    return this.api.post<ArtefactoMarkdown>(
      `/api/admin/markdown/${id}/regenerar`,
      {},
      { campaniaId },
    );
  }

  /**
   * P-34 §2.1 (H-02/H-03/H-04): recorre las páginas de un listado paginado hasta reunir el `total`
   * que declara el servidor y devuelve un único resultado con todos los elementos. Se detiene ante
   * una página vacía o un servidor que no informa `total`, de modo que un backend anterior degrada
   * al comportamiento actual (una sola página) en vez de girar en falso.
   */
  private paginarTodo<T>(
    cargarPagina: (page: number) => Observable<PagedResult<T>>,
  ): Observable<PagedResult<T>> {
    const siguiente = (page: number, acumulados: T[]): Observable<PagedResult<T>> =>
      cargarPagina(page).pipe(
        switchMap((resultado) => {
          const recibidos = resultado.items ?? [];
          const items = page === 1 ? [...recibidos] : [...acumulados, ...recibidos];
          const total = resultado.total ?? items.length;
          const faltan = items.length < total && recibidos.length > 0 && page < MAXIMO_PAGINAS;
          return faltan
            ? siguiente(page + 1, items)
            : of({ ...resultado, items, page: 1, pageSize: items.length, total });
        }),
      );

    return siguiente(1, []);
  }
}
