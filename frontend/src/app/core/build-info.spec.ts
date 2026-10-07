import { TestBed } from '@angular/core/testing';

import { BUILD_INFO, readBuildInfo } from './build-info';

// SPEC-009 AC-03 / AC-06: the stamping path's fallback. The defines are injected by the
// production build only (see frontend/Dockerfile); here nothing is defined.
describe('build info', () => {
  it('reports a development build when no version was defined at build time', () => {
    expect(readBuildInfo()).toEqual({ version: 'dev', revision: null });
  });

  it('is available as a root-provided token', () => {
    expect(TestBed.inject(BUILD_INFO)).toEqual({ version: 'dev', revision: null });
  });
});
