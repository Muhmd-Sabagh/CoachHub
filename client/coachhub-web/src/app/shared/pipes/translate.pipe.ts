import { Pipe, PipeTransform } from '@angular/core';
import { I18nService, Named, TranslationParams } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';
import { EnumName, enumLabelKey } from '../../core/i18n/enum-labels';

// The pipes read the language signal, so OnPush views refresh when it changes; they are impure for the same reason.

/** `{{ 'nutrition.title' | translate }}`, `{{ 'common.records' | translate: { count: n } }}`. Keys are type-checked. */
@Pipe({ name: 'translate', pure: false, standalone: true })
export class TranslatePipe implements PipeTransform {
  constructor(private readonly i18n: I18nService) {}
  transform(key: TranslationKey, params?: TranslationParams): string { return this.i18n.translate(key, params); }
}

/** A date in the current language: `{{ day | localDate: 'EEE d MMM' }}`. */
@Pipe({ name: 'localDate', pure: false, standalone: true })
export class LocalDatePipe implements PipeTransform {
  constructor(private readonly i18n: I18nService) {}
  transform(value: string | number | Date | null | undefined, format = 'mediumDate'): string { return this.i18n.formatDate(value, format); }
}

/** A number in the current language, Western digits in both: `{{ kcal | localNumber: '1.0-0' }}`. */
@Pipe({ name: 'localNumber', pure: false, standalone: true })
export class LocalNumberPipe implements PipeTransform {
  constructor(private readonly i18n: I18nService) {}
  transform(value: number | null | undefined, digitsInfo?: string): string { return this.i18n.formatNumber(value, digitsInfo); }
}

/** A catalog item's name in the current language: `{{ food | localName }}`. */
@Pipe({ name: 'localName', pure: false, standalone: true })
export class LocalNamePipe implements PipeTransform {
  constructor(private readonly i18n: I18nService) {}
  transform(item: Named | null | undefined): string { return this.i18n.name(item); }
}

/** An enum value's label: `{{ entry.slot | enumLabel: 'mealType' }}`. */
@Pipe({ name: 'enumLabel', pure: false, standalone: true })
export class EnumLabelPipe implements PipeTransform {
  constructor(private readonly i18n: I18nService) {}
  transform(value: string | null | undefined, name: EnumName): string {
    if (!value) return '';
    const key = enumLabelKey(name, value);
    return key ? this.i18n.translate(key) : value;
  }
}

/** Every localisation pipe, for a standalone component's `imports`. */
export const I18N_PIPES = [TranslatePipe, LocalDatePipe, LocalNumberPipe, LocalNamePipe, EnumLabelPipe] as const;
