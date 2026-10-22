import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, finalize } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { addDays, isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import { PersonalRecord, recordText } from '../training-tracking/training-tracking.service';
import { PhotoCompareComponent } from './photo-compare.component';
import {
  BodyProgress, Lifestyle, MeasurementSite, PhotoPose, ProgressDashboard, ProgressPhoto, ProgressTrackingService, SeriesSummary,
  changeText, hoursText, measurementSites, percentText, photoPoses,
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
  selector: 'app-my-progress',
  standalone: true,
  imports: [FormsModule, RouterLink, TrendChartComponent, PhotoCompareComponent, ClientReportsComponent, ...I18N_PIPES],
  template: `
    <section class="dashboard-hero"><div><p class="eyebrow">{{ 'progress.eyebrow' | translate }}</p><h1>{{ 'progress.title' | translate }}</h1></div>
      <a class="button button-secondary" routerLink="/app/tests">{{ 'progress.myTests' | translate }}</a></section>
    <nav class="segmented sections" role="tablist" [attr.aria-label]="'progress.sections' | translate">
      @for (s of sections; track s.id) {
        <button type="button" role="tab" [class.active]="section() === s.id" [attr.aria-selected]="section() === s.id" (click)="open(s.id)">{{ s.label | translate }}</button>
      }
    </nav>
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (message()) { <p class="success-message" role="status">{{ message() }}</p> }

    @switch (section()) {
      @case ('overview') {
        @if (dashboard(); as d) {
          <p class="muted">{{ 'progress.last4Weeks' | translate }}</p>
          <div class="tiles">
            <article class="card workbench tile"><small>{{ 'progress.weight' | translate }}</small><strong>{{ d.body.weight.current ? (d.body.weight.current.value | localNumber: '1.0-1') + ' ' + ('unit.kg' | translate) : '–' }}</strong>
              <span class="muted">{{ 'progress.sinceStart' | translate: { change: change(d.body.weight, 'unit.kg') } }}</span></article>
            <article class="card workbench tile"><small>{{ 'progress.bodyFat' | translate }}</small><strong>{{ d.body.bodyFat.current ? (d.body.bodyFat.current.value | localNumber: '1.0-1') + '%' : '–' }}</strong>
              <span class="muted">{{ change(d.body.bodyFat, 'unit.percent') }}</span></article>
            <article class="card workbench tile"><small>{{ 'progress.nutritionAdherence' | translate }}</small><strong>{{ percent(d.nutrition.adherencePercent) }}</strong>
              <span class="muted">{{ 'progress.daysLogged' | translate: { logged: d.nutrition.loggedDays, days: d.nutrition.days } }}</span></article>
            <article class="card workbench tile"><small>{{ 'progress.workouts' | translate }}</small><strong>{{ d.training.summary.completedSessions }} / {{ d.training.summary.scheduledSessions }}</strong>
              <span class="muted">{{ 'progress.volume' | translate: { value: (d.training.summary.volumeKg | localNumber: '1.0-0') } }}</span></article>
            <article class="card workbench tile"><small>{{ 'progress.sleep' | translate }}</small><strong>{{ hours(d.lifestyle.averageSleepHours) }}</strong>
              <span class="muted">{{ 'progress.goal' | translate: { value: percent(d.lifestyle.sleepGoalPercent) } }}</span></article>
            <article class="card workbench tile"><small>{{ 'progress.steps' | translate }}</small><strong>{{ d.lifestyle.averageSteps !== null ? (d.lifestyle.averageSteps | localNumber) : '–' }}</strong>
              <span class="muted">{{ 'progress.goal' | translate: { value: percent(d.lifestyle.stepsGoalPercent) } }}</span></article>
            <article class="card workbench tile"><small>{{ 'progress.water' | translate }}</small><strong>{{ d.lifestyle.averageWaterMl !== null ? (d.lifestyle.averageWaterMl | localNumber) + ' ' + ('unit.ml' | translate) : '–' }}</strong>
              <span class="muted">{{ 'progress.goal' | translate: { value: percent(d.lifestyle.waterGoalPercent) } }}</span></article>
            <article class="card workbench tile"><small>{{ 'progress.overallAdherence' | translate }}</small><strong>{{ percent(d.adherence.overallPercent) }}</strong>
              <span class="muted">{{ 'progress.weightedByCoach' | translate }}</span></article>
            <article class="card workbench tile"><small>{{ 'progress.checkIns' | translate }}</small><strong>{{ d.checkIn.adherence.completed }} / {{ d.checkIn.adherence.due }}</strong>
              <span class="muted">{{ d.checkIn.schedule?.nextDueOn ? ('progress.nextDue' | translate: { date: (d.checkIn.schedule!.nextDueOn | localDate: 'd MMM') }) : ('progress.noneScheduled' | translate) }}</span></article>
            <article class="card workbench tile"><small>{{ 'progress.photos' | translate }}</small><strong>{{ d.body.photoCount }}</strong>
              <span class="muted">{{ d.body.latestPhoto ? ('progress.latest' | translate: { date: (d.body.latestPhoto.date | localDate: 'd MMM') }) : ('progress.noneYet' | translate) }}</span></article>
          </div>
          @if (d.checkIn.latestFeedback; as f) {
            <section class="card workbench"><h2>{{ 'progress.latestFromCoach' | translate }}</h2><p translate="no">{{ f.text }}</p>
              <small class="muted">{{ f.createdAt | localDate: 'd MMM, HH:mm' }}</small></section>
          }
          @if (d.training.recentRecords.length) {
            <section class="card workbench"><h2>{{ 'progress.recentPrs' | translate }}</h2>
              <ul class="plain-list">@for (r of d.training.recentRecords; track r.id) { <li>{{ recordText(r) }}</li> }</ul></section>
          }
        } @else if (loading()) { <p class="table-message">{{ 'common.loading' | translate }}</p> }
      }

      @case ('body') {
        <section class="card workbench entry">
          <h2>{{ 'progress.record' | translate }}</h2>
          <label><span>{{ 'progress.day' | translate }}</span><input type="date" [(ngModel)]="date" name="bodyDate" [max]="tomorrow" /></label>
          <div class="entry-row">
            <label><span>{{ 'progress.weightKg' | translate }}</span><input type="number" inputmode="decimal" min="20" max="400" step="0.1" [(ngModel)]="weightKg" name="weightKg" /></label>
            <button class="button button-primary" type="button" [disabled]="busy() || !weightKg()" (click)="saveWeight()">{{ 'progress.saveWeight' | translate }}</button>
          </div>
          <div class="entry-row">
            <label><span>{{ 'progress.bodyFatPercent' | translate }}</span><input type="number" inputmode="decimal" min="2" max="75" step="0.1" [(ngModel)]="bodyFat" name="bodyFat" /></label>
            <button class="button button-secondary" type="button" [disabled]="busy() || !bodyFat()" (click)="saveBodyFat()">{{ 'progress.saveBodyFat' | translate }}</button>
          </div>
          <small class="muted">{{ 'progress.bodyFatNotice' | translate }}</small>
          <fieldset class="sites"><legend>{{ 'progress.measurementsCm' | translate }}</legend>
            @for (site of sites; track site) {
              <label><span>{{ site | enumLabel: 'measurementSite' }}</span><input type="number" inputmode="decimal" min="5" max="300" step="0.1" [(ngModel)]="measurements()[site]" [name]="'m' + site" /></label>
            }
          </fieldset>
          <button class="button button-secondary" type="button" [disabled]="busy() || !hasMeasurements()" (click)="saveMeasurements()">{{ 'progress.saveMeasurements' | translate }}</button>
        </section>

        @if (body(); as b) {
          <section class="card workbench">
            <h2>{{ 'progress.weight' | translate }}</h2>
            <div class="stats">
              <div><small>{{ 'progress.starting' | translate }}</small><strong>{{ b.weight.summary.start?.value ?? b.profileStartingWeightKg ?? '–' }}</strong></div>
              <div><small>{{ 'progress.current' | translate }}</small><strong>{{ b.weight.summary.current?.value ?? '–' }}</strong></div>
              <div><small>{{ 'progress.lowest' | translate }}</small><strong>{{ b.weight.summary.lowest?.value ?? '–' }}</strong></div>
              <div><small>{{ 'progress.highest' | translate }}</small><strong>{{ b.weight.summary.highest?.value ?? '–' }}</strong></div>
              <div><small>{{ 'progress.change' | translate }}</small><strong>{{ change(b.weight.summary, 'unit.kg') }}</strong></div>
              <div><small>{{ 'progress.weekAverage' | translate }}</small><strong>{{ b.weight.summary.weeks.at(-1)?.average ?? '–' }}</strong></div>
            </div>
            <app-trend-chart [points]="b.weight.entries" [series]="'progress.weight' | translate" [unit]="'unit.kg' | translate" [label]="'progress.weightTrend' | translate" />
            <ul class="plain-list">
              @for (e of b.weight.entries.slice().reverse(); track e.id) {
                <li class="line"><span>{{ e.date | localDate: 'EEE d MMM' }} · {{ e.value | localNumber: '1.0-2' }} {{ 'unit.kg' | translate }}</span>
                  <button class="link-button danger" type="button" [disabled]="busy()" (click)="deleteWeight(e.date)">{{ 'progress.delete' | translate }}</button></li>
              }
            </ul>
          </section>
          <section class="card workbench">
            <h2>{{ 'progress.bodyFat' | translate }}</h2>
            <p class="muted">{{ 'progress.bodyFatNotice' | translate }}</p>
            <div class="stats">
              <div><small>{{ 'progress.starting' | translate }}</small><strong>{{ b.bodyFat.summary.start?.value ?? b.profileStartingBodyFatPercent ?? '–' }}</strong></div>
              <div><small>{{ 'progress.current' | translate }}</small><strong>{{ b.bodyFat.summary.current?.value ?? '–' }}</strong></div>
              <div><small>{{ 'progress.change' | translate }}</small><strong>{{ change(b.bodyFat.summary, 'unit.percent') }}</strong></div>
            </div>
            @if (b.bodyFat.entries.length) { <app-trend-chart [points]="b.bodyFat.entries" [series]="'progress.bodyFat' | translate" unit="%" [label]="'progress.bodyFatTrend' | translate" /> }
          </section>
          <section class="card workbench">
            <h2>{{ 'progress.measurements' | translate }}</h2>
            <div class="table-scroll"><table class="compact-table">
              <thead><tr><th>{{ 'progress.site' | translate }}</th><th>{{ 'progress.starting' | translate }}</th><th>{{ 'progress.current' | translate }}</th><th>{{ 'progress.difference' | translate }}</th></tr></thead>
              <tbody>
                @for (m of b.measurements; track m.site) {
                  <tr [class.chosen]="m.site === chartSite()" (click)="chartSite.set(m.site)"><td>{{ m.site | enumLabel: 'measurementSite' }}</td>
                    <td>{{ m.summary.start?.value ?? '–' }}</td><td>{{ m.summary.current?.value ?? '–' }}</td><td>{{ change(m.summary, 'unit.cm') }}</td></tr>
                }
              </tbody>
            </table></div>
            @if (siteSeries(b); as s) {
              @if (s.entries.length) { <app-trend-chart [points]="s.entries" [series]="chartSite() | enumLabel: 'measurementSite'" [unit]="'unit.cm' | translate" [label]="'progress.siteTrend' | translate: { site: (chartSite() | enumLabel: 'measurementSite') }" /> }
            }
          </section>
        } @else if (loading()) { <p class="table-message">{{ 'common.loading' | translate }}</p> }
      }

      @case ('photos') {
        <section class="card workbench entry">
          <h2>{{ 'progress.addPhoto' | translate }}</h2>
          <div class="entry-row">
            <label><span>{{ 'progress.day' | translate }}</span><input type="date" [(ngModel)]="photoDate" name="photoDate" [max]="tomorrow" /></label>
            <label><span>{{ 'progress.pose' | translate }}</span><select [(ngModel)]="pose" name="pose">@for (p of poses; track p) { <option [ngValue]="p">{{ p | enumLabel: 'photoPose' }}</option> }</select></label>
            @if (pose() === 'Custom') { <label><span>{{ 'progress.label' | translate }}</span><input [(ngModel)]="poseLabel" name="poseLabel" maxlength="50" /></label> }
          </div>
          <label class="file"><span>{{ 'progress.photo' | translate }}</span><input type="file" accept="image/jpeg,image/png,image/webp" capture="environment" (change)="pick($event)" /></label>
          <button class="button button-primary" type="button" [disabled]="busy() || !file()" (click)="upload()">{{ 'progress.upload' | translate }}</button>
          <small class="muted">{{ 'progress.photosPrivate' | translate }}</small>
        </section>
        <section class="card workbench"><app-photo-compare [photos]="photos()" [deletable]="true" (deleted)="deletePhoto($event)" /></section>
      }

      @case ('lifestyle') {
        <section class="card workbench entry">
          <h2>{{ 'progress.today' | translate }}</h2>
          <label><span>{{ 'progress.day' | translate }}</span><input type="date" [(ngModel)]="date" name="lifestyleDate" [max]="tomorrow" (ngModelChange)="loadLifestyle()" /></label>
          @if (lifestyle(); as l) {
            <p class="muted">{{ 'progress.targets' | translate: {
              steps: l.targets.steps ? ('progress.stepsTarget' | translate: { value: l.targets.steps }) : ('progress.noStepsTarget' | translate),
              water: l.targets.waterMl ? ('progress.waterTarget' | translate: { value: l.targets.waterMl }) : ('progress.noWaterTarget' | translate),
              sleep: l.targets.sleepHours ? ('progress.sleepTarget' | translate: { value: hours(l.targets.sleepHours) }) : ('progress.noSleepTarget' | translate) } }}</p>
            @if (dayOf(l); as day) {
              <div class="water">
                <strong>{{ 'progress.waterToday' | translate: { value: day.waterMl } }}</strong>
                @if (l.targets.waterMl) { <span class="muted">{{ 'progress.toGo' | translate: { value: remaining(l.targets.waterMl, day.waterMl) } }}</span> }
                <div class="bar" role="progressbar" [attr.aria-valuenow]="day.waterCompletionPercent ?? 0" aria-valuemin="0" aria-valuemax="100" [attr.aria-label]="'progress.water' | translate">
                  <span [style.width.%]="day.waterCompletionPercent ?? 0"></span></div>
                <div class="entry-row">
                  <button class="button button-secondary" type="button" [disabled]="busy()" (click)="addWater(250)">{{ 'progress.add250' | translate }}</button>
                  <button class="button button-secondary" type="button" [disabled]="busy()" (click)="addWater(500)">{{ 'progress.add500' | translate }}</button>
                  <input type="number" min="1" max="10000" [(ngModel)]="waterMl" name="waterMl" [attr.aria-label]="'progress.waterMl' | translate" />
                  <button class="button button-secondary" type="button" [disabled]="busy() || !waterMl()" (click)="addWater(waterMl()!)">{{ 'progress.add' | translate }}</button>
                </div>
                <ul class="plain-list">
                  @for (w of day.water; track w.id) {
                    <li class="line"><span>{{ w.amountMl | localNumber }} {{ 'unit.ml' | translate }} · {{ w.createdAt | localDate: 'shortTime' }}</span>
                      <button class="link-button danger" type="button" [disabled]="busy()" (click)="deleteWater(w.id)">{{ 'progress.delete' | translate }}</button></li>
                  }
                </ul>
              </div>
            }
          }
          <div class="entry-row">
            <label><span>{{ 'progress.steps' | translate }}</span><input type="number" inputmode="numeric" min="0" max="200000" [(ngModel)]="steps" name="steps" /></label>
            <button class="button button-primary" type="button" [disabled]="busy() || steps() === null" (click)="saveSteps()">{{ 'progress.saveSteps' | translate }}</button>
          </div>
          <div class="entry-row">
            <label><span>{{ 'progress.bedtime' | translate }}</span><input type="time" [(ngModel)]="bedtime" name="bedtime" /></label>
            <label><span>{{ 'progress.wakeTime' | translate }}</span><input type="time" [(ngModel)]="wakeTime" name="wakeTime" /></label>
            <label><span>{{ 'progress.orHours' | translate }}</span><input type="number" min="0" max="24" step="0.25" [(ngModel)]="sleepHours" name="sleepHours" /></label>
            <label><span>{{ 'progress.quality' | translate }}</span><input type="number" min="1" max="5" [(ngModel)]="sleepQuality" name="sleepQuality" /></label>
            <button class="button button-primary" type="button" [disabled]="busy() || !(sleepHours() || (bedtime() && wakeTime()))" (click)="saveSleep()">{{ 'progress.saveSleep' | translate }}</button>
          </div>
        </section>
        @if (lifestyle(); as l) {
          <section class="card workbench">
            <h2>{{ 'progress.last14' | translate }}</h2>
            <div class="stats">
              <div><small>{{ 'progress.averageSleep' | translate }}</small><strong>{{ hours(l.summary.averageSleepHours) }}</strong><span class="muted">{{ 'progress.goal' | translate: { value: percent(l.summary.sleepGoalPercent) } }}</span></div>
              <div><small>{{ 'progress.averageSteps' | translate }}</small><strong>{{ l.summary.averageSteps !== null ? (l.summary.averageSteps | localNumber) : '–' }}</strong><span class="muted">{{ 'progress.goal' | translate: { value: percent(l.summary.stepsGoalPercent) } }}</span></div>
              <div><small>{{ 'progress.averageWater' | translate }}</small><strong>{{ l.summary.averageWaterMl !== null ? (l.summary.averageWaterMl | localNumber) + ' ' + ('unit.ml' | translate) : '–' }}</strong><span class="muted">{{ 'progress.goal' | translate: { value: percent(l.summary.waterGoalPercent) } }}</span></div>
            </div>
            <h3>{{ 'progress.steps' | translate }}</h3>
            <app-trend-chart [points]="stepsPoints(l)" [series]="'progress.steps' | translate" [unit]="'unit.steps' | translate" [target]="l.targets.steps" [bars]="true" [label]="'progress.dailySteps' | translate" />
            <h3>{{ 'progress.sleep' | translate }}</h3>
            <app-trend-chart [points]="sleepPoints(l)" [series]="'progress.sleep' | translate" [unit]="'progress.hoursUnit' | translate" [target]="l.targets.sleepHours" [label]="'progress.hoursSlept' | translate" />
            <h3>{{ 'progress.water' | translate }}</h3>
            <app-trend-chart [points]="waterPoints(l)" [series]="'progress.water' | translate" [unit]="'unit.ml' | translate" [target]="l.targets.waterMl" [bars]="true" [label]="'progress.dailyWater' | translate" />
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
  readonly sections: { id: Section; label: TranslationKey }[] = [
    { id: 'overview', label: 'progress.section.overview' }, { id: 'body', label: 'progress.section.body' },
    { id: 'photos', label: 'progress.section.photos' }, { id: 'lifestyle', label: 'progress.section.lifestyle' },
  ];
  readonly sites = measurementSites;
  readonly poses = photoPoses;
  readonly today = isoDate(new Date());
  readonly tomorrow = addDays(this.today, 1);
  readonly percent = percentText;
  readonly section = signal<Section>('overview');
  readonly dashboard = signal<ProgressDashboard | null>(null);
  readonly body = signal<BodyProgress | null>(null);
  readonly photos = signal<ProgressPhoto[]>([]);
  readonly lifestyle = signal<Lifestyle | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly message = signal('');

  readonly date = signal(this.today);
  readonly weightKg = signal<number | null>(null);
  readonly bodyFat = signal<number | null>(null);
  readonly measurements = signal<Partial<Record<MeasurementSite, number | null>>>({});
  readonly chartSite = signal<MeasurementSite>('Waist');
  readonly photoDate = signal(this.today);
  readonly pose = signal<PhotoPose>('Front');
  readonly poseLabel = signal('');
  readonly file = signal<File | null>(null);
  readonly steps = signal<number | null>(null);
  readonly bedtime = signal('');
  readonly wakeTime = signal('');
  readonly sleepHours = signal<number | null>(null);
  readonly sleepQuality = signal<number | null>(null);
  readonly waterMl = signal<number | null>(null);

  constructor(private readonly api: ProgressTrackingService, private readonly i18n: I18nService) {}

  change(summary: SeriesSummary, unit: TranslationKey): string { return changeText(summary, this.i18n.translate(unit)); }
  hours(value: number | null | undefined): string { return hoursText(value, this.i18n); }
  recordText(record: PersonalRecord): string { return recordText(record, this.i18n); }

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
    return Object.values(this.measurements()).some(x => x !== null && x !== undefined);
  }

  siteSeries(body: BodyProgress) {
    return body.measurements.find(x => x.site === this.chartSite()) ?? null;
  }

  saveWeight(): void {
    this.save(this.api.setWeight(this.date(), this.weightKg()!), this.i18n.translate('progress.saved.weight'), () => this.loadBody());
  }

  deleteWeight(date: string): void {
    this.save(this.api.deleteWeight(date), this.i18n.translate('progress.saved.weightDeleted'), () => this.loadBody());
  }

  saveBodyFat(): void {
    this.save(this.api.setBodyFat(this.date(), this.bodyFat()!), this.i18n.translate('progress.saved.bodyFat'), () => this.loadBody());
  }

  saveMeasurements(): void {
    const values = this.sites.filter(site => this.measurements()[site] !== null && this.measurements()[site] !== undefined)
      .map(site => ({ site, valueCm: this.measurements()[site]! }));
    this.save(this.api.setMeasurements(this.date(), values), this.i18n.translate('progress.saved.measurements'), () => { this.measurements.set({}); this.loadBody(); });
  }

  pick(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.[0] ?? null);
  }

  upload(): void {
    const file = this.file();
    if (!file) return;
    this.save(this.api.uploadPhoto(file, this.photoDate(), this.pose(), this.pose() === 'Custom' ? this.poseLabel() : null), this.i18n.translate('progress.saved.photo'), () => {
      this.file.set(null);
      this.open('photos');
    });
  }

  deletePhoto(photo: ProgressPhoto): void {
    this.save(this.api.deletePhoto(photo.id), this.i18n.translate('progress.saved.photoDeleted'), () => this.open('photos'));
  }

  dayOf(lifestyle: Lifestyle) {
    return lifestyle.days.find(x => x.date === this.date()) ?? null;
  }

  remaining(target: number, consumed: number): number {
    return Math.max(0, target - consumed);
  }

  addWater(ml: number): void {
    this.save(this.api.addWater(this.date(), ml), '', () => { this.waterMl.set(null); this.loadLifestyle(); });
  }

  deleteWater(id: string): void {
    this.save(this.api.deleteWater(id), '', () => this.loadLifestyle());
  }

  saveSteps(): void {
    this.save(this.api.setSteps(this.date(), this.steps()!), this.i18n.translate('progress.saved.steps'), () => this.loadLifestyle());
  }

  saveSleep(): void {
    const times = this.bedtime() && this.wakeTime();
    this.save(this.api.setSleep(this.date(), {
      hours: this.sleepHours() || null, quality: this.sleepQuality() || null,
      bedtime: times ? this.bedtime() : null, wakeTime: times ? this.wakeTime() : null,
    }), this.i18n.translate('progress.saved.sleep'), () => this.loadLifestyle());
  }

  stepsPoints(l: Lifestyle) { return l.days.map(x => ({ date: x.date, value: x.steps?.steps ?? null })); }
  sleepPoints(l: Lifestyle) { return l.days.map(x => ({ date: x.date, value: x.sleep?.hours ?? null })); }
  waterPoints(l: Lifestyle) { return l.days.map(x => ({ date: x.date, value: x.waterMl || null })); }

  loadLifestyle(): void {
    const to = this.date() > this.today ? this.date() : this.today;
    this.load(this.api.lifestyle(addDays(to, -13), to), l => {
      this.lifestyle.set(l);
      const day = l.days.find(x => x.date === this.date());
      this.steps.set(day?.steps?.steps ?? null);
      this.sleepHours.set(day?.sleep?.hours ?? null);
      this.sleepQuality.set(day?.sleep?.quality ?? null);
      this.bedtime.set(day?.sleep?.bedtime?.slice(0, 5) ?? '');
      this.wakeTime.set(day?.sleep?.wakeTime?.slice(0, 5) ?? '');
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
