import { Entry, Language } from './define';
import { calendar } from './translations/calendar';
import { client } from './translations/client';
import { core } from './translations/core';
import { enums } from './translations/enums';
import { forms } from './translations/forms';

export type { Language } from './define';

/** Every area's table (P19). Add a new area here; a key may appear in only one area. */
export const tables = [core, enums, client, forms, calendar] as const;

export const translations = {
  en: { ...core.en, ...enums.en, ...client.en, ...forms.en, ...calendar.en },
  ar: { ...core.ar, ...enums.ar, ...client.ar, ...forms.ar, ...calendar.ar },
};

export type TranslationKey = keyof typeof translations.en;

/** The entry for a key, typed loosely for the service's lookups. */
export function entryOf(language: Language, key: string): Entry | undefined {
  return (translations[language] as Record<string, Entry>)[key];
}
