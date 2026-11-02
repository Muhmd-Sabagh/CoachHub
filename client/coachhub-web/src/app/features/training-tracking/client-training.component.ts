import { Component, Input, OnChanges, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { addDays, isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import { TrainingChartComponent } from './training-chart.component';
import {
  ExerciseHistory, LoggedExercise, LoggedSet, PersonalRecord, PersonalRecords, TrainingAnalytics, TrainingPeriod, TrainingTrackingService,
  WorkoutSession, WorkoutSessionSummary, recordText, setText,
} from './training-tracking.service';

/**
 * A client's training for their coach (§40): adherence, volume and the muscle distribution (§20, §23), PRs (§21),
 * workouts as logged (§18) and exercise history (§19). Read-only; the client logs their workouts.
 */
@Component({
  selector: 'app-client-training',
  standalone: true,
  imports: [FormsModule, TrainingChartComponent, ...I18N_PIPES],
  template: `
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    <section class="card workbench">
      <div class="section-heading"><h2>{{ 'coachTraining.title' | translate }}</h2>
        <label class="range"><span class="muted">{{ 'range.label' | translate }}</span>
          <select [(ngModel)]="range" (ngModelChange)="loadAnalytics()" name="range">
            <option [ngValue]="7">{{ 'range.7' | translate }}</option><option [ngValue]="28">{{ 'range.28' | translate }}</option>
            <option [ngValue]="90">{{ 'range.90' | translate }}</option><option [ngValue]="365">{{ 'range.365' | translate }}</option>
          </select></label></div>
      @if (analytics(); as a) {
        <div class="report-metrics">
          <article><small>{{ 'coachTraining.workouts' | translate }}</small><strong>{{ a.summary.completedSessions }}</strong><span>{{ 'coachTraining.ofScheduled' | translate: { count: a.summary.scheduledSessions } }}</span></article>
          <article><small>{{ 'coachTraining.adherence' | translate }}</small><strong>{{ percent(a.summary.adherencePercent) }}</strong><span>{{ 'coachTraining.rotation' | translate }}</span></article>
          <article><small>{{ 'coachTraining.trainingDays' | translate }}</small><strong>{{ a.summary.trainingDays }}</strong><span>{{ 'coachTraining.frequency' | translate }}</span></article>
          <article><small>{{ 'coachTraining.setsReps' | translate }}</small><strong>{{ a.summary.sets }} / {{ a.summary.reps }}</strong><span>{{ 'coachTraining.workSets' | translate }}</span></article>
          <article><small>{{ 'coachTraining.volume' | translate }}</small><strong>{{ a.summary.volumeKg | localNumber: '1.0-0' }}</strong><span>{{ 'coachTraining.volumeHint' | translate }}</span></article>
        </div>

        <div class="section-heading"><h3>{{ (grouping === 'weeks' ? 'coachTraining.volumeByWeek' : 'coachTraining.volumeByMonth') | translate }}</h3>
          <div class="segmented" role="group" [attr.aria-label]="'group.label' | translate">
            <button type="button" [class.active]="grouping === 'weeks'" (click)="grouping = 'weeks'">{{ 'group.weeks' | translate }}</button>
            <button type="button" [class.active]="grouping === 'months'" (click)="grouping = 'months'">{{ 'group.months' | translate }}</button>
          </div></div>
        <app-training-chart [categories]="periodLabels(a)" [values]="periodVolumes(a)" [series]="'coachTraining.volumeSeries' | translate" [unit]="'unit.kg' | translate"
          [label]="(grouping === 'weeks' ? 'coachTraining.volumeByWeek' : 'coachTraining.volumeByMonth') | translate" />
        <div class="table-scroll"><table>
          <thead><tr><th>{{ 'coachTraining.period' | translate }}</th><th>{{ 'coachTraining.workouts' | translate }}</th><th>{{ 'coachTraining.scheduled' | translate }}</th>
            <th>{{ 'coachTraining.adherenceColumn' | translate }}</th><th>{{ 'coachTraining.days' | translate }}</th><th>{{ 'coachTraining.sets' | translate }}</th>
            <th>{{ 'coachTraining.reps' | translate }}</th><th>{{ 'coachTraining.volumeKg' | translate }}</th></tr></thead>
          <tbody>
            @for (p of periods(a).slice().reverse(); track p.start) {
              <tr><td>{{ p.start | localDate: 'd MMM' }} – {{ p.end | localDate: 'd MMM' }}</td><td>{{ p.completedSessions }}</td><td>{{ p.scheduledSessions }}</td>
                <td>{{ percent(p.adherencePercent) }}</td><td>{{ p.trainingDays }}</td><td>{{ p.sets }}</td><td>{{ p.reps }}</td>
                <td>{{ p.volumeKg | localNumber: '1.0-0' }}</td></tr>
            }
          </tbody>
        </table></div>

        <h3>{{ 'coachTraining.muscles' | translate }}</h3>
        <p class="muted">{{ a.secondaryMuscleWeight > 0 ? ('coachTraining.secondaryCounts' | translate: { weight: (a.secondaryMuscleWeight | localNumber: '1.0-2') }) : ('coachTraining.primaryOnly' | translate) }}</p>
        @if (a.summary.muscles.length) {
          <app-training-chart [categories]="muscleNames(a)" [values]="muscleSets(a)" [series]="'coachTraining.sets' | translate" [unit]="'coachTraining.setsUnit' | translate" [horizontal]="true"
            [label]="'coachTraining.distribution' | translate" />
        }
        <div class="table-scroll"><table>
          <thead><tr><th>{{ 'coachTraining.muscleGroup' | translate }}</th><th>{{ 'coachTraining.sets' | translate }}</th><th>{{ 'coachTraining.share' | translate }}</th><th>{{ 'coachTraining.volumeKg' | translate }}</th></tr></thead>
          <tbody>
            @for (m of a.summary.muscles; track m.muscleGroupId ?? 'none') {
              <tr><td translate="no">{{ m | localName }}</td><td>{{ m.sets }}</td><td>{{ m.sharePercent }}%</td><td>{{ m.volumeKg | localNumber: '1.0-0' }}</td></tr>
            } @empty { <tr><td colspan="4" class="muted">{{ 'coachTraining.noTraining' | translate }}</td></tr> }
          </tbody>
        </table></div>

        <h3>{{ 'coachTraining.progression' | translate }}</h3>
        @if (a.exercises.length) {
          <label class="range"><span class="muted">{{ 'coachTraining.exercise' | translate }}</span>
            <select [(ngModel)]="exerciseId" name="exercise">
              @for (x of a.exercises; track x.exerciseId) { <option [ngValue]="x.exerciseId" translate="no">{{ x | localName }} ({{ x.points.length }})</option> }
            </select></label>
          @if (progress(a); as x) {
            <div class="table-scroll"><table>
              <thead><tr><th>{{ 'coachTraining.day' | translate }}</th><th>{{ 'coachTraining.topSet' | translate }}</th><th>{{ 'coachTraining.mostReps' | translate }}</th>
                <th>{{ 'coachTraining.e1rm' | translate: { formula: a.oneRepMaxFormula } }}</th><th>{{ 'coachTraining.sets' | translate }}</th><th>{{ 'coachTraining.reps' | translate }}</th><th>{{ 'coachTraining.volumeKg' | translate }}</th></tr></thead>
              <tbody>
                @for (p of x.points.slice().reverse(); track p.workoutSessionId) {
                  <tr><td>{{ p.date | localDate: 'EEE d MMM' }}</td>
                    <td>{{ p.topWeightKg !== null ? ('coachTraining.weightReps' | translate: { weight: (p.topWeightKg | localNumber: '1.0-2'), reps: p.topWeightReps }) : '–' }}</td>
                    <td>{{ p.maxReps ?? '–' }}</td><td>{{ p.bestEstimatedOneRepMaxKg !== null ? (p.bestEstimatedOneRepMaxKg | localNumber: '1.0-1') : '–' }}</td>
                    <td>{{ p.sets }}</td><td>{{ p.reps }}</td><td>{{ p.volumeKg | localNumber: '1.0-0' }}</td></tr>
                }
              </tbody>
            </table></div>
            <button class="link-button" type="button" (click)="openHistory(x.exerciseId)">{{ 'coachTraining.fullHistory' | translate }}</button>
          }
        } @else { <p class="muted">{{ 'coachTraining.noExercises' | translate }}</p> }
      } @else if (loading()) { <p class="table-message">{{ 'common.loading' | translate }}</p> }
    </section>

    @if (history(); as h) {
      <section class="card workbench">
        <div class="section-heading"><h2 translate="no">{{ h | localName }}</h2><button class="link-button" type="button" (click)="history.set(null)">{{ 'common.close' | translate }}</button></div>
        <h3>{{ 'coachTraining.prHistory' | translate }}</h3>
        <ul class="plain-list">
          @for (r of h.records; track r.id) {
            <li>{{ r.achievedOn | localDate: 'd MMM y' }} · {{ r.type | enumLabel: 'personalRecordType' }}: {{ record(r) }}
              @if (r.previousValue !== null) { <small class="muted">{{ 'coachTraining.was' | translate: { value: (r.previousValue | localNumber: '1.0-2') } }}</small> }</li>
          } @empty { <li class="muted">{{ 'coachTraining.noRecords' | translate }}</li> }
        </ul>
        <div class="table-scroll"><table>
          <thead><tr><th>{{ 'coachTraining.day' | translate }}</th><th>{{ 'coachTraining.sets' | translate }}</th><th>{{ 'coachTraining.reps' | translate }}</th><th>{{ 'coachTraining.volumeKg' | translate }}</th><th>{{ 'coachTraining.loggedSets' | translate }}</th></tr></thead>
          <tbody>
            @for (e of h.entries; track e.workoutSessionId) {
              <tr><td>{{ e.date | localDate: 'EEE d MMM' }}</td><td>{{ e.sets }}</td><td>{{ e.reps }}</td><td>{{ e.volumeKg | localNumber: '1.0-0' }}</td>
                <td>@for (s of e.loggedSets; track s.id) { <span class="nowrap">{{ set(s) }}{{ s.rir !== null ? ' RIR ' + s.rir : '' }}{{ s.rpe !== null ? ' RPE ' + s.rpe : '' }}</span>{{ $last ? '' : ', ' }} }</td></tr>
            }
          </tbody>
        </table></div>
      </section>
    }

    <section class="card workbench">
      <h2>{{ 'coachTraining.records' | translate }}</h2>
      @if (records(); as r) {
        <div class="table-scroll"><table>
          <thead><tr><th>{{ 'coachTraining.exercise' | translate }}</th><th>{{ 'coachTraining.record' | translate }}</th><th>{{ 'coachTraining.best' | translate }}</th><th>{{ 'coachTraining.set' | translate }}</th><th>{{ 'coachTraining.day' | translate }}</th></tr></thead>
          <tbody>
            @for (x of r.current; track x.id) {
              <tr><td><button class="link-button" type="button" (click)="openHistory(x.exerciseId)" translate="no">{{ exerciseName(x) }}</button></td>
                <td>{{ x.type | enumLabel: 'personalRecordType' }}</td><td>{{ x.value | localNumber: '1.0-2' }}{{ x.type === 'Reps' ? '' : ' ' + ('unit.kg' | translate) }}</td>
                <td>{{ x.weightKg !== null && x.reps !== null ? ('coachTraining.weightReps' | translate: { weight: (x.weightKg | localNumber: '1.0-2'), reps: x.reps }) : '–' }}</td><td>{{ x.achievedOn | localDate: 'd MMM y' }}</td></tr>
            } @empty { <tr><td colspan="5" class="muted">{{ 'coachTraining.noRecords' | translate }}</td></tr> }
          </tbody>
        </table></div>
      }
    </section>

    <section class="card workbench">
      <h2>{{ 'coachTraining.workoutList' | translate }}</h2>
      <ul class="session-list">
        @for (w of sessions(); track w.id) {
          <li>
            <div><strong translate="no">{{ w | localName }}</strong>
              <small class="muted">{{ 'coachTraining.sessionLine' | translate: { date: (w.date | localDate: 'EEE d MMM y'), status: (w.status | enumLabel: 'workoutSessionStatus'), sets: w.completedSets, volume: (w.volumeKg | localNumber: '1.0-0') } }}@if (w.versionNumber) {{{ 'coachTraining.planVersion' | translate: { version: w.versionNumber } }}}</small></div>
            <button class="link-button" type="button" (click)="openSession(w.id)">{{ (session()?.id === w.id ? 'common.close' : 'common.open') | translate }}</button>
          </li>
          @if (session()?.id === w.id) {
            @if (session(); as s) {
              <li class="session-detail">
                @if (s.notes) { <p translate="no">“{{ s.notes }}”</p> }
                @for (r of s.records; track r.id) { <p class="pr">🏆 {{ record(r) }}</p> }
                <div class="table-scroll"><table>
                  <thead><tr><th>{{ 'coachTraining.exercise' | translate }}</th><th>{{ 'coachTraining.prescribed' | translate }}</th><th>{{ 'coachTraining.logged' | translate }}</th><th>{{ 'coachTraining.volumeKg' | translate }}</th></tr></thead>
                  <tbody>
                    @for (e of s.exercises; track e.id) {
                      <tr><td><span translate="no">{{ e.groupLabel ? e.groupLabel + ' ' : '' }}{{ loggedName(e) }}</span>@if (e.isSkipped) {{{ 'coachTraining.skipped' | translate }}}</td>
                        <td>{{ e.planned.length ? ('coachTraining.prescribedSets' | translate: { count: e.planned.length }) : ('coachTraining.addedByClient' | translate) }}</td>
                        <td>@for (x of e.sets; track x.id) { <span class="nowrap">{{ set(x) }}</span>{{ $last ? '' : ', ' }} } @empty { – }</td>
                        <td>{{ e.volumeKg | localNumber: '1.0-0' }}</td></tr>
                    }
                  </tbody>
                </table></div>
              </li>
            }
          }
        } @empty { <li class="muted">{{ 'coachTraining.noWorkouts' | translate }}</li> }
      </ul>
      @if (hasMore()) { <button class="button button-secondary" type="button" (click)="more()">{{ 'coachTraining.loadMore' | translate }}</button> }
    </section>
  `,
  styles: [`
    .range { display: flex; gap: 8px; align-items: center; }
    .segmented { display: inline-flex; border: 1px solid var(--gray-200); border-radius: 8px; overflow: hidden; }
    .segmented button { padding: 6px 12px; border: 0; background: transparent; color: inherit; }
    .segmented button.active { background: var(--brand-soft); color: var(--brand-dark); font-weight: 600; }
    .plain-list { list-style: none; margin: 0 0 12px; padding: 0; display: grid; gap: 6px; }
    .session-detail { display: block; }
    .pr { margin: 4px 0; color: var(--success); }
    .nowrap { white-space: nowrap; }
  `],
})
export class ClientTrainingComponent implements OnChanges {
  @Input({ required: true }) clientId = '';
  readonly today = isoDate(new Date());
  readonly analytics = signal<TrainingAnalytics | null>(null);
  readonly records = signal<PersonalRecords | null>(null);
  readonly sessions = signal<WorkoutSessionSummary[]>([]);
  readonly session = signal<WorkoutSession | null>(null);
  readonly history = signal<ExerciseHistory | null>(null);
  readonly total = signal(0);
  readonly hasMore = computed(() => this.sessions().length < this.total());
  readonly loading = signal(false);
  readonly error = signal('');
  range = 28;
  grouping: 'weeks' | 'months' = 'weeks';
  exerciseId: string | null = null;
  private page = 1;

  constructor(private readonly api: TrainingTrackingService, private readonly i18n: I18nService) {}

  record(record: PersonalRecord): string { return recordText(record, this.i18n); }
  set(set: LoggedSet): string { return setText(set, this.i18n); }
  exerciseName(record: PersonalRecord): string { return this.i18n.name({ nameEn: record.exerciseNameEn, nameAr: record.exerciseNameAr }); }
  loggedName(exercise: LoggedExercise): string { return this.i18n.name({ nameEn: exercise.exerciseNameEn, nameAr: exercise.exerciseNameAr }); }

  ngOnChanges(): void {
    this.loadAnalytics();
    this.api.clientRecords(this.clientId).subscribe({ next: r => this.records.set(r), error: e => this.error.set(apiErrorMessage(e)) });
    this.page = 1;
    this.sessions.set([]);
    this.loadSessions();
  }

  loadAnalytics(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.analytics(this.clientId, addDays(this.today, -(this.range - 1)), this.today).pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: a => {
          this.analytics.set(a);
          if (!a.exercises.some(x => x.exerciseId === this.exerciseId)) this.exerciseId = a.exercises[0]?.exerciseId ?? null;
        },
        error: e => this.error.set(apiErrorMessage(e)),
      });
  }

  periods(analytics: TrainingAnalytics): TrainingPeriod[] {
    return this.grouping === 'weeks' ? analytics.weeks : analytics.months;
  }

  periodLabels(analytics: TrainingAnalytics): string[] {
    return this.periods(analytics).map(x => x.start.slice(5));
  }

  periodVolumes(analytics: TrainingAnalytics): number[] {
    return this.periods(analytics).map(x => x.volumeKg);
  }

  muscleNames(analytics: TrainingAnalytics): string[] {
    return analytics.summary.muscles.map(x => this.i18n.name(x));
  }

  muscleSets(analytics: TrainingAnalytics): number[] {
    return analytics.summary.muscles.map(x => x.sets);
  }

  progress(analytics: TrainingAnalytics) {
    return analytics.exercises.find(x => x.exerciseId === this.exerciseId) ?? null;
  }

  openHistory(exerciseId: string): void {
    this.api.clientHistory(this.clientId, exerciseId).subscribe({ next: h => this.history.set(h), error: e => this.error.set(apiErrorMessage(e)) });
  }

  openSession(id: string): void {
    if (this.session()?.id === id) { this.session.set(null); return; }
    this.api.clientSession(this.clientId, id).subscribe({ next: s => this.session.set(s), error: e => this.error.set(apiErrorMessage(e)) });
  }

  more(): void {
    this.page += 1;
    this.loadSessions();
  }

  percent(value: number | null | undefined): string {
    return value === null || value === undefined ? '–' : `${value}%`;
  }

  private loadSessions(): void {
    this.api.clientSessions(this.clientId, this.page).subscribe({
      next: page => { this.sessions.set([...this.sessions(), ...page.items]); this.total.set(page.totalCount); },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }
}
