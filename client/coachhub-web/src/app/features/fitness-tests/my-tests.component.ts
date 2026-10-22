import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import { TrendChartComponent } from '../progress-tracking/trend-chart.component';
import { ClientTest, FitnessTestsService, TestDefinition, TestResult, TestUnit, localChangeText, localCutOffText, localUnit } from './fitness-tests.service';

interface Draft { testId: string; resultId: string | null; date: string; value: number | null; notes: string; }

/**
 * The client's tests (§34–§36), mobile first: the tests the coach assigned with instructions and video, a quick form
 * to record a result, and each test's current against previous result and history. Results the coach recorded are
 * read-only here.
 */
@Component({
  selector: 'app-my-tests',
  standalone: true,
  imports: [FormsModule, RouterLink, TrendChartComponent, ...I18N_PIPES],
  template: `
    <section class="dashboard-hero"><div><p class="eyebrow"><a routerLink="/app/progress">{{ 'tests.progressLink' | translate }}</a></p><h1>{{ 'tests.title' | translate }}</h1>
      <p>{{ 'tests.intro' | translate }}</p></div></section>
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (data(); as d) {
      @for (t of d.tests; track t.testDefinitionId) {
        <section class="card workbench test">
          <div class="line">
            <div><p class="eyebrow">{{ t.kind | enumLabel: 'testKind' }}</p><h2 translate="no">{{ t | localName }}</h2></div>
            @if (t.results[0]?.classification; as c) { <span class="badge" [class.ok]="c === 'Good'" [class.alert]="c === 'Poor'">{{ c | enumLabel: 'testClassification' }}</span> }
          </div>
          @if (t.definition; as def) {
            @if (instructions(def); as text) { <p class="instructions" translate="no">{{ text }}</p> }
            <small class="muted">{{ cutOffs(def) }}</small>
            @if (def.youTubeUrl) { <a [href]="def.youTubeUrl" target="_blank" rel="noopener">{{ 'tests.watch' | translate }}</a> }
          }
          @if (t.progress.current; as current) {
            <div class="stats">
              <div><small>{{ 'tests.latest' | translate }}</small><strong>{{ current.value | localNumber: '1.0-2' }} {{ unit(t.unit) }}</strong><small class="muted">{{ current.date | localDate: 'd MMM' }}</small></div>
              <div><small>{{ 'tests.change' | translate }}</small><strong>{{ change(t.progress.changeFromPrevious, t) ?? '–' }}</strong>
                <small class="muted">{{ (t.progress.trend === 'Improved' ? 'tests.better' : t.progress.trend === 'Declined' ? 'tests.worse' : t.progress.trend ? 'tests.same' : 'tests.first') | translate }}</small></div>
              <div><small>{{ 'tests.best' | translate }}</small><strong>{{ t.progress.best?.value | localNumber: '1.0-2' }} {{ unit(t.unit) }}</strong><small class="muted">{{ t.progress.best?.date | localDate: 'd MMM y' }}</small></div>
            </div>
          }
          @if (draft()?.testId === t.testDefinitionId) {
              <form class="record" (ngSubmit)="save(t)">
                <label><span>{{ 'tests.result' | translate: { unit: unit(t.unit) } }}</span><input type="number" inputmode="decimal" min="0" step="0.01" [(ngModel)]="draft()!.value" name="value" required /></label>
                <label><span>{{ 'tests.date' | translate }}</span><input type="date" [(ngModel)]="draft()!.date" name="date" required /></label>
                <label class="wide"><span>{{ 'tests.notes' | translate }}</span><input [(ngModel)]="draft()!.notes" name="notes" maxlength="1000" /></label>
                <div class="actions">
                  <button type="submit" class="button button-primary" [disabled]="draft()!.value === null || busy()">{{ 'tests.save' | translate }}</button>
                  <button type="button" class="button button-secondary" (click)="draft.set(null)">{{ 'tests.cancel' | translate }}</button>
                </div>
              </form>
          } @else if (canRecord(t)) { <button type="button" class="button button-primary" (click)="start(t)">{{ 'tests.record' | translate }}</button> }
          @if (t.results.length > 1) { <app-trend-chart [points]="points(t)" [series]="t | localName" [unit]="unit(t.unit)" [label]="'tests.overTime' | translate: { name: (t | localName) }" /> }
          @if (t.results.length) {
            <ul class="history">
              @for (r of t.results; track r.id) {
                <li>
                  <span><strong>{{ r.value | localNumber: '1.0-2' }} {{ unit(r.unit) }}</strong> · {{ r.date | localDate: 'd MMM y' }}
                    @if (r.classification) { · {{ r.classification | enumLabel: 'testClassification' }} }
                    @if (r.recordedBy === 'Coach') { <small class="muted">{{ 'tests.byCoach' | translate }}</small> }</span>
                  @if (r.notes) { <small class="muted block" translate="no">{{ r.notes }}</small> }
                  @if (r.recordedBy === 'Client') {
                    <span class="actions">
                      <button type="button" class="link-button" (click)="editResult(t, r)">{{ 'tests.edit' | translate }}</button>
                      <button type="button" class="link-button danger" (click)="remove(r)">{{ 'tests.delete' | translate }}</button>
                    </span>
                  }
                </li>
              }
            </ul>
          }
        </section>
      } @empty { <section class="card workbench"><p class="muted">{{ 'tests.none' | translate }}</p></section> }
    }
  `,
  styles: [`
    .test { display: grid; gap: 10px; margin-bottom: 12px; }
    .test h2 { margin: 0; font-size: 1.1rem; }
    .line { display: flex; justify-content: space-between; gap: 8px; align-items: flex-start; }
    .instructions { margin: 0; white-space: pre-line; }
    .stats { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; }
    .stats div { display: grid; gap: 2px; }
    .record { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
    .record label { display: grid; gap: 4px; }
    .record input { min-height: 44px; width: 100%; }
    .record .wide, .record .actions { grid-column: 1 / -1; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; }
    .button { min-height: 44px; }
    .history { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
    .history li { display: grid; gap: 2px; padding-top: 8px; border-top: 1px solid var(--gray-200); }
    .block { display: block; }
    .badge { padding: 2px 8px; border-radius: 999px; background: var(--gray-100); color: var(--gray-700); font-size: 0.75rem; font-weight: 600; white-space: nowrap; }
    .badge.ok { background: var(--brand-soft); color: var(--brand-dark); }
    .badge.alert { color: var(--error); }
  `],
})
export class MyTestsComponent implements OnInit {
  readonly data = signal<{ tests: ClientTest[] } | null>(null);
  readonly draft = signal<Draft | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');

