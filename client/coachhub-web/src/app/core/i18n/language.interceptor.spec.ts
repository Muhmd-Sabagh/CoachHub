import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { I18nService } from './i18n.service';
import { languageInterceptor } from './language.interceptor';

describe('languageInterceptor (P19)', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([languageInterceptor])), provideHttpClientTesting()] });
  });
  afterEach(() => { TestBed.inject(HttpTestingController).verify(); localStorage.clear(); });

  it('tells the API the interface language', () => {
    TestBed.inject(I18nService).setLanguage('ar');
    TestBed.inject(HttpClient).get('/api/v1/me/home').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/me/home');
    expect(request.request.headers.get('Accept-Language')).toBe('ar');
    request.flush({});
  });

  it('leaves other hosts alone', () => {
    TestBed.inject(HttpClient).get('https://example.test/file').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne('https://example.test/file');
    expect(request.request.headers.has('Accept-Language')).toBe(false);
    request.flush({});
  });
});
