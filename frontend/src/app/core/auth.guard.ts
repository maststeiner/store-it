import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * Protects routes that require an authenticated user.
 *
 * If the user state is still unknown (undefined), loadMe() is awaited first.
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

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
