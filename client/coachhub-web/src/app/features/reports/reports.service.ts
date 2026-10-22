import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ProgressDashboard } from '../progress-tracking/progress-tracking.service';

export type ReportPeriod = 'Week' | 'Month';
export type ReportLanguage = 'English' | 'Arabic';
export type ClientExport = 'Client' | 'Progress' | 'Nutrition' | 'Workouts' | 'CheckIns' | 'All';
export type PlatformExport = 'Coaches' | 'Clients' | 'Subscriptions' | 'Revenue';

export const clientExports: { id: ClientExport; label: string }[] = [
  { id: 'Client', label: 'Client data' }, { id: 'Progress', label: 'Progress' }, { id: 'Nutrition', label: 'Nutrition diary' },
  { id: 'Workouts', label: 'Workout history' }, { id: 'CheckIns', label: 'Check-ins' }, { id: 'All', label: 'Everything (ZIP)' },
];
export const platformExports: { id: PlatformExport; label: string }[] = [
  { id: 'Coaches', label: 'Coaches' }, { id: 'Clients', label: 'Clients' }, { id: 'Subscriptions', label: 'Subscriptions' },
  { id: 'Revenue', label: 'Revenue ledger' },
];

export interface ReportCheckIn { dueOn: string; status: string; isLate: boolean; submittedAt: string; replies: number; }
export interface ClientReport {
  clientId: string;
  clientName: string;
  clientCode: string;
  period: ReportPeriod;
  from: string;
  to: string;
  generatedAt: string;
  brandName: string;
  brandColor: string;
  progress: ProgressDashboard;
  checkIns: ReportCheckIn[];
}
export interface DownloadedFile { blob: Blob; fileName: string; }

/** The file name from Content-Disposition (RFC 5987 filename* first), or the fallback. */
export function fileNameOf(disposition: string | null, fallback: string): string {
  if (!disposition) return fallback;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (star) return decodeURIComponent(star[1].trim());
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain ? plain[1].trim() : fallback;
}

/** Saves a downloaded file through a temporary link. */
export function saveFile(file: DownloadedFile): void {
  const url = URL.createObjectURL(file.blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = file.fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/** Weekly and monthly reports (§44) as JSON and PDF, and the CSV exports (§51). */
@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly api = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  /** A client's report as the coach sees it; without a client, the signed-in client's own. */
  report(period: ReportPeriod, date: string, clientId?: string) {
    return this.http.get<ClientReport>(this.reportBase(clientId), { params: new HttpParams().set('period', period).set('date', date) });
  }

  pdf(period: ReportPeriod, date: string, language: ReportLanguage, clientId?: string) {
    const params = new HttpParams().set('period', period).set('date', date).set('language', language);
    return this.download(`${this.reportBase(clientId)}/pdf`, params, `report-${date}.pdf`);
  }

  clientList() { return this.download(`${this.api}/exports/clients`, new HttpParams(), 'clients.csv'); }

  clientExport(clientId: string, dataset: ClientExport) {
    return this.download(`${this.api}/exports/clients/${clientId}/${dataset}`, new HttpParams(), `${dataset.toLowerCase()}.${dataset === 'All' ? 'zip' : 'csv'}`);
  }

  platformExport(dataset: PlatformExport, from?: string, to?: string) {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.download(`${this.api}/exports/platform/${dataset}`, params, `${dataset.toLowerCase()}.csv`);
  }

  private download(url: string, params: HttpParams, fallback: string): Observable<DownloadedFile> {
    return this.http.get(url, { params, responseType: 'blob', observe: 'response' }).pipe(
      map((response: HttpResponse<Blob>) => ({
        blob: response.body ?? new Blob(),
        fileName: fileNameOf(response.headers.get('Content-Disposition'), fallback),
      })));
  }

  private reportBase(clientId?: string) { return clientId ? `${this.api}/clients/${clientId}/reports` : `${this.api}/me/reports`; }
}
