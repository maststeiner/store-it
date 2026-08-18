import { registerLocaleData } from '@angular/common';
import localeDe from '@angular/common/locales/de';
import localeFr from '@angular/common/locales/fr';
import localeIt from '@angular/common/locales/it';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

// Locale data for the supported UI languages so DatePipe formats dates per locale
// (en is built in). See LanguageService / SPEC-001 i18n.
registerLocaleData(localeDe);
registerLocaleData(localeFr);
registerLocaleData(localeIt);

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
