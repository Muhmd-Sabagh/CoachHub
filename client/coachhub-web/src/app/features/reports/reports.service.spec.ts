import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ReportsService, fileNameOf } from './reports.service';

describe('ReportsService (§44 reports, §51 exports)', () => {
  let service: ReportsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ReportsService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('reads reports for the coach and the client', () => {
    service.report('Week', '2026-10-22', 'c').subscribe();
    http.expectOne('/api/v1/clients/c/reports?period=Week&date=2026-10-22').flush({});
    service.report('Month', '2026-10-22').subscribe();
    http.expectOne('/api/v1/me/reports?period=Month&date=2026-10-22').flush({});
  });

  it('downloads the PDF with the server file name', () => {
    let name = '';
    service.pdf('Week', '2026-10-22', 'Arabic', 'c').subscribe(file => (name = file.fileName));
    http.expectOne('/api/v1/clients/c/reports/pdf?period=Week&date=2026-10-22&language=Arabic')
      .flush(new Blob(['%PDF']), { headers: { 'Content-Disposition': "attachment; filename=r.pdf; filename*=UTF-8''sam-weekly-report.pdf" } });
    expect(name).toBe('sam-weekly-report.pdf');
  });

  it('exports CSV for a client, the client list and the platform', () => {
    service.clientExport('c', 'All').subscribe(file => expect(file.fileName).toBe('all.zip'));
    http.expectOne('/api/v1/exports/clients/c/All').flush(new Blob(['x']));
    service.clientList().subscribe();
    http.expectOne('/api/v1/exports/clients').flush(new Blob(['x']));
    service.platformExport('Revenue', '2026-10-01', '2026-10-31').subscribe();
    http.expectOne('/api/v1/exports/platform/Revenue?from=2026-10-01&to=2026-10-31').flush(new Blob(['x']));
  });

  it('reads file names from Content-Disposition', () => {
    expect(fileNameOf('attachment; filename="clients-2026.csv"', 'x.csv')).toBe('clients-2026.csv');
    expect(fileNameOf("attachment; filename*=UTF-8''%D8%B9.pdf", 'x.pdf')).toBe('ع.pdf');
    expect(fileNameOf(null, 'x.csv')).toBe('x.csv');
  });
});
