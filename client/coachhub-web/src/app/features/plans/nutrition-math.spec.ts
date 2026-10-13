import { describe, expect, it } from 'vitest';
import {
  NutritionFood,
  baseAmount,
  countedRows,
  difference,
  macroCalories,
  rowTotals,
  sumTotals,
  unitLabel,
} from './nutrition-math';

const oats: NutritionFood = {
  id: 'oats',
  baseUnit: 'Gram',
  baseUnitLabel: 'g',
  servingSize: 80,
  caloriesPer100: 389,
  proteinPer100: 16.9,
  carbohydratesPer100: 66.3,
  fatPer100: 6.9,
  fiberPer100: 10.6,
  units: [{ id: 'cup', nameEn: 'cup', nameAr: 'كوب', baseQuantity: 80 }],
};
const egg: NutritionFood = {
  id: 'egg',
  baseUnit: 'Piece',
  baseUnitLabel: 'large egg',
  servingSize: 1,
  caloriesPer100: 7150,
  proteinPer100: 600,
  carbohydratesPer100: 50,
  fatPer100: 500,
  fiberPer100: null,
  units: [],
};

describe('nutrition maths (§6, mirrors the API)', () => {
  it('converts named units to base units', () => {
    expect(baseAmount(oats, 1.5, 'cup')).toBe(120);
    expect(baseAmount(oats, 50)).toBe(50);
    expect(baseAmount(oats, 2, 'unknown')).toBe(2);
  });

  it('matches the server totals for a cup of oats and three eggs', () => {
    expect(rowTotals(oats, 1.5, 'cup')).toEqual({
      weight: 120,
      calories: 466.8,
      protein: 20.28,
      carbohydrates: 79.56,
      fat: 8.28,
      fiber: 12.72,
    });
    const meal = sumTotals([rowTotals(oats, 1.5, 'cup'), rowTotals(egg, 3)]);
    expect(meal.calories).toBe(681.3);
    expect(meal.fiber).toBe(12.72);
    expect(rowTotals(undefined, 10).calories).toBe(0);
  });

  it('labels quantities and compares with targets', () => {
    expect(unitLabel(oats, 'cup')).toBe('cup');
    expect(unitLabel(egg, null)).toBe('large egg');
    expect(macroCalories(150, 200, 60)).toBe(1940);
    expect(difference(2100, 2200)).toBe(-100);
    expect(difference(2100, null)).toBeNull();
  });

  it('counts the chosen meal option, by default the first', () => {
    const rows = [
      { id: 'a1', mealOptionId: 'a' },
      { id: 'b1', mealOptionId: 'b' },
      { id: 'b2', mealOptionId: 'b' },
    ];
    const options = [
      { id: 'a', nameEn: 'Option A' },
      { id: 'b', nameEn: 'Option B' },
    ];
    expect(countedRows(rows, options).map((x) => x.id)).toEqual(['a1']);
    expect(countedRows(rows, options, 'b').map((x) => x.id)).toEqual(['b1', 'b2']);
    expect(countedRows(rows, options, 'missing').map((x) => x.id)).toEqual(['a1']);
    expect(countedRows(rows, []).length).toBe(3);
  });
});
