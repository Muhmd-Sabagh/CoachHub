import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { queryParams } from '../../shared/services/query-params';

export interface Coach {
  id: string;
  displayName: string;
  isActive: boolean;
  createdAt: string;
  userId?: string | null;
  email?: string | null;
  userName?: string | null;
  clientCount: number;
}

export interface CoachCreateInput { displayName: string; email: string; userName: string | null; password: string | null; }

/** Administrator management of coaches, the platform's tenants. */
@Injectable({ providedIn: 'root' })
export class CoachesService {
  private readonly url = `${environment.apiBaseUrl}/admin/coaches`;
  constructor(private readonly http: HttpClient) {}
  list(query: { pageNumber?: number; pageSize?: number; searchTerm?: string; isActive?: boolean | '' }) {
    return this.http.get<PagedResult<Coach>>(this.url, { params: queryParams(query) });
  }
  create(input: CoachCreateInput) { return this.http.post<Coach>(this.url, input); }
  update(id: string, input: { displayName: string; isActive: boolean }) { return this.http.put<Coach>(`${this.url}/${id}`, input); }
}
