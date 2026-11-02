import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { MediaVideoComponent } from '../../shared/components/media-video/media-video.component';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import {
  FitnessTestsService, TestDefinition, TestDefinitionInput, TestKind, TestScope, TestUnit, localCutOffText, localUnit, testKinds, testUnits,
} from './fitness-tests.service';

const blank = (): TestDefinitionInput => ({
  kind: 'Strength', nameEn: '', nameAr: null, unit: 'Repetitions', higherIsBetter: true, goodThreshold: null, poorThreshold: null,
  instructionsEn: null, instructionsAr: null, youTubeUrl: null, isActive: true,
});

/**
 * The test library (§34–§36): the platform's starter tests and the coach's own. Coaches add and edit their custom
 * tests; administrators also manage the platform tests. Assign tests to a client from the client's Tests tab. A
 * saved test takes an uploaded video (P20).
 */
@Component({
  selector: 'app-test-library',
  standalone: true,
  imports: [FormsModule, MediaVideoComponent, ...I18N_PIPES],
  template: `
    <section class="dashboard-hero"><div><p class="eyebrow">{{ 'testLibrary.eyebrow' | translate }}</p><h1>{{ 'testLibrary.title' | translate }}</h1>
      <p>{{ 'testLibrary.intro' | translate }}</p></div>
      <button type="button" class="button button-primary" (click)="startNew()">{{ 'testLibrary.new' | translate }}</button></section>
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }

    @if (draft(); as d) {
      <form class="card workbench editor" (ngSubmit)="save(d)">
        <h2>{{ (editingId() ? 'testLibrary.edit' : 'testLibrary.new') | translate }}</h2>
        <div class="form-grid">
          <label><span>{{ 'testLibrary.nameEn' | translate }}</span><input [(ngModel)]="d.nameEn" name="nameEn" required maxlength="255" /></label>
          <label><span>{{ 'testLibrary.nameAr' | translate }}</span><input [(ngModel)]="d.nameAr" name="nameAr" maxlength="255" dir="rtl" /></label>
          <label><span>{{ 'testLibrary.kind' | translate }}</span><select [(ngModel)]="d.kind" name="kind">@for (k of kinds; track k) { <option [value]="k">{{ k | enumLabel: 'testKind' }}</option> }</select></label>
          <label><span>{{ 'testLibrary.unit' | translate }}</span><select [(ngModel)]="d.unit" name="unit">@for (u of units; track u) { <option [value]="u">{{ u | enumLabel: 'testUnit' }} ({{ unit(u) }})</option> }</select></label>
          <label><span>{{ 'testLibrary.better' | translate }}</span>
            <select [(ngModel)]="d.higherIsBetter" name="direction"><option [ngValue]="true">{{ 'testLibrary.higher' | translate }}</option><option [ngValue]="false">{{ 'testLibrary.lower' | translate }}</option></select></label>
          <label><span>{{ (d.higherIsBetter ? 'testLibrary.goodFrom' : 'testLibrary.goodUpTo') | translate }}</span><input type="number" min="0" step="0.01" [(ngModel)]="d.goodThreshold" name="good" /></label>
          <label><span>{{ (d.higherIsBetter ? 'testLibrary.poorBelow' : 'testLibrary.poorAbove') | translate }}</span><input type="number" min="0" step="0.01" [(ngModel)]="d.poorThreshold" name="poor" /></label>
          <label><span>{{ 'testLibrary.youTube' | translate }}</span><input type="url" [(ngModel)]="d.youTubeUrl" name="video" placeholder="https://www.youtube.com/watch?v=…" /></label>
        </div>
        <small class="muted">{{ 'testLibrary.cutOffHint' | translate }}</small>
        <label class="wide"><span>{{ 'testLibrary.instructionsEn' | translate }}</span><textarea [(ngModel)]="d.instructionsEn" name="instructionsEn" rows="3" maxlength="4000"></textarea></label>
        <label class="wide"><span>{{ 'testLibrary.instructionsAr' | translate }}</span><textarea [(ngModel)]="d.instructionsAr" name="instructionsAr" rows="3" maxlength="4000" dir="rtl"></textarea></label>
        <label class="check"><input type="checkbox" [(ngModel)]="d.isActive" name="active" /> {{ 'testLibrary.active' | translate }}</label>
        <fieldset class="plain-fieldset video-field">
          <legend>{{ 'catalog.uploadedVideo' | translate }}</legend>
          @if (editingId(); as id) {
            @if (videoMediaId(); as video) {
              <app-media-video [mediaId]="video" [label]="d.nameEn" />
              <button class="link-button danger" type="button" [disabled]="uploading()" (click)="removeVideo(id)">{{ 'catalog.removeVideo' | translate }}</button>
            } @else { <small class="muted">{{ 'catalog.noVideo' | translate }}</small> }
            <label class="wide"><span>{{ (uploading() ? 'catalog.uploading' : 'catalog.uploadVideo') | translate }}</span>
              <input type="file" name="videoFile" accept="video/mp4,video/webm,video/quicktime" [disabled]="uploading()" (change)="uploadVideo(id, $event)" /></label>
          } @else { <small class="muted">{{ 'catalog.saveFirst' | translate }}</small> }
        </fieldset>
        <div class="actions">
          <button type="submit" class="button button-primary" [disabled]="busy()">{{ 'catalog.save' | translate }}</button>
          <button type="button" class="button button-secondary" (click)="draft.set(null)">{{ 'catalog.cancel' | translate }}</button>
        </div>
      </form>
    }

    <section class="card workbench">
      <form class="filters" (ngSubmit)="load()">
        <label><span>{{ 'common.search' | translate }}</span><input [(ngModel)]="searchTerm" name="search" /></label>
        <label><span>{{ 'testLibrary.kind' | translate }}</span><select [(ngModel)]="kind" name="kindFilter"><option value="">{{ 'testLibrary.allKinds' | translate }}</option>
          @for (k of kinds; track k) { <option [value]="k">{{ k | enumLabel: 'testKind' }}</option> }</select></label>
        <label><span>{{ 'testLibrary.library' | translate }}</span><select [(ngModel)]="scope" name="scope">
          <option value="All">{{ 'testLibrary.allTests' | translate }}</option><option value="Platform">{{ 'testLibrary.platform' | translate }}</option>
          <option value="Custom">{{ 'testLibrary.mine' | translate }}</option></select></label>
        <button type="submit" class="button button-secondary">{{ 'common.search' | translate }}</button>
      </form>
      @if (tests().length) {
        <div class="table-scroll"><table>
          <thead><tr><th>{{ 'testLibrary.test' | translate }}</th><th>{{ 'testLibrary.kind' | translate }}</th><th>{{ 'testLibrary.unit' | translate }}</th>
            <th>{{ 'testLibrary.rating' | translate }}</th><th>{{ 'testLibrary.library' | translate }}</th><th></th></tr></thead>
          <tbody>
            @for (t of tests(); track t.id) {
              <tr [class.inactive]="!t.isActive">
                <td><strong translate="no">{{ t.nameEn }}</strong>@if (t.nameAr) { <small class="muted block" dir="rtl" translate="no">{{ t.nameAr }}</small> }
                  @if (t.videoMediaId) { <small class="block">{{ 'catalog.hasVideo' | translate }}</small> }
                  @if (t.youTubeUrl) { <a class="block" [href]="t.youTubeUrl" target="_blank" rel="noopener">{{ 'catalog.youTubeLink' | translate }}</a> }</td>
                <td>{{ t.kind | enumLabel: 'testKind' }}</td>
                <td>{{ unit(t.unit) }}</td>
                <td><small>{{ cutOffs(t) }}</small></td>
                <td>{{ (t.isCustom ? 'testLibrary.mine' : 'testLibrary.platform') | translate }}@if (!t.isActive) {{{ 'testLibrary.inactive' | translate }}}</td>
                <td class="actions">
                  @if (canEdit(t)) {
                    <button type="button" class="link-button" (click)="edit(t)">{{ 'catalog.edit' | translate }}</button>
                    <button type="button" class="link-button danger" (click)="remove(t)">{{ 'catalog.delete' | translate }}</button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table></div>
      } @else if (!loading()) { <p class="muted">{{ 'testLibrary.none' | translate }}</p> }
    </section>
  `,
  styles: [`
    .dashboard-hero { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; flex-wrap: wrap; }
    .editor { display: grid; gap: 12px; margin-bottom: 16px; }
    .wide, .wide textarea { display: grid; gap: 4px; width: 100%; }
    .check { display: flex; gap: 8px; align-items: center; }
    .filters { display: flex; flex-wrap: wrap; gap: 12px; align-items: flex-end; margin-bottom: 12px; }
    .filters label { display: grid; gap: 4px; }
    .video-field { display: grid; gap: 8px; }
    .block { display: block; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; }
    tr.inactive td { opacity: 0.6; }
  `],
})
export class TestLibraryComponent implements OnInit {
  readonly kinds = testKinds;
  readonly units = testUnits;
  readonly tests = signal<TestDefinition[]>([]);
  readonly draft = signal<TestDefinitionInput | null>(null);
  readonly editingId = signal<string | null>(null);
  /** The edited test's uploaded video; changed at once, apart from Save. */
  readonly videoMediaId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly uploading = signal(false);
  readonly error = signal('');
  searchTerm = '';
  kind: TestKind | '' = '';
  scope: TestScope = 'All';

