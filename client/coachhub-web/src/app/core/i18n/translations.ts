import { Entry, Language } from './define';
import { account } from './translations/account';
import { calendar } from './translations/calendar';
import { chat } from './translations/chat';
import { client } from './translations/client';
import { core } from './translations/core';
import { enums } from './translations/enums';
import { files } from './translations/files';
import { forms } from './translations/forms';
import { notifications } from './translations/notifications';
import { reports } from './translations/reports';
import { tests } from './translations/tests';
import { units } from './translations/units';

export type { Language } from './define';

/** Every area's table (P19). Add a new area here and in both spreads below; a key may appear in only one area. */
export const tables = [core, enums, units, client, forms, calendar, chat, files, tests, notifications, reports, account] as const;

export const translations = {
  en: { ...core.en, ...enums.en, ...units.en, ...client.en, ...forms.en, ...calendar.en, ...chat.en, ...files.en, ...tests.en, ...notifications.en, ...reports.en, ...account.en },
  ar: { ...core.ar, ...enums.ar, ...units.ar, ...client.ar, ...forms.ar, ...calendar.ar, ...chat.ar, ...files.ar, ...tests.ar, ...notifications.ar, ...reports.ar, ...account.ar },
};

export type TranslationKey = keyof typeof translations.en;

/** The entry for a key, typed loosely for the service's lookups. */
export function entryOf(language: Language, key: string): Entry | undefined {
  return (translations[language] as Record<string, Entry>)[key];
}
