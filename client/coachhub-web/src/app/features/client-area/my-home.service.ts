import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface MyHome {
  client: { client: { name: string; subscriptionStatus: string }; subscriptions: unknown[] };
  invoices: Array<{ id: string; number: string; total: number; paid: number; balance: number; status: string }>;
  deliveredPlans: Array<{ id: string; planType: string; planName: string; language: string; channel: string; deliveredAt: string }>;
}

/** The client area's home: the signed-in client's subscription, invoices and delivered plans. */
@Injectable({ providedIn: 'root' })
export class MyHomeService {
  constructor(private readonly http: HttpClient) {}
  get() { return this.http.get<MyHome>(`${environment.apiBaseUrl}/me/home`); }
}
