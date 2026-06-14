import { HttpErrorResponse } from '@angular/common/http';

import { ApiError } from './core/api-models';

export function formatApiError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as ApiError | null;
    return body?.error?.message ?? `No se pudo completar la accion (${error.status}).`;
  }

  return 'No se pudo completar la accion.';
}
