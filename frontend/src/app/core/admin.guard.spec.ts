import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';

import { adminGuard } from './admin.guard';
import { AuthService, AuthUser } from './auth.service';

describe('adminGuard', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = { url: '/admin' } as RouterStateSnapshot;

  function setup(user: AuthUser | null | undefined, loadMeResult: AuthUser | null = null) {
    const auth = {
      user: jasmineLikeSignal(user),
      loadMe: vi.fn(async () => {
        auth.user.set(loadMeResult);
      }),
    };
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: auth }] });
    return auth;
  }

  async function run(): Promise<boolean | UrlTree> {
    return (await TestBed.runInInjectionContext(() => adminGuard(route, state))) as
      boolean | UrlTree;
  }

  it('lets the operator through', async () => {
    setup({ displayName: 'Olga', email: 'operator@test.local', isAdmin: true });

    expect(await run()).toBe(true);
  });

  it('redirects a signed-in non-admin to the storage list', async () => {
    setup({ displayName: 'Max', email: 'max@test.local', isAdmin: false });

    const result = await run();

    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/storages');
  });

  it('loads the session first when it is still unknown', async () => {
    const auth = setup(undefined, {
      displayName: 'Olga',
      email: 'operator@test.local',
      isAdmin: true,
    });

    expect(await run()).toBe(true);
    expect(auth.loadMe).toHaveBeenCalledTimes(1);
  });
});

/** A minimal writable signal stand-in with the call/set surface the guard uses. */
function jasmineLikeSignal<T>(initial: T): { (): T; set(value: T): void } {
  let value = initial;
  const fn = (() => value) as { (): T; set(value: T): void };
  fn.set = (next: T) => {
    value = next;
  };
  return fn;
}
