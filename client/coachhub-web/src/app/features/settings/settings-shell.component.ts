import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';

/** One settings area (P18A): branding, coaching defaults and platform settings, then the reference data. */
@Component({
  selector: 'app-settings-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, ...I18N_PIPES],
  template: `
    <nav class="resource-tabs settings-tabs" [attr.aria-label]="'settings.sections' | translate">
      <a routerLink="branding" routerLinkActive="active">{{ 'settings.branding' | translate }}</a>
      <a routerLink="reference-data" routerLinkActive="active">{{ 'settings.reference' | translate }}</a>
    </nav>
    <router-outlet />
  `,
})
export class SettingsShellComponent {}
