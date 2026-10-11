import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { roleGuard } from './core/auth/auth.guard';
import { ShellComponent } from './core/layout/shell.component';
import { AssessmentsComponent } from './features/assessments/assessments.component';
import { LoginComponent } from './features/auth/login/login.component';
import { CatalogComponent } from './features/catalog/catalog.component';
import { ClientsComponent } from './features/clients/clients.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { PlansComponent } from './features/plans/plans.component';
import { SettingsComponent } from './features/settings/settings.component';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'forgot-password', loadComponent: () => import('./features/auth/password-recovery.component').then(m => m.PasswordRecoveryComponent) },
  { path: 'reset-password', loadComponent: () => import('./features/auth/password-recovery.component').then(m => m.PasswordRecoveryComponent) },
  { path: 'portal', canActivate: [roleGuard('Client')], loadComponent: () => import('./features/client-portal/client-portal.component').then(m => m.ClientPortalComponent) },
  {
    path: 'assessment',
    loadComponent: () =>
      import('./features/public-assessment/client-assessment.component').then(
        (module) => module.ClientAssessmentComponent,
      ),
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [roleGuard('Administrator', 'Coach')],
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'clients', component: ClientsComponent },
      { path: 'subscriptions', component: ClientsComponent, data: { workspace: 'subscriptions' } },
      { path: 'assessments', component: AssessmentsComponent },
      { path: 'nutrition', component: CatalogComponent, data: { kind: 'foods' } },
      { path: 'training', component: CatalogComponent, data: { kind: 'exercises' } },
      { path: 'plans', component: PlansComponent },
      { path: 'coaches', canActivate: [roleGuard('Administrator')], loadComponent: () => import('./features/coaches/coaches.component').then(m => m.CoachesComponent) },
      { path: 'operations', loadComponent: () => import('./features/operations/operations.component').then(m => m.OperationsComponent) },
      {
        path: 'audit',
        canActivate: [roleGuard('Administrator')],
        loadComponent: () =>
          import('./features/auditing/audit.component').then((module) => module.AuditComponent),
      },
      { path: 'settings', component: SettingsComponent },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    ],
  },
  { path: '**', redirectTo: '' },
];
@NgModule({
  imports: [RouterModule.forRoot(routes, { scrollPositionRestoration: 'enabled' })],
  exports: [RouterModule],
})
export class AppRoutingModule {}
