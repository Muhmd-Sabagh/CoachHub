import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
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
  selector: 'app-my-nutrition',
  standalone: true,
  imports: [FormsModule, ...I18N_PIPES],
  template: `
    <section class="dashboard-hero">
      <div>
        <p class="eyebrow">{{ 'nutrition.eyebrow' | translate }}</p>
        <div class="day-nav">
          <button class="icon-button" type="button" (click)="go(-1)" [attr.aria-label]="'nutrition.previousDay' | translate"><span class="flip-rtl">‹</span></button>
          <h1>{{ date() === today ? ('nutrition.today' | translate) : (date() | localDate: 'EEE d MMM') }}</h1>
          <button class="icon-button" type="button" (click)="go(1)" [disabled]="date() >= today" [attr.aria-label]="'nutrition.nextDay' | translate"><span class="flip-rtl">›</span></button>
        </div>
        @if (day()?.plan; as plan) { <p>{{ 'nutrition.planVersion' | translate: { name: (plan | localName), version: plan.versionNumber } }}</p> }
        @else if (day()) { <p>{{ 'nutrition.noPlan' | translate }}</p> }
      </div>
    </section>
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (loading() && !day()) { <p class="table-message">{{ 'common.loading' | translate }}</p> }

    @if (day(); as d) {
      <section class="card workbench summary-card" [attr.aria-label]="'nutrition.summary' | translate">
        <div class="kcal-line">
          <div><small class="muted">{{ 'nutrition.consumed' | translate }}</small><strong>{{ d.summary.consumed.calories | localNumber: '1.0-0' }}</strong></div>
          <div><small class="muted">{{ 'nutrition.target' | translate }}</small><strong>{{ d.summary.target ? (d.summary.target.calories | localNumber: '1.0-0') : '–' }}</strong></div>
          <div><small class="muted">{{ 'nutrition.remaining' | translate }}</small>
            <strong [class.over]="(d.summary.remaining?.calories ?? 0) < 0">{{ d.summary.remaining ? (d.summary.remaining.calories | localNumber: '1.0-0') : '–' }}</strong></div>
        </div>
        <div class="bar" role="progressbar" [attr.aria-valuenow]="bar(d.summary.consumed.calories, d.summary.target?.calories)" aria-valuemin="0" aria-valuemax="100" [attr.aria-label]="'nutrition.calories' | translate">
          <span [style.width.%]="bar(d.summary.consumed.calories, d.summary.target?.calories)"></span>
        </div>
        <div class="macro-grid">
          @for (m of macros; track m.key) {
            <div>
              <small class="muted">{{ m.label | translate }}</small>
              <span>{{ d.summary.consumed[m.key] | localNumber: '1.0-0' }} / {{ d.summary.target ? (d.summary.target[m.key] | localNumber: '1.0-0') : '–' }} {{ 'unit.g' | translate }}</span>
              <div class="bar thin"><span [style.width.%]="bar(d.summary.consumed[m.key], d.summary.target?.[m.key])"></span></div>
            </div>
          }
        </div>
        <p class="muted stats-line">
          {{ 'nutrition.meals' | translate: { done: d.summary.completedMeals, planned: d.summary.plannedMeals } }}
          @if (d.summary.mealCompletionPercent !== null) { ({{ d.summary.mealCompletionPercent | localNumber }}%) }
          · {{ 'nutrition.adherence' | translate: { value: d.summary.adherencePercent !== null ? (d.summary.adherencePercent | localNumber) + '%' : '–' } }}
          · {{ 'nutrition.logged' | translate: { count: d.summary.entryCount } }}
        </p>
      </section>

      @if (d.plan; as plan) {
        @if (plan.variants.length > 1) {
          <label class="variant-picker"><span>{{ 'nutrition.todaysPlan' | translate }}</span>
            <select [ngModel]="plan.variantId" (ngModelChange)="chooseVariant($event)" name="variant">
              @for (v of plan.variants; track v.id) { <option [ngValue]="v.id">{{ v | localName }}</option> }
            </select>
          </label>
        }
        <h2 class="section-title">{{ 'nutrition.plannedMeals' | translate }}</h2>
        @for (meal of plan.meals; track meal.id) {
          <article class="card workbench meal-card">
            <div class="meal-top">
              <div><strong translate="no">{{ meal | localName }}</strong><small class="muted"> · {{ 'nutrition.kcal' | translate: { value: (meal.totals.calories | localNumber: '1.0-0') } }}</small></div>
              @if (meal.isLogged) { <span class="logged">{{ 'nutrition.mealLogged' | translate }}</span> }
              @else if (writable) { <button class="button button-secondary" type="button" [disabled]="busy()" (click)="logMeal(meal)">{{ 'nutrition.logMeal' | translate }}</button> }
            </div>
            @if (meal.options.length) {
              <div class="option-chips" role="radiogroup" [attr.aria-label]="'nutrition.mealOptions' | translate: { meal: (meal | localName) }">
                @for (o of meal.options; track o.id) {
                  <button type="button" role="radio" [attr.aria-checked]="o.id === meal.chosenOptionId" [class.active]="o.id === meal.chosenOptionId"
                    [disabled]="!writable || busy()" (click)="chooseOption(meal, o.id)"><span translate="no">{{ o | localName }}</span> · {{ o.totals.calories | localNumber: '1.0-0' }}</button>
                }
              </div>
            }
            <ul class="food-list">
              @for (food of meal.foods; track food.id) {
                <li>
                  <div class="food-line">
                    <span translate="no">{{ food | localName }}</span>
                    <small class="muted">{{ food.quantity | localNumber: '1.0-2' }} <span translate="no">{{ food | localUnitLabel }}</span> · {{ 'nutrition.kcal' | translate: { value: (food.totals.calories | localNumber: '1.0-0') } }}</small>
                    @if (food.alternativeCount > 0 && writable) {
                      <button class="link-button" type="button" (click)="toggleAlternatives(meal, food)">{{ (openFood() === food.id ? 'nutrition.close' : 'nutrition.changeFood') | translate }}</button>
                    }
                  </div>
                  @if (openFood() === food.id) {
                    <ul class="alternatives" [attr.aria-label]="'nutrition.alternatives' | translate">
                      @for (a of alternatives(); track a.optionId) {
                        <li>
                          <span translate="no">{{ a | localName }}</span>
                          <input type="number" min="0.01" step="any" [(ngModel)]="a.quantity" [name]="'alt' + a.optionId" [attr.aria-label]="'nutrition.quantity' | translate" />
                          <small class="muted"><span translate="no">{{ a | localUnitLabel }}</span>@if (a.isCalculated) {{{ 'nutrition.matched' | translate }}}</small>
                          <button class="button button-primary" type="button" [disabled]="busy() || !a.quantity" (click)="logSwap(meal, food, a)">{{ 'nutrition.ateThis' | translate }}</button>
                        </li>
                      } @empty { <li class="muted">{{ 'nutrition.loadingAlternatives' | translate }}</li> }
                    </ul>
                  }
                </li>
              }
            </ul>
          </article>
        }
      }

      <h2 class="section-title">{{ 'nutrition.diary' | translate }}</h2>
      @for (slot of slots; track slot) {
        @if (entriesFor(d, slot); as entries) {
          @if (entries.length) {
            <section class="card workbench diary-slot">
              <strong>{{ slot | enumLabel: 'mealType' }}</strong>
              <ul class="food-list">
                @for (e of entries; track e.id) {
                  <li class="food-line">
                    <span><span translate="no">{{ e | localName }}</span> @if (e.source !== 'Planned') { <small class="badge">{{ e.source | enumLabel: 'foodLogSource' }}</small> }</span>
                    @if (editing() === e.id) {
                      <input type="number" min="0.01" step="any" [(ngModel)]="editQuantity" name="editQuantity" [attr.aria-label]="'nutrition.quantity' | translate" />
                      <button class="link-button" type="button" (click)="saveEdit(e)">{{ 'nutrition.save' | translate }}</button>
                    } @else {
                      <small class="muted">@if (e.quantity !== null) {{{ e.quantity | localNumber: '1.0-2' }} <span translate="no">{{ e | localUnitLabel }}</span> · }{{ 'nutrition.kcal' | translate: { value: (e.nutrients.calories | localNumber: '1.0-0') } }}</small>
                      @if (writable) {
                        @if (!e.isCustom) { <button class="link-button" type="button" (click)="startEdit(e)">{{ 'nutrition.edit' | translate }}</button> }
                        <button class="link-button danger" type="button" [disabled]="busy()" (click)="remove(e)">{{ 'nutrition.delete' | translate }}</button>
                      }
                    }
                  </li>
                }
              </ul>
            </section>
          }
        }
      }
      @if (!d.entries.length) { <p class="muted">{{ 'nutrition.nothingLogged' | translate }}</p> }

      @if (writable) {
        <section class="card workbench add-food" [attr.aria-label]="'nutrition.addFood' | translate">
          <div class="section-heading"><h2>{{ 'nutrition.addFood' | translate }}</h2>
            <button class="link-button" type="button" (click)="custom.set(!custom())">{{ (custom() ? 'nutrition.searchFoods' : 'nutrition.notInList') | translate }}</button></div>
          <label><span>{{ 'nutrition.meal' | translate }}</span>
            <select [(ngModel)]="slot" name="slot">@for (s of slots; track s) { <option [ngValue]="s">{{ s | enumLabel: 'mealType' }}</option> }</select></label>
          @if (!custom()) {
            <form class="search-row" (ngSubmit)="search()">
              <input type="search" [(ngModel)]="term" name="term" [placeholder]="'nutrition.searchFoods' | translate" [attr.aria-label]="'nutrition.searchFoods' | translate" />
              <button class="button button-secondary" type="submit" [disabled]="!term().trim()">{{ 'nutrition.search' | translate }}</button>
            </form>
            <ul class="food-list">
              @for (f of results(); track f.id) {
                <li class="food-line"><span translate="no">{{ f | localName }}</span><button class="link-button" type="button" (click)="pick(f)">{{ 'nutrition.choose' | translate }}</button></li>
              }
            </ul>
            @if (picked(); as f) {
              <div class="pick-row">
                <strong translate="no">{{ f | localName }}</strong>
                <input type="number" min="0.01" step="any" [(ngModel)]="quantity" name="quantity" [attr.aria-label]="'nutrition.quantity' | translate" />
                <select [(ngModel)]="unitId" name="unit" [attr.aria-label]="'nutrition.unit' | translate">
                  <option [ngValue]="null">{{ baseUnit(f) }}</option>
                  @for (u of f.units; track u.id) { <option [ngValue]="u.id">{{ u | localName }}</option> }
                </select>
                <button class="button button-primary" type="button" [disabled]="busy() || !quantity()" (click)="addPicked()">{{ 'nutrition.add' | translate }}</button>
              </div>
            }
          } @else {
            <div class="custom-grid">
              <label class="wide"><span>{{ 'nutrition.name' | translate }}</span><input [(ngModel)]="customFood().nameEn" name="customName" maxlength="255" /></label>
              <label><span>{{ 'nutrition.kcalLabel' | translate }}</span><input type="number" min="0" [(ngModel)]="customFood().calories" name="customKcal" /></label>
              <label><span>{{ 'nutrition.proteinG' | translate }}</span><input type="number" min="0" [(ngModel)]="customFood().protein" name="customProtein" /></label>
              <label><span>{{ 'nutrition.carbsG' | translate }}</span><input type="number" min="0" [(ngModel)]="customFood().carbohydrates" name="customCarbs" /></label>
              <label><span>{{ 'nutrition.fatG' | translate }}</span><input type="number" min="0" [(ngModel)]="customFood().fat" name="customFat" /></label>
            </div>
            <button class="button button-primary" type="button" [disabled]="busy() || !customFood().nameEn.trim()" (click)="addCustom()">{{ 'nutrition.add' | translate }}</button>
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
  readonly macros: { key: 'protein' | 'carbohydrates' | 'fat'; label: TranslationKey }[] = [
    { key: 'protein', label: 'nutrition.protein' },
    { key: 'carbohydrates', label: 'nutrition.carbs' },
    { key: 'fat', label: 'nutrition.fat' },
  ];
  readonly today = isoDate(new Date());
  readonly bar = progress;
  readonly date = signal(this.today);
  readonly day = signal<NutritionDay | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly openFood = signal<string | null>(null);
  readonly alternatives = signal<FoodAlternative[]>([]);
  readonly editing = signal<string | null>(null);
  readonly editQuantity = signal<number | null>(null);
  readonly slot = signal<MealType>('Snack');
  readonly custom = signal(false);
  readonly term = signal('');
  readonly results = signal<DiaryFood[]>([]);
  readonly picked = signal<DiaryFood | null>(null);
  readonly quantity = signal<number | null>(null);
  readonly unitId = signal<string | null>(null);
  readonly customFood = signal({ nameEn: '', calories: 0, protein: 0, carbohydrates: 0, fat: 0 });

  constructor(private readonly api: NutritionTrackingService, private readonly i18n: I18nService) {}

  /** A food's base unit ("g", "ml") in the interface language. */
  baseUnit(food: DiaryFood): string {
    const key: TranslationKey | null = food.baseUnitLabel === 'g' ? 'unit.g' : food.baseUnitLabel === 'ml' ? 'unit.ml' : null;
    return key ? this.i18n.translate(key) : food.baseUnitLabel;
  }

  /** The diary takes today and earlier days; the server enforces the same. */
  get writable(): boolean {
    return this.date() <= this.today;
  }

  ngOnInit(): void {
    this.load();
  }

  go(days: number): void {
    const next = addDays(this.date(), days);
    if (next > this.today) return;
    this.date.set(next);
    this.openFood.set(null);
    this.load();
  }

  entriesFor(day: NutritionDay, slot: MealType): FoodLogEntry[] {
    return day.entries.filter(x => x.slot === slot);
  }

  chooseVariant(variantId: string): void {
    this.run(this.api.choose(this.date(), { variantId }));
  }

  chooseOption(meal: PlannedMeal, optionId: string): void {
    if (optionId === meal.chosenOptionId) return;
    this.run(this.api.choose(this.date(), { mealOptions: [{ mealId: meal.id, optionId }] }));
  }

  logMeal(meal: PlannedMeal): void {
    this.run(this.api.logMeal(this.date(), meal.id));
  }

  toggleAlternatives(meal: PlannedMeal, food: PlannedFood): void {
    if (this.openFood() === food.id) { this.openFood.set(null); return; }
    this.openFood.set(food.id);
    this.alternatives.set([]);
    this.api.alternatives(this.date(), food.id).subscribe({
      next: list => (this.alternatives.set(list)),
      error: e => (this.error.set(apiErrorMessage(e))),
    });
  }

  /** §7: the alternative is logged instead of the planned food, in the coach's (or the client's) quantity. */
  logSwap(meal: PlannedMeal, food: PlannedFood, alternative: FoodAlternative): void {
    this.run(this.api.log({
      date: this.date(), slot: meal.type, plannedMealFoodItemId: food.id, replacementOptionId: alternative.optionId,
      quantity: alternative.quantity, foodUnitId: alternative.foodUnitId,
    }), () => (this.openFood.set(null)));
  }

  startEdit(entry: FoodLogEntry): void {
    this.editing.set(entry.id);
    this.editQuantity.set(entry.quantity);
  }

  saveEdit(entry: FoodLogEntry): void {
    const editQuantity = this.editQuantity();
    if (!editQuantity) return;
    this.run(this.api.update(entry.id, { quantity: editQuantity, foodUnitId: entry.foodUnitId }), () => (this.editing.set(null)));
  }

  remove(entry: FoodLogEntry): void {
    this.run(this.api.remove(entry.id));
  }

  search(): void {
    const term = this.term().trim();
    if (!term) return;
    this.api.searchFoods(term).subscribe({
      next: page => (this.results.set(page.items)),
      error: e => (this.error.set(apiErrorMessage(e))),
    });
  }

  pick(food: DiaryFood): void {
    this.picked.set(food);
    this.quantity.set(food.servingSize);
    this.unitId.set(null);
  }

  addPicked(): void {
    const picked = this.picked();
    const quantity = this.quantity();
    if (!picked || !quantity) return;
    this.run(this.api.log({ date: this.date(), slot: this.slot(), foodItemId: picked.id, quantity, foodUnitId: this.unitId() }), () => {
      this.picked.set(null);
      this.results.set([]);
      this.term.set('');
    });
  }

  addCustom(): void {
    const values = this.customFood();
    this.run(this.api.log({
      date: this.date(), slot: this.slot(),
      custom: { nameEn: values.nameEn.trim(), calories: +values.calories || 0, protein: +values.protein || 0,
        carbohydrates: +values.carbohydrates || 0, fat: +values.fat || 0 },
    }), () => (this.customFood.set({ nameEn: '', calories: 0, protein: 0, carbohydrates: 0, fat: 0 })));
  }

  private load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.myDay(this.date()).pipe(finalize(() => (this.loading.set(false)))).subscribe({
      next: day => (this.day.set(day)),
      error: e => (this.error.set(apiErrorMessage(e))),
    });
  }

  /** Runs a change, then shows the day as the server now sees it (its totals are authoritative). */
  private run(request: Observable<unknown>, after?: () => void): void {
    this.busy.set(true);
    this.error.set('');
    request.pipe(finalize(() => (this.busy.set(false)))).subscribe({
      next: result => {
        after?.();
        if (result && typeof result === 'object' && 'summary' in result) this.day.set(result as NutritionDay);
        else this.load();
      },
      error: e => (this.error.set(apiErrorMessage(e))),
    });
  }
}
