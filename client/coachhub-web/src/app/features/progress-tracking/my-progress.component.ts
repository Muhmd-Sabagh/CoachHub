import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, finalize } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { addDays, isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import { recordText } from '../training-tracking/training-tracking.service';
import { PhotoCompareComponent } from './photo-compare.component';
import {
  BodyProgress, Lifestyle, MeasurementSite, PhotoPose, ProgressDashboard, ProgressPhoto, ProgressTrackingService, SeriesSummary,
  bodyFatNotice, changeText, hoursText, measurementSites, percentText, photoPoses,
} from './progress-tracking.service';
import { TrendChartComponent } from './trend-chart.component';
import { ClientReportsComponent } from '../reports/client-reports.component';

type Section = 'overview' | 'body' | 'photos' | 'lifestyle';

/**
 * The client's progress (§24–§27, §31–§33, §37), mobile first: an overview of body, nutrition, training and lifestyle;
 * weigh-ins, body fat and measurements with their statistics and graphs; progress photos with comparison; and daily
 * sleep, steps and water against the coach's targets.
 */
@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-my-progress',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, TrendChartComponent, PhotoCompareComponent, ClientReportsComponent],
  template: `
    <section class="dashboard-hero"><div><p class="eyebrow">Progress</p><h1>My progress</h1></div>
      <a class="button button-secondary" routerLink="/app/tests">My tests</a></section>
    <nav class="segmented sections" role="tablist" aria-label="Progress sections">
      @for (s of sections; track s.id) {
        <button type="button" role="tab" [class.active]="section() === s.id" [attr.aria-selected]="section() === s.id" (click)="open(s.id)">{{ s.label }}</button>
      }
    </nav>
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (message()) { <p class="success-message" role="status">{{ message() }}</p> }

    @switch (section()) {
      @case ('overview') {
        @if (dashboard(); as d) {
          <p class="muted">Last 4 weeks</p>
          <div class="tiles">
            <article class="card workbench tile"><small>Weight</small><strong>{{ d.body.weight.current ? d.body.weight.current.value + ' kg' : '–' }}</strong>
              <span class="muted">{{ change(d.body.weight, 'kg') }} since start</span></article>
            <article class="card workbench tile"><small>Body fat</small><strong>{{ d.body.bodyFat.current ? d.body.bodyFat.current.value + '%' : '–' }}</strong>
              <span class="muted">{{ change(d.body.bodyFat, '%') }}</span></article>
            <article class="card workbench tile"><small>Nutrition adherence</small><strong>{{ percent(d.nutrition.adherencePercent) }}</strong>
              <span class="muted">{{ d.nutrition.loggedDays }} of {{ d.nutrition.days }} days logged</span></article>
            <article class="card workbench tile"><small>Workouts</small><strong>{{ d.training.summary.completedSessions }} / {{ d.training.summary.scheduledSessions }}</strong>
              <span class="muted">{{ d.training.summary.volumeKg | number: '1.0-0' }} kg volume</span></article>
            <article class="card workbench tile"><small>Sleep</small><strong>{{ hours(d.lifestyle.averageSleepHours) }}</strong>
              <span class="muted">goal {{ percent(d.lifestyle.sleepGoalPercent) }}</span></article>
            <article class="card workbench tile"><small>Steps</small><strong>{{ d.lifestyle.averageSteps !== null ? (d.lifestyle.averageSteps | number) : '–' }}</strong>
              <span class="muted">goal {{ percent(d.lifestyle.stepsGoalPercent) }}</span></article>
            <article class="card workbench tile"><small>Water</small><strong>{{ d.lifestyle.averageWaterMl !== null ? (d.lifestyle.averageWaterMl | number) + ' ml' : '–' }}</strong>
              <span class="muted">goal {{ percent(d.lifestyle.waterGoalPercent) }}</span></article>
            <article class="card workbench tile"><small>Overall adherence</small><strong>{{ percent(d.adherence.overallPercent) }}</strong>
              <span class="muted">weighted by your coach</span></article>
            <article class="card workbench tile"><small>Check-ins</small><strong>{{ d.checkIn.adherence.completed }} / {{ d.checkIn.adherence.due }}</strong>
              <span class="muted">{{ d.checkIn.schedule?.nextDueOn ? 'next ' + (d.checkIn.schedule!.nextDueOn | date: 'd MMM') : 'none scheduled' }}</span></article>
            <article class="card workbench tile"><small>Photos</small><strong>{{ d.body.photoCount }}</strong>
              <span class="muted">{{ d.body.latestPhoto ? 'latest ' + (d.body.latestPhoto.date | date: 'd MMM') : 'none yet' }}</span></article>
          </div>
          @if (d.checkIn.latestFeedback; as f) {
            <section class="card workbench"><h2>Latest from your coach</h2><p>{{ f.text }}</p>
              <small class="muted">{{ f.createdAt | date: 'd MMM, HH:mm' }}</small></section>
          }
          @if (d.training.recentRecords.length) {
            <section class="card workbench"><h2>Recent PRs</h2>
              <ul class="plain-list">@for (r of d.training.recentRecords; track r.id) { <li>{{ recordText(r) }}</li> }</ul></section>
          }
        } @else if (loading()) { <p class="table-message">Loading…</p> }
      }

      @case ('body') {
        <section class="card workbench entry">
          <h2>Record</h2>
          <label><span>Day</span><input type="date" [(ngModel)]="date" name="bodyDate" [max]="tomorrow" /></label>
          <div class="entry-row">
            <label><span>Weight (kg)</span><input type="number" inputmode="decimal" min="20" max="400" step="0.1" [(ngModel)]="weightKg" name="weightKg" /></label>
            <button class="button button-primary" type="button" [disabled]="busy() || !weightKg" (click)="saveWeight()">Save weight</button>
          </div>
          <div class="entry-row">
            <label><span>Body fat (%)</span><input type="number" inputmode="decimal" min="2" max="75" step="0.1" [(ngModel)]="bodyFat" name="bodyFat" /></label>
            <button class="button button-secondary" type="button" [disabled]="busy() || !bodyFat" (click)="saveBodyFat()">Save body fat</button>
          </div>
          <small class="muted">{{ notice }}</small>
          <fieldset class="sites"><legend>Measurements (cm)</legend>
            @for (site of sites; track site) {
              <label><span>{{ site }}</span><input type="number" inputmode="decimal" min="5" max="300" step="0.1" [(ngModel)]="measurements[site]" [name]="'m' + site" /></label>
            }
          </fieldset>
          <button class="button button-secondary" type="button" [disabled]="busy() || !hasMeasurements()" (click)="saveMeasurements()">Save measurements</button>
        </section>

        @if (body(); as b) {
          <section class="card workbench">
            <h2>Weight</h2>
            <div class="stats">
              <div><small>Starting</small><strong>{{ b.weight.summary.start?.value ?? b.profileStartingWeightKg ?? '–' }}</strong></div>
              <div><small>Current</small><strong>{{ b.weight.summary.current?.value ?? '–' }}</strong></div>
              <div><small>Lowest</small><strong>{{ b.weight.summary.lowest?.value ?? '–' }}</strong></div>
              <div><small>Highest</small><strong>{{ b.weight.summary.highest?.value ?? '–' }}</strong></div>
              <div><small>Change</small><strong>{{ change(b.weight.summary, 'kg') }}</strong></div>
              <div><small>This week's average</small><strong>{{ b.weight.summary.weeks.at(-1)?.average ?? '–' }}</strong></div>
            </div>
            <app-trend-chart [points]="b.weight.entries" series="Weight" unit="kg" label="Weight trend" />
            <ul class="plain-list">
              @for (e of b.weight.entries.slice().reverse(); track e.id) {
                <li class="line"><span>{{ e.date | date: 'EEE d MMM' }} · {{ e.value }} kg</span>
                  <button class="link-button danger" type="button" [disabled]="busy()" (click)="deleteWeight(e.date)">Delete</button></li>
              }
            </ul>
          </section>
          <section class="card workbench">
            <h2>Body fat</h2>
            <p class="muted">{{ notice }}</p>
            <div class="stats">
              <div><small>Starting</small><strong>{{ b.bodyFat.summary.start?.value ?? b.profileStartingBodyFatPercent ?? '–' }}</strong></div>
              <div><small>Current</small><strong>{{ b.bodyFat.summary.current?.value ?? '–' }}</strong></div>
              <div><small>Change</small><strong>{{ change(b.bodyFat.summary, '%') }}</strong></div>
            </div>
            @if (b.bodyFat.entries.length) { <app-trend-chart [points]="b.bodyFat.entries" series="Body fat" unit="%" label="Body fat trend" /> }
          </section>
          <section class="card workbench">
            <h2>Measurements</h2>
            <div class="table-scroll"><table>
              <thead><tr><th>Site</th><th>Starting</th><th>Current</th><th>Difference</th></tr></thead>
              <tbody>
                @for (m of b.measurements; track m.site) {
                  <tr [class.chosen]="m.site === chartSite" (click)="chartSite = m.site"><td>{{ m.site }}</td>
                    <td>{{ m.summary.start?.value ?? '–' }}</td><td>{{ m.summary.current?.value ?? '–' }}</td><td>{{ change(m.summary, 'cm') }}</td></tr>
                }
              </tbody>
            </table></div>
            @if (siteSeries(b); as s) {
              @if (s.entries.length) { <app-trend-chart [points]="s.entries" [series]="chartSite" unit="cm" [label]="chartSite + ' measurement trend'" /> }
            }
          </section>
        } @else if (loading()) { <p class="table-message">Loading…</p> }
      }

      @case ('photos') {
        <section class="card workbench entry">
          <h2>Add a photo</h2>
          <div class="entry-row">
            <label><span>Day</span><input type="date" [(ngModel)]="photoDate" name="photoDate" [max]="tomorrow" /></label>
            <label><span>Pose</span><select [(ngModel)]="pose" name="pose">@for (p of poses; track p) { <option [ngValue]="p">{{ p }}</option> }</select></label>
            @if (pose === 'Custom') { <label><span>Label</span><input [(ngModel)]="poseLabel" name="poseLabel" maxlength="50" /></label> }
          </div>
          <label class="file"><span>Photo</span><input type="file" accept="image/jpeg,image/png,image/webp" capture="environment" (change)="pick($event)" /></label>
          <button class="button button-primary" type="button" [disabled]="busy() || !file" (click)="upload()">Upload</button>
          <small class="muted">Only you and your coach can see your photos.</small>
        </section>
        <section class="card workbench"><app-photo-compare [photos]="photos()" [deletable]="true" (deleted)="deletePhoto($event)" /></section>
      }

      @case ('lifestyle') {
        <section class="card workbench entry">
          <h2>Today</h2>
          <label><span>Day</span><input type="date" [(ngModel)]="date" name="lifestyleDate" [max]="tomorrow" (ngModelChange)="loadLifestyle()" /></label>
          @if (lifestyle(); as l) {
            <p class="muted">Targets: {{ l.targets.steps ? (l.targets.steps | number) + ' steps' : 'no steps target' }} ·
              {{ l.targets.waterMl ? (l.targets.waterMl | number) + ' ml water' : 'no water target' }} ·
              {{ l.targets.sleepHours ? hours(l.targets.sleepHours) + ' sleep' : 'no sleep target' }}</p>
            @if (dayOf(l); as day) {
              <div class="water">
                <strong>Water {{ day.waterMl | number }} ml</strong>
                @if (l.targets.waterMl) { <span class="muted">· {{ remaining(l.targets.waterMl, day.waterMl) }} ml to go</span> }
                <div class="bar" role="progressbar" [attr.aria-valuenow]="day.waterCompletionPercent ?? 0" aria-valuemin="0" aria-valuemax="100" aria-label="Water">
                  <span [style.width.%]="day.waterCompletionPercent ?? 0"></span></div>
                <div class="entry-row">
                  <button class="button button-secondary" type="button" [disabled]="busy()" (click)="addWater(250)">+250 ml</button>
                  <button class="button button-secondary" type="button" [disabled]="busy()" (click)="addWater(500)">+500 ml</button>
                  <input type="number" min="1" max="10000" [(ngModel)]="waterMl" name="waterMl" aria-label="Water in ml" />
                  <button class="button button-secondary" type="button" [disabled]="busy() || !waterMl" (click)="addWater(waterMl!)">Add</button>
                </div>
                <ul class="plain-list">
                  @for (w of day.water; track w.id) {
                    <li class="line"><span>{{ w.amountMl }} ml · {{ w.createdAt | date: 'shortTime' }}</span>
                      <button class="link-button danger" type="button" [disabled]="busy()" (click)="deleteWater(w.id)">Delete</button></li>
                  }
                </ul>
              </div>
            }
          }
          <div class="entry-row">
            <label><span>Steps</span><input type="number" inputmode="numeric" min="0" max="200000" [(ngModel)]="steps" name="steps" /></label>
            <button class="button button-primary" type="button" [disabled]="busy() || steps === null" (click)="saveSteps()">Save steps</button>
          </div>
          <div class="entry-row">
            <label><span>Bedtime</span><input type="time" [(ngModel)]="bedtime" name="bedtime" /></label>
            <label><span>Wake time</span><input type="time" [(ngModel)]="wakeTime" name="wakeTime" /></label>
            <label><span>Or hours</span><input type="number" min="0" max="24" step="0.25" [(ngModel)]="sleepHours" name="sleepHours" /></label>
            <label><span>Quality (1–5)</span><input type="number" min="1" max="5" [(ngModel)]="sleepQuality" name="sleepQuality" /></label>
            <button class="button button-primary" type="button" [disabled]="busy() || !(sleepHours || (bedtime && wakeTime))" (click)="saveSleep()">Save sleep</button>
          </div>
        </section>
        @if (lifestyle(); as l) {
          <section class="card workbench">
            <h2>Last 14 days</h2>
            <div class="stats">
              <div><small>Average sleep</small><strong>{{ hours(l.summary.averageSleepHours) }}</strong><span class="muted">goal {{ percent(l.summary.sleepGoalPercent) }}</span></div>
              <div><small>Average steps</small><strong>{{ l.summary.averageSteps !== null ? (l.summary.averageSteps | number) : '–' }}</strong><span class="muted">goal {{ percent(l.summary.stepsGoalPercent) }}</span></div>
              <div><small>Average water</small><strong>{{ l.summary.averageWaterMl !== null ? (l.summary.averageWaterMl | number) + ' ml' : '–' }}</strong><span class="muted">goal {{ percent(l.summary.waterGoalPercent) }}</span></div>
            </div>
            <h3>Steps</h3>
            <app-trend-chart [points]="stepsPoints(l)" series="Steps" unit="steps" [target]="l.targets.steps" [bars]="true" label="Daily steps" />
            <h3>Sleep</h3>
            <app-trend-chart [points]="sleepPoints(l)" series="Sleep" unit="hours" [target]="l.targets.sleepHours" label="Hours slept" />
            <h3>Water</h3>
            <app-trend-chart [points]="waterPoints(l)" series="Water" unit="ml" [target]="l.targets.waterMl" [bars]="true" label="Daily water" />
          </section>
        }
      }
    }
    <app-client-reports />
  `,
  styles: [`
    .sections { display: flex; margin: 0 0 12px; overflow-x: auto; border: 1px solid var(--gray-200); border-radius: 8px; }
    .sections button { flex: 1 0 auto; min-height: 44px; padding: 6px 12px; border: 0; background: transparent; color: inherit; }
    .sections button.active { background: var(--brand-soft); color: var(--brand-dark); font-weight: 600; }
    .card.workbench { margin-bottom: 12px; }
    .tiles { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; margin-bottom: 12px; }
    @media (min-width: 640px) { .tiles { grid-template-columns: repeat(4, minmax(0, 1fr)); } }
    .tile { display: grid; gap: 4px; margin: 0 !important; }
    .tile strong { font-size: 1.3rem; }
    .entry { display: grid; gap: 10px; }
    .entry label { display: grid; gap: 4px; }
    .entry-row { display: flex; flex-wrap: wrap; gap: 8px; align-items: end; }
    .entry-row input { width: 120px; min-height: 44px; }
    .entry-row .button { min-height: 44px; }
    .sites { display: grid; grid-template-columns: repeat(auto-fill, minmax(100px, 1fr)); gap: 8px; border: 0; padding: 0; margin: 0; }
    .sites input { width: 100%; min-height: 44px; }
    .stats { display: grid; grid-template-columns: repeat(auto-fill, minmax(110px, 1fr)); gap: 10px; margin-bottom: 8px; }
    .stats div { display: grid; gap: 2px; }
    .stats strong { font-size: 1.15rem; }
    .plain-list { list-style: none; margin: 8px 0 0; padding: 0; display: grid; gap: 6px; }
    .line { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 6px; }
    tr.chosen td { background: var(--brand-soft); }
    tbody tr { cursor: pointer; }
    .water { display: grid; gap: 8px; }
    .bar { height: 10px; border-radius: 999px; background: var(--gray-100); overflow: hidden; }
    .bar span { display: block; height: 100%; border-radius: 999px; background: var(--brand); }
  `],
})
export class MyProgressComponent implements OnInit {
  readonly sections: { id: Section; label: string }[] = [
    { id: 'overview', label: 'Overview' }, { id: 'body', label: 'Body' }, { id: 'photos', label: 'Photos' }, { id: 'lifestyle', label: 'Lifestyle' },
  ];
  readonly sites = measurementSites;
  readonly poses = photoPoses;
  readonly notice = bodyFatNotice;
  readonly today = isoDate(new Date());
  readonly tomorrow = addDays(this.today, 1);
  readonly change = changeText;
  readonly percent = percentText;
  readonly hours = hoursText;
  readonly recordText = recordText;
  readonly section = signal<Section>('overview');
  readonly dashboard = signal<ProgressDashboard | null>(null);
  readonly body = signal<BodyProgress | null>(null);
  readonly photos = signal<ProgressPhoto[]>([]);
  readonly lifestyle = signal<Lifestyle | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly message = signal('');

