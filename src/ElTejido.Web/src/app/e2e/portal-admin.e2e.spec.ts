import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';

import { AdminApiService } from '../core/admin-api.service';
import { ApiClient } from '../core/api-client.service';
import {
  ArtefactoMarkdown,
  Campania,
  ConfigLlm,
  PagedResult,
  ParticipanteCampania,
  PromptConfig,
  ReporteCargaMasiva,
  Respuesta,
  Rubrica,
  SesionResponse,
  UsuarioAdmin,
} from '../core/api-models';
import { authInterceptor } from '../core/auth.interceptor';
import { AuthService } from '../core/auth.service';
import { ConfigLlmPage } from '../features/config-llm/config-llm.page';
import { CampaniasPage } from '../features/campanias/campanias.page';
import { PromptsPage } from '../features/prompts/prompts.page';
import { RubricasPage } from '../features/rubricas/rubricas.page';
import { UsuariosPage } from '../features/usuarios/usuarios.page';

/**
 * E2E del portal Angular (13 §1, criterios de aceptacion 13 §2): recorre el wiring real de la SPA
 * —AuthService, ApiClient, authInterceptor, AdminApiService y la pantalla UsuariosPage— contra un
 * backend HTTP simulado (HttpTestingController). Cubre el camino del administrador: login OTP →
 * sesion/CSRF → seguridad transversal del interceptor → alta/consulta de usuarios → consulta de
 * resultados/Markdown. Las llamadas reales al backend se mockean (13 §1).
 */
