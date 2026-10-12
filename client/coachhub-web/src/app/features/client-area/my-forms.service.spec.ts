import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { time } from '../client-profile/client-profile.component';
import { ClientsService } from '../clients/clients.service';
import { FormQuestion, MyFormsService, hasAnswer } from './my-forms.service';

describe('MyFormsService (D5: signed-in client forms)', () => {
  let service: MyFormsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(MyFormsService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('lists, opens and submits forms under /me with no codes', () => {
    service.list().subscribe();
    http.expectOne('/api/v1/me/forms').flush({ clientName: 'C', eligibleForms: [] });
    service.get('f-1').subscribe();
    http.expectOne('/api/v1/me/forms/f-1').flush({});
    service.submit('f-1', [{ questionId: 'q-1', value: true }]).subscribe();
    const submit = http.expectOne('/api/v1/me/forms/f-1/submissions');
    expect(submit.request.method).toBe('POST');
    expect(submit.request.body).toEqual({ answers: [{ questionId: 'q-1', value: true }] });
    expect(JSON.stringify(submit.request.body)).not.toContain('Code');
    submit.flush({ submissionId: 's', submittedAt: '' });
  });

  it('uploads answer files as the client', () => {
    service.upload(new File([new Uint8Array([1])], 'front.png', { type: 'image/png' })).subscribe();
    const upload = http.expectOne('/api/v1/me/forms/media');
    expect((upload.request.body as FormData).has('clientCode')).toBe(false);
    upload.flush({ id: 'm' });
  });

  it.each([
    ['MediaUpload', null, 'm-1', true],
    ['MediaUpload', null, undefined, false],
    ['Boolean', false, undefined, true],
    ['MultipleChoice', [], undefined, false],
    ['Number', 0, undefined, true],
    ['ShortText', '  ', undefined, false],
  ])('decides whether a %s answer is given', (type, value, mediaId, expected) => {
    const question = { questionType: type } as FormQuestion;
    expect(hasAnswer(question, value, mediaId as string | undefined)).toBe(expected);
  });
});

describe('ClientsService onboarding and status', () => {
  let service: ClientsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ClientsService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('invites, overrides and clears status, and saves the profile', () => {
    service.invite('c-1').subscribe();
    expect(http.expectOne('/api/v1/clients/c-1/invite').request.method).toBe('POST');
    service.setStatus('c-1', 'NeedsReview', '2026-11-01').subscribe();
    const status = http.expectOne('/api/v1/clients/c-1/status');
    expect(status.request.body).toEqual({ status: 'NeedsReview', until: '2026-11-01' });
    status.flush({});
    service.clearStatus('c-1').subscribe();
    expect(http.expectOne('/api/v1/clients/c-1/status').request.method).toBe('DELETE');
    service.profile('c-1').subscribe();
    http.expectOne('/api/v1/clients/c-1/profile').flush({});
  });

  it('sends time inputs with seconds', () => {
    expect(time('23:30')).toBe('23:30:00');
    expect(time('07:00:00')).toBe('07:00:00');
    expect(time('')).toBeNull();
  });
});
