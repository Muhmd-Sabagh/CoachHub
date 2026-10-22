import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import { AuditRecord, AuditService } from './audit.service';

@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-audit',
  templateUrl: './audit.component.html',
  standalone: true,
  imports: [CommonModule, FormsModule],
})
export class AuditComponent implements OnInit {
  page: PagedResult<AuditRecord> = emptyPage();
  search = '';
  entityType = '';
  clientId = '';
  operation = '';
  expanded: string | null = null;
  actorKind = '';
  occurredFrom = '';
  occurredTo = '';
  loading = false;
  error = '';

  private applied = this.filters();

  constructor(private readonly data: AuditService) {}

  ngOnInit(): void {
    this.load(1);
  }

  applySearch(): void {
    this.applied = this.filters();
    this.load(1);
  }

  reset(): void {
    this.search = '';
    this.entityType = '';
    this.clientId = '';
    this.operation = '';
    this.actorKind = '';
    this.occurredFrom = '';
    this.occurredTo = '';
    this.applySearch();
  }

  load(pageNumber = this.page.pageNumber): void {
    this.loading = true;
    this.error = '';
    this.data.list({ pageNumber, pageSize: 20, ...this.applied })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (page) => (this.page = page),
        error: (error) => (this.error = apiErrorMessage(error)),
      });
  }

  previous(): void {
    if (this.page.pageNumber > 1) this.load(this.page.pageNumber - 1);
  }

  next(): void {
    if (this.page.pageNumber < this.page.totalPages) this.load(this.page.pageNumber + 1);
  }

  actor(record: AuditRecord): string {
    return record.actorDisplayName ||
      (record.actorKind === 'PublicClient' ? 'Public client' : record.actorKind);
  }

  toggle(record: AuditRecord): void {
    this.expanded = this.expanded === record.id ? null : record.id;
  }

  /** Shows only this client's history (§58: the affected client). */
  showClient(clientId: string): void {
    this.clientId = clientId;
    this.applySearch();
  }

  value(value: unknown): string { return formatAuditValue(value); }

  private filters() {
    return {
      searchTerm: this.search.trim() || undefined,
      entityType: this.entityType || undefined,
      clientId: this.clientId.trim() || undefined,
      operation: this.operation || undefined,
      actorKind: this.actorKind || undefined,
      occurredFrom: this.occurredFrom || undefined,
      occurredTo: this.occurredTo || undefined,
    };
  }
}

/** A readable audit value: "—" for none, text as is, other JSON compactly. */
export function formatAuditValue(value: unknown): string {
  if (value === undefined || value === null) return '—';
  if (typeof value === 'string') return value === '' ? '""' : value;
  return JSON.stringify(value);
}
