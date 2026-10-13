import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { NutritionTrackingService, addDays, isoDate, progress } from './nutrition-tracking.service';

describe('NutritionTrackingService (§8–§11: food diary and analytics)', () => {
  let service: NutritionTrackingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(NutritionTrackingService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('keeps the client diary under /me, with no client id in the request', () => {
    service.myDay('2026-10-13').subscribe();
    http.expectOne('/api/v1/me/nutrition/days/2026-10-13').flush({});
    service.choose('2026-10-13', { mealOptions: [{ mealId: 'm', optionId: 'b' }] }).subscribe();
    const choose = http.expectOne('/api/v1/me/nutrition/days/2026-10-13/choices');
    expect(choose.request.method).toBe('PUT');
    choose.flush({});
    service.alternatives('2026-10-13', 'row').subscribe();
    http.expectOne('/api/v1/me/nutrition/days/2026-10-13/foods/row/alternatives').flush([]);
    service.logMeal('2026-10-13', 'm').subscribe();
    expect(http.expectOne('/api/v1/me/nutrition/days/2026-10-13/meals/m/log').request.method).toBe('POST');

    service.log({ date: '2026-10-13', slot: 'Snack', foodItemId: 'f', quantity: 2, foodUnitId: 'u' }).subscribe();
    const log = http.expectOne('/api/v1/me/nutrition/entries');
    expect(log.request.body).toEqual({ date: '2026-10-13', slot: 'Snack', foodItemId: 'f', quantity: 2, foodUnitId: 'u' });
    expect(JSON.stringify(log.request.body)).not.toContain('clientId');
    log.flush({});
    service.update('e', { quantity: 1 }).subscribe();
    expect(http.expectOne('/api/v1/me/nutrition/entries/e').request.method).toBe('PUT');
    service.remove('e').subscribe();
    expect(http.expectOne('/api/v1/me/nutrition/entries/e').request.method).toBe('DELETE');
    service.searchFoods('oats').subscribe();
    http.expectOne('/api/v1/me/nutrition/foods?searchTerm=oats').flush({ items: [] });
  });

  it('reads a client diary and analytics as the coach', () => {
    service.clientDay('c', '2026-10-13').subscribe();
    http.expectOne('/api/v1/clients/c/nutrition/days/2026-10-13').flush({});
    service.analytics('c', '2026-10-07', '2026-10-13').subscribe();
    http.expectOne('/api/v1/clients/c/nutrition/analytics?from=2026-10-07&to=2026-10-13').flush({});
  });

  it('works in local calendar dates and caps progress at the target', () => {
    expect(isoDate(new Date(2026, 0, 5, 23, 30))).toBe('2026-01-05');
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
    expect(progress(1500, 2000)).toBe(75);
    expect(progress(2600, 2000)).toBe(100);
    expect(progress(100, null)).toBe(0);
  });
});
