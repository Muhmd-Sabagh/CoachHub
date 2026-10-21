import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ProgressPhoto, ProgressTrackingService, SeriesSummary, beforeAfter, changeText, hoursText, percentText } from './progress-tracking.service';

const summary = (change: number | null, changePercent: number | null): SeriesSummary => ({
  start: null, current: null, lowest: null, highest: null, change, changePercent, count: 0, weeks: [],
});
const photo = (id: string, date: string, pose: ProgressPhoto['pose']): ProgressPhoto => ({
  id, date, pose, label: null, mediaId: 'm' + id, notes: null, createdAt: date + 'T08:00:00Z',
});

describe('ProgressTrackingService (§24–§27, §31–§33, §37: progress tracking)', () => {
  let service: ProgressTrackingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ProgressTrackingService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('keeps the client entries under /me, with no client id in the request', () => {
    service.setWeight('2026-10-21', 82.5).subscribe();
    const weight = http.expectOne('/api/v1/me/progress/weight/2026-10-21');
    expect(weight.request.method).toBe('PUT');
    expect(weight.request.body).toEqual({ weightKg: 82.5, notes: undefined });
    weight.flush(null);
    service.setMeasurements('2026-10-21', [{ site: 'Waist', valueCm: 84 }]).subscribe();
    expect(http.expectOne('/api/v1/me/progress/measurements/2026-10-21').request.body).toEqual({ values: [{ site: 'Waist', valueCm: 84 }] });
    service.setSleep('2026-10-21', { bedtime: '23:00', wakeTime: '07:00', quality: 4 }).subscribe();
    expect(http.expectOne('/api/v1/me/progress/sleep/2026-10-21').request.method).toBe('PUT');
    service.setSteps('2026-10-21', 9000).subscribe();
    expect(http.expectOne('/api/v1/me/progress/steps/2026-10-21').request.body).toEqual({ steps: 9000 });
    service.addWater('2026-10-21', 500).subscribe();
    const water = http.expectOne('/api/v1/me/progress/water');
    expect(JSON.stringify(water.request.body)).not.toContain('clientId');
    water.flush({});
    service.deleteWater('w').subscribe();
    expect(http.expectOne('/api/v1/me/progress/water/w').request.method).toBe('DELETE');
    service.uploadPhoto(new File(['x'], 'front.png', { type: 'image/png' }), '2026-10-21', 'Front').subscribe();
    const upload = http.expectOne('/api/v1/me/progress/photos');
    expect((upload.request.body as FormData).get('pose')).toBe('Front');
    upload.flush({});
    service.dashboard('2026-09-24', '2026-10-21').subscribe();
    http.expectOne('/api/v1/me/progress/dashboard?from=2026-09-24&to=2026-10-21').flush({});
  });

  it('reads a client as the coach and sets their targets', () => {
    service.body('2026-09-24', '2026-10-21', 'c').subscribe();
    http.expectOne('/api/v1/clients/c/progress/body?from=2026-09-24&to=2026-10-21').flush({});
    service.lifestyle('2026-10-08', '2026-10-21', 'c').subscribe();
    http.expectOne('/api/v1/clients/c/progress/lifestyle?from=2026-10-08&to=2026-10-21').flush({});
    service.photos('c').subscribe();
    http.expectOne('/api/v1/clients/c/progress/photos').flush([]);
    service.setTargets('c', { steps: 10000, waterMl: null, sleepHours: 8 }).subscribe();
    const targets = http.expectOne('/api/v1/clients/c/progress/targets');
    expect(targets.request.method).toBe('PUT');
    expect(targets.request.body).toEqual({ steps: 10000, waterMl: null, sleepHours: 8 });
    targets.flush({});
    service.image('m1').subscribe();
    http.expectOne('/api/v1/media/m1').flush(new Blob(['x']));
  });

  it('describes changes, percentages and hours', () => {
    expect(changeText(summary(-2, -2.4), 'kg')).toBe('−2 kg (−2.4%)');
    expect(changeText(summary(1.5, 2), 'cm')).toBe('+1.5 cm (+2%)');
    expect(changeText(summary(null, null), 'kg')).toBe('–');
    expect(percentText(75)).toBe('75%');
    expect(percentText(null)).toBe('–');
    expect(hoursText(7.5)).toBe('7 h 30 m');
    expect(hoursText(null)).toBe('–');
  });

  it('pairs the first and latest photo of a pose for before/after', () => {
    const photos = [photo('b', '2026-10-21', 'Front'), photo('s', '2026-10-01', 'Side'), photo('a', '2026-09-01', 'Front')];
    expect(beforeAfter(photos, 'Front')?.map(x => x.id)).toEqual(['a', 'b']);
    expect(beforeAfter(photos, 'Side')).toBeNull();
  });
});
