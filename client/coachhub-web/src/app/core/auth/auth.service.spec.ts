import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const loginResult = {
  accessToken: 'test-token', expiresAt: '2099-01-01T00:00:00Z', userId: 'user-1',
  email: 'coach@example.com', displayName: 'Coach', roles: ['Administrator']
};
const expired = { ...loginResult, accessToken: 'old-token', expiresAt: '2000-01-01T00:00:00Z' };

describe('AuthService and authInterceptor', () => {
  let service: AuthService;
  let http: HttpTestingController;

  function setup(): void {
    TestBed.configureTestingModule({ providers: [provideRouter([{ path: 'login', children: [] }]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  }

  beforeEach(() => { sessionStorage.clear(); localStorage.clear(); });
  afterEach(() => { http.verify(); sessionStorage.clear(); localStorage.clear(); });

  it('logs in through the API and attaches the returned bearer token', () => {
    setup();
    service.login({ login: 'coach@example.com', password: 'Password!123' }).subscribe();
    const login = http.expectOne('/api/v1/auth/login');
    expect(login.request.method).toBe('POST');
    expect(login.request.headers.has('Authorization')).toBe(false);
    login.flush(loginResult);
    expect(service.isAuthenticated()).toBe(true);
    expect(sessionStorage.getItem('coachhub.session')).toContain('test-token');
    expect(localStorage.getItem('coachhub.session')).toBeNull();
    // Only a marker that a refresh cookie may exist; the refresh token itself never reaches scripts.
    expect(localStorage.getItem('coachhub.signedIn')).toBe('true');

    TestBed.inject(HttpClient).get('/api/v1/settings').subscribe();
    const settings = http.expectOne('/api/v1/settings');
    expect(settings.request.headers.get('Authorization')).toBe('Bearer test-token');
    settings.flush({ productName: 'CoachHub', coachName: 'Coach' });
  });

  it.each([
    [['Administrator'], '/dashboard', true, false],
    [['Coach'], '/dashboard', false, true],
    [['Client'], '/portal', false, false],
  ])('routes %j to their landing page', (roles, landing, isAdmin, isCoach) => {
    setup();
    service.login({ login: 'user', password: 'Password!123' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush({ ...loginResult, roles });
    expect(service.landingRoute()).toBe(landing);
    expect(service.isAdmin()).toBe(isAdmin);
    expect(service.isCoach()).toBe(isCoach);
  });

  it('refreshes an expired access token before sending a request', () => {
    sessionStorage.setItem('coachhub.session', JSON.stringify(expired));
    localStorage.setItem('coachhub.signedIn', 'true');
    setup();

    TestBed.inject(HttpClient).get('/api/v1/clients').subscribe();
    const refresh = http.expectOne('/api/v1/auth/refresh');
    expect(refresh.request.headers.has('Authorization')).toBe(false);
    refresh.flush({ ...loginResult, accessToken: 'fresh-token' });

    const clients = http.expectOne('/api/v1/clients');
    expect(clients.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    clients.flush([]);
  });

  it('retries a refused request once after refreshing, sharing one refresh between requests', () => {
    setup();
    service.login({ login: 'user', password: 'Password!123' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush(loginResult);
    const client = TestBed.inject(HttpClient);

    client.get('/api/v1/clients').subscribe();
    client.get('/api/v1/plans').subscribe();
    http.expectOne('/api/v1/clients').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/v1/plans').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/v1/auth/refresh').flush({ ...loginResult, accessToken: 'fresh-token' });

    const retried = http.match(request => request.url.startsWith('/api/v1/') && !request.url.includes('/auth/'));
    expect(retried.map(request => request.request.headers.get('Authorization'))).toEqual(['Bearer fresh-token', 'Bearer fresh-token']);
    retried.forEach(request => request.flush([]));
  });

  it('signs the user out locally when the refresh is refused', () => {
    setup();
    service.login({ login: 'user', password: 'Password!123' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush(loginResult);
    let failed = false;

    TestBed.inject(HttpClient).get('/api/v1/clients').subscribe({ error: () => (failed = true) });
    http.expectOne('/api/v1/clients').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
    expect(service.canRefresh()).toBe(false);
    expect(sessionStorage.getItem('coachhub.session')).toBeNull();
  });

  it('logs out on the server with the current token and clears local state', () => {
    setup();
    service.login({ login: 'user', password: 'Password!123' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush(loginResult);

    service.logout(false);

    const logout = http.expectOne('/api/v1/auth/logout');
    expect(logout.request.headers.get('Authorization')).toBe('Bearer test-token');
    logout.flush(null, { status: 204, statusText: 'No Content' });
    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('coachhub.signedIn')).toBeNull();
  });

  it('restores a session from the refresh cookie only when one may exist', async () => {
    setup();
    expect(await firstValueFrom(service.ensureSession())).toBe(false);
    http.expectNone('/api/v1/auth/refresh');

    localStorage.setItem('coachhub.signedIn', 'true');
    const restored = firstValueFrom(service.ensureSession());
    http.expectOne('/api/v1/auth/refresh').flush(loginResult);
    expect(await restored).toBe(true);
    expect(service.currentUser()?.displayName).toBe('Coach');
  });

  it('keeps the signed-in name current after a profile edit', () => {
    setup();
    service.login({ login: 'user', password: 'Password!123' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush(loginResult);

    service.updateDisplayName('Renamed');

    expect(service.currentUser()?.displayName).toBe('Renamed');
    expect(sessionStorage.getItem('coachhub.session')).toContain('Renamed');
  });
});
