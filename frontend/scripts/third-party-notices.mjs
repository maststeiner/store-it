#!/usr/bin/env node
// SPEC-009 D3 / AC-08 — third-party notices of the web bundle, generated at build time.
//
// Angular's production build extracts the license of every package it actually bundles into
// dist/frontend/3rdpartylicenses.txt (full texts included) — but outside the served `browser/`
// folder. This post-build step turns that file into browser/assets/third-party-notices.json,
// adding each package's version and homepage from node_modules, so the About page lists exactly
// what ships, never a hand-maintained copy. A package without license information fails the
// build (exit 1): the license gate denies unknown licenses, and attribution must not skip one.
//
// Usage: node scripts/third-party-notices.mjs [<dist dir> [<node_modules dir>]]
// Defaults: dist/frontend and node_modules, relative to the current directory (npm runs
// scripts from the package root).

import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const EXTRACTION_FILE = '3rdpartylicenses.txt';
export const OUTPUT_FILE = join('browser', 'assets', 'third-party-notices.json');

const BLOCK_SEPARATOR = /^-{10,}\r?\n/m;
const BLOCK_HEAD =
  /^Package: (?<name>[^\r\n]+)\r?\nLicense: (?<license>[^\r\n]*)(?:\r?\n)?(?<text>[\s\S]*)$/;

/**
 * Parses Angular's extraction: blocks separated by a dashed line, each `Package:` + `License:`
 * (the license is JSON-encoded, e.g. `"MIT"`) followed by the license text.
 * @param {string} extraction
 * @returns {{ name: string, license: string, text: string | null }[]}
 */
export function parseLicenseExtraction(extraction) {
  const entries = [];
  for (const block of extraction.split(BLOCK_SEPARATOR)) {
    const match = BLOCK_HEAD.exec(block.trim());
    if (!match?.groups) {
      continue;
    }
    const name = match.groups.name.trim();
    const license = unquote(match.groups.license.trim());
    if (!license) {
      throw new Error(`${name}: the license extraction carries no license information`);
    }
    const text = match.groups.text.trim();
    entries.push({ name, license, text: text || null });
  }
  return entries;
}

/**
 * Adds version and homepage from each package's package.json; sorted by name.
 * @param {{ name: string, license: string, text: string | null }[]} entries
 * @param {(name: string) => Record<string, unknown> | null} readPackageJson
 */
export function enrich(entries, readPackageJson) {
  return entries
    .map((entry) => {
      const pkg = readPackageJson(entry.name);
      return {
        name: entry.name,
        version: typeof pkg?.version === 'string' ? pkg.version : null,
        license: entry.license,
        copyright: null,
        url: homepageOf(pkg),
        text: entry.text,
      };
    })
    .sort((a, b) => a.name.localeCompare(b.name, 'en'));
}

/** @param {Record<string, unknown> | null} pkg */
function homepageOf(pkg) {
  if (!pkg) {
    return null;
  }
  if (typeof pkg.homepage === 'string' && pkg.homepage) {
    return pkg.homepage;
  }
  const repository = pkg.repository;
  const url =
    typeof repository === 'string'
      ? repository
      : repository && typeof repository === 'object' && 'url' in repository
        ? repository.url
        : null;
  return typeof url === 'string' && url
    ? url
        .replace(/^git\+/, '')
        .replace(/^git:\/\//, 'https://')
        .replace(/\.git$/, '')
    : null;
}

/** @param {string} value */
function unquote(value) {
  return value.startsWith('"') && value.endsWith('"') && value.length >= 2
    ? value.slice(1, -1)
    : value;
}

/**
 * @param {string} distDir
 * @param {string} nodeModulesDir
 * @returns {number} exit code
 */
export function main(distDir, nodeModulesDir) {
  const extractionPath = join(distDir, EXTRACTION_FILE);
  if (!existsSync(extractionPath)) {
    // Development builds (`extractLicenses: false`) produce no extraction — nothing to ship.
    console.log(`third-party-notices: no ${extractionPath}, skipping (development build)`);
    return 0;
  }

  const readPackageJson = (/** @type {string} */ name) => {
    const path = join(nodeModulesDir, name, 'package.json');
    return existsSync(path) ? JSON.parse(readFileSync(path, 'utf8')) : null;
  };

  const components = enrich(
    parseLicenseExtraction(readFileSync(extractionPath, 'utf8')),
    readPackageJson,
  );
  const outputPath = join(distDir, OUTPUT_FILE);
  mkdirSync(dirname(outputPath), { recursive: true });
  writeFileSync(outputPath, JSON.stringify(components, null, 2) + '\n');
  console.log(`third-party-notices: ${components.length} components written to ${outputPath}`);
  return 0;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const [distDir = join('dist', 'frontend'), nodeModulesDir = 'node_modules'] =
    process.argv.slice(2);
  try {
    process.exitCode = main(distDir, nodeModulesDir);
  } catch (error) {
    console.error(`third-party-notices: ${error instanceof Error ? error.message : error}`);
    process.exitCode = 1;
  }
}
