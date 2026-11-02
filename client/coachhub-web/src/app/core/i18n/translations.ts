import { Entry, Language } from './define';
import { account } from './translations/account';
import { calendar } from './translations/calendar';
import { catalog } from './translations/catalog';
import { chat } from './translations/chat';
import { staff } from './translations/staff';
import { profile } from './translations/profile';
import { coaching } from './translations/coaching';
import { plans } from './translations/plans';
import { staffEnums } from './translations/staff-enums';
import { checkIns } from './translations/check-ins';
import { client } from './translations/client';
import { core } from './translations/core';
import { enums } from './translations/enums';
import { files } from './translations/files';
import { forms } from './translations/forms';
import { notifications } from './translations/notifications';
import { progress } from './translations/progress';
import { nutrition } from './translations/nutrition';
import { reports } from './translations/reports';
import { tests } from './translations/tests';
import { training } from './translations/training';
import { units } from './translations/units';

export type { Language } from './define';

/** Every area's table (P19). Add a new area here and in both spreads below; a key may appear in only one area. */
export const tables = [core, enums, units, client, forms, calendar, chat, files, tests, notifications, reports, account, nutrition, training, progress, checkIns, catalog, staff, staffEnums, profile, coaching, plans] as const;

export const translations = {
  en: { ...core.en, ...enums.en, ...units.en, ...client.en, ...forms.en, ...calendar.en, ...chat.en, ...files.en, ...tests.en, ...notifications.en, ...reports.en, ...account.en, ...nutrition.en, ...training.en, ...progress.en, ...checkIns.en, ...catalog.en, ...staff.en, ...staffEnums.en, ...profile.en, ...coaching.en, ...plans.en },
  ar: { ...core.ar, ...enums.ar, ...units.ar, ...client.ar, ...forms.ar, ...calendar.ar, ...chat.ar, ...files.ar, ...tests.ar, ...notifications.ar, ...reports.ar, ...account.ar, ...nutrition.ar, ...training.ar, ...progress.ar, ...checkIns.ar, ...catalog.ar, ...staff.ar, ...staffEnums.ar, ...profile.ar, ...coaching.ar, ...plans.ar },
};

export type TranslationKey = keyof typeof translations.en;

/** The entry for a key, typed loosely for the service's lookups. */
export function entryOf(language: Language, key: string): Entry | undefined {
  return (translations[language] as Record<string, Entry>)[key];
}
