import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { BrandingService } from '../../core/branding/branding.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { MyHome, MyHomeService } from './my-home.service';

/** The client area's home page: subscription, delivered plans and invoices. */
@Component({
  selector: 'app-my-home',
  standalone: true,
  imports: [DatePipe, RouterLink],
  template: `
    <section class="dashboard-hero">
      <div>
        <p class="eyebrow">{{ branding.brand().brandName }}</p>
        <h1>Welcome, {{ home()?.client?.client?.name || auth.currentUser()?.displayName }}</h1>
        <p>{{ branding.brand().dashboardWelcome || 'Your subscription, delivered plans and invoices in one private workspace.' }}</p>
      </div>
      <div class="hero-actions"><a class="button button-primary" routerLink="/app/forms">My forms</a> <a class="button button-secondary" routerLink="/app/calendar">My calendar</a></div>
    </section>
    @if (loading()) { <p class="table-message">Loading your workspace…</p> }
    @if (error()) { <p class="form-error">{{ error() }}</p> }
    @if (home(); as h) {
      <section class="report-metrics">
        <article><span>Subscription</span><strong>{{ subscription() }}</strong><small>{{ h.client.subscriptions.length }} record(s)</small></article>
        <article><span>Delivered plans</span><strong>{{ h.deliveredPlans.length }}</strong></article>
        <article><span>Open invoices</span><strong>{{ openInvoices() }}</strong></article>
      </section>
      <section class="report-columns">
        <article class="card workbench">
          <div class="section-heading"><h2>Delivered plans</h2></div>
          <div class="table-scroll"><table class="compact-table"><thead><tr><th>Plan</th><th>Type</th><th>Language</th><th>Delivered</th></tr></thead><tbody>
            @for (item of h.deliveredPlans; track item.id) { <tr><td><strong>{{ item.planName }}</strong></td><td>{{ item.planType }}</td><td>{{ item.language }}</td><td>{{ item.deliveredAt | date: 'mediumDate' }}</td></tr> }
            @empty { <tr><td colspan="4" class="table-message">No plans delivered yet.</td></tr> }
          </tbody></table></div>
        </article>
        <article class="card workbench">
          <div class="section-heading"><h2>Invoices</h2></div>
          <div class="table-scroll"><table class="compact-table"><thead><tr><th>Invoice</th><th>Status</th><th>Total</th><th>Balance</th></tr></thead><tbody>
            @for (item of h.invoices; track item.id) { <tr><td>{{ item.number }}</td><td><strong>{{ item.status }}</strong></td><td>{{ item.total }}</td><td>{{ item.balance }}</td></tr> }
            @empty { <tr><td colspan="4" class="table-message">No invoices.</td></tr> }
          </tbody></table></div>
        </article>
      </section>
    }
  `,
})
export class MyHomeComponent implements OnInit {
  readonly home = signal<MyHome | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  /** "Inactive" means no subscription; other statuses read as words ("Active", "Suspended"). */
  readonly subscription = computed(() => {
    const status = this.home()?.client.client.subscriptionStatus ?? '';
    return status === 'Inactive' ? 'None' : status;
  });
  readonly openInvoices = computed(() => this.home()?.invoices.filter(x => x.balance > 0).length ?? 0);

  constructor(private readonly data: MyHomeService, readonly auth: AuthService, readonly branding: BrandingService) {}

  ngOnInit(): void {
    this.loading.set(true);
    this.data.get().subscribe({
      next: x => { this.home.set(x); this.loading.set(false); },
      error: e => { this.error.set(apiErrorMessage(e)); this.loading.set(false); },
    });
  }
}
