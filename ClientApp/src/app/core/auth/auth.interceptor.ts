import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from './auth.store';

const AUTH_PREFIX = '/api/auth/';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const store = inject(AuthStore);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        const isAuthCall = req.url.startsWith(AUTH_PREFIX);
        if (!isAuthCall) {
          store.markAnonymous();
          const current = router.url && router.url !== '/login' ? router.url : '/';
          router.navigate(['/login'], { queryParams: { returnUrl: current } });
        }
      }
      return throwError(() => error);
    }),
  );
};
