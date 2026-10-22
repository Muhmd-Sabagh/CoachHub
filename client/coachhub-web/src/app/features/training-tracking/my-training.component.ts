import { Component, OnDestroy, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { PrescribedSet, summarizeSets } from '../plans/workout-math';
import { isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import {
  ExerciseHistory, LoggedExercise, LoggedSet, LoggedSetInput, PersonalRecord, PersonalRecords, TrainingDay, TrainingExercise,
  TrainingTrackingService, WorkoutSession, WorkoutSessionSummary, clock, nextPlannedSet, recordText, restAfter,
  restRemaining, setDraft, setText,
} from './training-tracking.service';

/**
 * The client's training (§18, §19, §21), mobile first: today's place in the weekly rotation, then the workout
 * itself — log each set against the prescription, a rest timer on the device, skip what the coach permits,
 * complete — with exercise history and PRs. The API is the authority; this screen shows what it returns.
 */
@Component({
  selector: 'app-my-training',
  standalone: true,
  imports: [FormsModule, ...I18N_PIPES],
  template: `
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }

    @if (session(); as s) {
      <section class="dashboard-hero">
        <div>
          <button class="link-button" type="button" (click)="close()">{{ 'training.back' | translate }}</button>
          <h1 translate="no">{{ s | localName }}</h1>
          <p>{{ 'training.sessionLine' | translate: { date: (s.date | localDate: 'EEE d MMM'), sets: s.completedSets, volume: (s.volumeKg | localNumber: '1.0-0') } }}
            @if (s.status === 'Completed') { · <strong>{{ 'training.completed' | translate }}</strong> }</p>
        </div>
      </section>

      @if (s.records.length) {
        <section class="card workbench pr-card" [attr.aria-label]="'training.newRecords' | translate">
          <strong>{{ 'training.newPr' | translate: { count: s.records.length } }}</strong>
          <ul>@for (r of s.records; track r.id) { <li>{{ recordText(r) }}</li> }</ul>
        </section>
      }

      @for (e of s.exercises; track e.id) {
        <article class="card workbench exercise-card" [class.skipped]="e.isSkipped">
          <div class="exercise-top">
            <div>
              @if (e.groupLabel) { <span class="group-badge" translate="no">{{ e.groupLabel }}</span> }
              <strong translate="no">{{ exerciseName(e) }}</strong>
              @if (e.block !== 'Main') { <small class="badge">{{ (e.block === 'WarmUp' ? 'training.warmUp' : 'training.coolDown') | translate }}</small> }
              @if (e.planned.length) { <small class="muted block">{{ summary(e.planned) }}</small> }
              @if (e.plannedNotes) { <small class="muted block" translate="no">{{ e.plannedNotes }}</small> }
            </div>
            <div class="exercise-actions">
              <button class="link-button" type="button" (click)="toggleHistory(e)">{{ (historyFor() === e.exerciseId ? 'training.close' : 'training.history') | translate }}</button>
              @if (e.youTubeUrl) { <a class="link-button" [href]="e.youTubeUrl" target="_blank" rel="noopener">{{ 'training.video' | translate }}</a> }
            </div>
          </div>

          @if (historyFor() === e.exerciseId) {
            <div class="history" [attr.aria-label]="'training.exerciseHistory' | translate">
              @if (history(); as h) {
                @for (entry of h.entries.slice(0, 5); track entry.workoutSessionId) {
                  <div class="history-row"><span>{{ entry.date | localDate: 'd MMM' }}</span>
                    <small class="muted">{{ 'training.historyLine' | translate: { sets: entry.sets, weight: entry.topWeightKg ?? '–', reps: entry.topWeightReps ?? '–', volume: (entry.volumeKg | localNumber: '1.0-0') } }}</small></div>
                } @empty { <p class="muted">{{ 'training.noHistory' | translate }}</p> }
              } @else { <p class="muted">{{ 'common.loading' | translate }}</p> }
            </div>
          }

          @if (e.isSkipped) {
            <p class="muted">{{ 'training.skipped' | translate }} <button class="link-button" type="button" [disabled]="busy()" (click)="skip(e, false)">{{ 'training.resume' | translate }}</button></p>
          } @else {
            <ol class="set-list">
              @for (set of e.sets; track set.id) {
                <li class="set-row" [class.done]="set.status === 'Completed'">
                  <span class="set-tag">{{ setTag(set.type, $index) }}</span>
                  @if (editing() === set.id) {
                    <div class="set-inputs">
                      <label><span>{{ 'training.reps' | translate }}</span><input type="number" min="0" max="1000" [(ngModel)]="edit.reps" name="editReps" /></label>
                      <label><span>{{ 'training.kg' | translate }}</span><input type="number" min="0" max="1000" step="0.5" [(ngModel)]="edit.weightKg" name="editKg" /></label>
                      <label><span>{{ 'training.rir' | translate }}</span><input type="number" min="0" max="10" [(ngModel)]="edit.rir" name="editRir" /></label>
                      <label><span>{{ 'training.rpe' | translate }}</span><input type="number" min="1" max="10" step="0.5" [(ngModel)]="edit.rpe" name="editRpe" /></label>
                    </div>
                    <button class="button button-primary" type="button" [disabled]="busy()" (click)="saveEdit(s, set)">{{ 'training.save' | translate }}</button>
                    <button class="link-button" type="button" (click)="editing.set(null)">{{ 'training.cancel' | translate }}</button>
                  } @else {
                    <span class="set-result">{{ setText(set) }}
                      @if (set.rir !== null) { <small class="muted">{{ 'training.rir' | translate }} {{ set.rir }}</small> }
                      @if (set.rpe !== null) { <small class="muted">{{ 'training.rpe' | translate }} {{ set.rpe }}</small> }</span>
                    <button class="link-button" type="button" (click)="startEdit(set)">{{ 'training.edit' | translate }}</button>
                    <button class="link-button danger" type="button" [disabled]="busy()" (click)="deleteSet(s, set)">{{ 'training.delete' | translate }}</button>
                  }
                </li>
              }
            </ol>

            @if (drafts[e.id]; as d) {
              <div class="next-set" [attr.aria-label]="'training.nextSetOf' | translate: { name: exerciseName(e) }">
                @if (plannedFor(e, d); as p) { <small class="muted">{{ 'training.setTarget' | translate: { number: e.sets.length + 1, target: target(p) } }}</small> }
                <div class="set-inputs">
                  <label><span>{{ 'training.reps' | translate }}</span><input type="number" inputmode="numeric" min="0" max="1000" [(ngModel)]="d.reps" [name]="'reps' + e.id" /></label>
                  <label><span>{{ 'training.kg' | translate }}</span><input type="number" inputmode="decimal" min="0" max="1000" step="0.5" [(ngModel)]="d.weightKg" [name]="'kg' + e.id" /></label>
                  <label><span>{{ 'training.rir' | translate }}</span><input type="number" inputmode="numeric" min="0" max="10" [(ngModel)]="d.rir" [name]="'rir' + e.id" /></label>
                  <label><span>{{ 'training.rpe' | translate }}</span><input type="number" inputmode="decimal" min="1" max="10" step="0.5" [(ngModel)]="d.rpe" [name]="'rpe' + e.id" /></label>
                  @if (d.durationSeconds !== null || !d.reps) {
                    <label><span>{{ 'training.seconds' | translate }}</span><input type="number" inputmode="numeric" min="1" [(ngModel)]="d.durationSeconds" [name]="'sec' + e.id" /></label>
                  }
                </div>
                <div class="set-buttons">
                  <button class="button button-primary" type="button" [disabled]="busy()" (click)="logSet(s, e, d)">{{ 'training.completeSet' | translate }}</button>
                  @if (plannedFor(e, d)?.isSkippable) { <button class="button button-secondary" type="button" [disabled]="busy()" (click)="skipSet(s, e, d)">{{ 'training.skipSet' | translate }}</button> }
                  @if (e.canSkip && !hasCompleted(e)) { <button class="link-button" type="button" [disabled]="busy()" (click)="skip(e, true)">{{ 'training.skipExercise' | translate }}</button> }
                  @if (!e.plannedExerciseId && !e.sets.length) { <button class="link-button danger" type="button" [disabled]="busy()" (click)="removeExercise(s, e)">{{ 'training.remove' | translate }}</button> }
                </div>
              </div>
            }
          }
        </article>
      }

      <section class="card workbench add-exercise" [attr.aria-label]="'training.addExerciseLabel' | translate">
        <h2>{{ 'training.addExercise' | translate }}</h2>
        <form class="search-row" (ngSubmit)="search()">
          <input type="search" [(ngModel)]="term" name="term" [placeholder]="'training.searchExercises' | translate" [attr.aria-label]="'training.searchExercises' | translate" />
          <button class="button button-secondary" type="submit" [disabled]="!term.trim()">{{ 'training.search' | translate }}</button>
        </form>
        <ul class="plain-list">
          @for (x of results(); track x.id) {
            <li class="line"><span translate="no">{{ x | localName }}</span><button class="link-button" type="button" [disabled]="busy()" (click)="addExercise(s, x)">{{ 'training.add' | translate }}</button></li>
          }
        </ul>
      </section>

      <section class="card workbench finish">
        <label><span>{{ 'training.notes' | translate }}</span><textarea [(ngModel)]="notes" name="notes" rows="2" maxlength="2000"></textarea></label>
        <div class="set-buttons">
          @if (s.status === 'InProgress') {
            <button class="button button-primary" type="button" [disabled]="busy()" (click)="complete(s)">{{ 'training.completeWorkout' | translate }}</button>
            <button class="link-button danger" type="button" [disabled]="busy()" (click)="discard(s)">{{ 'training.discard' | translate }}</button>
          } @else {
            <button class="button button-secondary" type="button" [disabled]="busy()" (click)="saveNotes(s)">{{ 'training.saveNotes' | translate }}</button>
            <button class="link-button danger" type="button" [disabled]="busy()" (click)="discard(s)">{{ 'training.deleteWorkout' | translate }}</button>
          }
        </div>
      </section>

      @if (resting() > 0) {
        <div class="rest-timer" role="timer" aria-live="polite">
          <span>{{ 'training.rest' | translate }}</span><strong>{{ clock(resting()) }}</strong>
          <button class="button button-secondary" type="button" (click)="addRest(15)">{{ 'training.addRest' | translate }}</button>
          <button class="link-button" type="button" (click)="stopRest()">{{ 'training.skipRest' | translate }}</button>
        </div>
      }
    } @else {
      <section class="dashboard-hero">
        <div>
          <p class="eyebrow">{{ 'training.eyebrow' | translate }}</p>
          <h1>{{ 'training.today' | translate }}</h1>
          @if (day(); as d) {
            @if (d.plan; as plan) { <p>{{ 'training.planLine' | translate: { name: (plan | localName), version: plan.versionNumber, done: d.week.completedSessions, planned: d.week.plannedSessions } }}</p> }
            @else { <p>{{ 'training.noPlan' | translate }}</p> }
          }
        </div>
      </section>
      @if (loading()) { <p class="table-message">{{ 'common.loading' | translate }}</p> }

      @if (day(); as d) {
        @if (d.inProgress; as w) {
          <section class="card workbench resume">
            <div><strong translate="no">{{ w | localName }}</strong><small class="muted block">{{ 'training.inProgress' | translate: { count: w.completedSets } }}</small></div>
            <button class="button button-primary" type="button" (click)="open(w.id)">{{ 'training.resume' | translate }}</button>
          </section>
        }
        @if (d.plan; as plan) {
          <h2 class="section-title">{{ 'training.rotation' | translate }}</h2>
          @for (day of plan.days; track day.id) {
            <article class="card workbench day-card" [class.next]="day.id === plan.suggestedDayId">
              <div>
                <strong translate="no">{{ day | localName }}</strong>
                @if (day.id === plan.suggestedDayId) { <small class="badge next-badge">{{ 'training.next' | translate }}</small> }
                @if (day.doneThisWeek) { <small class="done-mark">{{ 'training.done' | translate }}</small> }
                <small class="muted block">@if (day.subtitle) {<span translate="no">{{ day.subtitle }}</span> · }{{ 'training.dayLine' | translate: { exercises: day.exerciseCount, sets: day.setCount } }}</small>
              </div>
              <button class="button" type="button" [class.button-primary]="day.id === plan.suggestedDayId" [class.button-secondary]="day.id !== plan.suggestedDayId"
                [disabled]="busy() || !!d.inProgress" (click)="start(day.id)">{{ 'training.start' | translate }}</button>
            </article>
          }
          @if (!plan.suggestedDayId) { <p class="muted">{{ 'training.rotationDone' | translate }}</p> }
        }
        <section class="card workbench free">
          <label><span>{{ 'training.freeWorkout' | translate }}</span><input [(ngModel)]="freeName" name="freeName" maxlength="100" [placeholder]="'training.freePlaceholder' | translate" /></label>
          <button class="button button-secondary" type="button" [disabled]="busy() || !!d.inProgress" (click)="start(null)">{{ 'training.start' | translate }}</button>
        </section>
      }

      @if (records(); as r) {
        @if (r.recent.length) {
          <h2 class="section-title">{{ 'training.records' | translate }}</h2>
          <section class="card workbench">
            <ul class="plain-list">
              @for (x of r.recent.slice(0, 6); track x.id) {
                <li class="line"><span>{{ recordText(x) }}</span><small class="muted">{{ x.achievedOn | localDate: 'd MMM' }}</small></li>
              }
            </ul>
          </section>
        }
      }

      @if (recent().length) {
        <h2 class="section-title">{{ 'training.recent' | translate }}</h2>
        <section class="card workbench">
          <ul class="plain-list">
            @for (w of recent(); track w.id) {
              <li class="line">
                <span><span translate="no">{{ w | localName }}</span> <small class="muted">{{ 'training.recentLine' | translate: { date: (w.date | localDate: 'EEE d MMM'), sets: w.completedSets, volume: (w.volumeKg | localNumber: '1.0-0') } }}</small></span>
                <button class="link-button" type="button" (click)="open(w.id)">{{ 'training.open' | translate }}</button>
              </li>
            }
          </ul>
        </section>
      }
    }
  `,
  styles: [`
    .block { display: block; }
    .section-title { margin: 20px 0 8px; font-size: 1.05rem; }
    .card.workbench { margin-bottom: 12px; }
    .resume, .day-card, .free { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .free label { display: grid; gap: 4px; flex: 1; }
    .day-card.next { border-color: var(--brand); }
    .badge { margin-inline-start: 6px; padding: 1px 6px; border-radius: 6px; background: var(--gray-100); color: var(--gray-600); font-size: 0.75rem; }
    .next-badge { background: var(--brand-soft); color: var(--brand-dark); }
    .done-mark { margin-inline-start: 6px; color: var(--success); font-weight: 600; }
    .pr-card { border-color: var(--success); }
    .pr-card ul { margin: 6px 0 0; padding-inline-start: 18px; }
    .exercise-card { display: grid; gap: 10px; }
    .exercise-card.skipped { opacity: 0.7; }
    .exercise-top { display: flex; justify-content: space-between; gap: 8px; }
    .exercise-actions { display: flex; gap: 10px; align-items: flex-start; white-space: nowrap; }
    .group-badge { display: inline-block; min-width: 28px; margin-inline-end: 6px; padding: 1px 6px; border-radius: 6px;
      background: var(--brand-soft); color: var(--brand-dark); font-weight: 700; text-align: center; }
    .history { display: grid; gap: 6px; padding: 8px; border-radius: 10px; background: var(--gray-50); }
    .history-row { display: flex; flex-wrap: wrap; gap: 4px 10px; }
    .set-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
    .set-row { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; min-height: 40px; }
    .set-row.done .set-tag { background: var(--brand-soft); color: var(--brand-dark); }
    .set-tag { display: inline-grid; place-items: center; min-width: 28px; height: 28px; border-radius: 999px; background: var(--gray-100); font-size: 0.8rem; font-weight: 600; }
    .set-result { flex: 1 1 120px; display: flex; flex-wrap: wrap; gap: 6px; align-items: baseline; }
    .next-set { display: grid; gap: 8px; padding: 10px; border-radius: 10px; background: var(--gray-50); }
    .set-inputs { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 8px; flex: 1 1 100%; }
    .set-inputs label { display: grid; gap: 2px; font-size: 0.8rem; }
    .set-inputs input { width: 100%; min-height: 44px; font-size: 1rem; }
    .set-buttons { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    .set-buttons .button { min-height: 44px; }
    .search-row { display: flex; flex-wrap: wrap; gap: 8px; }
    .search-row input { flex: 1 1 160px; }
    .plain-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
    .line { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 6px 10px; }
    .finish { display: grid; gap: 8px; }
    .finish label { display: grid; gap: 4px; }
    .rest-timer { position: fixed; inset: auto 0 calc(64px + env(safe-area-inset-bottom)) 0; z-index: 11; display: flex; align-items: center;
      justify-content: center; gap: 12px; padding: 10px 16px; background: var(--brand-soft); color: var(--brand-dark); border-top: 1px solid var(--gray-200); }
    .rest-timer strong { font-size: 1.5rem; font-variant-numeric: tabular-nums; }
    @media (min-width: 761px) { .rest-timer { max-width: 480px; margin: 0 auto; border-radius: 12px 12px 0 0; } }
  `],
})
export class MyTrainingComponent implements OnInit, OnDestroy {
  readonly today = isoDate(new Date());
  readonly clock = clock;
  readonly day = signal<TrainingDay | null>(null);
  readonly session = signal<WorkoutSession | null>(null);
  readonly records = signal<PersonalRecords | null>(null);
  readonly recent = signal<WorkoutSessionSummary[]>([]);
  readonly history = signal<ExerciseHistory | null>(null);
  readonly historyFor = signal<string | null>(null);
  readonly results = signal<TrainingExercise[]>([]);
  readonly editing = signal<string | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  private readonly restEndsAt = signal<number | null>(null);
  private readonly now = signal(Date.now());
  readonly resting = computed(() => restRemaining(this.restEndsAt(), this.now()));
  private timer: ReturnType<typeof setInterval> | null = null;

  /** The next set's inputs per exercise, prefilled from the prescription. */
  drafts: Record<string, LoggedSetInput> = {};
  edit: LoggedSetInput = {};
  notes = '';
  freeName = '';
  term = '';

  constructor(private readonly api: TrainingTrackingService, private readonly i18n: I18nService) {}

  setText(set: LoggedSet): string { return setText(set, this.i18n); }
  recordText(record: PersonalRecord): string { return recordText(record, this.i18n); }
  /** The exercise's Arabic name in Arabic when the library has one. */
  exerciseName(exercise: LoggedExercise): string {
    return this.i18n.language() === 'ar' && exercise.exerciseNameAr?.trim() ? exercise.exerciseNameAr : exercise.exerciseNameEn;
  }

  ngOnInit(): void {
    this.loadHome();
  }

  ngOnDestroy(): void {
    this.stopRest();
  }

  summary(sets: PrescribedSet[]): string {
    return summarizeSets(sets, this.i18n);
  }

  /** "W" for a warm-up, "D" for a drop set, otherwise the working set's number. */
  setTag(type: string, index: number): string {
    return type === 'WarmUp' ? 'W' : type === 'Drop' ? 'D' : String(index + 1);
  }

  target(set: PrescribedSet): string {
    const t = (key: Parameters<I18nService['translate']>[0], params?: Record<string, string | number>) => this.i18n.translate(key, params);
    const reps = set.repsMin === null ? null : set.repsMax !== null && set.repsMax !== set.repsMin ? `${set.repsMin}–${set.repsMax}` : `${set.repsMin}`;
    return [reps && t('training.repsCount', { value: reps }), set.loadKg !== null && t('training.recordWeight', { value: set.loadKg }),
      set.durationSeconds && clock(set.durationSeconds), set.rir !== null && `${t('training.rir')} ${set.rir}`,
      set.rpe !== null && `${t('training.rpe')} ${set.rpe}`].filter(Boolean).join(' · ') || t('training.asYouFeel');
  }

  plannedFor(exercise: LoggedExercise, draft: LoggedSetInput): PrescribedSet | null {
    return exercise.planned.find(x => x.id === draft.plannedSetId) ?? null;
  }

  hasCompleted(exercise: LoggedExercise): boolean {
    return exercise.sets.some(x => x.status === 'Completed');
  }

  start(workoutDayId: string | null): void {
    this.run(this.api.start(this.today, workoutDayId, workoutDayId ? undefined : this.freeName.trim() || undefined));
  }

  open(id: string): void {
    this.run(this.api.session(id));
  }

  close(): void {
    this.session.set(null);
    this.historyFor.set(null);
    this.stopRest();
    this.loadHome();
  }

  logSet(session: WorkoutSession, exercise: LoggedExercise, draft: LoggedSetInput): void {
    const planned = this.plannedFor(exercise, draft);
    this.run(this.api.logSet(session.id, exercise.id, { ...draft, status: 'Completed' }), () => {
      const rest = restAfter(exercise, planned);
      if (rest) this.startRest(rest);
    });
  }

  skipSet(session: WorkoutSession, exercise: LoggedExercise, draft: LoggedSetInput): void {
    this.run(this.api.logSet(session.id, exercise.id, { plannedSetId: draft.plannedSetId, type: draft.type, status: 'Skipped' }));
  }

  skip(exercise: LoggedExercise, skipped: boolean): void {
    const session = this.session();
    if (session) this.run(this.api.skipExercise(session.id, exercise.id, skipped));
  }

  startEdit(set: LoggedSet): void {
    this.editing.set(set.id);
    this.edit = { type: set.type, status: 'Completed', reps: set.reps, weightKg: set.weightKg, rir: set.rir, rpe: set.rpe,
      durationSeconds: set.durationSeconds, notes: set.notes };
  }

  saveEdit(session: WorkoutSession, set: LoggedSet): void {
    this.run(this.api.updateSet(session.id, set.id, this.edit), () => this.editing.set(null));
  }

  deleteSet(session: WorkoutSession, set: LoggedSet): void {
    this.run(this.api.deleteSet(session.id, set.id));
  }

  removeExercise(session: WorkoutSession, exercise: LoggedExercise): void {
    this.run(this.api.removeExercise(session.id, exercise.id));
  }

  search(): void {
    const term = this.term.trim();
    if (!term) return;
    this.api.searchExercises(term).subscribe({ next: page => this.results.set(page.items), error: e => this.error.set(apiErrorMessage(e)) });
  }

  addExercise(session: WorkoutSession, exercise: TrainingExercise): void {
    this.run(this.api.addExercise(session.id, exercise.id), () => { this.results.set([]); this.term = ''; });
  }

  complete(session: WorkoutSession): void {
    this.stopRest();
    this.run(this.api.complete(session.id, this.notes.trim() || null));
  }

  saveNotes(session: WorkoutSession): void {
    this.run(this.api.saveNotes(session.id, this.notes.trim() || null));
  }

  discard(session: WorkoutSession): void {
    this.busy.set(true);
    this.api.discard(session.id).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => this.close(),
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  toggleHistory(exercise: LoggedExercise): void {
    if (this.historyFor() === exercise.exerciseId) { this.historyFor.set(null); return; }
    this.historyFor.set(exercise.exerciseId);
    this.history.set(null);
    this.api.myHistory(exercise.exerciseId).subscribe({ next: h => this.history.set(h), error: e => this.error.set(apiErrorMessage(e)) });
  }

  addRest(seconds: number): void {
    const ends = this.restEndsAt();
    if (ends !== null) this.restEndsAt.set(Math.max(ends, Date.now()) + seconds * 1000);
  }

  stopRest(): void {
    this.restEndsAt.set(null);
    if (this.timer !== null) { clearInterval(this.timer); this.timer = null; }
  }

  /** The rest timer runs on the device (§18); it vibrates where supported when the rest is over. */
  private startRest(seconds: number): void {
    this.stopRest();
    this.now.set(Date.now());
    this.restEndsAt.set(Date.now() + seconds * 1000);
    this.timer = setInterval(() => {
      this.now.set(Date.now());
      if (this.resting() === 0) {
        this.stopRest();
        navigator.vibrate?.(300);
      }
    }, 250);
  }

  private loadHome(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.myDay(this.today).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: day => this.day.set(day),
      error: e => this.error.set(apiErrorMessage(e)),
    });
    this.api.myRecords().subscribe({ next: r => this.records.set(r), error: () => this.records.set(null) });
    this.api.mySessions().subscribe({ next: page => this.recent.set(page.items.slice(0, 10)), error: () => this.recent.set([]) });
  }

  private show(session: WorkoutSession): void {
    const drafts: Record<string, LoggedSetInput> = {};
    for (const exercise of session.exercises) {
      const planned = nextPlannedSet(exercise);
      const previous = exercise.sets.at(-1);
      drafts[exercise.id] = setDraft(planned, previous);
    }
    this.drafts = drafts;
    if (this.session()?.id !== session.id) this.notes = session.notes ?? '';
    this.session.set(session);
  }

  /** Runs a change and shows the workout as the server now sees it (its totals and PRs are authoritative). */
  private run(request: Observable<WorkoutSession>, after?: () => void): void {
    this.busy.set(true);
    this.error.set('');
    request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: session => { after?.(); this.show(session); },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }
}
