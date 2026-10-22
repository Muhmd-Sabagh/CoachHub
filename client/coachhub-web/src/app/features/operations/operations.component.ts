import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, forkJoin, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { Account, Invoice, Notification, OperationsService } from './operations.service';

@Component({
  selector: 'app-operations', standalone: true, imports: [CommonModule, FormsModule], templateUrl: './operations.component.html' })
export class OperationsComponent implements OnInit {
  readonly accounts = signal<Account[]>([]); readonly invoices = signal<Invoice[]>([]); readonly notifications = signal<Notification[]>([]); readonly loading = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly account = signal({ email: '', userName: '', displayName: '', password: '', clientId: '', role: 'Client' });
  readonly invoice = signal({ clientId: '', subscriptionId: '', currencyId: '', total: 0, dueAt: '' });
  readonly payment = signal({ invoiceId: '', paymentAccountId: '', amount: 0, reference: '' });
  readonly notification = signal({ clientId: '', channel: 'Email', recipient: '', subject: '', body: '', scheduledAt: '' });
  /** D4: a delivery names the plan version sent (blank = current) and, for diets, the variant. */
  readonly delivery = signal({ clientId: '', planType: 'Diet', planId: '', versionNumber: '', variantId: '', language: 'en', channel: 'Download', recipient: '' });
  constructor(private readonly data: OperationsService, private readonly auth: AuthService) {}
  ngOnInit() { this.load(); }
  /** Platform administration (accounts) is Admin-only; coaching operations are open to coaches. */
  get isAdmin() { return this.auth.isAdmin(); }
  load() {
    this.loading.set(true);
    forkJoin([
      this.isAdmin ? this.data.accounts() : of([] as Account[]),
      this.data.invoices(),
      this.data.notifications()
    ]).subscribe({ next: ([a, i, n]) => { this.accounts.set(a); this.invoices.set(i); this.notifications.set(n); this.loading.set(false); }, error: e => { this.error.set(apiErrorMessage(e)); this.loading.set(false); } });
  }
  createAccount() {
    const clientId = this.account().role === 'Client' ? this.account().clientId || null : null;
    this.run(this.data.createAccount({ ...this.account(), userName: this.account().userName || null, password: this.account().password || null, clientId }), 'Account created.');
  }
  createInvoice() { this.run(this.data.createInvoice({ ...this.invoice(), subscriptionId: this.invoice().subscriptionId || null, dueAt: this.invoice().dueAt || null }), 'Invoice issued.'); }
  recordPayment() { this.run(this.data.pay(this.payment().invoiceId, { ...this.payment(), paymentAccountId: this.payment().paymentAccountId || null }), 'Payment settled.'); }
  schedule() { this.run(this.data.schedule({ ...this.notification(), clientId: this.notification().clientId || null, scheduledAt: this.notification().scheduledAt || null }), 'Notification scheduled.'); }
  dispatch() { this.run(this.data.dispatch(), 'Due notifications processed.'); }
  deliver() {
    const d = this.delivery();
    this.run(this.data.deliver({
      ...d,
      versionNumber: d.versionNumber ? Number(d.versionNumber) : null,
      variantId: d.planType === 'Diet' ? d.variantId || null : null,
      recipient: d.recipient || null,
    }), 'Immutable delivery record created.');
  }
  private run(request: Observable<unknown>, message: string) { this.error.set(''); this.message.set(''); request.subscribe({ next: () => { this.message.set(message); this.load(); }, error: e => this.error.set(apiErrorMessage(e)) }); }
}
