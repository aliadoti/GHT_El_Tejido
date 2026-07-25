import { Injectable } from '@angular/core';

/** Recuerda la última campaña de Resultados mientras la SPA siga abierta; no persiste datos. */
@Injectable({ providedIn: 'root' })
export class ResultadosSesionService {
  campaniaId = '';
}
