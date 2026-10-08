import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { apiErrorInterceptor } from '../core/api-error.interceptor';
import { BUILD_INFO, BuildInfo } from '../core/build-info';
import { TranslateService } from '../core/translate';
import { AboutPage, LICENSE_URL, WEB_NOTICES_URL } from './about-page';

const TRANSLATIONS = {
  about: {
    title: 'About store-it',
    version: {
      title: 'Version',
      web: 'Web client',
      api: 'API',
      runtime: 'Runtime',
      release: 'Release notes on GitHub',
      loading: 'Loading…',
    },
    versionMismatch: 'Web client and API run different versions.',
    license: {
      title: 'License',
      text: 'store-it is published under the MIT License.',
      link: 'License in the repository',
    },
    thirdParty: {
      title: 'Third-party software',
      intro: 'Built with:',
      web: 'Web client',
      api: 'API',
      platform: 'Platform',
      licenseText: 'Show license text',
    },
    notAvailableInDev: 'Not available in development builds.',
  },
  errors: { generic: 'Something went wrong.' },
};

const ABOUT_URL = '/api/v1/about';

const API_ABOUT = {
  version: 'v0.3.0',
  revision: 'fe65db263c991632c7814ed7dbbcfb8764a95317',
  runtime: '.NET 10.0.2',
  components: [
    {
      name: 'Npgsql',
      version: '10.0.3',
      license: 'PostgreSQL',
      copyright: 'Copyright 2025 © The Npgsql Development Team',
      url: 'https://github.com/npgsql/npgsql',
      text: null,
    },
    {
      name: 'Microsoft.OpenApi',
      version: '2.12.2',
      license: 'MIT',
      copyright: '© Microsoft Corporation. All rights reserved.',
      url: 'https://github.com/Microsoft/OpenAPI.NET',
      text: null,
    },
  ],
};

const WEB_NOTICES = [
  {
    name: '@angular/core',
    version: '22.1.4',
    license: 'MIT',
    copyright: null,
    url: 'https://github.com/angular/angular',
    text: 'The MIT License\n\nCopyright (c) 2010-2026 Google LLC.',
  },
  {
    name: 'rxjs',
    version: '7.8.2',
    license: 'Apache-2.0',
    copyright: null,
    url: 'https://rxjs.dev',
    text: 'Apache License, Version 2.0',
  },
];

const RELEASE_BUILD: BuildInfo = {
  version: 'v0.3.0',
  revision: 'fe65db263c991632c7814ed7dbbcfb8764a95317',
};

