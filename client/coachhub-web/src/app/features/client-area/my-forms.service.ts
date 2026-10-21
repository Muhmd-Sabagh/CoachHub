import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export type QuestionType = 'ShortText' | 'LongText' | 'Number' | 'Date' | 'Boolean' | 'SingleChoice' | 'MultipleChoice' | 'MediaUpload';
export interface EligibleForm { id: string; name: string; formType: 'InitialAssessment' | 'UpdateAssessment' | 'CheckIn'; }
export interface MyForms { clientName: string; eligibleForms: EligibleForm[]; }
export interface QuestionOption { id: string; value: string; label: string; order: number; }
export interface FormQuestion {
  id: string; stableKey: string; sectionId?: string | null; text: string; questionType: QuestionType;
  isRequired: boolean; order: number; options: QuestionOption[];
  /** The measure the answer records (P12): "Current weight" becomes a weigh-in, a front photo a progress photo. */
  metric?: string | null;
}
export interface PublishedForm {
  definitionId: string; name: string; formType: EligibleForm['formType']; versionId: string; versionNumber: number;
  sections: { id: string; title: string; order: number }[]; questions: FormQuestion[];
}
export interface UploadedMedia { id: string; originalFileName: string; contentType: string; sizeBytes: number; }
export interface AnswerInput { questionId: string; value: unknown; mediaId?: string | null; }

/** The signed-in client's forms (D5): no codes, the account decides whose forms they are. */
@Injectable({ providedIn: 'root' })
export class MyFormsService {
  private readonly url = `${environment.apiBaseUrl}/me/forms`;
  constructor(private readonly http: HttpClient) {}
  list() { return this.http.get<MyForms>(this.url); }
  get(definitionId: string) { return this.http.get<PublishedForm>(`${this.url}/${definitionId}`); }
  upload(file: File) {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<UploadedMedia>(`${this.url}/media`, body);
  }
  /** <paramref name="date"/> is the client's local day: measures in the answers are recorded for it (P12). */
  submit(definitionId: string, answers: AnswerInput[], date?: string) {
    return this.http.post<{ submissionId: string; submittedAt: string }>(`${this.url}/${definitionId}/submissions`, date ? { answers, date } : { answers });
  }
}

export function hasAnswer(question: FormQuestion, value: unknown, mediaId?: string): boolean {
  if (question.questionType === 'MediaUpload') return Boolean(mediaId);
  if (question.questionType === 'Boolean') return typeof value === 'boolean';
  if (question.questionType === 'MultipleChoice') return Array.isArray(value) && value.length > 0;
  if (question.questionType === 'Number') return typeof value === 'number' && Number.isFinite(value);
  if (typeof value === 'string') return value.trim().length > 0;
  return value !== null && value !== undefined;
}
