import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { isoDate } from '../nutrition-tracking/nutrition-tracking.service';

export type CalendarEntryKind = 'WorkoutDay' | 'RestDay' | 'CheckInDue' | 'CheckIn' | 'ProgramStart' | 'ProgramEnd' | 'Event' | 'FollowUp';
export type CalendarEventKind = 'Event' | 'FollowUp';
export type Weekday = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday';

export const weekdayNames: Weekday[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

export interface CalendarEntry {
  date: string;
  kind: CalendarEntryKind;
  title: string;
  clientId: string | null;
  clientName: string | null;
  referenceId: string | null;
  startTime: string | null;
  endTime: string | null;
  done: boolean | null;
  detail: string | null;
  planKind: 'Diet' | 'Workout' | null;
  visibleToClient: boolean | null;
}
export interface ClientCalendar { clientId: string; clientName: string; from: string; to: string; trainingWeekdays: Weekday[]; entries: CalendarEntry[]; }
export interface CoachCalendar { from: string; to: string; entries: CalendarEntry[]; }
export interface CalendarEventInput {
  clientId: string | null;
  kind: CalendarEventKind;
  title: string;
  date: string;
  notes?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  visibleToClient: boolean;
  isDone?: boolean;
}
export interface CalendarEvent extends CalendarEventInput { id: string; clientName: string | null; isDone: boolean; notes: string | null; }

/** The month grid around a month: Monday of the first week to Sunday of the last (at most 42 days). */
export function monthGrid(month: Date): { from: string; to: string; days: string[] } {
  const first = new Date(month.getFullYear(), month.getMonth(), 1);
  const start = new Date(first);
  start.setDate(first.getDate() - ((first.getDay() + 6) % 7));
  const last = new Date(month.getFullYear(), month.getMonth() + 1, 0);
  const end = new Date(last);
  end.setDate(last.getDate() + ((7 - last.getDay()) % 7));
  const days: string[] = [];
  for (const day = new Date(start); day <= end; day.setDate(day.getDate() + 1)) days.push(isoDate(day));
  return { from: days[0], to: days[days.length - 1], days };
}

/** "09:00–11:00", "09:00" or nothing. */
export function timeText(entry: Pick<CalendarEntry, 'startTime' | 'endTime'>): string {
  const short = (value: string | null) => (value ? value.slice(0, 5) : '');
  if (!entry.startTime) return '';
  return entry.endTime ? `${short(entry.startTime)}–${short(entry.endTime)}` : short(entry.startTime);
}

/** The client calendar (§45), the coach calendar (§46) and the coach's events. */
@Injectable({ providedIn: 'root' })
export class CalendarService {
  private readonly api = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  /** A client's calendar as the coach sees it; without a client, the signed-in client's own. */
  client(from: string, to: string, clientId?: string) {
    const params = new HttpParams().set('from', from).set('to', to);
    return this.http.get<ClientCalendar>(clientId ? `${this.api}/clients/${clientId}/calendar` : `${this.api}/me/calendar`, { params });
  }

  coach(from: string, to: string, clientId?: string | null) {
    let params = new HttpParams().set('from', from).set('to', to);
    if (clientId) params = params.set('clientId', clientId);
    return this.http.get<CoachCalendar>(`${this.api}/calendar`, { params });
  }

  setTrainingDays(clientId: string, weekdays: Weekday[]) {
    return this.http.put<ClientCalendar>(`${this.api}/clients/${clientId}/calendar/training-days`, { weekdays });
  }

  createEvent(input: CalendarEventInput) { return this.http.post<CalendarEvent>(`${this.api}/calendar/events`, input); }
  updateEvent(id: string, input: CalendarEventInput) { return this.http.put<CalendarEvent>(`${this.api}/calendar/events/${id}`, input); }
  deleteEvent(id: string) { return this.http.delete<void>(`${this.api}/calendar/events/${id}`); }
}
