import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';

@Component({ selector: 'app-password-recovery', standalone: true, imports: [CommonModule, FormsModule, RouterLink], template: `<main class="auth-page"><section class="auth-brand-panel"><div class="brand-mark large">CH</div><p class="eyebrow">Secure account recovery</p><h1>CoachHub</h1><p>Reset links are single-use and delivered to the account email.</p></section><section class="auth-form-panel"><form class="auth-card" (ngSubmit)="submit()"><h2>{{ token ? 'Choose a new password' : 'Forgot password' }}</h2><label><span>Email</span><input type="email" name="email" [(ngModel)]="email" required /></label>@if (token) { <label><span>New password</span><input type="password" name="password" [(ngModel)]="password" minlength="12" required /></label> } @if (message) { <p class="success-message">{{ message }}</p> } @if (failed) { <p class="form-error">The request could not be completed.</p> }<button class="button button-primary button-block">{{ token ? 'Reset password' : 'Send reset link' }}</button><a routerLink="/login">Return to sign in</a></form></section></main>` })
export class PasswordRecoveryComponent {
  email = ''; password = ''; token = ''; message = ''; failed = false;
  constructor(private readonly http: HttpClient, route: ActivatedRoute) { this.email = route.snapshot.queryParamMap.get('email') || ''; this.token = route.snapshot.queryParamMap.get('token') || ''; }
  submit() { this.failed = false; const url = `${environment.apiBaseUrl}/auth/password-reset/${this.token ? 'complete' : 'request'}`; const body = this.token ? { email: this.email, token: this.token, newPassword: this.password } : { email: this.email }; this.http.post(url, body).subscribe({ next: () => this.message = this.token ? 'Password updated. You can sign in now.' : 'If the account exists, a reset link has been scheduled.', error: () => this.failed = true }); }
}
