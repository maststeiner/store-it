import { Injectable, inject } from '@angular/core';

import { ApiError } from './storage-api';
import { TranslateService } from './translate';

const GENERIC_KEY = 'errors.generic';

/** Maps API errors (ProblemDetails errorCode) to translated user-facing messages. */
@Injectable({ providedIn: 'root' })
export class ErrorMessages {
  private readonly translate = inject(TranslateService);

  messageFor(error: unknown): string {
    if (error instanceof ApiError && error.errorCode) {
      const key = `errors.${error.errorCode}`;
      const message = this.translate.instant(key);
      if (message !== key) {
        return message;
      }
    }
    return this.translate.instant(GENERIC_KEY);
  }
}
