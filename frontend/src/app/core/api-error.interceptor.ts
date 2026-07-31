import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

import { ApiError } from './api-error';

function extractErrorCode(error: HttpErrorResponse): string | null {
  const body: unknown = error.error;
  if (body && typeof body === 'object' && 'errorCode' in body) {
    const code = (body as Record<string, unknown>)['errorCode'];
    if (typeof code === 'string') {
      return code;
    }
  }
  return null;
}

/**
 * Maps any failed HTTP response to a domain {@link ApiError} carrying the status and the
 * ProblemDetails `errorCode`, so callers (and {@link ErrorMessages}) see one error shape.
 */
export const apiErrorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        return throwError(() => new ApiError(error.status, extractErrorCode(error)));
      }
      return throwError(() => error);
    }),
  );
