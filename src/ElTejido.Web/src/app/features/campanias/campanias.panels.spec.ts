import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Campania, ParticipantePreview } from '../../core/api-models';
import {
  CampaniaConfiguracionPanel,
  CampaniaDetallePanel,
  CampaniasListaPanel,
  ParticipantesCampaniaPanel,
} from './campanias.panels';

describe('paneles de campanias', () => {
  it('emite la campania que el administrador abre desde el listado', () => {
    const fixture = TestBed.createComponent(CampaniasListaPanel);
    fixture.componentRef.setInput('campanias', [
      { id: 'campania-1', nombre: 'Prueba', estado: 'borrador', objetivo: 'Validar' } as Campania,
    ]);
    fixture.componentRef.setInput('seleccionadaId', null);
    fixture.componentRef.setInput('esAdmin', true);
    let abierta = '';
    fixture.componentInstance.abrir.subscribe((id) => (abierta = id));

    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.table-button') as HTMLButtonElement).click();

    expect(abierta).toBe('campania-1');
  });

  it('abre la creacion solo cuando el administrador la solicita', () => {
    const fixture = TestBed.createComponent(CampaniasListaPanel);
    fixture.componentRef.setInput('campanias', []);
    fixture.componentRef.setInput('seleccionadaId', null);
    fixture.componentRef.setInput('esAdmin', true);
    let solicitada = false;
    fixture.componentInstance.nueva.subscribe(() => (solicitada = true));

    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.primary-button') as HTMLButtonElement).click();

    expect(solicitada).toBe(true);
  });

  it('mantiene el conjunto de la vista previa y lo vuelve a seleccionar al refrescarlo', () => {
    const fixture = TestBed.createComponent(ParticipantesCampaniaPanel);
    const preview: ParticipantePreview[] = [
      {
        usuarioId: 'usuario-1',
        nombre: 'Ana',
        area: 'Producto',
        empresa: 'GHT',
        whatsappNormalizado: '+571234567890',
        tags: [],
      },
    ];
    fixture.componentRef.setInput('participantes', []);
    fixture.componentRef.setInput('preview', preview);
    fixture.componentRef.setInput('areas', ['Producto']);
    fixture.componentRef.setInput('empresas', ['GHT']);
    fixture.componentRef.setInput('nombres', new Map([['usuario-1', 'Ana (Producto)']]));
    fixture.componentRef.setInput('esAdmin', true);
    fixture.componentRef.setInput('participantes', []);
    let asociados: readonly string[] = [];
    fixture.componentInstance.asociar.subscribe((ids) => (asociados = ids));

    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.primary-button') as HTMLButtonElement).click();

    expect(asociados).toEqual(['usuario-1']);
  });

  it('relaciona las pestanas con su panel y permite recorrerlas con teclado', () => {
    TestBed.configureTestingModule({
      imports: [CampaniaDetallePanel],
      providers: [provideRouter([])],
    });
    const fixture = TestBed.createComponent(CampaniaDetallePanel);
    fixture.componentRef.setInput('campania', {
      id: 'campania-1',
      nombre: 'Prueba',
      estado: 'borrador',
      objetivo: 'Validar',
      mensajesIniciales: [{ estado: 'activo' }],
      preguntas: [],
    } as unknown as Campania);
    fixture.componentRef.setInput('esAdmin', true);
    fixture.componentRef.setInput('participantes', [{}]);
    fixture.componentRef.setInput('activa', 'config');
    fixture.componentInstance.tabCambiada.subscribe((tab) =>
      fixture.componentRef.setInput('activa', tab),
    );
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const tablist = host.querySelector<HTMLElement>('[role="tablist"]');
    const tabs = Array.from(host.querySelectorAll<HTMLButtonElement>('[role="tab"]'));
    const panel = host.querySelector<HTMLElement>('[role="tabpanel"]');

    expect(tablist?.getAttribute('aria-label')).toBe('Secciones de la campaña');
    expect(tabs.length).toBe(4);
    expect(new Set(tabs.map((tab) => tab.id)).size).toBe(4);
    expect(tabs.map((tab) => tab.getAttribute('aria-selected'))).toEqual([
      'true',
      'false',
      'false',
      'false',
    ]);
    expect(tabs.map((tab) => tab.tabIndex)).toEqual([0, -1, -1, -1]);
    expect(tabs.map((tab) => tab.getAttribute('aria-label'))).toEqual([
      'Paso 1, Configuracion',
      'Paso 2, Mensajes iniciales, completo',
      'Paso 3, Preguntas, pendiente',
      'Paso 4, Participantes, completo',
    ]);
    expect(panel?.id).toBe(tabs[0].getAttribute('aria-controls'));
    expect(panel?.getAttribute('aria-labelledby')).toBe(tabs[0].id);

    tabs[0].focus();
    tabs[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    fixture.detectChanges();
    expect(document.activeElement).toBe(tabs[1]);
    expect(tabs[1].getAttribute('aria-selected')).toBe('true');
    expect(tabs[1].tabIndex).toBe(0);

    tabs[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }));
    fixture.detectChanges();
    expect(document.activeElement).toBe(tabs[3]);
    expect(tabs[3].getAttribute('aria-selected')).toBe('true');

    tabs[3].dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true }));
    fixture.detectChanges();
    expect(document.activeElement).toBe(tabs[0]);
    expect(tabs[0].getAttribute('aria-selected')).toBe('true');

    tabs[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }));
    fixture.detectChanges();
    expect(document.activeElement).toBe(tabs[3]);
    expect(tabs[3].getAttribute('aria-selected')).toBe('true');

    tabs[2].click();
    fixture.detectChanges();
    expect(tabs[2].getAttribute('aria-selected')).toBe('true');
    expect(panel?.id).toBe(tabs[2].getAttribute('aria-controls'));
  });

  it('agrupa la configuracion y explica los valores que requieren contexto', () => {
    const fixture = TestBed.createComponent(CampaniaConfiguracionPanel);
    fixture.componentRef.setInput('campania', {
      id: 'campania-1',
      nombre: 'Prueba',
      estado: 'borrador',
      objetivo: 'Validar',
    } as Campania);
    fixture.componentRef.setInput('rubricas', []);
    fixture.componentRef.setInput('configsLlm', []);
    fixture.componentRef.setInput('prompts', []);
    fixture.componentRef.setInput('esAdmin', true);

    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const leyendas = Array.from(host.querySelectorAll('legend')).map((legend) =>
      legend.textContent?.trim(),
    );

    expect(leyendas).toEqual(['Evaluacion', 'Seguridad y costo', 'Conversacion']);
    expect(
      host.querySelector('[name="editarUmbralCierreAnticipado"]')?.getAttribute('aria-describedby'),
    ).toBe('ayuda-umbral');
    expect(
      host
        .querySelector('[name="editarMinutosInactividadSesion"]')
        ?.getAttribute('aria-describedby'),
    ).toBe('ayuda-inactividad');
    expect(
      host
        .querySelector('[name="editarCoachingSecuencialIdeas"]')
        ?.getAttribute('aria-describedby'),
    ).toBe('ayuda-coaching-ideas');
    expect(
      host.querySelector('[name="editarMinutosCoachingPorIdea"]')?.getAttribute('aria-describedby'),
    ).toBe('ayuda-minutos-coaching');
    expect(
      host.querySelector('[name="editarPresupuestoTokens"]')?.getAttribute('aria-describedby'),
    ).toBe('ayuda-presupuesto');
  });
});
