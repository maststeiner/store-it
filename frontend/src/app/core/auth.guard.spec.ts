import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, provideRouter } from '@angular/router';

import { AuthService, AuthUser } from './auth.service';
import { authGuard } from './auth.guard';

function fakeRoute(): ActivatedRouteSnapshot {
  return {} as ActivatedRouteSnapshot;
}

function fakeState(): RouterStateSnapshot {
  return { url: '/storages' } as RouterStateSnapshot;
}

describe('authGuard', () => {
  it('allows_when_user_present', async () => {
    const userSignal = signal<AuthUser | null | undefined>({
      displayName: 'Alice',
      email: 'alice@example.com',
    });
    const loadMe = vi.fn().mockResolvedValue(undefined);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', children: [] }]),
        { provide: AuthService, useValue: { user: userSignal, loadMe } },
      ],
    });

    const result = await TestBed.runInInjectionContext(() =>
      authGuard(fakeRoute(), fakeState()),
    );

    expect(result).toBe(true);
    expect(loadMe).not.toHaveBeenCalled();
  });

  it('redirects_to_login_when_anonymous', async () => {
    const userSignal = signal<AuthUser | null | undefined>(null);
    const loadMe = vi.fn().mockResolvedValue(undefined);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', children: [] }]),
        { provide: AuthService, useValue: { user: userSignal, loadMe } },
      ],
    });

    const result = await TestBed.runInInjectionContext(() =>
      authGuard(fakeRoute(), fakeState()),
    );

    const router = TestBed.inject(Router);
    expect(result).toEqual(router.parseUrl('/login'));
  });

  it('calls loadMe when user is undefined then redirects if still anonymous', async () => {
    const userSignal = signal<AuthUser | null | undefined>(undefined);
    const loadMe = vi.fn().mockImplementation(() => {
      userSignal.set(null);
      return Promise.resolve();
    });

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', children: [] }]),
        { provide: AuthService, useValue: { user: userSignal, loadMe } },
      ],
    });

    const result = await TestBed.runInInjectionContext(() =>
      authGuard(fakeRoute(), fakeState()),
    );

    const router = TestBed.inject(Router);
    expect(loadMe).toHaveBeenCalledOnce();
    expect(result).toEqual(router.parseUrl('/login'));
  });

  it('undefined_then_loadMe_populates_user_allows', async () => {
    const userSignal = signal<AuthUser | null | undefined>(undefined);
    const loadMe = vi.fn().mockImplementation(() => {
      userSignal.set({ displayName: 'Bob', email: 'bob@example.com' });
      return Promise.resolve();
    });

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', children: [] }]),
        { provide: AuthService, useValue: { user: userSignal, loadMe } },
      ],
    });

    const result = await TestBed.runInInjectionContext(() =>
      authGuard(fakeRoute(), fakeState()),
    );

    expect(loadMe).toHaveBeenCalledOnce();
    expect(result).toBe(true);
  });
});
