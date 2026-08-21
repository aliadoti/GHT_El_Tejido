import { Injectable } from '@angular/core';

/** Vista principal de Resultados: la tabla compara, el maestro-detalle lee (P-34 §4.3). */
export type VistaResultados = 'tabla' | 'lectura';

/** Columnas opcionales de la tabla; participante y idea son fijas y no se ocultan. */
export type ColumnaResultados =
  'area' | 'pregunta' | 'estado' | 'calificacion' | 'creada' | 'actualizada' | 'documento';

export const COLUMNAS_RESULTADOS: { clave: ColumnaResultados; etiqueta: string }[] = [
  { clave: 'area', etiqueta: 'Área' },
  { clave: 'pregunta', etiqueta: 'Pregunta' },
  { clave: 'estado', etiqueta: 'Estado' },
  { clave: 'calificacion', etiqueta: 'Calificación' },
  { clave: 'creada', etiqueta: 'Creada' },
  { clave: 'actualizada', etiqueta: 'Actualizada' },
  { clave: 'documento', etiqueta: 'Documento' },
];

/**
 * Recuerda las preferencias de Resultados mientras la SPA siga abierta; no persiste datos
 * (`01 §11`: sin `localStorage`). Al recargar se vuelve a los valores por defecto.
 */
@Injectable({ providedIn: 'root' })
export class ResultadosSesionService {
  campaniaId = '';
  vista: VistaResultados = 'tabla';
  columnas: ColumnaResultados[] = COLUMNAS_RESULTADOS.map((columna) => columna.clave);
  densidadCompacta = false;
  tamanoPagina = 25;
  agruparPorParticipante = false;
}
