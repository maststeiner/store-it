import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * Protects routes that require an authenticated user.
 *
 * If the user state is still unknown (undefined), loadMe() is awaited first.
 * A URL fragment is never carried in returnUrl (see below).
 * A present user (non-null) passes through; an anonymous user is redirected to /login,
 * carrying the route they wanted as `returnUrl` so sign-in can hand them back to it
 * instead of dropping them on the storage list.
 */
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.user() === undefined) {
    await auth.loadMe();
  }

  if (auth.user()) {
    return true;
  }

  // SPEC-007 EC-10: a URL fragment (the invitation token of /join#<token>) must never travel
  // in a query string — returnUrl goes to the API and into access logs. Park it in this
  // tab's sessionStorage and send the visitor back to the path alone; JoinPage picks it up.
  const target = router.parseUrl(state.url);
  if (target.fragment) {
    stashFragment(target.fragment);
    target.fragment = null;
  }
  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: router.serializeUrl(target) },
  });
};

/** Where a stripped fragment waits across the sign-in round trip (per tab, never sent). */
export const PENDING_FRAGMENT_KEY = 'storeit.pendingFragment';

function stashFragment(fragment: string): void {
  try {
    sessionStorage.setItem(PENDING_FRAGMENT_KEY, fragment);
  } catch {
    // Storage unavailable (private mode, blocked): the visitor has to open the link again.
  }
}

/** Returns and clears the parked fragment, if any. */
export function takePendingFragment(): string | null {
  try {
    const value = sessionStorage.getItem(PENDING_FRAGMENT_KEY);
    sessionStorage.removeItem(PENDING_FRAGMENT_KEY);
    return value;
  } catch {
    return null;
  }
}
