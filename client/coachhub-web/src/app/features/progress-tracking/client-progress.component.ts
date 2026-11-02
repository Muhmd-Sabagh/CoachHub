import { Component, Input, OnChanges, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { TestSummaryComponent } from '../fitness-tests/test-summary.component';
import { addDays, isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import { PersonalRecord, recordText } from '../training-tracking/training-tracking.service';
import { PhotoCompareComponent } from './photo-compare.component';
import {
  BodyProgress, Lifestyle, LifestyleTargets, LifestyleTargetsResponse, MeasurementSite, ProgressDashboard, ProgressPhoto,
  ProgressTrackingService, SeriesSummary, changeText, hoursText, percentText,
} from './progress-tracking.service';
import { TrendChartComponent } from './trend-chart.component';

/**
 * A client's progress for their coach (§37, §40): the progress dashboard with the client's daily targets and
 * lifestyle, body tracking (weight, body fat, measurements), or progress photos. Read-only except the targets.
 */
@Component({
  selector: 'app-client-progress',
  standalone: true,
  imports: [FormsModule, TrendChartComponent, PhotoCompareComponent, TestSummaryComponent, ...I18N_PIPES],
  template: `
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (view === 'progress') {
      <section class="card workbench">
        <div class="section-heading"><h2>{{ 'coachProgress.dashboard' | translate }}</h2>
          <label class="range"><span class="muted">{{ 'range.label' | translate }}</span>
            <select [(ngModel)]="range" (ngModelChange)="load()" name="range">
              <option [ngValue]="7">{{ 'range.7' | translate }}</option><option [ngValue]="28">{{ 'range.28' | translate }}</option><option [ngValue]="90">{{ 'range.90' | translate }}</option>
            </select></label></div>
        @if (dashboard(); as d) {
          <div class="report-metrics">
            <article><small>{{ 'progress.weight' | translate }}</small><strong>{{ d.body.weight.current ? (d.body.weight.current.value | localNumber: '1.0-1') + ' ' + ('unit.kg' | translate) : '–' }}</strong><span>{{ 'progress.sinceStart' | translate: { change: change(d.body.weight, 'unit.kg') } }}</span></article>
            <article><small>{{ 'progress.bodyFat' | translate }}</small><strong>{{ d.body.bodyFat.current ? (d.body.bodyFat.current.value | localNumber: '1.0-1') + '%' : '–' }}</strong><span>{{ change(d.body.bodyFat, 'unit.percent') }}</span></article>
            <article><small>{{ 'coachProgress.calories' | translate }}</small><strong>{{ d.nutrition.averageConsumed ? (d.nutrition.averageConsumed.calories | localNumber: '1.0-0') : '–' }}</strong>
              <span>{{ 'coachProgress.adherence' | translate: { value: percent(d.nutrition.adherencePercent) } }}</span></article>
            <article><small>{{ 'coachProgress.macros' | translate }}</small>
              <strong>{{ d.nutrition.averageConsumed ? (d.nutrition.averageConsumed.protein | localNumber: '1.0-0') + ' / ' + (d.nutrition.averageConsumed.carbohydrates | localNumber: '1.0-0') + ' / ' + (d.nutrition.averageConsumed.fat | localNumber: '1.0-0') : '–' }}</strong>
              <span>{{ 'coachNutrition.perLoggedDay' | translate }}</span></article>
            <article><small>{{ 'progress.workouts' | translate }}</small><strong>{{ d.training.summary.completedSessions }} / {{ d.training.summary.scheduledSessions }}</strong>
              <span>{{ 'coachProgress.workoutsLine' | translate: { value: percent(d.training.summary.adherencePercent), volume: (d.training.summary.volumeKg | localNumber: '1.0-0') } }}</span></article>
            <article><small>{{ 'progress.sleep' | translate }}</small><strong>{{ hours(d.lifestyle.averageSleepHours) }}</strong><span>{{ 'progress.goal' | translate: { value: percent(d.lifestyle.sleepGoalPercent) } }}</span></article>
            <article><small>{{ 'progress.steps' | translate }}</small><strong>{{ d.lifestyle.averageSteps !== null ? (d.lifestyle.averageSteps | localNumber) : '–' }}</strong><span>{{ 'progress.goal' | translate: { value: percent(d.lifestyle.stepsGoalPercent) } }}</span></article>
            <article><small>{{ 'progress.water' | translate }}</small><strong>{{ d.lifestyle.averageWaterMl !== null ? (d.lifestyle.averageWaterMl | localNumber) + ' ' + ('unit.ml' | translate) : '–' }}</strong><span>{{ 'progress.goal' | translate: { value: percent(d.lifestyle.waterGoalPercent) } }}</span></article>
            <article><small>{{ 'progress.photos' | translate }}</small><strong>{{ d.body.photoCount }}</strong><span>{{ d.body.latestPhoto ? ('progress.latest' | translate: { date: (d.body.latestPhoto.date | localDate: 'd MMM') }) : ('progress.noneYet' | translate) }}</span></article>
          </div>
          @if (d.body.measurements.length) {
            <h3>{{ 'progress.measurements' | translate }}</h3>
            <div class="table-scroll"><table>
              <thead><tr><th>{{ 'progress.site' | translate }}</th><th>{{ 'progress.starting' | translate }}</th><th>{{ 'progress.current' | translate }}</th><th>{{ 'progress.change' | translate }}</th></tr></thead>
              <tbody>@for (m of d.body.measurements; track m.site) {
                <tr><td>{{ m.site | enumLabel: 'measurementSite' }}</td><td>{{ m.summary.start?.value | localNumber: '1.0-1' }}</td><td>{{ m.summary.current?.value | localNumber: '1.0-1' }}</td><td>{{ change(m.summary, 'unit.cm') }}</td></tr>
              }</tbody>
            </table></div>
          }
          <h3>{{ 'coachProgress.strength' | translate }}</h3>
          <ul class="plain-list">
            @for (r of d.training.strength; track r.id) { <li>{{ record(r) }}</li> }
            @for (r of d.training.recentRecords; track r.id) { <li class="muted">{{ r.achievedOn | localDate: 'd MMM' }} · {{ record(r) }}</li> }
            @if (!d.training.strength.length && !d.training.recentRecords.length) { <li class="muted">{{ 'coachTraining.noRecords' | translate }}</li> }
          </ul>
          @if (d.training.tests.length) {
            <h3>{{ 'testSummary.title' | translate }}</h3>
            <app-test-summary [tests]="d.training.tests" />
          }
          <h3>{{ 'coachProgress.adherenceTitle' | translate }}</h3>
          <div class="table-scroll"><table>
            <thead><tr><th>{{ 'coachProgress.area' | translate }}</th><th>{{ 'coachProgress.score' | translate }}</th><th>{{ 'coachProgress.weight' | translate }}</th></tr></thead>
            <tbody>
              @for (a of d.adherence.areas; track a.area) { <tr><td>{{ a.area | enumLabel: 'adherenceArea' }}</td><td>{{ percent(a.percent) }}</td><td>{{ a.weight }}%</td></tr> }
              <tr><td><strong>{{ 'coachProgress.overall' | translate }}</strong></td><td><strong>{{ percent(d.adherence.overallPercent) }}</strong></td><td></td></tr>
            </tbody>
          </table></div>
          <h3>{{ 'coachProgress.checkIn' | translate }}</h3>
          @if (d.checkIn.latest; as c) {
            <p>{{ 'coachProgress.latestCheckIn' | translate: { date: (c.dueOn | localDate: 'd MMM'), status: (c.status | enumLabel: 'checkInStatus') } }}@if (c.isLate) {{{ 'checkIns.late' | translate }}}{{ 'coachProgress.doneInRange' | translate: { done: d.checkIn.adherence.completed, due: d.checkIn.adherence.due } }}</p>
          } @else { <p class="muted">{{ 'coachProgress.noCheckIns' | translate }}</p> }
          @if (d.checkIn.latestFeedback; as f) { <p class="muted">{{ 'coachProgress.latestFeedback' | translate }} <span translate="no">“{{ f.text }}”</span></p> }
        } @else if (loading()) { <p class="table-message">{{ 'common.loading' | translate }}</p> }
      </section>

      <section class="card workbench">
        <h2>{{ 'coachProgress.dailyTargets' | translate }}</h2>
        @if (targets(); as t) {
          <div class="form-grid">
            <label><span>{{ 'coachProgress.stepsLabel' | translate }}</span><input type="number" min="100" max="100000" [(ngModel)]="draft.steps" name="tSteps"
              [placeholder]="t.coachDefaults.steps ? ('coachProgress.default' | translate: { value: t.coachDefaults.steps }) : ('coachProgress.none' | translate)" /></label>
            <label><span>{{ 'coachProgress.waterLabel' | translate }}</span><input type="number" min="250" max="10000" [(ngModel)]="draft.waterMl" name="tWater"
              [placeholder]="t.coachDefaults.waterMl ? ('coachProgress.default' | translate: { value: t.coachDefaults.waterMl }) : ('coachProgress.none' | translate)" /></label>
            <label><span>{{ 'coachProgress.sleepLabel' | translate }}</span><input type="number" min="3" max="14" step="0.25" [(ngModel)]="draft.sleepHours" name="tSleep"
              [placeholder]="t.coachDefaults.sleepHours ? ('coachProgress.default' | translate: { value: t.coachDefaults.sleepHours }) : ('coachProgress.none' | translate)" /></label>
          </div>
          <p class="muted">{{ 'coachProgress.targetsHint' | translate }}</p>
          <button class="button button-primary" type="button" [disabled]="busy()" (click)="saveTargets()">{{ 'coachProgress.saveTargets' | translate }}</button>
        }
      </section>

      @if (lifestyle(); as l) {
        <section class="card workbench">
          <h2>{{ 'coachProgress.lifestyle' | translate }}</h2>
          <h3>{{ 'progress.steps' | translate }}</h3>
          <app-trend-chart [points]="points(l, 'steps')" [series]="'progress.steps' | translate" [unit]="'unit.steps' | translate" [target]="l.targets.steps" [bars]="true" [label]="'progress.dailySteps' | translate" />
          <h3>{{ 'progress.sleep' | translate }}</h3>
          <app-trend-chart [points]="points(l, 'sleep')" [series]="'progress.sleep' | translate" [unit]="'progress.hoursUnit' | translate" [target]="l.targets.sleepHours" [label]="'progress.hoursSlept' | translate" />
          <h3>{{ 'progress.water' | translate }}</h3>
          <app-trend-chart [points]="points(l, 'water')" [series]="'progress.water' | translate" [unit]="'unit.ml' | translate" [target]="l.targets.waterMl" [bars]="true" [label]="'progress.dailyWater' | translate" />
          <div class="table-scroll"><table>
            <thead><tr><th>{{ 'progress.day' | translate }}</th><th>{{ 'progress.sleep' | translate }}</th><th>{{ 'coachProgress.sleepQuality' | translate }}</th><th>{{ 'progress.steps' | translate }}</th><th>{{ 'coachProgress.waterColumn' | translate }}</th></tr></thead>
            <tbody>@for (day of l.days.slice().reverse(); track day.date) {
              <tr><td>{{ day.date | localDate: 'EEE d MMM' }}</td><td>{{ day.sleep ? hours(day.sleep.hours) : '–' }}</td><td>{{ day.sleep?.quality ?? '–' }}</td>
                <td>{{ day.steps ? (day.steps.steps | localNumber) : '–' }}</td><td>{{ day.waterMl ? (day.waterMl | localNumber) : '–' }}</td></tr>
            }</tbody>
          </table></div>
        </section>
      }
    }

    @if (view === 'measurements') {
      @if (body(); as b) {
        <section class="card workbench">
          <h2>{{ 'progress.weight' | translate }}</h2>
          <div class="report-metrics">
            <article><small>{{ 'progress.starting' | translate }}</small><strong>{{ (b.weight.summary.start?.value ?? b.profileStartingWeightKg) !== null && (b.weight.summary.start?.value ?? b.profileStartingWeightKg) !== undefined ? ((b.weight.summary.start?.value ?? b.profileStartingWeightKg) | localNumber: '1.0-1') : '–' }}</strong><span>{{ 'unit.kg' | translate }}</span></article>
            <article><small>{{ 'progress.current' | translate }}</small><strong>{{ b.weight.summary.current ? (b.weight.summary.current.value | localNumber: '1.0-1') : '–' }}</strong><span>{{ 'unit.kg' | translate }}</span></article>
            <article><small>{{ 'coachProgress.lowestHighest' | translate }}</small><strong>{{ b.weight.summary.lowest ? (b.weight.summary.lowest.value | localNumber: '1.0-1') : '–' }} / {{ b.weight.summary.highest ? (b.weight.summary.highest.value | localNumber: '1.0-1') : '–' }}</strong><span>{{ 'unit.kg' | translate }}</span></article>
            <article><small>{{ 'progress.change' | translate }}</small><strong>{{ change(b.weight.summary, 'unit.kg') }}</strong><span>{{ 'coachProgress.sinceFirst' | translate }}</span></article>
          </div>
          <app-trend-chart [points]="b.weight.entries" [series]="'progress.weight' | translate" [unit]="'unit.kg' | translate" [label]="'progress.weightTrend' | translate" />
          <div class="table-scroll"><table>
            <thead><tr><th>{{ 'coachProgress.week' | translate }}</th><th>{{ 'coachProgress.averageKg' | translate }}</th><th>{{ 'coachProgress.weighIns' | translate }}</th></tr></thead>
            <tbody>@for (w of b.weight.summary.weeks.slice().reverse(); track w.weekStart) {
              <tr><td>{{ w.weekStart | localDate: 'd MMM' }}</td><td>{{ w.average | localNumber: '1.0-1' }}</td><td>{{ w.count }}</td></tr>
            }</tbody>
          </table></div>
        </section>
        <section class="card workbench">
          <h2>{{ 'progress.bodyFat' | translate }}</h2>
          <p class="muted">{{ 'progress.bodyFatNotice' | translate }}</p>
          <p>{{ 'coachProgress.bodyFatLine' | translate: { start: b.bodyFat.summary.start?.value ?? b.profileStartingBodyFatPercent ?? '–', current: b.bodyFat.summary.current?.value ?? '–', change: change(b.bodyFat.summary, 'unit.percent') } }}</p>
          @if (b.bodyFat.entries.length) { <app-trend-chart [points]="b.bodyFat.entries" [series]="'progress.bodyFat' | translate" unit="%" [label]="'progress.bodyFatTrend' | translate" /> }
        </section>
        <section class="card workbench">
          <h2>{{ 'progress.measurements' | translate }}</h2>
          <div class="table-scroll"><table>
            <thead><tr><th>{{ 'progress.site' | translate }}</th><th>{{ 'progress.starting' | translate }}</th><th>{{ 'progress.current' | translate }}</th><th>{{ 'progress.difference' | translate }}</th><th>{{ 'coachProgress.entries' | translate }}</th></tr></thead>
            <tbody>@for (m of b.measurements; track m.site) {
              <tr [class.chosen]="m.site === site" (click)="site = m.site"><td>{{ m.site | enumLabel: 'measurementSite' }}</td><td>{{ m.summary.start ? (m.summary.start.value | localNumber: '1.0-1') : '–' }}</td>
                <td>{{ m.summary.current ? (m.summary.current.value | localNumber: '1.0-1') : '–' }}</td><td>{{ change(m.summary, 'unit.cm') }}</td><td>{{ m.summary.count }}</td></tr>
            }</tbody>
          </table></div>
          @if (siteEntries(b); as entries) {
            @if (entries.length) { <app-trend-chart [points]="entries" [series]="site | enumLabel: 'measurementSite'" [unit]="'unit.cm' | translate" [label]="'progress.siteTrend' | translate: { site: (site | enumLabel: 'measurementSite') }" /> }
          }
        </section>
      } @else if (loading()) { <p class="table-message">{{ 'common.loading' | translate }}</p> }
    }

    @if (view === 'photos') {
      <section class="card workbench"><h2>{{ 'coachProgress.photos' | translate }}</h2><app-photo-compare [photos]="photos()" /></section>
    }
  `,
  styles: [`
    .range { display: flex; gap: 8px; align-items: center; }
    .plain-list { list-style: none; margin: 0 0 12px; padding: 0; display: grid; gap: 6px; }
    tr.chosen td { background: var(--brand-soft); }
  `],
})
export class ClientProgressComponent implements OnChanges {
  @Input({ required: true }) clientId = '';
  @Input() view: 'progress' | 'measurements' | 'photos' = 'progress';
  readonly today = isoDate(new Date());
  readonly percent = percentText;
  readonly dashboard = signal<ProgressDashboard | null>(null);
  readonly lifestyle = signal<Lifestyle | null>(null);
  readonly targets = signal<LifestyleTargetsResponse | null>(null);
  readonly body = signal<BodyProgress | null>(null);
  readonly photos = signal<ProgressPhoto[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  range = 28;
  site: MeasurementSite = 'Waist';
  draft: LifestyleTargets = { steps: null, waterMl: null, sleepHours: null };

  constructor(private readonly api: ProgressTrackingService, private readonly i18n: I18nService) {}

  change(summary: SeriesSummary, unit: TranslationKey): string { return changeText(summary, this.i18n.translate(unit)); }
  hours(value: number | null | undefined): string { return hoursText(value, this.i18n); }
  record(record: PersonalRecord): string { return recordText(record, this.i18n); }

  ngOnChanges(): void {
    this.load();
  }

  load(): void {
    const from = addDays(this.today, -(this.range - 1));
    if (this.view === 'progress') {
      this.fetch(this.api.dashboard(from, this.today, this.clientId), d => this.dashboard.set(d));
      this.fetch(this.api.lifestyle(addDays(this.today, -13), this.today, this.clientId), l => this.lifestyle.set(l));
      this.fetch(this.api.targets(this.clientId), t => { this.targets.set(t); this.draft = { ...t.client }; });
    }
    if (this.view === 'measurements') this.fetch(this.api.body(addDays(this.today, -179), this.today, this.clientId), b => this.body.set(b));
    if (this.view === 'photos') this.fetch(this.api.photos(this.clientId), p => this.photos.set(p));
  }

  saveTargets(): void {
    const value = (x: number | null | undefined) => (x === null || x === undefined || (x as unknown) === '' ? null : Number(x));
    this.busy.set(true);
    this.error.set('');
    this.api.setTargets(this.clientId, { steps: value(this.draft.steps), waterMl: value(this.draft.waterMl), sleepHours: value(this.draft.sleepHours) })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({ next: t => { this.targets.set(t); this.load(); }, error: e => this.error.set(apiErrorMessage(e)) });
  }

  points(l: Lifestyle, kind: 'steps' | 'sleep' | 'water') {
    return l.days.map(x => ({
      date: x.date,
      value: kind === 'steps' ? x.steps?.steps ?? null : kind === 'sleep' ? x.sleep?.hours ?? null : x.waterMl || null,
    }));
  }

  siteEntries(body: BodyProgress) {
    return body.measurements.find(x => x.site === this.site)?.entries ?? [];
  }

  private fetch<T>(request: Observable<T>, next: (value: T) => void): void {
    this.loading.set(true);
    this.error.set('');
    request.pipe(finalize(() => this.loading.set(false))).subscribe({ next, error: e => this.error.set(apiErrorMessage(e)) });
  }
}
