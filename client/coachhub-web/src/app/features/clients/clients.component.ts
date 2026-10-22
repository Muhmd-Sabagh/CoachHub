import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import { Coach, CoachesService } from '../coaches/coaches.service';
import { ReferenceDataService, ReferenceRecord } from '../settings/reference-data.service';
import { Client, ClientDetail, ClientsService, Subscription } from './clients.service';
import { ReportsService, saveFile } from '../reports/reports.service';
@Component({
  selector: 'app-clients', templateUrl: './clients.component.html', standalone: false })
export class ClientsComponent implements OnInit {
  readonly subscriptionsWorkspace: boolean;
  readonly page = signal<PagedResult<Client>>(emptyPage());
  readonly search = signal('');
  readonly appliedSearch = signal('');
  readonly active = signal('');
  readonly subscriptionStatus = signal('');
  /** The computed client status (§39); the coach dashboard links here with ?status=. */
  readonly clientStatus = signal('');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly editorOpen = signal(false);
  readonly detailOpen = signal(false);
  readonly subscriptionOpen = signal(false);
  readonly renewalOpen = signal(false);
  readonly renewingSubscription = signal<Subscription | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly detail = signal<ClientDetail | null>(null);
  readonly packages = signal<ReferenceRecord[]>([]);
  readonly currencies = signal<ReferenceRecord[]>([]);
  readonly accounts = signal<ReferenceRecord[]>([]);
  readonly coaches = signal<Coach[]>([]);
  readonly transferCoachId = signal('');
  readonly isAdmin: boolean;
  readonly editor = signal(this.emptyClient());
  readonly subscription = signal(this.emptySubscription());
  readonly renewal = signal(this.emptyRenewal());
  readonly editingSubscriptionId = signal<string | null>(null);
  constructor(
    route: ActivatedRoute,
    private readonly data: ClientsService,
    private readonly refs: ReferenceDataService,
    private readonly coachDirectory: CoachesService,
    auth: AuthService,
    private readonly reports: ReportsService,
  ) {
    this.isAdmin = auth.isAdmin();
    this.subscriptionsWorkspace = route.snapshot.data['workspace'] === 'subscriptions';
    this.search.set(route.snapshot.queryParamMap.get('search') ?? '');
    this.appliedSearch.set(this.search());
    this.clientStatus.set(route.snapshot.queryParamMap.get('status') ?? '');
  }
  ngOnInit(): void {
    forkJoin({
      packages: this.refs.list('packages', { pageSize: 100, isActive: true }),
      currencies: this.refs.list('currencies', { pageSize: 100, isActive: true }),
      accounts: this.refs.list('payment-accounts', { pageSize: 100, isActive: true }),
    }).subscribe((x) => {
      this.packages.set(x.packages.items);
      this.currencies.set(x.currencies.items);
      this.accounts.set(x.accounts.items);
    });
    this.data.durationPresets().subscribe({ next: (x) => (this.durationPresets.set(x.durationPresets)), error: () => undefined });
    if (this.isAdmin) {
      this.coachDirectory.list({ pageSize: 100, isActive: true }).subscribe((p) => (this.coaches.set(p.items)));
    }
    this.load();
  }
  coachName(id: string | undefined): string {
    return this.coaches().find((coach) => coach.id === id)?.displayName ?? '—';
  }
  transfer(): void {
    const detail = this.detail();
    if (!detail || !this.transferCoachId()) return;
    const id = detail.client.id;
    this.saving.set(true);
    this.data
      .transfer(id, this.transferCoachId())
      .pipe(finalize(() => (this.saving.set(false))))
      .subscribe({
        next: () => {
          this.transferCoachId.set('');
          this.data.get(id).subscribe((d) => (this.detail.set(d)));
          this.load();
        },
        error: (e) => (this.error.set(apiErrorMessage(e))),
      });
  }
  applySearch(): void {
    this.appliedSearch.set(this.search().trim());
    this.load(1);
  }
  reset(): void {
    this.search.set('');
    this.appliedSearch.set('');
    this.active.set('');
    this.subscriptionStatus.set('');
    this.clientStatus.set('');
    this.load(1);
  }
  load(pageNumber = this.page().pageNumber): void {
    this.loading.set(true);
    this.data
      .list({
        pageNumber,
        pageSize: 10,
        searchTerm: this.appliedSearch(),
        isActive: this.active(),
        subscriptionStatus: this.subscriptionStatus(),
        status: this.clientStatus(),
      })
      .pipe(finalize(() => (this.loading.set(false))))
      .subscribe({ next: (p) => (this.page.set(p)), error: (e) => (this.error.set(apiErrorMessage(e))) });
  }
  create(): void {
    this.editingId.set(null);
    this.editor.set(this.emptyClient());
    this.editorOpen.set(true);
  }
  edit(c: Client): void {
    this.editingId.set(c.id);
    this.editor.set({
      name: c.name,
      phone: c.phone ?? '',
      email: c.email ?? '',
      joinDate: c.joinDate,
      isActive: c.isActive,
      coachId: c.coachId,
      invite: false,
    });
    this.editorOpen.set(true);
  }
  save(): void {
    const editingId = this.editingId();
    const create = {
      name: this.editor().name,
      phone: this.editor().phone || null,
      email: this.editor().email || null,
      joinDate: this.editor().joinDate || null,
      coachId: this.isAdmin ? this.editor().coachId || null : null,
      invite: this.editor().invite && !!this.editor().email,
    };
    const update = {
      name: this.editor().name,
      phone: this.editor().phone || null,
      email: this.editor().email || null,
      isActive: this.editor().isActive,
    };
    this.saving.set(true);
    const req = editingId
      ? this.data.update(editingId, update)
      : this.data.create(create);
    req.pipe(finalize(() => (this.saving.set(false)))).subscribe({
      next: () => {
        this.editorOpen.set(false);
        this.load();
      },
      error: (e) => (this.error.set(apiErrorMessage(e))),
    });
  }
  openDetail(c: Client): void {
    this.data.get(c.id).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.detailOpen.set(true);
      },
      error: (e) => (this.error.set(apiErrorMessage(e))),
    });
  }
  addSubscription(): void {
    this.editingSubscriptionId.set(null);
    this.subscription.set(this.emptySubscription());
    this.subscriptionOpen.set(true);
  }
  editSubscription(s: Subscription): void {
    this.editingSubscriptionId.set(s.id);
    this.subscription.set({
      packageId: s.packageId,
      startDate: s.startDate,
      durationMonths: s.durationMonths,
      price: s.price,
      currencyId: s.currencyId,
      paymentAccountId: s.paymentAccountId ?? '',
      renewalCount: s.renewalCount,
    });
    this.subscriptionOpen.set(true);
  }
  saveSubscription(): void {
    const editingSubscriptionId = this.editingSubscriptionId();
    const detail = this.detail();
    if (!detail) return;
    const input = {
      ...this.subscription(),
      paymentAccountId: this.subscription().paymentAccountId || null,
    };
    const req = editingSubscriptionId
      ? this.data.updateSubscription(detail.client.id, editingSubscriptionId, input)
      : this.data.createSubscription(detail.client.id, input);
    req.subscribe({
      next: () => {
        this.subscriptionOpen.set(false);
        this.refreshDetail();
      },
      error: (e) => (this.error.set(apiErrorMessage(e))),
    });
  }
  openRenewal(s: Subscription): void {
    this.renewingSubscription.set(s);
    this.renewal.set({
      ...this.emptyRenewal(),
      currencyId: s.currencyId,
      paymentAccountId: s.paymentAccountId ?? '',
    });
    this.renewalOpen.set(true);
  }
  saveRenewal(): void {
    const detail = this.detail();
    const renewing = this.renewingSubscription();
    if (!detail || !renewing) return;
    const input = {
      ...this.renewal(),
      paymentAccountId: this.renewal().paymentAccountId || null,
    };
    this.saving.set(true);
    this.data
      .renewSubscription(detail.client.id, renewing.id, input)
      .pipe(finalize(() => (this.saving.set(false))))
      .subscribe({
        next: () => {
          this.renewalOpen.set(false);
          this.renewingSubscription.set(null);
          this.refreshDetail();
        },
        error: (e) => (this.error.set(apiErrorMessage(e))),
      });
  }
  suspendSubscription(s: Subscription): void {
    const detail = this.detail();
    if (!detail) return;
    const reason = prompt('Why is the subscription suspended? The client sees the access rules for suspended subscriptions.');
    if (!reason?.trim()) return;
    this.data.suspendSubscription(detail.client.id, s.id, reason.trim())
      .subscribe({ next: () => this.refreshDetail(), error: (e) => (this.error.set(apiErrorMessage(e))) });
  }
  resumeSubscription(s: Subscription): void {
    const detail = this.detail();
    if (!detail) return;
    this.data.resumeSubscription(detail.client.id, s.id)
      .subscribe({ next: () => this.refreshDetail(), error: (e) => (this.error.set(apiErrorMessage(e))) });
  }
  readonly paymentLabels: Record<string, string> = {
    NoInvoice: 'No invoice', Unpaid: 'Unpaid', PartiallyPaid: 'Partially paid', Paid: 'Paid', Refunded: 'Refunded',
  };
  readonly durationPresets = signal<number[]>([1, 3, 6, 12]);
  /** The client list as CSV (§51); administrators get every coach's clients. */
  exportList(): void {
    this.reports.clientList().subscribe({ next: (file) => saveFile(file), error: (e) => (this.error.set(apiErrorMessage(e))) });
  }
  removeSubscription(s: Subscription): void {
    const detail = this.detail();
    if (detail && confirm('Delete this subscription?'))
      this.data
        .deleteSubscription(detail.client.id, s.id)
        .subscribe(() => this.refreshDetail());
  }
  remove(c: Client): void {
    if (confirm(`Delete ${c.name}?`))
      this.data
        .delete(c.id)
        .subscribe({ next: () => this.load(), error: (e) => (this.error.set(apiErrorMessage(e))) });
  }
  nameOf(items: ReferenceRecord[], id?: string | null): string {
    return items.find((x) => x.id === id)?.nameEn ?? items.find((x) => x.id === id)?.name ?? '—';
  }
  private refreshDetail(): void {
    const detail = this.detail();
    if (detail)
      this.data.get(detail.client.id).subscribe((d) => {
        this.detail.set(d);
        this.load();
      });
  }
  private emptyClient() {
    return {
      name: '',
      phone: '',
      email: '',
      joinDate: new Date().toISOString().slice(0, 10),
      isActive: true,
      coachId: '',
      invite: true,
    };
  }
  private emptyRenewal() {
    return {
      durationMonths: 1,
      price: 0,
      currencyId: '',
      paymentAccountId: '',
    };
  }
  private emptySubscription() {
    return {
      packageId: '',
      startDate: new Date().toISOString().slice(0, 10),
      durationMonths: 1,
      price: 0,
      currencyId: '',
      paymentAccountId: '',
      renewalCount: 0,
    };
  }
}
