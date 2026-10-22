import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** One settings area (P18A): branding, coaching defaults and platform settings, then the reference data. */
@Component({
  selector: 'app-settings-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <nav class="resource-tabs settings-tabs" aria-label="Settings sections">
      <a routerLink="branding" routerLinkActive="active">Branding &amp; defaults</a>
      <a routerLink="reference-data" routerLinkActive="active">Reference data</a>
    </nav>
    <router-outlet />
  `,
})
export class SettingsShellComponent {}
