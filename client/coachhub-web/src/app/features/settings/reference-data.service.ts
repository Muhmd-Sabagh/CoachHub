import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { queryParams } from '../../shared/services/query-params';

export type ReferenceKey = 'packages' | 'currencies' | 'payment-accounts' | 'food-categories' | 'exercise-categories';
export interface ReferenceRecord { id: string; nameEn?: string; nameAr?: string | null; name?: string; code?: string; symbol?: string | null; description?: string | null; details?: string | null; isActive: boolean; }

@Injectable({ providedIn: 'root' })
export class ReferenceDataService {
  private readonly base = `${environment.apiBaseUrl}/reference-data`;
  constructor(private readonly http: HttpClient) {}
  list(key: ReferenceKey, query: Record<string, unknown>): Observable<PagedResult<ReferenceRecord>> { return this.http.get<PagedResult<ReferenceRecord>>(`${this.base}/${key}`, { params: queryParams(query) }); }
  create(key: ReferenceKey, input: object): Observable<ReferenceRecord> { return this.http.post<ReferenceRecord>(`${this.base}/${key}`, input); }
  update(key: ReferenceKey, id: string, input: object): Observable<ReferenceRecord> { return this.http.put<ReferenceRecord>(`${this.base}/${key}/${id}`, input); }
  delete(key: ReferenceKey, id: string): Observable<void> { return this.http.delete<void>(`${this.base}/${key}/${id}`); }
}