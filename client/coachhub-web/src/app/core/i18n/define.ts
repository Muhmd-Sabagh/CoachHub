export type Language = 'en' | 'ar';

/**
 * A translation: plain text, or plural forms chosen by the `count` parameter with the language's plural rules
 * (Arabic has zero, one, two, few, many and other). `{name}` marks a parameter.
 */
export type Entry = string | PluralEntry;
export type PluralEntry = Partial<Record<Intl.LDMLPluralRule, string>> & { other: string };

/**
 * One area's translations (P19). The Arabic table must have exactly the English keys, so a missing or misspelt key
 * fails the build.
 */
export function defineTranslations<const E extends Record<string, Entry>>(table: { en: E; ar: { [K in keyof E]: Entry } }) {
  return table;
}
