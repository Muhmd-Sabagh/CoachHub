import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { AdvancedReport, DashboardService, OperationalReport } from './dashboard.service';

@Component({ selector: 'app-dashboard', templateUrl: './dashboard.component.html', standalone: false })
export class DashboardComponent implements OnInit {
  from = this.isoDate(-29);
  to = this.isoDate(0);
  report: OperationalReport | null = null;
  advanced: AdvancedReport | null = null;
  loading = false;
  error = '';

  constructor(
    readonly auth: AuthService,
    private readonly router: Router,
    private readonly data: DashboardService,
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
  }

  findClient(term: string): void {
    void this.router.navigate(['/clients'], { queryParams: term ? { search: term } : {} });
  }

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