describe('AboutPage', () => {
  let http: HttpTestingController;

  async function setup(build: BuildInfo = RELEASE_BUILD) {
    await TestBed.configureTestingModule({
      imports: [AboutPage],
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: BUILD_INFO, useValue: build },
      ],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('en', TRANSLATIONS);
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(AboutPage);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => http.verify());

  function text(el: HTMLElement, selector: string): string {
    return el.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  }

  /** The version and (short) revision of one dd, as separate tokens. */
  function versionTokens(el: HTMLElement, testId: string): string[] {
    return [...el.querySelectorAll(`[data-testid="${testId}"] .mono`)].map(
      (span) => span.textContent?.trim() ?? '',
    );
  }

  it('AC-03 / AC-04: shows the web version from the build and the API version from the endpoint', async () => {
    const fixture = await setup();
    http.expectOne(ABOUT_URL).flush(API_ABOUT);
    http.expectOne(WEB_NOTICES_URL).flush(WEB_NOTICES);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(versionTokens(el, 'web-version')).toEqual(['v0.3.0', 'fe65db263c99']);
    expect(versionTokens(el, 'api-version')).toEqual(['v0.3.0', 'fe65db263c99']);
    expect(el.textContent).toContain('.NET 10.0.2');
    expect(el.querySelector('.about-notice')).toBeNull();
    expect(el.querySelector<HTMLAnchorElement>('a.about-link')?.href).toBe(
      'https://github.com/maststeiner/store-it/releases/tag/v0.3.0',
    );
  });

  it('AC-03 / EC-01: a development build shows dev, no revision and no release link', async () => {
    const fixture = await setup({ version: 'dev', revision: null });
    http
      .expectOne(ABOUT_URL)
      .flush({ ...API_ABOUT, version: 'dev', revision: null, components: [] });
    http.expectOne(WEB_NOTICES_URL).flush(WEB_NOTICES);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(versionTokens(el, 'web-version')).toEqual(['dev']);
    expect(el.querySelector('.about-notice')).toBeNull();
    const links = [...el.querySelectorAll<HTMLAnchorElement>('a.about-link')].map((a) => a.href);
    expect(links).not.toContain(expect.stringContaining('/releases/tag/'));
    // The API shipped no notices → hint for that group only.
    expect(text(el, 'h3[data-group="api"] + p')).toBe('Not available in development builds.');
    expect(el.querySelector('ul[data-group="web"]')).not.toBeNull();
  });

  it('AC-05: flags web and API running different versions', async () => {
    const fixture = await setup();
    http.expectOne(ABOUT_URL).flush({ ...API_ABOUT, version: 'v0.2.0' });
    http.expectOne(WEB_NOTICES_URL).flush(WEB_NOTICES);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(text(el, '.about-notice')).toBe('Web client and API run different versions.');
    expect(el.querySelector('.about-notice')?.tagName).toBe('OUTPUT'); // implicit status role
  });

  it('AC-07: shows the MIT license with the copyright line and a link to the repository', async () => {
    const fixture = await setup();
    http.expectOne(ABOUT_URL).flush(API_ABOUT);
    http.expectOne(WEB_NOTICES_URL).flush(WEB_NOTICES);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const license = el.querySelector('.about-license-text')?.textContent ?? '';
    expect(license.startsWith('MIT License')).toBe(true);
    expect(license).toContain('Copyright (c) 2026 Marcel Steiner');
    expect(license).toContain('THE SOFTWARE IS PROVIDED "AS IS"');
    const links = [...el.querySelectorAll<HTMLAnchorElement>('a.about-link')].map((a) => a.href);
    expect(links).toContain(LICENSE_URL);
  });

  it('AC-10: lists the components in three groups with name, version, license, link and text', async () => {
    const fixture = await setup();
    http.expectOne(ABOUT_URL).flush(API_ABOUT);
    http.expectOne(WEB_NOTICES_URL).flush(WEB_NOTICES);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const groups = [...el.querySelectorAll('h3')].map((h) => h.textContent?.trim());
    expect(groups).toEqual(['Web client', 'API', 'Platform']);

    const names = (group: string) =>
      [...el.querySelectorAll(`ul[data-group="${group}"] .about-component-name`)].map((n) =>
        n.textContent?.trim(),
      );
    expect(names('web')).toEqual(['@angular/core', 'rxjs']);
    expect(names('api')).toEqual(['Npgsql', 'Microsoft.OpenApi']);
    expect(names('platform')).toEqual(['.NET', 'nginx', 'PostgreSQL']);

    const npgsql = [...el.querySelectorAll('ul[data-group="api"] .about-component')].find((li) =>
      li.textContent?.includes('Npgsql'),
    ) as HTMLElement;
    expect(text(npgsql, '.about-component-version')).toBe('10.0.3');
    expect(text(npgsql, '.about-component-license')).toBe('PostgreSQL');
    expect(npgsql.textContent).toContain('The Npgsql Development Team');
    expect(npgsql.querySelector<HTMLAnchorElement>('a')?.href).toBe(
      'https://github.com/npgsql/npgsql',
    );
    expect(npgsql.querySelector('details')).toBeNull();

    // EC-08: license texts are collapsed, not truncated.
    const angular = el.querySelector('ul[data-group="web"] .about-component') as HTMLElement;
    const details = angular.querySelector('details');
    expect(details?.hasAttribute('open')).toBe(false);
    expect(details?.querySelector('pre')?.textContent).toContain('Google LLC');

    // D4: the .NET runtime version comes from the API, nginx and PostgreSQL have none.
    const platform = [...el.querySelectorAll('ul[data-group="platform"] .about-component')];
    expect(text(platform[0] as HTMLElement, '.about-component-version')).toBe('10.0.2');
    expect(platform[1].querySelector('.about-component-version')).toBeNull();
    expect(text(platform[1] as HTMLElement, '.about-component-license')).toBe('BSD-2-Clause');
  });

  it('D4: a runtime description without a version token is shown as given', async () => {
    const fixture = await setup();
    http.expectOne(ABOUT_URL).flush({ ...API_ABOUT, runtime: 'Mono' });
    http.expectOne(WEB_NOTICES_URL).flush(WEB_NOTICES);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const dotnet = el.querySelector('ul[data-group="platform"] .about-component') as HTMLElement;
    expect(text(dotnet, '.about-component-version')).toBe('Mono');
  });

  it('AC-11: keeps the page when the API refuses — error line, web and platform groups stay', async () => {
    const fixture = await setup();
    http.expectOne(ABOUT_URL).flush({}, { status: 500, statusText: 'Server Error' });
    http.expectOne(WEB_NOTICES_URL).flush(WEB_NOTICES);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(versionTokens(el, 'web-version')).toEqual(['v0.3.0', 'fe65db263c99']);
    expect(text(el, '[role="alert"]')).toBe('Something went wrong.');
    expect(el.querySelector('.about-notice')).toBeNull();
    expect(el.querySelector('ul[data-group="web"]')).not.toBeNull();
    expect(text(el, 'h3[data-group="api"] + p')).toBe('Not available in development builds.');
    expect(el.querySelectorAll('ul[data-group="platform"] .about-component')).toHaveLength(3);
  });

  it('AC-11 / EC-01: a missing web notices file shows the hint instead of the web list', async () => {
    const fixture = await setup();
    http.expectOne(ABOUT_URL).flush(API_ABOUT);
    http.expectOne(WEB_NOTICES_URL).flush('', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('ul[data-group="web"]')).toBeNull();
    expect(text(el, 'h3[data-group="web"] + p')).toBe('Not available in development builds.');
    expect(el.querySelector('ul[data-group="api"]')).not.toBeNull();
    expect(el.querySelector('[role="alert"]')).toBeNull();
  });
});
