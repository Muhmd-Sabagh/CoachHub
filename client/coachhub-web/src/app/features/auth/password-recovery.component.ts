import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { BrandingService, brandInitials } from '../../core/branding/branding.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-password-recovery', standalone: true, imports: [CommonModule, FormsModule, RouterLink, TranslatePipe], template: `<main class="auth-page"><section class="auth-brand-panel">@if (branding.brand().logoUrl; as logo) { <img class="brand-logo large" [src]="logo" [alt]="branding.brand().brandName" /> } @else { <div class="brand-mark large">{{ initials(branding.brand().brandName) }}</div> }<p class="eyebrow">{{ 'auth.recoveryEyebrow' | translate }}</p><h1 translate="no">{{ branding.brand().brandName }}</h1><p>{{ 'auth.recoveryIntro' | translate }}</p></section><section class="auth-form-panel"><form class="auth-card" (ngSubmit)="submit()"><button class="language-button" type="button" (click)="i18n.toggle()">{{ 'shell.language' | translate }}</button><h2>{{ (token ? 'auth.choosePassword' : 'auth.forgotTitle') | translate }}</h2><label><span>{{ 'auth.email' | translate }}</span><input type="email" name="email" [(ngModel)]="email" required /></label>@if (token) { <label><span>{{ 'auth.newPassword' | translate }}</span><input type="password" name="password" [(ngModel)]="password" minlength="12" required /></label> } @if (message()) { <p class="success-message">{{ message() }}</p> } @if (failed()) { <p class="form-error">{{ 'auth.requestFailed' | translate }}</p> }<button class="button button-primary button-block">{{ (token ? 'auth.resetPassword' : 'auth.sendLink') | translate }}</button><a routerLink="/login">{{ 'auth.backToSignIn' | translate }}</a></form></section></main>` })
export class PasswordRecoveryComponent {
  email = ''; password = ''; token = '';
  readonly message = signal('');
  readonly failed = signal(false);
  readonly initials = brandInitials;
  constructor(private readonly http: HttpClient, route: ActivatedRoute, readonly branding: BrandingService, readonly i18n: I18nService) { this.email = route.snapshot.queryParamMap.get('email') || ''; this.token = route.snapshot.queryParamMap.get('token') || ''; }
  submit() { this.failed.set(false); const url = `${environment.apiBaseUrl}/auth/password-reset/${this.token ? 'complete' : 'request'}`; const body = this.token ? { email: this.email, token: this.token, newPassword: this.password } : { email: this.email }; this.http.post(url, body).subscribe({ next: () => this.message.set(this.i18n.translate(this.token ? 'auth.passwordUpdated' : 'auth.linkScheduled')), error: () => this.failed.set(true) }); }
}
