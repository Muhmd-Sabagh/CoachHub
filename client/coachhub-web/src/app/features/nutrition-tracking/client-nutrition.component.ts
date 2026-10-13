import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { mealTypes } from '../plans/nutrition-math';
import { CalorieChartComponent } from './calorie-chart.component';
import {
  NutritionAnalytics, NutritionDay, NutritionPeriod, NutritionTrackingService, PlannedMeal, addDays, isoDate,
} from './nutrition-tracking.service';

/**
 * A client's nutrition for their coach (§40): analytics with planned versus actual (§9, §11), or the food
 * diary of one day (§8). Read-only; the client keeps the diary.
 */
@Component({
  selector: 'app-client-nutrition',
  standalone: true,
  imports: [CommonModule, FormsModule, CalorieChartComponent],
  template: `
    @if (error) { <p class="form-error" role="alert">{{ error }}</p> }
    @if (view === 'analytics') {
      <section class="card workbench">
        <div class="section-heading"><h2>Nutrition</h2>
          <label class="range"><span class="muted">Range</span>
            <select [(ngModel)]="range" (ngModelChange)="loadAnalytics()" name="range">
              <option [ngValue]="7">Last 7 days</option><option [ngValue]="30">Last 30 days</option><option [ngValue]="90">Last 90 days</option>
            </select></label></div>
        @if (analytics; as a) {
          <div class="report-metrics">
            <article><small>Average calories</small><strong>{{ a.summary.averageConsumed ? (a.summary.averageConsumed.calories | number: '1.0-0') : '–' }}</strong>
              <span>target {{ a.summary.averageTarget ? (a.summary.averageTarget.calories | number: '1.0-0') : '–' }}</span></article>
            <article><small>Average protein / carbs / fat</small>
              <strong>{{ a.summary.averageConsumed ? (a.summary.averageConsumed.protein | number: '1.0-0') + ' / ' + (a.summary.averageConsumed.carbohydrates | number: '1.0-0') + ' / ' + (a.summary.averageConsumed.fat | number: '1.0-0') : '–' }}</strong>
              <span>g per logged day</span></article>
            <article><small>Nutrition adherence</small><strong>{{ percent(a.summary.adherencePercent) }}</strong><span>against target</span></article>
            <article><small>Food logging</small><strong>{{ percent(a.summary.loggingAdherencePercent) }}</strong>
              <span>{{ a.summary.loggedDays }} of {{ a.summary.days }} days</span></article>
            <article><small>Completed days</small><strong>{{ a.summary.completedDays }}</strong><span>every planned meal logged</span></article>
          </div>
          <app-calorie-chart [rows]="a.days" />
          <div class="section-heading"><h3>Target vs actual</h3>
            <div class="segmented" role="group" aria-label="Group by">
              <button type="button" [class.active]="grouping === 'weeks'" (click)="grouping = 'weeks'">Weekly</button>
              <button type="button" [class.active]="grouping === 'months'" (click)="grouping = 'months'">Monthly</button>
              <button type="button" [class.active]="grouping === 'days'" (click)="grouping = 'days'">Daily</button>
            </div></div>
          <div class="table-scroll">
            @if (grouping === 'days') {
              <table>
                <thead><tr><th>Day</th><th>Target kcal</th><th>Eaten kcal</th><th>Protein g</th><th>Carbs g</th><th>Fat g</th><th>Meals</th><th>Adherence</th></tr></thead>
                <tbody>
                  @for (row of a.days.slice().reverse(); track row.date) {
                    <tr>
                      <td><button class="link-button" type="button" (click)="openDay(row.date)">{{ row.date | date: 'EEE d MMM' }}</button></td>
                      <td>{{ row.target ? (row.target.calories | number: '1.0-0') : '–' }}</td>
                      <td>{{ row.entryCount ? (row.consumed.calories | number: '1.0-0') : '–' }}</td>
                      <td>{{ row.consumed.protein | number: '1.0-0' }}{{ row.target ? ' / ' + (row.target.protein | number: '1.0-0') : '' }}</td>
                      <td>{{ row.consumed.carbohydrates | number: '1.0-0' }}{{ row.target ? ' / ' + (row.target.carbohydrates | number: '1.0-0') : '' }}</td>
                      <td>{{ row.consumed.fat | number: '1.0-0' }}{{ row.target ? ' / ' + (row.target.fat | number: '1.0-0') : '' }}</td>
                      <td>{{ percent(row.mealCompletionPercent) }}{{ row.isCompleted ? ' ✓' : '' }}</td>
                      <td>{{ percent(row.adherencePercent) }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            } @else {
              <table>
                <thead><tr><th>Period</th><th>Avg target kcal</th><th>Avg eaten kcal</th><th>Avg P / C / F g</th><th>Adherence</th><th>Logging</th><th>Completed</th></tr></thead>
                <tbody>
                  @for (p of periods(a); track p.start) {
                    <tr>
                      <td>{{ p.start | date: 'd MMM' }} – {{ p.end | date: 'd MMM' }}</td>
                      <td>{{ p.averageTarget ? (p.averageTarget.calories | number: '1.0-0') : '–' }}</td>
                      <td>{{ p.averageConsumed ? (p.averageConsumed.calories | number: '1.0-0') : '–' }}</td>
                      <td>{{ p.averageConsumed ? (p.averageConsumed.protein | number: '1.0-0') + ' / ' + (p.averageConsumed.carbohydrates | number: '1.0-0') + ' / ' + (p.averageConsumed.fat | number: '1.0-0') : '–' }}</td>
                      <td>{{ percent(p.adherencePercent) }}</td>
                      <td>{{ p.loggedDays }}/{{ p.days }} ({{ percent(p.loggingAdherencePercent) }})</td>
                      <td>{{ p.completedDays }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>
        } @else if (loading) { <p class="table-message">Loading…</p> }
      </section>
    } @else {
      <section class="card workbench">
        <div class="section-heading"><h2>Food diary</h2>
          <div class="day-nav">
            <button class="icon-button" type="button" (click)="shift(-1)" aria-label="Previous day">‹</button>
            <input type="date" [(ngModel)]="date" (ngModelChange)="loadDay()" name="date" [max]="today" aria-label="Day" />
            <button class="icon-button" type="button" (click)="shift(1)" [disabled]="date >= today" aria-label="Next day">›</button>
          </div></div>
        @if (day; as d) {
          <p>
            <strong>{{ d.summary.consumed.calories | number: '1.0-0' }}</strong> of
            {{ d.summary.target ? (d.summary.target.calories | number: '1.0-0') : '–' }} kcal
            · P {{ d.summary.consumed.protein | number: '1.0-0' }} · C {{ d.summary.consumed.carbohydrates | number: '1.0-0' }}
            · F {{ d.summary.consumed.fat | number: '1.0-0' }} g
            · meals {{ d.summary.completedMeals }}/{{ d.summary.plannedMeals }} · adherence {{ percent(d.summary.adherencePercent) }}
          </p>
          @if (d.plan; as plan) {
            <p class="muted">{{ plan.nameEn }} v{{ plan.versionNumber }} · {{ variantName(d) }}</p>
            <div class="table-scroll"><table>
              <thead><tr><th>Planned meal</th><th>Foods</th><th>Planned kcal</th><th>Logged</th></tr></thead>
              <tbody>
                @for (meal of plan.meals; track meal.id) {
                  <tr>
                    <td>{{ meal.nameEn }}@if (meal.options.length) { <small class="muted"> · {{ optionName(meal) }}</small> }</td>
                    <td>@for (f of meal.foods; track f.id) { <span class="nowrap">{{ f.nameEn }} {{ f.quantity | number: '1.0-2' }} {{ f.unitLabel }}</span>{{ $last ? '' : ', ' }} }</td>
                    <td>{{ meal.totals.calories | number: '1.0-0' }}</td>
                    <td>{{ meal.isLogged ? '✓' : '–' }}</td>
                  </tr>
                }
              </tbody>
            </table></div>
          } @else { <p class="muted">No diet plan applied to this day.</p> }
          <div class="table-scroll"><table>
            <thead><tr><th>Meal</th><th>Food</th><th>Amount</th><th>Source</th><th>kcal</th><th>P / C / F g</th></tr></thead>
            <tbody>
              @for (e of sorted(d); track e.id) {
                <tr>
                  <td>{{ e.slot }}</td><td>{{ e.nameEn }}</td>
                  <td>{{ e.quantity !== null ? (e.quantity | number: '1.0-2') + ' ' + e.unitLabel : 'custom' }}</td>
                  <td>{{ e.source }}</td>
                  <td>{{ e.nutrients.calories | number: '1.0-0' }}</td>
                  <td>{{ e.nutrients.protein | number: '1.0-0' }} / {{ e.nutrients.carbohydrates | number: '1.0-0' }} / {{ e.nutrients.fat | number: '1.0-0' }}</td>
                </tr>
              } @empty { <tr><td colspan="6" class="muted">Nothing logged.</td></tr> }
            </tbody>
          </table></div>
        } @else if (loading) { <p class="table-message">Loading…</p> }
      </section>
    }
  `,
  styles: [`
    .range { display: flex; gap: 8px; align-items: center; }
    .day-nav { display: flex; gap: 8px; align-items: center; }
    .segmented { display: inline-flex; border: 1px solid var(--gray-200); border-radius: 8px; overflow: hidden; }
    .segmented button { padding: 6px 12px; border: 0; background: transparent; color: inherit; }
    .segmented button.active { background: var(--brand-soft); color: var(--brand-dark); font-weight: 600; }
    .nowrap { white-space: nowrap; }
  `],
})
export class ClientNutritionComponent implements OnChanges {
  @Input({ required: true }) clientId = '';
  @Input() view: 'analytics' | 'diary' = 'analytics';
  readonly today = isoDate(new Date());
  range = 30;
  grouping: 'weeks' | 'months' | 'days' = 'weeks';
  date = this.today;
  analytics: NutritionAnalytics | null = null;
  day: NutritionDay | null = null;
  loading = false;
  error = '';

