/** Domain error surfaced to callers for any failed API call (see apiErrorInterceptor). */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly errorCode: string | null,
  ) {
    super(errorCode ?? `HTTP ${status}`);
    this.name = 'ApiError';
  }
}
