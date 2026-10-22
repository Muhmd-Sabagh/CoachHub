import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { NgModule, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { BrowserModule } from '@angular/platform-browser';
import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { firstValueFrom } from 'rxjs';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { BrandingService } from './core/branding/branding.service';
import { ShellComponent } from './core/layout/shell.component';
import { AssessmentsComponent } from './features/assessments/assessments.component';
import { LoginComponent } from './features/auth/login/login.component';
import { CatalogComponent } from './features/catalog/catalog.component';
import { ClientsComponent } from './features/clients/clients.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { PlansComponent } from './features/plans/plans.component';
import { ReferenceDataComponent } from './features/settings/reference-data.component';
import { PaginationComponent } from './shared/components/pagination/pagination.component';
import { SearchFilterComponent } from './shared/components/search-filter/search-filter.component';
import { StatusBadgeComponent } from './shared/components/status-badge/status-badge.component';
import { TranslatePipe } from './shared/pipes/translate.pipe';
import { NotificationBellComponent } from './features/notifications/notification-bell.component';
import { viewRefreshInterceptor } from './core/http/view-refresh.interceptor';
@NgModule({
  declarations: [
    App,
    ShellComponent,
    LoginComponent,
    DashboardComponent,
    ReferenceDataComponent,
    CatalogComponent,
    ClientsComponent,
    AssessmentsComponent,
    PlansComponent,
    PaginationComponent,
    SearchFilterComponent,
    StatusBadgeComponent,
    TranslatePipe,
  ],
  imports: [BrowserModule, FormsModule, ReactiveFormsModule, AppRoutingModule, NotificationBellComponent],
  providers: [
    provideBrowserGlobalErrorListeners(),
    // The view refresh runs outermost, after each response's own handlers (P10A: zoneless screens).
    provideHttpClient(withInterceptors([viewRefreshInterceptor, authInterceptor])),
    // A new tab, or a reload after the access token expired, signs in again from the refresh cookie.
    // Then the brand: the user's coach once signed in, otherwise the host's (D1, §63).
    provideAppInitializer(() => {
      const auth = inject(AuthService);
      const branding = inject(BrandingService);
      return firstValueFrom(auth.ensureSession()).then(() => firstValueFrom(branding.load()));
    }),
  ],
  bootstrap: [App],
})
export class AppModule {}
