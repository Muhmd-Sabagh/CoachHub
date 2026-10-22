import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { BrandingService, brandInitials } from '../branding/branding.service';
import { AppConfigService } from '../config/app-config.service';
import { I18nService } from '../i18n/i18n.service';
import { ThemeService } from '../theme/theme.service';

import { Role } from '../auth/auth.models';

interface NavItem { route: string; label: string; icon: string; roles: Role[]; }
const coachOrAdmin: Role[] = ['Administrator', 'Coach'];
@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager, selector: 'app-shell', templateUrl: './shell.component.html', standalone: false })
export class ShellComponent implements OnInit {
  sidebarOpen = false;
  private readonly allNavItems: NavItem[] = [
    { route: '/dashboard', label: 'nav.dashboard', icon: 'DB', roles: coachOrAdmin },
    { route: '/coaches', label: 'nav.coaches', icon: 'CO', roles: ['Administrator'] },
    { route: '/clients', label: 'nav.clients', icon: 'CL', roles: coachOrAdmin },
    { route: '/subscriptions', label: 'nav.subscriptions', icon: 'SB', roles: coachOrAdmin },
    { route: '/assessments', label: 'nav.assessments', icon: 'AS', roles: coachOrAdmin },
    { route: '/calendar', label: 'nav.calendar', icon: 'CA', roles: coachOrAdmin },
    { route: '/messages', label: 'nav.messages', icon: 'MS', roles: coachOrAdmin },
    { route: '/check-ins', label: 'nav.checkIns', icon: 'CI', roles: coachOrAdmin },
    { route: '/fitness-tests', label: 'nav.fitnessTests', icon: 'FT', roles: coachOrAdmin },
    { route: '/nutrition', label: 'nav.nutrition', icon: 'NU', roles: coachOrAdmin },
    { route: '/training', label: 'nav.training', icon: 'TR', roles: coachOrAdmin },
    { route: '/plans', label: 'nav.plans', icon: 'PL', roles: coachOrAdmin },
    { route: '/operations', label: 'nav.operations', icon: 'OP', roles: coachOrAdmin },
    { route: '/audit', label: 'nav.audit', icon: 'AU', roles: coachOrAdmin },
    { route: '/settings', label: 'nav.settings', icon: 'ST', roles: coachOrAdmin }
  ];
  get navItems(): NavItem[] {
    return this.allNavItems.filter(item => item.roles.some(role => this.auth.hasRole(role)));
  }
  constructor(readonly auth: AuthService, readonly config: AppConfigService, readonly i18n: I18nService, readonly theme: ThemeService, readonly branding: BrandingService) {}
  readonly initials = brandInitials;
  ngOnInit(): void { this.config.load().subscribe(); }
  closeSidebar(): void { this.sidebarOpen = false; }
}