describe('Portal admin E2E (recorrido SPA)', () => {
  const NUMERO_ADMIN = '573001119999';
  const CODIGO = '482913';
  const CSRF = 'csrf-token-abc';

  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  /** Realiza el intercambio OTP completo y deja una sesion admin con CSRF activa. */
  function autenticarComoAdmin(auth: AuthService, rol = 'admin'): void {
    let recibido: SesionResponse | undefined;
    auth.verifyCode(NUMERO_ADMIN, CODIGO).subscribe((s) => (recibido = s));

    const req = http.expectOne('/api/auth/verify-code');
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    req.flush({
      usuario: { id: 'u_admin', nombre: 'Admin GHT', rol },
      csrfToken: CSRF,
      expiraEn: '2026-12-31T00:00:00Z',
    } satisfies SesionResponse);

    expect(recibido?.csrfToken).toBe(CSRF);
  }

  it('paso 1-2: el login OTP es neutral y emite sesion + token CSRF', () => {
    const auth = TestBed.inject(AuthService);

    let mensaje = '';
    auth.requestCode(NUMERO_ADMIN).subscribe((r) => (mensaje = r.message));
    const reqCode = http.expectOne('/api/auth/request-code');
    expect(reqCode.request.method).toBe('POST');
    expect(reqCode.request.body).toEqual({ numero: NUMERO_ADMIN });
    reqCode.flush({ message: 'Si el numero esta registrado, recibiras un codigo.' });
    expect(mensaje).toContain('codigo');

    expect(auth.isAuthenticated()).toBe(false);
    autenticarComoAdmin(auth);

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.isAdmin()).toBe(true);
    expect(auth.csrfToken()).toBe(CSRF);
  });

  it('paso 3 (seguridad 13 §4): withCredentials siempre; X-CSRF-Token solo en mutaciones', () => {
    const auth = TestBed.inject(AuthService);
    const api = TestBed.inject(ApiClient);
    autenticarComoAdmin(auth);

    // Las lecturas viajan con credenciales pero sin token CSRF.
    api.get('/api/admin/usuarios').subscribe();
    const get = http.expectOne((r) => r.url === '/api/admin/usuarios' && r.method === 'GET');
    expect(get.request.withCredentials).toBe(true);
    expect(get.request.headers.has('X-CSRF-Token')).toBe(false);
    get.flush({ items: [], page: 1, pageSize: 50, total: 0 } satisfies PagedResult<UsuarioAdmin>);

    // Las mutaciones adjuntan el token CSRF de la sesion.
    api.post('/api/admin/usuarios', { nombre: 'X' }).subscribe();
    const post = http.expectOne((r) => r.url === '/api/admin/usuarios' && r.method === 'POST');
    expect(post.request.withCredentials).toBe(true);
    expect(post.request.headers.get('X-CSRF-Token')).toBe(CSRF);
    post.flush({});
  });

  it('paso 4 (13 §2.3): tras login el admin lista y crea usuarios desde la pantalla', async () => {
    const auth = TestBed.inject(AuthService);
    autenticarComoAdmin(auth);

    const fixture = TestBed.createComponent(UsuariosPage);

    // El constructor dispara load(): GET usuarios + GET tags + GET campanias.
    responderListaUsuarios([usuario('u1', 'Ana')]);
    http
      .expectOne((r) => r.url === '/api/admin/tags' && r.method === 'GET')
      .flush({ items: [], page: 1, pageSize: 100, total: 0 });
    responderListaCampanias([]);

    await fixture.whenStable();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ana');

    // Alta de usuario: mutacion con CSRF + recarga.
    const comp = fixture.componentInstance as unknown as {
      nuevoUsuario: Record<string, string>;
      guardarUsuario: () => void;
    };
    comp.nuevoUsuario = {
      nombre: 'ARENAS CHAVES JUAN PABLO',
      nombreSaludo: 'Juan Pablo',
      numero: '573004445566',
      rol: 'participante',
      area: 'Ventas',
      empresa: 'GHT',
    };
    comp.guardarUsuario();

    const post = http.expectOne((r) => r.url === '/api/admin/usuarios' && r.method === 'POST');
    expect(post.request.headers.get('X-CSRF-Token')).toBe(CSRF);
    expect((post.request.body as { nombre: string }).nombre).toBe('ARENAS CHAVES JUAN PABLO');
    expect((post.request.body as { nombreSaludo: string }).nombreSaludo).toBe('Juan Pablo');
    post.flush(usuario('u2', 'Beto'));

    // Recarga posterior al alta.
    responderListaUsuarios([usuario('u1', 'Ana'), usuario('u2', 'Beto')]);
    http
      .expectOne((r) => r.url === '/api/admin/tags' && r.method === 'GET')
      .flush({ items: [], page: 1, pageSize: 100, total: 0 });
    responderListaCampanias([]);

    await fixture.whenStable();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Beto');
  });

  it('I-08 UI: carga masiva de participantes sube CSV con CSRF y muestra el reporte por fila', async () => {
    const auth = TestBed.inject(AuthService);
    autenticarComoAdmin(auth);

    const fixture = TestBed.createComponent(UsuariosPage);

    // El constructor dispara load(): GET usuarios + GET tags + GET campanias.
    responderListaUsuarios([]);
    http
      .expectOne((r) => r.url === '/api/admin/tags' && r.method === 'GET')
      .flush({ items: [], page: 1, pageSize: 100, total: 0 });
    responderListaCampanias([campania('c_1', 'llm_1')]);

    await fixture.whenStable();
    fixture.detectChanges();

    const comp = fixture.componentInstance as unknown as {
      cargarArchivo: (solicitud: {
        archivo: File;
        modo: string;
        campaniaId: string;
        resoluciones: unknown[];
      }) => void;
      reporteCarga: () => ReporteCargaMasiva | null;
    };
    const archivo = new File(
      [
        'Empresa,ID Empresa,Sede,Nombre,Cargo,Email,Antigüedad en la empresa en años,Idioma,Telefono\n',
      ],
      'participantes.csv',
      { type: 'text/csv' },
    );
    comp.cargarArchivo({ archivo, modo: 'upsert', campaniaId: 'c_1', resoluciones: [] });

    const post = http.expectOne(
      (r) => r.url === '/api/admin/usuarios/carga-masiva' && r.method === 'POST',
    );
    expect(post.request.withCredentials).toBe(true);
    expect(post.request.headers.get('X-CSRF-Token')).toBe(CSRF);
    expect(post.request.params.get('campaniaId')).toBe('c_1');
    expect(post.request.body instanceof FormData).toBe(true);
    const archivoEnviado = (post.request.body as FormData).get('archivo') as File;
    expect(archivoEnviado.name).toBe(archivo.name);
    expect(archivoEnviado.type).toBe(archivo.type);

    const reporte: ReporteCargaMasiva = {
      totalFilas: 2,
      creados: 1,
      actualizados: 0,
      reasignados: 0,
      rechazados: 1,
      asociados: 1,
      filas: [
        { fila: 2, resultado: 'creado', usuarioId: 'u_1', motivo: null },
        { fila: 3, resultado: 'rechazado', usuarioId: null, motivo: 'numero_invalido' },
      ],
    };
    post.flush(reporte);

    // Recarga posterior a la carga.
    responderListaUsuarios([]);
    http
      .expectOne((r) => r.url === '/api/admin/tags' && r.method === 'GET')
      .flush({ items: [], page: 1, pageSize: 100, total: 0 });
    responderListaCampanias([campania('c_1', 'llm_1')]);

    await fixture.whenStable();
    fixture.detectChanges();

    expect(comp.reporteCarga()).toEqual(reporte);
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    // El motivo se muestra traducido al lenguaje del administrador, no como codigo tecnico.
    expect(texto).toContain('El teléfono no es un número válido');
    expect(texto).not.toMatch(/57300\d{7}/);
  });

  it('P-14: un visor consulta rubricas y prompts sin exponer ni disparar mutaciones', async () => {
    const auth = TestBed.inject(AuthService);
    autenticarComoAdmin(auth, 'visor');

    const rubricasFixture = TestBed.createComponent(RubricasPage);
    responderListaRubricas([rubrica('rub_1')]);
    await rubricasFixture.whenStable();
    rubricasFixture.detectChanges();
    pulsar(rubricasFixture.nativeElement as HTMLElement, 'Ver');
    rubricasFixture.detectChanges();

    const rubricasTexto = (rubricasFixture.nativeElement as HTMLElement).textContent ?? '';
    expect(rubricasTexto).toContain('Vista de rubrica');
    expect(rubricasTexto).toContain('Contenido derivado por el servidor');
    expect(rubricasTexto).toContain('Impacto');
    // DT-RUB-01: el visor ve la estructura completa (ids, pesos y orden), no solo el Markdown.
    expect(rubricasTexto).toContain('claridad');
    expect(rubricasTexto).toContain('60%');
    expect(rubricasTexto).not.toContain('Crear nueva version');
    expect(rubricasFixture.nativeElement.querySelector('textarea')).toBeNull();

    const promptsFixture = TestBed.createComponent(PromptsPage);
    responderListaPrompts([prompt('pr_1')]);
    await promptsFixture.whenStable();
    promptsFixture.detectChanges();
    pulsar(promptsFixture.nativeElement as HTMLElement, 'Ver');
    promptsFixture.detectChanges();

    const promptsTexto = (promptsFixture.nativeElement as HTMLElement).textContent ?? '';
    expect(promptsTexto).toContain('Vista de prompt');
    expect(promptsTexto).toContain('Contenido completo del prompt');
    expect(promptsTexto).toContain('Admin GHT');
    expect(promptsTexto).not.toContain('Aprobar');
    expect(promptsFixture.nativeElement.querySelector('textarea')).toBeNull();
  });

  it('paso 8 (13 §2.8): consulta respuestas y Markdown acotadas por campania, sin fuga de secretos', () => {
    const auth = TestBed.inject(AuthService);
    const admin = TestBed.inject(AdminApiService);
    autenticarComoAdmin(auth);

    let respuestas: PagedResult<Respuesta> | undefined;
    admin.respuestas('c_1').subscribe((p) => (respuestas = p));
    const rq = http.expectOne((r) => r.url === '/api/admin/respuestas');
    expect(rq.request.params.get('campaniaId')).toBe('c_1');
    rq.flush({
      items: [respuesta('resp_1')],
      page: 1,
      pageSize: 50,
      total: 1,
    } satisfies PagedResult<Respuesta>);
    expect(respuestas?.items[0].id).toBe('resp_1');

    let markdown: PagedResult<ArtefactoMarkdown> | undefined;
    admin.markdown('c_1').subscribe((p) => (markdown = p));
    const mq = http.expectOne((r) => r.url === '/api/admin/markdown');
    expect(mq.request.params.get('campaniaId')).toBe('c_1');
    mq.flush({
      items: [artefacto('md_1')],
      page: 1,
      pageSize: 50,
      total: 1,
    } satisfies PagedResult<ArtefactoMarkdown>);

    const contenido = markdown?.items[0].contenidoMarkdown ?? '';
    expect(contenido).toContain('# Respuesta');
    expect(contenido).not.toMatch(/llm-key|api[-_]?key|secret/i);
  });

  it('fase 10.1: el preset LLM rellena proveedor y endpoint antes de guardar', async () => {
    const auth = TestBed.inject(AuthService);
    autenticarComoAdmin(auth);

    const fixture = TestBed.createComponent(ConfigLlmPage);
    http
      .expectOne((r) => r.url === '/api/admin/config-llm' && r.method === 'GET')
      .flush({
        items: [],
        page: 1,
        pageSize: 50,
        total: 0,
      } satisfies PagedResult<ConfigLlm>);

    const comp = fixture.componentInstance as unknown as {
      form: Record<string, string>;
      aplicarPreset: (preset: string) => void;
      crear: () => void;
    };
    comp.aplicarPreset('Anthropic');
    comp.form['apiKeyRef'] = 'anthropic-key';
    comp.crear();

    const post = http.expectOne((r) => r.url === '/api/admin/config-llm' && r.method === 'POST');
    expect(post.request.headers.get('X-CSRF-Token')).toBe(CSRF);
    expect(post.request.body).toMatchObject({
      proveedor: 'Anthropic',
      endpoint: 'https://api.anthropic.com',
      modelo: 'claude-3-5-sonnet-latest',
      apiKeyRef: 'anthropic-key',
    });
    post.flush(configLlm('llm_anthropic'));
    http
      .expectOne((r) => r.url === '/api/admin/config-llm' && r.method === 'GET')
      .flush({
        items: [configLlm('llm_anthropic')],
        page: 1,
        pageSize: 50,
        total: 1,
      } satisfies PagedResult<ConfigLlm>);
    await fixture.whenStable();
  });

  it('fase 10.3: actualiza ConfigLLM, rubrica y prompt de una campania por PUT con CSRF', () => {
    const auth = TestBed.inject(AuthService);
    const admin = TestBed.inject(AdminApiService);
    autenticarComoAdmin(auth);

    let actualizada: Campania | undefined;
    admin
      .actualizarCampania('c_1', {
        nombre: 'Ideas 2026',
        descripcion: 'Nueva descripcion',
        objetivo: 'Nuevo objetivo',
        rubricaRef: 'rub_2',
        promptRefs: { evaluar: 'pr_2' },
        configLLMRef: 'llm_2',
      })
      .subscribe((campania) => (actualizada = campania));

    const put = http.expectOne((r) => r.url === '/api/admin/campanias/c_1' && r.method === 'PUT');
    expect(put.request.headers.get('X-CSRF-Token')).toBe(CSRF);
    expect(put.request.body).toMatchObject({
      rubricaRef: 'rub_2',
      promptRefs: { evaluar: 'pr_2' },
      configLLMRef: 'llm_2',
    });
    put.flush(campania('c_1', 'llm_2'));
    expect(actualizada?.configLLMRef).toBe('llm_2');
  });

  it('mejora campanias: actualiza una pregunta por PUT con CSRF', () => {
    const auth = TestBed.inject(AuthService);
    const admin = TestBed.inject(AdminApiService);
    autenticarComoAdmin(auth);

    let preguntaActualizada: { texto: string; maxRepreguntas: number } | undefined;
    admin
      .actualizarPregunta('c_1', 'p_1', {
        texto: 'Que mejora propone para reducir desperdicio?',
        instruccion: 'Valora claridad, impacto y viabilidad.',
        categoria: 'productividad',
        orden: 2,
        estado: 'activo',
        rubricaRef: 'rub_2',
        promptRefs: { evaluar: 'pr_2' },
        maxRepreguntas: 1,
        limitesSeguridad: { maxCaracteresMensaje: 1500, maxLlamadasLlm: 2 },
        configMarkdown: { tipoArtefacto: 'respuesta' },
      })
      .subscribe((pregunta) => (preguntaActualizada = pregunta));

    const put = http.expectOne(
      (r) => r.url === '/api/admin/campanias/c_1/preguntas/p_1' && r.method === 'PUT',
    );
    expect(put.request.headers.get('X-CSRF-Token')).toBe(CSRF);
    expect(put.request.body).toMatchObject({
      texto: 'Que mejora propone para reducir desperdicio?',
      promptRefs: { evaluar: 'pr_2' },
      limitesSeguridad: { maxCaracteresMensaje: 1500, maxLlamadasLlm: 2 },
    });
    put.flush({
      id: 'p_1',
      texto: 'Que mejora propone para reducir desperdicio?',
      instruccion: 'Valora claridad, impacto y viabilidad.',
      categoria: 'productividad',
      orden: 2,
      estado: 'activo',
      rubricaRef: 'rub_2',
      promptRefs: { evaluar: 'pr_2' },
      maxRepreguntas: 1,
      limitesSeguridad: { maxCaracteresMensaje: 1500, maxLlamadasLlm: 2 },
      configMarkdown: { tipoArtefacto: 'respuesta' },
    });
    expect(preguntaActualizada?.maxRepreguntas).toBe(1);
  });

  it('P-03: ambos reinicios del portal dejan el envio pendiente para una nueva prueba', () => {
    const auth = TestBed.inject(AuthService);
    autenticarComoAdmin(auth);

    const fixture = TestBed.createComponent(CampaniasPage);
    http
      .expectOne((r) => r.url === '/api/admin/campanias' && r.method === 'GET')
      .flush({ items: [campania('c_1', 'llm_1')], page: 1, pageSize: 50, total: 1 });
    responderListaRubricas([]);
    responderListaConfigsLlm([]);
    responderListaPrompts([]);
    responderListaUsuarios([usuario('u_1', 'Ana')]);

    const comp = fixture.componentInstance as unknown as {
      reiniciarParticipante: (campaniaId: string, participante: ParticipanteCampania) => void;
      reiniciarDatosCampania: (campania: Campania) => void;
    };
    const participante: ParticipanteCampania = {
      id: 'pc_1',
      campaniaId: 'c_1',
      usuarioId: 'u_1',
      whatsappNormalizado: '573001112233',
      estado: 'activo',
      estadoEnvio: 'enviado',
      estadoRespuesta: 'respondio',
      fechaInclusion: '2026-08-04T00:00:00Z',
      fechaPrimerEnvio: '2026-08-04T00:00:00Z',
    };
    const reporte = {
      conversaciones: 1,
      mensajes: 1,
      respuestas: 1,
      evaluaciones: 1,
      artefactos: 1,
      blobsBorrados: 1,
      blobsFallidos: 0,
      participantesReseteados: 1,
    };

    vi.spyOn(window, 'confirm').mockReturnValue(true);
    comp.reiniciarParticipante('c_1', participante);
    const reinicioParticipante = http.expectOne(
      '/api/admin/campanias/c_1/participantes/u_1/reiniciar',
    );
    expect(reinicioParticipante.request.body).toEqual({ reiniciarEnvios: true });
    reinicioParticipante.flush(reporte);
    http.expectOne('/api/admin/campanias/c_1/participantes').flush([]);

    vi.spyOn(window, 'prompt').mockReturnValue('Ideas 2026');
    comp.reiniciarDatosCampania(campania('c_1', 'llm_1'));
    const reinicioCampania = http.expectOne('/api/admin/campanias/c_1/reiniciar-datos');
    expect(reinicioCampania.request.body).toEqual({ reiniciarEnvios: true });
    reinicioCampania.flush(reporte);
    http.expectOne('/api/admin/campanias/c_1/participantes').flush([]);
  });

  // --- helpers de datos / respuestas mock -----------------------------------

  function responderListaUsuarios(items: UsuarioAdmin[]): void {
    http
      .expectOne((r) => r.url === '/api/admin/usuarios' && r.method === 'GET')
      .flush({
        items,
        page: 1,
        pageSize: 50,
        total: items.length,
      } satisfies PagedResult<UsuarioAdmin>);
  }

  function responderListaCampanias(items: Campania[]): void {
    http
      .expectOne((r) => r.url === '/api/admin/campanias' && r.method === 'GET')
      .flush({
        items,
        page: 1,
        pageSize: 100,
        total: items.length,
      } satisfies PagedResult<Campania>);
  }

  function responderListaRubricas(items: Rubrica[]): void {
    http
      .expectOne((r) => r.url === '/api/admin/rubricas' && r.method === 'GET')
      .flush({ items, page: 1, pageSize: 50, total: items.length } satisfies PagedResult<Rubrica>);
  }

  function responderListaConfigsLlm(items: ConfigLlm[]): void {
    http
      .expectOne((r) => r.url === '/api/admin/config-llm' && r.method === 'GET')
      .flush({
        items,
        page: 1,
        pageSize: 100,
        total: items.length,
      } satisfies PagedResult<ConfigLlm>);
  }

  function responderListaPrompts(items: PromptConfig[]): void {
    http
      .expectOne((r) => r.url === '/api/admin/prompts' && r.method === 'GET')
      .flush({
        items,
        page: 1,
        pageSize: 50,
        total: items.length,
      } satisfies PagedResult<PromptConfig>);
  }

  function pulsar(elemento: HTMLElement, etiqueta: string): void {
    const boton = Array.from(elemento.querySelectorAll('button')).find(
      (candidato) => candidato.textContent?.trim() === etiqueta,
    );
    expect(boton).toBeDefined();
    boton?.click();
  }

  function usuario(id: string, nombre: string): UsuarioAdmin {
    return {
      id,
      codigoUsuario: 1,
      codigoUsuarioLegible: 'U-000001',
      nombre,
      nombreSaludo: nombre,
      whatsappNormalizado: '573001112233',
      rol: 'participante',
      estado: 'activo',
      area: 'Operaciones',
      empresa: 'GHT',
      idioma: 'es',
      tags: [],
      propiedadesDinamicas: {},
      creadoEn: '2026-06-14T00:00:00Z',
      actualizadoEn: '2026-06-14T00:00:00Z',
    };
  }

  function respuesta(id: string): Respuesta {
    return {
      id,
      campaniaId: 'c_1',
      usuarioId: 'u1',
      preguntaId: 'p_1',
      conversacionId: 'conv_1',
      texto: 'Mi idea es reducir desperdicio',
      canal: 'whatsapp',
      esRepregunta: false,
      estado: 'evaluada',
      fecha: '2026-06-14T01:00:00Z',
      tagsSnapshot: [],
    };
  }

  function artefacto(id: string): ArtefactoMarkdown {
    return {
      id,
      campaniaId: 'c_1',
      tipoArtefacto: 'respuesta',
      usuarioId: 'u1',
      preguntaId: 'p_1',
      respuestaRef: 'resp_1',
      evaluacionRef: 'eval_1',
      contenidoMarkdown: '# Respuesta\n\nContenido determinista del artefacto operativo.',
      blobPath: 'c_1/md_1.md',
      estado: 'generado',
      version: 1,
      creadoEn: '2026-06-14T02:00:00Z',
      actualizadoEn: '2026-06-14T02:00:00Z',
    };
  }

  function configLlm(id: string): ConfigLlm {
    return {
      id,
      nombre: 'Anthropic',
      proveedor: 'Anthropic',
      modelo: 'claude-3-5-sonnet-latest',
      endpoint: 'https://api.anthropic.com',
      apiKeyRef: 'anthropic-key',
      apiKeyMascara: '********',
      parametros: {},
      limitesTokens: { maxPrompt: 6000, maxCompletion: 800 },
      timeoutSegundos: 30,
      maxReintentos: 2,
      estado: 'activo',
      creadoEn: '2026-06-14T00:00:00Z',
      actualizadoEn: '2026-06-14T00:00:00Z',
    };
  }

  function rubrica(id: string): Rubrica {
    return {
      id,
      nombre: 'Rubrica de impacto',
      descripcion: 'Valora la propuesta.',
      instruccionesGenerales: 'Evalua con evidencia del aporte.',
      contenidoMarkdown: '# Rubrica: Rubrica de impacto\n\nContenido derivado por el servidor.',
      hashEstructura: 'sha256:abc',
      integridadEstructural: 'valida',
      escala: { min: 1, max: 5 },
      criterios: [
        {
          id: 'claridad',
          nombre: 'Claridad',
          descripcion: 'Que tan concreta es.',
          peso: 0.4,
          orden: 1,
        },
        {
          id: 'impacto',
          nombre: 'Impacto',
          descripcion: 'Beneficio esperado.',
          peso: 0.6,
          orden: 2,
        },
      ],
      version: 3,
      estado: 'activa',
      creadoEn: '2026-07-21T00:00:00Z',
      actualizadoEn: '2026-07-21T00:00:00Z',
    };
  }

  function prompt(id: string): PromptConfig {
    return {
      id,
      nombre: 'Prompt de evaluacion',
      tipoPrompt: 'evaluar',
      contenido: 'Contenido completo del prompt.',
      version: 2,
      estado: 'activo',
      aprobadoPor: 'Admin GHT',
      fechaAprobacion: '2026-07-21T00:00:00Z',
      creadoEn: '2026-07-21T00:00:00Z',
      actualizadoEn: '2026-07-21T00:00:00Z',
    };
  }

  function campania(id: string, configLLMRef: string): Campania {
    return {
      id,
      nombre: 'Ideas 2026',
      descripcion: 'Nueva descripcion',
      objetivo: 'Nuevo objetivo',
      estado: 'borrador',
      rubricaRef: 'rub_2',
      promptRefs: { evaluar: 'pr_2' },
      configLLMRef,
      creadoEn: '2026-06-14T00:00:00Z',
      actualizadoEn: '2026-06-14T00:00:00Z',
    };
  }
});
