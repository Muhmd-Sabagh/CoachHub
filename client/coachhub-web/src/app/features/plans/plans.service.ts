import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { queryParams } from '../../shared/services/query-params';

export interface SavedPlan { id:string; planType:'Diet'|'Workout'; nameEn:string; nameAr?:string|null; clientId?:string|null; clientName?:string|null; clientCode?:string|null; createdAt:string; totalWeight?:number|null; totalCalories?:number|null; totalProtein?:number|null; totalCarbohydrates?:number|null; totalFat?:number|null; workoutDayCount?:number|null; }
@Injectable({ providedIn:'root' })
export class PlansService {
  constructor(private readonly http: HttpClient) {}
  list(query: Record<string, unknown>) { return this.http.get<PagedResult<SavedPlan>>(`${environment.apiBaseUrl}/saved-plans`, { params: queryParams(query) }); }
  get(type:'Diet'|'Workout', id:string) { return this.http.get<any>(`${environment.apiBaseUrl}/${type === 'Diet' ? 'diet' : 'workout'}-plans/${id}`); }
  save(type:'Diet'|'Workout', id:string|null, input:object) { const url = `${environment.apiBaseUrl}/${type === 'Diet' ? 'diet' : 'workout'}-plans`; return id ? this.http.put<any>(`${url}/${id}`, input) : this.http.post<any>(url, input); }
  copy(type:'Diet'|'Workout', id:string, input:object) { return this.http.post<any>(`${environment.apiBaseUrl}/${type === 'Diet' ? 'diet' : 'workout'}-plans/${id}/copies`, input); }
  assign(type:'Diet'|'Workout', id:string, clientId:string|null) { return this.http.put<any>(`${environment.apiBaseUrl}/${type === 'Diet' ? 'diet' : 'workout'}-plans/${id}/assignment`, { clientId }); }
  delete(type:'Diet'|'Workout', id:string) { return this.http.delete<void>(`${environment.apiBaseUrl}/${type === 'Diet' ? 'diet' : 'workout'}-plans/${id}`); }
  calculate(input:object) { return this.http.post<any>(`${environment.apiBaseUrl}/nutrition-calculator/energy`, input); }
  pdf(type:'Diet'|'Workout', id:string, mode:'preview'|'download', language:string) { return this.http.get(`${environment.apiBaseUrl}/${type === 'Diet' ? 'diet' : 'workout'}-plans/${id}/pdf/${mode}`, { params:{ language }, responseType:'blob' }); }
}
@Injectable({ providedIn:'root' })
export class CalculatorStore { readonly open = signal(false); readonly result = signal<any|null>(null); show(){ this.open.set(true); } close(){ this.open.set(false); } }