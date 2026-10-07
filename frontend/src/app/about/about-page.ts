import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';

import { AboutResponse } from '../api/models';
import { AboutService } from '../api/services';
import { BUILD_INFO, DEVELOPMENT_VERSION } from '../core/build-info';
import { ErrorMessages } from '../core/error-messages';
import { TranslatePipe } from '../core/translate';

/** One third-party component as the page shows it (web, API and platform share the shape). */
export interface ThirdPartyEntry {
  name: string;
  version: string | null;
  license: string;
  copyright: string | null;
  url: string | null;
  text: string | null;
}

/** A group of components; `entries === null` means the list is not available in this build. */
interface ComponentGroup {
  key: 'web' | 'api' | 'platform';
  labelKey: string;
  entries: ThirdPartyEntry[] | null;
}

export const REPOSITORY_URL = 'https://github.com/maststeiner/store-it';
export const LICENSE_URL = `${REPOSITORY_URL}/blob/main/LICENSE`;
/** Where the web build writes the notices (scripts/third-party-notices.mjs). */
export const WEB_NOTICES_URL = './assets/third-party-notices.json';

/** The application's own license — the text of /LICENSE, verbatim (AC-07). */
export const MIT_LICENSE_TEXT = `MIT License

Copyright (c) 2026 Marcel Steiner

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.`;

/**
 * D4: components that are neither an npm nor a NuGet package. The .NET runtime version comes
 * from the API (`runtime`); nginx and PostgreSQL are pinned by image tags the page cannot see.
 */
const PLATFORM: readonly Omit<ThirdPartyEntry, 'version'>[] = [
  {
    name: '.NET',
    license: 'MIT',
    copyright: '© Microsoft Corporation',
    url: 'https://dotnet.microsoft.com',
    text: null,
  },
  {
    name: 'nginx',
    license: 'BSD-2-Clause',
    copyright: '© F5, Inc.',
    url: 'https://nginx.org',
    text: null,
  },
  {
    name: 'PostgreSQL',
    license: 'PostgreSQL',
    copyright: '© The PostgreSQL Global Development Group',
    url: 'https://www.postgresql.org',
    text: null,
  },
];

const RELEASE_TAG = /^v\d+\.\d+\.\d+/;
const SHORT_REVISION_LENGTH = 12;

/**
 * SPEC-009 D6: what is running, under which license, built from what. The web part renders
 * from the build alone; the API part and both notices lists arrive asynchronously, and each
 * failure stays local to its section (AC-11) — never a blank page.
 */
@Component({
  selector: 'app-about-page',
  imports: [TranslatePipe],
  templateUrl: './about-page.html',
})
export class AboutPage implements OnInit {
  private readonly aboutApi = inject(AboutService);
  private readonly http = inject(HttpClient);
  private readonly errors = inject(ErrorMessages);

  protected readonly web = inject(BUILD_INFO);
  protected readonly mitLicense = MIT_LICENSE_TEXT;
  protected readonly licenseUrl = LICENSE_URL;

  protected readonly api = signal<AboutResponse | null>(null);
  protected readonly apiError = signal<string | null>(null);
  /** `undefined` = still loading, `null` = not available (development build, EC-01). */
  protected readonly webNotices = signal<ThirdPartyEntry[] | null | undefined>(undefined);

  protected readonly webRevision = computed(() => shortRevision(this.web.revision));
  protected readonly apiRevision = computed(() => shortRevision(this.api()?.revision ?? null));

  /** AC-05: only once both sides are known, and never for two development builds. */
  protected readonly versionMismatch = computed(() => {
    const api = this.api();
    return api !== null && api.version !== this.web.version;
  });

  /** The GitHub release of this build — release builds only. */
  protected readonly releaseUrl = computed(() =>
    RELEASE_TAG.test(this.web.version)
      ? `${REPOSITORY_URL}/releases/tag/${this.web.version}`
      : null,
  );

  protected readonly groups = computed<ComponentGroup[]>(() => {
    const api = this.api();
    const runtimeVersion = api ? runtimeVersionOf(api.runtime) : null;
    return [
      {
        key: 'web',
        labelKey: 'about.thirdParty.web',
        entries: this.webNotices() ?? null,
      },
      {
        key: 'api',
        labelKey: 'about.thirdParty.api',
        entries:
          api && api.components.length > 0
            ? api.components.map((component) => ({
                name: component.name,
                version: component.version,
                license: component.license,
                copyright: component.copyright,
                url: component.url,
                text: component.text,
              }))
            : null,
      },
      {
        key: 'platform',
        labelKey: 'about.thirdParty.platform',
        entries: PLATFORM.map((entry) => ({
          ...entry,
          version: entry.name === '.NET' ? runtimeVersion : null,
        })),
      },
    ];
  });

  ngOnInit(): void {
    this.aboutApi.getAbout().subscribe({
      next: (about) => this.api.set(about),
      error: (error: unknown) => this.apiError.set(this.errors.messageFor(error)),
    });
    this.http.get<ThirdPartyEntry[]>(WEB_NOTICES_URL).subscribe({
      next: (entries) => this.webNotices.set(entries),
      // Missing under `ng serve` (the extraction runs in production builds only): a hint, not an error.
      error: () => this.webNotices.set(null),
    });
  }

  protected isDevelopment(version: string): boolean {
    return version === DEVELOPMENT_VERSION;
  }
}

function shortRevision(revision: string | null): string | null {
  return revision ? revision.slice(0, SHORT_REVISION_LENGTH) : null;
}

/** ".NET 10.0.2" → "10.0.2"; anything unexpected is shown as given. */
function runtimeVersionOf(runtime: string): string {
  const match = /(\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?)/.exec(runtime);
  return match ? match[1] : runtime;
}
