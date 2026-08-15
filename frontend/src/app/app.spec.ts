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
  auth: {
    session: { menu: 'Account menu — signed in as {{name}}', logout: 'Sign out' },
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

  async function configure(
    user: { displayName: string | null; email: string | null } | null | undefined,
  ): Promise<void> {
    logout.mockClear();
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
});
