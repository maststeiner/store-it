import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { AuthService } from './auth.service';

@Component({ template: '' })
class DummyComponent {}

describe('AuthService', () => {
  let service: AuthService;
  let ctrl: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', component: DummyComponent }]),
      ],
    });
    service = TestBed.inject(AuthService);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  it('loadMe_sets_user_on_200', async () => {
    const promise = service.loadMe();
    ctrl.expectOne('/auth/me').flush({ displayName: 'Alice', email: 'alice@example.com' });
    await promise;

    expect(service.user()).toEqual({ displayName: 'Alice', email: 'alice@example.com' });
    expect(service.loadError()).toBe(false);
  });

  it('loadMe_sets_null_on_401', async () => {
    const promise = service.loadMe();
    ctrl.expectOne('/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await promise;

    expect(service.user()).toBeNull();
    expect(service.loadError()).toBe(false);
  });

  it('loadMe_sets_loadError_on_500', async () => {
    const promise = service.loadMe();
    ctrl
      .expectOne('/auth/me')
      .flush('Internal Server Error', { status: 500, statusText: 'Internal Server Error' });
    await promise;

    expect(service.user()).toBeUndefined();
    expect(service.loadError()).toBe(true);
  });

  it('loadMe_500_does_not_clear_signed_in_user', async () => {
    // First: sign the user in via a successful loadMe.
    const firstLoad = service.loadMe();
    ctrl.expectOne('/auth/me').flush({ displayName: 'Alice', email: 'alice@example.com' });
    await firstLoad;
    expect(service.user()).toEqual({ displayName: 'Alice', email: 'alice@example.com' });

    // Then: a 500 must leave the signed-in user unchanged.
    const secondLoad = service.loadMe();
    ctrl
      .expectOne('/auth/me')
      .flush('Internal Server Error', { status: 500, statusText: 'Internal Server Error' });
    await secondLoad;

    expect(service.user()).toEqual({ displayName: 'Alice', email: 'alice@example.com' });
    expect(service.loadError()).toBe(true);
  });

  it('logout_clears_and_redirects_even_on_error', async () => {
    // Put the user in a signed-in state first.
    const loadPromise = service.loadMe();
    ctrl.expectOne('/auth/me').flush({ displayName: 'Bob', email: 'bob@example.com' });
    await loadPromise;
    expect(service.user()).not.toBeNull();

    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    const logoutPromise = service.logout();
    ctrl
      .expectOne('/auth/logout')
      .flush('error', { status: 500, statusText: 'Internal Server Error' });
    await logoutPromise;

    expect(service.user()).toBeNull();
    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });
});
