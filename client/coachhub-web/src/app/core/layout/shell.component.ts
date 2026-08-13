import { Component, OnInit } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { AppConfigService } from '../config/app-config.service';
import { I18nService } from '../i18n/i18n.service';
import { ThemeService } from '../theme/theme.service';

interface NavItem { route: string; label: string; icon: string; }
@Component({ selector: 'app-shell', templateUrl: './shell.component.html', standalone: false })
export class ShellComponent implements OnInit {
  sidebarOpen = false;
  readonly navItems: NavItem[] = [
    { route: '/dashboard', label: 'nav.dashboard', icon: 'DB' }, { route: '/clients', label: 'nav.clients', icon: 'CL' },
    { route: '/subscriptions', label: 'nav.subscriptions', icon: 'SB' }, { route: '/assessments', label: 'nav.assessments', icon: 'AS' },
    { route: '/nutrition', label: 'nav.nutrition', icon: 'NU' }, { route: '/training', label: 'nav.training', icon: 'TR' },
    { route: '/plans', label: 'nav.plans', icon: 'PL' }, { route: '/settings', label: 'nav.settings', icon: 'ST' }
  ];
  constructor(readonly auth: AuthService, readonly config: AppConfigService, readonly i18n: I18nService, readonly theme: ThemeService) {}
  ngOnInit(): void { this.config.load().subscribe(); }
  closeSidebar(): void { this.sidebarOpen = false; }
}