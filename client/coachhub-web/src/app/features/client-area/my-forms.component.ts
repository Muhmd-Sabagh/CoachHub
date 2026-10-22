import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { MyCheckInsComponent } from '../check-ins/my-check-ins.component';
import { MyForms, MyFormsService } from './my-forms.service';

/** The forms the coach asked the client to fill in: assessments, and check-ins while one is open (P12). */
@Component({
  selector: 'app-my-forms',
  standalone: true,
  imports: [RouterLink, MyCheckInsComponent, TranslatePipe],
  template: `
    <section class="dashboard-hero"><div><p class="eyebrow">{{ 'client.forms.eyebrow' | translate }}</p><h1>{{ 'client.forms.title' | translate }}</h1>
      <p>{{ 'client.forms.intro' | translate }}</p></div></section>
    <app-my-check-ins />
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (loading()) { <p class="table-message">{{ 'common.loading' | translate }}</p> }
    @if (forms(); as list) {
      <ul class="form-list">
        @for (form of list.eligibleForms; track form.id) {
          <li><a class="card workbench" [routerLink]="['/app/forms', form.id]">
            <strong translate="no">{{ form.name }}</strong>
            <small class="muted">{{ (form.formType === 'InitialAssessment' ? 'client.forms.initial' : form.formType === 'CheckIn' ? 'client.forms.checkIn' : 'client.forms.update') | translate }}</small>
          </a></li>
        } @empty {
          <li class="muted">{{ 'client.forms.nothing' | translate }}</li>
        }
      </ul>
    }
  `,
  styles: [`.form-list { list-style: none; padding: 0; margin: 0; display: grid; gap: 12px; }
    .form-list a { display: grid; gap: 4px; min-height: 64px; }`],
})
export class MyFormsComponent implements OnInit {
  readonly forms = signal<MyForms | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  constructor(private readonly api: MyFormsService) {}
  ngOnInit(): void {
    this.loading.set(true);
    this.api.list().subscribe({
      next: forms => { this.forms.set(forms); this.loading.set(false); },
      error: e => { this.error.set(apiErrorMessage(e)); this.loading.set(false); },
    });
  }
}
