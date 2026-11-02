import { Component, Input } from '@angular/core';
import { EnumName } from '../../../core/i18n/enum-labels';
import { TranslationKey } from '../../../core/i18n/translations';

/**
 * A status pill. Give it an enum value with its enum (`enumName="clientStatus"`) or a translation key (`label`); it shows
 * the label in the interface language (P21), never the raw name.
 */
@Component({
  selector: 'app-status-badge',
  template: `<span class="status-badge" [class.success]="positive" [class.neutral]="!positive">{{
    label ? (label | translate) : enumName ? (value | enumLabel: enumName) : value
  }}</span>`,
  standalone: false,
})
export class StatusBadgeComponent {
  @Input() value = '';
  @Input() enumName?: EnumName;
  @Input() label?: TranslationKey;
  /** Shown in the brand colour; by default the values that mean "fine". */
  @Input() success?: boolean;
  get positive(): boolean {
    return this.success ?? ['Active', 'Published', 'OnPlan', 'OnTrack'].includes(this.value);
  }
}
