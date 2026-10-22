import { Component, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { AppConfigService } from '../../core/config/app-config.service';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import { ReferenceDataService, ReferenceKey, ReferenceRecord } from './reference-data.service';

interface Resource {
  key: ReferenceKey;
  label: string;
  kind: 'bilingual' | 'package' | 'currency' | 'payment';
  /** Coach-owned data belongs to the signed-in coach; platform data is Admin-managed (D7). */
  owner: 'coach' | 'platform';
}
@Component({
  selector: 'app-reference-data',
  templateUrl: './reference-data.component.html',
  standalone: false,
})
export class ReferenceDataComponent implements OnInit {
  readonly resources: Resource[] = [
    { key: 'packages', label: 'Packages', kind: 'package', owner: 'coach' },
    { key: 'currencies', label: 'Currencies', kind: 'currency', owner: 'platform' },
    { key: 'payment-accounts', label: 'Payment accounts', kind: 'payment', owner: 'coach' },
    { key: 'food-categories', label: 'Food categories', kind: 'bilingual', owner: 'platform' },
    { key: 'exercise-categories', label: 'Exercise categories', kind: 'bilingual', owner: 'platform' },
    { key: 'equipment', label: 'Equipment', kind: 'bilingual', owner: 'platform' },
    { key: 'muscle-groups', label: 'Muscle groups', kind: 'bilingual', owner: 'platform' },
  ];
  readonly resource = signal(this.resources[0]);
  readonly page = signal<PagedResult<ReferenceRecord>>(emptyPage());
  readonly search = signal('');
  readonly appliedSearch = signal('');
  readonly active = signal('');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly editorOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly editor = signal(this.emptyEditor());
  constructor(
    readonly config: AppConfigService,
    private readonly data: ReferenceDataService,
    private readonly auth: AuthService,
  ) {}
  /** Coaches create their own packages and accounts; administrators create platform data. */
  get canCreate(): boolean {
    return this.resource().owner === 'platform' ? this.auth.isAdmin() : this.auth.isCoach();
  }
  get canModify(): boolean {
    return this.resource().owner === 'platform' ? this.auth.isAdmin() : this.auth.isCoach() || this.auth.isAdmin();
  }
  ngOnInit(): void {
    this.load();
  }
  select(resource: Resource): void {
    this.resource.set(resource);
    this.search.set('');
    this.appliedSearch.set('');
    this.active.set('');
    this.closeEditor();
    this.load(1);
  }
  applySearch(): void {
    this.appliedSearch.set(this.search().trim());
    this.load(1);
  }
  clear(): void {
    this.search.set('');
    this.appliedSearch.set('');
    this.active.set('');
    this.load(1);
  }
  load(pageNumber = this.page().pageNumber): void {
    this.loading.set(true);
    this.error.set('');
    this.data
      .list(this.resource().key, {
        pageNumber,
        pageSize: 10,
        searchTerm: this.appliedSearch(),
        isActive: this.active(),
      })
      .pipe(finalize(() => (this.loading.set(false))))
      .subscribe({
        next: (page) => (this.page.set(page)),
        error: (e) => (this.error.set(apiErrorMessage(e))),
      });
  }
  create(): void {
    this.editingId.set(null);
    this.editor.set(this.emptyEditor());
    this.editorOpen.set(true);
  }
  edit(item: ReferenceRecord): void {
    this.editingId.set(item.id);
    this.editor.set({
      nameEn: item.nameEn ?? '',
      nameAr: item.nameAr ?? '',
      name: item.name ?? '',
      code: item.code ?? '',
      symbol: item.symbol ?? '',
      description: item.description ?? '',
      details: item.details ?? '',
      isActive: item.isActive,
    });
    this.editorOpen.set(true);
  }
  closeEditor(): void {
    this.editorOpen.set(false);
    this.editingId.set(null);
  }
  save(): void {
    const input = this.payload();
    this.saving.set(true);
    this.error.set('');
    const editingId = this.editingId();
    const request = editingId
      ? this.data.update(this.resource().key, editingId, input)
      : this.data.create(this.resource().key, input);
    request.pipe(finalize(() => (this.saving.set(false)))).subscribe({
      next: () => {
        this.closeEditor();
        this.load();
      },
      error: (e) => (this.error.set(apiErrorMessage(e))),
    });
  }
  remove(item: ReferenceRecord): void {
    if (!confirm(`Delete ${this.displayName(item)}?`)) return;
    this.data
      .delete(this.resource().key, item.id)
      .subscribe({ next: () => this.load(), error: (e) => (this.error.set(apiErrorMessage(e))) });
  }
  displayName(item: ReferenceRecord): string {
    return item.nameEn ?? item.name ?? item.code ?? '';
  }
  private emptyEditor() {
    return {
      nameEn: '',
      nameAr: '',
      name: '',
      code: '',
      symbol: '',
      description: '',
      details: '',
      isActive: true,
    };
  }
  private payload(): object {
    switch (this.resource().kind) {
      case 'currency':
        return {
          code: this.editor().code,
          name: this.editor().name,
          symbol: this.editor().symbol || null,
          isActive: this.editor().isActive,
        };
      case 'payment':
        return {
          name: this.editor().name,
          details: this.editor().details || null,
          isActive: this.editor().isActive,
        };
      case 'package':
        return {
          nameEn: this.editor().nameEn,
          nameAr: this.editor().nameAr || null,
          description: this.editor().description || null,
          isActive: this.editor().isActive,
        };
      default:
        return {
          nameEn: this.editor().nameEn,
          nameAr: this.editor().nameAr || null,
          isActive: this.editor().isActive,
        };
    }
  }
}
