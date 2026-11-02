import { NgTemplateOutlet } from '@angular/common';
import { Component, Input, OnChanges, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, finalize } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';
import { PagedResult, emptyPage } from '../../shared/models/paged-result';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { AssessmentsService, FormSummary } from '../assessments/assessments.service';
import {
  CheckInAnswer, CheckInDetail, CheckInDue, CheckInFilters, CheckInSchedule, CheckInStatus, CheckInSummary, CheckInsService, Weekday,
  answerText, cadenceText, weekdays,
} from './check-ins.service';

type View = 'AwaitingReview' | 'Pending' | 'Overdue' | 'All';

/**
 * Check-in management (§29). On its own page: check-ins awaiting review, pending and overdue clients, and the whole
 * history, searched by client name, form, due and sent dates (§50, P20). Inside a client's profile (with
 * <c>clientId</c>): their schedule (§28) and history. Either way a selected check-in opens the review: answers,
 * submissions, replies, private notes, approve and request changes.
 */
@Component({
  selector: 'app-check-ins',
  standalone: true,
  imports: [FormsModule, NgTemplateOutlet, RouterLink, ...I18N_PIPES],
  template: `
    @if (!clientId) {
      <section class="dashboard-hero"><div><p class="eyebrow">{{ 'checkIns.eyebrow' | translate }}</p><h1>{{ 'checkIns.title' | translate }}</h1>
        <p>{{ 'checkIns.intro' | translate }}</p></div></section>
    }
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }

    @if (clientId) {
      <section class="card workbench">
        <h2>{{ 'checkIns.schedule' | translate }}</h2>
        @if (schedule(); as s) {
          <p><strong translate="no">{{ s.formName }}</strong> · {{ cadence(s) }} ·
            {{ 'checkIns.nextDue' | translate: { date: s.nextDueOn ? (s.nextDueOn | localDate: 'EEE d MMM') : '–' } }}
            @if (s.state) { <span class="badge" [class.alert]="s.state === 'Overdue'">{{ s.state | enumLabel: 'checkInDueState' }}</span> }</p>
        } @else { <p class="muted">{{ 'checkIns.notScheduled' | translate }}</p> }
        <div class="form-grid">
          <label><span>{{ 'checkIns.form' | translate }}</span>
            <select [(ngModel)]="draft.formDefinitionId" name="form">
              <option value="">{{ 'checkIns.chooseForm' | translate }}</option>
              @for (f of forms(); track f.id) { <option [value]="f.id" translate="no">{{ f.name }}</option> }
            </select></label>
          <label><span>{{ 'checkIns.every' | translate }}</span>
            <select [(ngModel)]="draft.intervalWeeks" name="interval">
              @for (n of intervals; track n) { <option [ngValue]="n">{{ 'checkIns.weeks' | translate: { count: n } }}</option> }
            </select></label>
          <label><span>{{ 'checkIns.dueOn' | translate }}</span>
            <select [(ngModel)]="draft.dueWeekday" name="weekday">@for (d of days; track d) { <option [value]="d">{{ d | enumLabel: 'weekday' }}</option> }</select></label>
          <label><span>{{ 'checkIns.starting' | translate }}</span><input type="date" [(ngModel)]="draft.startsOn" name="startsOn" /></label>
        </div>
        @if (!forms().length) { <p class="muted"><a routerLink="/assessments">{{ 'checkIns.noForms' | translate }}</a></p> }
        <div class="actions">
          <button class="button button-primary" type="button" [disabled]="busy() || !draft.formDefinitionId" (click)="saveSchedule()">{{ 'checkIns.saveSchedule' | translate }}</button>
          @if (schedule()) { <button class="link-button danger" type="button" [disabled]="busy()" (click)="stopSchedule()">{{ 'checkIns.stop' | translate }}</button> }
        </div>
      </section>
    } @else {
      <nav class="segmented" role="tablist" [attr.aria-label]="'checkIns.lists' | translate">
        @for (v of views; track v) {
          <button type="button" role="tab" [class.active]="view() === v" [attr.aria-selected]="view() === v" (click)="open(v)">{{ viewKey(v) | translate }}</button>
        }
      </nav>
      <form class="card workbench filters" [attr.aria-label]="'checkIns.filters' | translate" (ngSubmit)="search()">
        <label><span>{{ 'checkIns.clientName' | translate }}</span><input name="searchTerm" [(ngModel)]="filters.searchTerm" autocomplete="off" /></label>
        @if (view() === 'AwaitingReview' || view() === 'All') {
          <label><span>{{ 'checkIns.form' | translate }}</span>
            <select name="formFilter" [(ngModel)]="filters.formDefinitionId">
              <option value="">{{ 'checkIns.anyForm' | translate }}</option>
              @for (f of forms(); track f.id) { <option [value]="f.id" translate="no">{{ f.name }}</option> }
            </select></label>
          <label><span>{{ 'checkIns.dueFrom' | translate }}</span><input type="date" name="dueFrom" [(ngModel)]="filters.dueFrom" /></label>
          <label><span>{{ 'checkIns.dueTo' | translate }}</span><input type="date" name="dueTo" [(ngModel)]="filters.dueTo" /></label>
          <label><span>{{ 'checkIns.sentFrom' | translate }}</span><input type="date" name="sentFrom" [(ngModel)]="filters.submittedFrom" /></label>
          <label><span>{{ 'checkIns.sentTo' | translate }}</span><input type="date" name="sentTo" [(ngModel)]="filters.submittedTo" /></label>
        }
        <div class="actions">
          <button class="button button-primary" type="submit">{{ 'common.search' | translate }}</button>
          <button class="button button-secondary" type="button" (click)="clearFilters()">{{ 'checkIns.clear' | translate }}</button>
        </div>
      </form>
    }

    @if (view() === 'Pending' || view() === 'Overdue') {
      <section class="card workbench">
        <ul class="session-list">
          @for (d of due().items; track d.clientId) {
            <li><div><a [routerLink]="['/clients', d.clientId]" translate="no">{{ d.clientName }}</a>
              <small class="muted">{{ 'checkIns.due' | translate: { date: (d.dueOn | localDate: 'EEE d MMM') } }}@if (d.daysOverdue) { · {{ 'checkIns.daysOverdue' | translate: { count: d.daysOverdue } }} }</small></div>
              <span class="badge" [class.alert]="d.state === 'Overdue'">{{ d.state | enumLabel: 'checkInDueState' }}</span></li>
          } @empty { <li class="muted">{{ 'checkIns.noClients' | translate }}</li> }
        </ul>
        <ng-container *ngTemplateOutlet="pager; context: { $implicit: due() }" />
      </section>
    } @else {
      <section class="card workbench">
        @if (clientId) { <h2>{{ 'checkIns.title' | translate }}</h2> }
        <div class="table-scroll"><table>
          <thead><tr>@if (!clientId) { <th>{{ 'checkIns.client' | translate }}</th> }<th>{{ 'checkIns.dueColumn' | translate }}</th>
            <th>{{ 'checkIns.sent' | translate }}</th><th>{{ 'checkIns.status' | translate }}</th><th>{{ 'checkIns.replies' | translate }}</th><th></th></tr></thead>
          <tbody>
            @for (c of list().items; track c.id) {
              <tr [class.chosen]="detail()?.checkIn?.id === c.id">
                @if (!clientId) { <td translate="no">{{ c.clientName }}</td> }
                <td>{{ c.dueOn | localDate: 'd MMM y' }}</td>
                <td>{{ c.submittedAt | localDate: 'd MMM, HH:mm' }}@if (c.isLate) {{{ 'checkIns.late' | translate }}}@if (c.submissionCount > 1) {{{ 'checkIns.resubmitted' | translate }}}</td>
                <td><span class="badge" [class.ok]="c.status === 'Approved'" [class.alert]="c.status === 'ChangesRequested'">{{ c.status | enumLabel: 'checkInStatus' }}</span></td>
                <td>{{ c.replyCount }}</td>
                <td><button class="link-button" type="button" (click)="select(c)">{{ 'checkIns.review' | translate }}</button></td>
              </tr>
            } @empty { <tr><td [attr.colspan]="clientId ? 5 : 6" class="muted">{{ 'checkIns.none' | translate }}</td></tr> }
          </tbody>
        </table></div>
        <ng-container *ngTemplateOutlet="pager; context: { $implicit: list() }" />
      </section>
    }

    <ng-template #pager let-page>
      @if (page.totalPages > 1) {
        <nav class="pager" [attr.aria-label]="'common.pagination' | translate">
          <span>{{ 'common.records' | translate: { count: page.totalCount } }}</span>
          <button class="button button-secondary" type="button" [disabled]="page.pageNumber <= 1" (click)="goTo(page.pageNumber - 1)">{{ 'common.previous' | translate }}</button>
          <span>{{ 'common.pageOf' | translate: { page: page.pageNumber, pages: page.totalPages } }}</span>
          <button class="button button-secondary" type="button" [disabled]="page.pageNumber >= page.totalPages" (click)="goTo(page.pageNumber + 1)">{{ 'common.next' | translate }}</button>
        </nav>
      }
    </ng-template>

    @if (detail(); as d) {
      <section class="card workbench review" [attr.aria-label]="'checkIns.reviewLabel' | translate">
        <div class="section-heading"><h2><span translate="no">{{ d.checkIn.clientName }}</span> · {{ d.checkIn.dueOn | localDate: 'd MMM y' }}</h2>
          <button class="link-button" type="button" (click)="detail.set(null)">{{ 'checkIns.close' | translate }}</button></div>
        <p class="muted"><span translate="no">{{ d.checkIn.formName }}</span> · {{ d.checkIn.status | enumLabel: 'checkInStatus' }}@if (d.checkIn.isLate) {{{ 'checkIns.late' | translate }}}
          · {{ 'checkIns.submissions' | translate: { count: d.submissions.length } }}</p>
        <div class="table-scroll"><table>
          <thead><tr><th>{{ 'checkIns.question' | translate }}</th><th>{{ 'checkIns.answer' | translate }}</th></tr></thead>
          <tbody>@for (a of d.answers; track a.id) { <tr><td translate="no">{{ a.questionText }}</td><td [attr.translate]="a.mediaId ? null : 'no'">{{ answer(a) }}</td></tr> }</tbody>
        </table></div>
        <h3>{{ 'checkIns.repliesTitle' | translate }}</h3>
        <ul class="plain-list">
          @for (r of d.replies; track r.id) {
            <li><span class="muted">{{ r.createdAt | localDate: 'd MMM, HH:mm' }} ·
              @if (r.authorName) { <span translate="no">{{ r.authorName }}</span> } @else { {{ 'checkIns.coach' | translate }} }:</span> <span translate="no">{{ r.text }}</span></li>
          }
          @empty { <li class="muted">{{ 'checkIns.noReplies' | translate }}</li> }
        </ul>
        <label><span>{{ 'checkIns.message' | translate }}</span><textarea [(ngModel)]="message" name="message" rows="3" maxlength="4000"></textarea></label>
        <div class="actions">
          <button class="button button-secondary" type="button" [disabled]="busy() || !message.trim()" (click)="reply(d)">{{ 'checkIns.sendReply' | translate }}</button>
          <button class="button button-primary" type="button" [disabled]="busy()" (click)="approve(d)">{{ 'checkIns.approve' | translate }}</button>
          <button class="button button-secondary" type="button" [disabled]="busy() || !message.trim()" (click)="requestChanges(d)">{{ 'checkIns.requestChanges' | translate }}</button>
        </div>
        <label><span>{{ 'checkIns.notes' | translate }}</span><textarea [(ngModel)]="notes" name="notes" rows="3" maxlength="4000"></textarea></label>
        <button class="link-button" type="button" [disabled]="busy()" (click)="saveNotes(d)">{{ 'checkIns.saveNotes' | translate }}</button>
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
    .filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 10px; align-items: end; margin-bottom: 12px; }
    .filters label { display: grid; gap: 4px; }
    .filters .actions { margin: 0; }
    .pager { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; justify-content: flex-end; margin-top: 10px; }
  `],
})
export class CheckInsComponent implements OnInit, OnChanges {
  /** Inside a client's profile: that client's schedule and check-ins. */
  @Input() clientId = '';
  readonly views: View[] = ['AwaitingReview', 'Pending', 'Overdue', 'All'];
  readonly intervals = [1, 2, 4];
  readonly days = weekdays;
  readonly view = signal<View>('AwaitingReview');
  readonly list = signal<PagedResult<CheckInSummary>>(emptyPage());
  readonly due = signal<PagedResult<CheckInDue>>(emptyPage());
  readonly detail = signal<CheckInDetail | null>(null);
  readonly schedule = signal<CheckInSchedule | null>(null);
  readonly forms = signal<FormSummary[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  draft: { formDefinitionId: string; intervalWeeks: number; dueWeekday: Weekday; startsOn: string | null } =
    { formDefinitionId: '', intervalWeeks: 1, dueWeekday: 'Sunday', startsOn: null };
  filters: CheckInFilters = {};
  message = '';
  notes = '';
  private page = 1;

  constructor(private readonly api: CheckInsService, private readonly assessments: AssessmentsService, private readonly i18n: I18nService) {}

  ngOnInit(): void {
    if (this.clientId) return;
    this.open('AwaitingReview');
    // Every check-in form, archived ones too, so older check-ins can still be found by their form.
    this.assessments.listForms({ formType: 'CheckIn', pageSize: 100 }).subscribe({
      next: page => this.forms.set(page.items),
      error: e => this.error.set(apiErrorMessage(e)),
    });
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

  viewKey(view: View): TranslationKey { return `checkIns.view.${view}`; }
  cadence(schedule: CheckInSchedule): string { return cadenceText(schedule, this.i18n); }
  answer(answer: CheckInAnswer): string { return answerText(answer, this.i18n); }

  open(view: View): void {
    this.view.set(view);
    this.detail.set(null);
    this.page = 1;
    this.load();
  }

  search(): void {
    this.page = 1;
    this.load();
  }

  clearFilters(): void {
    this.filters = {};
    this.search();
  }

  goTo(page: number): void {
    this.page = page;
    this.load();
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

  private load(): void {
    this.error.set('');
    const view = this.view();
    if (view === 'Pending' || view === 'Overdue') {
      this.api.due(view, this.page, this.filters.searchTerm ?? '')
        .subscribe({ next: page => this.due.set(page), error: e => this.error.set(apiErrorMessage(e)) });
    } else this.loadList();
  }

  private loadList(): void {
    const status: CheckInStatus | '' = this.view() === 'AwaitingReview' ? 'AwaitingReview' : '';
    const filters = this.clientId ? {} : this.filters;
    this.api.list({ ...filters, clientId: this.clientId || undefined, status, pageNumber: this.page }).subscribe({
      next: page => this.list.set(page),
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
