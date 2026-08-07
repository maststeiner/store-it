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
    session: { signedInAs: 'Signed in as {{name}}', logout: 'Sign out' },
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

describe('App — signed-in session', () => {
  beforeEach(async () => {
    const userSignal = signal<
      { displayName: string | null; email: string | null } | null | undefined
    >({ displayName: 'Alice Example', email: 'alice@example.com' });

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user: userSignal,
            initCsrf: vi.fn().mockResolvedValue(undefined),
            logout: vi.fn(),
          },
        },
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('en', BASE_TRANSLATIONS);
  });

  it('shows_display_name_and_logout_when_signed_in', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.session-user')?.textContent).toContain('Alice Example');
    expect(element.querySelector('.header-right button[type="button"]')?.textContent?.trim()).toBe(
      'Sign out',
    );
  });
});
