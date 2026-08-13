import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { CalculatorStore, PlansService } from './plans.service';

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

    service.save('Workout', 'plan-1', input).subscribe();
    const save = http.expectOne('/api/workout-plans/plan-1');
    expect(save.request.method).toBe('PUT');
    expect(save.request.body.days.map((day: { order: number }) => day.order)).toEqual([0, 1]);
    save.flush({ id: 'plan-1', ...input });

    service.get('Workout', 'plan-1').subscribe();
    const reload = http.expectOne('/api/workout-plans/plan-1');
    expect(reload.request.method).toBe('GET');
    reload.flush({ id: 'plan-1', ...input });
  });

  it('uses protected blob endpoints for both PDF preview and download', () => {
    service.pdf('Diet', 'diet-1', 'preview', 'Arabic').subscribe();
    const preview = http.expectOne(request =>
      request.url === '/api/diet-plans/diet-1/pdf/preview' &&
      request.params.get('language') === 'Arabic');
    expect(preview.request.responseType).toBe('blob');
    preview.flush(new Blob(['preview'], { type: 'application/pdf' }));

    service.pdf('Diet', 'diet-1', 'download', 'English').subscribe();
    const download = http.expectOne(request =>
      request.url === '/api/diet-plans/diet-1/pdf/download' &&
      request.params.get('language') === 'English');
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
