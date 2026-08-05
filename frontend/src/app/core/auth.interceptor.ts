import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

const MUTATING_METHODS = new Set(['POST', 'PUT', 'DELETE', 'PATCH']);

/**
 * Reads the XSRF-TOKEN cookie value (double-submit pattern).
 * Returns null when the cookie is absent.
 */
function readXsrfToken(): string | null {
  const match = document.cookie
    .split(';')
    .map((c) => c.trim())
    .find((c) => c.startsWith('XSRF-TOKEN='));
  return match ? decodeURIComponent(match.split('=').slice(1).join('=')) : null;
}

/**
 * - Attaches `X-XSRF-TOKEN` to mutating requests (POST/PUT/DELETE/PATCH).
 * - On 401 from any URL except `/auth/me`: clears the AuthService user signal
 *   and navigates to /login, then rethrows the error.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // Attach XSRF token for mutating requests.
  let outReq = req;
  if (MUTATING_METHODS.has(req.method)) {
    const token = readXsrfToken();
    if (token) {
      outReq = req.clone({ setHeaders: { 'X-XSRF-TOKEN': token } });
    }
  }

  return next(outReq).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !req.url.includes('/auth/me')
      ) {
        auth.user.set(null);
        void router.navigateByUrl('/login');
      }
      return throwError(() => error);
    }),
  );
};
