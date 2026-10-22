import { DOCUMENT, formatDate, formatNumber, registerLocaleData } from '@angular/common';
import localeAr from '@angular/common/locales/ar';
import { Inject, Injectable, computed, signal } from '@angular/core';
import { Entry } from './define';
import { Language, TranslationKey, entryOf } from './translations';

// Arabic dates and numbers, registered wherever the service is used; Angular's "ar" data writes Western digits
// (owner decision 2026-10-22).
registerLocaleData(localeAr, 'ar');

export type TranslationParams = Record<string, string | number | null | undefined>;

/** A catalog item with an English name and an optional Arabic one (foods, exercises, tests, muscles …). */
export interface Named { nameEn: string; nameAr?: string | null; }

/**
 * The interface language (P19): English or Arabic (right to left). Texts come from the typed translation tables;
 * dates and numbers use the language's locale data, which writes Western digits in Arabic (owner decision
 * 2026-10-22).
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly storageKey = 'coachhub.language';
  readonly language = signal<Language>(this.restoreLanguage());
  /** The locale for Angular's formatters ('ar' is registered in main.ts). */
  readonly locale = computed(() => this.language());

  constructor(@Inject(DOCUMENT) private readonly document: Document) { this.applyDocumentLanguage(this.language()); }

  /**
   * The text for a key in the current language, falling back to English. `{name}` placeholders take `params`; a
   * plural entry picks its form from `params.count`.
   */
  translate(key: TranslationKey, params?: TranslationParams): string {
    const language = this.language();
    const entry = entryOf(language, key) ?? entryOf('en', key);
    if (entry === undefined) return key;
    return this.fill(this.pick(entry, language, params?.['count']), params);
  }

  /** The Arabic name in Arabic when there is one, else the English name. */
  name(item: Named | null | undefined): string {
    if (!item) return '';
    return this.language() === 'ar' && item.nameAr?.trim() ? item.nameAr : item.nameEn;
  }

  formatDate(value: string | number | Date | null | undefined, format = 'mediumDate'): string {
    if (value === null || value === undefined || value === '') return '';
    return formatDate(value, format, this.locale());
  }

  formatNumber(value: number | null | undefined, digitsInfo?: string): string {
    if (value === null || value === undefined || Number.isNaN(value)) return '';
    return formatNumber(value, this.locale(), digitsInfo);
  }

  toggle(): void { this.setLanguage(this.language() === 'en' ? 'ar' : 'en'); }

  setLanguage(language: Language): void {
    this.language.set(language);
    try { localStorage.setItem(this.storageKey, language); } catch { /* storage may be unavailable */ }
    this.applyDocumentLanguage(language);
  }

  /** Applies a saved account language (from sign-in); unknown or empty values keep the current one. */
  applyPreference(language: string | null | undefined): void {
    if (language === 'en' || language === 'ar') this.setLanguage(language);
  }

  private pick(entry: Entry, language: Language, count: unknown): string {
    if (typeof entry === 'string') return entry;
    const rule = typeof count === 'number' ? new Intl.PluralRules(language).select(count) : 'other';
    return entry[rule] ?? entry.other;
  }

  private fill(text: string, params?: TranslationParams): string {
    if (!params) return text;
    return text.replace(/\{(\w+)\}/g, (match, name: string) => {
      const value = params[name];
      if (value === null || value === undefined) return match;
      return typeof value === 'number' ? this.formatNumber(value) : value;
    });
  }

  private restoreLanguage(): Language {
    try { return localStorage.getItem(this.storageKey) === 'ar' ? 'ar' : 'en'; } catch { return 'en'; }
  }

  private applyDocumentLanguage(language: Language): void {
    this.document.documentElement.lang = language;
    this.document.documentElement.dir = language === 'ar' ? 'rtl' : 'ltr';
  }
}