  date = this.today;
  weightKg: number | null = null;
  bodyFat: number | null = null;
  measurements: Partial<Record<MeasurementSite, number | null>> = {};
  chartSite: MeasurementSite = 'Waist';
  photoDate = this.today;
  pose: PhotoPose = 'Front';
  poseLabel = '';
  file: File | null = null;
  steps: number | null = null;
  bedtime = '';
  wakeTime = '';
  sleepHours: number | null = null;
  sleepQuality: number | null = null;
  waterMl: number | null = null;

  constructor(private readonly api: ProgressTrackingService) {}

  ngOnInit(): void {
    this.open('overview');
  }

  open(section: Section): void {
    this.section.set(section);
    this.message.set('');
    if (section === 'overview') this.load(this.api.dashboard(addDays(this.today, -27), this.today), d => this.dashboard.set(d));
    if (section === 'body') this.loadBody();
    if (section === 'photos') this.load(this.api.photos(), p => this.photos.set(p));
    if (section === 'lifestyle') this.loadLifestyle();
  }

  hasMeasurements(): boolean {
    return Object.values(this.measurements).some(x => x !== null && x !== undefined);
  }

  siteSeries(body: BodyProgress) {
    return body.measurements.find(x => x.site === this.chartSite) ?? null;
  }

  saveWeight(): void {
    this.save(this.api.setWeight(this.date, this.weightKg!), 'Weight saved.', () => this.loadBody());
  }

