import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ApiError } from './api-error';
import { apiErrorInterceptor } from './api-error.interceptor';

describe('apiErrorInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  it('maps a ProblemDetails errorCode into ApiError', () => {
    let caught: unknown;
    http.get('/api/v1/storages').subscribe({ error: (e: unknown) => (caught = e) });

    ctrl
      .expectOne('/api/v1/storages')
      .flush({ errorCode: 'storage.name.empty' }, { status: 400, statusText: 'Bad Request' });

    expect(caught).toBeInstanceOf(ApiError);
    expect((caught as ApiError).status).toBe(400);
    expect((caught as ApiError).errorCode).toBe('storage.name.empty');
  });

  it('maps errors without an errorCode into ApiError with a null code', () => {
    let caught: unknown;
    http.get('/api/v1/storages').subscribe({ error: (e: unknown) => (caught = e) });

    ctrl
      .expectOne('/api/v1/storages')
      .flush('boom', { status: 500, statusText: 'Internal Server Error' });

    expect(caught).toBeInstanceOf(ApiError);
    expect((caught as ApiError).errorCode).toBeNull();
  });
});
