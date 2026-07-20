import { HttpClient } from '@angular/common/http';
import { Injectable, Pipe, PipeTransform, inject, signal } from '@angular/core';

/**
 * Minimal in-house i18n layer (service + pipe).
 *
 * Translations live in per-language JSON files under `public/assets/i18n/`
 * (spec constraint) and are fetched at runtime. Keys use dot notation
 * (`storages.deleteConfirm`), values may interpolate params via `{{name}}`.
 * A missing key falls back to English, then to the key itself.
 */

export type TranslationParams = Record<string, string | number | null>;

export interface TranslationDict {
  [key: string]: string | TranslationDict;
}

const FALLBACK_LANGUAGE = 'en';
const PARAM_PATTERN = /\{\{\s*(\w+)\s*\}\}/g;

function lookup(dict: TranslationDict | undefined, key: string): string | null {
  let node: string | TranslationDict | undefined = dict;
  for (const part of key.split('.')) {
    if (node === undefined || typeof node === 'string') {
      return null;
    }
    node = node[part];
  }
  return typeof node === 'string' ? node : null;
}

function interpolate(text: string, params?: TranslationParams): string {
  if (!params) {
    return text;
  }
  return text.replace(PARAM_PATTERN, (match, name: string) => {
    const value = params[name];
    return value === undefined || value === null ? match : String(value);
  });
}

@Injectable({ providedIn: 'root' })
export class TranslateService {
  private readonly http = inject(HttpClient);

  private readonly dictionaries = signal<Record<string, TranslationDict>>({});
  private readonly activeLang = signal(FALLBACK_LANGUAGE);
  private readonly pending = new Set<string>();

  readonly lang = this.activeLang.asReadonly();

  /** Switches the active language and lazily loads its dictionary. */
  use(language: string): void {
    this.activeLang.set(language);
    this.ensureLoaded(language);
    if (language !== FALLBACK_LANGUAGE) {
      this.ensureLoaded(FALLBACK_LANGUAGE);
    }
  }

  /** Registers a dictionary directly (used by tests; skips HTTP). */
  setTranslation(language: string, dict: TranslationDict): void {
    this.dictionaries.update((all) => ({ ...all, [language]: dict }));
  }

  /** Resolves a key for the active language; falls back to English, then the key. */
  instant(key: string, params?: TranslationParams): string {
    const dictionaries = this.dictionaries();
    const text =
      lookup(dictionaries[this.activeLang()], key) ?? lookup(dictionaries[FALLBACK_LANGUAGE], key);
    return text === null ? key : interpolate(text, params);
  }

  private ensureLoaded(language: string): void {
    if (this.dictionaries()[language] || this.pending.has(language)) {
      return;
    }
    this.pending.add(language);
    this.http.get<TranslationDict>(`./assets/i18n/${language}.json`).subscribe({
      next: (dict) => {
        this.pending.delete(language);
        this.setTranslation(language, dict);
      },
      error: () => this.pending.delete(language),
    });
  }
}

/**
 * Impure by design: re-evaluates when the active language or a lazily loaded
 * dictionary changes (both are signals read inside `instant`).
 */
@Pipe({ name: 'translate', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly translate = inject(TranslateService);

  transform(key: string, params?: TranslationParams): string {
    return this.translate.instant(key, params);
  }
}
