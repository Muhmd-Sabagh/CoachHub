import { DOCUMENT } from '@angular/common';
import { Inject, Injectable, signal } from '@angular/core';
import { Language, translations } from './translations';

@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly storageKey = 'coachhub.language';
  readonly language = signal<Language>(this.restoreLanguage());

  constructor(@Inject(DOCUMENT) private readonly document: Document) { this.applyDocumentLanguage(this.language()); }

  translate(key: string): string { return translations[this.language()][key] ?? translations.en[key] ?? key; }
  toggle(): void { this.setLanguage(this.language() === 'en' ? 'ar' : 'en'); }
  setLanguage(language: Language): void {
    this.language.set(language);
    localStorage.setItem(this.storageKey, language);
    this.applyDocumentLanguage(language);
  }
  private restoreLanguage(): Language { return localStorage.getItem(this.storageKey) === 'ar' ? 'ar' : 'en'; }
  private applyDocumentLanguage(language: Language): void {
    this.document.documentElement.lang = language;
    this.document.documentElement.dir = language === 'ar' ? 'rtl' : 'ltr';
  }
}