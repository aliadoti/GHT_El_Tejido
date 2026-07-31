export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  continuationToken?: string;
}

export interface ApiError {
  error?: {
    code?: string;
    message?: string;
    details?: Array<{ field?: string; issue?: string }>;
    correlationId?: string;
  };
}

export interface UsuarioSesion {
  id: string;
  nombre: string;
  rol: 'admin' | 'visor' | string;
}

export interface SesionResponse {
  usuario: UsuarioSesion;
  csrfToken: string;
  expiraEn: string;
}

export interface MeResponse {
  usuario: UsuarioSesion;
}

export interface UsuarioAdmin {
  id: string;
  nombre: string;
  whatsappNormalizado: string;
  rol: 'admin' | 'visor' | 'participante' | string;
  estado: 'activo' | 'inactivo' | string;
  area: string;
  empresa: string;
  tags: string[];
  propiedadesDinamicas: Record<string, unknown>;
  creadoEn: string;
  actualizadoEn: string;
}

export interface TagAdmin {
  id: string;
  nombre: string;
  tipoTag: string;
  descripcion?: string | null;
  estado: string;
  creadoEn: string;
}

export interface Campania {
  id: string;
  nombre: string;
  descripcion: string;
  objetivo: string;
  estado: 'borrador' | 'activa' | 'cerrada' | 'archivada' | string;
  mensajesIniciales?: MensajeInicial[];
  preguntas?: Pregunta[];
  rubricaRef?: string;
  promptRefs?: Record<string, string>;
  configLLMRef?: string;
  configConversacional?: ConfigConversacional;
  configSeguridad?: ConfigSeguridad;
  usuariosHabilitados?: string[];
  creadoEn: string;
  actualizadoEn: string;
}

export interface ConfigConversacional {
  maxRepreguntas: number;
  mensajeCierre: string;
  segmentacionIdeas: boolean;
  // I-18: ambos campos son aditivos; OFF/null conserva el flujo multi-idea anterior.
  coachingSecuencialIdeas?: boolean;
  minutosCoachingPorIdea?: number | null;
  // I-09 (aditivo): tejido colectivo por campaña. La UI de activación la aporta I-10 (Sprint 2);
  // aquí solo se modela para preservar el valor en el round-trip de edición.
  tejidoColectivo?: boolean;
  // I-05 (aditivo): el portal preserva el flag aunque la activación UI se defina después.
  parafraseo?: boolean;
  // P-13 + I-17 (aditivo): umbral único compartido (cierre + madurez de guardado + paráfrasis).
  // null hereda el default numérico global; 0 apaga el cierre solo para esta campaña. Lo puede
  // sobreescribir además el umbral por pregunta (precedencia pregunta → campaña → global).
  umbralCierreAnticipado?: number | null;
  // I-17 §7 (aditivo): ventana de cierre por inactividad de sesión, en minutos. null hereda el
  // default global; 0 o negativo desactiva el cierre por inactividad solo para esta campaña.
  minutosInactividadSesion?: number | null;
  numeroWhatsAppSaliente?: string | null;
  // P-26 (aditivo, default false): mientras la campaña esté activa, permite que un participante que
  // ya terminó su recorrido vuelva y comience ideas nuevas, cada una con su propio historial.
  // Campo ausente = false; no reemplaza al estado de la campaña.
  participacionContinua?: boolean;
}

// P-10: cupos y presupuesto de la campaña (0 = desactivado en cada palanca).
export interface ConfigSeguridad {
  maxCaracteresMensaje: number;
  maxMensajesPorUsuario: number;
  maxLlamadasLlmPorUsuario: number;
  presupuestoTokensCampania: number;
}

export interface MensajeInicial {
  id: string;
  nombreInterno: string;
  texto: string;
  orden: number;
  variablesDinamicas: string[];
  estado: string;
}

