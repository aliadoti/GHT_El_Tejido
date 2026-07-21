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
  // I-09 (aditivo): tejido colectivo por campaña. La UI de activación la aporta I-10 (Sprint 2);
  // aquí solo se modela para preservar el valor en el round-trip de edición.
  tejidoColectivo?: boolean;
  // I-05 (aditivo): el portal preserva el flag aunque la activación UI se defina después.
  parafraseo?: boolean;
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
  respuestaRef: string;
  evaluacionRef: string;
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
