import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ClientsService } from './clients.service';

describe('ClientsService renewals', () => {
  let service: ClientsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ClientsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts a renewal transaction to the nested subscription endpoint', () => {
    service.renewSubscription('client-1', 'subscription-1', {
      durationMonths: 3,
      price: 750,
      currencyId: 'currency-1',
      paymentAccountId: null,
    }).subscribe();

    const request = http.expectOne(
      '/api/clients/client-1/subscriptions/subscription-1/renewals',
    );
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      durationMonths: 3,
      price: 750,
      currencyId: 'currency-1',
      paymentAccountId: null,
    });
    request.flush({});
  });
});
