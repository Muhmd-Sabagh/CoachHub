import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { BrowserModule } from '@angular/platform-browser';
import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { authInterceptor } from './core/auth/auth.interceptor';
import { ShellComponent } from './core/layout/shell.component';
import { AssessmentsComponent } from './features/assessments/assessments.component';
import { LoginComponent } from './features/auth/login/login.component';
import { CatalogComponent } from './features/catalog/catalog.component';
import { ClientsComponent } from './features/clients/clients.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { PlansComponent } from './features/plans/plans.component';
import { SettingsComponent } from './features/settings/settings.component';
import { PaginationComponent } from './shared/components/pagination/pagination.component';
import { SearchFilterComponent } from './shared/components/search-filter/search-filter.component';
import { StatusBadgeComponent } from './shared/components/status-badge/status-badge.component';
import { TranslatePipe } from './shared/pipes/translate.pipe';
@NgModule({ declarations: [App, ShellComponent, LoginComponent, DashboardComponent, SettingsComponent, CatalogComponent, ClientsComponent, AssessmentsComponent, PlansComponent, PaginationComponent, SearchFilterComponent, StatusBadgeComponent, TranslatePipe], imports: [BrowserModule, FormsModule, ReactiveFormsModule, AppRoutingModule], providers: [provideBrowserGlobalErrorListeners(), provideHttpClient(withInterceptors([authInterceptor]))], bootstrap: [App] })
export class AppModule {}