import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';
import { AuthService } from './core/auth.service';
import { TranslateService } from './core/translate';

const BASE_TRANSLATIONS = {
  nav: { storages: 'My storages' },
  languages: { de: 'DE', en: 'EN', fr: 'FR', it: 'IT' },
  header: { language: 'Language' },
  actions: { cancel: 'Cancel', delete: 'Delete' },
  errors: { generic: 'Something went wrong.' },
  auth: {
    session: {
      menu: 'Account menu — signed in as {{name}}',
      logout: 'Sign out',
      deleteAccount: 'Delete account',
    },
    deleteAccount: {
      title: 'Delete your account?',
      message: 'Everything goes.',
      challengeLabel: 'Type your e-mail address to confirm',
      done: 'Deleted.',
    },
  },
};

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('en', BASE_TRANSLATIONS);
  });

  it('renders the header with logo and navigation', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.logo')?.textContent).toContain('store-it');
    expect(compiled.querySelector('.logo img')?.getAttribute('src')).toContain('logo.svg');
    expect(compiled.querySelector('nav')?.textContent).toContain('My storages');
  });

  it('offers all four languages in the switcher', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const options = (fixture.nativeElement as HTMLElement).querySelectorAll(
      '.header-right button, .header-right option',
    );
    const labels = Array.from(options).map((option) => option.textContent?.trim());
    for (const lang of ['DE', 'EN', 'FR', 'IT']) {
      expect(labels).toContain(lang);
    }
  });
});

describe('App — session menu', () => {
  const logout = vi.fn();
  const deleteAccount = vi.fn();

  async function configure(
    user: { displayName: string | null; email: string | null } | null | undefined,
  ): Promise<void> {
    logout.mockClear();
    deleteAccount.mockReset();
    deleteAccount.mockResolvedValue(undefined);
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user: signal(user),
            initCsrf: vi.fn().mockResolvedValue(undefined),
            logout,
            deleteAccount,
          },
        },
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('en', BASE_TRANSLATIONS);
  }

  it('Header_WhenSignedIn_ShowsTheSessionChip', async () => {
    await configure({ displayName: 'Alice Example', email: 'alice@example.com' });

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('app-session-menu .session-chip')?.textContent?.trim()).toBe('AE');
  });

  it('Header_WhenSignedOut_ShowsNoSessionElement', async () => {
    await configure(null);

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('app-session-menu')).toBeNull();
  });

  it('Header_WhileSessionStillUnknown_ShowsNoSessionElement', async () => {
    await configure(undefined);

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('app-session-menu')).toBeNull();
  });

  it('SignOut_WhenChosenFromTheMenu_LogsTheUserOut', async () => {
    await configure({ displayName: 'Alice Example', email: 'alice@example.com' });

    const fixture = TestBed.createComponent(App);
    const element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);
    fixture.detectChanges();
    await fixture.whenStable();

    (element.querySelector('.session-chip') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    (element.querySelector('[role="menuitem"]') as HTMLButtonElement).click();

    expect(logout).toHaveBeenCalledTimes(1);
    element.remove();
  });

  // SPEC-006 AC-08 – AC-11: menu item → typed confirmation → service call; errors stay visible.
  describe('account deletion', () => {
    async function openConfirmation(): Promise<{
      fixture: ReturnType<typeof TestBed.createComponent<App>>;
      element: HTMLElement;
    }> {
      await configure({ displayName: 'Alice Example', email: 'alice@example.com' });
      const fixture = TestBed.createComponent(App);
      const element = fixture.nativeElement as HTMLElement;
      document.body.appendChild(element);
      fixture.detectChanges();
      await fixture.whenStable();

      (element.querySelector('.session-chip') as HTMLButtonElement).click();
      fixture.detectChanges();
      await fixture.whenStable();
      (element.querySelectorAll('[role="menuitem"]')[1] as HTMLButtonElement).click();
      fixture.detectChanges();
      await fixture.whenStable();
      return { fixture, element };
    }

    async function typeChallenge(
      fixture: ReturnType<typeof TestBed.createComponent<App>>,
      element: HTMLElement,
      value: string,
    ): Promise<void> {
      const input = element.querySelector('#confirm-dialog-challenge') as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
      await fixture.whenStable();
    }

    afterEach(() => {
      document.querySelectorAll('app-root').forEach((node) => node.remove());
    });

    it('DeleteAccount_WhenChosenFromTheMenu_AsksForTheEmailAddress', async () => {
      const { element } = await openConfirmation();

      const dialog = element.querySelector('app-confirm-dialog') as HTMLElement;
      expect(dialog).not.toBeNull();
      expect(dialog.querySelector('#confirm-dialog-title')?.textContent).toContain(
        'Delete your account?',
      );
      expect(
        (dialog.querySelector('#confirm-dialog-challenge') as HTMLInputElement).placeholder,
      ).toBe('alice@example.com');
      expect((dialog.querySelector('.btn-danger') as HTMLButtonElement).disabled).toBe(true);
      expect(deleteAccount).not.toHaveBeenCalled();
    });

    it('DeleteAccount_WhenEmailTypedAndConfirmed_CallsTheService', async () => {
      const { fixture, element } = await openConfirmation();

      await typeChallenge(fixture, element, 'alice@example.com');
      (element.querySelector('app-confirm-dialog .btn-danger') as HTMLButtonElement).click();
      fixture.detectChanges();
      await fixture.whenStable();

      expect(deleteAccount).toHaveBeenCalledTimes(1);
      expect(element.querySelector('app-confirm-dialog')).toBeNull();
      expect(element.querySelector('.header-alert')).toBeNull();
    });

    it('DeleteAccount_WhenCancelled_CallsNothing', async () => {
      const { fixture, element } = await openConfirmation();

      (element.querySelector('app-confirm-dialog .btn-ghost') as HTMLButtonElement).click();
      fixture.detectChanges();
      await fixture.whenStable();

      expect(deleteAccount).not.toHaveBeenCalled();
      expect(element.querySelector('app-confirm-dialog')).toBeNull();
    });

    it('DeleteAccount_WhenTheRequestFails_ShowsTheErrorAndKeepsTheSession', async () => {
      const { fixture, element } = await openConfirmation();
      deleteAccount.mockRejectedValue(new Error('boom'));

      await typeChallenge(fixture, element, 'alice@example.com');
      (element.querySelector('app-confirm-dialog .btn-danger') as HTMLButtonElement).click();
      // The rejection is handled in an async handler; let the microtasks drain before rendering.
      await new Promise((resolve) => setTimeout(resolve));
      fixture.detectChanges();
      await fixture.whenStable();

      expect(element.querySelector('.header-alert')?.textContent).toContain(
        'Something went wrong.',
      );
      expect(element.querySelector('app-session-menu')).not.toBeNull();
    });
  });
});
