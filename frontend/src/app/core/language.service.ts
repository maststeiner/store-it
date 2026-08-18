import { Injectable, inject, signal } from '@angular/core';

import { TranslateService } from './translate';

export const SUPPORTED_LANGUAGES = ['de', 'en', 'fr', 'it'] as const;

export type AppLanguage = (typeof SUPPORTED_LANGUAGES)[number];

const STORAGE_KEY = 'store-it.language';
const FALLBACK_LANGUAGE: AppLanguage = 'en';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);

  readonly supported = SUPPORTED_LANGUAGES;

  private readonly currentLang = signal<AppLanguage>(FALLBACK_LANGUAGE);
  readonly current = this.currentLang.asReadonly();

  /** Picks the persisted language, else the browser language, else English. */
  init(): void {
    const saved = this.asSupported(localStorage.getItem(STORAGE_KEY));
    const browser = this.asSupported(navigator.language?.slice(0, 2).toLowerCase());
    this.apply(saved ?? browser ?? FALLBACK_LANGUAGE);
  }

  set(language: string): void {
    const supported = this.asSupported(language) ?? FALLBACK_LANGUAGE;
    localStorage.setItem(STORAGE_KEY, supported);
    this.apply(supported);
  }

  private apply(language: AppLanguage): void {
    this.currentLang.set(language);
    this.translate.use(language);
  }

  private asSupported(value: string | null | undefined): AppLanguage | null {
    return SUPPORTED_LANGUAGES.includes(value as AppLanguage) ? (value as AppLanguage) : null;
  }
}
