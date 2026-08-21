import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { AdminApiService } from '../core/admin-api.service';
import { AuthService } from '../core/auth.service';
import { NotificacionesService } from '../core/notificaciones.service';
import { EnviosPage } from './envios/envios.page';
import { UsuariosPage } from './usuarios/usuarios.page';

describe('controles accesibles', () => {
  it('nombra la selección total y cada envío con su participante', () => {
    TestBed.configureTestingModule({
      imports: [EnviosPage],
      providers: [
        {
          provide: AdminApiService,
          useValue: {
            campanias: () => of({ items: [], page: 1, pageSize: 100, total: 0 }),
            usuarios: () =>
              of({
                items: [
                  {
                    id: 'usuario-ana',
                    nombre: 'Ana Pérez',
                    area: 'Producto',
                  },
                  {
                    id: 'usuario-luis',
                    nombre: 'Luis Díaz',
                    area: '',
                  },
                ],
                page: 1,
                pageSize: 100,
                total: 2,
              }),
            envios: () =>
              of([
                {
                  usuarioId: 'usuario-ana',
                  numero: '+571111111111',
                  estadoEnvio: 'pendiente',
                  estadoRespuesta: 'sin_respuesta',
                },
                {
                  usuarioId: 'usuario-luis',
                  numero: '+572222222222',
                  estadoEnvio: 'enviado',
                  estadoRespuesta: 'sin_respuesta',
                },
              ]),
            campania: () => of({ mensajesIniciales: [] }),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'campania-1' }) } },
        },
        { provide: AuthService, useValue: { isAdmin: () => true } },
        {
          provide: NotificacionesService,
          useValue: { error: () => {}, exito: () => {}, info: () => {} },
        },
      ],
    });

    const fixture = TestBed.createComponent(EnviosPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    const etiquetas = Array.from(
      element.querySelectorAll<HTMLInputElement>('input[type="checkbox"]'),
      (control) => control.getAttribute('aria-label'),
    );

    expect(etiquetas).toEqual([
      'Seleccionar todos los envíos visibles',
      'Seleccionar envío de Ana Pérez (Producto)',
      'Seleccionar envío de Luis Díaz',
    ]);
  });

  it('asocia los campos de etiquetas y el CSV con instrucciones legibles', () => {
    TestBed.configureTestingModule({
      imports: [UsuariosPage],
      providers: [
        {
          provide: AdminApiService,
          useValue: {
            usuarios: () => of({ items: [], page: 1, pageSize: 50, total: 0 }),
            tags: () => of({ items: [], page: 1, pageSize: 100, total: 0 }),
            campanias: () => of({ items: [], page: 1, pageSize: 100, total: 0 }),
            nombresSaludoPendientes: () => of({ pendientes: 0 }),
            completarNombresSaludo: () => of({ completados: 0 }),
          },
        },
        { provide: AuthService, useValue: { isAdmin: () => true } },
        {
          provide: NotificacionesService,
          useValue: { error: () => {}, exito: () => {}, info: () => {} },
        },
      ],
    });

    const fixture = TestBed.createComponent(UsuariosPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    for (const [id, texto] of [
      ['tagNombre', 'Nombre de la etiqueta'],
      ['tagTipo', 'Tipo de la etiqueta'],
      ['tagDescripcion', 'Descripción de la etiqueta'],
      // I-08 v2: la plantilla oficial reemplaza a la anterior y acepta .xlsx ademas de .csv.
      ['archivoCarga', 'Archivo de participantes (.xlsx o .csv)'],
    ]) {
      expect(element.querySelector(`label[for="${id}"]`)?.textContent?.trim()).toBe(texto);
    }

    const archivo = element.querySelector<HTMLInputElement>('#archivoCarga');
    expect(archivo?.getAttribute('accept')).toBe('.xlsx,.csv');
    expect(archivo?.getAttribute('aria-describedby')).toBe('instrucciones-carga');
    expect(element.querySelector('#instrucciones-carga')?.textContent).toContain(
      'Antigüedad en la empresa en años',
    );
  });
});
