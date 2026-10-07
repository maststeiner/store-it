import { InjectionToken } from '@angular/core';

/**
 * SPEC-009 D1: the release tag and commit the web bundle was built from. Both are injected by
 * the production build (`ng build --define STOREIT_VERSION='…' --define STOREIT_REVISION='…'`,
 * see frontend/Dockerfile); a build without them — `ng serve`, unit tests, CI job 1c — is a
 * development build and reports `dev`.
 */
declare const STOREIT_VERSION: string | undefined;
declare const STOREIT_REVISION: string | undefined;

export interface BuildInfo {
  /** The release tag (`v0.3.0`) or `dev`. */
  version: string;
  /** The commit SHA, or `null` for a development build. */
  revision: string | null;
}

export const DEVELOPMENT_VERSION = 'dev';

/** Reads the build-time defines; `typeof` keeps an undefined global from throwing. */
export function readBuildInfo(): BuildInfo {
  const version = typeof STOREIT_VERSION === 'string' ? STOREIT_VERSION.trim() : '';
  const revision = typeof STOREIT_REVISION === 'string' ? STOREIT_REVISION.trim() : '';
  return {
    version: version || DEVELOPMENT_VERSION,
    revision: revision || null,
  };
}

export const BUILD_INFO = new InjectionToken<BuildInfo>('BUILD_INFO', {
  providedIn: 'root',
  factory: readBuildInfo,
});
