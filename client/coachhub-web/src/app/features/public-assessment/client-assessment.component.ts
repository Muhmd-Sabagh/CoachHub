import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { assessmentCopy, AssessmentCopyKey } from './public-assessment.copy';
import {
  AccessCodes,
  AssessmentQuestion,
  ClientAssessmentService,
  EligibleForm,
  FormAccessResponse,
  hasAnswer,
  MediaMetadata,
  PublishedForm,
  SubmissionResponse,
} from './client-assessment.service';

type Stage = 'access' | 'selection' | 'form' | 'success';
@Component({
  selector: 'app-client-assessment',
  templateUrl: './client-assessment.component.html',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  styleUrl: './client-assessment.component.css',
})
export class ClientAssessmentComponent {
  stage: Stage = 'access';
  codes: AccessCodes = { clientCode: '', formCode: '' };
  access: FormAccessResponse | null = null;
  form: PublishedForm | null = null;
  submission: SubmissionResponse | null = null;
  answers: Record<string, unknown> = {};
  media: Record<string, MediaMetadata> = {};
  uploading: Record<string, boolean> = {};
  touched = new Set<string>();
  loading = false;
  error = '';
  constructor(
    private readonly api: ClientAssessmentService,
    readonly i18n: I18nService,
  ) {}
  copy(key: AssessmentCopyKey): string {
    return assessmentCopy(this.i18n.language(), key);
  }
  validateAccess(): void {
    if (!this.codes.clientCode.trim() || !this.codes.formCode.trim()) {
      this.error = this.copy('codeRequired');
      return;
    }
    this.loading = true;
    this.error = '';
    this.codes = {
      clientCode: this.codes.clientCode.trim().toUpperCase(),
      formCode: this.codes.formCode.trim().toUpperCase(),
    };
    this.api.validateAccess(this.codes).subscribe({
      next: (access) => {
        this.access = access;
        this.stage = 'selection';
        if (access.eligibleForms.length === 1) this.selectForm(access.eligibleForms[0]);
        else this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = this.copy('codeInvalid');
      },
    });
  }
  selectForm(selected: EligibleForm): void {
    this.loading = true;
    this.error = '';
    this.api
      .getForm(selected.id, this.codes)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (form) => {
          this.form = form;
          this.answers = {};
          this.media = {};
          this.touched.clear();
          this.stage = 'form';
          window.scrollTo({ top: 0, behavior: 'smooth' });
        },
        error: (error) => (this.error = apiErrorMessage(error)),
      });
  }
  orderedQuestions(sectionId: string | null): AssessmentQuestion[] {
    return (this.form?.questions ?? [])
      .filter((question) => (question.sectionId ?? null) === sectionId)
      .sort((a, b) => a.order - b.order);
  }
  toggleChoice(questionId: string, value: string, selected: boolean): void {
    const current = this.answers[questionId];
    const values = Array.isArray(current) ? ([...current] as string[]) : [];
    this.answers[questionId] = selected
      ? [...new Set([...values, value])]
      : values.filter((item) => item !== value);
    this.touch(questionId);
  }
  touch(questionId: string): void {
    this.touched.add(questionId);
  }
  invalid(question: AssessmentQuestion): boolean {
    return (
      question.isRequired &&
      !hasAnswer(question, this.answers[question.id], this.media[question.id]?.id)
    );
  }
  upload(question: AssessmentQuestion, event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.touch(question.id);
    this.uploading[question.id] = true;
    this.error = '';
    this.api
      .upload(this.codes, file)
      .pipe(finalize(() => (this.uploading[question.id] = false)))
      .subscribe({
        next: (metadata) => (this.media[question.id] = metadata),
        error: (error) => (this.error = apiErrorMessage(error)),
      });
  }
  submit(): void {
    if (!this.form) return;
    this.form.questions.forEach((question) => this.touch(question.id));
    const firstInvalid = this.form.questions.find((question) => this.invalid(question));
    if (firstInvalid) {
      this.error = this.copy('requiredError');
      document
        .getElementById(`question-${firstInvalid.id}`)
        ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      return;
    }
    const answers = this.form.questions
      .filter((question) =>
        hasAnswer(question, this.answers[question.id], this.media[question.id]?.id),
      )
      .map((question) => ({
        questionId: question.id,
        value: question.questionType === 'MediaUpload' ? null : this.answers[question.id],
        mediaId: this.media[question.id]?.id ?? null,
      }));
    this.loading = true;
    this.error = '';
    this.api
      .submit(this.codes, this.form.definitionId, answers)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (submission) => {
          this.submission = submission;
          this.stage = 'success';
          window.scrollTo({ top: 0, behavior: 'smooth' });
        },
        error: (error) => (this.error = apiErrorMessage(error)),
      });
  }
  submitAnother(): void {
    this.form = null;
    this.submission = null;
    this.answers = {};
    this.media = {};
    this.validateAccess();
  }
  restart(): void {
    this.stage = 'access';
    this.codes = { clientCode: '', formCode: '' };
    this.access = null;
    this.form = null;
    this.submission = null;
    this.error = '';
  }
  progress(): number {
    if (!this.form?.questions.length) return 0;
    const answered = this.form.questions.filter((question) =>
      hasAnswer(question, this.answers[question.id], this.media[question.id]?.id),
    ).length;
    return Math.round((answered / this.form.questions.length) * 100);
  }
}
