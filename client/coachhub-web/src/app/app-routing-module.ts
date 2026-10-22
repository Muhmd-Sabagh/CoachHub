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
import { ReferenceDataComponent } from './features/settings/reference-data.component';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'forgot-password', loadComponent: () => import('./features/auth/password-recovery.component').then(m => m.PasswordRecoveryComponent) },
  { path: 'reset-password', loadComponent: () => import('./features/auth/password-recovery.component').then(m => m.PasswordRecoveryComponent) },
  // The client area (§53 mobile first): its own shell with a bottom navigation.
  {
    path: 'app',
    canActivate: [roleGuard('Client')],
    loadComponent: () => import('./features/client-area/client-shell.component').then(m => m.ClientShellComponent),
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./features/client-area/my-home.component').then(m => m.MyHomeComponent) },
      { path: 'nutrition', loadComponent: () => import('./features/nutrition-tracking/my-nutrition.component').then(m => m.MyNutritionComponent) },
      { path: 'training', loadComponent: () => import('./features/training-tracking/my-training.component').then(m => m.MyTrainingComponent) },
      { path: 'progress', loadComponent: () => import('./features/progress-tracking/my-progress.component').then(m => m.MyProgressComponent) },
      { path: 'calendar', loadComponent: () => import('./features/calendar/my-calendar.component').then(m => m.MyCalendarComponent) },
      { path: 'chat', loadComponent: () => import('./features/chat/my-chat.component').then(m => m.MyChatComponent) },
      { path: 'files', loadComponent: () => import('./features/client-files/client-files.component').then(m => m.ClientFilesComponent) },
      { path: 'tests', loadComponent: () => import('./features/fitness-tests/my-tests.component').then(m => m.MyTestsComponent) },
      { path: 'forms', loadComponent: () => import('./features/client-area/my-forms.component').then(m => m.MyFormsComponent) },
      { path: 'forms/:id', loadComponent: () => import('./features/client-area/my-form.component').then(m => m.MyFormComponent) },
      { path: 'account', loadComponent: () => import('./features/account/account.component').then(m => m.AccountComponent) },
    ],
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [roleGuard('Administrator', 'Coach')],
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'clients', component: ClientsComponent },
      { path: 'clients/:id', loadComponent: () => import('./features/client-profile/client-profile.component').then(m => m.ClientProfileComponent) },
      { path: 'subscriptions', component: ClientsComponent, data: { workspace: 'subscriptions' } },
      { path: 'assessments', component: AssessmentsComponent },
      { path: 'fitness-tests', loadComponent: () => import('./features/fitness-tests/test-library.component').then(m => m.TestLibraryComponent) },
      { path: 'calendar', loadComponent: () => import('./features/calendar/coach-calendar.component').then(m => m.CoachCalendarComponent) },
      { path: 'messages', loadComponent: () => import('./features/chat/messages.component').then(m => m.MessagesComponent) },
      { path: 'check-ins', loadComponent: () => import('./features/check-ins/check-ins.component').then(m => m.CheckInsComponent) },
      { path: 'nutrition', component: CatalogComponent, data: { kind: 'foods' } },
      { path: 'training', component: CatalogComponent, data: { kind: 'exercises' } },
      { path: 'plans', component: PlansComponent },
      { path: 'coaches', canActivate: [roleGuard('Administrator')], loadComponent: () => import('./features/coaches/coaches.component').then(m => m.CoachesComponent) },
      { path: 'operations', loadComponent: () => import('./features/operations/operations.component').then(m => m.OperationsComponent) },
      {
        path: 'audit',
        loadComponent: () =>
          import('./features/auditing/audit.component').then((module) => module.AuditComponent),
      },
      {
        path: 'settings',
        loadComponent: () => import('./features/settings/settings-shell.component').then(m => m.SettingsShellComponent),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'branding' },
          { path: 'branding', loadComponent: () => import('./features/settings/branding-defaults.component').then(m => m.BrandingDefaultsComponent) },
          { path: 'reference-data', component: ReferenceDataComponent },
        ],
      },
      { path: 'workspace', redirectTo: 'settings/branding' },
      { path: 'account', loadComponent: () => import('./features/account/account.component').then(m => m.AccountComponent) },
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
