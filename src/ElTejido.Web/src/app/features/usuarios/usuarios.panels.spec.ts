import { TestBed } from '@angular/core/testing';

import { ReporteCargaMasiva, UsuarioAdmin } from '../../core/api-models';
import {
  CargaMasivaPanel,
  FichaUsuarioPanel,
  SolicitudCargaMasiva,
  describirMotivo,
} from './usuarios.panels';

function reporteConConflicto(): ReporteCargaMasiva {
  return {
    totalFilas: 2,
    creados: 1,
    actualizados: 0,
    reasignados: 0,
    rechazados: 1,
    asociados: 0,
    filas: [
      { fila: 2, resultado: 'creado', usuarioId: 'u_1', motivo: null, codigoUsuario: 1 },
      {
        fila: 3,
        resultado: 'rechazado',
        usuarioId: null,
        motivo: 'conflicto_titular',
        usuarioIdAnterior: 'u_9',
        codigoUsuarioAnterior: 7,
        nombreActual: 'ANA PEREZ',
        nombrePropuesto: 'CARLOS RODRIGUEZ',
      },
    ],
  };
}

function usuario(parcial: Partial<UsuarioAdmin> = {}): UsuarioAdmin {
  return {
    id: 'u_1',
    codigoUsuario: 7,
    codigoUsuarioLegible: 'U-000007',
    nombre: 'ANA PEREZ',
    whatsappNormalizado: '573001112233',
    rol: 'participante',
    estado: 'activo',
    idioma: 'es',
    tags: [],
    propiedadesDinamicas: {},
    creadoEn: '2026-08-01T00:00:00Z',
    actualizadoEn: '2026-08-01T00:00:00Z',
    ...parcial,
  };
}

describe('panel de carga masiva', () => {
  it('muestra actual vs. propuesto solo para las filas en conflicto de titular', () => {
    const fixture = TestBed.createComponent(CargaMasivaPanel);
    fixture.componentRef.setInput('campanias', []);
    fixture.componentRef.setInput('reporte', reporteConConflicto());
    fixture.componentRef.setInput('cargando', false);

    fixture.detectChanges();
    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('ANA PEREZ');
    expect(texto).toContain('CARLOS RODRIGUEZ');
    expect(texto).toContain('U-000007');
    // Una sola fila resoluble, aunque el reporte traiga dos.
    expect(fixture.nativeElement.querySelectorAll('select[id^="accion-"]').length).toBe(1);
  });

  it('no permite reenviar mientras todas las filas queden en omitir', () => {
    const fixture = TestBed.createComponent(CargaMasivaPanel);
    fixture.componentRef.setInput('campanias', []);
    fixture.componentRef.setInput('reporte', reporteConConflicto());
    fixture.componentRef.setInput('cargando', false);
    fixture.detectChanges();

    const botones = Array.from(
      fixture.nativeElement.querySelectorAll('button.primary-button'),
    ) as HTMLButtonElement[];
    const aplicar = botones.find((b) => b.textContent?.includes('Aplicar decisiones'));

    expect(aplicar?.disabled).toBe(true);
  });

  it('envia solo las decisiones distintas de omitir, con el archivo y el modo elegidos', () => {
    const fixture = TestBed.createComponent(CargaMasivaPanel);
    fixture.componentRef.setInput('campanias', []);
    fixture.componentRef.setInput('reporte', reporteConConflicto());
    fixture.componentRef.setInput('cargando', false);
    fixture.detectChanges();

    const panel = fixture.componentInstance as unknown as {
      archivo: { set: (valor: File | null) => void };
      modo: string;
      fijarAccion: (fila: number, accion: string) => void;
      emitirCarga: () => void;
    };
    let emitida: SolicitudCargaMasiva | null = null;
    fixture.componentInstance.cargar.subscribe((s) => (emitida = s));

    panel.archivo.set(new File(['x'], 'roster.xlsx'));
    panel.modo = 'solo_actualizar';
    panel.fijarAccion(3, 'reasignar');
    panel.fijarAccion(4, 'omitir');
    panel.emitirCarga();

    expect(emitida).not.toBeNull();
    expect(emitida!.modo).toBe('solo_actualizar');
    expect(emitida!.resoluciones).toEqual([{ fila: 3, accion: 'reasignar' }]);
  });

  it('no emite carga si no hay archivo seleccionado', () => {
    const fixture = TestBed.createComponent(CargaMasivaPanel);
    fixture.componentRef.setInput('campanias', []);
    fixture.componentRef.setInput('reporte', null);
    fixture.componentRef.setInput('cargando', false);
    fixture.detectChanges();

    let emitida = false;
    fixture.componentInstance.cargar.subscribe(() => (emitida = true));
    (fixture.componentInstance as unknown as { emitirCarga: () => void }).emitirCarga();

    expect(emitida).toBe(false);
  });

  it('pide la plantilla vacia sin pasar por el formulario', () => {
    const fixture = TestBed.createComponent(CargaMasivaPanel);
    fixture.componentRef.setInput('campanias', []);
    fixture.componentRef.setInput('reporte', null);
    fixture.componentRef.setInput('cargando', false);
    fixture.detectChanges();

    let pedida = false;
    fixture.componentInstance.descargarPlantilla.subscribe(() => (pedida = true));
    (fixture.nativeElement.querySelector('button.ghost-button') as HTMLButtonElement).click();

    expect(pedida).toBe(true);
  });

  it('traduce los motivos tipificados a lenguaje del administrador', () => {
    expect(describirMotivo('conflicto_titular')).toBe('El teléfono ya es de otra persona');
    expect(describirMotivo(null)).toBe('—');
    // Un motivo que el portal no conozca se muestra tal cual en vez de perderse.
    expect(describirMotivo('motivo_nuevo')).toBe('motivo_nuevo');
  });
});

