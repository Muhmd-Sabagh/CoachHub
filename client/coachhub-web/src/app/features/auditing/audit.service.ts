import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { queryParams } from '../../shared/services/query-params';

export type AuditActorKind = 'Administrator' | 'Coach' | 'Client' | 'PublicClient' | 'System';

/** One changed property (D2): the previous value and the new one, as JSON values. */
export interface AuditChange { property: string; from?: unknown; to?: unknown; }

export interface AuditRecord {
  id: string;
  entityType: string;
  entityId?: string | null;
  operation: 'Create' | 'Update' | 'Delete';
  actorKind: AuditActorKind;
  actorUserId?: string | null;
  actorDisplayName?: string | null;
  occurredAt: string;
  coachId?: string | null;
  clientId?: string | null;
  changes: AuditChange[];
}

export interface AuditQuery {
  pageNumber: number;
  pageSize: number;
  searchTerm?: string;
  entityType?: string;
  clientId?: string;
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
