import { registerLocaleData } from '@angular/common';
import localeAr from '@angular/common/locales/ar';
import { platformBrowser } from '@angular/platform-browser';
import { AppModule } from './app/app-module';

// Arabic dates and numbers (P19); Angular's "ar" data writes Western digits (owner decision 2026-10-22).
registerLocaleData(localeAr, 'ar');

platformBrowser().bootstrapModule(AppModule)
  .catch(err => console.error(err));
