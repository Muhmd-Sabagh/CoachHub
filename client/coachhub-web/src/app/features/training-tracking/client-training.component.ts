import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { addDays, isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import { TrainingChartComponent } from './training-chart.component';
import {
  ExerciseHistory, PersonalRecords, TrainingAnalytics, TrainingPeriod, TrainingTrackingService, WorkoutSession,
  WorkoutSessionSummary, recordText, recordTypeLabels, setText,
} from './training-tracking.service';

/**
 * A client's training for their coach (§40): adherence, volume and the muscle distribution (§20, §23), PRs (§21),
 * workouts as logged (§18) and exercise history (§19). Read-only; the client logs their workouts.
 */
@Component({
  selector: 'app-client-training',
  standalone: true,
  imports: [CommonModule, FormsModule, TrainingChartComponent],
  template: `
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    <section class="card workbench">
      <div class="section-heading"><h2>Training</h2>
        <label class="range"><span class="muted">Range</span>
          <select [(ngModel)]="range" (ngModelChange)="loadAnalytics()" name="range">
            <option [ngValue]="7">Last 7 days</option><option [ngValue]="28">Last 4 weeks</option>
            <option [ngValue]="90">Last 90 days</option><option [ngValue]="365">Last 12 months</option>
          </select></label></div>
      @if (analytics(); as a) {
        <div class="report-metrics">
          <article><small>Workouts</small><strong>{{ a.summary.completedSessions }}</strong><span>of {{ a.summary.scheduledSessions }} scheduled</span></article>
          <article><small>Workout adherence</small><strong>{{ percent(a.summary.adherencePercent) }}</strong><span>weekly rotation</span></article>
          <article><small>Training days</small><strong>{{ a.summary.trainingDays }}</strong><span>frequency</span></article>
          <article><small>Sets / reps</small><strong>{{ a.summary.sets }} / {{ a.summary.reps }}</strong><span>work sets</span></article>
          <article><small>Volume</small><strong>{{ a.summary.volumeKg | number: '1.0-0' }}</strong><span>kg (weight × reps)</span></article>
        </div>

        <div class="section-heading"><h3>Volume by {{ grouping === 'weeks' ? 'week' : 'month' }}</h3>
          <div class="segmented" role="group" aria-label="Group by">
            <button type="button" [class.active]="grouping === 'weeks'" (click)="grouping = 'weeks'">Weekly</button>
            <button type="button" [class.active]="grouping === 'months'" (click)="grouping = 'months'">Monthly</button>
          </div></div>
        <app-training-chart [categories]="periodLabels(a)" [values]="periodVolumes(a)" series="Volume" unit="kg"
          [label]="'Training volume by ' + (grouping === 'weeks' ? 'week' : 'month')" />
        <div class="table-scroll"><table>
          <thead><tr><th>Period</th><th>Workouts</th><th>Scheduled</th><th>Adherence</th><th>Days</th><th>Sets</th><th>Reps</th><th>Volume kg</th></tr></thead>
          <tbody>
            @for (p of periods(a).slice().reverse(); track p.start) {
              <tr><td>{{ p.start | date: 'd MMM' }} – {{ p.end | date: 'd MMM' }}</td><td>{{ p.completedSessions }}</td><td>{{ p.scheduledSessions }}</td>
                <td>{{ percent(p.adherencePercent) }}</td><td>{{ p.trainingDays }}</td><td>{{ p.sets }}</td><td>{{ p.reps }}</td>
                <td>{{ p.volumeKg | number: '1.0-0' }}</td></tr>
            }
          </tbody>
        </table></div>

        <h3>Muscle distribution</h3>
        <p class="muted">Sets per muscle group{{ a.secondaryMuscleWeight > 0 ? '; secondary muscles count ' + a.secondaryMuscleWeight + ' of a set' : ', primary muscle only' }}.</p>
        @if (a.summary.muscles.length) {
          <app-training-chart [categories]="muscleNames(a)" [values]="muscleSets(a)" series="Sets" unit="sets" [horizontal]="true"
            label="Training distribution across muscle groups" />
        }
        <div class="table-scroll"><table>
          <thead><tr><th>Muscle group</th><th>Sets</th><th>Share</th><th>Volume kg</th></tr></thead>
          <tbody>
            @for (m of a.summary.muscles; track m.muscleGroupId ?? 'none') {
              <tr><td>{{ m.nameEn }}</td><td>{{ m.sets }}</td><td>{{ m.sharePercent }}%</td><td>{{ m.volumeKg | number: '1.0-0' }}</td></tr>
            } @empty { <tr><td colspan="4" class="muted">No training in this range.</td></tr> }
          </tbody>
        </table></div>

        <h3>Exercise progression</h3>
        @if (a.exercises.length) {
          <label class="range"><span class="muted">Exercise</span>
            <select [(ngModel)]="exerciseId" name="exercise">
              @for (x of a.exercises; track x.exerciseId) { <option [ngValue]="x.exerciseId">{{ x.nameEn }} ({{ x.points.length }})</option> }
            </select></label>
          @if (progress(a); as x) {
            <div class="table-scroll"><table>
              <thead><tr><th>Day</th><th>Top set</th><th>Most reps</th><th>e1RM ({{ a.oneRepMaxFormula }})</th><th>Sets</th><th>Reps</th><th>Volume kg</th></tr></thead>
              <tbody>
                @for (p of x.points.slice().reverse(); track p.workoutSessionId) {
                  <tr><td>{{ p.date | date: 'EEE d MMM' }}</td>
                    <td>{{ p.topWeightKg !== null ? p.topWeightKg + ' kg × ' + p.topWeightReps : '–' }}</td>
                    <td>{{ p.maxReps ?? '–' }}</td><td>{{ p.bestEstimatedOneRepMaxKg ?? '–' }}</td>
                    <td>{{ p.sets }}</td><td>{{ p.reps }}</td><td>{{ p.volumeKg | number: '1.0-0' }}</td></tr>
                }
              </tbody>
            </table></div>
            <button class="link-button" type="button" (click)="openHistory(x.exerciseId)">Full history and PRs</button>
          }
        } @else { <p class="muted">No exercises logged in this range.</p> }
      } @else if (loading()) { <p class="table-message">Loading…</p> }
    </section>

    @if (history(); as h) {
      <section class="card workbench">
        <div class="section-heading"><h2>{{ h.nameEn }}</h2><button class="link-button" type="button" (click)="history.set(null)">Close</button></div>
        <h3>PR history</h3>
        <ul class="plain-list">
          @for (r of h.records; track r.id) {
            <li>{{ r.achievedOn | date: 'd MMM y' }} · {{ labels[r.type] }}: {{ recordText(r) }}
              @if (r.previousValue !== null) { <small class="muted">(was {{ r.previousValue }})</small> }</li>
          } @empty { <li class="muted">No records yet.</li> }
        </ul>
        <div class="table-scroll"><table>
          <thead><tr><th>Day</th><th>Sets</th><th>Reps</th><th>Volume kg</th><th>Logged sets (RIR / RPE)</th></tr></thead>
          <tbody>
            @for (e of h.entries; track e.workoutSessionId) {
              <tr><td>{{ e.date | date: 'EEE d MMM' }}</td><td>{{ e.sets }}</td><td>{{ e.reps }}</td><td>{{ e.volumeKg | number: '1.0-0' }}</td>
                <td>@for (s of e.loggedSets; track s.id) { <span class="nowrap">{{ setText(s) }}{{ s.rir !== null ? ' RIR ' + s.rir : '' }}{{ s.rpe !== null ? ' RPE ' + s.rpe : '' }}</span>{{ $last ? '' : ', ' }} }</td></tr>
            }
          </tbody>
        </table></div>
      </section>
    }

    <section class="card workbench">
      <h2>Personal records</h2>
      @if (records(); as r) {
        <div class="table-scroll"><table>
          <thead><tr><th>Exercise</th><th>Record</th><th>Best</th><th>Set</th><th>Day</th></tr></thead>
          <tbody>
            @for (x of r.current; track x.id) {
              <tr><td><button class="link-button" type="button" (click)="openHistory(x.exerciseId)">{{ x.exerciseNameEn }}</button></td>
                <td>{{ labels[x.type] }}</td><td>{{ x.value }}{{ x.type === 'Reps' ? '' : ' kg' }}</td>
                <td>{{ x.weightKg !== null && x.reps !== null ? x.weightKg + ' kg × ' + x.reps : '–' }}</td><td>{{ x.achievedOn | date: 'd MMM y' }}</td></tr>
            } @empty { <tr><td colspan="5" class="muted">No records yet.</td></tr> }
          </tbody>
        </table></div>
      }
    </section>

    <section class="card workbench">
      <h2>Workouts</h2>
      <ul class="session-list">
        @for (w of sessions(); track w.id) {
          <li>
            <div><strong>{{ w.nameEn }}</strong>
              <small class="muted">{{ w.date | date: 'EEE d MMM y' }} · {{ w.status === 'Completed' ? 'completed' : 'in progress' }}
                · {{ w.completedSets }} sets · {{ w.volumeKg | number: '1.0-0' }} kg{{ w.versionNumber ? ' · plan v' + w.versionNumber : '' }}</small></div>
            <button class="link-button" type="button" (click)="openSession(w.id)">{{ session()?.id === w.id ? 'Close' : 'Open' }}</button>
          </li>
          @if (session()?.id === w.id) {
            @if (session(); as s) {
              <li class="session-detail">
                @if (s.notes) { <p>“{{ s.notes }}”</p> }
                @for (r of s.records; track r.id) { <p class="pr">🏆 {{ recordText(r) }}</p> }
                <div class="table-scroll"><table>
                  <thead><tr><th>Exercise</th><th>Prescribed</th><th>Logged</th><th>Volume kg</th></tr></thead>
                  <tbody>
                    @for (e of s.exercises; track e.id) {
                      <tr><td>{{ e.groupLabel ? e.groupLabel + ' ' : '' }}{{ e.exerciseNameEn }}{{ e.isSkipped ? ' (skipped)' : '' }}</td>
                        <td>{{ e.planned.length ? e.planned.length + ' sets' : 'added by client' }}</td>
                        <td>@for (x of e.sets; track x.id) { <span class="nowrap">{{ setText(x) }}</span>{{ $last ? '' : ', ' }} } @empty { – }</td>
                        <td>{{ e.volumeKg | number: '1.0-0' }}</td></tr>
                    }
                  </tbody>
                </table></div>
              </li>
            }
          }
        } @empty { <li class="muted">No workouts logged yet.</li> }
      </ul>
      @if (hasMore()) { <button class="button button-secondary" type="button" (click)="more()">Load more</button> }
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
  readonly labels = recordTypeLabels;
  readonly recordText = recordText;
  readonly setText = setText;
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

  constructor(private readonly api: TrainingTrackingService) {}

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
    return analytics.summary.muscles.map(x => x.nameEn);
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
