import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { formatAuditValue } from './audit.component';
import { AuditService } from './audit.service';

describe('AuditService', () => {
  let service: AuditService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuditService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends explicit audit filters and server paging only when list is called', () => {
    service.list({
      pageNumber: 2,
      pageSize: 20,
      searchTerm: 'Coach',
      entityType: 'Client',
      operation: 'Update',
    }).subscribe();

    const request = http.expectOne((candidate) =>
      candidate.url === '/api/v1/audit-entries' && candidate.params.get('pageNumber') === '2');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('searchTerm')).toBe('Coach');
    expect(request.request.params.get('entityType')).toBe('Client');
    expect(request.request.params.get('operation')).toBe('Update');
    request.flush({ items: [], pageNumber: 2, pageSize: 20, totalCount: 0, totalPages: 0 });
  });

  it('filters by the affected client', () => {
    service.list({ pageNumber: 1, pageSize: 20, clientId: 'client-1', actorKind: 'Coach' }).subscribe();

    const request = http.expectOne((candidate) => candidate.url === '/api/v1/audit-entries');
    expect(request.request.params.get('clientId')).toBe('client-1');
    expect(request.request.params.get('actorKind')).toBe('Coach');
    request.flush({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
  });

  it.each([
    [undefined, '—'],
    [null, '—'],
    ['Old name', 'Old name'],
    ['', '""'],
    [42.5, '42.5'],
    [true, 'true'],
    [['a', 'b'], '["a","b"]'],
  ])('formats %j as %s', (value, text) => {
    expect(formatAuditValue(value)).toBe(text);
  });
});
