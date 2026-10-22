import { Component, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import {
  AssessmentReviewStore,
  AssessmentsService,
  FormSummary,
  FormVersion,
  Question,
  SubmissionSummary, QuestionMetric, questionMetrics } from './assessments.service';
@Component({
  selector: 'app-assessments',
  templateUrl: './assessments.component.html',
  standalone: false,
})
export class AssessmentsComponent implements OnInit {
  readonly tab = signal<'forms' | 'submissions'>('forms');
  readonly forms = signal<PagedResult<FormSummary>>(emptyPage());
  readonly submissions = signal<PagedResult<SubmissionSummary>>(emptyPage());
  readonly search = signal('');
  readonly appliedSearch = signal('');
  readonly loading = signal(false);
  readonly error = signal('');
  readonly formEditor = signal(false);
  readonly builder = signal<FormVersion | null>(null);
  readonly newForm = signal({ name: '', formType: 'InitialAssessment' });
  readonly questionOpen = signal(false);
  readonly editingQuestionId = signal<string | null>(null);
  readonly question = signal(this.emptyQuestion());
  readonly section = signal({ title: '', order: 0 });
  constructor(
    private data: AssessmentsService,
    readonly review: AssessmentReviewStore,
  ) {}
  ngOnInit() {
    this.loadForms();
    this.loadSubmissions();
  }
  searchNow() {
    this.appliedSearch.set(this.search().trim());
    this.tab() === 'forms' ? this.loadForms(1) : this.loadSubmissions(1);
  }
  reset() {
    this.search.set('');
    this.appliedSearch.set('');
    this.searchNow();
  }
  loadForms(pageNumber = 1) {
    this.loading.set(true);
    this.data
      .listForms({ pageNumber, pageSize: 10, searchTerm: this.appliedSearch() })
      .pipe(finalize(() => (this.loading.set(false))))
      .subscribe({
        next: (x) => (this.forms.set(x)),
        error: (e) => (this.error.set(apiErrorMessage(e))),
      });
  }
  loadSubmissions(pageNumber = 1) {
    this.loading.set(true);
    this.data
      .listSubmissions({ pageNumber, pageSize: 10, searchTerm: this.appliedSearch() })
      .pipe(finalize(() => (this.loading.set(false))))
      .subscribe({
        next: (x) => (this.submissions.set(x)),
        error: (e) => (this.error.set(apiErrorMessage(e))),
      });
  }
  createForm() {
    this.data.create(this.newForm()).subscribe({
      next: (x) => {
        this.builder.set(x);
        this.formEditor.set(false);
        this.loadForms();
      },
      error: (e) => (this.error.set(apiErrorMessage(e))),
    });
  }
  openForm(f: FormSummary) {
    this.data
      .preview(f.id)
      .subscribe({
        next: (x) => (this.builder.set(x)),
        error: (e) => (this.error.set(apiErrorMessage(e))),
      });
  }
  archive(f: FormSummary) {
    this.data
      .update(f.id, { name: f.name, isArchived: !f.isArchived })
      .subscribe(() => this.loadForms());
  }
  publish() {
    const builder = this.builder();
    if (builder) this.data.publish(builder.definitionId).subscribe((x) => this.builder.set(x));
  }
  createDraft() {
    const builder = this.builder();
    if (builder) this.data.draft(builder.definitionId).subscribe((x) => this.builder.set(x));
  }
  addSection() {
    const builder = this.builder();
    if (builder)
      this.data
        .addSection(builder.definitionId, {
          ...this.section(),
          order: builder.sections.length,
        })
        .subscribe(() => this.refresh());
  }
  addQuestion() {
    this.editingQuestionId.set(null);
    this.question.set(this.emptyQuestion());
    this.question().order = this.builder()?.questions.length ?? 0;
    this.questionOpen.set(true);
  }
  editQuestion(q: Question) {
    this.editingQuestionId.set(q.id);
    this.question.set({
      sectionId: q.sectionId ?? '',
      text: q.text,
      questionType: q.questionType,
      isRequired: q.isRequired,
      order: q.order,
      options: q.options.map((x) => ({ value: x.value, label: x.label, order: x.order })),
      metric: q.metric ?? null,
    });
    this.questionOpen.set(true);
  }
  saveQuestion() {
    const editingQuestionId = this.editingQuestionId();
    const builder = this.builder();
    if (!builder) return;
    const metric = this.metricsFor(this.question().questionType).some((x) => x.value === this.question().metric) ? this.question().metric : null;
    const input = { ...this.question(), sectionId: this.question().sectionId || null, metric };
    const req = editingQuestionId
      ? this.data.updateQuestion(builder.definitionId, editingQuestionId, input)
      : this.data.addQuestion(builder.definitionId, input);
    req.subscribe({
      next: () => {
        this.questionOpen.set(false);
        this.refresh();
      },
      error: (e) => (this.error.set(apiErrorMessage(e))),
    });
  }
  removeQuestion(q: Question) {
    const builder = this.builder();
    if (builder && confirm('Delete this question?'))
      this.data.deleteQuestion(builder.definitionId, q.id).subscribe(() => this.refresh());
  }
  move(q: Question, delta: number) {
    const builder = this.builder();
    if (!builder) return;
    const items = [...builder.questions].sort((a, b) => a.order - b.order);
    const i = items.findIndex((x) => x.id === q.id),
      j = i + delta;
    if (j < 0 || j >= items.length) return;
    [items[i], items[j]] = [items[j], items[i]];
    this.data
      .reorder(
        builder.definitionId,
        items.map((x) => x.id),
      )
      .subscribe(() => this.refresh());
  }
  addOption() {
    this.question().options.push({ value: '', label: '', order: this.question().options.length });
  }
  reviewSubmission(s: SubmissionSummary) {
    this.data.getSubmission(s.id).subscribe((x) => this.review.open(x));
  }
  displayValue(json: string): string {
    try {
      const v = JSON.parse(json);
      return Array.isArray(v) ? v.join(', ') : String(v);
    } catch {
      return json;
    }
  }
  private refresh() {
    const builder = this.builder();
    if (builder) this.data.preview(builder.definitionId).subscribe((x) => this.builder.set(x));
  }
  private emptyQuestion() {
    return {
      sectionId: '',
      text: '',
      questionType: 'ShortText',
      isRequired: false,
      order: 0,
      options: [] as { value: string; label: string; order: number }[],
      metric: null as QuestionMetric | null,
    };
  }

  /** The measures a question of this answer type can record. */
  metricsFor(type: string) {
    return questionMetrics.filter((x) => x.type === type);
  }

  metricLabel(metric: QuestionMetric | null | undefined): string {
    return questionMetrics.find((x) => x.value === metric)?.label ?? '';
  }
}
