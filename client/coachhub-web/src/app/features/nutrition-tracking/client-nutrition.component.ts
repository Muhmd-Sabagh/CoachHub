import { Component, Input, OnChanges, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
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
  imports: [FormsModule, CalorieChartComponent, ...I18N_PIPES],
  template: `
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (view === 'analytics') {
      <section class="card workbench">
        <div class="section-heading"><h2>{{ 'coachNutrition.title' | translate }}</h2>
          <label class="range"><span class="muted">{{ 'range.label' | translate }}</span>
            <select [(ngModel)]="range" (ngModelChange)="loadAnalytics()" name="range">
              <option [ngValue]="7">{{ 'range.7' | translate }}</option><option [ngValue]="30">{{ 'range.30' | translate }}</option><option [ngValue]="90">{{ 'range.90' | translate }}</option>
            </select></label></div>
        @if (analytics(); as a) {
          <div class="report-metrics">
            <article><small>{{ 'coachNutrition.avgCalories' | translate }}</small><strong>{{ a.summary.averageConsumed ? (a.summary.averageConsumed.calories | localNumber: '1.0-0') : '–' }}</strong>
              <span>{{ 'coachNutrition.target' | translate: { value: a.summary.averageTarget ? (a.summary.averageTarget.calories | localNumber: '1.0-0') : '–' } }}</span></article>
            <article><small>{{ 'coachNutrition.avgMacros' | translate }}</small>
              <strong>{{ a.summary.averageConsumed ? (a.summary.averageConsumed.protein | localNumber: '1.0-0') + ' / ' + (a.summary.averageConsumed.carbohydrates | localNumber: '1.0-0') + ' / ' + (a.summary.averageConsumed.fat | localNumber: '1.0-0') : '–' }}</strong>
              <span>{{ 'coachNutrition.perLoggedDay' | translate }}</span></article>
            <article><small>{{ 'coachNutrition.adherence' | translate }}</small><strong>{{ percent(a.summary.adherencePercent) }}</strong><span>{{ 'coachNutrition.againstTarget' | translate }}</span></article>
            <article><small>{{ 'coachNutrition.logging' | translate }}</small><strong>{{ percent(a.summary.loggingAdherencePercent) }}</strong>
              <span>{{ 'coachNutrition.loggedDays' | translate: { logged: a.summary.loggedDays, days: a.summary.days } }}</span></article>
            <article><small>{{ 'coachNutrition.completedDays' | translate }}</small><strong>{{ a.summary.completedDays }}</strong><span>{{ 'coachNutrition.everyMeal' | translate }}</span></article>
          </div>
          <app-calorie-chart [rows]="a.days" />
          <div class="section-heading"><h3>{{ 'coachNutrition.targetVsActual' | translate }}</h3>
            <div class="segmented" role="group" [attr.aria-label]="'group.label' | translate">
              <button type="button" [class.active]="grouping() === 'weeks'" (click)="grouping.set('weeks')">{{ 'group.weeks' | translate }}</button>
              <button type="button" [class.active]="grouping() === 'months'" (click)="grouping.set('months')">{{ 'group.months' | translate }}</button>
              <button type="button" [class.active]="grouping() === 'days'" (click)="grouping.set('days')">{{ 'group.days' | translate }}</button>
            </div></div>
          <div class="table-scroll">
            @if (grouping() === 'days') {
              <table>
                <thead><tr><th>{{ 'coachNutrition.day' | translate }}</th><th>{{ 'coachNutrition.targetKcal' | translate }}</th><th>{{ 'coachNutrition.eatenKcal' | translate }}</th>
                  <th>{{ 'coachNutrition.protein' | translate }}</th><th>{{ 'coachNutrition.carbs' | translate }}</th><th>{{ 'coachNutrition.fat' | translate }}</th>
                  <th>{{ 'coachNutrition.meals' | translate }}</th><th>{{ 'coachNutrition.adherenceColumn' | translate }}</th></tr></thead>
                <tbody>
                  @for (row of a.days.slice().reverse(); track row.date) {
                    <tr>
                      <td><button class="link-button" type="button" (click)="openDay(row.date)">{{ row.date | localDate: 'EEE d MMM' }}</button></td>
                      <td>{{ row.target ? (row.target.calories | localNumber: '1.0-0') : '–' }}</td>
                      <td>{{ row.entryCount ? (row.consumed.calories | localNumber: '1.0-0') : '–' }}</td>
                      <td>{{ row.consumed.protein | localNumber: '1.0-0' }}{{ row.target ? ' / ' + (row.target.protein | localNumber: '1.0-0') : '' }}</td>
                      <td>{{ row.consumed.carbohydrates | localNumber: '1.0-0' }}{{ row.target ? ' / ' + (row.target.carbohydrates | localNumber: '1.0-0') : '' }}</td>
                      <td>{{ row.consumed.fat | localNumber: '1.0-0' }}{{ row.target ? ' / ' + (row.target.fat | localNumber: '1.0-0') : '' }}</td>
                      <td>{{ percent(row.mealCompletionPercent) }}{{ row.isCompleted ? ' ✓' : '' }}</td>
                      <td>{{ percent(row.adherencePercent) }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            } @else {
              <table>
                <thead><tr><th>{{ 'coachNutrition.period' | translate }}</th><th>{{ 'coachNutrition.avgTarget' | translate }}</th><th>{{ 'coachNutrition.avgEaten' | translate }}</th>
                  <th>{{ 'coachNutrition.avgPcf' | translate }}</th><th>{{ 'coachNutrition.adherenceColumn' | translate }}</th><th>{{ 'coachNutrition.loggingColumn' | translate }}</th><th>{{ 'coachNutrition.completed' | translate }}</th></tr></thead>
                <tbody>
                  @for (p of periods(a); track p.start) {
                    <tr>
                      <td>{{ p.start | localDate: 'd MMM' }} – {{ p.end | localDate: 'd MMM' }}</td>
                      <td>{{ p.averageTarget ? (p.averageTarget.calories | localNumber: '1.0-0') : '–' }}</td>
                      <td>{{ p.averageConsumed ? (p.averageConsumed.calories | localNumber: '1.0-0') : '–' }}</td>
                      <td>{{ p.averageConsumed ? (p.averageConsumed.protein | localNumber: '1.0-0') + ' / ' + (p.averageConsumed.carbohydrates | localNumber: '1.0-0') + ' / ' + (p.averageConsumed.fat | localNumber: '1.0-0') : '–' }}</td>
                      <td>{{ percent(p.adherencePercent) }}</td>
                      <td>{{ p.loggedDays }}/{{ p.days }} ({{ percent(p.loggingAdherencePercent) }})</td>
                      <td>{{ p.completedDays }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>
        } @else if (loading()) { <p class="table-message">{{ 'common.loading' | translate }}</p> }
      </section>
    } @else {
      <section class="card workbench">
        <div class="section-heading"><h2>{{ 'coachNutrition.diary' | translate }}</h2>
          <div class="day-nav">
            <button class="icon-button flip-rtl" type="button" (click)="shift(-1)" [attr.aria-label]="'coachNutrition.previousDay' | translate">‹</button>
            <input type="date" [(ngModel)]="date" (ngModelChange)="loadDay()" name="date" [max]="today" [attr.aria-label]="'coachNutrition.dayInput' | translate" />
            <button class="icon-button flip-rtl" type="button" (click)="shift(1)" [disabled]="date() >= today" [attr.aria-label]="'coachNutrition.nextDay' | translate">›</button>
          </div></div>
        @if (day(); as d) {
          <p>{{ 'coachNutrition.daySummary' | translate: {
            eaten: (d.summary.consumed.calories | localNumber: '1.0-0'),
            target: d.summary.target ? (d.summary.target.calories | localNumber: '1.0-0') : '–',
            protein: (d.summary.consumed.protein | localNumber: '1.0-0'), carbs: (d.summary.consumed.carbohydrates | localNumber: '1.0-0'),
            fat: (d.summary.consumed.fat | localNumber: '1.0-0'), done: d.summary.completedMeals, planned: d.summary.plannedMeals,
            adherence: percent(d.summary.adherencePercent) } }}</p>
          @if (d.plan; as plan) {
            <p class="muted"><span translate="no">{{ plan | localName }}</span>{{ 'coachNutrition.planLine' | translate: { version: plan.versionNumber } }}<span translate="no">{{ variantName(d) }}</span></p>
            <div class="table-scroll"><table>
              <thead><tr><th>{{ 'coachNutrition.plannedMeal' | translate }}</th><th>{{ 'coachNutrition.foods' | translate }}</th><th>{{ 'coachNutrition.plannedKcal' | translate }}</th><th>{{ 'coachNutrition.loggedColumn' | translate }}</th></tr></thead>
              <tbody>
                @for (meal of plan.meals; track meal.id) {
                  <tr>
                    <td><span translate="no">{{ meal | localName }}</span>@if (meal.options.length) { <small class="muted" translate="no"> · {{ optionName(meal) }}</small> }</td>
                    <td>@for (f of meal.foods; track f.id) { <span class="nowrap" translate="no">{{ f | localName }} {{ f.quantity | localNumber: '1.0-2' }} {{ f | localUnitLabel }}</span>{{ $last ? '' : ', ' }} }</td>
                    <td>{{ meal.totals.calories | localNumber: '1.0-0' }}</td>
                    <td>{{ meal.isLogged ? '✓' : '–' }}</td>
                  </tr>
                }
              </tbody>
            </table></div>
          } @else { <p class="muted">{{ 'coachNutrition.noPlan' | translate }}</p> }
          <div class="table-scroll"><table>
            <thead><tr><th>{{ 'coachNutrition.meal' | translate }}</th><th>{{ 'coachNutrition.food' | translate }}</th><th>{{ 'coachNutrition.amount' | translate }}</th>
              <th>{{ 'coachNutrition.source' | translate }}</th><th>{{ 'coachNutrition.kcal' | translate }}</th><th>{{ 'coachNutrition.pcf' | translate }}</th></tr></thead>
            <tbody>
              @for (e of sorted(d); track e.id) {
                <tr>
                  <td>{{ e.slot | enumLabel: 'mealType' }}</td><td translate="no">{{ e | localName }}</td>
                  <td>@if (e.quantity !== null) { {{ e.quantity | localNumber: '1.0-2' }} <span translate="no">{{ e | localUnitLabel }}</span> } @else { {{ 'coachNutrition.custom' | translate }} }</td>
                  <td>{{ e.source | enumLabel: 'foodLogSource' }}</td>
                  <td>{{ e.nutrients.calories | localNumber: '1.0-0' }}</td>
                  <td>{{ e.nutrients.protein | localNumber: '1.0-0' }} / {{ e.nutrients.carbohydrates | localNumber: '1.0-0' }} / {{ e.nutrients.fat | localNumber: '1.0-0' }}</td>
                </tr>
              } @empty { <tr><td colspan="6" class="muted">{{ 'coachNutrition.nothing' | translate }}</td></tr> }
            </tbody>
          </table></div>
        } @else if (loading()) { <p class="table-message">{{ 'common.loading' | translate }}</p> }
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
  readonly range = signal(30);
  readonly grouping = signal<'weeks' | 'months' | 'days'>('weeks');
  readonly date = signal(this.today);
  readonly analytics = signal<NutritionAnalytics | null>(null);
  readonly day = signal<NutritionDay | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');

  constructor(private readonly api: NutritionTrackingService, private readonly i18n: I18nService) {}

  ngOnChanges(): void {
    if (this.view === 'analytics') this.loadAnalytics();
    else this.loadDay();
  }

  loadAnalytics(): void {
    this.fetch(() => this.api.analytics(this.clientId, addDays(this.today, -(this.range() - 1)), this.today)
      .pipe(finalize(() => (this.loading.set(false)))).subscribe({ next: a => (this.analytics.set(a)), error: e => (this.error.set(apiErrorMessage(e))) }));
  }

  loadDay(): void {
    if (!this.date()) return;
    this.fetch(() => this.api.clientDay(this.clientId, this.date())
      .pipe(finalize(() => (this.loading.set(false)))).subscribe({ next: d => (this.day.set(d)), error: e => (this.error.set(apiErrorMessage(e))) }));
  }

  openDay(date: string): void {
    this.date.set(date);
    this.view = 'diary';
    this.loadDay();
  }

  shift(days: number): void {
    const next = addDays(this.date(), days);
    if (next > this.today) return;
    this.date.set(next);
    this.loadDay();
  }

  periods(analytics: NutritionAnalytics): NutritionPeriod[] {
    return (this.grouping() === 'weeks' ? analytics.weeks : analytics.months).slice().reverse();
  }

  sorted(day: NutritionDay) {
    return day.entries.slice().sort((a, b) => mealTypes.indexOf(a.slot) - mealTypes.indexOf(b.slot));
  }

  variantName(day: NutritionDay): string {
    const variant = day.plan?.variants.find(x => x.id === day.plan?.variantId);
    return variant ? this.i18n.name(variant) : '';
  }

  optionName(meal: PlannedMeal): string {
    const option = meal.options.find(x => x.id === meal.chosenOptionId);
    return option ? this.i18n.name(option) : '';
  }

  percent(value: number | null | undefined): string {
    return value === null || value === undefined ? '–' : `${value}%`;
  }

  private fetch(start: () => void): void {
    this.loading.set(true);
    this.error.set('');
    start();
  }
}
