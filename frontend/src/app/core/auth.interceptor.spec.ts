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
});
