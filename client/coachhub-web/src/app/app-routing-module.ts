import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { ShellComponent } from './core/layout/shell.component';
import { LoginComponent } from './features/auth/login/login.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { FeaturePlaceholderComponent } from './features/placeholder/feature-placeholder.component';

const feature = (path: string, titleKey: string) => ({ path, component: FeaturePlaceholderComponent, data: { titleKey } });
const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', component: ShellComponent, canActivate: [authGuard], children: [
    { path: 'dashboard', component: DashboardComponent }, feature('clients', 'nav.clients'), feature('subscriptions', 'nav.subscriptions'),
    feature('assessments', 'nav.assessments'), feature('nutrition', 'nav.nutrition'), feature('training', 'nav.training'),
    feature('plans', 'nav.plans'), feature('settings', 'nav.settings'), { path: '', pathMatch: 'full', redirectTo: 'dashboard' }
  ]},
  { path: '**', redirectTo: '' }
];

@NgModule({ imports: [RouterModule.forRoot(routes, { scrollPositionRestoration: 'enabled' })], exports: [RouterModule] })
export class AppRoutingModule {}