import { Component, OnInit } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { BrandingService, brandInitials } from '../branding/branding.service';
import { AppConfigService } from '../config/app-config.service';
import { I18nService } from '../i18n/i18n.service';
import { ThemeService } from '../theme/theme.service';

import { Role } from '../auth/auth.models';

interface NavItem { route: string; label: string; icon: string; roles: Role[]; }
const staff: Role[] = ['Administrator', 'Coach'];
@Component({ selector: 'app-shell', templateUrl: './shell.component.html', standalone: false })
export class ShellComponent implements OnInit {
  sidebarOpen = false;
  private readonly allNavItems: NavItem[] = [
    { route: '/dashboard', label: 'nav.dashboard', icon: 'DB', roles: staff },
    { route: '/coaches', label: 'nav.coaches', icon: 'CO', roles: ['Administrator'] },
    { route: '/clients', label: 'nav.clients', icon: 'CL', roles: staff },
    { route: '/subscriptions', label: 'nav.subscriptions', icon: 'SB', roles: staff },
    { route: '/assessments', label: 'nav.assessments', icon: 'AS', roles: staff },
    { route: '/check-ins', label: 'nav.checkIns', icon: 'CI', roles: staff },
    { route: '/fitness-tests', label: 'nav.fitnessTests', icon: 'FT', roles: staff },
    { route: '/nutrition', label: 'nav.nutrition', icon: 'NU', roles: staff },
    { route: '/training', label: 'nav.training', icon: 'TR', roles: staff },
    { route: '/plans', label: 'nav.plans', icon: 'PL', roles: staff },
    { route: '/operations', label: 'nav.operations', icon: 'OP', roles: staff },
    { route: '/audit', label: 'nav.audit', icon: 'AU', roles: staff },
    { route: '/settings', label: 'nav.settings', icon: 'ST', roles: staff },
    { route: '/workspace', label: 'nav.workspace', icon: 'BR', roles: staff }
  ];
  get navItems(): NavItem[] {
    return this.allNavItems.filter(item => item.roles.some(role => this.auth.hasRole(role)));
  }
  constructor(readonly auth: AuthService, readonly config: AppConfigService, readonly i18n: I18nService, readonly theme: ThemeService, readonly branding: BrandingService) {}
  readonly initials = brandInitials;
  ngOnInit(): void { this.config.load().subscribe(); }
  closeSidebar(): void { this.sidebarOpen = false; }
}
