import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

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
  ) {
    TestBed.configureTestingModule({
      imports: [ResultadosPage],
      providers: [
        {
          provide: AdminApiService,
          useValue: {
            campanias: () => of({ items: campanias }),
            usuarios: () =>
              of({
                items: [{ id: 'usuario-1', nombre: 'Ana', area: 'Producto' } as UsuarioAdmin],
              }),
            conversaciones: () => of({ items: [] }),
            respuestas: () => of({ items: [respuesta] }),
            markdown: () => of({ items: [markdown, markdownIdea] }),
            ideas: () => of({ items: [idea, ideaEnCurso] }),
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
          },
        },
        { provide: AuthService, useValue: { isAdmin: () => true } },
        ResultadosSesionService,
      ],
    });
  }

  it('precarga la primera campaña y presenta una fila por idea con su estado', () => {
    configurar();
    const fixture = TestBed.createComponent(ResultadosPage);
    fixture.detectChanges();
    fixture.detectChanges();

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
