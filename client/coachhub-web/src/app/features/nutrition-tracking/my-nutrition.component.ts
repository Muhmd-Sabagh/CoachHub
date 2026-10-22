import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { MealType, mealTypes } from '../plans/nutrition-math';
import {
  DiaryFood, FoodAlternative, FoodLogEntry, NutritionDay, NutritionTrackingService, PlannedFood, PlannedMeal,
  addDays, isoDate, progress,
} from './nutrition-tracking.service';

/**
 * The client's nutrition day (§8, §10, §13): target / consumed / remaining, the plan's meals with their
 * options and permitted alternatives (§7), and the food diary. Mobile first.
 */
@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-my-nutrition',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="dashboard-hero">
      <div>
        <p class="eyebrow">Nutrition</p>
        <div class="day-nav">
          <button class="icon-button" type="button" (click)="go(-1)" aria-label="Previous day">‹</button>
          <h1>{{ date === today ? 'Today' : (date | date: 'EEE d MMM') }}</h1>
          <button class="icon-button" type="button" (click)="go(1)" [disabled]="date >= today" aria-label="Next day">›</button>
        </div>
        @if (day?.plan; as plan) { <p>{{ plan.nameEn }} · version {{ plan.versionNumber }}</p> }
        @else if (day) { <p>No diet plan for this day. You can still log what you eat.</p> }
      </div>
    </section>
    @if (error) { <p class="form-error" role="alert">{{ error }}</p> }
    @if (loading && !day) { <p class="table-message">Loading…</p> }

    @if (day; as d) {
      <section class="card workbench summary-card" aria-label="Daily summary">
        <div class="kcal-line">
          <div><small class="muted">Consumed</small><strong>{{ d.summary.consumed.calories | number: '1.0-0' }}</strong></div>
          <div><small class="muted">Target</small><strong>{{ d.summary.target ? (d.summary.target.calories | number: '1.0-0') : '–' }}</strong></div>
          <div><small class="muted">Remaining</small>
            <strong [class.over]="(d.summary.remaining?.calories ?? 0) < 0">{{ d.summary.remaining ? (d.summary.remaining.calories | number: '1.0-0') : '–' }}</strong></div>
        </div>
        <div class="bar" role="progressbar" [attr.aria-valuenow]="bar(d.summary.consumed.calories, d.summary.target?.calories)" aria-valuemin="0" aria-valuemax="100" aria-label="Calories">
          <span [style.width.%]="bar(d.summary.consumed.calories, d.summary.target?.calories)"></span>
        </div>
        <div class="macro-grid">
          @for (m of macros; track m.key) {
            <div>
              <small class="muted">{{ m.label }}</small>
              <span>{{ d.summary.consumed[m.key] | number: '1.0-0' }} / {{ d.summary.target ? (d.summary.target[m.key] | number: '1.0-0') : '–' }} g</span>
              <div class="bar thin"><span [style.width.%]="bar(d.summary.consumed[m.key], d.summary.target?.[m.key])"></span></div>
            </div>
          }
        </div>
        <p class="muted stats-line">
          Meals {{ d.summary.completedMeals }}/{{ d.summary.plannedMeals }}
          @if (d.summary.mealCompletionPercent !== null) { ({{ d.summary.mealCompletionPercent }}%) }
          · Adherence {{ d.summary.adherencePercent !== null ? d.summary.adherencePercent + '%' : '–' }}
          · {{ d.summary.entryCount }} logged
        </p>
      </section>

      @if (d.plan; as plan) {
        @if (plan.variants.length > 1) {
          <label class="variant-picker"><span>Today's plan</span>
            <select [ngModel]="plan.variantId" (ngModelChange)="chooseVariant($event)" name="variant">
              @for (v of plan.variants; track v.id) { <option [ngValue]="v.id">{{ v.nameEn }}</option> }
            </select>
          </label>
        }
        <h2 class="section-title">Planned meals</h2>
        @for (meal of plan.meals; track meal.id) {
          <article class="card workbench meal-card">
            <div class="meal-top">
              <div><strong>{{ meal.nameEn }}</strong><small class="muted"> · {{ meal.totals.calories | number: '1.0-0' }} kcal</small></div>
              @if (meal.isLogged) { <span class="logged">✓ Logged</span> }
              @else if (writable) { <button class="button button-secondary" type="button" [disabled]="busy" (click)="logMeal(meal)">Log meal</button> }
            </div>
            @if (meal.options.length) {
              <div class="option-chips" role="radiogroup" [attr.aria-label]="meal.nameEn + ' options'">
                @for (o of meal.options; track o.id) {
                  <button type="button" role="radio" [attr.aria-checked]="o.id === meal.chosenOptionId" [class.active]="o.id === meal.chosenOptionId"
                    [disabled]="!writable || busy" (click)="chooseOption(meal, o.id)">{{ o.nameEn }} · {{ o.totals.calories | number: '1.0-0' }}</button>
                }
              </div>
            }
            <ul class="food-list">
              @for (food of meal.foods; track food.id) {
                <li>
                  <div class="food-line">
                    <span>{{ food.nameEn }}</span>
                    <small class="muted">{{ food.quantity | number: '1.0-2' }} {{ food.unitLabel }} · {{ food.totals.calories | number: '1.0-0' }} kcal</small>
                    @if (food.alternativeCount > 0 && writable) {
                      <button class="link-button" type="button" (click)="toggleAlternatives(meal, food)">{{ openFood === food.id ? 'Close' : 'Change food' }}</button>
                    }
                  </div>
                  @if (openFood === food.id) {
                    <ul class="alternatives" aria-label="Permitted alternatives">
                      @for (a of alternatives; track a.optionId) {
                        <li>
                          <span>{{ a.nameEn }}</span>
                          <input type="number" min="0.01" step="any" [(ngModel)]="a.quantity" [name]="'alt' + a.optionId" aria-label="Quantity" />
                          <small class="muted">{{ a.unitLabel }}{{ a.isCalculated ? ' (matched)' : '' }}</small>
                          <button class="button button-primary" type="button" [disabled]="busy || !a.quantity" (click)="logSwap(meal, food, a)">Ate this</button>
                        </li>
                      } @empty { <li class="muted">Loading alternatives…</li> }
                    </ul>
                  }
                </li>
              }
            </ul>
          </article>
        }
      }

      <h2 class="section-title">Food diary</h2>
      @for (slot of slots; track slot) {
        @if (entriesFor(d, slot); as entries) {
          @if (entries.length) {
            <section class="card workbench diary-slot">
              <strong>{{ slot }}</strong>
              <ul class="food-list">
                @for (e of entries; track e.id) {
                  <li class="food-line">
                    <span>{{ e.nameEn }} @if (e.source !== 'Planned') { <small class="badge">{{ e.source === 'Swap' ? 'Swapped' : 'Extra' }}</small> }</span>
                    @if (editing === e.id) {
                      <input type="number" min="0.01" step="any" [(ngModel)]="editQuantity" name="editQuantity" aria-label="Quantity" />
                      <button class="link-button" type="button" (click)="saveEdit(e)">Save</button>
                    } @else {
                      <small class="muted">{{ e.quantity !== null ? (e.quantity | number: '1.0-2') + ' ' + e.unitLabel + ' · ' : '' }}{{ e.nutrients.calories | number: '1.0-0' }} kcal</small>
                      @if (writable) {
                        @if (!e.isCustom) { <button class="link-button" type="button" (click)="startEdit(e)">Edit</button> }
                        <button class="link-button danger" type="button" [disabled]="busy" (click)="remove(e)">Delete</button>
                      }
                    }
                  </li>
                }
              </ul>
            </section>
          }
        }
      }
      @if (!d.entries.length) { <p class="muted">Nothing logged yet.</p> }

      @if (writable) {
        <section class="card workbench add-food" aria-label="Add food">
          <div class="section-heading"><h2>Add food</h2>
            <button class="link-button" type="button" (click)="custom = !custom">{{ custom ? 'Search foods' : 'Not in the list?' }}</button></div>
          <label><span>Meal</span>
            <select [(ngModel)]="slot" name="slot">@for (s of slots; track s) { <option [ngValue]="s">{{ s }}</option> }</select></label>
          @if (!custom) {
            <form class="search-row" (ngSubmit)="search()">
              <input type="search" [(ngModel)]="term" name="term" placeholder="Search foods" aria-label="Search foods" />
              <button class="button button-secondary" type="submit" [disabled]="!term.trim()">Search</button>
            </form>
            <ul class="food-list">
              @for (f of results; track f.id) {
                <li class="food-line"><span>{{ f.nameEn }}</span><button class="link-button" type="button" (click)="pick(f)">Choose</button></li>
              }
            </ul>
            @if (picked; as f) {
              <div class="pick-row">
                <strong>{{ f.nameEn }}</strong>
                <input type="number" min="0.01" step="any" [(ngModel)]="quantity" name="quantity" aria-label="Quantity" />
                <select [(ngModel)]="unitId" name="unit" aria-label="Unit">
                  <option [ngValue]="null">{{ f.baseUnitLabel }}</option>
                  @for (u of f.units; track u.id) { <option [ngValue]="u.id">{{ u.nameEn }}</option> }
                </select>
                <button class="button button-primary" type="button" [disabled]="busy || !quantity" (click)="addPicked()">Add</button>
              </div>
            }
          } @else {
            <div class="custom-grid">
              <label class="wide"><span>Name</span><input [(ngModel)]="customFood.nameEn" name="customName" maxlength="255" /></label>
              <label><span>kcal</span><input type="number" min="0" [(ngModel)]="customFood.calories" name="customKcal" /></label>
              <label><span>Protein g</span><input type="number" min="0" [(ngModel)]="customFood.protein" name="customProtein" /></label>
              <label><span>Carbs g</span><input type="number" min="0" [(ngModel)]="customFood.carbohydrates" name="customCarbs" /></label>
              <label><span>Fat g</span><input type="number" min="0" [(ngModel)]="customFood.fat" name="customFat" /></label>
            </div>
            <button class="button button-primary" type="button" [disabled]="busy || !customFood.nameEn.trim()" (click)="addCustom()">Add</button>
          }
        </section>
      }
    }
  `,
  styles: [`
    .day-nav { display: flex; align-items: center; gap: 12px; }
    .day-nav h1 { margin: 0; }
    .summary-card { display: grid; gap: 12px; }
    .kcal-line { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; text-align: center; }
    .kcal-line div { display: grid; gap: 2px; }
    .kcal-line strong { font-size: 1.5rem; }
    .over { color: var(--error); }
    .bar { height: 10px; border-radius: 999px; background: var(--gray-100); overflow: hidden; }
    .bar.thin { height: 6px; }
    .bar span { display: block; height: 100%; border-radius: 999px; background: var(--brand); }
    .macro-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; }
    .macro-grid div { display: grid; gap: 4px; font-size: 0.85rem; }
    .stats-line { margin: 0; font-size: 0.85rem; }
    .section-title { margin: 20px 0 8px; font-size: 1.05rem; }
    .variant-picker { display: grid; gap: 4px; margin-top: 16px; }
    .meal-card, .diary-slot, .add-food { margin-bottom: 12px; display: grid; gap: 8px; }
    .meal-top { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
    .logged { color: var(--success); font-weight: 600; white-space: nowrap; }
    .option-chips { display: flex; flex-wrap: wrap; gap: 8px; }
    .option-chips button { min-height: 40px; padding: 6px 12px; border-radius: 999px; border: 1px solid var(--gray-300); background: transparent; color: inherit; }
    .option-chips button.active { border-color: var(--brand); background: var(--brand-soft); color: var(--brand-dark); font-weight: 600; }
    .food-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
    .food-line { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; min-height: 40px; }
    .food-line > span { flex: 1 1 140px; }
    .alternatives { list-style: none; margin: 8px 0 0; padding: 8px; display: grid; gap: 8px; border-radius: 10px; background: var(--gray-50); }
    .alternatives li { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .alternatives li > span { flex: 1 1 120px; }
    .alternatives input, .food-line input, .pick-row input { width: 84px; }
    .badge { margin-inline-start: 6px; padding: 1px 6px; border-radius: 6px; background: var(--gray-100); color: var(--gray-600); font-size: 0.75rem; }
    .add-food label { display: grid; gap: 4px; }
    .search-row, .pick-row { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    .search-row input { flex: 1 1 160px; }
    .custom-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 8px; }
    .custom-grid .wide { grid-column: 1 / -1; }
    @media (min-width: 640px) { .custom-grid { grid-template-columns: repeat(4, 1fr); } }
  `],
})
export class MyNutritionComponent implements OnInit {
  readonly slots = mealTypes;
  readonly macros = [
    { key: 'protein' as const, label: 'Protein' },
    { key: 'carbohydrates' as const, label: 'Carbs' },
    { key: 'fat' as const, label: 'Fat' },
  ];
  readonly today = isoDate(new Date());
  readonly bar = progress;
  date = this.today;
  day: NutritionDay | null = null;
  loading = false;
  busy = false;
  error = '';
  openFood: string | null = null;
  alternatives: FoodAlternative[] = [];
  editing: string | null = null;
  editQuantity: number | null = null;
  slot: MealType = 'Snack';
  custom = false;
  term = '';
  results: DiaryFood[] = [];
  picked: DiaryFood | null = null;
  quantity: number | null = null;
  unitId: string | null = null;
  customFood = { nameEn: '', calories: 0, protein: 0, carbohydrates: 0, fat: 0 };

  constructor(private readonly api: NutritionTrackingService) {}

  /** The diary takes today and earlier days; the server enforces the same. */
  get writable(): boolean {
    return this.date <= this.today;
  }

  ngOnInit(): void {
    this.load();
  }

  go(days: number): void {
    const next = addDays(this.date, days);
    if (next > this.today) return;
    this.date = next;
    this.openFood = null;
    this.load();
  }

  entriesFor(day: NutritionDay, slot: MealType): FoodLogEntry[] {
    return day.entries.filter(x => x.slot === slot);
  }

  chooseVariant(variantId: string): void {
    this.run(this.api.choose(this.date, { variantId }));
  }

  chooseOption(meal: PlannedMeal, optionId: string): void {
    if (optionId === meal.chosenOptionId) return;
    this.run(this.api.choose(this.date, { mealOptions: [{ mealId: meal.id, optionId }] }));
  }

  logMeal(meal: PlannedMeal): void {
    this.run(this.api.logMeal(this.date, meal.id));
  }

  toggleAlternatives(meal: PlannedMeal, food: PlannedFood): void {
    if (this.openFood === food.id) { this.openFood = null; return; }
    this.openFood = food.id;
    this.alternatives = [];
    this.api.alternatives(this.date, food.id).subscribe({
      next: list => (this.alternatives = list),
      error: e => (this.error = apiErrorMessage(e)),
    });
  }

  /** §7: the alternative is logged instead of the planned food, in the coach's (or the client's) quantity. */
  logSwap(meal: PlannedMeal, food: PlannedFood, alternative: FoodAlternative): void {
    this.run(this.api.log({
      date: this.date, slot: meal.type, plannedMealFoodItemId: food.id, replacementOptionId: alternative.optionId,
      quantity: alternative.quantity, foodUnitId: alternative.foodUnitId,
    }), () => (this.openFood = null));
  }

  startEdit(entry: FoodLogEntry): void {
    this.editing = entry.id;
    this.editQuantity = entry.quantity;
  }

  saveEdit(entry: FoodLogEntry): void {
    if (!this.editQuantity) return;
    this.run(this.api.update(entry.id, { quantity: this.editQuantity, foodUnitId: entry.foodUnitId }), () => (this.editing = null));
  }

  remove(entry: FoodLogEntry): void {
    this.run(this.api.remove(entry.id));
  }

  search(): void {
    const term = this.term.trim();
    if (!term) return;
    this.api.searchFoods(term).subscribe({
      next: page => (this.results = page.items),
      error: e => (this.error = apiErrorMessage(e)),
    });
  }

  pick(food: DiaryFood): void {
    this.picked = food;
    this.quantity = food.servingSize;
    this.unitId = null;
  }

  addPicked(): void {
    if (!this.picked || !this.quantity) return;
    this.run(this.api.log({ date: this.date, slot: this.slot, foodItemId: this.picked.id, quantity: this.quantity, foodUnitId: this.unitId }), () => {
      this.picked = null;
      this.results = [];
      this.term = '';
    });
  }

  addCustom(): void {
    const values = this.customFood;
    this.run(this.api.log({
      date: this.date, slot: this.slot,
      custom: { nameEn: values.nameEn.trim(), calories: +values.calories || 0, protein: +values.protein || 0,
        carbohydrates: +values.carbohydrates || 0, fat: +values.fat || 0 },
    }), () => (this.customFood = { nameEn: '', calories: 0, protein: 0, carbohydrates: 0, fat: 0 }));
  }

  private load(): void {
    this.loading = true;
    this.error = '';
    this.api.myDay(this.date).pipe(finalize(() => (this.loading = false))).subscribe({
      next: day => (this.day = day),
      error: e => (this.error = apiErrorMessage(e)),
    });
  }

  /** Runs a change, then shows the day as the server now sees it (its totals are authoritative). */
  private run(request: Observable<unknown>, after?: () => void): void {
    this.busy = true;
    this.error = '';
    request.pipe(finalize(() => (this.busy = false))).subscribe({
      next: result => {
        after?.();
        if (result && typeof result === 'object' && 'summary' in result) this.day = result as NutritionDay;
        else this.load();
      },
      error: e => (this.error = apiErrorMessage(e)),
    });
  }
}
