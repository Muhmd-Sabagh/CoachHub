import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { PrescribedSet } from '../plans/workout-math';
import {
  LoggedExercise, LoggedSet, PersonalRecord, TrainingTrackingService, clock, nextPlannedSet, recordText, restAfter,
  restRemaining, setDraft, setText,
} from './training-tracking.service';

const planned = (id: string, values: Partial<PrescribedSet> = {}): PrescribedSet => ({
  id, order: 0, type: 'Normal', repsMin: null, repsMax: null, loadKg: null, rir: null, rpe: null, tempo: null,
  restSeconds: null, durationSeconds: null, isSkippable: false, notes: null, ...values,
});
const logged = (values: Partial<LoggedSet> = {}): LoggedSet => ({
  id: 's', plannedSetId: null, order: 0, type: 'Normal', status: 'Completed', reps: null, weightKg: null, rir: null, rpe: null,
  durationSeconds: null, notes: null, volumeKg: 0, estimatedOneRepMaxKg: null, loggedAt: '', ...values,
});

describe('TrainingTrackingService (§18–§23: workout tracking, PRs and analytics)', () => {
  let service: TrainingTrackingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(TrainingTrackingService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('keeps the client workouts under /me, with no client id in the request', () => {
    service.myDay('2026-10-21').subscribe();
    http.expectOne('/api/v1/me/training/days/2026-10-21').flush({});
    service.start('2026-10-21', 'day').subscribe();
    const start = http.expectOne('/api/v1/me/training/sessions');
    expect(start.request.method).toBe('POST');
    expect(start.request.body).toEqual({ date: '2026-10-21', workoutDayId: 'day', nameEn: undefined });
    start.flush({});
    service.logSet('w', 'e', { plannedSetId: 'p', reps: 8, weightKg: 60 }).subscribe();
    const log = http.expectOne('/api/v1/me/training/sessions/w/exercises/e/sets');
    expect(JSON.stringify(log.request.body)).not.toContain('clientId');
    log.flush({});
    service.updateSet('w', 's', { reps: 6 }).subscribe();
    expect(http.expectOne('/api/v1/me/training/sessions/w/sets/s').request.method).toBe('PUT');
    service.deleteSet('w', 's').subscribe();
    expect(http.expectOne('/api/v1/me/training/sessions/w/sets/s').request.method).toBe('DELETE');
    service.skipExercise('w', 'e', true).subscribe();
    expect(http.expectOne('/api/v1/me/training/sessions/w/exercises/e/skip').request.body).toEqual({ isSkipped: true });
    service.addExercise('w', 'x').subscribe();
    http.expectOne('/api/v1/me/training/sessions/w/exercises').flush({});
    service.saveNotes('w', 'Later').subscribe();
    expect(http.expectOne('/api/v1/me/training/sessions/w/notes').request.method).toBe('PUT');
    service.complete('w', 'Good').subscribe();
    expect(http.expectOne('/api/v1/me/training/sessions/w/complete').request.body).toEqual({ notes: 'Good' });
    service.discard('w').subscribe();
    expect(http.expectOne('/api/v1/me/training/sessions/w').request.method).toBe('DELETE');
    service.myHistory('x').subscribe();
    http.expectOne('/api/v1/me/training/exercises/x/history').flush({});
    service.myRecords().subscribe();
    http.expectOne('/api/v1/me/training/records').flush({});
    service.searchExercises('row').subscribe();
    http.expectOne('/api/v1/me/training/exercises?searchTerm=row').flush({ items: [] });
  });

  it('reads a client training as the coach', () => {
    service.clientSessions('c').subscribe();
    http.expectOne('/api/v1/clients/c/training/sessions?pageNumber=1').flush({ items: [] });
    service.clientSession('c', 'w').subscribe();
    http.expectOne('/api/v1/clients/c/training/sessions/w').flush({});
    service.clientHistory('c', 'x').subscribe();
    http.expectOne('/api/v1/clients/c/training/exercises/x/history').flush({});
    service.clientRecords('c').subscribe();
    http.expectOne('/api/v1/clients/c/training/records').flush({});
    service.analytics('c', '2026-10-15', '2026-10-21').subscribe();
    http.expectOne('/api/v1/clients/c/training/analytics?from=2026-10-15&to=2026-10-21').flush({});
  });

  it('describes sets and records the way the client reads them', () => {
    expect(setText(logged({ reps: 8, weightKg: 60 }))).toBe('8 × 60 kg');
    expect(setText(logged({ reps: 12 }))).toBe('12 reps');
    expect(setText(logged({ durationSeconds: 45 }))).toBe('0:45');
    expect(setText(logged({ status: 'Skipped' }))).toBe('Skipped');
    const record: PersonalRecord = {
      id: 'r', exerciseId: 'x', exerciseNameEn: 'Bench Press', exerciseNameAr: null, type: 'Weight', value: 90, previousValue: 85,
      weightKg: 90, reps: 8, workoutSessionId: 'w', achievedOn: '2026-10-21',
    };
    expect(recordText(record)).toBe('Bench Press — 90 kg × 8');
    expect(recordText({ ...record, type: 'EstimatedOneRepMax', value: 114 })).toBe('Bench Press — e1RM 114 kg');
    expect(recordText({ ...record, type: 'Volume', value: 1350 })).toBe('Bench Press — 1350 kg volume');
  });

  it('runs the rest timer on the device clock', () => {
    expect(clock(90)).toBe('1:30');
    expect(clock(5)).toBe('0:05');
    expect(restRemaining(10_000, 1_000)).toBe(9);
    expect(restRemaining(10_000, 9_500)).toBe(1);
    expect(restRemaining(10_000, 12_000)).toBe(0);
    expect(restRemaining(null, 0)).toBe(0);
  });

  it('drafts the next set from the plan, then from the previous set', () => {
    const sets = [planned('a', { type: 'WarmUp', repsMin: 10, loadKg: 20, restSeconds: 60 }), planned('b', { repsMin: 8, repsMax: 12, loadKg: 60 })];
    const exercise = { planned: sets, sets: [logged({ plannedSetId: 'a', reps: 10, weightKg: 20 })], groupRestSeconds: 90 } as LoggedExercise;
    const next = nextPlannedSet(exercise);
    expect(next?.id).toBe('b');
    expect(setDraft(next, exercise.sets[0])).toMatchObject({ plannedSetId: 'b', type: 'Normal', reps: 12, weightKg: 60 });
    expect(setDraft(null, logged({ reps: 5, weightKg: 100 }))).toMatchObject({ plannedSetId: null, reps: 5, weightKg: 100 });
    expect(nextPlannedSet({ ...exercise, sets: [logged({ plannedSetId: 'a' }), logged({ plannedSetId: 'b' })] })).toBeNull();
    // A grouped exercise rests by the group when its set says nothing.
    expect(restAfter(exercise, sets[0])).toBe(60);
    expect(restAfter(exercise, sets[1])).toBe(90);
  });
});
