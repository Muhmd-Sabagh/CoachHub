import { AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, ViewChild } from '@angular/core';
import type ApexCharts from 'apexcharts';
import type { ApexOptions } from 'apexcharts';

/**
 * One series of bars on one axis (§20 weekly volume, §23 training distribution across muscle groups). Colours
 * come from the theme tokens at render time, so coach branding and dark mode apply; ApexCharts loads only when a
 * chart is shown. The table next to each chart carries the same numbers.
 */
@Component({
  selector: 'app-training-chart',
  standalone: true,
  template: `<div #host class="training-chart" role="img" [attr.aria-label]="label"></div>`,
  styles: [`.training-chart { min-height: 260px; }`],
})
export class TrainingChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) categories: string[] = [];
  @Input({ required: true }) values: number[] = [];
  @Input({ required: true }) series = '';
  @Input({ required: true }) unit = '';
  @Input() horizontal = false;
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
    const format = (value: number) => `${Math.round(value * 10) / 10}`;
    const options: ApexOptions = {
      chart: { type: 'bar', height: Math.max(260, this.horizontal ? this.categories.length * 34 + 60 : 260), toolbar: { show: false },
        zoom: { enabled: false }, fontFamily: 'inherit', foreColor: ink, background: 'transparent', animations: { enabled: false } },
      theme: { mode: dark ? 'dark' : 'light' },
      series: [{ name: this.series, data: this.values }],
      colors: [token('--brand', '#465fff')],
      plotOptions: { bar: { horizontal: this.horizontal, columnWidth: '55%', barHeight: '60%', borderRadius: 4, borderRadiusApplication: 'end' } },
      dataLabels: { enabled: false },
      xaxis: { categories: this.categories, axisBorder: { show: false }, axisTicks: { show: false },
        labels: this.horizontal ? { formatter: (value: string) => format(Number(value)) } : {},
        title: this.horizontal ? { text: unit } : undefined },
      yaxis: { min: 0, labels: this.horizontal ? {} : { formatter: format }, title: this.horizontal ? undefined : { text: unit } },
      grid: { borderColor: token(dark ? '--gray-700' : '--gray-200', '#e4e7ec'), strokeDashArray: 3 },
      legend: { show: false },
      tooltip: { y: { formatter: (value: number) => `${format(value)} ${unit}` } },
      noData: { text: 'No training in this range' },
    };
    if (this.chart) {
      await this.chart.updateOptions(options, true, false);
    } else {
      this.chart = new Apex(this.host.nativeElement, options);
      await this.chart.render();
    }
  }
}
