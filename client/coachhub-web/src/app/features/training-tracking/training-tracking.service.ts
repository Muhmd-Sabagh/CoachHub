import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { PrescribedSet, SetType, WorkoutBlock } from '../plans/workout-math';
import { OneRepMaxFormula } from '../settings/settings.service';

export type WorkoutSessionStatus = 'InProgress' | 'Completed';
export type LoggedSetStatus = 'Completed' | 'Skipped';
export type PersonalRecordType = 'Weight' | 'Reps' | 'Volume' | 'EstimatedOneRepMax';

export interface WorkoutSessionSummary {
  id: string;
  date: string;
  status: WorkoutSessionStatus;
  nameEn: string;
  nameAr: string | null;
  planId: string | null;
  versionNumber: number | null;
  workoutDayId: string | null;
  exerciseCount: number;
  completedSets: number;
  volumeKg: number;
  startedAt: string;
  completedAt: string | null;
}
export interface TrainingPlanDay {
  id: string;
  nameEn: string;
  nameAr: string | null;
  subtitle: string | null;
  order: number;
  exerciseCount: number;
  setCount: number;
  doneThisWeek: boolean;
}
export interface TrainingDay {
  date: string;
  inProgress: WorkoutSessionSummary | null;
  plan: { planId: string; nameEn: string; nameAr: string | null; versionNumber: number; suggestedDayId: string | null; days: TrainingPlanDay[] } | null;
  week: { weekStart: string; plannedSessions: number; completedSessions: number };
  sessions: WorkoutSessionSummary[];
}
export interface LoggedSet {
  id: string;
  plannedSetId: string | null;
  order: number;
  type: SetType;
  status: LoggedSetStatus;
  reps: number | null;
  weightKg: number | null;
  rir: number | null;
  rpe: number | null;
  durationSeconds: number | null;
  notes: string | null;
  volumeKg: number;
  estimatedOneRepMaxKg: number | null;
  loggedAt: string;
}
export interface LoggedExercise {
  id: string;
  exerciseId: string;
  exerciseNameEn: string;
  exerciseNameAr: string | null;
  mediaId: string | null;
  youTubeUrl: string | null;
  plannedExerciseId: string | null;
  order: number;
  block: WorkoutBlock;
  groupLabel: string | null;
  groupRestSeconds: number | null;
  isSkipped: boolean;
  canSkip: boolean;
  notes: string | null;
  plannedNotes: string | null;
  planned: PrescribedSet[];
  sets: LoggedSet[];
  volumeKg: number;
}
export interface PersonalRecord {
  id: string;
  exerciseId: string;
  exerciseNameEn: string;
  exerciseNameAr: string | null;
  type: PersonalRecordType;
  value: number;
  previousValue: number | null;
  weightKg: number | null;
  reps: number | null;
  workoutSessionId: string;
  achievedOn: string;
}
export interface WorkoutSession {
  id: string;
  date: string;
  status: WorkoutSessionStatus;
  nameEn: string;
  nameAr: string | null;
  planId: string | null;
  versionNumber: number | null;
  workoutDayId: string | null;
  planAvailable: boolean;
  notes: string | null;
  startedAt: string;
  completedAt: string | null;
  completedSets: number;
  volumeKg: number;
  exercises: LoggedExercise[];
  records: PersonalRecord[];
}
export interface LoggedSetInput {
  plannedSetId?: string | null;
  type?: SetType;
  status?: LoggedSetStatus;
  reps?: number | null;
  weightKg?: number | null;
  rir?: number | null;
  rpe?: number | null;
  durationSeconds?: number | null;
  notes?: string | null;
}
export interface ExerciseHistoryEntry {
  workoutSessionId: string;
  date: string;
  sets: number;
  reps: number;
  volumeKg: number;
  topWeightKg: number | null;
  topWeightReps: number | null;
  bestEstimatedOneRepMaxKg: number | null;
  loggedSets: LoggedSet[];
}
export interface ExerciseHistory {
  exerciseId: string;
  nameEn: string;
  nameAr: string | null;
  primaryMuscleGroupId: string | null;
  entries: ExerciseHistoryEntry[];
  records: PersonalRecord[];
}
export interface PersonalRecords {
  current: PersonalRecord[];
  recent: PersonalRecord[];
}
export interface MuscleVolume {
  muscleGroupId: string | null;
  nameEn: string;
  nameAr: string | null;
  sets: number;
  volumeKg: number;
  sharePercent: number;
}
export interface TrainingPeriod {
  start: string;
  end: string;
  completedSessions: number;
  trainingDays: number;
  scheduledSessions: number;
  adherencePercent: number | null;
  sets: number;
  reps: number;
  volumeKg: number;
  muscles: MuscleVolume[];
}
export interface ExerciseProgressPoint {
  date: string;
  workoutSessionId: string;
  topWeightKg: number | null;
  topWeightReps: number | null;
  maxReps: number | null;
  bestEstimatedOneRepMaxKg: number | null;
  volumeKg: number;
  sets: number;
  reps: number;
}
export interface TrainingAnalytics {
  from: string;
  to: string;
  oneRepMaxFormula: OneRepMaxFormula;
  secondaryMuscleWeight: number;
  summary: TrainingPeriod;
  weeks: TrainingPeriod[];
  months: TrainingPeriod[];
  exercises: { exerciseId: string; nameEn: string; nameAr: string | null; points: ExerciseProgressPoint[] }[];
}
export interface TrainingExercise {
  id: string;
  nameEn: string;
  nameAr: string | null;
}

