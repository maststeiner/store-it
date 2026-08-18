import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { AuthService, appLocalPath } from './auth.service';

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

  it('logout_Success_ClearsUserAndRedirectsToLogin', async () => {
    // Put the user in a signed-in state first.
    const loadPromise = service.loadMe();
    ctrl.expectOne('/auth/me').flush({ displayName: 'Bob', email: 'bob@example.com' });
    await loadPromise;
    expect(service.user()).not.toBeNull();

    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    const logoutPromise = service.logout();
    ctrl.expectOne('/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await logoutPromise;

    expect(service.user()).toBeNull();
    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });

  it('logout_ServerError_KeepsSessionAndSurfacesError', async () => {
    // Put the user in a signed-in state first.
    const loadPromise = service.loadMe();
    ctrl.expectOne('/auth/me').flush({ displayName: 'Bob', email: 'bob@example.com' });
    await loadPromise;
    expect(service.user()).not.toBeNull();

    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    let caughtError: unknown;
    const logoutPromise = service.logout().catch((e: unknown) => (caughtError = e));
    ctrl
      .expectOne('/auth/logout')
      .flush('error', { status: 500, statusText: 'Internal Server Error' });
    await logoutPromise;

    // Session must NOT be cleared — the server session may still be active.
    expect(service.user()).not.toBeNull();
    // Must NOT redirect — the user is still signed in server-side.
    expect(navigateSpy).not.toHaveBeenCalled();
    // The error must be surfaced to the caller.
    expect(caughtError).toBeDefined();
  });
  describe('login', () => {
    /**
     * The challenge is a full-page navigation, so the target is asserted through the URL
     * handed to location.assign rather than through the router.
     */
    function captureChallengeUrl(): { url: () => string } {
      const assign = vi.fn();
      vi.spyOn(window, 'location', 'get').mockReturnValue({
        ...window.location,
        assign,
        pathname: '/login',
      });
      return { url: () => String(assign.mock.calls[0]?.[0]) };
    }

    it('sends the caller-supplied route as returnUrl', () => {
      const captured = captureChallengeUrl();

      service.login('microsoft', '/storages/7');

      expect(captured.url()).toBe('/auth/login/microsoft?returnUrl=%2Fstorages%2F7');
    });

    it('falls back to the storage list when no route is supplied', () => {
      const captured = captureChallengeUrl();

      service.login('google');

      expect(captured.url()).toBe('/auth/login/google?returnUrl=%2F');
    });

    it('refuses to return to the sign-in page', () => {
      const captured = captureChallengeUrl();

      service.login('microsoft', '/login');

      expect(captured.url()).toBe('/auth/login/microsoft?returnUrl=%2F');
    });
  });

  describe('appLocalPath', () => {
    it.each([
      ['/storages', '/storages'],
      ['/storages/7?tab=items', '/storages/7?tab=items'],
      ['/login', '/'],
      ['/login?returnUrl=%2Fstorages', '/'],
      ['/login/extra', '/'],
      ['//evil.example.com', '/'],
      ['/\\evil.example.com', '/'],
      ['https://evil.example.com', '/'],
      ['storages', '/'],
      ['', '/'],
    ])('maps %s to %s', (candidate, expected) => {
      expect(appLocalPath(candidate)).toBe(expected);
    });

    it('maps absent values to the storage list', () => {
      expect(appLocalPath(null)).toBe('/');
      expect(appLocalPath(undefined)).toBe('/');
    });
  });
});
