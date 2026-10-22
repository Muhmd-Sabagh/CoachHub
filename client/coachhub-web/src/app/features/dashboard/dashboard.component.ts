import { Component, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { AdminDashboard, CoachDashboard, DashboardService } from './dashboard.service';
import { PlatformExport, ReportsService, platformExports, saveFile } from '../reports/reports.service';

@Component({ selector: 'app-dashboard', templateUrl: './dashboard.component.html', standalone: false })
export class DashboardComponent implements OnInit {
  from = this.isoDate(-29);
  to = this.isoDate(0);
  /** §38 for coaches, §48 for administrators. */
  readonly coach = signal<CoachDashboard | null>(null);
  readonly admin = signal<AdminDashboard | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly statusLabels: Record<string, string> = {
    OnTrack: 'On track', NeedsReview: 'Needs review', CheckInOverdue: 'Check-in overdue', SubscriptionExpired: 'Subscription expired', Inactive: 'Inactive',
  };
  readonly platformExports = platformExports;

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
    this.loading.set(true);
    this.error.set('');
    const done = () => this.loading.set(false);
    const failed = (e: unknown) => { this.error.set(apiErrorMessage(e)); done(); };
    if (this.auth.isAdmin()) {
      this.data.admin(this.from, this.to).subscribe({ next: x => { this.admin.set(x); done(); }, error: failed });
    } else {
      this.data.coach(this.from, this.to).subscribe({ next: x => { this.coach.set(x); done(); }, error: failed });
    }
  }

  findClient(term: string): void {
    void this.router.navigate(['/clients'], { queryParams: term ? { search: term } : {} });
  }

  activityText(item: CoachDashboard['recentActivity'][number]): string {
    const verb = item.operation === 'Create' ? 'added' : item.operation === 'Delete' ? 'removed' : 'updated';
    return `${item.actor} ${verb} ${item.entityType.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()}`;
  }

  /** Platform CSV exports (§51); revenue follows the selected period. */
  exportPlatform(dataset: PlatformExport): void {
    this.reports.platformExport(dataset, this.from, this.to).subscribe({ next: file => saveFile(file), error: e => this.error.set(apiErrorMessage(e)) });
  }

  maxActive(): number { return Math.max(1, ...(this.admin()?.userActivity.map(x => x.activeUsers) ?? [1])); }

  private isoDate(offset: number): string {
    const date = new Date();
    date.setDate(date.getDate() + offset);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