export interface Pregunta {
  id: string;
  texto: string;
  instruccion: string;
  categoria: string;
  orden: number;
  estado: string;
  rubricaRef?: string | null;
  versionRubrica?: number | null;
  promptRefs?: Record<string, string>;
  maxRepreguntas: number;
  limitesSeguridad?: {
    maxCaracteresMensaje: number;
    maxLlamadasLlm: number;
  };
  configMarkdown?: {
    tipoArtefacto: string;
  };
  // I-17 (aditivo): override del umbral compartido por pregunta. null hereda el de la campaña.
  umbralCierreAnticipado?: number | null;
}

export interface ParticipantePreview {
  usuarioId: string;
  nombre: string;
  whatsappNormalizado: string;
  area: string;
  empresa: string;
  tags: string[];
}

export interface ParticipanteCampania {
  id: string;
  campaniaId: string;
  usuarioId: string;
  whatsappNormalizado: string;
  estado: string;
  estadoEnvio: string;
  estadoRespuesta: string;
  fechaInclusion: string;
  fechaPrimerEnvio?: string | null;
  fechaUltimaRespuesta?: string | null;
}

export interface Rubrica {
  id: string;
  nombre: string;
  descripcion: string;
  contenidoMarkdown: string;
  escala: { min: number; max: number };
  criterios: Array<{ nombre: string; peso: number }>;
  version: number;
  estado: string;
  creadoEn: string;
  actualizadoEn: string;
}

export interface PromptConfig {
  id: string;
  nombre: string;
  tipoPrompt: string;
  contenido: string;
  version: number;
  estado: string;
  aprobadoPor?: string | null;
  fechaAprobacion?: string | null;
  creadoEn: string;
  actualizadoEn: string;
}

export interface ConfigLlm {
  id: string;
  nombre: string;
  proveedor: string;
  modelo: string;
  endpoint: string;
  apiKeyRef: string;
  apiKeyMascara: string;
  parametros: Record<string, unknown>;
  limitesTokens: { maxPrompt: number; maxCompletion: number };
  timeoutSegundos: number;
  maxReintentos: number;
  estado: string;
  creadoEn: string;
  actualizadoEn: string;
}

export interface EnvioEstado {
  usuarioId: string;
  numero: string;
  estadoEnvio: string;
  estadoRespuesta: string;
  error?: string | null;
}

export interface JobEnvio {
  jobId: string;
  campaniaId: string;
  encolados: number;
  enviados?: number;
  errores?: number;
  estado: string;
  creadoEn?: string;
}

export interface Conversacion {
  id: string;
  campaniaId: string;
  usuarioId: string;
  preguntaId: string;
  canal: string;
  estado: string;
  estadoMaquina: string;
  repreguntasUsadas: number;
  ventanaServicioVenceEn?: string | null;
  fechaInicio: string;
  fechaCierre?: string | null;
}

export interface Respuesta {
  id: string;
  campaniaId: string;
  usuarioId: string;
  preguntaId: string;
  conversacionId: string;
  texto: string;
  canal: string;
  esRepregunta: boolean;
  estado: string;
  fecha: string;
  tagsSnapshot: string[];
  ideaIndice?: number | null;
  respuestaPadreId?: string | null;
  // I-17 (aditivo): nivel de madurez sellado al evaluar. Ausente en datos históricos = incubación.
  nivelMadurez?: 'maduro' | 'incubacion' | string;
  // I-19 (aditivo): enlaza el aporte con su idea lógica. Ausente = resultado histórico.
  ideaId?: string | null;
  tipoAporte?: 'inicial' | 'complemento' | 'correccion' | 'nuevaIdea' | string | null;
}

