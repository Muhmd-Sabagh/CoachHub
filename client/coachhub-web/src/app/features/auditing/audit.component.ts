import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
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
  page: PagedResult<AuditRecord> = emptyPage();
  search = '';
  entityType = '';
  operation = '';
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

  private filters() {
    return {
      searchTerm: this.search.trim() || undefined,
      entityType: this.entityType || undefined,
      operation: this.operation || undefined,
      actorKind: this.actorKind || undefined,
      occurredFrom: this.occurredFrom || undefined,
      occurredTo: this.occurredTo || undefined,
    };
  }
}
