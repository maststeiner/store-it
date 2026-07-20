import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ApiError, StorageApi } from './storage-api';

describe('StorageApi', () => {
  let api: StorageApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(StorageApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('maps ProblemDetails errorCode into ApiError', () => {
    let caught: unknown;
    api.createStorage('').subscribe({ error: (error: unknown) => (caught = error) });

    http
      .expectOne('/api/v1/storages')
      .flush(
        { title: 'storage.name.empty', errorCode: 'storage.name.empty' },
        { status: 400, statusText: 'Bad Request' },
      );

    expect(caught).toBeInstanceOf(ApiError);
    expect((caught as ApiError).status).toBe(400);
    expect((caught as ApiError).errorCode).toBe('storage.name.empty');
  });

  it('maps errors without errorCode to ApiError with null code', () => {
    let caught: unknown;
    api.getStorages().subscribe({ error: (error: unknown) => (caught = error) });

    http
      .expectOne('/api/v1/storages')
      .flush('boom', { status: 500, statusText: 'Internal Server Error' });

    expect(caught).toBeInstanceOf(ApiError);
    expect((caught as ApiError).errorCode).toBeNull();
  });
});
