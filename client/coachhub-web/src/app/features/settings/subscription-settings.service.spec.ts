import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { MyAccessService, restrictionText } from '../client-area/access-banner.component';
import { ClientsService } from '../clients/clients.service';
import { SubscriptionSettingsService, parsePresets } from './subscription-access.component';

describe('Subscriptions and access control (§47, §57)', () => {
  let settings: SubscriptionSettingsService;
  let access: MyAccessService;
  let clients: ClientsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    settings = TestBed.inject(SubscriptionSettingsService);
    access = TestBed.inject(MyAccessService);
    clients = TestBed.inject(ClientsService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('reads and saves the access rules', () => {
    settings.get().subscribe();
    http.expectOne('/api/v1/settings/subscriptions').flush({ durationPresets: [1], rules: [] });
    settings.save({ durationPresets: [1, 6], rules: [{ status: 'Expired', feature: 'Chat', access: 'Blocked' }] }).subscribe();
    const put = http.expectOne('/api/v1/settings/subscriptions');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body.rules[0]).toEqual({ status: 'Expired', feature: 'Chat', access: 'Blocked' });
  });

  it('suspends and resumes a subscription', () => {
    clients.suspendSubscription('c', 's', 'Payment late').subscribe();
    expect(http.expectOne('/api/v1/clients/c/subscriptions/s/suspend').request.body).toEqual({ reason: 'Payment late' });
    clients.resumeSubscription('c', 's').subscribe();
    expect(http.expectOne('/api/v1/clients/c/subscriptions/s/resume').request.method).toBe('POST');
    access.get().subscribe();
    http.expectOne('/api/v1/me/access').flush({ subscriptionStatus: 'Active', features: [] });
  });

  it('describes the restrictions and reads presets', () => {
    expect(restrictionText({ subscriptionStatus: 'Expired', startsOn: null, endsOn: null, features: [
      { feature: 'Nutrition', access: 'ReadOnly' }, { feature: 'Chat', access: 'Blocked' }, { feature: 'Files', access: 'Full' },
    ] })).toBe('Read-only: Nutrition. Not available: Chat.');
    expect(restrictionText({ subscriptionStatus: 'Active', startsOn: null, endsOn: null, features: [{ feature: 'Chat', access: 'Full' }] })).toBeNull();
    expect(parsePresets(' 1, 3,6 ,12 ')).toEqual([1, 3, 6, 12]);
  });
});
