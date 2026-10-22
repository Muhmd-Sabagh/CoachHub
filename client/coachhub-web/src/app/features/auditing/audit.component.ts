import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import { AuditRecord, AuditService } from './audit.service';

@Component({
  selector: 'app-audit',
  templateUrl: './audit.component.html',
  standalone: true,
  imports: [CommonModule, FormsModule],
})
export class AuditComponent implements OnInit {
  readonly page = signal<PagedResult<AuditRecord>>(emptyPage());
  readonly search = signal('');
  readonly entityType = signal('');
  readonly clientId = signal('');
  readonly operation = signal('');
  readonly expanded = signal<string | null>(null);
  readonly actorKind = signal('');
  readonly occurredFrom = signal('');
  readonly occurredTo = signal('');
  readonly loading = signal(false);
  readonly error = signal('');

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
    this.search.set('');
    this.entityType.set('');
    this.clientId.set('');
    this.operation.set('');
    this.actorKind.set('');
    this.occurredFrom.set('');
    this.occurredTo.set('');
    this.applySearch();
  }

  load(pageNumber = this.page().pageNumber): void {
    this.loading.set(true);
    this.error.set('');
    this.data.list({ pageNumber, pageSize: 20, ...this.applied })
      .pipe(finalize(() => (this.loading.set(false))))
      .subscribe({
        next: (page) => (this.page.set(page)),
        error: (error) => (this.error.set(apiErrorMessage(error))),
      });
  }

  previous(): void {
    if (this.page().pageNumber > 1) this.load(this.page().pageNumber - 1);
  }

  next(): void {
    if (this.page().pageNumber < this.page().totalPages) this.load(this.page().pageNumber + 1);
  }

  actor(record: AuditRecord): string {
    return record.actorDisplayName ||
      (record.actorKind === 'PublicClient' ? 'Public client' : record.actorKind);
  }

  toggle(record: AuditRecord): void {
    this.expanded.set(this.expanded() === record.id ? null : record.id);
  }

  /** Shows only this client's history (§58: the affected client). */
  showClient(clientId: string): void {
    this.clientId.set(clientId);
    this.applySearch();
  }

  value(value: unknown): string { return formatAuditValue(value); }

  private filters() {
    return {
      searchTerm: this.search().trim() || undefined,
      entityType: this.entityType() || undefined,
      clientId: this.clientId().trim() || undefined,
      operation: this.operation() || undefined,
      actorKind: this.actorKind() || undefined,
      occurredFrom: this.occurredFrom() || undefined,
      occurredTo: this.occurredTo() || undefined,
    };
  }
}

/** A readable audit value: "—" for none, text as is, other JSON compactly. */
export function formatAuditValue(value: unknown): string {
  if (value === undefined || value === null) return '—';
  if (typeof value === 'string') return value === '' ? '""' : value;
  return JSON.stringify(value);
}
