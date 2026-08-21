import { DetalleIdea } from '../../core/api-models';
import { construirLineaTiempo } from './linea-tiempo-idea';

/**
 * P-34 §4.4: aportes y versiones se intercalan en una sola secuencia cronológica. Es una función
 * pura, así que se prueba sin componente ni API.
 */
describe('construirLineaTiempo', () => {
  const detalle = {
    aportes: [
      {
        id: 'ap-2',
        texto: 'Y además regar de noche',
        fecha: '2026-08-02T10:00:00Z',
        tipoAporte: 'complemento',
      },
      {
        id: 'ap-1',
        texto: 'Riego por goteo',
        fecha: '2026-08-01T09:00:00Z',
        tipoAporte: 'inicial',
      },
    ],
    versiones: [
      {
        id: 'v1',
        numeroVersion: 1,
        texto: 'Primera consolidación',
        estadoConfirmacion: 'descartada',
        generadaEn: '2026-08-01T09:05:00Z',
        confirmadaEn: null,
      },
      {
        id: 'v2',
        numeroVersion: 2,
        texto: 'Consolidación final',
        estadoConfirmacion: 'confirmada',
        generadaEn: '2026-08-02T10:05:00Z',
        confirmadaEn: '2026-08-03T08:00:00Z',
      },
    ],
  } as unknown as DetalleIdea;

  it('intercala aportes, versiones y confirmaciones en orden cronológico', () => {
    const eventos = construirLineaTiempo(detalle);

    expect(eventos.map((evento) => evento.id)).toEqual([
      'aporte-ap-1',
      'version-v1',
      'aporte-ap-2',
      'version-v2',
      'confirmacion-v2',
    ]);
    expect(eventos[0].etiqueta).toBe('Aporte inicial');
    expect(eventos[3].etiqueta).toBe('Versión 2 · confirmada');
  });

  // La confirmación ocurre después de generarse la versión, a veces al día siguiente: es su propio
  // momento y no puede contarse como parte del instante en que el modelo consolidó.
  it('separa la confirmación del momento en que se generó la versión', () => {
    const eventos = construirLineaTiempo(detalle);
    const confirmacion = eventos.find((evento) => evento.tipo === 'confirmacion');

    expect(confirmacion?.fecha).toBe('2026-08-03T08:00:00Z');
    expect(eventos.filter((evento) => evento.tipo === 'confirmacion')).toHaveLength(1);
  });

  it('devuelve una secuencia vacía cuando la idea aún no tiene historia', () => {
    expect(construirLineaTiempo({ aportes: [], versiones: [] } as unknown as DetalleIdea)).toEqual(
      [],
    );
  });
});