  constructor(private readonly api: NutritionTrackingService) {}

  ngOnChanges(): void {
    if (this.view === 'analytics') this.loadAnalytics();
    else this.loadDay();
  }

  loadAnalytics(): void {
    this.fetch(() => this.api.analytics(this.clientId, addDays(this.today, -(this.range - 1)), this.today)
      .pipe(finalize(() => (this.loading = false))).subscribe({ next: a => (this.analytics = a), error: e => (this.error = apiErrorMessage(e)) }));
  }

  loadDay(): void {
    if (!this.date) return;
    this.fetch(() => this.api.clientDay(this.clientId, this.date)
      .pipe(finalize(() => (this.loading = false))).subscribe({ next: d => (this.day = d), error: e => (this.error = apiErrorMessage(e)) }));
  }

  openDay(date: string): void {
    this.date = date;
    this.view = 'diary';
    this.loadDay();
  }

  shift(days: number): void {
    const next = addDays(this.date, days);
    if (next > this.today) return;
    this.date = next;
    this.loadDay();
  }

  periods(analytics: NutritionAnalytics): NutritionPeriod[] {
    return (this.grouping === 'weeks' ? analytics.weeks : analytics.months).slice().reverse();
  }

  sorted(day: NutritionDay) {
    return day.entries.slice().sort((a, b) => mealTypes.indexOf(a.slot) - mealTypes.indexOf(b.slot));
  }

  variantName(day: NutritionDay): string {
    return day.plan?.variants.find(x => x.id === day.plan?.variantId)?.nameEn ?? '';
  }

  optionName(meal: PlannedMeal): string {
    return meal.options.find(x => x.id === meal.chosenOptionId)?.nameEn ?? '';
  }

  percent(value: number | null | undefined): string {
    return value === null || value === undefined ? '–' : `${value}%`;
  }

  private fetch(start: () => void): void {
    this.loading = true;
    this.error = '';
    start();
  }
}
