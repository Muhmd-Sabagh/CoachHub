import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { queryParams } from '../../shared/services/query-params';
import { CatalogItem } from './catalog.models';
export type CatalogKind = 'foods' | 'exercises';
@Injectable({ providedIn: 'root' })
export class CatalogService {
  constructor(private readonly http: HttpClient) {}
  private url(kind: CatalogKind): string {
    return kind === 'foods'
      ? `${environment.apiBaseUrl}/nutrition/foods`
      : `${environment.apiBaseUrl}/training/exercises`;
  }
  list(kind: CatalogKind, query: Record<string, unknown>) {
    return this.http.get<PagedResult<CatalogItem>>(this.url(kind), { params: queryParams(query) });
  }
  create(kind: CatalogKind, input: object) {
    return this.http.post<CatalogItem>(this.url(kind), input);
  }
  update(kind: CatalogKind, id: string, input: object) {
    return this.http.put<CatalogItem>(`${this.url(kind)}/${id}`, input);
  }
  delete(kind: CatalogKind, id: string) {
    return this.http.delete<void>(`${this.url(kind)}/${id}`);
  }
}
