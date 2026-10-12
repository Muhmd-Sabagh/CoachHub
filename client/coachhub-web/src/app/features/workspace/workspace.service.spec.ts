import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { clean } from './workspace.component';
import { WorkspaceService, weightTotal } from './workspace.service';

describe('WorkspaceService', () => {
  let service: WorkspaceService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(WorkspaceService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('addresses the platform brand, the own brand, and a named coach brand', () => {
    service.brand({ kind: 'platform' }).subscribe();
    http.expectOne('/api/v1/branding/platform').flush({});
    service.brand({ kind: 'coach', coachId: null }).subscribe();
    http.expectOne('/api/v1/branding/coach').flush({});
    service.setImage({ kind: 'coach', coachId: 'c-1' }, 'Logo', new File([new Uint8Array([1])], 'logo.png')).subscribe();
    const upload = http.expectOne('/api/v1/branding/coach/images/Logo?coachId=c-1');
    expect(upload.request.method).toBe('PUT');
    upload.flush({});
  });

  it('saves hosts and coaching defaults for a named coach', () => {
    service.setHosts('c-1', 'coacha', null).subscribe();
    const hosts = http.expectOne('/api/v1/branding/coach/hosts?coachId=c-1');
    expect(hosts.request.body).toEqual({ subdomain: 'coacha', customDomain: null });
    hosts.flush({});
    service.coachDefaults(null).subscribe();
    http.expectOne('/api/v1/settings/coach').flush({});
  });

  it('builds public asset URLs only for set images', () => {
    expect(service.assetUrl('m-1')).toBe('/api/v1/branding/assets/m-1');
    expect(service.assetUrl(null)).toBeNull();
  });

  it('totals adherence weights and sends empty brand text as null', () => {
    expect(weightTotal({ nutrition: 35, workout: 35, checkIn: 15, steps: 5, water: 5, sleep: 5 })).toBe(100);
    expect(clean({ brandName: ' Coach ', primaryColor: ' ', secondaryColor: '#12b76a', loginHeadline: '', loginMessage: null,
      dashboardWelcome: ' Hi ', emailHeader: null, emailFooter: null })).toEqual({
      brandName: 'Coach', primaryColor: null, secondaryColor: '#12b76a', loginHeadline: null, loginMessage: null,
      dashboardWelcome: 'Hi', emailHeader: null, emailFooter: null });
  });
});
