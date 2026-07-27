import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';
import { TranslateService } from './core/translate';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('en', {
      nav: { storages: 'My storages' },
      languages: { de: 'DE', en: 'EN', fr: 'FR', it: 'IT' },
    });
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
