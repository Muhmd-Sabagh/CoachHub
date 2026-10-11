import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, forkJoin, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { Account, Invoice, Notification, OperationsService } from './operations.service';

@Component({ selector: 'app-operations', standalone: true, imports: [CommonModule, FormsModule], templateUrl: './operations.component.html' })
export class OperationsComponent implements OnInit {
  accounts: Account[] = []; invoices: Invoice[] = []; notifications: Notification[] = []; loading = false; error = ''; message = '';
  account = { email: '', userName: '', displayName: '', password: '', clientId: '', role: 'Client' };
  invoice = { clientId: '', subscriptionId: '', currencyId: '', total: 0, dueAt: '' };
  payment = { invoiceId: '', paymentAccountId: '', amount: 0, reference: '' };
  notification = { clientId: '', channel: 'Email', recipient: '', subject: '', body: '', scheduledAt: '' };
  delivery = { clientId: '', planType: 'Diet', planId: '', versionId: '', language: 'en', channel: 'Download', recipient: '' };
  constructor(private readonly data: OperationsService, private readonly auth: AuthService) {}
  ngOnInit() { this.load(); }
  /** Platform administration (accounts) is Admin-only; coaching operations are open to coaches. */
  get isAdmin() { return this.auth.isAdmin(); }
  load() {
    this.loading = true;
    forkJoin([
      this.isAdmin ? this.data.accounts() : of([] as Account[]),
      this.data.invoices(),
      this.data.notifications()
    ]).subscribe({ next: ([a, i, n]) => { this.accounts = a; this.invoices = i; this.notifications = n; this.loading = false; }, error: e => { this.error = apiErrorMessage(e); this.loading = false; } });
  }
  createAccount() {
    const clientId = this.account.role === 'Client' ? this.account.clientId || null : null;
    this.run(this.data.createAccount({ ...this.account, userName: this.account.userName || null, password: this.account.password || null, clientId }), 'Account created.');
  }
  createInvoice() { this.run(this.data.createInvoice({ ...this.invoice, subscriptionId: this.invoice.subscriptionId || null, dueAt: this.invoice.dueAt || null }), 'Invoice issued.'); }
  recordPayment() { this.run(this.data.pay(this.payment.invoiceId, { ...this.payment, paymentAccountId: this.payment.paymentAccountId || null }), 'Payment settled.'); }
  schedule() { this.run(this.data.schedule({ ...this.notification, clientId: this.notification.clientId || null, scheduledAt: this.notification.scheduledAt || null }), 'Notification scheduled.'); }
  dispatch() { this.run(this.data.dispatch(), 'Due notifications processed.'); }
  deliver() { this.run(this.data.deliver({ ...this.delivery, recipient: this.delivery.recipient || null }), 'Immutable delivery record created.'); }
  private run(request: Observable<unknown>, message: string) { this.error = ''; this.message = ''; request.subscribe({ next: () => { this.message = message; this.load(); }, error: e => this.error = apiErrorMessage(e) }); }
}