  constructor(private readonly api: FitnessTestsService, private readonly auth: AuthService, private readonly i18n: I18nService) {}

  ngOnInit(): void { this.load(); }

  unit(unit: TestUnit): string { return localUnit(unit, this.i18n); }
  cutOffs(test: TestDefinition): string { return localCutOffText(test, this.i18n); }

  load(): void {
    this.loading.set(true);
    this.api.list({ searchTerm: this.searchTerm.trim(), kind: this.kind, scope: this.scope }).subscribe({
      next: page => { this.tests.set(page.items); this.loading.set(false); },
      error: e => { this.error.set(apiErrorMessage(e)); this.loading.set(false); },
    });
  }

  canEdit(test: TestDefinition): boolean { return test.isCustom || this.auth.isAdmin(); }

  startNew(): void { this.editingId.set(null); this.videoMediaId.set(null); this.draft.set(blank()); this.error.set(''); }

  edit(test: TestDefinition): void {
    const { id: _id, coachId: _coach, isCustom: _custom, videoMediaId, ...input } = test;
    this.editingId.set(test.id);
    this.videoMediaId.set(videoMediaId ?? null);
    this.draft.set({ ...input });
    this.error.set('');
  }

  save(draft: TestDefinitionInput): void {
    const input: TestDefinitionInput = {
      ...draft,
      nameAr: draft.nameAr?.trim() || null,
      youTubeUrl: draft.youTubeUrl?.trim() || null,
      goodThreshold: numberOrNull(draft.goodThreshold),
      poorThreshold: numberOrNull(draft.poorThreshold),
    };
    const id = this.editingId();
    this.busy.set(true);
    (id ? this.api.update(id, input) : this.api.create(input)).subscribe({
      next: () => { this.busy.set(false); this.draft.set(null); this.error.set(''); this.load(); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e)); },
    });
  }

  /** Uploads the test's video at once; it replaces the previous one. */
  uploadVideo(id: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.error.set('');
    this.api.uploadVideo(id, file).pipe(finalize(() => { this.uploading.set(false); input.value = ''; })).subscribe({
      next: test => { this.videoMediaId.set(test.videoMediaId ?? null); this.load(); },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  removeVideo(id: string): void {
    this.uploading.set(true);
    this.api.removeVideo(id).pipe(finalize(() => this.uploading.set(false))).subscribe({
      next: () => { this.videoMediaId.set(null); this.load(); },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  remove(test: TestDefinition): void {
    if (!confirm(this.i18n.translate('catalog.confirmDelete', { name: this.i18n.name(test) }))) return;
    this.api.delete(test.id).subscribe({ next: () => this.load(), error: e => this.error.set(apiErrorMessage(e)) });
  }
}

function numberOrNull(value: number | string | null | undefined): number | null {
  return value === null || value === undefined || value === '' ? null : Number(value);
}
