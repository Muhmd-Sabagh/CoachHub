import { DOCUMENT } from '@angular/common';
import { Inject, Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

/** Light/dark mode, plus the brand colors (D1, §63) applied to the TailAdmin design tokens at runtime. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly storageKey = 'coachhub.theme';
  readonly theme = signal<Theme>(this.restoreTheme());
  constructor(@Inject(DOCUMENT) private readonly document: Document) { this.apply(this.theme()); }
  toggle(): void { this.set(this.theme() === 'light' ? 'dark' : 'light'); }
  set(theme: Theme): void {
    this.theme.set(theme);
    try { localStorage.setItem(this.storageKey, theme); } catch { /* Not persisted. */ }
    this.apply(theme);
  }

  /**
   * Overrides the brand tokens every feature uses (--brand, --brand-dark, --brand-soft,
   * --brand-secondary). Invalid colors are ignored so a bad value can never break the page.
   */
  applyBrand(primary: string, secondary: string): void {
    const style = this.document.documentElement.style;
    if (isHexColor(primary)) {
      style.setProperty('--brand', primary);
      style.setProperty('--brand-dark', mix(primary, '#000000', 0.15));
      style.setProperty('--brand-soft', mix(primary, '#ffffff', 0.9));
    }
    if (isHexColor(secondary)) style.setProperty('--brand-secondary', secondary);
  }

  /** The browser tab: brand name as title, brand favicon when there is one. */
  applyIdentity(title: string, faviconUrl: string | null): void {
    this.document.title = title;
    let link = this.document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (!faviconUrl) return;
    if (!link) {
      link = this.document.createElement('link');
      link.rel = 'icon';
      this.document.head.appendChild(link);
    }
    link.href = faviconUrl;
  }

  private restoreTheme(): Theme {
    let stored: string | null = null;
    try { stored = localStorage.getItem(this.storageKey); } catch { /* Unavailable. */ }
    if (stored === 'light' || stored === 'dark') return stored;
    return typeof matchMedia === 'function' && matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  private apply(theme: Theme): void { this.document.documentElement.classList.toggle('dark', theme === 'dark'); this.document.documentElement.style.colorScheme = theme; }
}

export function isHexColor(value: string | null | undefined): value is string {
  return !!value && /^#[0-9a-fA-F]{6}$/.test(value);
}

/** Mixes two #rrggbb colors; weight is the share of the second color (0–1). */
export function mix(color: string, other: string, weight: number): string {
  const channel = (hex: string, index: number) => parseInt(hex.slice(1 + index * 2, 3 + index * 2), 16);
  const parts = [0, 1, 2].map(index =>
    Math.round(channel(color, index) * (1 - weight) + channel(other, index) * weight).toString(16).padStart(2, '0'));
  return `#${parts.join('')}`;
}
