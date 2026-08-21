import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AdminApiService } from '../../core/admin-api.service';
import {
  ArtefactoMarkdown,
  Campania,
  DetalleIdea,
  IdeaConsolidada,
  Respuesta,
  UsuarioAdmin,
} from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { ResultadosPage } from './resultados.page';
import { ResultadosSesionService } from './resultados-sesion.service';

describe('ResultadosPage', () => {
  const respuesta = {
    id: 'respuesta-1',
    campaniaId: 'campania-1',
    usuarioId: 'usuario-1',
    texto:
      'Una respuesta extensa para comprobar que la lista maestra conserva un extracto legible.',
    estado: 'evaluada',
    nivelMadurez: 'maduro',
  } as Respuesta;
  const markdown = {
    id: 'markdown-1',
    respuestaRef: respuesta.id,
    contenidoMarkdown: '# Documento de prueba',
  } as ArtefactoMarkdown;
  const idea = {
    id: 'idea-1',
    usuarioId: 'usuario-1',
    participante: {
      usuarioId: 'usuario-1',
      codigoUsuarioLegible: 'U-000042',
      nombre: 'Ana',
      area: 'Producto',
      resuelto: true,
    },
    calificacionTotal: 9,
    texto: 'Idea consolidada y confirmada por el participante.',
    confirmada: true,
    estadoFlujo: 'cerrada',
    estadoResultado: 'madura',
    nivelMadurez: 'maduro',
    estadoCuraduria: 'pendiente',
    motivoCierre: 'umbral',
  } as IdeaConsolidada;
  const ideaEnCurso = {
    id: 'idea-2',
    usuarioId: 'usuario-1',
    texto: 'Idea que todavia espera confirmacion.',
    confirmada: false,
    estadoFlujo: 'pendienteConfirmacion',
    nivelMadurez: 'incubacion',
  } as IdeaConsolidada;
  const markdownIdea = {
    id: 'markdown-idea-1',
    ideaRef: idea.id,
    contenidoMarkdown: '# Documento de la idea',
  } as ArtefactoMarkdown;
  const detalleIdea = {
    idea,
    versionConfirmada: null,
    versionPropuesta: null,
    evaluacion: {
      calificacionTotal: 9,
      temas: ['Impacto'],
      retroalimentacionEnviada: 'Excelente idea',
      explicacion: 'Cubre el objetivo',
      recomendacion: 'cerrar',
    },
    versiones: [
      { id: 'v1', numeroVersion: 1, texto: 'Primera propuesta', estadoConfirmacion: 'descartada' },
      { id: 'v2', numeroVersion: 2, texto: 'Version final', estadoConfirmacion: 'confirmada' },
    ],
    aportes: [{ id: 'ap-1', texto: 'Aporte original', tipoAporte: 'inicial' }],
  } as unknown as DetalleIdea;

  function configurar(
    campanias: Campania[] = [{ id: 'campania-1', nombre: 'Piloto' } as Campania],
    sobreescrituras: Record<string, unknown> = {},
  ) {
    TestBed.configureTestingModule({
      imports: [ResultadosPage],
      providers: [
        {
          provide: AdminApiService,
          useValue: {
            campanias: () => of({ items: campanias }),
            usuariosTodos: () =>
              of({
                items: [{ id: 'usuario-1', nombre: 'Ana', area: 'Producto' } as UsuarioAdmin],
                total: 1,
              }),
            conversacionesTodas: () => of({ items: [], total: 0 }),
            respuestasTodas: () => of({ items: [respuesta], total: 1 }),
            markdownTodo: () => of({ items: [markdown, markdownIdea], total: 2 }),
            ideasTodas: () => of({ items: [idea, ideaEnCurso], total: 2 }),
            conteoIdeasCampania: () => of({ items: [], total: 2 }),
            idea: () => of(detalleIdea),
            respuesta: () =>
              of({
                respuesta,
                evaluacion: {
                  calificacionTotal: 8,
                  temas: ['Impacto'],
                  retroalimentacionEnviada: 'Buen aporte',
                  explicacion: 'Es clara',
                  recomendacion: 'cerrar',
                },
              }),
            markdownDetalle: (_campaniaId: string, id: string) =>
              of(id === markdownIdea.id ? markdownIdea : markdown),
            regenerarMarkdown: () => of(markdown),
            ...sobreescrituras,
          },
        },
        { provide: AuthService, useValue: { isAdmin: () => true } },
        provideRouter([]),
        ResultadosSesionService,
      ],
    });
  }

  /** P-34 §4.3: la tabla es la vista por defecto; el maestro-detalle de P-23 es «vista lectura». */
  function verEnLectura(fixture: { nativeElement: HTMLElement; detectChanges: () => void }) {
    const boton = Array.from(fixture.nativeElement.querySelectorAll('button')).find(
      (candidato) => candidato.textContent?.trim() === 'Vista lectura',
    ) as HTMLButtonElement;
    boton.click();
    fixture.detectChanges();
  }

  it('precarga la primera campaña y presenta una fila por idea con su estado', () => {
    configurar();
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();
    fixture.detectChanges();
    verEnLectura(fixture);

    const element = fixture.nativeElement as HTMLElement;
    expect((fixture.componentInstance as unknown as { campaniaId: string }).campaniaId).toBe(
      'campania-1',
    );
    expect(element.querySelector('.resultados-master-detail')).not.toBeNull();
    expect(element.querySelector('.resultados-leyenda')?.textContent).toContain('Maduras');
    expect(element.textContent).not.toContain('Consultar resultados');
    // I-19 §9.2: una fila por idea, con su estado y las marcas de flujo/curaduría.
    expect(element.querySelectorAll('.resultados-idea').length).toBe(2);
    expect(element.textContent).toContain('Idea consolidada y confirmada por el participante.');
    expect(element.textContent).toContain('pendiente de curaduría');
    expect(element.textContent).toContain('pendiente de confirmación');
    expect(element.textContent).toContain('2 ideas');
  });

  it('abre el detalle de una idea con su evaluación, su historial y su documento', () => {
    configurar();
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    verEnLectura(fixture);
    const ideaBoton = fixture.nativeElement.querySelector('.resultados-idea') as HTMLButtonElement;
    ideaBoton.click();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(ideaBoton.getAttribute('aria-current')).toBe('true');
    expect(element.textContent).toContain('Evaluación de la versión vigente');
    expect(element.textContent).toContain('Excelente idea');
    // El historial conserva aportes y versiones, incluida la propuesta descartada.
    expect(element.textContent).toContain('Aporte original');
    expect(element.textContent).toContain('descartada');
    expect(element.textContent).toContain('# Documento de la idea');
  });

  it('conserva las respuestas sin idea como resultados históricos', () => {
    configurar();
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.resultados-historicos')?.textContent).toContain(
      'resultado histórico',
    );
    expect(element.querySelectorAll('.resultados-respuesta').length).toBe(1);
  });

  it('selecciona una respuesta histórica y abre su evaluación y documento', () => {
    configurar();
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    const respuestaBoton = fixture.nativeElement.querySelector(
      '.resultados-respuesta',
    ) as HTMLButtonElement;
    respuestaBoton.click();
    fixture.detectChanges();

    expect(respuestaBoton.getAttribute('aria-current')).toBe('true');
    expect(fixture.nativeElement.textContent).toContain('Evaluación');
    expect(fixture.nativeElement.textContent).toContain('# Documento de prueba');
  });

  // P-34 H-01 y §9: contra un servidor anterior a P-34 (sin `participante`) el portal conserva el
  // camino previo —maestro descargado— y, si ese maestro falla, lo dice y ofrece reintentar.
  it('avisa cuando no puede cargar los participantes y no muestra el id técnico', () => {
    let intentos = 0;
    const ideaLegacy = { ...idea, participante: undefined } as unknown as IdeaConsolidada;
    configurar(undefined, {
      ideasTodas: () => of({ items: [ideaLegacy], total: 1 }),
      usuariosTodos: () => {
        intentos += 1;
        return intentos === 1
          ? throwError(() => new HttpErrorResponse({ status: 503 }))
          : of({
              items: [{ id: 'usuario-1', nombre: 'Ana', area: 'Producto' } as UsuarioAdmin],
              total: 1,
            });
      },
    });
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    verEnLectura(fixture);
    const element = fixture.nativeElement as HTMLElement;
    const aviso = element.querySelector('.resultados-aviso-usuarios');
    expect(aviso?.textContent).toContain('No se pudo cargar la lista de participantes');
    expect(aviso?.querySelector('p')?.getAttribute('role')).toBe('alert');
    // El id nunca se presenta solo, como si fuera el nombre: queda etiquetado y rastreable.
    expect(element.querySelector('.resultados-idea strong')?.textContent?.trim()).toBe(
      'Participante no identificado · usuario-1',
    );

    const reintentar = Array.from(element.querySelectorAll('button')).find((boton) =>
      boton.textContent?.includes('Reintentar la carga de participantes'),
    ) as HTMLButtonElement;
    reintentar.click();
    fixture.detectChanges();

    expect(intentos).toBe(2);
    expect(element.querySelector('.resultados-aviso-usuarios')).toBeNull();
    expect(element.textContent).toContain('Ana (Producto)');
    expect(element.textContent).not.toContain('Participante no identificado');
  });

  // P-34 H-04: los contadores son los de la campaña, no los del arreglo cargado. Desde el corte 2 el
  // listado se recorre completo, así que el aviso solo aparece si el servidor entrega menos de lo que
  // declara; sigue siendo preferible decirlo a presentar un desglose parcial como si fuera el total.
  it('cuenta las ideas con el total del servidor y advierte cuando falta cargar', () => {
    configurar(undefined, { ideasTodas: () => of({ items: [idea, ideaEnCurso], total: 137 }) });
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    verEnLectura(fixture);
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.resultados-resumen')?.textContent).toContain('137 ideas');
    expect(element.querySelector('.resultados-resumen')?.textContent).toContain(
      'sobre las 2 primeras',
    );
    expect(element.querySelector('.panel-heading .muted')?.textContent).toContain('2 de 137');
  });

  // P-34 §4.1: la identidad la resuelve el servidor; el maestro descargado solo queda de respaldo.
  it('usa el participante embebido y marca al que el servidor no pudo identificar', () => {
    const ideaSinIdentidad = {
      ...ideaEnCurso,
      participante: {
        usuarioId: 'usuario-9',
        codigoUsuarioLegible: 'U-000099',
        nombre: null,
        resuelto: false,
      },
    } as unknown as IdeaConsolidada;
    configurar(undefined, {
      ideasTodas: () => of({ items: [idea, ideaSinIdentidad], total: 2 }),
      // El maestro trae otro nombre a propósito: debe ganar el del servidor.
      usuariosTodos: () =>
        of({ items: [{ id: 'usuario-1', nombre: 'Nombre viejo' } as UsuarioAdmin], total: 1 }),
    });
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    verEnLectura(fixture);
    const filas = fixture.nativeElement.querySelectorAll('.resultados-idea strong');
    expect(filas[0].textContent.trim()).toBe('Ana (Producto)');
    expect(filas[1].textContent.trim()).toBe('Participante no identificado · U-000099');
  });

  // P-34 §4.2: los filtros viajan al servidor, se ven como chips y se desarman de a uno.
  it('envía los filtros al servidor y los muestra como chips removibles', () => {
    const filtrosVistos: Record<string, string>[] = [];
    configurar(undefined, {
      ideasTodas: (_campaniaId: string, filtros: Record<string, string>) => {
        filtrosVistos.push(filtros);
        return of({ items: [], total: 0 });
      },
    });
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    const pagina = fixture.componentInstance as unknown as {
      filtros: Record<string, string>;
      aplicarFiltros: () => void;
      quitarFiltro: (clave: string) => void;
    };
    pagina.filtros = { q: 'riego', area: 'Operaciones', hasta: '2026-08-20' };
    pagina.aplicarFiltros();
    fixture.detectChanges();

    const ultimo = filtrosVistos[filtrosVistos.length - 1];
    expect(ultimo['q']).toBe('riego');
    expect(ultimo['area']).toBe('Operaciones');
    // El día completo entra en el rango: «hasta» no corta en su medianoche.
    expect(ultimo['hasta']).toBe('2026-08-20T23:59:59Z');

    const element = fixture.nativeElement as HTMLElement;
    const chips = Array.from(element.querySelectorAll('.resultados-chip')).map((chip) =>
      chip.textContent?.trim(),
    );
    expect(chips.some((texto) => texto?.includes('Búsqueda: riego'))).toBe(true);
    expect(chips.some((texto) => texto?.includes('Área: Operaciones'))).toBe(true);
    // El vacío nombra el filtro que lo produjo.
    expect(element.textContent).toContain('Ninguna idea coincide con los filtros aplicados');
    expect(element.textContent).toContain('Búsqueda «riego»');

    pagina.quitarFiltro('q');
    fixture.detectChanges();
    expect(filtrosVistos[filtrosVistos.length - 1]['q']).toBeUndefined();
    expect(filtrosVistos[filtrosVistos.length - 1]['area']).toBe('Operaciones');
  });

  // P-34 §4.3: la tabla es la vista por defecto y el orden lo resuelve el servidor.
  it('presenta la tabla por defecto y manda el orden pedido al servidor', () => {
    const ordenes: Record<string, string>[] = [];
    configurar(undefined, {
      ideasTodas: (_campaniaId: string, filtros: Record<string, string>) => {
        ordenes.push(filtros);
        return of({ items: [idea, ideaEnCurso], total: 2 });
      },
    });
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const tabla = element.querySelector('.resultados-tabla') as HTMLTableElement;
    expect(tabla).not.toBeNull();
    expect(tabla.querySelectorAll('tbody tr').length).toBe(2);
    // Encabezado accesible: ordenable, anunciado y sin orden al principio.
    const encabezados = Array.from(tabla.querySelectorAll('thead th'));
    const calificacion = encabezados.find((th) => th.textContent?.includes('Calificación'))!;
    expect(calificacion.getAttribute('aria-sort')).toBe('none');

    (calificacion.querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(ordenes[ordenes.length - 1]['orden']).toBe('calificacion');
    expect(ordenes[ordenes.length - 1]['dir']).toBe('asc');
    expect(
      (fixture.nativeElement as HTMLElement)
        .querySelector('.resultados-tabla thead th[aria-sort="ascending"]')
        ?.textContent?.trim(),
    ).toContain('Calificación');

    // Segundo clic invierte; el tercero vuelve al orden natural en vez de dejarlo pegado.
    const mismoEncabezado = () =>
      Array.from(
        (fixture.nativeElement as HTMLElement).querySelectorAll('.resultados-tabla thead th'),
      ).find((th) => th.textContent?.includes('Calificación'))!;
    (mismoEncabezado().querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(ordenes[ordenes.length - 1]['dir']).toBe('desc');

    (mismoEncabezado().querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(ordenes[ordenes.length - 1]['orden']).toBeUndefined();
    expect(mismoEncabezado().getAttribute('aria-sort')).toBe('none');
  });

  // P-34 §4.3 (H-04): la paginación dice qué se ve, qué dejó el filtro y qué tiene la campaña.
  it('pagina en la tabla y describe el conteo sin insinuar que la página es todo', () => {
    const muchas = Array.from({ length: 30 }, (_, indice) => ({
      ...idea,
      id: `idea-${indice}`,
    })) as IdeaConsolidada[];
    configurar(undefined, {
      ideasTodas: () => of({ items: muchas, total: muchas.length }),
      conteoIdeasCampania: () => of({ items: [], total: 120 }),
    });
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelectorAll('.resultados-tabla tbody tr').length).toBe(25);
    expect(element.querySelector('.panel-heading .muted')?.textContent).toContain(
      'Mostrando 1–25 de 30 filtradas · 120 en la campaña',
    );

    const siguiente = Array.from(element.querySelectorAll('button')).find(
      (boton) => boton.textContent?.trim() === 'Siguiente',
    ) as HTMLButtonElement;
    siguiente.click();
    fixture.detectChanges();

    expect(element.querySelectorAll('.resultados-tabla tbody tr').length).toBe(5);
    expect(element.querySelector('.panel-heading .muted')?.textContent).toContain(
      'Mostrando 26–30 de 30 filtradas',
    );
  });

  // P-34 §4.3: la vista lectura de P-23 se conserva y se recuerda durante la sesión.
  it('conserva el maestro-detalle como vista lectura y recuerda la elección', () => {
    configurar();
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const lectura = Array.from(element.querySelectorAll('button')).find(
      (boton) => boton.textContent?.trim() === 'Vista lectura',
    ) as HTMLButtonElement;
    lectura.click();
    fixture.detectChanges();

    expect(element.querySelector('.resultados-master-detail')).not.toBeNull();
    expect(element.querySelector('.resultados-tabla')).toBeNull();
    expect(element.querySelectorAll('.resultados-idea').length).toBe(2);
    expect(TestBed.inject(ResultadosSesionService).vista).toBe('lectura');
  });

  // P-34 §4.4 (H-05): la ficha muestra la metadata que la API ya devolvía y no se pintaba.
  it('abre la ficha con la metadata y la línea de tiempo de la idea', () => {
    configurar();
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    const fila = fixture.nativeElement.querySelector(
      '.resultados-tabla tbody th button',
    ) as HTMLButtonElement;
    fila.click();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('U-000042');
    expect(element.textContent).toContain('Idea número');
    expect(element.textContent).toContain('Línea de tiempo de la idea');
    expect(element.textContent).toContain('Aporte inicial');
    expect(element.textContent).toContain('Identificador técnico');
    // La fila queda marcada como la actual sin perder la tabla (P-18/P-19).
    expect(element.querySelector('.resultados-tabla tbody tr[aria-current="true"]')).not.toBeNull();
  });

  it('muestra una guía educada cuando no hay campañas que consultar', () => {
    configurar([]);
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();

    const estado = fixture.nativeElement.querySelectorAll(
      'app-estado-accesible p',
    )[1] as HTMLElement;
    expect(estado.getAttribute('role')).toBe('status');
    expect(estado.textContent).toContain('Aún no hay campañas disponibles');
    expect(fixture.nativeElement.textContent).not.toContain('Ingresa campaniaId');
  });
});
