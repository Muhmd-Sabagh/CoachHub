import { HttpClient } from '@angular/common/http';
import { Injectable, effect, signal, untracked } from '@angular/core';
import { Observable, catchError, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { ThemeService } from '../theme/theme.service';

/** The brand the current visitor sees (D1, §63). */
export interface Brand {
  coachId?: string | null;
  brandName: string;
  primaryColor: string;
  secondaryColor: string;
  logoUrl: string | null;
  faviconUrl: string | null;
  defaultAvatarUrl?: string | null;
  loginHeadline: string | null;
  loginMessage: string | null;
  dashboardWelcome?: string | null;
}

export const defaultBrand: Brand = {
  brandName: 'CoachHub', primaryColor: '#465fff', secondaryColor: '#12b76a',
  logoUrl: null, faviconUrl: null, loginHeadline: null, loginMessage: null,
};

/**
 * Loads and applies branding. Before sign-in the host decides (a coach's subdomain or custom
 * domain, else the platform); once signed in, the user's coach does. Reloads whenever the
 * signed-in user changes.
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  readonly brand = signal<Brand>(defaultBrand);
  private readonly url = `${environment.apiBaseUrl}/branding`;

  constructor(private readonly http: HttpClient, private readonly auth: AuthService, private readonly theme: ThemeService) {
    let lastUser: string | null | undefined;
    effect(() => {
      const user = this.auth.currentUser()?.userId ?? null;
      if (user === lastUser) return;
      lastUser = user;
      untracked(() => this.load().subscribe());
    });
  }

  load(): Observable<Brand> {
    const host = encodeURIComponent(location.hostname);
    const request = this.auth.currentUser()
      ? this.http.get<Brand>(`${this.url}?host=${host}`)
      : this.http.get<Brand>(`${this.url}/public?host=${host}`);
    return request.pipe(
      tap(brand => this.apply(brand)),
      catchError(() => of(this.brand())),
    );
  }

  private apply(brand: Brand): void {
    this.brand.set(brand);
    this.theme.applyBrand(brand.primaryColor, brand.secondaryColor);
    this.theme.applyIdentity(brand.brandName, brand.faviconUrl);
  }
}

/** Two letters for a brand without a logo: "Coach A Fitness" → "CA". */
export function brandInitials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  const letters = words.length > 1 ? words[0][0] + words[1][0] : (words[0] ?? 'CH').slice(0, 2);
  return letters.toUpperCase();
}
