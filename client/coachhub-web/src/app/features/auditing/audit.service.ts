import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { queryParams } from '../../shared/services/query-params';

export interface AuditRecord {
  id: string;
  entityType: string;
  entityId?: string | null;
  operation: 'Create' | 'Update' | 'Delete';
  actorKind: 'Administrator' | 'PublicClient' | 'System';
  actorUserId?: string | null;
  actorDisplayName?: string | null;
  occurredAt: string;
}

export interface AuditQuery {
  pageNumber: number;
  pageSize: number;
  searchTerm?: string;
  entityType?: string;
  operation?: string;
  actorKind?: string;
  occurredFrom?: string;
  occurredTo?: string;
}

@Injectable({ providedIn: 'root' })
export class AuditService {
  constructor(private readonly http: HttpClient) {}

  list(query: AuditQuery) {
    return this.http.get<PagedResult<AuditRecord>>(`${environment.apiBaseUrl}/audit-entries`, {
      params: queryParams({ ...query }),
    });
  }
}
