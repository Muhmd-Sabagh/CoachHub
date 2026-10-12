import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { queryParams } from '../../shared/services/query-params';
export interface Client {
  id: string;
  coachId: string;
  clientCode: string;
  name: string;
  phone?: string | null;
  email?: string | null;
  joinDate: string;
  subscriptionStatus: string;
  dietStatus: string;
  workoutStatus: string;
  isActive: boolean;
  subscriptionCount: number;
  /** §39: computed, or the coach's manual override while it lasts. */
  status: ClientStatus;
  statusOverride?: ClientStatus | null;
  statusOverrideUntil?: string | null;
}
export type ClientStatus = 'OnTrack' | 'NeedsReview' | 'Inactive' | 'SubscriptionExpired' | 'CheckInOverdue';
export const clientStatuses: ClientStatus[] = ['OnTrack', 'NeedsReview', 'Inactive', 'SubscriptionExpired', 'CheckInOverdue'];
export type ClientAccountState = 'None' | 'Invited' | 'Active' | 'Disabled';
export interface NamedReference { id: string; nameEn: string; nameAr?: string | null; }
export const dietaryRestrictions = ['Vegetarian', 'Vegan', 'GlutenFree', 'LactoseFree', 'NutFree', 'Halal', 'Pescatarian', 'LowSodium', 'Diabetic'] as const;
export interface ClientProfileFields {
  dateOfBirth: string | null; sex: 'Male' | 'Female' | null; heightCm: number | null; startingWeightKg: number | null;
  startingBodyFatPercent: number | null;
  goal: 'LoseFat' | 'BuildMuscle' | 'Recomposition' | 'Maintain' | 'Performance' | 'GeneralHealth' | null;
  activityLevel: 'Sedentary' | 'LightlyActive' | 'ModeratelyActive' | 'VeryActive' | 'ExtraActive' | null;
  trainingDaysPerWeek: number | null; averageDailySteps: number | null; trainingLocation: 'Gym' | 'Home' | 'Outdoor' | 'Mixed' | null;
  trainingPreferences: string | null; foodPreferences: string | null; preferredFoodsNote: string | null; dislikedFoodsNote: string | null;
  dietaryRestrictions: string[]; dietaryNotes: string | null; preferredMealCount: number | null; bedtime: string | null; wakeTime: string | null;
  lifestyleNotes: string | null;
}
export interface ClientProfile extends ClientProfileFields {
  clientId: string; isSaved: boolean; age: number | null;
  equipment: NamedReference[]; preferredFoods: NamedReference[]; dislikedFoods: NamedReference[];
}
export interface ClientProfileInput extends ClientProfileFields { equipmentIds: string[]; preferredFoodIds: string[]; dislikedFoodIds: string[]; }
export interface ClientAccount { state: ClientAccountState; email?: string | null; lastLoginAt?: string | null; }
export interface SubscriptionRenewal {
  id: string;
  sequenceNumber: number;
  previousEndDate: string;
  newEndDate: string;
  durationMonths: number;
  price: number;
  currencyId: string;
  paymentAccountId?: string | null;
  recordedAt: string;
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
  renewals: SubscriptionRenewal[];
}
export interface ClientDetail {
  client: Client;
  subscriptions: Subscription[];
  account: ClientAccount;
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
  /** Administrator only: moves the client to another coach (commercial history stays, D7). */
  transfer(id: string, coachId: string) {
    return this.http.post<Client>(`${this.url}/${id}/transfer`, { coachId });
  }
  /** Creates the client's login if needed and emails a set-password invitation (onboarding). */
  invite(id: string) {
    return this.http.post<ClientAccount>(`${this.url}/${id}/invite`, {});
  }
  /** Sets the status by hand (§39) until a date, or until cleared. */
  setStatus(id: string, status: ClientStatus, until: string | null) {
    return this.http.put<Client>(`${this.url}/${id}/status`, { status, until });
  }
  clearStatus(id: string) {
    return this.http.delete<Client>(`${this.url}/${id}/status`);
  }
  /** The structured profile (§3, D3). */
  profile(id: string) {
    return this.http.get<ClientProfile>(`${this.url}/${id}/profile`);
  }
  saveProfile(id: string, input: ClientProfileInput) {
    return this.http.put<ClientProfile>(`${this.url}/${id}/profile`, input);
  }
  createSubscription(clientId: string, input: object) {
    return this.http.post<Subscription>(`${this.url}/${clientId}/subscriptions`, input);
  }
  updateSubscription(clientId: string, id: string, input: object) {
    return this.http.put<Subscription>(`${this.url}/${clientId}/subscriptions/${id}`, input);
  }
  renewSubscription(clientId: string, id: string, input: object) {
    return this.http.post<Subscription>(
      `${this.url}/${clientId}/subscriptions/${id}/renewals`,
      input,
    );
  }
  deleteSubscription(clientId: string, id: string) {
    return this.http.delete<void>(`${this.url}/${clientId}/subscriptions/${id}`);
  }
}
