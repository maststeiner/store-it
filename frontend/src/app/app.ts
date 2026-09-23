import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AccountService } from './api/services';
import { AuthService } from './core/auth.service';
import { ErrorMessages } from './core/error-messages';
import { LanguageService } from './core/language.service';
import { TranslatePipe, TranslateService } from './core/translate';
import { ConfirmDialog } from './shared/confirm-dialog';
import { SessionMenu } from './shared/session-menu';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    FormsModule,
    TranslatePipe,
    SessionMenu,
    ConfirmDialog,
  ],
  templateUrl: './app.html',
})
export class App implements OnInit {
  protected readonly language = inject(LanguageService);
  protected readonly auth = inject(AuthService);
  private readonly errors = inject(ErrorMessages);
  private readonly accountApi = inject(AccountService);
  private readonly translate = inject(TranslateService);

  /** SPEC-006: the account-deletion confirmation is open. */
  protected readonly deleteAccountOpen = signal(false);
  /** SPEC-006 AC-11: a failed deletion is shown here; the session stays as it is. */
  protected readonly deleteAccountError = signal<string | null>(null);
  /** SPEC-007 AC-22: owned storages that still have members — deleted for everyone (D5). */
  protected readonly ownedSharedStorages = signal(0);
  protected readonly deleteAccountMessage = computed(() => {
    const base = this.translate.instant('auth.deleteAccount.message');
    const shared = this.ownedSharedStorages();
    if (shared === 0) {
      return base;
    }
    const key =
      shared === 1
        ? 'auth.deleteAccount.sharedWarning.one'
        : 'auth.deleteAccount.sharedWarning.other';
    return `${base} ${this.translate.instant(key, { count: shared })}`;
  });

  /**
   * D4: what the user has to type — the e-mail address shown in the menu, or the display
   * name for provider accounts that deliver none (SPEC-003 EC-02 guarantees a display name).
   */
  protected readonly deleteAccountChallenge = computed(() => {
    const user = this.auth.user();
    return user?.email?.trim() || user?.displayName?.trim() || '';
  });
  /** The field label names what is actually asked for — e-mail address or display name. */
  protected readonly deleteAccountChallengeLabelKey = computed(() =>
    this.auth.user()?.email?.trim()
      ? 'auth.deleteAccount.challengeLabel'
      : 'auth.deleteAccount.challengeLabelName',
  );

  /**
   * Startup work belongs in the lifecycle hook, not the constructor: the constructor is for
   * dependency injection, and an async call started there escapes Angular's error handling
   * (Sonar S7059). ngOnInit still runs before the template is rendered, so the language is
   * resolved by first paint.
   */
  ngOnInit(): void {
    this.language.init();
    void this.auth.initCsrf();
  }

  protected openDeleteAccount(): void {
    this.deleteAccountError.set(null);
    this.ownedSharedStorages.set(0);
    this.deleteAccountOpen.set(true);
    // The count arrives asynchronously; the dialog is usable before it, the warning appears
    // as soon as it is known (a failed lookup simply shows no warning).
    this.accountApi.getAccount().subscribe({
      next: (summary) => this.ownedSharedStorages.set(summary.ownedSharedStorages),
      error: () => this.ownedSharedStorages.set(0),
    });
  }

  protected async confirmDeleteAccount(): Promise<void> {
    this.deleteAccountOpen.set(false);
    try {
      await this.auth.deleteAccount();
    } catch (error: unknown) {
      this.deleteAccountError.set(this.errors.messageFor(error));
    }
  }
}
