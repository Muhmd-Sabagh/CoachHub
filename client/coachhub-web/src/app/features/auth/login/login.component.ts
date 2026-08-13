import { Component } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';

@Component({ selector: 'app-login', templateUrl: './login.component.html', standalone: false })
export class LoginComponent {
  showPassword = false;
  submitting = false;
  failed = false;
  readonly form;
  constructor(fb: FormBuilder, private readonly auth: AuthService, private readonly route: ActivatedRoute, private readonly router: Router, readonly i18n: I18nService) {
    this.form = fb.nonNullable.group({ email: ['', [Validators.required, Validators.email]], password: ['', Validators.required] });
    if (auth.isAuthenticated()) void router.navigate(['/dashboard']);
  }
  submit(): void {
    if (this.form.invalid || this.submitting) { this.form.markAllAsTouched(); return; }
    this.submitting = true; this.failed = false;
    this.auth.login(this.form.getRawValue()).pipe(finalize(() => this.submitting = false)).subscribe({
      next: () => void this.router.navigateByUrl(this.route.snapshot.queryParamMap.get('returnUrl') || '/dashboard'),
      error: () => this.failed = true
    });
  }
}