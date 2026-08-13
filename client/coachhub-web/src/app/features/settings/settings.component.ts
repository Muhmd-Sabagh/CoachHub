import { Component, OnInit } from '@angular/core';
import { finalize } from 'rxjs';
import { AppConfigService } from '../../core/config/app-config.service';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import { ReferenceDataService, ReferenceKey, ReferenceRecord } from './reference-data.service';

interface Resource {
  key: ReferenceKey;
  label: string;
  kind: 'bilingual' | 'package' | 'currency' | 'payment';
}
@Component({
  selector: 'app-settings',
  templateUrl: './settings.component.html',
  standalone: false,
})
export class SettingsComponent implements OnInit {
  readonly resources: Resource[] = [
    { key: 'packages', label: 'Packages', kind: 'package' },
    { key: 'currencies', label: 'Currencies', kind: 'currency' },
    { key: 'payment-accounts', label: 'Payment accounts', kind: 'payment' },
    { key: 'food-categories', label: 'Food categories', kind: 'bilingual' },
    { key: 'exercise-categories', label: 'Exercise categories', kind: 'bilingual' },
  ];
  resource = this.resources[0];
  page: PagedResult<ReferenceRecord> = emptyPage();
  search = '';
  appliedSearch = '';
  active = '';
  loading = false;
  saving = false;
  error = '';
  editorOpen = false;
  editingId: string | null = null;
  editor = this.emptyEditor();
  constructor(
    readonly config: AppConfigService,
    private readonly data: ReferenceDataService,
  ) {}
  ngOnInit(): void {
    this.load();
  }
  select(resource: Resource): void {
    this.resource = resource;
    this.search = '';
    this.appliedSearch = '';
    this.active = '';
    this.closeEditor();
    this.load(1);
  }
  applySearch(): void {
    this.appliedSearch = this.search.trim();
    this.load(1);
  }
  clear(): void {
    this.search = '';
    this.appliedSearch = '';
    this.active = '';
    this.load(1);
  }
  load(pageNumber = this.page.pageNumber): void {
    this.loading = true;
    this.error = '';
    this.data
      .list(this.resource.key, {
        pageNumber,
        pageSize: 10,
        searchTerm: this.appliedSearch,
        isActive: this.active,
      })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (page) => (this.page = page),
        error: (e) => (this.error = apiErrorMessage(e)),
      });
  }
  create(): void {
    this.editingId = null;
    this.editor = this.emptyEditor();
    this.editorOpen = true;
  }
  edit(item: ReferenceRecord): void {
    this.editingId = item.id;
    this.editor = {
      nameEn: item.nameEn ?? '',
      nameAr: item.nameAr ?? '',
      name: item.name ?? '',
      code: item.code ?? '',
      symbol: item.symbol ?? '',
      description: item.description ?? '',
      details: item.details ?? '',
      isActive: item.isActive,
    };
    this.editorOpen = true;
  }
  closeEditor(): void {
    this.editorOpen = false;
    this.editingId = null;
  }
  save(): void {
    const input = this.payload();
    this.saving = true;
    this.error = '';
    const request = this.editingId
      ? this.data.update(this.resource.key, this.editingId, input)
      : this.data.create(this.resource.key, input);
    request.pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => {
        this.closeEditor();
        this.load();
      },
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  remove(item: ReferenceRecord): void {
    if (!confirm(`Delete ${this.displayName(item)}?`)) return;
    this.data
      .delete(this.resource.key, item.id)
      .subscribe({ next: () => this.load(), error: (e) => (this.error = apiErrorMessage(e)) });
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
    switch (this.resource.kind) {
      case 'currency':
        return {
          code: this.editor.code,
          name: this.editor.name,
          symbol: this.editor.symbol || null,
          isActive: this.editor.isActive,
        };
      case 'payment':
        return {
          name: this.editor.name,
          details: this.editor.details || null,
          isActive: this.editor.isActive,
        };
      case 'package':
        return {
          nameEn: this.editor.nameEn,
          nameAr: this.editor.nameAr || null,
          description: this.editor.description || null,
          isActive: this.editor.isActive,
        };
      default:
        return {
          nameEn: this.editor.nameEn,
          nameAr: this.editor.nameAr || null,
          isActive: this.editor.isActive,
        };
    }
  }
}
