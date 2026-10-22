import { ChangeDetectionStrategy, Component, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { AdminDashboard, AdvancedReport, CoachDashboard, DashboardService, OperationalReport } from './dashboard.service';
import { PlatformExport, ReportsService, platformExports, saveFile } from '../reports/reports.service';

@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager, selector: 'app-dashboard', templateUrl: './dashboard.component.html', standalone: false })
export class DashboardComponent implements OnInit {
  from = this.isoDate(-29);
  to = this.isoDate(0);
  report: OperationalReport | null = null;
  advanced: AdvancedReport | null = null;
  /** §38 for coaches, §48 for administrators; signals so the zoneless page updates when they arrive. */
  readonly coach = signal<CoachDashboard | null>(null);
  readonly admin = signal<AdminDashboard | null>(null);
  readonly statusLabels: Record<string, string> = {
    OnTrack: 'On track', NeedsReview: 'Needs review', CheckInOverdue: 'Check-in overdue', SubscriptionExpired: 'Subscription expired', Inactive: 'Inactive',
  };
  loading = false;
  error = '';

  constructor(
    readonly auth: AuthService,
    private readonly router: Router,
    private readonly data: DashboardService,
    private readonly reports: ReportsService,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';
    forkJoin([this.data.overview(this.from, this.to), this.data.advanced(this.from, this.to)])
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: ([report, advanced]) => { this.report = report; this.advanced = advanced; },
        error: (error) => (this.error = apiErrorMessage(error)),
      });
    if (this.auth.isAdmin()) {
      this.data.admin(this.from, this.to).subscribe({ next: x => this.admin.set(x), error: e => (this.error = apiErrorMessage(e)) });
    } else {
      this.data.coach(this.from, this.to).subscribe({ next: x => this.coach.set(x), error: e => (this.error = apiErrorMessage(e)) });
    }
  }

  findClient(term: string): void {
    void this.router.navigate(['/clients'], { queryParams: term ? { search: term } : {} });
  }

  activityText(item: CoachDashboard['recentActivity'][number]): string {
    const verb = item.operation === 'Create' ? 'added' : item.operation === 'Delete' ? 'removed' : 'updated';
    return `${item.actor} ${verb} ${item.entityType.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()}`;
  }

  readonly platformExports = platformExports;

  /** Platform CSV exports (§51); revenue follows the selected period. */
  exportPlatform(dataset: PlatformExport): void {
    this.reports.platformExport(dataset, this.from, this.to).subscribe({ next: file => saveFile(file), error: e => (this.error = apiErrorMessage(e)) });
  }

  maxActive(): number { return Math.max(1, ...(this.admin()?.userActivity.map(x => x.activeUsers) ?? [1])); }

  openClient(clientName: string): void {
    void this.router.navigate(['/clients'], { queryParams: { search: clientName } });
  }

  private isoDate(offset: number): string {
    const date = new Date();
    date.setDate(date.getDate() + offset);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}