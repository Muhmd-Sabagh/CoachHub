import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';
import { BrandingService } from '../../core/branding/branding.service';
import { apiErrorMessage } from '../../shared/services/api-error';

interface PortalOverview { client: { client: { name: string; clientCode: string; subscriptionStatus: string; dietStatus: string; workoutStatus: string }; subscriptions: unknown[] }; invoices: Array<{ id: string; number: string; total: number; paid: number; balance: number; status: string }>; deliveredPlans: Array<{ id: string; planType: string; planName: string; language: string; channel: string; deliveredAt: string }>; }
@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager, selector: 'app-client-portal', standalone: true, imports: [CommonModule, RouterLink], templateUrl: './client-portal.component.html' })
export class ClientPortalComponent implements OnInit {
  overview: PortalOverview | null = null; loading = false; error = '';
  constructor(private readonly http: HttpClient, readonly auth: AuthService, readonly branding: BrandingService) {}
  /** "NotStarted" → "Not started" (P10A: enum names read as words). */
  label(value: string): string { return value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/ ([A-Z])/g, (_, c: string) => ' ' + c.toLowerCase()); }
  ngOnInit() { this.loading = true; this.http.get<PortalOverview>(`${environment.apiBaseUrl}/client-portal/overview`).pipe(finalize(() => this.loading = false)).subscribe({ next: x => this.overview = x, error: e => this.error = apiErrorMessage(e) }); }
}
