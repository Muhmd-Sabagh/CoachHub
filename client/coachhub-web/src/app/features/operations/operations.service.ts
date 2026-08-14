import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface Account { id: string; email: string; displayName: string; isActive: boolean; clientId?: string; roles: string[]; permissions: string[]; }
export interface Invoice { id: string; clientId: string; number: string; total: number; paid: number; balance: number; status: string; issuedAt: string; payments: unknown[]; }
export interface Notification { id: string; channel: string; recipient: string; subject: string; scheduledAt: string; status: string; attemptCount: number; lastError?: string; }

@Injectable({ providedIn: 'root' })
export class OperationsService {
  constructor(private readonly http: HttpClient) {}
  accounts() { return this.http.get<Account[]>(`${environment.apiBaseUrl}/accounts`); }
  createAccount(input: object) { return this.http.post<Account>(`${environment.apiBaseUrl}/accounts`, input); }
  invoices() { return this.http.get<Invoice[]>(`${environment.apiBaseUrl}/billing/invoices`); }
  createInvoice(input: object) { return this.http.post<Invoice>(`${environment.apiBaseUrl}/billing/invoices`, input); }
  pay(id: string, input: object) { return this.http.post<Invoice>(`${environment.apiBaseUrl}/billing/invoices/${id}/payments`, input); }
  notifications() { return this.http.get<Notification[]>(`${environment.apiBaseUrl}/notifications`); }
  schedule(input: object) { return this.http.post<Notification>(`${environment.apiBaseUrl}/notifications`, input); }
  dispatch() { return this.http.post<{ sent: number }>(`${environment.apiBaseUrl}/notifications/dispatch`, {}); }
  deliver(input: object) { return this.http.post(`${environment.apiBaseUrl}/plan-deliveries`, input); }
}
