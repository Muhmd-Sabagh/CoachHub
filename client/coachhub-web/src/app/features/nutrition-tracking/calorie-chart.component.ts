import { AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, ViewChild } from '@angular/core';
import type ApexCharts from 'apexcharts';
import type { ApexOptions } from 'apexcharts';
import { NutritionDayRow } from './nutrition-tracking.service';

/**
 * Daily calories eaten (bars) against the target (dashed line) on one kcal axis (§11). Colours come from the
 * theme tokens at render time, so coach branding and dark mode apply. ApexCharts loads only when a chart is
 * shown. The analytics table next to it carries the same numbers for screen readers.
 */
@Component({
  selector: 'app-calorie-chart',
  standalone: true,
  template: `<div #host class="calorie-chart" role="img" [attr.aria-label]="label"></div>`,
  styles: [`.calorie-chart { min-height: 280px; }`],
})
export class CalorieChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) rows: NutritionDayRow[] = [];
  @ViewChild('host', { static: true }) host!: ElementRef<HTMLElement>;
  private chart: ApexCharts | null = null;
  private ready = false;

  get label(): string {
    return `Calories eaten against target for ${this.rows.length} days`;
  }

  ngAfterViewInit(): void {
    this.ready = true;
    void this.render();
  }

  ngOnChanges(): void {
    if (this.ready) void this.render();
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private async render(): Promise<void> {
    const { default: Apex } = await import('apexcharts');
    const style = getComputedStyle(document.documentElement);
    const token = (name: string, fallback: string) => style.getPropertyValue(name).trim() || fallback;
    const dark = document.documentElement.classList.contains('dark');
    const ink = token(dark ? '--gray-400' : '--gray-500', '#667085');
    const options: ApexOptions = {
      chart: { type: 'line', height: 280, toolbar: { show: false }, zoom: { enabled: false }, fontFamily: 'inherit', foreColor: ink,
        background: 'transparent', animations: { enabled: false } },
      theme: { mode: dark ? 'dark' : 'light' },
      series: [
        { name: 'Eaten', type: 'column', data: this.rows.map(x => (x.entryCount ? Math.round(x.consumed.calories) : null)) },
        { name: 'Target', type: 'line', data: this.rows.map(x => (x.target ? Math.round(x.target.calories) : null)) },
      ],
      colors: [token('--brand', '#465fff'), token(dark ? '--gray-300' : '--gray-700', '#344054')],
      stroke: { width: [0, 2], dashArray: [0, 6], curve: 'straight' },
      plotOptions: { bar: { columnWidth: '55%', borderRadius: 4, borderRadiusApplication: 'end' } },
      markers: { size: 0, hover: { size: 5 } },
      dataLabels: { enabled: false },
      xaxis: { categories: this.rows.map(x => x.date.slice(5)), tickAmount: Math.min(this.rows.length, 10), axisBorder: { show: false }, axisTicks: { show: false } },
      yaxis: { min: 0, labels: { formatter: (value: number) => `${Math.round(value)}` }, title: { text: 'kcal' } },
      grid: { borderColor: token(dark ? '--gray-700' : '--gray-200', '#e4e7ec'), strokeDashArray: 3 },
      legend: { position: 'top', horizontalAlign: 'left' },
      tooltip: { shared: true, intersect: false, y: { formatter: (value: number | null) => (value === null || value === undefined ? '–' : `${value} kcal`) } },
      noData: { text: 'Nothing logged in this range' },
    };
    if (this.chart) {
      await this.chart.updateOptions(options, true, false);
    } else {
      this.chart = new Apex(this.host.nativeElement, options);
      await this.chart.render();
    }
  }
}
