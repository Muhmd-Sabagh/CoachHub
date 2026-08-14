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
  account = { email: '', displayName: '', password: '', clientId: '', role: 'Client', permissions: '' };
  invoice = { clientId: '', subscriptionId: '', currencyId: '', total: 0, dueAt: '' };
  payment = { invoiceId: '', paymentAccountId: '', amount: 0, reference: '' };
  notification = { clientId: '', channel: 'Email', recipient: '', subject: '', body: '', scheduledAt: '' };
  delivery = { clientId: '', planType: 'Diet', planId: '', versionId: '', language: 'en', channel: 'Download', recipient: '' };
  constructor(private readonly data: OperationsService, private readonly auth: AuthService) {}
  ngOnInit() { this.load(); }
  can(permission: string) { const user = this.auth.currentUser(); return !!user?.roles.includes('Administrator') || !!user?.permissions?.includes(permission); }
  load() {
    this.loading = true;
    forkJoin([
      this.can('users.manage') ? this.data.accounts() : of([] as Account[]),
      this.can('billing.manage') ? this.data.invoices() : of([] as Invoice[]),
      this.can('communications.manage') ? this.data.notifications() : of([] as Notification[])
    ]).subscribe({ next: ([a, i, n]) => { this.accounts = a; this.invoices = i; this.notifications = n; this.loading = false; }, error: e => { this.error = apiErrorMessage(e); this.loading = false; } });
  }
  createAccount() {
    const roles = [this.account.role];
    const clientId = this.account.role === 'Client' ? this.account.clientId || null : null;
    const permissions = this.account.role === 'Administrator' ? [] : this.account.permissions.split(',').map(x => x.trim()).filter(Boolean);
    this.run(this.data.createAccount({ ...this.account, password: this.account.password || null, clientId, roles, permissions }), 'Account created.');
  }
  createInvoice() { this.run(this.data.createInvoice({ ...this.invoice, subscriptionId: this.invoice.subscriptionId || null, dueAt: this.invoice.dueAt || null }), 'Invoice issued.'); }
  recordPayment() { this.run(this.data.pay(this.payment.invoiceId, { ...this.payment, paymentAccountId: this.payment.paymentAccountId || null }), 'Payment settled.'); }
  schedule() { this.run(this.data.schedule({ ...this.notification, clientId: this.notification.clientId || null, scheduledAt: this.notification.scheduledAt || null }), 'Notification scheduled.'); }
  dispatch() { this.run(this.data.dispatch(), 'Due notifications processed.'); }
  deliver() { this.run(this.data.deliver({ ...this.delivery, recipient: this.delivery.recipient || null }), 'Immutable delivery record created.'); }
  private run(request: Observable<unknown>, message: string) { this.error = ''; this.message = ''; request.subscribe({ next: () => { this.message = message; this.load(); }, error: e => this.error = apiErrorMessage(e) }); }
}
