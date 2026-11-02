import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { I18nService } from '../../core/i18n/i18n.service';
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
    service.list({ searchTerm: ' sara ', formDefinitionId: 'f', dueFrom: '2026-10-01', dueTo: '', submittedTo: '2026-10-31', pageNumber: 2 }).subscribe();
    http.expectOne('/api/v1/check-ins?pageNumber=2&searchTerm=sara&formDefinitionId=f&dueFrom=2026-10-01&submittedTo=2026-10-31').flush({ items: [] });
    service.due('Overdue').subscribe();
    http.expectOne('/api/v1/check-ins/due?state=Overdue&pageNumber=1').flush({ items: [] });
    service.due('Pending', 3, 'Omar').subscribe();
    http.expectOne('/api/v1/check-ins/due?state=Pending&pageNumber=3&searchTerm=Omar').flush({ items: [] });
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

  it('describes cadences and answers in the interface language', () => {
    const i18n = TestBed.inject(I18nService);
    i18n.setLanguage('en');
    expect(cadenceText({ intervalWeeks: 1, dueWeekday: 'Sunday' }, i18n)).toBe('Every week on Sunday');
    expect(cadenceText({ intervalWeeks: 4, dueWeekday: 'Friday' }, i18n)).toBe('Every 4 weeks on Friday');
    const answer = (valueJson: string, mediaId: string | null = null) => ({ id: 'a', questionText: 'Q', questionType: 'Number', valueJson, mediaId });
    expect(answerText(answer('81.5'), i18n)).toBe('81.5');
    expect(answerText(answer('"Tired"'), i18n)).toBe('Tired');
    expect(answerText(answer('["a","b"]'), i18n)).toBe('a, b');
    expect(answerText(answer('true'), i18n)).toBe('Yes');
    expect(answerText(answer('"m"', 'm'), i18n)).toBe('Photo');
    i18n.setLanguage('ar');
    expect(cadenceText({ intervalWeeks: 2, dueWeekday: 'Friday' }, i18n)).toBe('كل أسبوعين يوم الجمعة');
    expect(answerText(answer('false'), i18n)).toBe('لا');
    expect(answerText(answer('["a","b"]'), i18n)).toBe('a، b');
    expect(answerText(answer('"m"', 'm'), i18n)).toBe('صورة');
    i18n.setLanguage('en');
  });
});
