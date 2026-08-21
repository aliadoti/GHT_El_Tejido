import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AdminApiService } from './admin-api.service';
import {
  ArtefactoMarkdown,
  Conversacion,
  IdeaConsolidada,
  PagedResult,
  UsuarioAdmin,
} from './api-models';

/**
 * P-34 §2.1 (H-02/H-03/H-04): el servidor recorta `pageSize` a 100 y responde `total`. Estas pruebas
 * fijan que el portal recorre las páginas hasta agotar ese total en vez de quedarse con la primera.
 */
describe('AdminApiService · listados completos', () => {
  let http: HttpTestingController;
  let admin: AdminApiService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    http = TestBed.inject(HttpTestingController);
    admin = TestBed.inject(AdminApiService);
  });

  afterEach(() => http.verify());

  function responder<T>(url: string, page: number, items: T[], total: number) {
    const peticion = http.expectOne((r) => r.url === url && r.params.get('page') === String(page));
    expect(peticion.request.params.get('pageSize')).toBe('100');
    peticion.flush({ items, page, pageSize: 100, total } satisfies PagedResult<T>);
  }

  // H-03: el artefacto de la idea 26 existía; el portal solo miraba las 25 primeras filas.
  it('recorre las páginas de Markdown hasta reunir el total declarado', () => {
    let resultado: PagedResult<ArtefactoMarkdown> | undefined;
    admin.markdownTodo('campania-1').subscribe((p) => (resultado = p));

    responder('/api/admin/markdown', 1, [{ id: 'md_1' } as ArtefactoMarkdown], 3);
    responder('/api/admin/markdown', 2, [{ id: 'md_2' } as ArtefactoMarkdown], 3);
    responder('/api/admin/markdown', 3, [{ id: 'md_3' } as ArtefactoMarkdown], 3);

    expect(resultado?.items.map((a) => a.id)).toEqual(['md_1', 'md_2', 'md_3']);
    expect(resultado?.total).toBe(3);
  });

  // H-02: pedir `pageSize: 500` no traía 500 usuarios; el servidor devolvía 100 sin avisar.
  it('recorre el maestro de usuarios y conserva la campaña u otros filtros en cada página', () => {
    let resultado: PagedResult<UsuarioAdmin> | undefined;
    admin.usuariosTodos({ rol: 'participante' }).subscribe((p) => (resultado = p));

    const primera = http.expectOne(
      (r) => r.url === '/api/admin/usuarios' && r.params.get('page') === '1',
    );
    expect(primera.request.params.get('rol')).toBe('participante');
    primera.flush({
      items: [{ id: 'u_1' } as UsuarioAdmin],
      page: 1,
      pageSize: 100,
      total: 2,
    } satisfies PagedResult<UsuarioAdmin>);

    const segunda = http.expectOne(
      (r) => r.url === '/api/admin/usuarios' && r.params.get('page') === '2',
    );
    expect(segunda.request.params.get('rol')).toBe('participante');
    segunda.flush({
      items: [{ id: 'u_2' } as UsuarioAdmin],
      page: 2,
      pageSize: 100,
      total: 2,
    } satisfies PagedResult<UsuarioAdmin>);

    expect(resultado?.items.map((u) => u.id)).toEqual(['u_1', 'u_2']);
  });

  // P-34 corte 2: con el listado ya barato en el servidor, las ideas también se recorren completas;
  // es lo que vuelve exacto el desglose por estado de una campaña de 1.000 ideas.
  it('recorre el listado de ideas conservando el filtro de estado', () => {
    let resultado: PagedResult<IdeaConsolidada> | undefined;
    admin
      .ideasTodas('campania-1', { estadoResultado: 'madura', area: 'Operaciones' })
      .subscribe((p) => (resultado = p));

    const primera = http.expectOne(
      (r) => r.url === '/api/admin/ideas' && r.params.get('page') === '1',
    );
    expect(primera.request.params.get('estadoResultado')).toBe('madura');
    // P-34 §4.2: los filtros del servidor viajan en cada página; un filtro vacío no viaja.
    expect(primera.request.params.get('area')).toBe('Operaciones');
    expect(primera.request.params.has('q')).toBe(false);
    expect(primera.request.params.get('pageSize')).toBe('100');
    primera.flush({
      items: [{ id: 'idea_1' } as IdeaConsolidada],
      page: 1,
      pageSize: 100,
      total: 2,
    } satisfies PagedResult<IdeaConsolidada>);

    responder<IdeaConsolidada>('/api/admin/ideas', 2, [{ id: 'idea_2' } as IdeaConsolidada], 2);

    expect(resultado?.items.map((i) => i.id)).toEqual(['idea_1', 'idea_2']);
    expect(resultado?.total).toBe(2);
  });

  // §9: un servidor anterior que no informe `total` degrada a una sola página, sin bucle.
  it('se detiene en la primera página cuando el servidor no informa el total', () => {
    let resultado: PagedResult<UsuarioAdmin> | undefined;
    admin.usuariosTodos().subscribe((p) => (resultado = p));

    http
      .expectOne((r) => r.url === '/api/admin/usuarios')
      .flush({ items: [{ id: 'u_1' } as UsuarioAdmin] });

    expect(resultado?.items.map((u) => u.id)).toEqual(['u_1']);
    expect(resultado?.total).toBe(1);
  });

  // Una página vacía cierra el recorrido aunque el total prometa más filas.
  it('no insiste cuando una página llega vacía', () => {
    let resultado: PagedResult<Conversacion> | undefined;
    admin.conversacionesTodas('campania-1').subscribe((p) => (resultado = p));

    responder<Conversacion>('/api/admin/conversaciones', 1, [{ id: 'c_1' } as Conversacion], 9);
    responder<Conversacion>('/api/admin/conversaciones', 2, [], 9);

    expect(resultado?.items.map((c) => c.id)).toEqual(['c_1']);
  });
});
