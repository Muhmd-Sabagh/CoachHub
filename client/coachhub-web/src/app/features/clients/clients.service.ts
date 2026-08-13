import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { queryParams } from '../../shared/services/query-params';
export interface Client {
  id: string;
  clientCode: string;
  formCode: string;
  name: string;
  phone?: string | null;
  email?: string | null;
  joinDate: string;
  subscriptionStatus: string;
  dietStatus: string;
  workoutStatus: string;
  isActive: boolean;
  subscriptionCount: number;
}
export interface Subscription {
  id: string;
  clientId: string;
  packageId: string;
  startDate: string;
  endDate: string;
  durationMonths: number;
  price: number;
  currencyId: string;
  paymentAccountId?: string | null;
  renewalCount: number;
  isActive: boolean;
}
export interface ClientDetail {
  client: Client;
  subscriptions: Subscription[];
}
@Injectable({ providedIn: 'root' })
export class ClientsService {
  private readonly url = `${environment.apiBaseUrl}/clients`;
  constructor(private readonly http: HttpClient) {}
  list(q: Record<string, unknown>) {
    return this.http.get<PagedResult<Client>>(this.url, { params: queryParams(q) });
  }
  get(id: string) {
    return this.http.get<ClientDetail>(`${this.url}/${id}`);
  }
  create(input: object) {
    return this.http.post<Client>(this.url, input);
  }
  update(id: string, input: object) {
    return this.http.put<Client>(`${this.url}/${id}`, input);
  }
  delete(id: string) {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
  regenerate(id: string) {
    return this.http.post<{ formCode: string }>(`${this.url}/${id}/form-code/regenerate`, {});
  }
  createSubscription(clientId: string, input: object) {
    return this.http.post<Subscription>(`${this.url}/${clientId}/subscriptions`, input);
  }
  updateSubscription(clientId: string, id: string, input: object) {
    return this.http.put<Subscription>(`${this.url}/${clientId}/subscriptions/${id}`, input);
  }
  deleteSubscription(clientId: string, id: string) {
    return this.http.delete<void>(`${this.url}/${clientId}/subscriptions/${id}`);
  }
}
