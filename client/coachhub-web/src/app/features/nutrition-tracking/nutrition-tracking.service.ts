import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { MealType } from '../plans/nutrition-math';

export interface MacroAmounts {
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
  fiber: number;
}
export type FoodLogSource = 'Planned' | 'Swap' | 'Extra';

export interface PlannedFood {
  id: string;
  foodItemId: string;
  nameEn: string;
  nameAr: string | null;
  quantity: number;
  foodUnitId: string | null;
  unitLabel: string;
  unitLabelAr: string;
  totals: MacroAmounts;
  alternativeCount: number;
  notes: string | null;
}
export interface PlannedMeal {
  id: string;
  nameEn: string;
  nameAr: string | null;
  type: MealType;
  notes: string | null;
  options: { id: string; nameEn: string; nameAr: string | null; totals: MacroAmounts }[];
  chosenOptionId: string | null;
  foods: PlannedFood[];
  totals: MacroAmounts;
  isLogged: boolean;
}
export interface PlannedDay {
  planId: string;
  nameEn: string;
  nameAr: string | null;
  versionNumber: number;
  variantId: string;
  variants: { id: string; nameEn: string; nameAr: string | null }[];
  meals: PlannedMeal[];
}
export interface FoodLogEntry {
  id: string;
  date: string;
  slot: MealType;
  source: FoodLogSource;
  nameEn: string;
  nameAr: string | null;
  isCustom: boolean;
  foodItemId: string | null;
  quantity: number | null;
  foodUnitId: string | null;
  unitLabel: string | null;
  unitLabelAr: string | null;
  nutrients: MacroAmounts;
  plannedMealId: string | null;
  plannedMealFoodItemId: string | null;
  replacementOptionId: string | null;
  loggedAt: string;
}
/** Target, planned, consumed and remaining for a day (§9, §10). */
export interface NutritionDaySummary {
  target: MacroAmounts | null;
  planned: MacroAmounts | null;
  consumed: MacroAmounts;
  remaining: MacroAmounts | null;
  plannedMeals: number;
  completedMeals: number;
  mealCompletionPercent: number | null;
  adherencePercent: number | null;
  entryCount: number;
  isCompleted: boolean;
}
export interface NutritionDay {
  date: string;
  plan: PlannedDay | null;
  entries: FoodLogEntry[];
  summary: NutritionDaySummary;
}
export interface FoodAlternative {
  optionId: string;
  groupId: string;
  groupTitle: string;
  mode: 'Exact' | 'Flexible';
  foodItemId: string;
  nameEn: string;
  nameAr: string | null;
  quantity: number | null;
  foodUnitId: string | null;
  unitLabel: string;
  unitLabelAr: string;
  isCalculated: boolean;
  totals: MacroAmounts;
  notes: string | null;
}
/** A diary entry to add (§8): a planned food, an alternative, a catalog extra or a custom food. */
export interface FoodLogInput {
  date: string;
  slot: MealType;
  foodItemId?: string | null;
  quantity?: number | null;
  foodUnitId?: string | null;
  plannedMealFoodItemId?: string | null;
  replacementOptionId?: string | null;
  custom?: { nameEn: string; calories: number; protein?: number; carbohydrates?: number; fat?: number; fiber?: number } | null;
}
export interface FoodLogUpdate {
  slot?: MealType | null;
  quantity?: number | null;
  foodUnitId?: string | null;
}
export interface DiaryFood {
  id: string;
  nameEn: string;
  nameAr: string | null;
  isCustom: boolean;
  baseUnitLabel: string;
  servingSize: number;
  units: { id: string; nameEn: string; nameAr: string | null; baseQuantity: number }[];
}
export interface NutritionDayRow {
  date: string;
  target: MacroAmounts | null;
  planned: MacroAmounts | null;
  consumed: MacroAmounts;
  adherencePercent: number | null;
  mealCompletionPercent: number | null;
  entryCount: number;
  isCompleted: boolean;
}
export interface NutritionPeriod {
  start: string;
  end: string;
  days: number;
  loggedDays: number;
  completedDays: number;
  averageConsumed: MacroAmounts | null;
  averageTarget: MacroAmounts | null;
  adherencePercent: number | null;
  loggingAdherencePercent: number | null;
}
export interface NutritionAnalytics {
  from: string;
  to: string;
  summary: NutritionPeriod;
  days: NutritionDayRow[];
  weeks: NutritionPeriod[];
  months: NutritionPeriod[];
}

/**
 * The food diary (§8) and planned versus actual (§9–§11). The client works on their own diary under
 * /me (the account decides whose it is); coaches read a client's diary and analytics.
 */
@Injectable({ providedIn: 'root' })
export class NutritionTrackingService {
  private readonly me = `${environment.apiBaseUrl}/me/nutrition`;
  constructor(private readonly http: HttpClient) {}

  myDay(date: string) {
    return this.http.get<NutritionDay>(`${this.me}/days/${date}`);
  }
  choose(date: string, input: { variantId?: string | null; mealOptions?: { mealId: string; optionId: string | null }[] }) {
    return this.http.put<NutritionDay>(`${this.me}/days/${date}/choices`, input);
  }
  alternatives(date: string, plannedFoodId: string) {
    return this.http.get<FoodAlternative[]>(`${this.me}/days/${date}/foods/${plannedFoodId}/alternatives`);
  }
  logMeal(date: string, mealId: string) {
    return this.http.post<NutritionDay>(`${this.me}/days/${date}/meals/${mealId}/log`, null);
  }
  log(input: FoodLogInput) {
    return this.http.post<FoodLogEntry>(`${this.me}/entries`, input);
  }
  update(id: string, input: FoodLogUpdate) {
    return this.http.put<FoodLogEntry>(`${this.me}/entries/${id}`, input);
  }
  remove(id: string) {
    return this.http.delete<void>(`${this.me}/entries/${id}`);
  }
  searchFoods(searchTerm: string) {
    return this.http.get<PagedResult<DiaryFood>>(`${this.me}/foods`, { params: { searchTerm } });
  }

  clientDay(clientId: string, date: string) {
    return this.http.get<NutritionDay>(`${environment.apiBaseUrl}/clients/${clientId}/nutrition/days/${date}`);
  }
  analytics(clientId: string, from: string, to: string) {
    return this.http.get<NutritionAnalytics>(`${environment.apiBaseUrl}/clients/${clientId}/nutrition/analytics`, {
      params: { from, to },
    });
  }
}

/** A local calendar date as yyyy-MM-dd: the client's own "today", not UTC. */
export function isoDate(date: Date): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function addDays(iso: string, days: number): string {
  const [year, month, day] = iso.split('-').map(Number);
  return isoDate(new Date(year, month - 1, day + days));
}

/** How full a target is, 0–100 for a progress bar; over target stays at 100. */
export function progress(consumed: number, target: number | null | undefined): number {
  if (!target || target <= 0) return 0;
  return Math.min(100, Math.round((consumed / target) * 100));
}
