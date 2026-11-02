import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { queryParams } from '../../shared/services/query-params';
export interface FormSummary {
  id: string;
  name: string;
  formType: string;
  isArchived: boolean;
}
export interface Section {
  id: string;
  title: string;
  order: number;
}
export interface Option {
  id: string;
  value: string;
  label: string;
  order: number;
}
export interface Question {
  id: string;
  stableKey: string;
  sectionId?: string | null;
  text: string;
  questionType: string;
  isRequired: boolean;
  order: number;
  options: Option[];
  /** The measure the answer records (P12), for example the weight or a front photo. */
  metric?: QuestionMetric | null;
}

export type QuestionMetric =
  | 'WeightKg' | 'BodyFatPercent' | 'WaistCm' | 'ChestCm' | 'ArmCm' | 'ThighCm' | 'HipCm' | 'NeckCm' | 'CalfCm'
  | 'Steps' | 'SleepHours' | 'WaterMl' | 'FrontPhoto' | 'SidePhoto' | 'BackPhoto';

/** Measures a question can record, by the answer type they need (P12); labels are `enum.questionMetric.<value>`. */
export const questionMetrics: { value: QuestionMetric; type: 'Number' | 'MediaUpload' }[] = [
  { value: 'WeightKg', type: 'Number' },
  { value: 'BodyFatPercent', type: 'Number' },
  { value: 'WaistCm', type: 'Number' },
  { value: 'ChestCm', type: 'Number' },
  { value: 'ArmCm', type: 'Number' },
  { value: 'ThighCm', type: 'Number' },
  { value: 'HipCm', type: 'Number' },
  { value: 'NeckCm', type: 'Number' },
  { value: 'CalfCm', type: 'Number' },
  { value: 'Steps', type: 'Number' },
  { value: 'SleepHours', type: 'Number' },
  { value: 'WaterMl', type: 'Number' },
  { value: 'FrontPhoto', type: 'MediaUpload' },
  { value: 'SidePhoto', type: 'MediaUpload' },
  { value: 'BackPhoto', type: 'MediaUpload' },
];
export interface FormVersion {
  definitionId: string;
  name: string;
  formType: string;
  versionId: string;
  versionNumber: number;
  status: string;
  sections: Section[];
  questions: Question[];
}
export interface SubmissionSummary {
  id: string;
  clientId: string;
  clientName: string;
  formDefinitionId: string;
  formName: string;
  formType: string;
  source: string;
  submittedAt: string;
  answerCount: number;
}
export interface SubmissionDetail {
  submission: SubmissionSummary;
  answers: {
    id: string;
    questionText: string;
    questionType: string;
    valueJson: string;
    mediaId?: string | null;
    externalMediaUrl?: string | null;
  }[];
}
@Injectable({ providedIn: 'root' })
export class AssessmentsService {
  private forms = `${environment.apiBaseUrl}/assessment-forms`;
  private submissions = `${environment.apiBaseUrl}/assessment-submissions`;
  constructor(private http: HttpClient) {}
  listForms(q: Record<string, unknown>) {
    return this.http.get<PagedResult<FormSummary>>(this.forms, { params: queryParams(q) });
  }
  create(input: object) {
    return this.http.post<FormVersion>(this.forms, input);
  }
  preview(id: string) {
    return this.http.get<FormVersion>(`${this.forms}/${id}/preview`);
  }
  update(id: string, input: object) {
    return this.http.put<FormSummary>(`${this.forms}/${id}`, input);
  }
  addSection(id: string, input: object) {
    return this.http.post<Section>(`${this.forms}/${id}/sections`, input);
  }
  addQuestion(id: string, input: object) {
    return this.http.post<Question>(`${this.forms}/${id}/questions`, input);
  }
  updateQuestion(id: string, qid: string, input: object) {
    return this.http.put<Question>(`${this.forms}/${id}/questions/${qid}`, input);
  }
  deleteQuestion(id: string, qid: string) {
    return this.http.delete<void>(`${this.forms}/${id}/questions/${qid}`);
  }
  reorder(id: string, ids: string[]) {
    return this.http.put<void>(`${this.forms}/${id}/questions/order`, { questionIds: ids });
  }
  publish(id: string) {
    return this.http.post<FormVersion>(`${this.forms}/${id}/publish`, {});
  }
  draft(id: string) {
    return this.http.post<FormVersion>(`${this.forms}/${id}/drafts`, {});
  }
  listSubmissions(q: Record<string, unknown>) {
    return this.http.get<PagedResult<SubmissionSummary>>(this.submissions, {
      params: queryParams(q),
    });
  }
  getSubmission(id: string) {
    return this.http.get<SubmissionDetail>(`${this.submissions}/${id}`);
  }
}
@Injectable({ providedIn: 'root' })
export class AssessmentReviewStore {
  readonly detail = signal<SubmissionDetail | null>(null);
  open(detail: SubmissionDetail) {
    this.detail.set(detail);
  }
  close() {
    this.detail.set(null);
  }
}
