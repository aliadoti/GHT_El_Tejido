import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return auth.me().pipe(map((ok) => (ok ? true : router.createUrlTree(['/login']))));
};

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return auth.isAdmin() ? true : router.createUrlTree(['/']);
  }

  return auth
    .me()
    .pipe(map((ok) => (ok && auth.isAdmin() ? true : router.createUrlTree(['/login']))));
};

export const loginRedirectGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return router.createUrlTree(['/']);
  }

  return auth.me().pipe(map((ok) => (ok ? router.createUrlTree(['/']) : true)));
};
