import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { CheckInsService, answerText, cadenceText } from './check-ins.service';

describe('CheckInsService (§28–§30: check-in workflow and adherence)', () => {
  let service: CheckInsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(CheckInsService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('reviews check-ins as the coach', () => {
    service.list({ status: 'AwaitingReview' }).subscribe();
    http.expectOne('/api/v1/check-ins?pageNumber=1&status=AwaitingReview').flush({ items: [] });
    service.list({ clientId: 'c' }).subscribe();
    http.expectOne('/api/v1/check-ins?pageNumber=1&clientId=c').flush({ items: [] });
    service.due('Overdue').subscribe();
    http.expectOne('/api/v1/check-ins/due?state=Overdue&pageNumber=1').flush({ items: [] });
    service.reply('k', 'Nice work').subscribe();
    expect(http.expectOne('/api/v1/check-ins/k/replies').request.body).toEqual({ text: 'Nice work' });
    service.notes('k', 'private').subscribe();
    expect(http.expectOne('/api/v1/check-ins/k/notes').request.method).toBe('PUT');
    service.approve('k', null).subscribe();
    expect(http.expectOne('/api/v1/check-ins/k/approve').request.body).toEqual({ message: null });
    service.requestChanges('k', 'Add a photo').subscribe();
    expect(http.expectOne('/api/v1/check-ins/k/request-changes').request.body).toEqual({ message: 'Add a photo' });
  });

  it('sets a client schedule and reads adherence', () => {
    service.setSchedule('c', { formDefinitionId: 'f', intervalWeeks: 2, dueWeekday: 'Sunday' }).subscribe();
    const set = http.expectOne('/api/v1/clients/c/check-in-schedule');
    expect(set.request.method).toBe('PUT');
    expect(set.request.body).toEqual({ formDefinitionId: 'f', intervalWeeks: 2, dueWeekday: 'Sunday' });
    set.flush({});
    service.schedule('c').subscribe(value => expect(value).toBeNull());
    http.expectOne('/api/v1/clients/c/check-in-schedule').flush(null, { status: 204, statusText: 'No Content' });
    service.deleteSchedule('c').subscribe();
    expect(http.expectOne('/api/v1/clients/c/check-in-schedule').request.method).toBe('DELETE');
    service.adherence('2026-10-08', '2026-10-21', 'c').subscribe();
    http.expectOne('/api/v1/clients/c/progress/adherence?from=2026-10-08&to=2026-10-21').flush({});
    service.adherence('2026-10-08', '2026-10-21').subscribe();
    http.expectOne('/api/v1/me/progress/adherence?from=2026-10-08&to=2026-10-21').flush({});
  });

  it('keeps the client check-ins under /me', () => {
    service.mine().subscribe();
    http.expectOne('/api/v1/me/check-ins').flush({ schedule: null, resubmitCheckInId: null, history: [] });
    service.myCheckIn('k').subscribe();
    http.expectOne('/api/v1/me/check-ins/k').flush({});
  });

  it('describes cadences and answers', () => {
    expect(cadenceText({ intervalWeeks: 1, dueWeekday: 'Sunday' })).toBe('Every week on Sunday');
    expect(cadenceText({ intervalWeeks: 4, dueWeekday: 'Friday' })).toBe('Every 4 weeks on Friday');
    const answer = (valueJson: string, mediaId: string | null = null) => ({ id: 'a', questionText: 'Q', questionType: 'Number', valueJson, mediaId });
    expect(answerText(answer('81.5'))).toBe('81.5');
    expect(answerText(answer('"Tired"'))).toBe('Tired');
    expect(answerText(answer('["a","b"]'))).toBe('a, b');
    expect(answerText(answer('true'))).toBe('Yes');
    expect(answerText(answer('"m"', 'm'))).toBe('Photo');
  });
});
