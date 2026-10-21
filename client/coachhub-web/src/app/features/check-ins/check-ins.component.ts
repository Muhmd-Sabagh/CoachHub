import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, finalize } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { AssessmentsService, FormSummary } from '../assessments/assessments.service';
import {
  CheckInDetail, CheckInDue, CheckInSchedule, CheckInStatus, CheckInSummary, CheckInsService, Weekday, answerText, cadenceText,
  dueStateLabels, statusLabels, weekdays,
} from './check-ins.service';

type View = 'AwaitingReview' | 'Pending' | 'Overdue' | 'All';

/**
 * Check-in management (§29). On its own page: check-ins awaiting review, pending and overdue clients, and the whole
 * history. Inside a client's profile (with <c>clientId</c>): their schedule (§28) and history. Either way a selected
 * check-in opens the review: answers, submissions, replies, private notes, approve and request changes.
 */
@Component({
  selector: 'app-check-ins',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    @if (!clientId) {
      <section class="dashboard-hero"><div><p class="eyebrow">Coaching</p><h1>Check-ins</h1>
        <p>Review what clients sent, and follow up on check-ins that are due or overdue.</p></div></section>
    }
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }

    @if (clientId) {
      <section class="card workbench">
        <h2>Check-in schedule</h2>
        @if (schedule(); as s) {
          <p><strong>{{ s.formName }}</strong> · {{ cadence(s) }} · next due {{ s.nextDueOn ? (s.nextDueOn | date: 'EEE d MMM') : '–' }}
            @if (s.state) { <span class="badge" [class.alert]="s.state === 'Overdue'">{{ dueLabels[s.state] }}</span> }</p>
        } @else { <p class="muted">No check-ins scheduled.</p> }
        <div class="form-grid">
          <label><span>Check-in form</span>
            <select [(ngModel)]="draft.formDefinitionId" name="form">
              <option value="">Choose a form</option>
              @for (f of forms(); track f.id) { <option [value]="f.id">{{ f.name }}</option> }
            </select></label>
          <label><span>Every</span>
            <select [(ngModel)]="draft.intervalWeeks" name="interval">
              <option [ngValue]="1">week</option><option [ngValue]="2">2 weeks</option><option [ngValue]="4">4 weeks</option>
            </select></label>
          <label><span>Due on</span>
            <select [(ngModel)]="draft.dueWeekday" name="weekday">@for (d of days; track d) { <option [value]="d">{{ d }}</option> }</select></label>
          <label><span>Starting</span><input type="date" [(ngModel)]="draft.startsOn" name="startsOn" /></label>
        </div>
        @if (!forms().length) { <p class="muted">Create and publish a form of type CheckIn in <a routerLink="/assessments">Assessments</a> first.</p> }
        <div class="actions">
          <button class="button button-primary" type="button" [disabled]="busy() || !draft.formDefinitionId" (click)="saveSchedule()">Save schedule</button>
          @if (schedule()) { <button class="link-button danger" type="button" [disabled]="busy()" (click)="stopSchedule()">Stop check-ins</button> }
        </div>
      </section>
    } @else {
      <nav class="segmented" role="tablist" aria-label="Check-in lists">
        @for (v of views; track v.id) {
          <button type="button" role="tab" [class.active]="view() === v.id" [attr.aria-selected]="view() === v.id" (click)="open(v.id)">{{ v.label }}</button>
        }
      </nav>
    }

    @if (view() === 'Pending' || view() === 'Overdue') {
      <section class="card workbench">
        <ul class="session-list">
          @for (d of due(); track d.clientId) {
            <li><div><a [routerLink]="['/clients', d.clientId]">{{ d.clientName }}</a>
              <small class="muted">due {{ d.dueOn | date: 'EEE d MMM' }}{{ d.daysOverdue ? ' · ' + d.daysOverdue + ' days overdue' : '' }}</small></div>
              <span class="badge" [class.alert]="d.state === 'Overdue'">{{ dueLabels[d.state] }}</span></li>
          } @empty { <li class="muted">No clients here.</li> }
        </ul>
      </section>
    } @else {
      <section class="card workbench">
        @if (clientId) { <h2>Check-ins</h2> }
        <div class="table-scroll"><table>
          <thead><tr>@if (!clientId) { <th>Client</th> }<th>Due</th><th>Sent</th><th>Status</th><th>Replies</th><th></th></tr></thead>
          <tbody>
            @for (c of list(); track c.id) {
              <tr [class.chosen]="detail()?.checkIn?.id === c.id">
                @if (!clientId) { <td>{{ c.clientName }}</td> }
                <td>{{ c.dueOn | date: 'd MMM y' }}</td>
                <td>{{ c.submittedAt | date: 'd MMM, shortTime' }}{{ c.isLate ? ' · late' : '' }}{{ c.submissionCount > 1 ? ' · resubmitted' : '' }}</td>
                <td><span class="badge" [class.ok]="c.status === 'Approved'" [class.alert]="c.status === 'ChangesRequested'">{{ statuses[c.status] }}</span></td>
                <td>{{ c.replyCount }}</td>
                <td><button class="link-button" type="button" (click)="select(c)">Review</button></td>
              </tr>
            } @empty { <tr><td [attr.colspan]="clientId ? 5 : 6" class="muted">No check-ins yet.</td></tr> }
          </tbody>
        </table></div>
      </section>
    }

    @if (detail(); as d) {
      <section class="card workbench review" aria-label="Check-in review">
        <div class="section-heading"><h2>{{ d.checkIn.clientName }} · {{ d.checkIn.dueOn | date: 'd MMM y' }}</h2>
          <button class="link-button" type="button" (click)="detail.set(null)">Close</button></div>
        <p class="muted">{{ d.checkIn.formName }} · {{ statuses[d.checkIn.status] }}{{ d.checkIn.isLate ? ' · late' : '' }}
          · {{ d.submissions.length }} submission{{ d.submissions.length > 1 ? 's' : '' }}</p>
        <div class="table-scroll"><table>
          <thead><tr><th>Question</th><th>Answer</th></tr></thead>
          <tbody>@for (a of d.answers; track a.id) { <tr><td>{{ a.questionText }}</td><td>{{ answer(a) }}</td></tr> }</tbody>
        </table></div>
        <h3>Replies (the client sees these)</h3>
        <ul class="plain-list">
          @for (r of d.replies; track r.id) { <li><span class="muted">{{ r.createdAt | date: 'd MMM, shortTime' }} · {{ r.authorName || 'Coach' }}:</span> {{ r.text }}</li> }
          @empty { <li class="muted">No replies yet.</li> }
        </ul>
        <label><span>Message to the client</span><textarea [(ngModel)]="message" name="message" rows="3" maxlength="4000"></textarea></label>
        <div class="actions">
          <button class="button button-secondary" type="button" [disabled]="busy() || !message.trim()" (click)="reply(d)">Send reply</button>
          <button class="button button-primary" type="button" [disabled]="busy()" (click)="approve(d)">Approve</button>
          <button class="button button-secondary" type="button" [disabled]="busy() || !message.trim()" (click)="requestChanges(d)">Request changes</button>
        </div>
        <label><span>Private notes (only you see these)</span><textarea [(ngModel)]="notes" name="notes" rows="3" maxlength="4000"></textarea></label>
        <button class="link-button" type="button" [disabled]="busy()" (click)="saveNotes(d)">Save notes</button>
      </section>
    }
  `,
  styles: [`
    .segmented { display: inline-flex; flex-wrap: wrap; margin-bottom: 12px; border: 1px solid var(--gray-200); border-radius: 8px; overflow: hidden; }
    .segmented button { padding: 8px 14px; border: 0; background: transparent; color: inherit; min-height: 40px; }
    .segmented button.active { background: var(--brand-soft); color: var(--brand-dark); font-weight: 600; }
    .badge { margin-inline-start: 6px; padding: 2px 8px; border-radius: 999px; background: var(--gray-100); color: var(--gray-700); font-size: 0.75rem; font-weight: 600; }
    .badge.ok { background: var(--brand-soft); color: var(--brand-dark); }
    .badge.alert { color: var(--error); }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; margin: 8px 0; }
    .review { display: grid; gap: 10px; }
    .review label { display: grid; gap: 4px; }
    .plain-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
    tr.chosen td { background: var(--brand-soft); }
  `],
})
export class CheckInsComponent implements OnInit, OnChanges {
  /** Inside a client's profile: that client's schedule and check-ins. */
  @Input() clientId = '';
  readonly views: { id: View; label: string }[] = [
    { id: 'AwaitingReview', label: 'Awaiting review' }, { id: 'Pending', label: 'Due' }, { id: 'Overdue', label: 'Overdue' }, { id: 'All', label: 'History' },
  ];
  readonly days = weekdays;
  readonly statuses = statusLabels;
  readonly dueLabels = dueStateLabels;
  readonly cadence = cadenceText;
  readonly answer = answerText;
  readonly view = signal<View>('AwaitingReview');
  readonly list = signal<CheckInSummary[]>([]);
  readonly due = signal<CheckInDue[]>([]);
  readonly detail = signal<CheckInDetail | null>(null);
  readonly schedule = signal<CheckInSchedule | null>(null);
  readonly forms = signal<FormSummary[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  draft: { formDefinitionId: string; intervalWeeks: number; dueWeekday: Weekday; startsOn: string | null } =
    { formDefinitionId: '', intervalWeeks: 1, dueWeekday: 'Sunday', startsOn: null };
  message = '';
  notes = '';

  constructor(private readonly api: CheckInsService, private readonly assessments: AssessmentsService) {}

  ngOnInit(): void {
    if (!this.clientId) this.open('AwaitingReview');
  }

  ngOnChanges(): void {
    if (!this.clientId) return;
    this.view.set('All');
    this.loadList();
    this.api.schedule(this.clientId).subscribe({
      next: s => {
        this.schedule.set(s);
        if (s) this.draft = { formDefinitionId: s.formDefinitionId, intervalWeeks: s.intervalWeeks, dueWeekday: s.dueWeekday, startsOn: s.startsOn };
      },
      error: e => this.error.set(apiErrorMessage(e)),
    });
    this.assessments.listForms({ formType: 'CheckIn', isArchived: false, pageSize: 100 }).subscribe({
      next: page => this.forms.set(page.items),
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  open(view: View): void {
    this.view.set(view);
    this.detail.set(null);
    if (view === 'Pending' || view === 'Overdue') {
      this.api.due(view).subscribe({ next: page => this.due.set(page.items), error: e => this.error.set(apiErrorMessage(e)) });
    } else this.loadList();
  }

  select(checkIn: CheckInSummary): void {
    this.api.get(checkIn.id).subscribe({ next: d => this.show(d), error: e => this.error.set(apiErrorMessage(e)) });
  }

  reply(d: CheckInDetail): void { this.run(this.api.reply(d.checkIn.id, this.message.trim())); }
  approve(d: CheckInDetail): void { this.run(this.api.approve(d.checkIn.id, this.message.trim() || null)); }
  requestChanges(d: CheckInDetail): void { this.run(this.api.requestChanges(d.checkIn.id, this.message.trim())); }
  saveNotes(d: CheckInDetail): void { this.run(this.api.notes(d.checkIn.id, this.notes.trim() || null)); }

  saveSchedule(): void {
    this.busy.set(true);
    this.error.set('');
    this.api.setSchedule(this.clientId, { ...this.draft, startsOn: this.draft.startsOn || null })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({ next: s => this.schedule.set(s), error: e => this.error.set(apiErrorMessage(e)) });
  }

  stopSchedule(): void {
    this.busy.set(true);
    this.api.deleteSchedule(this.clientId).pipe(finalize(() => this.busy.set(false)))
      .subscribe({ next: () => this.schedule.set(null), error: e => this.error.set(apiErrorMessage(e)) });
  }

  private loadList(): void {
    const status: CheckInStatus | '' = this.view() === 'AwaitingReview' ? 'AwaitingReview' : '';
    this.api.list({ clientId: this.clientId || undefined, status }).subscribe({
      next: page => this.list.set(page.items),
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  private show(detail: CheckInDetail): void {
    this.detail.set(detail);
    this.notes = detail.privateNotes ?? '';
  }

  private run(request: Observable<CheckInDetail>): void {
    this.busy.set(true);
    this.error.set('');
    request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: d => { this.show(d); this.message = ''; this.loadList(); },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }
}
