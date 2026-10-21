import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import {
  FitnessTestsService, TestDefinition, TestDefinitionInput, TestKind, TestScope, cutOffText, testKinds, testUnits, unitLabels,
} from './fitness-tests.service';

const blank = (): TestDefinitionInput => ({
  kind: 'Strength', nameEn: '', nameAr: null, unit: 'Repetitions', higherIsBetter: true, goodThreshold: null, poorThreshold: null,
  instructionsEn: null, instructionsAr: null, youTubeUrl: null, isActive: true,
});

/**
 * The test library (§34–§36): the platform's starter tests and the coach's own. Coaches add and edit their custom
 * tests; administrators also manage the platform tests. Assign tests to a client from the client's Tests tab.
 */
@Component({
  selector: 'app-test-library',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="dashboard-hero"><div><p class="eyebrow">Coaching</p><h1>Tests</h1>
      <p>Mobility, strength and endurance tests. Assign them to clients from the client's Tests tab.</p></div>
      <button type="button" class="button button-primary" (click)="startNew()">New test</button></section>
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }

    @if (draft(); as d) {
      <form class="card workbench editor" (ngSubmit)="save(d)">
        <h2>{{ editingId() ? 'Edit test' : 'New test' }}</h2>
        <div class="form-grid">
          <label><span>Name (English)</span><input [(ngModel)]="d.nameEn" name="nameEn" required maxlength="255" /></label>
          <label><span>Name (Arabic)</span><input [(ngModel)]="d.nameAr" name="nameAr" maxlength="255" dir="rtl" /></label>
          <label><span>Kind</span><select [(ngModel)]="d.kind" name="kind">@for (k of kinds; track k) { <option [value]="k">{{ k }}</option> }</select></label>
          <label><span>Unit</span><select [(ngModel)]="d.unit" name="unit">@for (u of units; track u) { <option [value]="u">{{ u }} ({{ unitLabel[u] }})</option> }</select></label>
          <label><span>Better result</span>
            <select [(ngModel)]="d.higherIsBetter" name="direction"><option [ngValue]="true">Higher</option><option [ngValue]="false">Lower</option></select></label>
          <label><span>Good {{ d.higherIsBetter ? 'from' : 'up to' }}</span><input type="number" min="0" step="0.01" [(ngModel)]="d.goodThreshold" name="good" /></label>
          <label><span>Poor {{ d.higherIsBetter ? 'below' : 'above' }}</span><input type="number" min="0" step="0.01" [(ngModel)]="d.poorThreshold" name="poor" /></label>
          <label><span>Video (YouTube)</span><input type="url" [(ngModel)]="d.youTubeUrl" name="video" placeholder="https://www.youtube.com/watch?v=…" /></label>
        </div>
        <small class="muted">Leave both cut-offs empty to classify results by hand.</small>
        <label class="wide"><span>Instructions (English)</span><textarea [(ngModel)]="d.instructionsEn" name="instructionsEn" rows="3" maxlength="4000"></textarea></label>
        <label class="wide"><span>Instructions (Arabic)</span><textarea [(ngModel)]="d.instructionsAr" name="instructionsAr" rows="3" maxlength="4000" dir="rtl"></textarea></label>
        <label class="check"><input type="checkbox" [(ngModel)]="d.isActive" name="active" /> Active (inactive tests take no new results)</label>
        <div class="actions">
          <button type="submit" class="button button-primary" [disabled]="busy()">Save</button>
          <button type="button" class="button button-secondary" (click)="draft.set(null)">Cancel</button>
        </div>
      </form>
    }

    <section class="card workbench">
      <form class="filters" (ngSubmit)="load()">
        <label><span>Search</span><input [(ngModel)]="searchTerm" name="search" /></label>
        <label><span>Kind</span><select [(ngModel)]="kind" name="kindFilter"><option value="">All kinds</option>
          @for (k of kinds; track k) { <option [value]="k">{{ k }}</option> }</select></label>
        <label><span>Library</span><select [(ngModel)]="scope" name="scope">
          <option value="All">All tests</option><option value="Platform">Platform</option><option value="Custom">My tests</option></select></label>
        <button type="submit" class="button button-secondary">Search</button>
      </form>
      @if (tests().length) {
        <div class="table-scroll"><table>
          <thead><tr><th>Test</th><th>Kind</th><th>Unit</th><th>Rating</th><th>Library</th><th></th></tr></thead>
          <tbody>
            @for (t of tests(); track t.id) {
              <tr [class.inactive]="!t.isActive">
                <td><strong>{{ t.nameEn }}</strong>@if (t.nameAr) { <small class="muted block" dir="rtl">{{ t.nameAr }}</small> }
                  @if (t.youTubeUrl) { <a class="block" [href]="t.youTubeUrl" target="_blank" rel="noopener">Video</a> }</td>
                <td>{{ t.kind }}</td>
                <td>{{ unitLabel[t.unit] }}</td>
                <td><small>{{ cutOffs(t) }}</small></td>
                <td>{{ t.isCustom ? 'Mine' : 'Platform' }}{{ t.isActive ? '' : ' · inactive' }}</td>
                <td class="actions">
                  @if (canEdit(t)) {
                    <button type="button" class="link-button" (click)="edit(t)">Edit</button>
                    <button type="button" class="link-button danger" (click)="remove(t)">Delete</button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table></div>
      } @else if (!loading()) { <p class="muted">No tests match.</p> }
    </section>
  `,
  styles: [`
    .dashboard-hero { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; flex-wrap: wrap; }
    .editor { display: grid; gap: 12px; margin-bottom: 16px; }
    .wide, .wide textarea { display: grid; gap: 4px; width: 100%; }
    .check { display: flex; gap: 8px; align-items: center; }
    .filters { display: flex; flex-wrap: wrap; gap: 12px; align-items: flex-end; margin-bottom: 12px; }
    .filters label { display: grid; gap: 4px; }
    .block { display: block; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; }
    tr.inactive td { opacity: 0.6; }
  `],
})
export class TestLibraryComponent implements OnInit {
  readonly kinds = testKinds;
  readonly units = testUnits;
  readonly unitLabel = unitLabels;
  readonly cutOffs = cutOffText;
  readonly tests = signal<TestDefinition[]>([]);
  readonly draft = signal<TestDefinitionInput | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  searchTerm = '';
  kind: TestKind | '' = '';
  scope: TestScope = 'All';

  constructor(private readonly api: FitnessTestsService, private readonly auth: AuthService) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.list({ searchTerm: this.searchTerm.trim(), kind: this.kind, scope: this.scope }).subscribe({
      next: page => { this.tests.set(page.items); this.loading.set(false); },
      error: e => { this.error.set(apiErrorMessage(e)); this.loading.set(false); },
    });
  }

  canEdit(test: TestDefinition): boolean { return test.isCustom || this.auth.isAdmin(); }

  startNew(): void { this.editingId.set(null); this.draft.set(blank()); this.error.set(''); }

  edit(test: TestDefinition): void {
    const { id: _id, coachId: _coach, isCustom: _custom, ...input } = test;
    this.editingId.set(test.id);
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

  remove(test: TestDefinition): void {
    if (!confirm(`Delete "${test.nameEn}"?`)) return;
    this.api.delete(test.id).subscribe({ next: () => this.load(), error: e => this.error.set(apiErrorMessage(e)) });
  }
}

function numberOrNull(value: number | string | null | undefined): number | null {
  return value === null || value === undefined || value === '' ? null : Number(value);
}
