import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { FitnessTestsService, changeText, cutOffText } from './fitness-tests.service';

describe('FitnessTestsService (§34–§36: mobility, strength and endurance tests)', () => {
  let service: FitnessTestsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(FitnessTestsService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('searches and edits the library', () => {
    service.list({ kind: 'Strength', scope: 'Custom', searchTerm: 'push', isActive: true }).subscribe();
    http.expectOne('/api/v1/fitness-tests?pageNumber=1&pageSize=100&searchTerm=push&kind=Strength&scope=Custom&isActive=true').flush({ items: [] });
    service.list({ scope: 'All', kind: '' }).subscribe();
    http.expectOne('/api/v1/fitness-tests?pageNumber=1&pageSize=100').flush({ items: [] });
    service.delete('t').subscribe();
    expect(http.expectOne('/api/v1/fitness-tests/t').request.method).toBe('DELETE');
  });

  it('assigns, records and classifies for a client as the coach', () => {
    service.clientTests('c').subscribe();
    http.expectOne('/api/v1/clients/c/fitness-tests').flush({ clientId: 'c', tests: [] });
    service.assign('c', 't').subscribe();
    expect(http.expectOne('/api/v1/clients/c/fitness-tests/assignments/t').request.method).toBe('PUT');
    service.record({ testDefinitionId: 't', date: '2026-10-21', value: 30 }, 'c').subscribe();
    expect(http.expectOne('/api/v1/clients/c/fitness-tests/results').request.body).toEqual({ testDefinitionId: 't', date: '2026-10-21', value: 30 });
    service.classify('c', 'r', null).subscribe();
    const classify = http.expectOne('/api/v1/clients/c/fitness-tests/results/r/classification');
    expect(classify.request.method).toBe('PUT');
    expect(classify.request.body).toEqual({ classification: null });
  });

  it('keeps the client tests under /me', () => {
    service.clientTests().subscribe();
    http.expectOne('/api/v1/me/fitness-tests').flush({ clientId: 'c', tests: [] });
    service.change('r', { date: '2026-10-21', value: 12 }).subscribe();
    expect(http.expectOne('/api/v1/me/fitness-tests/results/r').request.method).toBe('PUT');
    service.deleteResult('r').subscribe();
    expect(http.expectOne('/api/v1/me/fitness-tests/results/r').request.method).toBe('DELETE');
  });

  it('describes changes and cut-offs', () => {
    expect(changeText(3, 'Repetitions')).toBe('+3 reps');
    expect(changeText(-12.5, 'Seconds')).toBe('-12.5 s');
    expect(changeText(null, 'Seconds')).toBeNull();
    expect(cutOffText({ higherIsBetter: true, goodThreshold: 30, poorThreshold: 15, unit: 'Repetitions' }))
      .toBe('Higher is better · Good from 30 reps · Poor below 15 reps');
    expect(cutOffText({ higherIsBetter: false, goodThreshold: 600, poorThreshold: 780, unit: 'Seconds' }))
      .toBe('Lower is better · Good up to 600 s · Poor above 780 s');
    expect(cutOffText({ higherIsBetter: true, goodThreshold: null, poorThreshold: null, unit: 'Points' }))
      .toBe('Higher is better · classified by the coach');
  });
});
