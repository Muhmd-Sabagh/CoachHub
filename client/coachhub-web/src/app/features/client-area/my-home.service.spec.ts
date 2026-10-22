import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { MyHomeService } from './my-home.service';

describe('MyHomeService', () => {
  let service: MyHomeService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(MyHomeService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the signed-in client’s home from the token, with no client ID in the request', () => {
    service.get().subscribe();
    const request = http.expectOne('/api/v1/me/home');
    expect(request.request.method).toBe('GET');
    request.flush({ client: { client: { name: 'Sam', subscriptionStatus: 'Active' }, subscriptions: [] }, invoices: [], deliveredPlans: [] });
  });
});
