import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';

export type NotificationEvent =
  | 'NewWorkout' | 'NewDiet' | 'NewProgram' | 'ProgramUpdated' | 'CheckInRequired' | 'CheckInReviewed' | 'NewMessage' | 'SubscriptionExpired';

export const notificationEvents: NotificationEvent[] = [
  'NewWorkout', 'NewDiet', 'NewProgram', 'ProgramUpdated', 'CheckInRequired', 'CheckInReviewed', 'NewMessage', 'SubscriptionExpired',
];

export const eventLabels: Record<NotificationEvent, string> = {
  NewWorkout: 'New workout plan',
  NewDiet: 'New diet plan',
  NewProgram: 'New program version',
  ProgramUpdated: 'Program updated',
  CheckInRequired: 'Check-in required',
  CheckInReviewed: 'Check-in reviewed',
  NewMessage: 'New message',
  SubscriptionExpired: 'Subscription expired',
};

export interface InboxItem {
  id: string;
  event: NotificationEvent;
  title: string;
  body: string;
  link: string | null;
  clientId: string | null;
  createdAt: string;
  readAt: string | null;
}
export interface NotificationPreferences { emailEvents: NotificationEvent[]; emailEnabledOnPlatform: boolean; }

/** "5 min ago", "3 h ago", or the date for older items. */
export function ago(iso: string, now = Date.now()): string {
  const minutes = Math.floor((now - new Date(iso).getTime()) / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
}

/** The signed-in account's in-app notifications and email preferences (§42). */
@Injectable({ providedIn: 'root' })
export class InboxService {
  private readonly base = `${environment.apiBaseUrl}/inbox`;

  constructor(private readonly http: HttpClient) {}

  list(unreadOnly = false, pageNumber = 1) {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', 20);
    if (unreadOnly) params = params.set('unreadOnly', true);
    return this.http.get<PagedResult<InboxItem>>(this.base, { params });
  }

  unreadCount() { return this.http.get<{ count: number }>(`${this.base}/unread-count`); }
  read(id: string) { return this.http.post<InboxItem>(`${this.base}/${id}/read`, null); }
  readAll() { return this.http.post<void>(`${this.base}/read-all`, null); }
  preferences() { return this.http.get<NotificationPreferences>(`${this.base}/preferences`); }
  setPreferences(emailEvents: NotificationEvent[]) { return this.http.put<NotificationPreferences>(`${this.base}/preferences`, { emailEvents }); }
}
