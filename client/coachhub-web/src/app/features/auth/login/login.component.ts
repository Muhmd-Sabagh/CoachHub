import { Component, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { BrandingService, brandInitials } from '../../../core/branding/branding.service';
import { I18nService } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-login', templateUrl: './login.component.html', standalone: false })
export class LoginComponent {
  showPassword = false;
  readonly submitting = signal(false);
  readonly failed = signal(false);
  readonly form;
  constructor(fb: FormBuilder, private readonly auth: AuthService, private readonly route: ActivatedRoute, private readonly router: Router, readonly i18n: I18nService, readonly branding: BrandingService) {
    this.form = fb.nonNullable.group({ login: ['', Validators.required], password: ['', Validators.required] });
    if (auth.isAuthenticated()) void router.navigateByUrl(auth.landingRoute());
  }
  readonly initials = brandInitials;
  submit(): void {
    if (this.form.invalid || this.submitting()) { this.form.markAllAsTouched(); return; }
    this.submitting.set(true); this.failed.set(false);
    this.auth.login(this.form.getRawValue()).pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: result => void this.router.navigateByUrl(this.route.snapshot.queryParamMap.get('returnUrl') || this.auth.landingRoute()),
      error: () => this.failed.set(true)
    });
  }
}