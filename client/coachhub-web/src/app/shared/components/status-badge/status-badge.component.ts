import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-status-badge',
  template:
    '<span class="status-badge" [class.success]="isPositive" [class.neutral]="!isPositive">{{ value }}</span>',
  standalone: false,
})
export class StatusBadgeComponent {
  @Input() value = '';
  get isPositive(): boolean {
    return ['Active', 'Published', 'OnPlan', 'OnTrack'].includes(this.value);
  }
}
