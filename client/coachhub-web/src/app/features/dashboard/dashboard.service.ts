import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { queryParams } from '../../shared/services/query-params';

/** Revenue (D6): payments settled in the period minus refunds recorded in it, per currency. */
export interface RevenueLine {
  key: string;
  id?: string | null;
  label: string;
  currencyCode: string;
  payments: number;
  refunds: number;
  settled: number;
  refunded: number;
  net: number;
}

export interface CoachDashboard {
  from: string;
  to: string;
  asOf: string;
  clients: { total: number; active: number; inactive: number };
  statuses: Array<{ status: string; count: number }>;
  checkIns: { awaitingReview: number; due: number; overdue: number };
  expiredProgramCount: number;
  expiredPrograms: Array<{ clientId: string; clientName: string; kind: 'Diet' | 'Workout'; planId: string; planName: string; endedOn: string }>;
  recentActivity: Array<{ at: string; actor: string; actorKind: string; entityType: string; operation: string; clientId: string | null; clientName: string | null }>;
  revenue: RevenueLine[];
}

export interface AdminDashboard {
  from: string;
  to: string;
  asOf: string;
  clients: number;
  coaches: number;
  activeCoaches: number;
  activeUsers: number;
  newUsers: { coaches: number; clients: number };
  expiredSubscriptions: { clients: number; endedInPeriod: number };
  revenueByCurrency: RevenueLine[];
  revenueByCoach: RevenueLine[];
  userActivity: Array<{ date: string; activeUsers: number; changes: number }>;
  usage: { workoutsLogged: number; foodEntries: number; progressEntries: number; checkInsSent: number; chatMessages: number; filesShared: number; plansDelivered: number; formSubmissions: number };
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private readonly http: HttpClient) {}

  /** The coach dashboard (§38). */
  coach(from: string, to: string) {
    return this.http.get<CoachDashboard>(`${environment.apiBaseUrl}/dashboard/coach`, { params: queryParams({ from, to }) });
  }

  /** The administrator dashboard (§48). */
  admin(from: string, to: string) {
    return this.http.get<AdminDashboard>(`${environment.apiBaseUrl}/dashboard/admin`, { params: queryParams({ from, to }) });
  }
}