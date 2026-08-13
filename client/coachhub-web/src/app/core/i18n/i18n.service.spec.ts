import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { I18nService } from './i18n.service';
import { translations } from './translations';

describe('I18nService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.lang = '';
    document.documentElement.dir = '';
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.lang = '';
    document.documentElement.dir = '';
  });

  it('switches the entire document between LTR English and RTL Arabic', () => {
    const service = new I18nService(document);

    expect(service.language()).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(document.documentElement.dir).toBe('ltr');

    service.setLanguage('ar');

    expect(service.language()).toBe('ar');
    expect(document.documentElement.lang).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(localStorage.getItem('coachhub.language')).toBe('ar');
    expect(service.translate('common.search')).toBe(translations.ar['common.search']);
  });

  it('restores the saved direction on a new application session', () => {
    localStorage.setItem('coachhub.language', 'ar');

    const service = new I18nService(document);

    expect(service.language()).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
  });
});
