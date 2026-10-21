import { AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, ViewChild } from '@angular/core';
import type ApexCharts from 'apexcharts';
import type { ApexAxisChartSeries, ApexOptions } from 'apexcharts';

/**
 * A measure over time (§24 weight trend graph, §25, §27, §31–§33) as a line, with the target as a dashed line on the
 * same axis when there is one. Colours come from the theme tokens at render time; ApexCharts loads only when a chart
 * is shown. The table next to each chart carries the same numbers.
 */
@Component({
  selector: 'app-trend-chart',
  standalone: true,
  template: `<div #host class="trend-chart" role="img" [attr.aria-label]="label"></div>`,
  styles: [`.trend-chart { min-height: 240px; }`],
})
export class TrendChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) points: { date: string; value: number | null }[] = [];
  @Input({ required: true }) series = '';
  @Input({ required: true }) unit = '';
  @Input() target: number | null = null;
  @Input() bars = false;
  @Input() label = '';
  @ViewChild('host', { static: true }) host!: ElementRef<HTMLElement>;
  private chart: ApexCharts | null = null;
  private ready = false;

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
    const unit = this.unit;
    const format = (value: number | null) => (value === null || value === undefined ? '–' : `${Math.round(value * 100) / 100} ${unit}`);
    const series: ApexAxisChartSeries = [{ name: this.series, type: this.bars ? 'column' : 'line', data: this.points.map(x => x.value) }];
    if (this.target !== null) series.push({ name: 'Target', type: 'line', data: this.points.map(() => this.target) });
    const options: ApexOptions = {
      chart: { type: 'line', height: 240, toolbar: { show: false }, zoom: { enabled: false }, fontFamily: 'inherit', foreColor: ink,
        background: 'transparent', animations: { enabled: false } },
      theme: { mode: dark ? 'dark' : 'light' },
      series,
      colors: [token('--brand', '#465fff'), token(dark ? '--gray-300' : '--gray-700', '#344054')],
      stroke: { width: [this.bars ? 0 : 3, 2], dashArray: [0, 6], curve: 'straight' },
      markers: { size: this.bars ? 0 : 4, hover: { size: 6 } },
      plotOptions: { bar: { columnWidth: '55%', borderRadius: 4, borderRadiusApplication: 'end' } },
      dataLabels: { enabled: false },
      xaxis: { categories: this.points.map(x => x.date.slice(5)), tickAmount: Math.min(this.points.length, 10), axisBorder: { show: false }, axisTicks: { show: false } },
      yaxis: { labels: { formatter: (value: number) => `${Math.round(value * 10) / 10}` }, title: { text: unit } },
      grid: { borderColor: token(dark ? '--gray-700' : '--gray-200', '#e4e7ec'), strokeDashArray: 3 },
      legend: { show: this.target !== null, position: 'top', horizontalAlign: 'left' },
      tooltip: { shared: true, intersect: false, y: { formatter: format } },
      noData: { text: 'Nothing recorded in this range' },
    };
    if (this.chart) {
      await this.chart.updateOptions(options, true, false);
    } else {
      this.chart = new Apex(this.host.nativeElement, options);
      await this.chart.render();
    }
  }
}
