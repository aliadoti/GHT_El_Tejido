import { TestBed } from '@angular/core/testing';

import { Campania, ParticipantePreview } from '../../core/api-models';
import { CampaniasListaPanel, ParticipantesCampaniaPanel } from './campanias.panels';

describe('paneles de campanias', () => {
  it('emite la campania que el administrador abre desde el listado', () => {
    const fixture = TestBed.createComponent(CampaniasListaPanel);
    fixture.componentRef.setInput('campanias', [
      { id: 'campania-1', nombre: 'Prueba', estado: 'borrador', objetivo: 'Validar' } as Campania,
    ]);
    fixture.componentRef.setInput('seleccionadaId', null);
    let abierta = '';
    fixture.componentInstance.abrir.subscribe((id) => (abierta = id));

    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.table-button') as HTMLButtonElement).click();

    expect(abierta).toBe('campania-1');
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
    let asociados: readonly string[] = [];
    fixture.componentInstance.asociar.subscribe((ids) => (asociados = ids));

    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.primary-button') as HTMLButtonElement).click();

    expect(asociados).toEqual(['usuario-1']);
  });
});
