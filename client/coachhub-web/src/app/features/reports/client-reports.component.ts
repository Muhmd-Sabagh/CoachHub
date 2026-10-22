import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
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
  imports: [CommonModule, FormsModule],
  template: `
    <section class="card workbench reports">
      <h2>Reports</h2>
      @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
      <form class="controls" (ngSubmit)="load()">
        <label><span>Period</span><select [(ngModel)]="period" name="period">
          <option value="Week">Week (Mon–Sun)</option><option value="Month">Month</option></select></label>
        <label><span>Including</span><input type="date" [(ngModel)]="date" name="date" required /></label>
        <label><span>PDF language</span><select [(ngModel)]="language" name="language">
          <option value="English">English</option><option value="Arabic">العربية</option></select></label>
        <div class="actions">
          <button type="submit" class="button button-secondary" [disabled]="busy()">Show</button>
          <button type="button" class="button button-primary" (click)="pdf()" [disabled]="busy()">Download PDF</button>
        </div>
      </form>

      @if (report(); as r) {
        <p class="muted">{{ r.from | date: 'd MMM' }} – {{ r.to | date: 'd MMM y' }}</p>
        <div class="table-scroll"><table>
          <tbody>
            <tr><th scope="row">Weight</th><td>{{ r.progress.body.weight.current ? r.progress.body.weight.current.value + ' kg' : '–' }}</td>
              <td>{{ signed(r.progress.body.weight.change, ' kg') }}</td></tr>
            <tr><th scope="row">Nutrition</th><td>{{ r.progress.nutrition.averageConsumed ? (r.progress.nutrition.averageConsumed.calories | number: '1.0-0') + ' kcal/day' : '–' }}</td>
              <td>{{ r.progress.nutrition.loggedDays }} / {{ r.progress.nutrition.days }} days logged</td></tr>
            <tr><th scope="row">Training</th><td>{{ r.progress.training.summary.completedSessions }} / {{ r.progress.training.summary.scheduledSessions }} workouts</td>
              <td>{{ r.progress.training.summary.volumeKg | number: '1.0-0' }} kg volume</td></tr>
            <tr><th scope="row">Steps</th><td>{{ r.progress.lifestyle.averageSteps !== null ? r.progress.lifestyle.averageSteps + ' a day' : '–' }}</td><td>{{ r.progress.lifestyle.stepsGoalDays }} days on target</td></tr>
            <tr><th scope="row">Sleep</th><td>{{ r.progress.lifestyle.averageSleepHours !== null ? r.progress.lifestyle.averageSleepHours + ' h a night' : '–' }}</td><td>{{ r.progress.lifestyle.sleepGoalNights }} nights on target</td></tr>
            <tr><th scope="row">Water</th><td>{{ r.progress.lifestyle.averageWaterMl !== null ? r.progress.lifestyle.averageWaterMl + ' ml a day' : '–' }}</td><td>{{ r.progress.lifestyle.waterGoalDays }} days on target</td></tr>
            <tr><th scope="row">Adherence</th><td>{{ r.progress.adherence.overallPercent !== null ? r.progress.adherence.overallPercent + '%' : '–' }}</td><td></td></tr>
            <tr><th scope="row">Check-ins</th><td>{{ r.checkIns.length }} sent</td><td>{{ r.progress.checkIn.adherence.completed }} / {{ r.progress.checkIn.adherence.due }} due</td></tr>
          </tbody>
        </table></div>
      }

      @if (clientId) {
        <h3>Export data (CSV)</h3>
        <div class="actions">
          @for (e of exports; track e.id) {
            <button type="button" class="button button-secondary" (click)="export(e.id)" [disabled]="busy()">{{ e.label }}</button>
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
  language: ReportLanguage = 'English';

  constructor(private readonly api: ReportsService) {}

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

  signed(value: number | null, unit: string): string {
    if (value === null || value === undefined) return '–';
    const rounded = Math.round(value * 10) / 10;
    return `${rounded > 0 ? '+' : ''}${rounded}${unit}`;
  }

  private save(request: Observable<DownloadedFile>): void {
    this.busy.set(true);
    request.subscribe({
      next: file => { saveFile(file); this.busy.set(false); this.error.set(''); },
      error: e => { this.busy.set(false); this.error.set('The download failed. ' + apiErrorMessage(e)); },
    });
  }
}
