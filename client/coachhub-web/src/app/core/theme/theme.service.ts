import { DOCUMENT } from '@angular/common';
import { Inject, Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly storageKey = 'coachhub.theme';
  readonly theme = signal<Theme>(this.restoreTheme());
  constructor(@Inject(DOCUMENT) private readonly document: Document) { this.apply(this.theme()); }
  toggle(): void { this.set(this.theme() === 'light' ? 'dark' : 'light'); }
  set(theme: Theme): void { this.theme.set(theme); localStorage.setItem(this.storageKey, theme); this.apply(theme); }
  private restoreTheme(): Theme {
    const stored = localStorage.getItem(this.storageKey);
    if (stored === 'light' || stored === 'dark') return stored;
    return matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  private apply(theme: Theme): void { this.document.documentElement.classList.toggle('dark', theme === 'dark'); this.document.documentElement.style.colorScheme = theme; }
}