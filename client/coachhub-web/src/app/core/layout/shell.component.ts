import { Component, OnInit } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { AppConfigService } from '../config/app-config.service';
import { I18nService } from '../i18n/i18n.service';
import { ThemeService } from '../theme/theme.service';

interface NavItem { route: string; label: string; icon: string; permissions?: string[]; }
@Component({ selector: 'app-shell', templateUrl: './shell.component.html', standalone: false })
export class ShellComponent implements OnInit {
  sidebarOpen = false;
  private readonly allNavItems: NavItem[] = [
    { route: '/dashboard', label: 'nav.dashboard', icon: 'DB', permissions: ['reports.view'] },
    { route: '/clients', label: 'nav.clients', icon: 'CL', permissions: ['clients.manage'] },
    { route: '/subscriptions', label: 'nav.subscriptions', icon: 'SB', permissions: ['clients.manage'] },
    { route: '/assessments', label: 'nav.assessments', icon: 'AS', permissions: ['assessments.manage'] },
    { route: '/nutrition', label: 'nav.nutrition', icon: 'NU', permissions: ['catalog.manage'] },
    { route: '/training', label: 'nav.training', icon: 'TR', permissions: ['catalog.manage'] },
    { route: '/plans', label: 'nav.plans', icon: 'PL', permissions: ['plans.manage'] },
    { route: '/operations', label: 'nav.operations', icon: 'OP', permissions: ['users.manage', 'billing.manage', 'communications.manage', 'plans.manage'] },
    { route: '/audit', label: 'nav.audit', icon: 'AU', permissions: ['audit.view'] },
    { route: '/settings', label: 'nav.settings', icon: 'ST', permissions: ['settings.manage'] }
  ];
  get navItems(): NavItem[] {
    const user = this.auth.currentUser();
    if (user?.roles.includes('Administrator')) return this.allNavItems;
    return this.allNavItems.filter(item => item.permissions?.some(permission => user?.permissions?.includes(permission)));
  }
  constructor(readonly auth: AuthService, readonly config: AppConfigService, readonly i18n: I18nService, readonly theme: ThemeService) {}
  ngOnInit(): void { this.config.load().subscribe(); }
  closeSidebar(): void { this.sidebarOpen = false; }
}
