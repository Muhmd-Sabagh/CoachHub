import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { I18nService } from '../i18n/i18n.service';
import { LoginRequest, LoginResult, Role } from './auth.models';

const storageKey = 'coachhub.session';
/** Not a secret: only remembers that a refresh cookie may exist, so anonymous visits skip the refresh call. */
const signedInKey = 'coachhub.signedIn';

/**
 * Sessions (§2, §52). The short-lived access token lives in sessionStorage. The refresh token is
 * an HttpOnly cookie the browser sends only to /api/v1/auth, so scripts never see it; refresh()
 * trades it for a new access token, which also signs new tabs in.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly session = signal<LoginResult | null>(this.restoreSession());
  private refreshing: Observable<LoginResult> | null = null;
  readonly currentUser = this.session.asReadonly();
  readonly isAuthenticated = computed(() => this.isSessionValid(this.session()));

  constructor(private readonly http: HttpClient, private readonly router: Router, private readonly i18n: I18nService) {}

  login(request: LoginRequest): Observable<LoginResult> {
    // Signing in applies the account's saved language (P19); a refresh keeps whatever the user switched to since.
    return this.http.post<LoginResult>(`${environment.apiBaseUrl}/auth/login`, request).pipe(tap(result => {
      this.store(result);
      this.i18n.applyPreference(result.preferredLanguage);
    }));
  }

  /** Exchanges the refresh cookie for a new access token. Concurrent callers share one request. */
  refresh(): Observable<LoginResult> {
    this.refreshing ??= this.http.post<LoginResult>(`${environment.apiBaseUrl}/auth/refresh`, null).pipe(
      tap({ next: result => this.store(result), error: () => this.clear() }),
      finalize(() => (this.refreshing = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshing;
  }

  /** True when a refresh cookie may still sign this browser in. */
  canRefresh(): boolean { return safeGet(localStorage, signedInKey) === 'true'; }

  /** Resolves to whether the user is signed in, refreshing an expired access token first. */
  ensureSession(): Observable<boolean> {
    if (this.isSessionValid(this.session())) return of(true);
    if (!this.canRefresh()) return of(false);
    return this.refresh().pipe(map(() => true), catchError(() => of(false)));
  }

  /** Signs out on the server (ending this session everywhere it is used) and locally. */
  logout(redirect = true): void {
    const token = this.accessToken();
    if (token) {
      this.http.post(`${environment.apiBaseUrl}/auth/logout`, null, { headers: { Authorization: `Bearer ${token}` } })
        .pipe(catchError(() => of(null))).subscribe();
    }
    this.clear();
    if (redirect) void this.router.navigate(['/login']);
  }

  /** Ends the local session without calling the server (the server already refused it). */
  expire(): void {
    this.clear();
    void this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
  }

  /** Keeps the signed-in name current after the user edits their profile. */
  updateDisplayName(displayName: string): void {
    const current = this.session();
    if (current) this.store({ ...current, displayName });
  }

  hasRole(role: Role): boolean { return !!this.session()?.roles.includes(role); }
  isAdmin(): boolean { return this.hasRole('Administrator'); }
  isCoach(): boolean { return this.hasRole('Coach'); }

  accessToken(): string | null { return this.isSessionValid(this.session()) ? this.session()?.accessToken ?? null : null; }

  landingRoute(): string {
    if (this.hasRole('Client')) return '/app';
    if (this.isAdmin() || this.isCoach()) return '/dashboard';
    return '/login';
  }

  private store(result: LoginResult): void {
    safeSet(sessionStorage, storageKey, JSON.stringify(result));
    safeSet(localStorage, signedInKey, 'true');
    this.session.set(result);
  }

  private clear(): void {
    safeRemove(sessionStorage, storageKey);
    safeRemove(localStorage, signedInKey);
    this.session.set(null);
  }

  private restoreSession(): LoginResult | null {
    const raw = safeGet(sessionStorage, storageKey);
    if (!raw) return null;
    try {
      const result = JSON.parse(raw) as LoginResult;
      if (result?.accessToken && Array.isArray(result.roles)) return result;
    } catch { /* Discard malformed browser state. */ }
    safeRemove(sessionStorage, storageKey);
    return null;
  }

  private isSessionValid(session: LoginResult | null): boolean {
    // Treat tokens about to expire as expired so requests do not race the expiry.
    return !!session?.accessToken && new Date(session.expiresAt).getTime() - 30_000 > Date.now();
  }
}

function safeGet(storage: Storage, key: string): string | null {
  try { return storage.getItem(key); } catch { return null; }
}
function safeSet(storage: Storage, key: string, value: string): void {
  try { storage.setItem(key, value); } catch { /* Storage unavailable: the session lasts for this page only. */ }
}
function safeRemove(storage: Storage, key: string): void {
  try { storage.removeItem(key); } catch { /* Nothing stored. */ }
}
