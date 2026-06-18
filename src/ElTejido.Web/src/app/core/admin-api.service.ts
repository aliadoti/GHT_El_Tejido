import { Injectable, inject } from '@angular/core';

import { ApiClient } from './api-client.service';
import {
  ArtefactoMarkdown,
  Campania,
  ConfigLlm,
  Conversacion,
  EnvioEstado,
  Evaluacion,
  JobEnvio,
  PagedResult,
  ParticipanteCampania,
  ParticipantePreview,
  Pregunta,
  PromptConfig,
  Respuesta,
  Rubrica,
  TagAdmin,
  UsuarioAdmin,
} from './api-models';

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

  prompts(query?: Record<string, string | number | undefined>) {
    return this.api.get<PagedResult<PromptConfig>>('/api/admin/prompts', query);
  }

  crearPrompt(body: unknown) {
    return this.api.post<PromptConfig>('/api/admin/prompts', body);
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

  respuestas(campaniaId: string) {
    return this.api.get<PagedResult<Respuesta>>('/api/admin/respuestas', { campaniaId });
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
