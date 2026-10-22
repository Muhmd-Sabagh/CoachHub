import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { queryParams } from '../../shared/services/query-params';
import { DietTargets } from './nutrition-math';

export interface SavedPlan {
  id: string;
  planType: 'Diet' | 'Workout';
  nameEn: string;
  nameAr?: string | null;
  clientId?: string | null;
  clientName?: string | null;
  createdAt: string;
  totalWeight?: number | null;
  totalCalories?: number | null;
  totalProtein?: number | null;
  totalCarbohydrates?: number | null;
  totalFat?: number | null;
  workoutDayCount?: number | null;
  versionNumber?: number | null;
  versionStatus?: VersionStatus | null;
  startDate?: string | null;
  endDate?: string | null;
}

export type PlanType = 'Diet' | 'Workout';
/** D4: each plan has one current version (Draft or Active); earlier ones are Archived. */
export type VersionStatus = 'Draft' | 'Active' | 'Archived';

export interface PlanVersion {
  id: string;
  versionNumber: number;
  status: VersionStatus;
  startDate?: string | null;
  endDate?: string | null;
  createdAt: string;
  createdByUserId?: string | null;
  createdByName?: string | null;
  updatedAt: string;
  archivedAt?: string | null;
  isCurrent: boolean;
  isReadOnly: boolean;
}

export type ActivityLevel =
  'Sedentary' | 'LightlyActive' | 'ModeratelyActive' | 'VeryActive' | 'ExtraActive';
export type MacroMode = 'GramsPerKg' | 'Percentages' | 'FixedGrams';

export interface MacroPreferences {
  mode: MacroMode;
  proteinGramsPerKg?: number | null;
  fatGramsPerKg?: number | null;
  proteinPercent?: number | null;
  carbohydratePercent?: number | null;
  fatPercent?: number | null;
  proteinGrams?: number | null;
  fatGrams?: number | null;
}

/** Calorie and macro targets with the coefficients used (§14, §15). */
export interface EnergyTargets {
  weightKg: number;
  age: number;
  activityLevel: ActivityLevel;
  activityFactor: number;
  goal: 'LoseWeight' | 'Maintain' | 'GainWeight';
  adjustmentPercent: number;
  basalMetabolicRate: number;
  maintenanceCalories: number;
  goalCalories: number;
  macros: MacroPreferences;
  protein: number;
  carbohydrates: number;
  fat: number;
}

/** The editor's working copy: plan fields plus the content of one version. */
export interface PlanEditorInput {
  nameEn: string;
  nameAr?: string | null;
  clientId?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  status: VersionStatus;
  baseVersionNumber?: number | null;
  targets?: DietTargets | null;
  notes: any[];
  variants?: any[];
  days?: any[];
  [key: string]: unknown;
}

/** Turns a plan response (any version) into editor input based on that version. */
export function toEditorInput(plan: any): PlanEditorInput {
  return {
    ...plan,
    startDate: plan.revision?.startDate ?? null,
    endDate: plan.revision?.endDate ?? null,
    status: plan.revision?.status === 'Draft' ? 'Draft' : 'Active',
    targets: plan.targets ? { ...plan.targets } : undefined,
    baseVersionNumber: plan.currentVersionNumber ?? plan.revision?.versionNumber ?? null,
  };
}
@Injectable({ providedIn: 'root' })
export class PlansService {
  constructor(private readonly http: HttpClient) {}
  list(query: Record<string, unknown>) {
    return this.http.get<PagedResult<SavedPlan>>(`${environment.apiBaseUrl}/saved-plans`, {
      params: queryParams(query),
    });
  }
  get(type: PlanType, id: string) {
    return this.http.get<any>(planUrl(type, id));
  }
  create(type: PlanType, input: object) {
    return this.http.post<any>(planUrl(type), input);
  }
  /** D4 "Save": updates the current version in place. */
  saveInPlace(type: PlanType, id: string, input: object) {
    return this.http.put<any>(`${planUrl(type, id)}/revisions/current`, input);
  }
  /** D4 "Save as new version": the current version is archived and kept read-only. */
  saveAsNewVersion(type: PlanType, id: string, input: object) {
    return this.http.post<any>(`${planUrl(type, id)}/revisions`, input);
  }
  versions(type: PlanType, id: string) {
    return this.http.get<PlanVersion[]>(`${planUrl(type, id)}/revisions`);
  }
  version(type: PlanType, id: string, versionNumber: number) {
    return this.http.get<any>(`${planUrl(type, id)}/revisions/${versionNumber}`);
  }
  copy(type: 'Diet' | 'Workout', id: string, input: object) {
    return this.http.post<any>(
      `${environment.apiBaseUrl}/${type === 'Diet' ? 'diet' : 'workout'}-plans/${id}/copies`,
      input,
    );
  }
  assign(type: 'Diet' | 'Workout', id: string, clientId: string | null) {
    return this.http.put<any>(
      `${environment.apiBaseUrl}/${type === 'Diet' ? 'diet' : 'workout'}-plans/${id}/assignment`,
      { clientId },
    );
  }
  delete(type: 'Diet' | 'Workout', id: string) {
    return this.http.delete<void>(
      `${environment.apiBaseUrl}/${type === 'Diet' ? 'diet' : 'workout'}-plans/${id}`,
    );
  }
  calculate(input: object) {
    return this.http.post<EnergyTargets>(
      `${environment.apiBaseUrl}/nutrition-calculator/energy`,
      input,
    );
  }
  /** The client's calorie and macro targets from their profile and the coach's settings (§14, §15). */
  clientTargets(clientId: string) {
    return this.http.get<EnergyTargets>(
      `${environment.apiBaseUrl}/clients/${clientId}/nutrition-targets`,
    );
  }
  pdf(
    type: PlanType,
    id: string,
    mode: 'preview' | 'download',
    language: string,
    version?: number,
  ) {
    const params: Record<string, string> = { language };
    if (version) params['version'] = String(version);
    return this.http.get(`${planUrl(type, id)}/pdf/${mode}`, { params, responseType: 'blob' });
  }
}
function planUrl(type: PlanType, id?: string) {
  const base = `${environment.apiBaseUrl}/${type === 'Diet' ? 'diet' : 'workout'}-plans`;
  return id ? `${base}/${id}` : base;
}

@Injectable({ providedIn: 'root' })
export class CalculatorStore {
  readonly open = signal(false);
  readonly result = signal<any | null>(null);
  show() {
    this.open.set(true);
  }
  close() {
    this.open.set(false);
  }
}