  constructor(private readonly api: FitnessTestsService, private readonly i18n: I18nService) {}

  unit(unit: TestUnit): string { return localUnit(unit, this.i18n); }
  cutOffs(test: TestDefinition): string { return localCutOffText(test, this.i18n); }
  /** The coach's instructions in Arabic when they wrote some, else in English. */
  instructions(test: TestDefinition): string | null {
    return (this.i18n.language() === 'ar' && test.instructionsAr?.trim()) ? test.instructionsAr : test.instructionsEn;
  }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.clientTests().subscribe({
      // Assigned tests first; earlier tests with results follow.
      next: d => this.data.set({ tests: [...d.tests].sort((a, b) => Number(b.isAssigned) - Number(a.isAssigned)) }),
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  canRecord(test: ClientTest): boolean { return test.isAssigned && !!test.definition?.isActive; }

  change(value: number | null, test: ClientTest): string | null { return localChangeText(value, test.unit, this.i18n); }

  points(test: ClientTest) { return [...test.results].reverse().map(r => ({ date: r.date, value: r.value })); }

  start(test: ClientTest): void {
    this.draft.set({ testId: test.testDefinitionId, resultId: null, date: isoDate(new Date()), value: null, notes: '' });
  }

  editResult(test: ClientTest, result: TestResult): void {
    this.draft.set({ testId: test.testDefinitionId, resultId: result.id, date: result.date, value: result.value, notes: result.notes ?? '' });
  }

  save(test: ClientTest): void {
    const d = this.draft();
    if (!d || d.value === null) return;
    const values = { date: d.date, value: Number(d.value), notes: d.notes.trim() || null };
    this.run(d.resultId ? this.api.change(d.resultId, values) : this.api.record({ testDefinitionId: test.testDefinitionId, ...values }));
  }

  remove(result: TestResult): void {
    if (!confirm(this.i18n.translate('tests.confirmDelete'))) return;
    this.run(this.api.deleteResult(result.id));
  }

  private run(request: Observable<unknown>): void {
    this.busy.set(true);
    request.subscribe({
      next: () => { this.busy.set(false); this.error.set(''); this.draft.set(null); this.load(); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e)); },
    });
  }
}
