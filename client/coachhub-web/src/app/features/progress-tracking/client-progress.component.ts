import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { addDays, isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import { recordText } from '../training-tracking/training-tracking.service';
import { PhotoCompareComponent } from './photo-compare.component';
import {
  BodyProgress, Lifestyle, LifestyleTargets, LifestyleTargetsResponse, MeasurementSite, ProgressDashboard, ProgressPhoto,
  ProgressTrackingService, bodyFatNotice, changeText, hoursText, percentText,
} from './progress-tracking.service';
import { TrendChartComponent } from './trend-chart.component';

/**
 * A client's progress for their coach (§37, §40): the progress dashboard with the client's daily targets and
 * lifestyle, body tracking (weight, body fat, measurements), or progress photos. Read-only except the targets.
 */
@Component({
  selector: 'app-client-progress',
  standalone: true,
  imports: [CommonModule, FormsModule, TrendChartComponent, PhotoCompareComponent],
  template: `
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (view === 'progress') {
      <section class="card workbench">
        <div class="section-heading"><h2>Progress dashboard</h2>
          <label class="range"><span class="muted">Range</span>
            <select [(ngModel)]="range" (ngModelChange)="load()" name="range">
              <option [ngValue]="7">Last 7 days</option><option [ngValue]="28">Last 4 weeks</option><option [ngValue]="90">Last 90 days</option>
            </select></label></div>
        @if (dashboard(); as d) {
          <div class="report-metrics">
            <article><small>Weight</small><strong>{{ d.body.weight.current ? d.body.weight.current.value + ' kg' : '–' }}</strong><span>{{ change(d.body.weight, 'kg') }} since start</span></article>
            <article><small>Body fat</small><strong>{{ d.body.bodyFat.current ? d.body.bodyFat.current.value + '%' : '–' }}</strong><span>{{ change(d.body.bodyFat, '%') }}</span></article>
            <article><small>Calories (avg)</small><strong>{{ d.nutrition.averageConsumed ? (d.nutrition.averageConsumed.calories | number: '1.0-0') : '–' }}</strong>
              <span>adherence {{ percent(d.nutrition.adherencePercent) }}</span></article>
            <article><small>Macros P / C / F (avg)</small>
              <strong>{{ d.nutrition.averageConsumed ? (d.nutrition.averageConsumed.protein | number: '1.0-0') + ' / ' + (d.nutrition.averageConsumed.carbohydrates | number: '1.0-0') + ' / ' + (d.nutrition.averageConsumed.fat | number: '1.0-0') : '–' }}</strong>
              <span>g per logged day</span></article>
            <article><small>Workouts</small><strong>{{ d.training.summary.completedSessions }} / {{ d.training.summary.scheduledSessions }}</strong>
              <span>adherence {{ percent(d.training.summary.adherencePercent) }} · {{ d.training.summary.volumeKg | number: '1.0-0' }} kg</span></article>
            <article><small>Sleep</small><strong>{{ hours(d.lifestyle.averageSleepHours) }}</strong><span>goal {{ percent(d.lifestyle.sleepGoalPercent) }}</span></article>
            <article><small>Steps</small><strong>{{ d.lifestyle.averageSteps !== null ? (d.lifestyle.averageSteps | number) : '–' }}</strong><span>goal {{ percent(d.lifestyle.stepsGoalPercent) }}</span></article>
            <article><small>Water</small><strong>{{ d.lifestyle.averageWaterMl !== null ? (d.lifestyle.averageWaterMl | number) + ' ml' : '–' }}</strong><span>goal {{ percent(d.lifestyle.waterGoalPercent) }}</span></article>
            <article><small>Photos</small><strong>{{ d.body.photoCount }}</strong><span>{{ d.body.latestPhoto ? 'latest ' + (d.body.latestPhoto.date | date: 'd MMM') : 'none yet' }}</span></article>
          </div>
          @if (d.body.measurements.length) {
            <h3>Measurements</h3>
            <div class="table-scroll"><table>
              <thead><tr><th>Site</th><th>Starting</th><th>Current</th><th>Change</th></tr></thead>
              <tbody>@for (m of d.body.measurements; track m.site) {
                <tr><td>{{ m.site }}</td><td>{{ m.summary.start?.value }}</td><td>{{ m.summary.current?.value }}</td><td>{{ change(m.summary, 'cm') }}</td></tr>
              }</tbody>
            </table></div>
          }
          <h3>Strength and PRs</h3>
          <ul class="plain-list">
            @for (r of d.training.strength; track r.id) { <li>{{ recordText(r) }}</li> }
            @for (r of d.training.recentRecords; track r.id) { <li class="muted">{{ r.achievedOn | date: 'd MMM' }} · {{ recordText(r) }}</li> }
            @if (!d.training.strength.length && !d.training.recentRecords.length) { <li class="muted">No records yet.</li> }
          </ul>
          <h3>Adherence</h3>
          <div class="table-scroll"><table>
            <thead><tr><th>Area</th><th>Score</th><th>Weight</th></tr></thead>
            <tbody>
              @for (a of d.adherence.areas; track a.area) { <tr><td>{{ a.area }}</td><td>{{ percent(a.percent) }}</td><td>{{ a.weight }}%</td></tr> }
              <tr><td><strong>Overall</strong></td><td><strong>{{ percent(d.adherence.overallPercent) }}</strong></td><td></td></tr>
            </tbody>
          </table></div>
          <h3>Check-in</h3>
          @if (d.checkIn.latest; as c) {
            <p>Latest due {{ c.dueOn | date: 'd MMM' }} · {{ c.status }}{{ c.isLate ? ' · late' : '' }}
              · {{ d.checkIn.adherence.completed }} of {{ d.checkIn.adherence.due }} done in this range</p>
          } @else { <p class="muted">No check-ins yet.</p> }
          @if (d.checkIn.latestFeedback; as f) { <p class="muted">Latest feedback: “{{ f.text }}”</p> }
        } @else if (loading()) { <p class="table-message">Loading…</p> }
      </section>

      <section class="card workbench">
        <h2>Daily targets</h2>
        @if (targets(); as t) {
          <div class="form-grid">
            <label><span>Steps</span><input type="number" min="100" max="100000" [(ngModel)]="draft.steps" name="tSteps"
              [placeholder]="t.coachDefaults.steps ? 'default ' + t.coachDefaults.steps : 'none'" /></label>
            <label><span>Water (ml)</span><input type="number" min="250" max="10000" [(ngModel)]="draft.waterMl" name="tWater"
              [placeholder]="t.coachDefaults.waterMl ? 'default ' + t.coachDefaults.waterMl : 'none'" /></label>
            <label><span>Sleep (hours)</span><input type="number" min="3" max="14" step="0.25" [(ngModel)]="draft.sleepHours" name="tSleep"
              [placeholder]="t.coachDefaults.sleepHours ? 'default ' + t.coachDefaults.sleepHours : 'none'" /></label>
          </div>
          <p class="muted">Leave a target empty to use your default from Workspace settings.</p>
          <button class="button button-primary" type="button" [disabled]="busy()" (click)="saveTargets()">Save targets</button>
        }
      </section>

      @if (lifestyle(); as l) {
        <section class="card workbench">
          <h2>Lifestyle</h2>
          <h3>Steps</h3>
          <app-trend-chart [points]="points(l, 'steps')" series="Steps" unit="steps" [target]="l.targets.steps" [bars]="true" label="Daily steps" />
          <h3>Sleep</h3>
          <app-trend-chart [points]="points(l, 'sleep')" series="Sleep" unit="hours" [target]="l.targets.sleepHours" label="Hours slept" />
          <h3>Water</h3>
          <app-trend-chart [points]="points(l, 'water')" series="Water" unit="ml" [target]="l.targets.waterMl" [bars]="true" label="Daily water" />
          <div class="table-scroll"><table>
            <thead><tr><th>Day</th><th>Sleep</th><th>Quality</th><th>Steps</th><th>Water ml</th></tr></thead>
            <tbody>@for (day of l.days.slice().reverse(); track day.date) {
              <tr><td>{{ day.date | date: 'EEE d MMM' }}</td><td>{{ day.sleep ? hours(day.sleep.hours) : '–' }}</td><td>{{ day.sleep?.quality ?? '–' }}</td>
                <td>{{ day.steps ? (day.steps.steps | number) : '–' }}</td><td>{{ day.waterMl || '–' }}</td></tr>
            }</tbody>
          </table></div>
        </section>
      }
    }

    @if (view === 'measurements') {
      @if (body(); as b) {
        <section class="card workbench">
          <h2>Weight</h2>
          <div class="report-metrics">
            <article><small>Starting</small><strong>{{ b.weight.summary.start?.value ?? b.profileStartingWeightKg ?? '–' }}</strong><span>kg</span></article>
            <article><small>Current</small><strong>{{ b.weight.summary.current?.value ?? '–' }}</strong><span>kg</span></article>
            <article><small>Lowest / highest</small><strong>{{ b.weight.summary.lowest?.value ?? '–' }} / {{ b.weight.summary.highest?.value ?? '–' }}</strong><span>kg</span></article>
            <article><small>Change</small><strong>{{ change(b.weight.summary, 'kg') }}</strong><span>since the first weigh-in</span></article>
          </div>
          <app-trend-chart [points]="b.weight.entries" series="Weight" unit="kg" label="Weight trend" />
          <div class="table-scroll"><table>
            <thead><tr><th>Week</th><th>Average kg</th><th>Weigh-ins</th></tr></thead>
            <tbody>@for (w of b.weight.summary.weeks.slice().reverse(); track w.weekStart) {
              <tr><td>{{ w.weekStart | date: 'd MMM' }}</td><td>{{ w.average }}</td><td>{{ w.count }}</td></tr>
            }</tbody>
          </table></div>
        </section>
        <section class="card workbench">
          <h2>Body fat</h2>
          <p class="muted">{{ notice }}</p>
          <p>Starting {{ b.bodyFat.summary.start?.value ?? b.profileStartingBodyFatPercent ?? '–' }}% · current {{ b.bodyFat.summary.current?.value ?? '–' }}% · change {{ change(b.bodyFat.summary, '%') }}</p>
          @if (b.bodyFat.entries.length) { <app-trend-chart [points]="b.bodyFat.entries" series="Body fat" unit="%" label="Body fat trend" /> }
        </section>
        <section class="card workbench">
          <h2>Measurements</h2>
          <div class="table-scroll"><table>
            <thead><tr><th>Site</th><th>Starting</th><th>Current</th><th>Difference</th><th>Entries</th></tr></thead>
            <tbody>@for (m of b.measurements; track m.site) {
              <tr [class.chosen]="m.site === site" (click)="site = m.site"><td>{{ m.site }}</td><td>{{ m.summary.start?.value ?? '–' }}</td>
                <td>{{ m.summary.current?.value ?? '–' }}</td><td>{{ change(m.summary, 'cm') }}</td><td>{{ m.summary.count }}</td></tr>
            }</tbody>
          </table></div>
          @if (siteEntries(b); as entries) {
            @if (entries.length) { <app-trend-chart [points]="entries" [series]="site" unit="cm" [label]="site + ' measurement trend'" /> }
          }
        </section>
      } @else if (loading()) { <p class="table-message">Loading…</p> }
    }

    @if (view === 'photos') {
      <section class="card workbench"><h2>Progress photos</h2><app-photo-compare [photos]="photos()" /></section>
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
  readonly notice = bodyFatNotice;
  readonly change = changeText;
  readonly percent = percentText;
  readonly hours = hoursText;
  readonly recordText = recordText;
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

  constructor(private readonly api: ProgressTrackingService) {}

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
