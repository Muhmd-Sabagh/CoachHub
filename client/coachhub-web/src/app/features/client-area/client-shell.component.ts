import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { BrandingService, brandInitials } from '../../core/branding/branding.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { NotificationBellComponent } from '../notifications/notification-bell.component';
import { AccessBannerComponent } from './access-banner.component';

/**
 * The client area (§53): mobile first, branded with the client's coach (§63), with a bottom
 * navigation that thumbs can reach. Separate from the staff shell.
 */
@Component({
  selector: 'app-client-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NotificationBellComponent, AccessBannerComponent],
  template: `
    <div class="client-shell">
      <header class="client-topbar">
        @if (branding.brand().logoUrl; as logo) { <img class="brand-logo" [src]="logo" [alt]="branding.brand().brandName" /> }
        @else { <span class="brand-mark">{{ initials(branding.brand().brandName) }}</span> }
        <strong>{{ branding.brand().brandName }}</strong>
        <span class="topbar-spacer"></span>
        <app-notification-bell />
        <a class="icon-link" routerLink="/app/account" aria-label="Account">☺</a>
        <button class="language-button" type="button" (click)="i18n.toggle()">{{ i18n.language() === 'en' ? 'ع' : 'EN' }}</button>
        <button class="link-button" type="button" (click)="auth.logout()">Sign out</button>
      </header>
      <main class="client-content"><app-access-banner /><router-outlet /></main>
      <nav class="client-tabbar" aria-label="Client navigation">
        <a routerLink="/app" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }"><span aria-hidden="true">⌂</span>Home</a>
        <a routerLink="/app/training" routerLinkActive="active"><span aria-hidden="true">⚑</span>Training</a>
        <a routerLink="/app/nutrition" routerLinkActive="active"><span aria-hidden="true">◔</span>Nutrition</a>
        <a routerLink="/app/progress" routerLinkActive="active"><span aria-hidden="true">↗</span>Progress</a>
        <a routerLink="/app/forms" routerLinkActive="active"><span aria-hidden="true">✎</span>Forms</a>
        <a routerLink="/app/chat" routerLinkActive="active"><span aria-hidden="true">✉</span>Chat</a>
      </nav>
    </div>
  `,
  styles: [`
    .client-shell { min-height: 100dvh; display: flex; flex-direction: column; background: var(--gray-50); }
    .client-topbar { position: sticky; top: 0; z-index: 10; display: flex; align-items: center; gap: 10px;
      padding: 10px 16px; background: white; border-bottom: 1px solid var(--gray-200); }
    .topbar-spacer { flex: 1; }
    .icon-link { display: grid; place-items: center; min-width: 40px; min-height: 40px; font-size: 1.2rem; color: inherit; }
    .client-content { flex: 1; width: 100%; max-width: 960px; margin: 0 auto; padding: 16px 16px 96px; }
    .client-tabbar { position: fixed; inset: auto 0 0 0; z-index: 10; display: grid; grid-template-columns: repeat(6, 1fr);
      background: white; border-top: 1px solid var(--gray-200); padding-bottom: env(safe-area-inset-bottom); }
    .client-tabbar a { display: flex; flex-direction: column; align-items: center; gap: 2px; padding: 10px 4px;
      min-height: 56px; color: var(--gray-500); font-size: 0.8rem; }
    .client-tabbar a span { font-size: 1.25rem; line-height: 1; }
    .client-tabbar a.active { color: var(--brand); font-weight: 600; }
    @media (min-width: 761px) { .client-tabbar { inset: auto 0 0 0; max-width: 480px; margin: 0 auto; border-radius: 12px 12px 0 0; } }
  `],
})
export class ClientShellComponent {
  readonly initials = brandInitials;
  constructor(readonly auth: AuthService, readonly branding: BrandingService, readonly i18n: I18nService) {}
}
