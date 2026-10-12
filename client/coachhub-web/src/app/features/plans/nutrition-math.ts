/**
 * Client-side nutrition maths for instant feedback in the diet builder (§6). It mirrors the API's
 * NutritionCalculator; the server's totals stay authoritative once a plan is saved.
 */
export type FoodBaseUnit = 'Gram' | 'Milliliter' | 'Piece';
export type MealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack';
export const mealTypes: MealType[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

export interface FoodUnit {
  id: string;
  nameEn: string;
  nameAr?: string | null;
  baseQuantity: number;
}

export interface NutritionFood {
  id: string;
  baseUnit: FoodBaseUnit;
  baseUnitLabel: string;
  servingSize: number;
  caloriesPer100: number;
  proteinPer100: number;
  carbohydratesPer100: number;
  fatPer100: number;
  fiberPer100?: number | null;
  units: FoodUnit[];
}

export interface NutritionTotals {
  weight: number;
  calories: number;
  protein: number;
  carbohydrates: number;
  fat: number;
  fiber: number;
}

export interface DietTargets {
  calories?: number | null;
  protein?: number | null;
  carbohydrates?: number | null;
  fat?: number | null;
  mealCount?: number | null;
  snackCount?: number | null;
}

export const zeroTotals: NutritionTotals = {
  weight: 0,
  calories: 0,
  protein: 0,
  carbohydrates: 0,
  fat: 0,
  fiber: 0,
};

const round = (value: number) => Math.round(value * 100) / 100;

/** The base-unit amount of a quantity in one of the food's units (its base unit when none). */
export function baseAmount(food: NutritionFood, quantity: number, unitId?: string | null): number {
  const unit = unitId ? food.units.find((x) => x.id === unitId) : undefined;
  return (Number(quantity) || 0) * (unit ? unit.baseQuantity : 1);
}

export function rowTotals(
  food: NutritionFood | undefined,
  quantity: number,
  unitId?: string | null,
): NutritionTotals {
  if (!food) return zeroTotals;
  const amount = baseAmount(food, quantity, unitId);
  const factor = amount / 100;
  return {
    weight: round(amount),
    calories: round(food.caloriesPer100 * factor),
    protein: round(food.proteinPer100 * factor),
    carbohydrates: round(food.carbohydratesPer100 * factor),
    fat: round(food.fatPer100 * factor),
    fiber: round((food.fiberPer100 ?? 0) * factor),
  };
}

export function sumTotals(items: NutritionTotals[]): NutritionTotals {
  const sum = items.reduce(
    (a, b) => ({
      weight: a.weight + b.weight,
      calories: a.calories + b.calories,
      protein: a.protein + b.protein,
      carbohydrates: a.carbohydrates + b.carbohydrates,
      fat: a.fat + b.fat,
      fiber: a.fiber + b.fiber,
    }),
    zeroTotals,
  );
  return {
    weight: round(sum.weight),
    calories: round(sum.calories),
    protein: round(sum.protein),
    carbohydrates: round(sum.carbohydrates),
    fat: round(sum.fat),
    fiber: round(sum.fiber),
  };
}

/** Calories implied by macro grams (4/4/9 kcal per gram). */
export function macroCalories(protein = 0, carbohydrates = 0, fat = 0): number {
  return round((protein ?? 0) * 4 + (carbohydrates ?? 0) * 4 + (fat ?? 0) * 9);
}

/** The label a quantity is shown in: a named unit, or the food's base unit. */
export function unitLabel(food: NutritionFood | undefined, unitId?: string | null): string {
  if (!food) return '';
  return food.units.find((x) => x.id === unitId)?.nameEn ?? food.baseUnitLabel;
}

/** Planned minus target (positive = over target); null when there is no target. */
export function difference(planned: number, target?: number | null): number | null {
  return target === null || target === undefined ? null : round(planned - target);
}
