import { Component, Input, OnChanges, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import { TrendChartComponent } from '../progress-tracking/trend-chart.component';
import {
  ClientTest, FitnessTestsService, TestClassification, TestDefinition, TestResult, TestUnit, classifications, localChangeText, localCutOffText,
  localUnit,
} from './fitness-tests.service';

interface ResultDraft { testDefinitionId: string; date: string; value: number | null; notes: string; }

/**
 * A client's tests as the coach sees them (§34–§36): assign tests, record results after a session, correct results
 * and override their classification, and follow each test's previous-against-current progress over time.
 */
@Component({
  selector: 'app-client-tests',
  standalone: true,
  imports: [FormsModule, TrendChartComponent, ...I18N_PIPES],
  template: `
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    <section class="card workbench">
      <h2>{{ 'coachTests.assign' | translate }}</h2>
      <form class="form-grid" (ngSubmit)="assign()">
        <label><span>{{ 'coachTests.test' | translate }}</span>
          <select [(ngModel)]="assignId" name="assign">
            <option value="">{{ 'coachTests.choose' | translate }}</option>
            @for (t of unassigned(); track t.id) { <option [value]="t.id">{{ t.kind | enumLabel: 'testKind' }} · {{ t | localName }}</option> }
          </select></label>
        <button type="submit" class="button button-secondary" [disabled]="!assignId() || busy()">{{ 'coachTests.assignButton' | translate }}</button>
      </form>
      <form class="form-grid record" (ngSubmit)="saveResult()">
        <label><span>{{ 'coachTests.resultFor' | translate }}</span>
          <select [(ngModel)]="draft().testDefinitionId" name="test" [disabled]="!!editing()">
            <option value="">{{ 'coachTests.choose' | translate }}</option>
            @for (t of library(); track t.id) { <option [value]="t.id">{{ t.kind | enumLabel: 'testKind' }} · {{ t | localName }} ({{ unit(t.unit) }})</option> }
          </select></label>
        <label><span>{{ 'coachTests.date' | translate }}</span><input type="date" [(ngModel)]="draft().date" name="date" required /></label>
        <label><span>{{ 'coachTests.value' | translate }}</span><input type="number" min="0" step="0.01" [(ngModel)]="draft().value" name="value" required /></label>
        <label><span>{{ 'coachTests.notes' | translate }}</span><input [(ngModel)]="draft().notes" name="notes" maxlength="1000" /></label>
        <div class="actions">
          <button type="submit" class="button button-primary" [disabled]="!draft().testDefinitionId || draft().value === null || busy()">
            {{ (editing() ? 'coachTests.saveResult' : 'coachTests.recordResult') | translate }}</button>
          @if (editing()) { <button type="button" class="button button-secondary" (click)="resetDraft()">{{ 'common.cancel' | translate }}</button> }
        </div>
      </form>
    </section>

    @if (data(); as d) {
      @for (t of d.tests; track t.testDefinitionId) {
        <section class="card workbench test">
          <div class="line">
            <div><p class="eyebrow">{{ t.kind | enumLabel: 'testKind' }}</p><h3 translate="no">{{ t | localName }}</h3>
              <small class="muted">{{ t.definition ? cutOffs(t.definition) : ('coachTests.previousCoach' | translate) }}</small></div>
            <div class="actions">
              @if (t.isAssigned) {
                <span class="badge ok">{{ 'coachTests.assigned' | translate }}</span>
                <button type="button" class="link-button danger" (click)="unassign(t)">{{ 'coachTests.unassign' | translate }}</button>
              } @else { <span class="badge">{{ 'coachTests.notAssigned' | translate }}</span> }
            </div>
          </div>
          @if (t.progress.current; as current) {
            <div class="stats">
              <div><small>{{ 'coachTests.current' | translate }}</small><strong>{{ current.value | localNumber: '1.0-2' }} {{ unit(t.unit) }}</strong><small class="muted">{{ current.date | localDate: 'd MMM y' }}</small></div>
              <div><small>{{ 'coachTests.previous' | translate }}</small><strong>{{ t.progress.previous ? (t.progress.previous.value | localNumber: '1.0-2') + ' ' + unit(t.unit) : '–' }}</strong>
                <small class="muted">{{ change(t.progress.changeFromPrevious, t) ?? '' }}@if (t.progress.trend; as trend) { · {{ trend | enumLabel: 'testTrend' }} }</small></div>
              <div><small>{{ 'coachTests.sinceFirst' | translate }}</small><strong>{{ change(t.progress.changeFromFirst, t) ?? '–' }}</strong>
                <small class="muted">{{ t.progress.percentChangeFromFirst !== null ? (t.progress.percentChangeFromFirst | localNumber: '1.0-1') + '%' : '' }}</small></div>
              <div><small>{{ 'coachTests.best' | translate }}</small><strong>{{ t.progress.best?.value | localNumber: '1.0-2' }} {{ unit(t.unit) }}</strong><small class="muted">{{ t.progress.best?.date | localDate: 'd MMM y' }}</small></div>
            </div>
            @if (t.results.length > 1) { <app-trend-chart [points]="points(t)" [series]="t | localName" [unit]="unit(t.unit)" [label]="'tests.overTime' | translate: { name: (t | localName) }" /> }
            <div class="table-scroll"><table>
              <thead><tr><th>{{ 'coachTests.date' | translate }}</th><th>{{ 'coachTests.result' | translate }}</th><th>{{ 'coachTests.rating' | translate }}</th><th>{{ 'coachTests.by' | translate }}</th><th>{{ 'coachTests.notes' | translate }}</th><th></th></tr></thead>
              <tbody>
                @for (r of t.results; track r.id) {
                  <tr>
                    <td>{{ r.date | localDate: 'd MMM y' }}</td>
                    <td>{{ r.value | localNumber: '1.0-2' }} {{ unit(r.unit) }}</td>
                    <td><select [ngModel]="r.isClassificationOverridden ? r.classification : ''" (ngModelChange)="classify(r, $event)" [name]="'c' + r.id"
                        [attr.aria-label]="'coachTests.ratingOf' | translate: { date: (r.date | localDate: 'd MMM') }">
                        <option value="">{{ !r.isClassificationOverridden && r.classification ? ('coachTests.automaticWith' | translate: { rating: (r.classification | enumLabel: 'testClassification') }) : ('coachTests.automatic' | translate) }}</option>
                        @for (c of classes; track c) { <option [value]="c">{{ c | enumLabel: 'testClassification' }}</option> }
                      </select></td>
                    <td>{{ r.recordedBy | enumLabel: 'testRecorder' }}</td>
                    <td translate="no">{{ r.notes }}</td>
                    <td class="actions">
                      <button type="button" class="link-button" (click)="edit(r)">{{ 'common.edit' | translate }}</button>
                      <button type="button" class="link-button danger" (click)="removeResult(r)">{{ 'common.delete' | translate }}</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table></div>
          } @else { <p class="muted">{{ 'coachTests.noResults' | translate }}</p> }
        </section>
      } @empty { <p class="muted">{{ 'coachTests.none' | translate }}</p> }
    }
  `,
  styles: [`
    .record { margin-top: 12px; }
    .test { display: grid; gap: 12px; margin-top: 12px; }
    .test h3 { margin: 0; }
    .line { display: flex; justify-content: space-between; gap: 12px; flex-wrap: wrap; align-items: flex-start; }
    .actions { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 12px; }
    .stats div { display: grid; gap: 2px; }
    .badge { padding: 2px 8px; border-radius: 999px; background: var(--gray-100); color: var(--gray-700); font-size: 0.75rem; font-weight: 600; }
    .badge.ok { background: var(--brand-soft); color: var(--brand-dark); }
  `],
})
export class ClientTestsComponent implements OnChanges {
  @Input({ required: true }) clientId!: string;
  readonly classes = classifications;
  readonly data = signal<{ tests: ClientTest[] } | null>(null);
  readonly library = signal<TestDefinition[]>([]);
  readonly unassigned = signal<TestDefinition[]>([]);
  readonly editing = signal<TestResult | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly assignId = signal('');
  readonly draft = signal<ResultDraft>(this.emptyDraft());

  constructor(private readonly api: FitnessTestsService, private readonly i18n: I18nService) {}

  unit(unit: TestUnit): string { return localUnit(unit, this.i18n); }
  cutOffs(test: TestDefinition): string { return localCutOffText(test, this.i18n); }

  ngOnChanges(): void {
    this.api.list({ isActive: true }).subscribe({
      next: page => { this.library.set(page.items); this.refreshUnassigned(); },
      error: e => this.error.set(apiErrorMessage(e)),
    });
    this.load();
  }

  load(): void {
    this.api.clientTests(this.clientId).subscribe({
      next: d => { this.data.set(d); this.refreshUnassigned(); },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  change(value: number | null, test: ClientTest): string | null { return localChangeText(value, test.unit, this.i18n); }

  points(test: ClientTest) { return [...test.results].reverse().map(r => ({ date: r.date, value: r.value })); }

  assign(): void { this.run(this.api.assign(this.clientId, this.assignId()), () => { this.assignId.set(''); }); }

  unassign(test: ClientTest): void {
    if (!confirm(this.i18n.translate('coachTests.confirmUnassign', { name: this.i18n.name(test) }))) return;
    this.run(this.api.unassign(this.clientId, test.testDefinitionId));
  }

  saveResult(): void {
    const { testDefinitionId, date, value, notes } = this.draft();
    if (value === null) return;
    const editing = this.editing();
    const request = editing
      ? this.api.change(editing.id, { date, value: Number(value), notes: notes.trim() || null }, this.clientId)
      : this.api.record({ testDefinitionId, date, value: Number(value), notes: notes.trim() || null }, this.clientId);
    this.run(request, () => this.resetDraft());
  }

  edit(result: TestResult): void {
    this.editing.set(result);
    this.draft.set({ testDefinitionId: result.testDefinitionId, date: result.date, value: result.value, notes: result.notes ?? '' });
  }

  resetDraft(): void { this.editing.set(null); this.draft.set(this.emptyDraft()); }

  classify(result: TestResult, value: TestClassification | ''): void {
    this.run(this.api.classify(this.clientId, result.id, value || null));
  }

  removeResult(result: TestResult): void {
    if (!confirm(this.i18n.translate('coachTests.confirmDelete'))) return;
    this.run(this.api.deleteResult(result.id, this.clientId));
  }

  private run(request: Observable<unknown>, done?: () => void): void {
    this.busy.set(true);
    request.subscribe({
      next: () => { this.busy.set(false); this.error.set(''); done?.(); this.load(); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e)); },
    });
  }

  private refreshUnassigned(): void {
    const assigned = new Set((this.data()?.tests ?? []).filter(t => t.isAssigned).map(t => t.testDefinitionId));
    this.unassigned.set(this.library().filter(t => !assigned.has(t.id)));
  }

  private emptyDraft(): ResultDraft { return { testDefinitionId: '', date: isoDate(new Date()), value: null, notes: '' }; }
}
