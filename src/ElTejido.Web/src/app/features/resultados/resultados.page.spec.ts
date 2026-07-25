import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { AdminApiService } from '../../core/admin-api.service';
import { ArtefactoMarkdown, Campania, Respuesta, UsuarioAdmin } from '../../core/api-models';
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
            markdown: () => of({ items: [markdown] }),
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
            markdownDetalle: () => of(markdown),
            regenerarMarkdown: () => of(markdown),
          },
        },
        { provide: AuthService, useValue: { isAdmin: () => true } },
        ResultadosSesionService,
      ],
    });
  }

  it('precarga la primera campaña y presenta respuestas en una lista maestra con leyenda', () => {
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
  });

  it('selecciona una respuesta, la marca como actual y abre su evaluación y documento', () => {
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
