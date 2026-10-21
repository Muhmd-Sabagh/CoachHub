import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiErrorMessage } from '../../shared/services/api-error';
import { CheckInsService, MyCheckIns, cadenceText, dueStateLabels, statusLabels } from './check-ins.service';

/**
 * The client's check-ins (§28, §29), mobile first: when the next one is due, a check-in to resubmit after the coach
 * asked for changes, and the history with the coach's replies.
 */
@Component({
  selector: 'app-my-check-ins',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (data(); as d) {
      @if (d.schedule; as s) {
        <section class="card workbench schedule" [class.overdue]="s.state === 'Overdue'">
          <div>
            <p class="eyebrow">Check-in</p>
            <strong>{{ s.formName }}</strong>
            <small class="muted block">{{ cadence(s) }}</small>
          </div>
          @if (s.nextDueOn) {
            <div class="due">
              <span class="badge" [class.alert]="s.state === 'Overdue'">{{ s.state ? dueLabels[s.state] : '' }}</span>
              <strong>{{ s.nextDueOn | date: 'EEE d MMM' }}</strong>
              @if (s.state === 'Upcoming') { <small class="muted">opens {{ s.opensOn | date: 'EEE d MMM' }}</small> }
              @else { <a class="button button-primary" [routerLink]="['/app/forms', s.formDefinitionId]">Check in now</a> }
            </div>
          }
        </section>
      }
      @if (resubmit(d); as again) {
        <section class="card workbench resubmit" role="status">
          <strong>Your coach asked for changes</strong>
          @if (again.replies.at(-1); as last) { <p>“{{ last.text }}”</p> }
          <a class="button button-primary" [routerLink]="['/app/forms', again.formDefinitionId]">Update and resubmit</a>
        </section>
      }
      @if (d.history.length) {
        <h2 class="section-title">Your check-ins</h2>
        <ul class="history">
          @for (c of d.history; track c.id) {
            <li class="card workbench">
              <div class="line">
                <strong>{{ c.dueOn | date: 'd MMM y' }}</strong>
                <span class="badge" [class.ok]="c.status === 'Approved'" [class.alert]="c.status === 'ChangesRequested'">{{ statuses[c.status] }}</span>
              </div>
              <small class="muted">Sent {{ c.submittedAt | date: 'EEE d MMM, shortTime' }}{{ c.isLate ? ' · late' : '' }}{{ c.submissions.length > 1 ? ' · resubmitted' : '' }}</small>
              @for (r of c.replies; track r.id) {
                <p class="reply"><span class="muted">{{ r.authorName || 'Your coach' }}:</span> {{ r.text }}</p>
              }
            </li>
          }
        </ul>
      }
    }
  `,
  styles: [`
    .block { display: block; }
    .schedule { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 12px; align-items: center; margin-bottom: 12px; }
    .schedule.overdue { border-color: var(--error); }
    .due { display: grid; gap: 4px; justify-items: end; }
    .due .button { min-height: 44px; }
    .badge { padding: 2px 8px; border-radius: 999px; background: var(--gray-100); color: var(--gray-700); font-size: 0.75rem; font-weight: 600; }
    .badge.ok { background: var(--brand-soft); color: var(--brand-dark); }
    .badge.alert { background: var(--gray-100); color: var(--error); }
    .resubmit { display: grid; gap: 8px; margin-bottom: 12px; border-color: var(--brand); }
    .resubmit p, .reply { margin: 0; }
    .section-title { margin: 20px 0 8px; font-size: 1.05rem; }
    .history { list-style: none; margin: 0 0 16px; padding: 0; display: grid; gap: 10px; }
    .history li { display: grid; gap: 6px; }
    .line { display: flex; justify-content: space-between; gap: 8px; align-items: center; }
  `],
})
export class MyCheckInsComponent implements OnInit {
  readonly statuses = statusLabels;
  readonly dueLabels = dueStateLabels;
  readonly cadence = cadenceText;
  readonly data = signal<MyCheckIns | null>(null);
  readonly error = signal('');

  constructor(private readonly api: CheckInsService) {}

  ngOnInit(): void {
    this.api.mine().subscribe({ next: d => this.data.set(d), error: e => this.error.set(apiErrorMessage(e)) });
  }

  resubmit(data: MyCheckIns) {
    return data.history.find(x => x.id === data.resubmitCheckInId) ?? null;
  }
}
