import { Component, OnInit, signal } from '@angular/core';
import { apiErrorMessage } from '../../shared/services/api-error';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { InboxService, NotificationEvent, notificationEvents } from './inbox.service';

/** Which notifications the account also receives by email (§42); in-app notifications always arrive. */
@Component({
  selector: 'app-notification-preferences',
  standalone: true,
  imports: [...I18N_PIPES],
  template: `
    <section class="card workbench preferences">
      <h2>{{ 'notifications.email' | translate }}</h2>
      <p class="muted">{{ 'notifications.emailIntro' | translate }}</p>
      @if (!platformEmail()) { <p class="muted" role="status">{{ 'notifications.emailOff' | translate }}</p> }
      @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
      <ul>
        @for (e of events; track e) {
          <li><label><input type="checkbox" [checked]="selected().has(e)" (change)="toggle(e)" [disabled]="busy()" /> {{ e | enumLabel: 'notificationEvent' }}</label></li>
        }
      </ul>
      @if (saved()) { <p class="success-message" role="status">{{ 'notifications.saved' | translate }}</p> }
    </section>
  `,
  styles: [`
    .preferences { display: grid; gap: 8px; }
    .preferences h2 { margin: 0; font-size: 1.05rem; }
    ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
    label { display: flex; gap: 8px; align-items: center; min-height: 36px; }
  `],
})
export class NotificationPreferencesComponent implements OnInit {
  readonly events = notificationEvents;
  readonly selected = signal(new Set<NotificationEvent>());
  readonly platformEmail = signal(true);
  readonly busy = signal(false);
  readonly saved = signal(false);
  readonly error = signal('');

  constructor(private readonly inbox: InboxService) {}

  ngOnInit(): void {
    this.inbox.preferences().subscribe({
      next: p => { this.selected.set(new Set(p.emailEvents)); this.platformEmail.set(p.emailEnabledOnPlatform); },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  toggle(event: NotificationEvent): void {
    const next = new Set(this.selected());
    if (next.has(event)) next.delete(event); else next.add(event);
    this.busy.set(true);
    this.saved.set(false);
    this.inbox.setPreferences([...next]).subscribe({
      next: p => { this.selected.set(new Set(p.emailEvents)); this.busy.set(false); this.saved.set(true); this.error.set(''); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e)); },
    });
  }
}