/**
 * Workout tracking (§18), history (§19), PRs (§21) and training analytics (§20, §23). The client logs their own
 * workouts under /me (the account decides whose they are); coaches read a client's.
 */
@Injectable({ providedIn: 'root' })
export class TrainingTrackingService {
  private readonly me = `${environment.apiBaseUrl}/me/training`;
  constructor(private readonly http: HttpClient) {}

  myDay(date: string) {
    return this.http.get<TrainingDay>(`${this.me}/days/${date}`);
  }
  mySessions(pageNumber = 1) {
    return this.http.get<PagedResult<WorkoutSessionSummary>>(`${this.me}/sessions`, { params: { pageNumber } });
  }
  session(id: string) {
    return this.http.get<WorkoutSession>(`${this.me}/sessions/${id}`);
  }
  start(date: string, workoutDayId: string | null, nameEn?: string) {
    return this.http.post<WorkoutSession>(`${this.me}/sessions`, { date, workoutDayId, nameEn });
  }
  saveNotes(id: string, notes: string | null) {
    return this.http.put<WorkoutSession>(`${this.me}/sessions/${id}/notes`, { notes });
  }
  complete(id: string, notes: string | null) {
    return this.http.post<WorkoutSession>(`${this.me}/sessions/${id}/complete`, { notes });
  }
  discard(id: string) {
    return this.http.delete<void>(`${this.me}/sessions/${id}`);
  }
  addExercise(id: string, exerciseId: string) {
    return this.http.post<WorkoutSession>(`${this.me}/sessions/${id}/exercises`, { exerciseId });
  }
  removeExercise(id: string, loggedExerciseId: string) {
    return this.http.delete<WorkoutSession>(`${this.me}/sessions/${id}/exercises/${loggedExerciseId}`);
  }
  skipExercise(id: string, loggedExerciseId: string, isSkipped: boolean) {
    return this.http.put<WorkoutSession>(`${this.me}/sessions/${id}/exercises/${loggedExerciseId}/skip`, { isSkipped });
  }
  logSet(id: string, loggedExerciseId: string, input: LoggedSetInput) {
    return this.http.post<WorkoutSession>(`${this.me}/sessions/${id}/exercises/${loggedExerciseId}/sets`, input);
  }
  updateSet(id: string, setId: string, input: LoggedSetInput) {
    return this.http.put<WorkoutSession>(`${this.me}/sessions/${id}/sets/${setId}`, input);
  }
  deleteSet(id: string, setId: string) {
    return this.http.delete<WorkoutSession>(`${this.me}/sessions/${id}/sets/${setId}`);
  }
  myHistory(exerciseId: string) {
    return this.http.get<ExerciseHistory>(`${this.me}/exercises/${exerciseId}/history`);
  }
  myRecords() {
    return this.http.get<PersonalRecords>(`${this.me}/records`);
  }
  searchExercises(searchTerm: string) {
    return this.http.get<PagedResult<TrainingExercise>>(`${this.me}/exercises`, { params: { searchTerm } });
  }

