import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
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
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager, selector: 'app-clients', templateUrl: './clients.component.html', standalone: false })
export class ClientsComponent implements OnInit {
  readonly subscriptionsWorkspace: boolean;
  page: PagedResult<Client> = emptyPage();
  search = '';
  appliedSearch = '';
  active = '';
  subscriptionStatus = '';
  /** The computed client status (§39); the coach dashboard links here with ?status=. */
  clientStatus = '';
  loading = false;
  saving = false;
  error = '';
  editorOpen = false;
  detailOpen = false;
  subscriptionOpen = false;
  renewalOpen = false;
  renewingSubscription: Subscription | null = null;
  editingId: string | null = null;
  detail: ClientDetail | null = null;
  packages: ReferenceRecord[] = [];
  currencies: ReferenceRecord[] = [];
  accounts: ReferenceRecord[] = [];
  coaches: Coach[] = [];
  transferCoachId = '';
  readonly isAdmin: boolean;
  editor = this.emptyClient();
  subscription = this.emptySubscription();
  renewal = this.emptyRenewal();
  editingSubscriptionId: string | null = null;
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
    this.search = route.snapshot.queryParamMap.get('search') ?? '';
    this.appliedSearch = this.search;
    this.clientStatus = route.snapshot.queryParamMap.get('status') ?? '';
  }
  ngOnInit(): void {
    forkJoin({
      packages: this.refs.list('packages', { pageSize: 100, isActive: true }),
      currencies: this.refs.list('currencies', { pageSize: 100, isActive: true }),
      accounts: this.refs.list('payment-accounts', { pageSize: 100, isActive: true }),
    }).subscribe((x) => {
      this.packages = x.packages.items;
      this.currencies = x.currencies.items;
      this.accounts = x.accounts.items;
    });
    this.data.durationPresets().subscribe({ next: (x) => (this.durationPresets = x.durationPresets), error: () => undefined });
    if (this.isAdmin) {
      this.coachDirectory.list({ pageSize: 100, isActive: true }).subscribe((p) => (this.coaches = p.items));
    }
    this.load();
  }
  coachName(id: string | undefined): string {
    return this.coaches.find((coach) => coach.id === id)?.displayName ?? '—';
  }
  transfer(): void {
    if (!this.detail || !this.transferCoachId) return;
    const id = this.detail.client.id;
    this.saving = true;
    this.data
      .transfer(id, this.transferCoachId)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.transferCoachId = '';
          this.data.get(id).subscribe((d) => (this.detail = d));
          this.load();
        },
        error: (e) => (this.error = apiErrorMessage(e)),
      });
  }
  applySearch(): void {
    this.appliedSearch = this.search.trim();
    this.load(1);
  }
  reset(): void {
    this.search = '';
    this.appliedSearch = '';
    this.active = '';
    this.subscriptionStatus = '';
    this.clientStatus = '';
    this.load(1);
  }
  load(pageNumber = this.page.pageNumber): void {
    this.loading = true;
    this.data
      .list({
        pageNumber,
        pageSize: 10,
        searchTerm: this.appliedSearch,
        isActive: this.active,
        subscriptionStatus: this.subscriptionStatus,
        status: this.clientStatus,
      })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({ next: (p) => (this.page = p), error: (e) => (this.error = apiErrorMessage(e)) });
  }
  create(): void {
    this.editingId = null;
    this.editor = this.emptyClient();
    this.editorOpen = true;
  }
  edit(c: Client): void {
    this.editingId = c.id;
    this.editor = {
      name: c.name,
      phone: c.phone ?? '',
      email: c.email ?? '',
      joinDate: c.joinDate,
      isActive: c.isActive,
      coachId: c.coachId,
      invite: false,
    };
    this.editorOpen = true;
  }
  save(): void {
    const create = {
      name: this.editor.name,
      phone: this.editor.phone || null,
      email: this.editor.email || null,
      joinDate: this.editor.joinDate || null,
      coachId: this.isAdmin ? this.editor.coachId || null : null,
      invite: this.editor.invite && !!this.editor.email,
    };
    const update = {
      name: this.editor.name,
      phone: this.editor.phone || null,
      email: this.editor.email || null,
      isActive: this.editor.isActive,
    };
    this.saving = true;
    const req = this.editingId
      ? this.data.update(this.editingId, update)
      : this.data.create(create);
    req.pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => {
        this.editorOpen = false;
        this.load();
      },
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  openDetail(c: Client): void {
    this.data.get(c.id).subscribe({
      next: (d) => {
        this.detail = d;
        this.detailOpen = true;
      },
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  addSubscription(): void {
    this.editingSubscriptionId = null;
    this.subscription = this.emptySubscription();
    this.subscriptionOpen = true;
  }
  editSubscription(s: Subscription): void {
    this.editingSubscriptionId = s.id;
    this.subscription = {
      packageId: s.packageId,
      startDate: s.startDate,
      durationMonths: s.durationMonths,
      price: s.price,
      currencyId: s.currencyId,
      paymentAccountId: s.paymentAccountId ?? '',
      renewalCount: s.renewalCount,
    };
    this.subscriptionOpen = true;
  }
  saveSubscription(): void {
    if (!this.detail) return;
    const input = {
      ...this.subscription,
      paymentAccountId: this.subscription.paymentAccountId || null,
    };
    const req = this.editingSubscriptionId
      ? this.data.updateSubscription(this.detail.client.id, this.editingSubscriptionId, input)
      : this.data.createSubscription(this.detail.client.id, input);
    req.subscribe({
      next: () => {
        this.subscriptionOpen = false;
        this.refreshDetail();
      },
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  openRenewal(s: Subscription): void {
    this.renewingSubscription = s;
    this.renewal = {
      ...this.emptyRenewal(),
      currencyId: s.currencyId,
      paymentAccountId: s.paymentAccountId ?? '',
    };
    this.renewalOpen = true;
  }
  saveRenewal(): void {
    if (!this.detail || !this.renewingSubscription) return;
    const input = {
      ...this.renewal,
      paymentAccountId: this.renewal.paymentAccountId || null,
    };
    this.saving = true;
    this.data
      .renewSubscription(this.detail.client.id, this.renewingSubscription.id, input)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.renewalOpen = false;
          this.renewingSubscription = null;
          this.refreshDetail();
        },
        error: (e) => (this.error = apiErrorMessage(e)),
      });
  }
  suspendSubscription(s: Subscription): void {
    if (!this.detail) return;
    const reason = prompt('Why is the subscription suspended? The client sees the access rules for suspended subscriptions.');
    if (!reason?.trim()) return;
    this.data.suspendSubscription(this.detail.client.id, s.id, reason.trim())
      .subscribe({ next: () => this.refreshDetail(), error: (e) => (this.error = apiErrorMessage(e)) });
  }
  resumeSubscription(s: Subscription): void {
    if (!this.detail) return;
    this.data.resumeSubscription(this.detail.client.id, s.id)
      .subscribe({ next: () => this.refreshDetail(), error: (e) => (this.error = apiErrorMessage(e)) });
  }
  readonly paymentLabels: Record<string, string> = {
    NoInvoice: 'No invoice', Unpaid: 'Unpaid', PartiallyPaid: 'Partially paid', Paid: 'Paid', Refunded: 'Refunded',
  };
  durationPresets: number[] = [1, 3, 6, 12];
  /** The client list as CSV (§51); administrators get every coach's clients. */
  exportList(): void {
    this.reports.clientList().subscribe({ next: (file) => saveFile(file), error: (e) => (this.error = apiErrorMessage(e)) });
  }
  removeSubscription(s: Subscription): void {
    if (this.detail && confirm('Delete this subscription?'))
      this.data
        .deleteSubscription(this.detail.client.id, s.id)
        .subscribe(() => this.refreshDetail());
  }
  remove(c: Client): void {
    if (confirm(`Delete ${c.name}?`))
      this.data
        .delete(c.id)
        .subscribe({ next: () => this.load(), error: (e) => (this.error = apiErrorMessage(e)) });
  }
  nameOf(items: ReferenceRecord[], id?: string | null): string {
    return items.find((x) => x.id === id)?.nameEn ?? items.find((x) => x.id === id)?.name ?? '—';
  }
  private refreshDetail(): void {
    if (this.detail)
      this.data.get(this.detail.client.id).subscribe((d) => {
        this.detail = d;
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
