import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { I18nService } from '../../core/i18n/i18n.service';
import { InboxService, ago } from './inbox.service';

describe('InboxService (§42: event notifications)', () => {
  let service: InboxService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(InboxService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('lists and reads the account notifications', () => {
    service.list(true).subscribe();
    http.expectOne('/api/v1/inbox?pageNumber=1&pageSize=20&unreadOnly=true').flush({ items: [] });
    service.unreadCount().subscribe(x => expect(x.count).toBe(3));
    http.expectOne('/api/v1/inbox/unread-count').flush({ count: 3 });
    service.read('n').subscribe();
    expect(http.expectOne('/api/v1/inbox/n/read').request.method).toBe('POST');
    service.readAll().subscribe();
    expect(http.expectOne('/api/v1/inbox/read-all').request.method).toBe('POST');
  });

  it('keeps the email preferences', () => {
    service.preferences().subscribe();
    http.expectOne('/api/v1/inbox/preferences').flush({ emailEvents: [], emailEnabledOnPlatform: true });
    service.setPreferences(['NewDiet', 'CheckInRequired']).subscribe();
    const put = http.expectOne('/api/v1/inbox/preferences');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ emailEvents: ['NewDiet', 'CheckInRequired'] });
  });

  it('says how long ago', () => {
    const now = Date.parse('2026-10-22T12:00:00Z');
    const i18n = TestBed.inject(I18nService);
    i18n.setLanguage('en');
    expect(ago('2026-10-22T11:59:40Z', i18n, now)).toBe('just now');
    expect(ago('2026-10-22T11:45:00Z', i18n, now)).toBe('15 min ago');
    expect(ago('2026-10-22T09:00:00Z', i18n, now)).toBe('3 h ago');
    i18n.setLanguage('ar');
    expect(ago('2026-10-22T11:45:00Z', i18n, now)).toBe('قبل 15 د');
    i18n.setLanguage('en');
  });
});
