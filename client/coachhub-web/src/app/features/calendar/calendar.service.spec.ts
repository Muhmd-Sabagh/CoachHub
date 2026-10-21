import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { DashboardService } from '../dashboard/dashboard.service';
import { CalendarService, monthGrid, timeText } from './calendar.service';

describe('CalendarService (§45, §46) and the dashboards (§38, §48)', () => {
  let calendar: CalendarService;
  let dashboards: DashboardService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    calendar = TestBed.inject(CalendarService);
    dashboards = TestBed.inject(DashboardService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('reads the client and coach calendars', () => {
    calendar.client('2026-10-26', '2026-12-06', 'c').subscribe();
    http.expectOne('/api/v1/clients/c/calendar?from=2026-10-26&to=2026-12-06').flush({ entries: [] });
    calendar.client('2026-10-26', '2026-12-06').subscribe();
    http.expectOne('/api/v1/me/calendar?from=2026-10-26&to=2026-12-06').flush({ entries: [] });
    calendar.coach('2026-10-26', '2026-12-06', 'c').subscribe();
    http.expectOne('/api/v1/calendar?from=2026-10-26&to=2026-12-06&clientId=c').flush({ entries: [] });
    calendar.coach('2026-10-26', '2026-12-06', null).subscribe();
    http.expectOne('/api/v1/calendar?from=2026-10-26&to=2026-12-06').flush({ entries: [] });
  });

  it('edits training days and events', () => {
    calendar.setTrainingDays('c', ['Monday', 'Thursday']).subscribe();
    const put = http.expectOne('/api/v1/clients/c/calendar/training-days');
    expect(put.request.body).toEqual({ weekdays: ['Monday', 'Thursday'] });
    calendar.createEvent({ clientId: null, kind: 'FollowUp', title: 'Call', date: '2026-11-02', visibleToClient: false }).subscribe();
    expect(http.expectOne('/api/v1/calendar/events').request.method).toBe('POST');
    calendar.deleteEvent('e').subscribe();
    expect(http.expectOne('/api/v1/calendar/events/e').request.method).toBe('DELETE');
  });

  it('loads the coach and admin dashboards for the period', () => {
    dashboards.coach('2026-10-01', '2026-10-30').subscribe();
    http.expectOne('/api/v1/dashboard/coach?from=2026-10-01&to=2026-10-30').flush({});
    dashboards.admin('2026-10-01', '2026-10-30').subscribe();
    http.expectOne('/api/v1/dashboard/admin?from=2026-10-01&to=2026-10-30').flush({});
  });

  it('lays out a month from Monday to Sunday', () => {
    // November 2026 starts on a Sunday and ends on a Monday.
    const grid = monthGrid(new Date(2026, 10, 1));
    expect(grid.from).toBe('2026-10-26');
    expect(grid.to).toBe('2026-12-06');
    expect(grid.days.length).toBe(42);
    expect(timeText({ startTime: '09:00:00', endTime: '11:30:00' })).toBe('09:00–11:30');
    expect(timeText({ startTime: null, endTime: null })).toBe('');
  });
});
