import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';

export type CheckInStatus = 'AwaitingReview' | 'Approved' | 'ChangesRequested';
export type CheckInDueState = 'Upcoming' | 'Pending' | 'Overdue';
export type Weekday = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';
export type AdherenceArea = 'Nutrition' | 'Workout' | 'CheckIn' | 'Steps' | 'Water' | 'Sleep';

export const weekdays: Weekday[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

export interface CheckInSchedule {
  clientId: string;
  formDefinitionId: string;
  formName: string;
  intervalWeeks: number;
  dueWeekday: Weekday;
  startsOn: string;
  nextDueOn: string | null;
  opensOn: string | null;
  state: CheckInDueState | null;
}
export interface CheckInScheduleInput { formDefinitionId: string; intervalWeeks: number; dueWeekday: Weekday; startsOn?: string | null; }
export interface CheckInSummary {
  id: string;
  clientId: string;
  clientName: string;
  formDefinitionId: string;
  formName: string;
  dueOn: string;
  submittedAt: string;
  isLate: boolean;
  status: CheckInStatus;
  submissionCount: number;
  replyCount: number;
}
export interface CheckInDue { clientId: string; clientName: string; dueOn: string; state: CheckInDueState; daysOverdue: number; }
export interface CheckInReply { id: string; text: string; authorName: string | null; createdAt: string; }
export interface CheckInSubmission { formSubmissionId: string; submittedAt: string; isLate: boolean; }
export interface CheckInAnswer { id: string; questionText: string; questionType: string; valueJson: string; mediaId?: string | null; }
export interface CheckInDetail {
  checkIn: CheckInSummary;
  answers: CheckInAnswer[];
  submissions: CheckInSubmission[];
  replies: CheckInReply[];
  privateNotes: string | null;
  reviewedAt: string | null;
}
export interface MyCheckIn {
  id: string;
  formDefinitionId: string;
  formName: string;
  dueOn: string;
  submittedAt: string;
  isLate: boolean;
  status: CheckInStatus;
  submissions: CheckInSubmission[];
  replies: CheckInReply[];
  answers: CheckInAnswer[];
}
export interface MyCheckIns { schedule: CheckInSchedule | null; resubmitCheckInId: string | null; history: MyCheckIn[]; }
export interface CheckInAdherence { due: number; completed: number; late: number; percent: number | null; }
export interface Adherence {
  from: string;
  to: string;
  areas: { area: AdherenceArea; percent: number | null; weight: number }[];
  overallPercent: number | null;
}

/**
 * The check-in workflow (§28–§30): coaches schedule, review and reply under /check-ins and
 * /clients/{id}/check-in-schedule; the client reads their own under /me/check-ins (they submit through /me/forms).
 */
@Injectable({ providedIn: 'root' })
export class CheckInsService {
  private readonly base = `${environment.apiBaseUrl}/check-ins`;
  constructor(private readonly http: HttpClient) {}

  list(params: { clientId?: string; status?: CheckInStatus | ''; pageNumber?: number }) {
    const query: Record<string, string | number> = { pageNumber: params.pageNumber ?? 1 };
    if (params.clientId) query['clientId'] = params.clientId;
    if (params.status) query['status'] = params.status;
    return this.http.get<PagedResult<CheckInSummary>>(this.base, { params: query });
  }
  due(state: 'Pending' | 'Overdue', pageNumber = 1) {
    return this.http.get<PagedResult<CheckInDue>>(`${this.base}/due`, { params: { state, pageNumber } });
  }
  get(id: string) {
    return this.http.get<CheckInDetail>(`${this.base}/${id}`);
  }
  reply(id: string, text: string) {
    return this.http.post<CheckInDetail>(`${this.base}/${id}/replies`, { text });
  }
  notes(id: string, notes: string | null) {
    return this.http.put<CheckInDetail>(`${this.base}/${id}/notes`, { notes });
  }
  approve(id: string, message: string | null) {
    return this.http.post<CheckInDetail>(`${this.base}/${id}/approve`, { message });
  }
  requestChanges(id: string, message: string) {
    return this.http.post<CheckInDetail>(`${this.base}/${id}/request-changes`, { message });
  }

  /** 204 (no schedule) comes back as null. */
  schedule(clientId: string) {
    return this.http.get<CheckInSchedule | null>(`${environment.apiBaseUrl}/clients/${clientId}/check-in-schedule`);
  }
  setSchedule(clientId: string, input: CheckInScheduleInput) {
    return this.http.put<CheckInSchedule>(`${environment.apiBaseUrl}/clients/${clientId}/check-in-schedule`, input);
  }
  deleteSchedule(clientId: string) {
    return this.http.delete<void>(`${environment.apiBaseUrl}/clients/${clientId}/check-in-schedule`);
  }
  adherence(from: string, to: string, clientId?: string) {
    const url = clientId ? `${environment.apiBaseUrl}/clients/${clientId}/progress/adherence` : `${environment.apiBaseUrl}/me/progress/adherence`;
    return this.http.get<Adherence>(url, { params: { from, to } });
  }

  mine() {
    return this.http.get<MyCheckIns>(`${environment.apiBaseUrl}/me/check-ins`);
  }
  myCheckIn(id: string) {
    return this.http.get<MyCheckIn>(`${environment.apiBaseUrl}/me/check-ins/${id}`);
  }
}

export const statusLabels: Record<CheckInStatus, string> = {
  AwaitingReview: 'Awaiting review',
  Approved: 'Approved',
  ChangesRequested: 'Changes requested',
};

export const dueStateLabels: Record<CheckInDueState, string> = { Upcoming: 'Upcoming', Pending: 'Due', Overdue: 'Overdue' };

/** "Every week" / "Every 2 weeks" on a weekday. */
export function cadenceText(schedule: Pick<CheckInSchedule, 'intervalWeeks' | 'dueWeekday'>): string {
  return `${schedule.intervalWeeks === 1 ? 'Every week' : `Every ${schedule.intervalWeeks} weeks`} on ${schedule.dueWeekday}`;
}

/** An answer's value for reading: text, numbers and choices as written; a photo as "Photo". */
export function answerText(answer: CheckInAnswer): string {
  if (answer.mediaId) return 'Photo';
  try {
    const value = JSON.parse(answer.valueJson) as unknown;
    if (Array.isArray(value)) return value.join(', ');
    if (typeof value === 'boolean') return value ? 'Yes' : 'No';
    return value === null || value === undefined ? '–' : String(value);
  } catch {
    return answer.valueJson;
  }
}
