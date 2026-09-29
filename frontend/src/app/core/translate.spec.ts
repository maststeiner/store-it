import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { TranslateService } from './translate';

/**
 * Lazy dictionary loading (#184): the URL carries a per-app-start stamp so a browser cache
 * filled before nginx sent `Cache-Control: no-cache` is bypassed.
 */
describe('TranslateService loading', () => {
  let service: TranslateService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TranslateService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function expectDictionaryRequest(language: string) {
    return http.expectOne(
      (request) =>
        request.method === 'GET' &&
        new RegExp(`^\\./assets/i18n/${language}\\.json\\?v=\\d+$`).test(request.urlWithParams),
    );
  }

  it('use_LoadsTheDictionaryOnceWithACacheBustingStampAndResolvesKeys', () => {
    service.use('de');
    expect(service.instant('items.new')).toBe('items.new'); // not loaded yet → the key

    expectDictionaryRequest('de').flush({ items: { new: '+ Neuer Artikel' } });

    expect(service.lang()).toBe('de');
    expect(service.instant('items.new')).toBe('+ Neuer Artikel');

    // Switching back and forth does not fetch a loaded dictionary again.
    service.use('en');
    expectDictionaryRequest('en').flush({});
    service.use('de');
    http.expectNone((request) => request.url.includes('/assets/i18n/de.json'));
  });

  it('use_WhenTheDictionaryFailsToLoad_KeepsTheKeyAndAllowsARetry', () => {
    service.use('fr');
    expectDictionaryRequest('fr').flush('nope', {
      status: 500,
      statusText: 'Internal Server Error',
    });

    expect(service.instant('items.new')).toBe('items.new');

    // The failed language is no longer pending: the next use() tries again.
    service.use('fr');
    expectDictionaryRequest('fr').flush({ items: { new: '+ Nouvel article' } });
    expect(service.instant('items.new')).toBe('+ Nouvel article');
  });
});
