import { Component, Input, OnChanges, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import {
  ClientExport, ClientReport, DownloadedFile, ReportLanguage, ReportPeriod, ReportsService, clientExports, saveFile,
} from './reports.service';

/**
 * Weekly and monthly reports (§44): pick a week or a month, see the summary, download the PDF in English or Arabic.
 * With <c>clientId</c> (the coach's view) it also exports the client's data as CSV (§51); without, it is the
 * signed-in client's own reports.
 */
@Component({
  selector: 'app-client-reports',
  standalone: true,
  imports: [FormsModule, ...I18N_PIPES],
  template: `
    <section class="card workbench reports">
      <h2>{{ 'reports.title' | translate }}</h2>
      @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
      <form class="controls" (ngSubmit)="load()">
        <label><span>{{ 'reports.period' | translate }}</span><select [(ngModel)]="period" name="period">
          <option value="Week">{{ 'reports.week' | translate }}</option><option value="Month">{{ 'reports.month' | translate }}</option></select></label>
        <label><span>{{ 'reports.including' | translate }}</span><input type="date" [(ngModel)]="date" name="date" required /></label>
        <label><span>{{ 'reports.pdfLanguage' | translate }}</span><select [(ngModel)]="language" name="language">
          <option value="English">English</option><option value="Arabic">العربية</option></select></label>
        <div class="actions">
          <button type="submit" class="button button-secondary" [disabled]="busy()">{{ 'reports.show' | translate }}</button>
          <button type="button" class="button button-primary" (click)="pdf()" [disabled]="busy()">{{ 'reports.downloadPdf' | translate }}</button>
        </div>
      </form>
      @if (report(); as r) {
        <p class="muted">{{ r.from | localDate: 'd MMM' }} – {{ r.to | localDate: 'd MMM y' }}</p>
        <div class="table-scroll"><table class="compact-table">
          <tbody>
            <tr><th scope="row">{{ 'reports.weight' | translate }}</th><td>{{ r.progress.body.weight.current ? (r.progress.body.weight.current.value | localNumber: '1.0-1') + ' ' + ('unit.kg' | translate) : '–' }}</td>
              <td>{{ signed(r.progress.body.weight.change, 'unit.kg') }}</td></tr>
            <tr><th scope="row">{{ 'reports.nutrition' | translate }}</th><td>{{ r.progress.nutrition.averageConsumed ? ('reports.kcalPerDay' | translate: { value: (r.progress.nutrition.averageConsumed.calories | localNumber: '1.0-0') }) : '–' }}</td>
              <td>{{ 'reports.daysLogged' | translate: { logged: r.progress.nutrition.loggedDays, days: r.progress.nutrition.days } }}</td></tr>
            <tr><th scope="row">{{ 'reports.training' | translate }}</th><td>{{ 'reports.workouts' | translate: { done: r.progress.training.summary.completedSessions, scheduled: r.progress.training.summary.scheduledSessions } }}</td>
              <td>{{ 'reports.volume' | translate: { value: (r.progress.training.summary.volumeKg | localNumber: '1.0-0') } }}</td></tr>
            <tr><th scope="row">{{ 'reports.steps' | translate }}</th><td>{{ r.progress.lifestyle.averageSteps !== null ? ('reports.stepsPerDay' | translate: { value: r.progress.lifestyle.averageSteps }) : '–' }}</td><td>{{ 'reports.daysOnTarget' | translate: { count: r.progress.lifestyle.stepsGoalDays } }}</td></tr>
            <tr><th scope="row">{{ 'reports.sleep' | translate }}</th><td>{{ r.progress.lifestyle.averageSleepHours !== null ? ('reports.sleepPerNight' | translate: { value: r.progress.lifestyle.averageSleepHours }) : '–' }}</td><td>{{ 'reports.nightsOnTarget' | translate: { count: r.progress.lifestyle.sleepGoalNights } }}</td></tr>
            <tr><th scope="row">{{ 'reports.water' | translate }}</th><td>{{ r.progress.lifestyle.averageWaterMl !== null ? ('reports.waterPerDay' | translate: { value: r.progress.lifestyle.averageWaterMl }) : '–' }}</td><td>{{ 'reports.daysOnTarget' | translate: { count: r.progress.lifestyle.waterGoalDays } }}</td></tr>
            <tr><th scope="row">{{ 'reports.adherence' | translate }}</th><td>{{ r.progress.adherence.overallPercent !== null ? (r.progress.adherence.overallPercent | localNumber) + '%' : '–' }}</td><td></td></tr>
            <tr><th scope="row">{{ 'reports.checkIns' | translate }}</th><td>{{ 'reports.checkInsSent' | translate: { count: r.checkIns.length } }}</td><td>{{ 'reports.checkInsDue' | translate: { completed: r.progress.checkIn.adherence.completed, due: r.progress.checkIn.adherence.due } }}</td></tr>
          </tbody>
        </table></div>
      }
      @if (clientId) {
        <h3>{{ 'reports.exportTitle' | translate }}</h3>
        <div class="actions">
          @for (e of exports; track e.id) {
            <button type="button" class="button button-secondary" (click)="export(e.id)" [disabled]="busy()">{{ exportLabel(e.id) | translate }}</button>
          }
        </div>
      }
    </section>
  `,
  styles: [`
    .reports { display: grid; gap: 10px; }
    .reports h2, .reports h3 { margin: 0; font-size: 1.05rem; }
    .controls { display: flex; flex-wrap: wrap; gap: 10px; align-items: flex-end; }
    .controls label { display: grid; gap: 4px; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; justify-content: flex-start; }
    .actions .button { min-height: 44px; }
    th[scope="row"] { text-align: start; }
  `],
})
export class ClientReportsComponent implements OnInit, OnChanges {
  @Input() clientId?: string;
  readonly exports = clientExports;
  readonly report = signal<ClientReport | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  period: ReportPeriod = 'Week';
  date = isoDate(new Date());
  language: ReportLanguage;

  constructor(private readonly api: ReportsService, private readonly i18n: I18nService) {
    // The PDF follows the interface language unless the user picks the other one.
    this.language = i18n.language() === 'ar' ? 'Arabic' : 'English';
  }

  exportLabel(dataset: ClientExport): TranslationKey { return `reports.export.${dataset}`; }

  ngOnInit(): void { if (!this.clientId) this.load(); }
  ngOnChanges(): void { if (this.clientId) this.load(); }

  load(): void {
    this.busy.set(true);
    this.api.report(this.period, this.date, this.clientId).subscribe({
      next: r => { this.report.set(r); this.busy.set(false); this.error.set(''); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e)); },
    });
  }

  pdf(): void { this.save(this.api.pdf(this.period, this.date, this.language, this.clientId)); }

  export(dataset: ClientExport): void { if (this.clientId) this.save(this.api.clientExport(this.clientId, dataset)); }

  signed(value: number | null, unit: TranslationKey): string {
    if (value === null || value === undefined) return '–';
    const rounded = Math.round(value * 10) / 10;
    return `${rounded > 0 ? '+' : ''}${this.i18n.formatNumber(rounded, '1.0-1')} ${this.i18n.translate(unit)}`;
  }

  private save(request: Observable<DownloadedFile>): void {
    this.busy.set(true);
    request.subscribe({
      next: file => { saveFile(file); this.busy.set(false); this.error.set(''); },
      error: e => { this.busy.set(false); this.error.set(this.i18n.translate('reports.downloadFailed', { reason: apiErrorMessage(e) })); },
    });
  }
}
