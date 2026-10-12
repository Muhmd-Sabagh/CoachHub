import { FoodBaseUnit, FoodUnit } from '../plans/nutrition-math';

export interface CatalogItem {
  id: string;
  nameEn: string;
  nameAr?: string | null;
  foodCategoryId?: string;
  exerciseCategoryId?: string;
  /** Foods (§5): null for platform foods, the owning coach for custom foods. */
  coachId?: string | null;
  isCustom?: boolean;
  baseUnit?: FoodBaseUnit;
  baseUnitLabel?: string;
  pieceNameEn?: string | null;
  pieceNameAr?: string | null;
  servingSize?: number;
  caloriesPer100?: number;
  proteinPer100?: number;
  carbohydratesPer100?: number;
  fatPer100?: number;
  fiberPer100?: number | null;
  sugarPer100?: number | null;
  sodiumMgPer100?: number | null;
  units?: FoodUnit[];
  mediaId?: string | null;
  youTubeUrl?: string | null;
  isActive: boolean;
}
