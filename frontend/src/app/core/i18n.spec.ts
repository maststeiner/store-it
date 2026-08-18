import { readFileSync } from 'node:fs';
import { join } from 'node:path';

// Guards the actual translation files (public/assets/i18n/*.json) — the component
// specs mock translations, so a missing/empty/inconsistent key in a real language
// file would otherwise ship unnoticed (exactly how chip keys slipped through once).

const LANGUAGES = ['de', 'en', 'fr', 'it'] as const;
const REFERENCE = 'en';

type Dict = Record<string, unknown>;

function loadDict(lang: string): Dict {
  const path = join(process.cwd(), 'public', 'assets', 'i18n', `${lang}.json`);
  return JSON.parse(readFileSync(path, 'utf-8')) as Dict;
}

function flatten(dict: Dict, prefix = '', out = new Map<string, string>()): Map<string, string> {
  for (const [key, value] of Object.entries(dict)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') {
      out.set(path, value);
    } else if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      flatten(value as Dict, path, out);
    } else {
      throw new Error(`Translation value at "${path}" must be a string or object`);
    }
  }
  return out;
}

function placeholders(value: string): string[] {
  return (value.match(/\{\{\s*\w+\s*\}\}/g) ?? []).map((p) => p.replace(/\s/g, '')).sort();
}

const dicts = new Map(LANGUAGES.map((lang) => [lang, flatten(loadDict(lang))]));
const referenceKeys = [...dicts.get(REFERENCE)!.keys()].sort();

describe('i18n translation files', () => {
  it.each(LANGUAGES)('%s has exactly the same keys as the reference (en)', (lang) => {
    const keys = [...dicts.get(lang)!.keys()].sort();
    expect(keys).toEqual(referenceKeys);
  });

  it.each(LANGUAGES)('%s has no empty values', (lang) => {
    const empty = [...dicts.get(lang)!.entries()]
      .filter(([, value]) => value.trim() === '')
      .map(([key]) => key);
    expect(empty).toEqual([]);
  });

  it.each(LANGUAGES)('%s preserves interpolation placeholders from the reference', (lang) => {
    const dict = dicts.get(lang)!;
    const mismatches: string[] = [];
    for (const [key, referenceValue] of dicts.get(REFERENCE)!) {
      const expected = placeholders(referenceValue);
      if (expected.length === 0) {
        continue;
      }
      const value = dict.get(key);
      if (!value || placeholders(value).join(',') !== expected.join(',')) {
        mismatches.push(key);
      }
    }
    expect(mismatches).toEqual([]);
  });
});
