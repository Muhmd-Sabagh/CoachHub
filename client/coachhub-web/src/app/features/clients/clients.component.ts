import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import { ReferenceDataService, ReferenceRecord } from '../settings/reference-data.service';
import { Client, ClientDetail, ClientsService, Subscription } from './clients.service';
@Component({ selector: 'app-clients', templateUrl: './clients.component.html', standalone: false })
export class ClientsComponent implements OnInit {
  readonly subscriptionsWorkspace: boolean;
  page: PagedResult<Client> = emptyPage();
  search = '';
  appliedSearch = '';
  active = '';
  subscriptionStatus = '';
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
  editor = this.emptyClient();
  subscription = this.emptySubscription();
  renewal = this.emptyRenewal();
  editingSubscriptionId: string | null = null;
  constructor(
    route: ActivatedRoute,
    private readonly data: ClientsService,
    private readonly refs: ReferenceDataService,
  ) {
    this.subscriptionsWorkspace = route.snapshot.data['workspace'] === 'subscriptions';
    this.search = route.snapshot.queryParamMap.get('search') ?? '';
    this.appliedSearch = this.search;
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
    this.load();
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
      dietStatus: c.dietStatus,
      workoutStatus: c.workoutStatus,
      isActive: c.isActive,
    };
    this.editorOpen = true;
  }
  save(): void {
    const create = {
      name: this.editor.name,
      phone: this.editor.phone || null,
      email: this.editor.email || null,
      joinDate: this.editor.joinDate || null,
    };
    const update = {
      name: this.editor.name,
      phone: this.editor.phone || null,
      email: this.editor.email || null,
      dietStatus: this.editor.dietStatus,
      workoutStatus: this.editor.workoutStatus,
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
  regenerate(): void {
    if (this.detail && confirm('Regenerate this client form code?'))
      this.data.regenerate(this.detail.client.id).subscribe((x) => {
        if (this.detail) this.detail.client.formCode = x.formCode;
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
      dietStatus: 'NotStarted',
      workoutStatus: 'NotStarted',
      isActive: true,
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
