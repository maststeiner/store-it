import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

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

describe('LoginPage', () => {
  let ctrl: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('en', TRANSLATIONS);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  it('renders both provider buttons with translated labels', async () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    await fixture.whenStable();

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button.btn-provider');
    expect(buttons.length).toBe(2);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Sign in with Microsoft');
    expect(text).toContain('Sign in with Google');
  });

  it('calls AuthService.login with "microsoft" when the Microsoft button is clicked', () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();

    const auth = TestBed.inject(AuthService);
    const loginSpy = vi.spyOn(auth, 'login');
    // login() calls window.location.assign — stub it to avoid navigation errors in jsdom.
    vi.spyOn(window, 'location', 'get').mockReturnValue({
      ...window.location,
      assign: vi.fn(),
      pathname: '/login',
    });

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button.btn-provider');
    (buttons[0] as HTMLButtonElement).click();

    expect(loginSpy).toHaveBeenCalledWith('microsoft');
  });

  it('calls AuthService.login with "google" when the Google button is clicked', () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();

    const auth = TestBed.inject(AuthService);
    const loginSpy = vi.spyOn(auth, 'login');
    vi.spyOn(window, 'location', 'get').mockReturnValue({
      ...window.location,
      assign: vi.fn(),
      pathname: '/login',
    });

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button.btn-provider');
    (buttons[1] as HTMLButtonElement).click();

    expect(loginSpy).toHaveBeenCalledWith('google');
  });
});
