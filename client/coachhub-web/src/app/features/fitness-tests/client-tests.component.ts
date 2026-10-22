import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnChanges, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import { TrendChartComponent } from '../progress-tracking/trend-chart.component';
import {
  ClientTest, FitnessTestsService, TestClassification, TestDefinition, TestResult, changeText, classificationLabels, classifications,
  cutOffText, unitLabels,
} from './fitness-tests.service';

interface ResultDraft { testDefinitionId: string; date: string; value: number | null; notes: string; }

/**
 * A client's tests as the coach sees them (§34–§36): assign tests, record results after a session, correct results
 * and override their classification, and follow each test's previous-against-current progress over time.
 */
@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-client-tests',
  standalone: true,
  imports: [CommonModule, FormsModule, TrendChartComponent],
  template: `
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    <section class="card workbench">
      <h2>Assign and record</h2>
      <form class="form-grid" (ngSubmit)="assign()">
        <label><span>Test</span>
          <select [(ngModel)]="assignId" name="assign">
            <option value="">Choose a test…</option>
            @for (t of unassigned(); track t.id) { <option [value]="t.id">{{ t.kind }} · {{ t.nameEn }}</option> }
          </select></label>
        <button type="submit" class="button button-secondary" [disabled]="!assignId || busy()">Assign to client</button>
      </form>
      <form class="form-grid record" (ngSubmit)="saveResult()">
        <label><span>Result for</span>
          <select [(ngModel)]="draft.testDefinitionId" name="test" [disabled]="!!editing()">
            <option value="">Choose a test…</option>
            @for (t of library(); track t.id) { <option [value]="t.id">{{ t.kind }} · {{ t.nameEn }} ({{ unitLabel[t.unit] }})</option> }
          </select></label>
        <label><span>Date</span><input type="date" [(ngModel)]="draft.date" name="date" required /></label>
        <label><span>Value</span><input type="number" min="0" step="0.01" [(ngModel)]="draft.value" name="value" required /></label>
        <label><span>Notes</span><input [(ngModel)]="draft.notes" name="notes" maxlength="1000" /></label>
        <div class="actions">
          <button type="submit" class="button button-primary" [disabled]="!draft.testDefinitionId || draft.value === null || busy()">
            {{ editing() ? 'Save result' : 'Record result' }}</button>
          @if (editing()) { <button type="button" class="button button-secondary" (click)="resetDraft()">Cancel</button> }
        </div>
      </form>
    </section>

    @if (data(); as d) {
      @for (t of d.tests; track t.testDefinitionId) {
        <section class="card workbench test">
          <div class="line">
            <div><p class="eyebrow">{{ t.kind }}</p><h3>{{ t.nameEn }}</h3>
              <small class="muted">{{ t.definition ? cutOffs(t.definition) : 'A previous coach\\'s test' }}</small></div>
            <div class="actions">
              @if (t.isAssigned) {
                <span class="badge ok">Assigned</span>
                <button type="button" class="link-button danger" (click)="unassign(t)">Unassign</button>
              } @else { <span class="badge">Not assigned</span> }
            </div>
          </div>
          @if (t.progress.current; as current) {
            <div class="stats">
              <div><small>Current</small><strong>{{ current.value }} {{ unitLabel[t.unit] }}</strong><small class="muted">{{ current.date | date: 'd MMM y' }}</small></div>
              <div><small>Previous</small><strong>{{ t.progress.previous ? t.progress.previous.value + ' ' + unitLabel[t.unit] : '–' }}</strong>
                <small class="muted">{{ change(t.progress.changeFromPrevious, t) ?? '' }} {{ t.progress.trend ? '· ' + t.progress.trend.toLowerCase() : '' }}</small></div>
              <div><small>Since first</small><strong>{{ change(t.progress.changeFromFirst, t) ?? '–' }}</strong>
                <small class="muted">{{ t.progress.percentChangeFromFirst !== null ? t.progress.percentChangeFromFirst + '%' : '' }}</small></div>
              <div><small>Best</small><strong>{{ t.progress.best?.value }} {{ unitLabel[t.unit] }}</strong><small class="muted">{{ t.progress.best?.date | date: 'd MMM y' }}</small></div>
            </div>
            @if (t.results.length > 1) { <app-trend-chart [points]="points(t)" [series]="t.nameEn" [unit]="unitLabel[t.unit]" [label]="t.nameEn + ' over time'" /> }
            <div class="table-scroll"><table>
              <thead><tr><th>Date</th><th>Result</th><th>Rating</th><th>By</th><th>Notes</th><th></th></tr></thead>
              <tbody>
                @for (r of t.results; track r.id) {
                  <tr>
                    <td>{{ r.date | date: 'd MMM y' }}</td>
                    <td>{{ r.value }} {{ unitLabel[r.unit] }}</td>
                    <td><select [ngModel]="r.isClassificationOverridden ? r.classification : ''" (ngModelChange)="classify(r, $event)" [name]="'c' + r.id"
                        [attr.aria-label]="'Rating of ' + (r.date | date: 'd MMM')">
                        <option value="">Automatic{{ !r.isClassificationOverridden && r.classification ? ' (' + labels[r.classification] + ')' : '' }}</option>
                        @for (c of classes; track c) { <option [value]="c">{{ labels[c] }}</option> }
                      </select></td>
                    <td>{{ r.recordedBy }}</td>
                    <td>{{ r.notes }}</td>
                    <td class="actions">
                      <button type="button" class="link-button" (click)="edit(r)">Edit</button>
                      <button type="button" class="link-button danger" (click)="removeResult(r)">Delete</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table></div>
          } @else { <p class="muted">No results yet.</p> }
        </section>
      } @empty { <p class="muted">No tests assigned or recorded yet.</p> }
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
  readonly unitLabel = unitLabels;
  readonly labels = classificationLabels;
  readonly classes = classifications;
  readonly cutOffs = cutOffText;
  readonly data = signal<{ tests: ClientTest[] } | null>(null);
  readonly library = signal<TestDefinition[]>([]);
  readonly unassigned = signal<TestDefinition[]>([]);
  readonly editing = signal<TestResult | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  assignId = '';
  draft: ResultDraft = this.emptyDraft();

  constructor(private readonly api: FitnessTestsService) {}

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

  change(value: number | null, test: ClientTest): string | null { return changeText(value, test.unit); }

  points(test: ClientTest) { return [...test.results].reverse().map(r => ({ date: r.date, value: r.value })); }

  assign(): void { this.run(this.api.assign(this.clientId, this.assignId), () => { this.assignId = ''; }); }

  unassign(test: ClientTest): void {
    if (!confirm(`Unassign "${test.nameEn}"? Its results stay.`)) return;
    this.run(this.api.unassign(this.clientId, test.testDefinitionId));
  }

  saveResult(): void {
    const { testDefinitionId, date, value, notes } = this.draft;
    if (value === null) return;
    const editing = this.editing();
    const request = editing
      ? this.api.change(editing.id, { date, value: Number(value), notes: notes.trim() || null }, this.clientId)
      : this.api.record({ testDefinitionId, date, value: Number(value), notes: notes.trim() || null }, this.clientId);
    this.run(request, () => this.resetDraft());
  }

  edit(result: TestResult): void {
    this.editing.set(result);
    this.draft = { testDefinitionId: result.testDefinitionId, date: result.date, value: result.value, notes: result.notes ?? '' };
  }

  resetDraft(): void { this.editing.set(null); this.draft = this.emptyDraft(); }

  classify(result: TestResult, value: TestClassification | ''): void {
    this.run(this.api.classify(this.clientId, result.id, value || null));
  }

  removeResult(result: TestResult): void {
    if (!confirm('Delete this result?')) return;
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
