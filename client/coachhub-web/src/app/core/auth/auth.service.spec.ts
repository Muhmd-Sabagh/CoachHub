import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const loginResult = {
  accessToken: 'test-token', expiresAt: '2099-01-01T00:00:00Z', userId: 'user-1',
  email: 'coach@example.com', displayName: 'Coach', roles: ['Administrator']
};

describe('AuthService and authInterceptor', () => {
  let service: AuthService;
  let http: HttpTestingController;
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); sessionStorage.clear(); });

  it('logs in through the API and attaches the returned bearer token', () => {
    service.login({ email: 'coach@example.com', password: 'Password!123' }).subscribe();
    const login = http.expectOne('/api/auth/login');
    expect(login.request.method).toBe('POST');
    expect(login.request.headers.has('Authorization')).toBe(false);
    login.flush(loginResult);
    expect(service.isAuthenticated()).toBe(true);
    expect(sessionStorage.getItem('coachhub.session')).toContain('test-token');
    expect(localStorage.getItem('coachhub.session')).toBeNull();

    TestBed.inject(HttpClient).get('/api/settings').subscribe();
    const settings = http.expectOne('/api/settings');
    expect(settings.request.headers.get('Authorization')).toBe('Bearer test-token');
    settings.flush({ productName: 'CoachHub', coachName: 'Coach' });
  });
});