describe('ficha de usuario', () => {
  it('lista el historico del numero con su estado', () => {
    const fixture = TestBed.createComponent(FichaUsuarioPanel);
    fixture.componentRef.setInput('usuario', usuario());
    fixture.componentRef.setInput('historico', [
      usuario({
        id: 'u_0',
        codigoUsuarioLegible: 'U-000001',
        nombre: 'TITULAR VIEJO',
        estado: 'inactivo',
      }),
      usuario(),
    ]);
    fixture.componentRef.setInput('esAdmin', true);

    fixture.detectChanges();
    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('TITULAR VIEJO');
    expect(texto).toContain('inactivo');
  });

  it('ofrece reasignar solo al administrador y solo sobre un usuario activo', () => {
    const visor = TestBed.createComponent(FichaUsuarioPanel);
    visor.componentRef.setInput('usuario', usuario());
    visor.componentRef.setInput('historico', []);
    visor.componentRef.setInput('esAdmin', false);
    visor.detectChanges();
    expect(visor.nativeElement.textContent).not.toContain('Reasignar este teléfono');

    const inactivo = TestBed.createComponent(FichaUsuarioPanel);
    inactivo.componentRef.setInput('usuario', usuario({ estado: 'inactivo' }));
    inactivo.componentRef.setInput('historico', []);
    inactivo.componentRef.setInput('esAdmin', true);
    inactivo.detectChanges();
    expect(inactivo.nativeElement.textContent).not.toContain('Reasignar este teléfono');

    const admin = TestBed.createComponent(FichaUsuarioPanel);
    admin.componentRef.setInput('usuario', usuario());
    admin.componentRef.setInput('historico', []);
    admin.componentRef.setInput('esAdmin', true);
    admin.detectChanges();
    expect(admin.nativeElement.textContent).toContain('Reasignar este teléfono');
  });

  it('emite los datos del nuevo titular al confirmar la reasignacion', () => {
    const fixture = TestBed.createComponent(FichaUsuarioPanel);
    fixture.componentRef.setInput('usuario', usuario());
    fixture.componentRef.setInput('historico', []);
    fixture.componentRef.setInput('esAdmin', true);
    fixture.detectChanges();

    let emitido: { nombre: string } | null = null;
    fixture.componentInstance.reasignar.subscribe((f) => (emitido = f));
    const panel = fixture.componentInstance as unknown as {
      formulario: { nombre: string };
      confirmar: () => void;
    };

    // Sin nombre no se emite nada: inactivar a alguien sin saber a quien se le entrega el numero
    // seria peor que fallar (I-08 §4.4).
    panel.confirmar();
    expect(emitido).toBeNull();

    panel.formulario.nombre = 'CARLOS RODRIGUEZ';
    panel.confirmar();

    expect(emitido).not.toBeNull();
    expect(emitido!.nombre).toBe('CARLOS RODRIGUEZ');
  });
});
