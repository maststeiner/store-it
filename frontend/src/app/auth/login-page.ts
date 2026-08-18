import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService, appLocalPath } from '../core/auth.service';
import { TranslatePipe } from '../core/translate';

@Component({
  selector: 'app-login-page',
  imports: [TranslatePipe],
  templateUrl: './login-page.html',
})
export class LoginPage {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** The route authGuard bounced the visitor off, and where sign-in sends them back. */
  private readonly returnUrl = appLocalPath(this.route.snapshot.queryParamMap.get('returnUrl'));

  constructor() {
    void this.skipWhenAlreadySignedIn();
  }

  protected signIn(provider: 'microsoft' | 'google'): void {
    this.auth.login(provider, this.returnUrl);
  }

  /**
   * A visitor who already holds a session has no business on the sign-in page: the OIDC
   * round trip would succeed silently at the provider and land them right back here,
   * which reads as a broken sign-in. Resolves an unknown session with the same single
   * /auth/me call authGuard uses, so the two never disagree.
   */
  private async skipWhenAlreadySignedIn(): Promise<void> {
    if (this.auth.user() === undefined) {
      await this.auth.loadMe();
    }
    if (this.auth.user()) {
      await this.router.navigateByUrl(this.returnUrl);
    }
  }
}
