import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { finalize } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';
import { apiErrorMessage } from '../../shared/services/api-error';

interface PortalOverview { client: { client: { name: string; clientCode: string; subscriptionStatus: string; dietStatus: string; workoutStatus: string }; subscriptions: unknown[] }; invoices: Array<{ id: string; number: string; total: number; paid: number; balance: number; status: string }>; deliveredPlans: Array<{ id: string; planType: string; planName: string; language: string; channel: string; deliveredAt: string }>; }
@Component({ selector: 'app-client-portal', standalone: true, imports: [CommonModule], templateUrl: './client-portal.component.html' })
export class ClientPortalComponent implements OnInit {
  overview: PortalOverview | null = null; loading = false; error = '';
  constructor(private readonly http: HttpClient, readonly auth: AuthService) {}
  ngOnInit() { this.loading = true; this.http.get<PortalOverview>(`${environment.apiBaseUrl}/client-portal/overview`).pipe(finalize(() => this.loading = false)).subscribe({ next: x => this.overview = x, error: e => this.error = apiErrorMessage(e) }); }
}
