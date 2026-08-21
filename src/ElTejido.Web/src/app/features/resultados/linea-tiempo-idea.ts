import { DetalleIdea } from '../../core/api-models';

/** Un momento de la vida de la idea, tal como ocurrió (P-34 §4.4). */
export interface EventoIdea {
  id: string;
  fecha: string;
  tipo: 'aporte' | 'version' | 'confirmacion';
  etiqueta: string;
  texto: string;
}

/**
 * P-34 §4.4: aportes y versiones dejan de ser dos listas separadas y se intercalan en una sola
 * secuencia cronológica —aporte, versión propuesta, confirmación, complemento…—, que es como
 * realmente ocurrió la conversación. Es una función pura: no consulta nada ni depende del DOM.
 *
 * La confirmación se emite como evento propio porque ocurre **después** de generarse la versión, a
 * veces horas más tarde: mostrarla en el instante de la versión contaría mal la historia.
 */
export function construirLineaTiempo(detalle: DetalleIdea): EventoIdea[] {
  const eventos: EventoIdea[] = [];

  for (const aporte of detalle.aportes ?? []) {
    eventos.push({
      id: `aporte-${aporte.id}`,
      fecha: aporte.fecha,
      tipo: 'aporte',
      etiqueta: etiquetaAporte(aporte.tipoAporte),
      texto: aporte.texto,
    });
  }

  for (const version of detalle.versiones ?? []) {
    eventos.push({
      id: `version-${version.id}`,
      fecha: version.generadaEn,
      tipo: 'version',
      etiqueta: `Versión ${version.numeroVersion} · ${version.estadoConfirmacion}`,
      texto: version.texto,
    });

    if (version.confirmadaEn) {
      eventos.push({
        id: `confirmacion-${version.id}`,
        fecha: version.confirmadaEn,
        tipo: 'confirmacion',
        etiqueta: `Confirmada la versión ${version.numeroVersion}`,
        texto: 'El participante confirmó esta versión como su idea.',
      });
    }
  }

  // Orden estable: ante la misma marca de tiempo, primero el aporte que la originó.
  const peso = { aporte: 0, version: 1, confirmacion: 2 };
  return eventos.sort((izquierda, derecha) => {
    const diferencia = Date.parse(izquierda.fecha) - Date.parse(derecha.fecha);
    return diferencia !== 0 ? diferencia : peso[izquierda.tipo] - peso[derecha.tipo];
  });
}

function etiquetaAporte(tipo?: string | null): string {
  switch (tipo) {
    case 'inicial':
      return 'Aporte inicial';
    case 'complemento':
      return 'Complemento';
    case 'correccion':
      return 'Corrección';
    case 'nuevaIdea':
      return 'Idea nueva';
    default:
      return 'Aporte';
  }
}
