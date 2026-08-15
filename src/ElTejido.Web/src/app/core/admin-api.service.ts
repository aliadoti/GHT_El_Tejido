import { Injectable, inject } from '@angular/core';

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
  PromptConfig,
  ReporteCargaMasiva,
  ReportePurgaCampanias,
  ReporteReinicioDatos,
  ResolucionConflictoTitular,
  Respuesta,
  ResultadoReasignacionNumero,
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

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly api = inject(ApiClient);

  usuarios(query?: Record<string, string | number | undefined>) {
    return this.api.get<PagedResult<UsuarioAdmin>>('/api/admin/usuarios', query);
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

  /** I-19 (04 §5.8): una fila por idea lógica; es la unidad principal de Resultados. */
  ideas(campaniaId: string, estadoResultado?: string) {
    return this.api.get<PagedResult<IdeaConsolidada>>('/api/admin/ideas', {
      campaniaId,
      estadoResultado: estadoResultado || undefined,
      pageSize: 100,
    });
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
}
