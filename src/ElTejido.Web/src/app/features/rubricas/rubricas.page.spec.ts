import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { AdminApiService } from '../../core/admin-api.service';
import { PrevalidacionRubrica, Rubrica } from '../../core/api-models';
import { AuthService } from '../../core/auth.service';
import { RubricasPage } from './rubricas.page';

/**
 * DT-RUB-01 corte 3: el editor autoriza la estructura y el servidor compila el Markdown. Estas
 * pruebas fijan que el portal no vuelve a inventar criterios ni escalas y que el preview sale del
 * servidor.
 */
describe('RubricasPage', () => {
  const rubricaActiva: Rubrica = {
    id: 'r_general',
    nombre: 'Rubrica general',
    descripcion: 'Evalua ideas',
    instruccionesGenerales: 'Evalua con evidencia del aporte.',
    contenidoMarkdown: '# Rubrica: Rubrica general',
    hashEstructura: 'sha256:abc',
    integridadEstructural: 'valida',
    escala: { min: 1, max: 4 },
    criterios: [
      {
        id: 'claridad',
        nombre: 'Claridad',
        descripcion: 'Que tan concreta es.',
        peso: 0.3,
        orden: 1,
      },
      {
        id: 'viabilidad',
        nombre: 'Viabilidad',
        descripcion: 'Que tan realizable es.',
        peso: 0.7,
        orden: 2,
      },
    ],
    version: 2,
    estado: 'activa',
    creadoEn: '2026-08-01T00:00:00Z',
    actualizadoEn: '2026-08-01T00:00:00Z',
  };

  const rubricaLegacy: Rubrica = {
    ...rubricaActiva,
    id: 'r_legacy',
    estado: 'borrador',
    integridadEstructural: 'legacy_no_verificada',
  };

  let cuerposEnviados: unknown[];
  let prevalidaciones: unknown[];
  let prevalidacion: PrevalidacionRubrica;

  function crear(rol: 'admin' | 'visor' = 'admin', lista: Rubrica[] = [rubricaActiva]) {
    cuerposEnviados = [];
    prevalidaciones = [];
    prevalidacion = {
      valido: true,
      errores: [],
      contenidoMarkdown: '# Rubrica: Rubrica general\n\n## Criterios\n\n1. **Claridad**',
      hashEstructura: 'sha256:def',
    };

    TestBed.configureTestingModule({
      imports: [RubricasPage],
      providers: [
        {
          provide: AdminApiService,
          useValue: {
            rubricas: () => of({ items: lista }),
            crearRubrica: (body: unknown) => {
              cuerposEnviados.push(body);
              return of(rubricaActiva);
            },
            actualizarRubrica: (_id: string, body: unknown) => {
              cuerposEnviados.push(body);
              return of(rubricaActiva);
            },
            crearVersionRubrica: (_id: string, body: unknown) => {
              cuerposEnviados.push(body);
              return of(rubricaActiva);
            },
            cambiarEstadoRubrica: () => of(rubricaActiva),
            prevalidarRubrica: (body: unknown) => {
              prevalidaciones.push(body);
              return of(prevalidacion);
            },
          },
        },
        {
          provide: AuthService,
          useValue: { isAdmin: () => rol === 'admin' },
        },
      ],
    });

    const fixture = TestBed.createComponent(RubricasPage);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('no arranca con criterio ni escala quemados', () => {
    const fixture = crear();
    const componente = fixture.componentInstance as unknown as {
      criterios: () => unknown[];
      form: { escalaMin: number; escalaMax: number };
    };

    // DT-RUB-01 §13.1: sin "Impacto" ni escala fija en el flujo de guardado.
    expect(componente.criterios()).toEqual([]);
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').not.toContain('Impacto');
  });

  it('permite agregar, editar, reordenar y quitar criterios', () => {
    const fixture = crear();
    const p = fixture.componentInstance as unknown as {
      agregar: () => void;
      quitar: (i: number) => void;
      mover: (i: number, d: number) => void;
      cambiarCampo: (i: number, campo: 'id' | 'nombre' | 'descripcion', v: string) => void;
      criterios: () => Array<{ id: string }>;
    };

    p.agregar();
    p.agregar();
    p.cambiarCampo(0, 'id', 'claridad');
    p.cambiarCampo(1, 'id', 'viabilidad');
    expect(p.criterios().map((c) => c.id)).toEqual(['claridad', 'viabilidad']);

    p.mover(0, 1);
    expect(p.criterios().map((c) => c.id)).toEqual(['viabilidad', 'claridad']);

    p.quitar(0);
    expect(p.criterios().map((c) => c.id)).toEqual(['claridad']);
  });

  it('muestra la suma de pesos y solo la da por valida en 100%', () => {
    const fixture = crear();
    const p = fixture.componentInstance as unknown as {
      agregar: () => void;
      cambiarPeso: (i: number, v: number) => void;
      sumaPesos: () => number;
      sumaValida: () => boolean;
    };

    p.agregar();
    p.agregar();
    p.cambiarPeso(0, 30);
    p.cambiarPeso(1, 50);
    expect(p.sumaPesos()).toBe(80);
    expect(p.sumaValida()).toBe(false);

    p.cambiarPeso(1, 70);
    expect(p.sumaPesos()).toBe(100);
    expect(p.sumaValida()).toBe(true);
  });

  it('el preview lo entrega el servidor y el cuerpo no lleva contenidoMarkdown', () => {
    const fixture = crear();
    const p = fixture.componentInstance as unknown as {
      agregar: () => void;
      cambiarCampo: (i: number, campo: 'id' | 'nombre' | 'descripcion', v: string) => void;
      cambiarPeso: (i: number, v: number) => void;
      previsualizar: () => void;
      preview: () => string;
    };

    p.agregar();
    p.cambiarCampo(0, 'id', 'claridad');
    p.cambiarCampo(0, 'nombre', 'Claridad');
    p.cambiarPeso(0, 100);
    p.previsualizar();

    expect(prevalidaciones).toHaveLength(1);
    const cuerpo = prevalidaciones[0] as Record<string, unknown>;
    // El portal no envia la proyeccion como autoridad ni compila en TypeScript.
    expect(cuerpo).not.toHaveProperty('contenidoMarkdown');
    expect(p.preview()).toContain('## Criterios');
  });

  it('la prevalidacion invalida muestra motivos en lenguaje de administrador y no publica preview', () => {
    const fixture = crear();
    prevalidacion = {
      valido: false,
      errores: [
        { campo: 'criterios.1.id', motivo: 'duplicado' },
        { campo: 'criterios.pesos', motivo: 'suma_invalida' },
      ],
      contenidoMarkdown: '',
      hashEstructura: '',
    };
    const p = fixture.componentInstance as unknown as {
      previsualizar: () => void;
      preview: () => string;
      erroresValidacion: () => Array<{ campo: string; motivo: string }>;
      describirError: (campo: string, motivo: string) => string;
    };

    p.previsualizar();

    expect(p.preview()).toBe('');
    expect(p.erroresValidacion()).toHaveLength(2);
    expect(p.describirError('criterios.1.id', 'duplicado')).toContain('Criterio 2');
    expect(p.describirError('criterios.pesos', 'suma_invalida')).toBe(
      'Los pesos deben sumar 100%.',
    );
  });

  it('guardar envia la estructura canonica con orden explicito y peso en fraccion', () => {
    const fixture = crear();
    const p = fixture.componentInstance as unknown as {
      form: {
        id: string;
        nombre: string;
        descripcion: string;
        escalaMin: number;
        escalaMax: number;
      };
      agregar: () => void;
      cambiarCampo: (i: number, campo: 'id' | 'nombre' | 'descripcion', v: string) => void;
      cambiarPeso: (i: number, v: number) => void;
      guardar: () => void;
    };

    p.form.id = 'r_qa';
    p.form.nombre = 'Rubrica QA';
    p.form.descripcion = 'desc';
    p.form.escalaMin = 1;
    p.form.escalaMax = 7;
    p.agregar();
    p.agregar();
    p.cambiarCampo(0, 'id', 'claridad');
    p.cambiarCampo(1, 'id', 'viabilidad');
    p.cambiarPeso(0, 30);
    p.cambiarPeso(1, 70);

    p.guardar();

    const cuerpo = cuerposEnviados[0] as {
      escala: { min: number; max: number };
      criterios: Array<{ id: string; peso: number; orden: number }>;
      contenidoMarkdown?: string;
    };
    expect(cuerpo.contenidoMarkdown).toBeUndefined();
    expect(cuerpo.escala).toEqual({ min: 1, max: 7 });
    expect(cuerpo.criterios).toEqual([
      { id: 'claridad', nombre: '', descripcion: '', peso: 0.3, orden: 1 },
      { id: 'viabilidad', nombre: '', descripcion: '', peso: 0.7, orden: 2 },
    ]);
  });

  it('editar una version activa clona la estructura en una version nueva, no en sitio', () => {
    const fixture = crear();
    const p = fixture.componentInstance as unknown as {
      editar: (r: Rubrica) => void;
      modo: () => string;
      criterios: () => Array<{ id: string; pesoPorcentaje: number }>;
      guardar: () => void;
    };

    p.editar(rubricaActiva);

    expect(p.modo()).toBe('version');
    // La estructura llega clonada y en porcentaje para el administrador.
    expect(p.criterios()).toEqual([
      {
        id: 'claridad',
        nombre: 'Claridad',
        descripcion: 'Que tan concreta es.',
        pesoPorcentaje: 30,
      },
      {
        id: 'viabilidad',
        nombre: 'Viabilidad',
        descripcion: 'Que tan realizable es.',
        pesoPorcentaje: 70,
      },
    ]);

    p.guardar();

    // Una version nueva nace en borrador: activar es una accion aparte y explicita.
    expect((cuerposEnviados[0] as { estado: string }).estado).toBe('borrador');
  });

  it('una version sin estructura verificada no se puede activar desde el portal', () => {
    const fixture = crear('admin', [rubricaLegacy]);
    const html = fixture.nativeElement as HTMLElement;

    const activar = Array.from(html.querySelectorAll('button')).find(
      (b) => (b.textContent ?? '').trim() === 'Activar',
    );

    expect(activar).toBeTruthy();
    expect(activar!.disabled).toBe(true);
    expect(html.textContent ?? '').toContain('sin verificar');
  });
});
