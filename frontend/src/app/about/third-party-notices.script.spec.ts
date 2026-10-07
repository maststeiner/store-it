import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { enrich, main, parseLicenseExtraction } from '../../../scripts/third-party-notices.mjs';

// SPEC-009 AC-08 — the post-build generator, against a fixture in the shape Angular's
// `extractLicenses` writes (dashed separators, JSON-encoded license, full text).

const EXTRACTION = `--------------------------------------------------------------------------------
Package: @angular/core
License: "MIT"

The MIT License

Copyright (c) 2010-2026 Google LLC. https://angular.dev/license

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction.

--------------------------------------------------------------------------------
Package: rxjs
License: "Apache-2.0"

                               Apache License
                         Version 2.0, January 2004

--------------------------------------------------------------------------------
Package: tslib
License: "0BSD"

Copyright (c) Microsoft Corporation.
`;

describe('third-party-notices script', () => {
  it('parses one entry per package with its license id and text', () => {
    const entries = parseLicenseExtraction(EXTRACTION);

    expect(entries.map((e) => e.name)).toEqual(['@angular/core', 'rxjs', 'tslib']);
    expect(entries[0].license).toBe('MIT');
    expect(entries[0].text?.startsWith('The MIT License')).toBe(true);
    expect(entries[0].text).toContain('Google LLC');
    expect(entries[1].license).toBe('Apache-2.0');
    expect(entries[1].text).toContain('Version 2.0, January 2004');
    expect(entries[2].license).toBe('0BSD');
  });

  it('accepts an unquoted license id and treats a missing text as null', () => {
    const entries = parseLicenseExtraction('Package: solo\nLicense: ISC\n');

    expect(entries).toEqual([{ name: 'solo', license: 'ISC', text: null }]);
  });

  it('fails when a package carries no license information', () => {
    expect(() => parseLicenseExtraction('Package: mystery\nLicense: ""\n\nsome text')).toThrow(
      /mystery: .*no license/,
    );
  });

  it('adds version and homepage from package.json, sorted by name', () => {
    const packages: Record<string, Record<string, unknown>> = {
      rxjs: { version: '7.8.2', homepage: 'https://rxjs.dev' },
      '@angular/core': {
        version: '22.1.4',
        repository: { type: 'git', url: 'git+https://github.com/angular/angular.git' },
      },
    };

    const notices = enrich(parseLicenseExtraction(EXTRACTION), (name) => packages[name] ?? null);

    expect(notices.map((n) => n.name)).toEqual(['@angular/core', 'rxjs', 'tslib']);
    expect(notices[0]).toMatchObject({
      name: '@angular/core',
      version: '22.1.4',
      license: 'MIT',
      copyright: null,
      url: 'https://github.com/angular/angular',
    });
    expect(notices[1].url).toBe('https://rxjs.dev');
    // Unknown to node_modules (not installed here): still listed, without version or link.
    expect(notices[2]).toMatchObject({ name: 'tslib', version: null, url: null, license: '0BSD' });
  });

  describe('main', () => {
    let dist: string;
    let nodeModules: string;

    beforeEach(() => {
      const root = mkdtempSync(join(tmpdir(), 'notices-'));
      dist = join(root, 'dist');
      nodeModules = join(root, 'node_modules');
      rmSync(root, { recursive: true, force: true });
    });

    afterEach(() => rmSync(join(dist, '..'), { recursive: true, force: true }));

    it('writes browser/assets/third-party-notices.json next to the bundle', () => {
      writeFileSync(join(mkdirp(dist), '3rdpartylicenses.txt'), EXTRACTION);
      writeFileSync(
        join(mkdirp(join(nodeModules, 'rxjs')), 'package.json'),
        JSON.stringify({ version: '7.8.2', homepage: 'https://rxjs.dev' }),
      );

      expect(main(dist, nodeModules)).toBe(0);

      const written = JSON.parse(
        readFileSync(join(dist, 'browser', 'assets', 'third-party-notices.json'), 'utf8'),
      ) as { name: string; version: string | null }[];
      expect(written.map((n) => n.name)).toEqual(['@angular/core', 'rxjs', 'tslib']);
      expect(written.find((n) => n.name === 'rxjs')?.version).toBe('7.8.2');
    });

    it('skips quietly when there is no extraction (development build, EC-01)', () => {
      mkdirp(dist);

      expect(main(dist, nodeModules)).toBe(0);
      expect(() =>
        readFileSync(join(dist, 'browser', 'assets', 'third-party-notices.json'), 'utf8'),
      ).toThrow();
    });
  });
});

function mkdirp(path: string): string {
  mkdirSync(path, { recursive: true });
  return path;
}
