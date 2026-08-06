import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, from, switchMap, throwError } from 'rxjs';

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
 * Attaches the `X-XSRF-TOKEN` header to a request from the current cookie value.
 * Returns the request unchanged when no token cookie is present.
 */
function withXsrfHeader<T>(req: HttpRequest<T>): HttpRequest<T> {
  const token = readXsrfToken();
  return token ? req.clone({ setHeaders: { 'X-XSRF-TOKEN': token } }) : req;
}

/**
 * - Attaches `X-XSRF-TOKEN` to mutating requests (POST/PUT/DELETE/PATCH).
 *   If the XSRF-TOKEN cookie is missing (e.g. the app issued a mutation before the
 *   startup `GET /auth/csrf` completed, or that call failed), it fetches a token first
 *   (awaiting `AuthService.initCsrf()`) and then attaches the freshly-set cookie value —
 *   so an early or post-failure mutation is not silently sent without CSRF protection.
 * - On 401 from any URL except `/auth/me`: clears the AuthService user signal
 *   and navigates to /login, then rethrows the error.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const onError = (error: unknown): Observable<never> => {
    if (
      error instanceof HttpErrorResponse &&
      error.status === 401 &&
      !req.url.includes('/auth/me')
    ) {
      auth.user.set(null);
      void router.navigateByUrl('/login');
    }
    return throwError(() => error);
  };

  const isMutating = MUTATING_METHODS.has(req.method);

  // For a mutation with no token cookie yet, fetch one first, then attach and proceed.
  // Never recurse: the /auth/csrf GET itself is not a mutating request.
  if (isMutating && readXsrfToken() === null) {
    return from(auth.initCsrf()).pipe(
      switchMap(() => next(withXsrfHeader(req)).pipe(catchError(onError))),
    );
  }

  const outReq = isMutating ? withXsrfHeader(req) : req;
  return next(outReq).pipe(catchError(onError));
};
