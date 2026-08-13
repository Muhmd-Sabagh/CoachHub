import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { DashboardService } from './dashboard.service';

describe('DashboardService', () => {
  let service: DashboardService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DashboardService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests reporting only with the explicitly applied period', () => {
    service.overview('2026-07-15', '2026-08-13').subscribe();

    const request = http.expectOne((candidate) =>
      candidate.url === '/api/reporting/overview' &&
      candidate.params.get('from') === '2026-07-15' &&
      candidate.params.get('to') === '2026-08-13');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });
});