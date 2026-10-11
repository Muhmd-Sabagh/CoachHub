import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResult, Role } from './auth.models';

const storageKey = 'coachhub.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly session = signal<LoginResult | null>(this.restoreSession());
  readonly currentUser = this.session.asReadonly();
  readonly isAuthenticated = computed(() => this.isSessionValid(this.session()));

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  login(request: LoginRequest): Observable<LoginResult> {
    return this.http.post<LoginResult>(`${environment.apiBaseUrl}/auth/login`, request).pipe(tap(result => {
      sessionStorage.setItem(storageKey, JSON.stringify(result));
      this.session.set(result);
    }));
  }

  logout(redirect = true): void {
    sessionStorage.removeItem(storageKey);
    this.session.set(null);
    if (redirect) void this.router.navigate(['/login']);
  }

  hasRole(role: Role): boolean { return !!this.session()?.roles.includes(role); }
  isAdmin(): boolean { return this.hasRole('Administrator'); }
  isCoach(): boolean { return this.hasRole('Coach'); }

  accessToken(): string | null { return this.isAuthenticated() ? this.session()?.accessToken ?? null : null; }

  landingRoute(): string {
    if (this.hasRole('Client')) return '/portal';
    if (this.isAdmin() || this.isCoach()) return '/dashboard';
    return '/login';
  }

  private restoreSession(): LoginResult | null {
    const raw = sessionStorage.getItem(storageKey);
    if (!raw) return null;
    try {
      const result = JSON.parse(raw) as LoginResult;
      if (this.isSessionValid(result)) return result;
    } catch { /* Discard malformed browser state. */ }
    sessionStorage.removeItem(storageKey);
    return null;
  }

  private isSessionValid(session: LoginResult | null): boolean {
    return !!session?.accessToken && new Date(session.expiresAt).getTime() > Date.now();
  }
}