import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { MyCheckInsComponent } from '../check-ins/my-check-ins.component';
import { MyForms, MyFormsService } from './my-forms.service';

/** The forms the coach asked the client to fill in: assessments, and check-ins while one is open (P12). */
@Component({
  selector: 'app-my-forms',
  standalone: true,
  imports: [RouterLink, MyCheckInsComponent],
  template: `
    <section class="dashboard-hero"><div><p class="eyebrow">Forms</p><h1>Your forms</h1>
      <p>Answer what your coach asked for. Your answers go straight to them.</p></div></section>
    <app-my-check-ins />
    @if (error) { <p class="form-error" role="alert">{{ error }}</p> }
    @if (loading) { <p class="table-message">Loading…</p> }
    @if (forms; as list) {
      <ul class="form-list">
        @for (form of list.eligibleForms; track form.id) {
          <li><a class="card workbench" [routerLink]="['/app/forms', form.id]">
            <strong>{{ form.name }}</strong>
            <small class="muted">{{ form.formType === 'InitialAssessment' ? 'Starting assessment' : form.formType === 'CheckIn' ? 'Check-in' : 'Progress update' }}</small>
          </a></li>
        } @empty {
          <li class="muted">Nothing to fill in right now.</li>
        }
      </ul>
    }
  `,
  styles: [`.form-list { list-style: none; padding: 0; margin: 0; display: grid; gap: 12px; }
    .form-list a { display: grid; gap: 4px; min-height: 64px; }`],
})
export class MyFormsComponent implements OnInit {
  forms: MyForms | null = null;
  loading = false;
  error = '';
  constructor(private readonly api: MyFormsService) {}
  ngOnInit(): void {
    this.loading = true;
    this.api.list().pipe(finalize(() => (this.loading = false))).subscribe({
      next: forms => (this.forms = forms),
      error: e => (this.error = apiErrorMessage(e)),
    });
  }
}
