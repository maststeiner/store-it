import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * SPEC-008 AC-11: only the operator reaches /admin. Runs after authGuard, so the user is
 * known; a signed-in non-admin is sent to the storage list without a flash of the page.
 * This is comfort — the API's Admin policy is the real gate and answers 403 regardless.
 */
export const adminGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.user() === undefined) {
    await auth.loadMe();
  }

  return auth.user()?.isAdmin === true ? true : router.createUrlTree(['/storages']);
};
