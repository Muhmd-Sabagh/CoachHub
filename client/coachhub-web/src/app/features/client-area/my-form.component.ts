import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { FormQuestion, MyFormsService, PublishedForm, UploadedMedia, hasAnswer } from './my-forms.service';

/** Fill in one form, signed in (D5). One question per row, large controls for phones. */
@Component({
  selector: 'app-my-form',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <a class="link-button" routerLink="/app/forms">← All forms</a>
    @if (error) { <p class="form-error" role="alert">{{ error }}</p> }
    @if (done) {
      <section class="card workbench" role="status"><h1>Thank you</h1><p>Your coach has your answers.</p>
        <a class="button button-primary" routerLink="/app">Back home</a></section>
    } @else if (form; as f) {
      <section class="dashboard-hero"><div><p class="eyebrow">{{ progress() }}% answered</p><h1>{{ f.name }}</h1></div></section>
      <form class="question-list" (ngSubmit)="submit()">
        @for (question of ordered(); track question.id) {
          <fieldset class="card workbench" [id]="'question-' + question.id" [class.invalid]="touched.has(question.id) && invalid(question)">
            <legend><strong>{{ question.text }}</strong>@if (question.isRequired) { <span aria-label="required"> *</span> }</legend>
            @switch (question.questionType) {
              @case ('ShortText') { <input [name]="question.id" [(ngModel)]="answers[question.id]" maxlength="1000" (blur)="touch(question.id)" /> }
              @case ('LongText') { <textarea [name]="question.id" rows="4" [(ngModel)]="answers[question.id]" maxlength="10000" (blur)="touch(question.id)"></textarea> }
              @case ('Number') { <input type="number" inputmode="decimal" [name]="question.id" [(ngModel)]="answers[question.id]" (blur)="touch(question.id)" /> }
              @case ('Date') { <input type="date" [name]="question.id" [(ngModel)]="answers[question.id]" (blur)="touch(question.id)" /> }
              @case ('Boolean') {
                <div class="choice-row">
                  <label><input type="radio" [name]="question.id" [value]="true" [(ngModel)]="answers[question.id]" (change)="touch(question.id)" /> Yes</label>
                  <label><input type="radio" [name]="question.id" [value]="false" [(ngModel)]="answers[question.id]" (change)="touch(question.id)" /> No</label>
                </div>
              }
              @case ('SingleChoice') {
                <div class="choice-list">@for (option of question.options; track option.id) {
                  <label><input type="radio" [name]="question.id" [value]="option.value" [(ngModel)]="answers[question.id]" (change)="touch(question.id)" /> {{ option.label }}</label>
                }</div>
              }
              @case ('MultipleChoice') {
                <div class="choice-list">@for (option of question.options; track option.id) {
                  <label><input type="checkbox" [checked]="isChosen(question.id, option.value)" (change)="toggle(question.id, option.value, $any($event.target).checked)" /> {{ option.label }}</label>
                }</div>
              }
              @case ('MediaUpload') {
                <label class="button button-secondary">{{ media[question.id] ? 'Replace photo' : 'Add photo or file' }}
                  <input type="file" accept="image/*,application/pdf" hidden (change)="upload(question, $event)" /></label>
                @if (uploading[question.id]) { <small class="muted">Uploading…</small> }
                @if (media[question.id]; as m) { <small class="muted">{{ m.originalFileName }}</small> }
              }
            }
            @if (touched.has(question.id) && invalid(question)) { <small class="form-error">This answer is required.</small> }
          </fieldset>
        }
        <button class="button button-primary button-block" type="submit" [disabled]="saving">{{ saving ? 'Sending…' : 'Send to my coach' }}</button>
      </form>
    } @else if (!error) { <p class="table-message">Loading…</p> }
  `,
  styles: [`.question-list { display: grid; gap: 12px; }
    fieldset { border: 1px solid var(--gray-200); margin: 0; }
    fieldset.invalid { border-color: var(--error); }
    legend { padding: 0 4px; }
    .choice-list, .choice-row { display: grid; gap: 8px; }
    .choice-row { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .choice-list label, .choice-row label { display: flex; align-items: center; gap: 8px; min-height: 44px; }`],
})
export class MyFormComponent implements OnInit {
  form: PublishedForm | null = null;
  answers: Record<string, unknown> = {};
  media: Record<string, UploadedMedia> = {};
  uploading: Record<string, boolean> = {};
  touched = new Set<string>();
  saving = false;
  done = false;
  error = '';

  constructor(private readonly api: MyFormsService, private readonly route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.get(id).subscribe({ next: form => (this.form = form), error: e => (this.error = apiErrorMessage(e)) });
  }

  /** Questions in section order, then question order. */
  ordered(): FormQuestion[] {
    if (!this.form) return [];
    const sectionOrder = new Map(this.form.sections.map(section => [section.id, section.order]));
    return [...this.form.questions].sort((a, b) =>
      (sectionOrder.get(a.sectionId ?? '') ?? -1) - (sectionOrder.get(b.sectionId ?? '') ?? -1) || a.order - b.order);
  }

  touch(id: string): void { this.touched.add(id); }
  isChosen(id: string, value: string): boolean { return Array.isArray(this.answers[id]) && (this.answers[id] as string[]).includes(value); }
  toggle(id: string, value: string, chosen: boolean): void {
    const current = Array.isArray(this.answers[id]) ? (this.answers[id] as string[]) : [];
    this.answers[id] = chosen ? [...new Set([...current, value])] : current.filter(item => item !== value);
    this.touch(id);
  }
  invalid(question: FormQuestion): boolean {
    return question.isRequired && !hasAnswer(question, this.answers[question.id], this.media[question.id]?.id);
  }
  progress(): number {
    if (!this.form?.questions.length) return 0;
    const answered = this.form.questions.filter(q => hasAnswer(q, this.answers[q.id], this.media[q.id]?.id)).length;
    return Math.round((answered / this.form.questions.length) * 100);
  }

  upload(question: FormQuestion, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.touch(question.id);
    this.uploading[question.id] = true;
    this.api.upload(file).pipe(finalize(() => (this.uploading[question.id] = false))).subscribe({
      next: media => (this.media[question.id] = media),
      error: e => (this.error = apiErrorMessage(e)),
    });
  }

  submit(): void {
    if (!this.form) return;
    this.form.questions.forEach(question => this.touch(question.id));
    const firstInvalid = this.ordered().find(question => this.invalid(question));
    if (firstInvalid) {
      document.getElementById(`question-${firstInvalid.id}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      return;
    }
    const answers = this.form.questions
      .filter(question => hasAnswer(question, this.answers[question.id], this.media[question.id]?.id))
      .map(question => ({
        questionId: question.id,
        value: question.questionType === 'MediaUpload' ? null : this.answers[question.id],
        mediaId: this.media[question.id]?.id ?? null,
      }));
    this.saving = true; this.error = '';
    this.api.submit(this.form.definitionId, answers).pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => { this.done = true; window.scrollTo({ top: 0 }); },
      error: e => (this.error = apiErrorMessage(e)),
    });
  }
}
