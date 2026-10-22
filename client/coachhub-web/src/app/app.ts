import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager, selector: 'app-root', templateUrl: './app.html', styleUrl: './app.css', standalone: false })
export class App {}