  deleteWeight(date: string): void {
    this.save(this.api.deleteWeight(date), 'Weigh-in deleted.', () => this.loadBody());
  }

  saveBodyFat(): void {
    this.save(this.api.setBodyFat(this.date, this.bodyFat!), 'Body fat saved.', () => this.loadBody());
  }

  saveMeasurements(): void {
    const values = this.sites.filter(site => this.measurements[site] !== null && this.measurements[site] !== undefined)
      .map(site => ({ site, valueCm: this.measurements[site]! }));
    this.save(this.api.setMeasurements(this.date, values), 'Measurements saved.', () => { this.measurements = {}; this.loadBody(); });
  }

  pick(event: Event): void {
    this.file = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  upload(): void {
    if (!this.file) return;
    this.save(this.api.uploadPhoto(this.file, this.photoDate, this.pose, this.pose === 'Custom' ? this.poseLabel : null), 'Photo added.', () => {
      this.file = null;
      this.open('photos');
    });
  }

  deletePhoto(photo: ProgressPhoto): void {
    this.save(this.api.deletePhoto(photo.id), 'Photo deleted.', () => this.open('photos'));
  }

  dayOf(lifestyle: Lifestyle) {
    return lifestyle.days.find(x => x.date === this.date) ?? null;
  }

  remaining(target: number, consumed: number): number {
    return Math.max(0, target - consumed);
  }

  addWater(ml: number): void {
    this.save(this.api.addWater(this.date, ml), '', () => { this.waterMl = null; this.loadLifestyle(); });
  }

  deleteWater(id: string): void {
    this.save(this.api.deleteWater(id), '', () => this.loadLifestyle());
  }

  saveSteps(): void {
    this.save(this.api.setSteps(this.date, this.steps!), 'Steps saved.', () => this.loadLifestyle());
  }

  saveSleep(): void {
    const times = this.bedtime && this.wakeTime;
    this.save(this.api.setSleep(this.date, {
      hours: this.sleepHours || null, quality: this.sleepQuality || null,
      bedtime: times ? this.bedtime : null, wakeTime: times ? this.wakeTime : null,
    }), 'Sleep saved.', () => this.loadLifestyle());
  }

  stepsPoints(l: Lifestyle) { return l.days.map(x => ({ date: x.date, value: x.steps?.steps ?? null })); }
  sleepPoints(l: Lifestyle) { return l.days.map(x => ({ date: x.date, value: x.sleep?.hours ?? null })); }
  waterPoints(l: Lifestyle) { return l.days.map(x => ({ date: x.date, value: x.waterMl || null })); }

  loadLifestyle(): void {
    const to = this.date > this.today ? this.date : this.today;
    this.load(this.api.lifestyle(addDays(to, -13), to), l => {
      this.lifestyle.set(l);
      const day = l.days.find(x => x.date === this.date);
      this.steps = day?.steps?.steps ?? null;
      this.sleepHours = day?.sleep?.hours ?? null;
      this.sleepQuality = day?.sleep?.quality ?? null;
      this.bedtime = day?.sleep?.bedtime?.slice(0, 5) ?? '';
      this.wakeTime = day?.sleep?.wakeTime?.slice(0, 5) ?? '';
    });
  }

  private loadBody(): void {
    this.load(this.api.body(addDays(this.today, -89), this.today), b => this.body.set(b));
  }

  private load<T>(request: Observable<T>, next: (value: T) => void): void {
    this.loading.set(true);
    this.error.set('');
    request.pipe(finalize(() => this.loading.set(false))).subscribe({ next, error: e => this.error.set(apiErrorMessage(e)) });
  }

  private save(request: Observable<unknown>, message: string, after: () => void): void {
    this.busy.set(true);
    this.error.set('');
    this.message.set('');
    request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { this.message.set(message); after(); },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }
}
