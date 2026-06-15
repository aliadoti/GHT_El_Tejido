import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const method = request.method.toUpperCase();
  const csrfToken = auth.csrfToken();
  const isMutation = !['GET', 'HEAD', 'OPTIONS'].includes(method);

  const securedRequest = request.clone({
    withCredentials: true,
    setHeaders: isMutation && csrfToken ? { 'X-CSRF-Token': csrfToken } : {},
  });

  return next(securedRequest).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        request.url.includes('/api/') &&
        !request.url.includes('/api/auth/me')
      ) {
        auth.clearSession();
        void router.navigateByUrl('/login');
      }

      return throwError(() => error);
    }),
  );
};
