import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { describeDevice } from './account.component';
import { AccountService } from './account.service';

describe('AccountService', () => {
  let service: AccountService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AccountService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('reads and saves the profile under /me', () => {
    service.profile().subscribe();
    http.expectOne('/api/v1/me/profile').flush({});

    service.updateProfile({ displayName: 'Me', phoneNumber: null, preferredLanguage: 'ar' }).subscribe();
    const save = http.expectOne('/api/v1/me/profile');
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual({ displayName: 'Me', phoneNumber: null, preferredLanguage: 'ar' });
    save.flush({});
  });

  it('changes the password with the current one', () => {
    service.changePassword('old', 'new').subscribe();
    const request = http.expectOne('/api/v1/me/password');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ currentPassword: 'old', newPassword: 'new' });
    request.flush(null);
  });

  it('uploads the profile picture as multipart form data', () => {
    service.setAvatar(new File([new Uint8Array([1])], 'me.png', { type: 'image/png' })).subscribe();
    const request = http.expectOne('/api/v1/me/avatar');
    expect(request.request.method).toBe('PUT');
    expect((request.request.body as FormData).get('file')).toBeInstanceOf(File);
    request.flush({ id: 'media-1' });
  });

  it('lists and signs out devices', () => {
    service.sessions().subscribe();
    http.expectOne('/api/v1/me/sessions').flush([]);
    service.revokeSession('s-1').subscribe();
    expect(http.expectOne('/api/v1/me/sessions/s-1').request.method).toBe('DELETE');
    service.revokeOtherSessions().subscribe();
    const others = http.expectOne('/api/v1/me/sessions');
    expect(others.request.method).toBe('DELETE');
    others.flush(null);
  });

  it.each([
    ['Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/126.0 Mobile Safari/537.36', 'Chrome on Android'],
    ['Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 Version/17.5 Mobile/15E148 Safari/604.1', 'Safari on iOS'],
    ['Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/126.0 Safari/537.36 Edg/126.0', 'Edge on Windows'],
    [null, 'Unknown device'],
  ])('describes %s as %s', (userAgent, label) => {
    expect(describeDevice(userAgent)).toBe(label);
  });
});
