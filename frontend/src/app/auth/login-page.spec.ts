import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';

import { AuthService } from '../core/auth.service';
import { TranslateService } from '../core/translate';
import { LoginPage } from './login-page';

const TRANSLATIONS = {
  auth: {
    login: {
      title: 'Sign in to store-it',
      subtitle: 'Choose your identity provider to continue.',
      microsoft: 'Sign in with Microsoft',
      google: 'Sign in with Google',
    },
  },
};

/**
 * The page reads `returnUrl` from the snapshot, so the route has to be stubbed rather
 * than navigated to — a real navigation would also activate the guard under test elsewhere.
 */
function routeWithQuery(query: Record<string, string> = {}) {
  return {
    provide: ActivatedRoute,
    useValue: { snapshot: { queryParamMap: convertToParamMap(query) } },
  };
}

async function setup(query: Record<string, string> = {}) {
  await TestBed.configureTestingModule({
    imports: [LoginPage],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([]),
      routeWithQuery(query),
    ],
  }).compileComponents();

  TestBed.inject(TranslateService).setTranslation('en', TRANSLATIONS);
  return TestBed.inject(HttpTestingController);
}

/** Stub window.location so login()'s real navigation does not fail jsdom. */
function stubLocation(pathname = '/login') {
  vi.spyOn(window, 'location', 'get').mockReturnValue({
    ...window.location,
    assign: vi.fn(),
    pathname,
  });
}

describe('LoginPage', () => {
  let ctrl: HttpTestingController;

  afterEach(() => ctrl.verify());

  /**
   * The page resolves an unknown session on construction, exactly as authGuard does.
   * An anonymous answer leaves the sign-in card on screen.
   */
  function answerAnonymous() {
    ctrl.expectOne('/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
  }

  it('renders both provider buttons with translated labels', async () => {
    ctrl = await setup();
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    answerAnonymous();
    await fixture.whenStable();

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button.btn-provider');
    expect(buttons).toHaveLength(2);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Sign in with Microsoft');
    expect(text).toContain('Sign in with Google');
  });

  it('signs in with the storage list as the target when no returnUrl is given', async () => {
    ctrl = await setup();
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    answerAnonymous();
    await fixture.whenStable();

    const loginSpy = vi.spyOn(TestBed.inject(AuthService), 'login');
    stubLocation();

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button.btn-provider');
    (buttons[0] as HTMLButtonElement).click();

    expect(loginSpy).toHaveBeenCalledWith('microsoft', '/');
  });

  it('hands the returnUrl from the query string to the provider sign-in', async () => {
    ctrl = await setup({ returnUrl: '/storages/7' });
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    answerAnonymous();
    await fixture.whenStable();

    const loginSpy = vi.spyOn(TestBed.inject(AuthService), 'login');
    stubLocation();

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button.btn-provider');
    (buttons[1] as HTMLButtonElement).click();

    expect(loginSpy).toHaveBeenCalledWith('google', '/storages/7');
  });

  it('never sends the visitor back to the sign-in page', async () => {
    ctrl = await setup({ returnUrl: '/login' });
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    answerAnonymous();
    await fixture.whenStable();

    const loginSpy = vi.spyOn(TestBed.inject(AuthService), 'login');
    stubLocation();

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button.btn-provider');
    (buttons[0] as HTMLButtonElement).click();

    expect(loginSpy).toHaveBeenCalledWith('microsoft', '/');
  });

  it('leaves the page immediately when a session already exists', async () => {
    ctrl = await setup({ returnUrl: '/storages/7' });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    ctrl.expectOne('/auth/me').flush({ displayName: 'Alice', email: 'alice@example.com' });
    // whenStable() does not drain the promise chain behind loadMe(); a macrotask does.
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(navigate).toHaveBeenCalledWith('/storages/7');
  });

  it('does not re-check a session that is already known', async () => {
    ctrl = await setup();
    const auth = TestBed.inject(AuthService);
    auth.user.set({ displayName: 'Bob', email: 'bob@example.com' });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    await fixture.whenStable();

    // No /auth/me: ctrl.verify() in afterEach fails if one was issued.
    expect(navigate).toHaveBeenCalledWith('/');
  });
});
