import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { CalculatorStore, PlansService, toEditorInput } from './plans.service';

describe('PlansService', () => {
  let service: PlansService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PlansService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('saves ordered plan content and reloads the persisted plan', () => {
    const input = {
      nameEn: 'Strength plan',
      nameAr: null,
      days: [
        { order: 0, nameEn: 'Day one', exercises: [{ order: 0, exerciseId: 'exercise-2' }] },
        { order: 1, nameEn: 'Day two', exercises: [{ order: 0, exerciseId: 'exercise-1' }] },
      ],
    };

    service.saveInPlace('Workout', 'plan-1', input).subscribe();
    const save = http.expectOne('/api/v1/workout-plans/plan-1/revisions/current');
    expect(save.request.method).toBe('PUT');
    expect(save.request.body.days.map((day: { order: number }) => day.order)).toEqual([0, 1]);
    save.flush({ id: 'plan-1', ...input });

    service.get('Workout', 'plan-1').subscribe();
    const reload = http.expectOne('/api/v1/workout-plans/plan-1');
    expect(reload.request.method).toBe('GET');
    reload.flush({ id: 'plan-1', ...input });
  });

  it('saves explicitly in place or as a new version and reads the history (D4)', () => {
    service.create('Diet', { nameEn: 'Cut' }).subscribe();
    expect(http.expectOne('/api/v1/diet-plans').request.method).toBe('POST');

    service.saveAsNewVersion('Diet', 'diet-1', { baseVersionNumber: 2 }).subscribe();
    const next = http.expectOne('/api/v1/diet-plans/diet-1/revisions');
    expect(next.request.method).toBe('POST');
    expect(next.request.body).toEqual({ baseVersionNumber: 2 });
    next.flush({});

    service.versions('Diet', 'diet-1').subscribe();
    expect(http.expectOne('/api/v1/diet-plans/diet-1/revisions').request.method).toBe('GET');
    service.version('Workout', 'w-1', 1).subscribe();
    http.expectOne('/api/v1/workout-plans/w-1/revisions/1').flush({});

    service.pdf('Workout', 'w-1', 'preview', 'English', 1).subscribe();
    const pdf = http.expectOne(
      (request) =>
        request.url === '/api/v1/workout-plans/w-1/pdf/preview' &&
        request.params.get('version') === '1',
    );
    pdf.flush(new Blob(['v1'], { type: 'application/pdf' }));
  });

  it('bases editor input on the current version, even when an archived one is shown', () => {
    const archived = {
      nameEn: 'Cut',
      notes: [],
      variants: [],
      currentVersionNumber: 3,
      revision: { versionNumber: 1, status: 'Archived', startDate: '2026-10-01', endDate: null },
    };

    const input = toEditorInput(archived);

    expect(input.baseVersionNumber).toBe(3);
    expect(input.status).toBe('Active');
    expect(input.startDate).toBe('2026-10-01');
    expect(input.endDate).toBeNull();
    expect(
      toEditorInput({ ...archived, revision: { ...archived.revision, status: 'Draft' } }).status,
    ).toBe('Draft');
  });

  it('uses protected blob endpoints for both PDF preview and download', () => {
    service.pdf('Diet', 'diet-1', 'preview', 'Arabic').subscribe();
    const preview = http.expectOne(
      (request) =>
        request.url === '/api/v1/diet-plans/diet-1/pdf/preview' &&
        request.params.get('language') === 'Arabic',
    );
    expect(preview.request.responseType).toBe('blob');
    preview.flush(new Blob(['preview'], { type: 'application/pdf' }));

    service.pdf('Diet', 'diet-1', 'download', 'English').subscribe();
    const download = http.expectOne(
      (request) =>
        request.url === '/api/v1/diet-plans/diet-1/pdf/download' &&
        request.params.get('language') === 'English',
    );
    expect(download.request.responseType).toBe('blob');
    download.flush(new Blob(['download'], { type: 'application/pdf' }));
  });
});

describe('CalculatorStore', () => {
  it('keeps calculator state while the surrounding page reloads data', () => {
    const store = new CalculatorStore();
    const result = { calories: 2100, protein: 160 };

    store.show();
    store.result.set(result);

    expect(store.open()).toBe(true);
    expect(store.result()).toEqual(result);

    store.close();

    expect(store.open()).toBe(false);
    expect(store.result()).toEqual(result);
  });
});
