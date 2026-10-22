import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { I18nService } from '../../core/i18n/i18n.service';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';

export type NotificationEvent =
  | 'NewWorkout' | 'NewDiet' | 'NewProgram' | 'ProgramUpdated' | 'CheckInRequired' | 'CheckInReviewed' | 'NewMessage' | 'SubscriptionExpired';

export const notificationEvents: NotificationEvent[] = [
  'NewWorkout', 'NewDiet', 'NewProgram', 'ProgramUpdated', 'CheckInRequired', 'CheckInReviewed', 'NewMessage', 'SubscriptionExpired',
];

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
export function ago(iso: string, i18n: I18nService, now = Date.now()): string {
  const minutes = Math.floor((now - new Date(iso).getTime()) / 60000);
  if (minutes < 1) return i18n.translate('notifications.justNow');
  if (minutes < 60) return i18n.translate('notifications.minutesAgo', { count: minutes });
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return i18n.translate('notifications.hoursAgo', { count: hours });
  return i18n.formatDate(iso, 'd MMM');
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