/** I-19 (04 §5.8): unidad principal de Resultados; una fila por idea, no por aporte. */
export interface IdeaConsolidada {
  id: string;
  campaniaId: string;
  usuarioId: string;
  preguntaId: string;
  conversacionId: string;
  ideaIndice: number;
  respuestaRaizId: string;
  /** Texto de la versión vigente: la confirmada o, si aún no hay, la propuesta. */
  texto?: string | null;
  /** `false` cuando el texto mostrado todavía no fue confirmado por el participante. */
  confirmada: boolean;
  estadoFlujo: 'pendienteConfirmacion' | 'enMejora' | 'enRevision' | 'cerrada' | string;
  estadoResultado?: 'madura' | 'pendiente' | 'rechazada' | string | null;
  nivelMadurez: 'maduro' | 'incubacion' | string;
  estadoCuraduria?: 'pendiente' | string | null;
  motivoCierre?: string | null;
  versionConfirmadaRef?: string | null;
  versionPropuestaRef?: string | null;
  evaluacionVigenteRef?: string | null;
  creadaEn: string;
  actualizadaEn: string;
}

/** I-19: versión inmutable de una idea; el historial nunca se sobrescribe. */
export interface VersionIdea {
  id: string;
  ideaId: string;
  numeroVersion: number;
  versionAnteriorId?: string | null;
  texto: string;
  estadoConfirmacion: 'propuesta' | 'confirmada' | 'descartada' | 'expirada' | string;
  origen: string;
  aporteIdsAcumulados: string[];
  aporteNuevoIds: string[];
  evaluacionRef?: string | null;
  generadaEn: string;
  confirmadaEn?: string | null;
}

/** I-19: detalle auditable de una idea (04 §5.8). */
export interface DetalleIdea {
  idea: IdeaConsolidada;
  versionConfirmada: VersionIdea | null;
  versionPropuesta: VersionIdea | null;
  evaluacion: Evaluacion | null;
  versiones: VersionIdea[];
  aportes: Respuesta[];
}

export interface Evaluacion {
  id: string;
  campaniaId: string;
  respuestaId: string;
  usuarioId: string;
  preguntaId: string;
  calificacionTotal: number;
  explicacion: string;
  retroalimentacionEnviada: string;
  parafraseoDevuelto?: string | null;
  recomendacion: string;
  repreguntaSugerida?: string | null;
  temas: string[];
  entidades: string[];
  fecha: string;
}

export interface ArtefactoMarkdown {
  id: string;
  campaniaId: string;
  tipoArtefacto: string;
  usuarioId: string;
  preguntaId: string;
  respuestaRef?: string | null;
  evaluacionRef?: string | null;
  // I-19 (03 §3.10): referencias del artefacto canónico por idea.
  ideaRef?: string | null;
  versionIdeaRef?: string | null;
  contenidoMarkdown?: string;
  blobPath: string;
  estado: string;
  version: number;
  creadoEn: string;
  actualizadoEn: string;
}

// I-08: reporte por fila de la carga masiva de participantes (04 §5.1). Sin PII: solo usuarioId.
export interface ResultadoFilaCarga {
  fila: number;
  resultado: 'creado' | 'actualizado' | 'rechazado' | string;
  usuarioId: string | null;
  motivo?: string | null;
}

export interface ReporteCargaMasiva {
  totalFilas: number;
  creados: number;
  actualizados: number;
  rechazados: number;
  asociados: number;
  filas: ResultadoFilaCarga[];
}

// P-03: reporte de conteos que devuelven los endpoints de reinicio de datos.
export interface ReporteReinicioDatos {
  conversaciones: number;
  mensajes: number;
  respuestas: number;
  evaluaciones: number;
  artefactos: number;
  blobsBorrados: number;
  blobsFallidos: number;
  participantesReseteados: number;
}

// P-15: purga total de datos de campañas (borra campañas y usuarios no administrativos).
export interface ReportePurgaCampanias {
  campanias: number;
  conversaciones: number;
  mensajes: number;
  respuestas: number;
  evaluaciones: number;
  artefactos: number;
  blobsBorrados: number;
  blobsFallidos: number;
  participantes: number;
  usuariosBorrados: number;
}