  clientSessions(clientId: string, pageNumber = 1) {
    return this.http.get<PagedResult<WorkoutSessionSummary>>(`${this.client(clientId)}/sessions`, { params: { pageNumber } });
  }
  clientSession(clientId: string, id: string) {
    return this.http.get<WorkoutSession>(`${this.client(clientId)}/sessions/${id}`);
  }
  clientHistory(clientId: string, exerciseId: string) {
    return this.http.get<ExerciseHistory>(`${this.client(clientId)}/exercises/${exerciseId}/history`);
  }
  clientRecords(clientId: string) {
    return this.http.get<PersonalRecords>(`${this.client(clientId)}/records`);
  }
  analytics(clientId: string, from: string, to: string) {
    return this.http.get<TrainingAnalytics>(`${this.client(clientId)}/analytics`, { params: { from, to } });
  }

  private client(clientId: string) {
    return `${environment.apiBaseUrl}/clients/${clientId}/training`;
  }
}

/** A logged set in a few words: "8 × 60 kg", "12 reps", "45 s", "Skipped". */
export function setText(set: Pick<LoggedSet, 'status' | 'reps' | 'weightKg' | 'durationSeconds'>): string {
  if (set.status === 'Skipped') return 'Skipped';
  const parts: string[] = [];
  if (set.reps !== null && set.weightKg !== null) parts.push(`${set.reps} × ${set.weightKg} kg`);
  else if (set.reps !== null) parts.push(`${set.reps} reps`);
  if (set.durationSeconds) parts.push(clock(set.durationSeconds));
  return parts.join(' · ');
}

/** "New PR: Bench Press — 90 kg × 8" (§21). */
export function recordText(record: PersonalRecord): string {
  const value = (() => {
    switch (record.type) {
      case 'Weight': return `${record.value} kg${record.reps ? ` × ${record.reps}` : ''}`;
      case 'Reps': return `${record.value} reps${record.weightKg ? ` @ ${record.weightKg} kg` : ''}`;
      case 'Volume': return `${record.value} kg volume`;
      case 'EstimatedOneRepMax': return `e1RM ${record.value} kg`;
    }
  })();
  return `${record.exerciseNameEn} — ${value}`;
}

export const recordTypeLabels: Record<PersonalRecordType, string> = {
  Weight: 'Heaviest weight',
  Reps: 'Most reps',
  Volume: 'Most volume',
  EstimatedOneRepMax: 'Estimated 1RM',
};

/** Seconds as m:ss ("1:30", "0:45"). */
export function clock(seconds: number): string {
  const total = Math.max(0, Math.round(seconds));
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
}

/** Seconds left on a rest timer that ends at <paramref name="endsAt"/> (epoch ms); never below zero. */
export function restRemaining(endsAt: number | null, now: number): number {
  return endsAt === null ? 0 : Math.max(0, Math.ceil((endsAt - now) / 1000));
}

/**
 * The values a new set starts with: the prescription's (reps at the top of a range, load), else the
 * previous logged set's, so "log as planned" is one tap.
 */
export function setDraft(planned: PrescribedSet | null | undefined, previous: LoggedSet | null | undefined): LoggedSetInput {
  return {
    plannedSetId: planned?.id ?? null,
    type: planned?.type ?? previous?.type ?? 'Normal',
    status: 'Completed',
    reps: planned?.repsMax ?? planned?.repsMin ?? previous?.reps ?? null,
    weightKg: planned?.loadKg ?? previous?.weightKg ?? null,
    rir: null,
    rpe: null,
    durationSeconds: planned?.durationSeconds ?? previous?.durationSeconds ?? null,
    notes: null,
  };
}

/** The next prescribed set not yet logged, in order. */
export function nextPlannedSet(exercise: LoggedExercise): PrescribedSet | null {
  const logged = new Set(exercise.sets.map(x => x.plannedSetId).filter(Boolean));
  return exercise.planned.find(x => !logged.has(x.id)) ?? null;
}

/** The rest after a set: the group's rest between rounds for grouped exercises, else the prescription's. */
export function restAfter(exercise: LoggedExercise, planned: PrescribedSet | null | undefined): number | null {
  return planned?.restSeconds ?? exercise.groupRestSeconds ?? null;
}
