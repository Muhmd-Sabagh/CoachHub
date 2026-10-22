import { Component, OnInit, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { BrandingService } from '../../core/branding/branding.service';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { MyHome, MyHomeService } from './my-home.service';

/** The client area's home page: subscription, delivered plans and invoices. */
@Component({
  selector: 'app-my-home',
  standalone: true,
  imports: [RouterLink, ...I18N_PIPES],
  template: `
    <section class="dashboard-hero">
      <div>
        <p class="eyebrow" translate="no">{{ branding.brand().brandName }}</p>
        <h1>{{ 'client.home.welcome' | translate: { name: home()?.client?.client?.name || auth.currentUser()?.displayName } }}</h1>
        @if (branding.brand().dashboardWelcome; as welcome) { <p translate="no">{{ welcome }}</p> } @else { <p>{{ 'client.home.intro' | translate }}</p> }
      </div>
      <div class="hero-actions"><a class="button button-primary" routerLink="/app/forms">{{ 'client.home.myForms' | translate }}</a> <a class="button button-secondary" routerLink="/app/calendar">{{ 'client.home.myCalendar' | translate }}</a></div>
    </section>
    @if (loading()) { <p class="table-message">{{ 'client.home.loading' | translate }}</p> }
    @if (error()) { <p class="form-error">{{ error() }}</p> }
    @if (home(); as h) {
      <section class="report-metrics">
        <article><span>{{ 'client.home.subscription' | translate }}</span><strong>{{ h.client.client.subscriptionStatus | enumLabel: 'subscriptionStatus' }}</strong><small>{{ 'common.records' | translate: { count: h.client.subscriptions.length } }}</small></article>
        <article><span>{{ 'client.home.deliveredPlans' | translate }}</span><strong>{{ h.deliveredPlans.length | localNumber }}</strong></article>
        <article><span>{{ 'client.home.openInvoices' | translate }}</span><strong>{{ openInvoices() | localNumber }}</strong></article>
      </section>
      <section class="report-columns">
        <article class="card workbench">
          <div class="section-heading"><h2>{{ 'client.home.deliveredPlans' | translate }}</h2></div>
          <div class="table-scroll"><table class="compact-table"><thead><tr><th>{{ 'client.home.plan' | translate }}</th><th>{{ 'client.home.type' | translate }}</th><th>{{ 'client.home.language' | translate }}</th><th>{{ 'client.home.delivered' | translate }}</th></tr></thead><tbody>
            @for (item of h.deliveredPlans; track item.id) { <tr><td><strong translate="no">{{ item.planName }}</strong></td><td>{{ item.planType | enumLabel: 'planType' }}</td><td>{{ item.language | enumLabel: 'planLanguage' }}</td><td>{{ item.deliveredAt | localDate }}</td></tr> }
            @empty { <tr><td colspan="4" class="table-message">{{ 'client.home.noPlans' | translate }}</td></tr> }
          </tbody></table></div>
        </article>
        <article class="card workbench">
          <div class="section-heading"><h2>{{ 'client.home.invoices' | translate }}</h2></div>
          <div class="table-scroll"><table class="compact-table"><thead><tr><th>{{ 'client.home.invoice' | translate }}</th><th>{{ 'client.home.status' | translate }}</th><th>{{ 'client.home.total' | translate }}</th><th>{{ 'client.home.balance' | translate }}</th></tr></thead><tbody>
            @for (item of h.invoices; track item.id) { <tr><td translate="no">{{ item.number }}</td><td><strong>{{ item.status | enumLabel: 'invoiceStatus' }}</strong></td><td>{{ item.total | localNumber: '1.2-2' }}</td><td>{{ item.balance | localNumber: '1.2-2' }}</td></tr> }
            @empty { <tr><td colspan="4" class="table-message">{{ 'client.home.noInvoices' | translate }}</td></tr> }
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
