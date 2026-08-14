import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import {
  AdminApiService,
  CatalogoTextos,
  PrevalidacionCatalogoTextos,
  ReadinessCatalogosTextos,
} from '../../core/admin-api.service';
import { CatalogosTextosPage } from './catalogos-textos.page';

/** DT-P32-02 corte 3/3: descargar → editar → prevalidar → confirmar, sin activar nada. */
describe('CatalogosTextosPage', () => {
  const activa = {
    familiaId: 'catalogo_conversacion',
    idioma: 'es',
    version: 1,
    estado: 'activo',
    etag: '"v1"',
    huella: 'aaa',
    mensajes: { acuseContinuar: 'Perfecto, seguimos.' },
    frases: { continuar: ['listo', 'seguimos'] },
  } as unknown as CatalogoTextos;
  const borradorNuevo = {
    ...activa,
    version: 2,
    estado: 'borrador',
    etag: '"v2"',
    huella: 'bbb',
    mensajes: { acuseContinuar: 'Perfecto, sigamos con tu idea.' },
    frases: { continuar: ['listo'] },
  } as unknown as CatalogoTextos;
  const readiness: ReadinessCatalogosTextos = {
    gateHabilitado: false,
    limites: { maxFrasesPorGrupo: 100, maxBytesImportacionJson: 262144 },
    listo: false,
    idiomas: [
      {
        idioma: 'es',
        listo: true,
        tieneActivo: true,
        versionActiva: 1,
        huellaActiva: 'aaa',
        activaValida: true,
        problemasActiva: [],
        tieneBorrador: false,
        totalVersiones: 1,
        semillaBaseDisponible: true,
        legacyValido: true,
        conteosLegacy: { mensajes: 29, gruposFrases: 16, frases: 74 },
        problemasLegacy: [],
        campaniasBloqueadas: [],
      },
      {
        idioma: 'en',
        listo: false,
        tieneActivo: false,
        versionActiva: null,
        huellaActiva: null,
        activaValida: false,
        problemasActiva: [],
        tieneBorrador: false,
        totalVersiones: 0,
        semillaBaseDisponible: true,
        legacyValido: true,
        conteosLegacy: { mensajes: 29, gruposFrases: 16, frases: 74 },
        problemasLegacy: [],
        campaniasBloqueadas: [
          {
            campaniaId: 'c_1',
            nombre: 'Convención 2026',
            estado: 'borrador',
            motivo: 'catalogo_activo_faltante',
          },
        ],
      },
    ],
  };
  const valido: PrevalidacionCatalogoTextos = {
    valido: true,
    familiaId: 'catalogo_conversacion',
    idioma: 'es',
    conteos: { mensajes: 29, gruposFrases: 16, frases: 74 },
    errores: [],
  };

  function crearApi(overrides: Partial<Record<string, unknown>> = {}) {
    return {
      catalogosTextos: vi.fn(() => of([activa])),
      catalogoTextosEfectivo: vi.fn(() => of({ origen: 'emergencia', catalogo: null })),
      readinessCatalogosTextos: vi.fn(() => of(readiness)),
      crearSemillaBaseCatalogoTextos: vi.fn(() => of(borradorNuevo)),
      prevalidarSemillaLegacy: vi.fn(() => of(valido)),
      exportarSemillaLegacy: vi.fn(() => of(new Blob(['{}']))),
      importarSemillaLegacy: vi.fn(() => of(borradorNuevo)),
      prevalidarImportacionCatalogoTextos: vi.fn(() => of(valido)),
      importarCatalogoTextos: vi.fn(() => of(borradorNuevo)),
      exportarCatalogoTextos: vi.fn(() => of(new Blob(['{}']))),
      actualizarCatalogoTextos: vi.fn(() => of(borradorNuevo)),
      activarCatalogoTextos: vi.fn(() => of(activa)),
      ...overrides,
    };
  }

  function configurar(api: ReturnType<typeof crearApi>) {
    TestBed.configureTestingModule({
      imports: [CatalogosTextosPage],
      providers: [{ provide: AdminApiService, useValue: api }],
    });
    const fixture = TestBed.createComponent(CatalogosTextosPage);
    fixture.detectChanges();
    return fixture;
  }

  /** Reemplaza el archivo elegido por uno controlado y dispara el mismo evento del navegador. */
  async function elegirArchivo(
    fixture: ComponentFixture<CatalogosTextosPage>,
    contenido: unknown,
    nombre = 'catalogo.json',
    tamano = 2048,
  ) {
    const input = fixture.nativeElement.querySelector('#archivo-catalogo') as HTMLInputElement;
    const texto = typeof contenido === 'string' ? contenido : JSON.stringify(contenido);
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: [{ name: nombre, size: tamano, text: () => Promise.resolve(texto) }],
    });
    input.dispatchEvent(new Event('change'));
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();
    return input;
  }

  function textoVisible(fixture: ComponentFixture<CatalogosTextosPage>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('da nombre accesible al selector de archivo y describe qué se puede editar', () => {
    const fixture = configurar(crearApi());

    const input = fixture.nativeElement.querySelector('#archivo-catalogo') as HTMLInputElement;
    const etiqueta = fixture.nativeElement.querySelector('label[for="archivo-catalogo"]');
    const ayuda = fixture.nativeElement.querySelector('#ayuda-masiva');

    expect(etiqueta?.textContent).toContain('Archivo JSON');
    expect(input.getAttribute('aria-describedby')).toBe('ayuda-masiva');
    expect(ayuda?.textContent).toContain('mensajes');
    expect(input.getAttribute('accept')).toContain('json');
  });

  it('explica en preparación qué idioma falta y qué campaña queda bloqueada', () => {
    const fixture = configurar(crearApi());

    const texto = textoVisible(fixture);

    expect(texto).toContain('todavía no se usan');
    expect(texto).toContain('inglés');
    expect(texto).toContain('Convención 2026');
  });

  it('separa crear la semilla base de revisar la configuración anterior', () => {
    const api = crearApi();
    const fixture = configurar(api);

    const botones = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    botones.find((boton) => boton.textContent?.includes('Crear semilla base'))?.click();
    botones.find((boton) => boton.textContent?.includes('Revisar configuración'))?.click();
    botones.find((boton) => boton.textContent?.includes('Descargar configuración'))?.click();

    expect(api.crearSemillaBaseCatalogoTextos).toHaveBeenCalledWith('es');
    expect(api.prevalidarSemillaLegacy).toHaveBeenCalledWith('es');
    expect(api.exportarSemillaLegacy).toHaveBeenCalledWith('es');
    expect(api.importarSemillaLegacy).not.toHaveBeenCalled();
  });

  it('muestra los problemas de la configuración anterior sin ofrecer importarla', () => {
    const api = crearApi({
      prevalidarSemillaLegacy: vi.fn(() =>
        of({
          valido: false,
          familiaId: 'catalogo_conversacion',
          idioma: 'es',
          conteos: { mensajes: 29, gruposFrases: 16, frases: 105 },
          errores: [
            { field: 'frases.despertarProactivo', issue: 'debe_tener_entre_1_y_30_elementos' },
          ],
        } as PrevalidacionCatalogoTextos),
      ),
    });
    const fixture = configurar(api);

    const botones = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    botones.find((boton) => boton.textContent?.includes('Revisar configuración'))?.click();
    fixture.detectChanges();

    const texto = textoVisible(fixture);
    expect(texto).toContain('frases.despertarProactivo');
    expect(texto).toContain('fuera del límite');
    expect(texto).not.toContain('Importar configuración anterior como borrador');
  });

  it('revisa el archivo antes de escribir y solo importa cuando el admin confirma', async () => {
    const api = crearApi();
    const fixture = configurar(api);
    const archivo = { formato: 'catalogo-textos/v1', idioma: 'es', mensajes: {}, frases: {} };

    await elegirArchivo(fixture, archivo);

    expect(api.prevalidarImportacionCatalogoTextos).toHaveBeenCalledWith(archivo, 'es');
    expect(api.importarCatalogoTextos).not.toHaveBeenCalled();
    expect(textoVisible(fixture)).toContain('29 mensajes');

    const confirmar = Array.from(fixture.nativeElement.querySelectorAll('button')).find((boton) =>
      (boton as HTMLButtonElement).textContent?.includes('Importar como nuevo borrador'),
    ) as HTMLButtonElement;
    confirmar.click();
    fixture.detectChanges();

    expect(api.importarCatalogoTextos).toHaveBeenCalledWith(archivo, 'es');
    // Importar nunca publica: no se llama a activar.
    expect(api.activarCatalogoTextos).not.toHaveBeenCalled();
  });

  it('deja seleccionado el borrador nuevo y lo compara con la versión activa', async () => {
    const api = crearApi({
      catalogosTextos: vi
        .fn()
        .mockReturnValueOnce(of([activa]))
        .mockReturnValue(of([activa, borradorNuevo])),
    });
    const fixture = configurar(api);
    await elegirArchivo(fixture, { idioma: 'es', mensajes: {}, frases: {} });

    (
      Array.from(fixture.nativeElement.querySelectorAll('button')).find((boton) =>
        (boton as HTMLButtonElement).textContent?.includes('Importar como nuevo borrador'),
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();

    const texto = textoVisible(fixture);
    expect(texto).toContain('Diferencias con la versión activa');
    expect(texto).toContain('acuseContinuar');
    expect(texto).toContain('Perfecto, sigamos con tu idea.');
    // El grupo cambió de dos frases a una.
    expect(texto).toContain('2 frases');
    expect(texto).toContain('1 frases');
  });

  it('cancelar descarta la carga sin escribir nada', async () => {
    const api = crearApi();
    const fixture = configurar(api);
    await elegirArchivo(fixture, { idioma: 'es', mensajes: {}, frases: {} });

    (
      Array.from(fixture.nativeElement.querySelectorAll('button')).find((boton) =>
        (boton as HTMLButtonElement).textContent?.trim().startsWith('Cancelar'),
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();

    expect(api.importarCatalogoTextos).not.toHaveBeenCalled();
    expect(textoVisible(fixture)).not.toContain('Importar como nuevo borrador');
  });

  it('muestra los errores del backend por campo y no importa el archivo', async () => {
    const api = crearApi({
      prevalidarImportacionCatalogoTextos: vi.fn(() =>
        of({
          valido: false,
          familiaId: 'catalogo_conversacion',
          idioma: 'es',
          conteos: { mensajes: 29, gruposFrases: 16, frases: 74 },
          errores: [
            { field: 'mensajes.acuseContinuar', issue: 'vacio' },
            { field: 'mensajes.claveInventada', issue: 'clave_desconocida' },
            { field: 'frases.continuar', issue: 'frase_duplicada' },
          ],
        } as PrevalidacionCatalogoTextos),
      ),
    });
    const fixture = configurar(api);

    await elegirArchivo(fixture, { idioma: 'es', mensajes: {}, frases: {} });

    const texto = textoVisible(fixture);
    expect(texto).toContain('mensajes.acuseContinuar');
    expect(texto).toContain('sin texto');
    expect(texto).toContain('no se pueden inventar claves');
    expect(texto).toContain('frases repetidas');
    expect(texto).not.toContain('Importar como nuevo borrador');
    expect(api.importarCatalogoTextos).not.toHaveBeenCalled();
  });

  it('avisa cuando el archivo es de otro idioma que el seleccionado', async () => {
    const api = crearApi({
      prevalidarImportacionCatalogoTextos: vi.fn(() =>
        of({
          valido: false,
          familiaId: 'catalogo_conversacion',
          idioma: 'en',
          conteos: { mensajes: 29, gruposFrases: 16, frases: 74 },
          errores: [{ field: 'idioma', issue: 'no_coincide_con_seleccion' }],
        } as PrevalidacionCatalogoTextos),
      ),
    });
    const fixture = configurar(api);

    await elegirArchivo(fixture, { idioma: 'en', mensajes: {}, frases: {} });

    expect(textoVisible(fixture)).toContain('no corresponde al idioma');
    expect(api.importarCatalogoTextos).not.toHaveBeenCalled();
  });

  it('permite volver a elegir el mismo archivo corregido', async () => {
    const api = crearApi({
      prevalidarImportacionCatalogoTextos: vi
        .fn()
        .mockReturnValueOnce(
          of({
            valido: false,
            familiaId: 'catalogo_conversacion',
            idioma: 'es',
            conteos: { mensajes: 29, gruposFrases: 16, frases: 74 },
            errores: [{ field: 'mensajes.acuseContinuar', issue: 'vacio' }],
          } as PrevalidacionCatalogoTextos),
        )
        .mockReturnValue(of(valido)),
    });
    const fixture = configurar(api);

    const input = await elegirArchivo(fixture, { idioma: 'es', mensajes: {}, frases: {} });
    expect(input.value).toBe('');

    await elegirArchivo(fixture, { idioma: 'es', mensajes: {}, frases: {} });

    expect(api.prevalidarImportacionCatalogoTextos).toHaveBeenCalledTimes(2);
    expect(textoVisible(fixture)).toContain('Importar como nuevo borrador');
  });

  it('rechaza un archivo que no es JSON sin llamar al servidor', async () => {
    const api = crearApi();
    const fixture = configurar(api);

    await elegirArchivo(fixture, 'esto no es json');

    expect(api.prevalidarImportacionCatalogoTextos).not.toHaveBeenCalled();
    expect(textoVisible(fixture)).toContain('no contiene JSON válido');
  });

  it('rechaza un archivo por encima del máximo configurado sin leerlo', async () => {
    const api = crearApi();
    const fixture = configurar(api);

    await elegirArchivo(fixture, { idioma: 'es' }, 'grande.json', 300000);

    expect(api.prevalidarImportacionCatalogoTextos).not.toHaveBeenCalled();
    expect(textoVisible(fixture)).toContain('pesa más de lo permitido');
  });

  it('conserva la edición individual, la activación y el rollback', () => {
    const api = crearApi({ catalogosTextos: vi.fn(() => of([borradorNuevo])) });
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const fixture = configurar(api);

    const botones = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    botones.find((boton) => boton.textContent?.includes('Guardar borrador'))?.click();
    botones.find((boton) => boton.textContent?.trim() === 'Activar')?.click();
    botones
      .find((boton) => boton.textContent?.includes('Descargar JSON para edición masiva'))
      ?.click();

    expect(api.actualizarCatalogoTextos).toHaveBeenCalled();
    expect(api.activarCatalogoTextos).toHaveBeenCalled();
    expect(api.exportarCatalogoTextos).toHaveBeenCalled();
    vi.restoreAllMocks();
  });

  it('muestra el error del servidor cuando la importación falla', async () => {
    const api = crearApi({
      importarCatalogoTextos: vi.fn(() =>
        throwError(
          () =>
            new HttpErrorResponse({
              status: 400,
              error: { error: { message: 'El catalogo de textos no es valido.' } },
            }),
        ),
      ),
    });
    const fixture = configurar(api);
    await elegirArchivo(fixture, { idioma: 'es', mensajes: {}, frases: {} });

    (
      Array.from(fixture.nativeElement.querySelectorAll('button')).find((boton) =>
        (boton as HTMLButtonElement).textContent?.includes('Importar como nuevo borrador'),
      ) as HTMLButtonElement
    ).click();
    fixture.detectChanges();

    expect(textoVisible(fixture)).toContain('no es valido');
  });
});
