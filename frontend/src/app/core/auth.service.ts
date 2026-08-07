import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

export interface AuthUser {
  displayName: string | null;
  email: string | null;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly user = signal<AuthUser | null | undefined>(undefined);
  readonly loadError = signal(false);

  /**
   * Fetches the current user from GET /auth/me.
   * - 200: sets user to the returned AuthUser.
   * - 401: sets user to null (not signed in).
   * - Other errors: sets loadError=true; leaves user unchanged (network/5xx must not
   *   turn a signed-in user anonymous).
   */
  async loadMe(): Promise<void> {
    try {
      const user = await firstValueFrom(this.http.get<AuthUser>('/auth/me'));
      this.user.set(user);
    } catch (error: unknown) {
      const status = (error as { status?: number })?.status;
      if (status === 401) {
        this.user.set(null);
      } else {
        this.loadError.set(true);
      }
    }
  }

  /**
   * Initialises the XSRF cookie by hitting GET /auth/csrf once.
   * Called on app init so the cookie exists before any mutation.
   */
  async initCsrf(): Promise<void> {
    try {
      await firstValueFrom(this.http.get('/auth/csrf', { responseType: 'text' }));
    } catch {
      // Best-effort — CSRF init failure is not fatal on load.
    }
  }

  /**
   * Redirects the browser to the OAuth login page for the given provider.
   * The returnUrl is the current pathname so the user returns after sign-in.
   */
  login(provider: 'microsoft' | 'google'): void {
    window.location.assign(
      '/auth/login/' + provider + '?returnUrl=' + encodeURIComponent(location.pathname),
    );
  }

  /**
   * POSTs to /auth/logout, then clears the user signal and redirects to /login.
   * Only clears state and navigates on a successful response — if the server returns
   * an error the session may still be active, so we do NOT clear state or redirect
   * (a stale local clear would desync the UI from the real server session).
   * The error is rethrown so the caller can surface it.
   */
  async logout(): Promise<void> {
    await firstValueFrom(this.http.post('/auth/logout', null, { responseType: 'text' }));
    this.user.set(null);
    void this.router.navigateByUrl('/login');
  }
}
