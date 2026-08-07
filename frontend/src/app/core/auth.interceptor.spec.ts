import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let auth: AuthService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => {
    ctrl.verify();
    // Clean up any XSRF cookie set in a test.
    document.cookie = 'XSRF-TOKEN=; Max-Age=0; path=/';
  });

  it('redirects_and_clears_on_401', () => {
    // Start with a signed-in user.
    auth.user.set({ displayName: 'Alice', email: 'alice@example.com' });

    http.get('/api/v1/storages').subscribe({ error: () => undefined });
    ctrl
      .expectOne('/api/v1/storages')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.user()).toBeNull();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('ignores_401_from_auth_me', () => {
    auth.user.set({ displayName: 'Alice', email: 'alice@example.com' });

    http.get('/auth/me').subscribe({ error: () => undefined });
    ctrl.expectOne('/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    // user signal must NOT be touched for /auth/me 401
    expect(auth.user()).toEqual({ displayName: 'Alice', email: 'alice@example.com' });
  });

  it('attaches_xsrf_header_on_post', () => {
    // Plant the XSRF cookie.
    document.cookie = 'XSRF-TOKEN=test-csrf-token; path=/';

    http.post('/api/v1/storages', { name: 'Pantry' }).subscribe({ error: () => undefined });

    const req = ctrl.expectOne('/api/v1/storages');
    expect(req.request.headers.get('X-XSRF-TOKEN')).toBe('test-csrf-token');
    req.flush({ id: 's1', name: 'Pantry' });
  });

  it('fetches_csrf_then_attaches_when_cookie_absent_on_post', async () => {
    // No XSRF-TOKEN cookie yet (simulates a mutation issued before/without CSRF init).
    expect(document.cookie.includes('XSRF-TOKEN=')).toBe(false);

    let completed = false;
    http
      .post('/api/v1/storages', { name: 'Pantry' })
      .subscribe({ next: () => (completed = true), error: () => undefined });

    // The interceptor must first fetch a token via GET /auth/csrf. Flushing it plants
    // the cookie the same way the real endpoint does (JS-readable XSRF-TOKEN cookie).
    const csrfReq = ctrl.expectOne('/auth/csrf');
    expect(csrfReq.request.method).toBe('GET');
    document.cookie = 'XSRF-TOKEN=fetched-token; path=/';
    csrfReq.flush(null);

    // initCsrf resolves a promise; drain the microtask queue so switchMap fires the
    // original POST before we assert on it.
    await Promise.resolve();
    await Promise.resolve();

    // Then the original POST proceeds, now carrying the freshly-fetched token.
    const postReq = ctrl.expectOne('/api/v1/storages');
    expect(postReq.request.headers.get('X-XSRF-TOKEN')).toBe('fetched-token');
    postReq.flush({ id: 's1', name: 'Pantry' });

    expect(completed).toBe(true);
  });

  it('mutation_failedCsrfInit_notSentTokenless', async () => {
    // No XSRF-TOKEN cookie and initCsrf() will fail (simulate /auth/csrf returning an error
    // without setting the cookie). The interceptor must NOT forward the mutation tokenless.
    expect(document.cookie.includes('XSRF-TOKEN=')).toBe(false);

    let caughtError: unknown;
    http
      .post('/api/v1/storages', { name: 'Pantry' })
      .subscribe({ error: (e: unknown) => (caughtError = e) });

    // Flush /auth/csrf with an error — cookie remains absent.
    const csrfReq = ctrl.expectOne('/auth/csrf');
    csrfReq.flush('Service Unavailable', { status: 503, statusText: 'Service Unavailable' });

    // Drain microtasks so the switchMap branch runs.
    await Promise.resolve();
    await Promise.resolve();

    // The mutation must NOT have been sent (no pending request for /api/v1/storages).
    ctrl.expectNone('/api/v1/storages');

    // The observable must have errored (caller receives the failure).
    expect(caughtError).toBeInstanceOf(Error);
    expect((caughtError as Error).message).toContain('CSRF token unavailable');
  });
});
