import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import {
  AssessmentQuestion,
  ClientAssessmentService,
  hasAnswer,
} from './client-assessment.service';

const question = (questionType: AssessmentQuestion['questionType']): AssessmentQuestion => ({
  id: 'q1',
  stableKey: 'key',
  text: 'Question',
  questionType,
  isRequired: true,
  order: 0,
  options: [],
});

describe('ClientAssessmentService', () => {
  let service: ClientAssessmentService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ClientAssessmentService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('uses code-protected public endpoints without putting codes in the URL', () => {
    const codes = { clientCode: 'CLIENT-1', formCode: 'FORM-1' };
    service.validateAccess(codes).subscribe();
    const access = http.expectOne('/api/client-forms/access/validate');
    expect(access.request.method).toBe('POST');
    expect(access.request.body).toEqual(codes);
    access.flush({ clientId: '1', clientName: 'Client', eligibleForms: [] });

    service.getForm('definition-1', codes).subscribe();
    const form = http.expectOne('/api/client-forms/definition-1/questions');
    expect(form.request.method).toBe('POST');
    expect(form.request.body).toEqual(codes);
    form.flush({ definitionId: 'definition-1', sections: [], questions: [] });
  });

  it('posts typed answers as one submission payload', () => {
    service
      .submit({ clientCode: 'C', formCode: 'F' }, 'definition-1', [
        { questionId: 'q1', value: 72.5, mediaId: null },
      ])
      .subscribe();
    const request = http.expectOne('/api/client-forms/submissions');
    expect(request.request.body).toEqual({
      clientCode: 'C',
      formCode: 'F',
      formDefinitionId: 'definition-1',
      answers: [{ questionId: 'q1', value: 72.5, mediaId: null }],
    });
    request.flush({ submissionId: 'submission-1', submittedAt: '2026-08-13T00:00:00Z' });
  });
});

describe('hasAnswer', () => {
  it('handles boolean false, numeric zero, choices, and media as valid answers', () => {
    expect(hasAnswer(question('Boolean'), false)).toBe(true);
    expect(hasAnswer(question('Number'), 0)).toBe(true);
    expect(hasAnswer(question('MultipleChoice'), ['one'])).toBe(true);
    expect(hasAnswer(question('MediaUpload'), null, 'media-1')).toBe(true);
  });

  it('rejects empty required values', () => {
    expect(hasAnswer(question('ShortText'), '   ')).toBe(false);
    expect(hasAnswer(question('MultipleChoice'), [])).toBe(false);
    expect(hasAnswer(question('MediaUpload'), null)).toBe(false);
  });
});
