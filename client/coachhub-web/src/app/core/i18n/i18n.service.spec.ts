import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { enumLabelKey, enumValues } from './enum-labels';
import { I18nService } from './i18n.service';
import { tables, translations } from './translations';

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

  it('fills parameters and picks Arabic plural forms by count (P19)', () => {
    const service = new I18nService(document);
    const lookup = translations.en as Record<string, unknown>;
    lookup['test.plural'] = { one: '{count} set', other: '{count} sets' };
    (translations.ar as Record<string, unknown>)['test.plural'] = { zero: 'لا مجموعات', one: 'مجموعة واحدة', two: 'مجموعتان', few: '{count} مجموعات', many: '{count} مجموعة', other: '{count} مجموعة' };
    try {
      const key = 'test.plural' as keyof typeof translations.en;
      expect(service.translate(key, { count: 1 })).toBe('1 set');
      expect(service.translate(key, { count: 1200 })).toBe('1,200 sets');
      service.setLanguage('ar');
      expect([0, 1, 2, 3, 11].map(count => service.translate(key, { count })))
        .toEqual(['لا مجموعات', 'مجموعة واحدة', 'مجموعتان', '3 مجموعات', '11 مجموعة']);
    } finally {
      delete lookup['test.plural'];
      delete (translations.ar as Record<string, unknown>)['test.plural'];
    }
  });

  it('formats Arabic dates with Arabic names and Western digits, and picks Arabic catalog names', () => {
    const service = new I18nService(document);
    service.setLanguage('ar');
    expect(service.formatDate('2026-10-23', 'd MMMM y')).toBe('23 أكتوبر 2026');
    expect(service.formatNumber(1234.5, '1.0-1')).toBe('1,234.5');
    expect(service.name({ nameEn: 'Oats', nameAr: 'شوفان' })).toBe('شوفان');
    expect(service.name({ nameEn: 'Oats', nameAr: ' ' })).toBe('Oats');
    service.setLanguage('en');
    expect(service.name({ nameEn: 'Oats', nameAr: 'شوفان' })).toBe('Oats');
  });

  it('applies only a known saved language', () => {
    const service = new I18nService(document);
    service.applyPreference('xx');
    expect(service.language()).toBe('en');
    service.applyPreference('ar');
    expect(service.language()).toBe('ar');
  });
});

describe('translation tables (P19)', () => {
  it('define every key once across the areas', () => {
    const keys = tables.flatMap(table => Object.keys(table.en));
    expect(keys.length).toBe(new Set(keys).size);
    expect(Object.keys(translations.ar).sort()).toEqual(Object.keys(translations.en).sort());
  });

  it('label every enum value in both languages', () => {
    const missing = Object.entries(enumValues).flatMap(([name, values]) =>
      values.filter(value => !enumLabelKey(name as keyof typeof enumValues, value)).map(value => `${name}.${value}`));
    expect(missing).toEqual([]);
  });
});
