import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export type QuestionType =
  | 'ShortText'
  | 'LongText'
  | 'Number'
  | 'Date'
  | 'Boolean'
  | 'SingleChoice'
  | 'MultipleChoice'
  | 'MediaUpload';
export interface AccessCodes {
  clientCode: string;
  formCode: string;
}
export interface EligibleForm {
  id: string;
  name: string;
  formType: 'InitialAssessment' | 'UpdateAssessment';
}
export interface FormAccessResponse {
  clientId: string;
  clientName: string;
  eligibleForms: EligibleForm[];
}
export interface QuestionOption {
  id: string;
  value: string;
  label: string;
  order: number;
}
export interface AssessmentQuestion {
  id: string;
  stableKey: string;
  sectionId?: string | null;
  text: string;
  questionType: QuestionType;
  isRequired: boolean;
  order: number;
  options: QuestionOption[];
}
export interface PublishedForm {
  definitionId: string;
  name: string;
  formType: EligibleForm['formType'];
  versionId: string;
  versionNumber: number;
  status: string;
  sections: { id: string; title: string; order: number }[];
  questions: AssessmentQuestion[];
}
export interface MediaMetadata {
  id: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  externalUrl: string;
}
export interface SubmissionResponse {
  submissionId: string;
  submittedAt: string;
}
export interface SubmissionAnswer {
  questionId: string;
  value: unknown;
  mediaId?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ClientAssessmentService {
  private readonly endpoint = `${environment.apiBaseUrl}/client-forms`;
  constructor(private readonly http: HttpClient) {}
  validateAccess(codes: AccessCodes) {
    return this.http.post<FormAccessResponse>(`${this.endpoint}/access/validate`, codes);
  }
  getForm(definitionId: string, codes: AccessCodes) {
    return this.http.post<PublishedForm>(`${this.endpoint}/${definitionId}/questions`, codes);
  }
  upload(codes: AccessCodes, file: File) {
    const body = new FormData();
    body.append('clientCode', codes.clientCode);
    body.append('formCode', codes.formCode);
    body.append('file', file, file.name);
    return this.http.post<MediaMetadata>(`${this.endpoint}/media`, body);
  }
  submit(codes: AccessCodes, formDefinitionId: string, answers: SubmissionAnswer[]) {
    return this.http.post<SubmissionResponse>(`${this.endpoint}/submissions`, {
      ...codes,
      formDefinitionId,
      answers,
    });
  }
}

export function hasAnswer(question: AssessmentQuestion, value: unknown, mediaId?: string): boolean {
  if (question.questionType === 'MediaUpload') return Boolean(mediaId);
  if (question.questionType === 'Boolean') return typeof value === 'boolean';
  if (question.questionType === 'MultipleChoice') return Array.isArray(value) && value.length > 0;
  if (question.questionType === 'Number')
    return typeof value === 'number' && Number.isFinite(value);
  if (typeof value === 'string') return value.trim().length > 0;
  return value !== null && value !== undefined;
}
