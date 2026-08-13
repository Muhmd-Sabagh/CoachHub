import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import {
  ReferenceDataService,
  ReferenceKey,
  ReferenceRecord,
} from '../settings/reference-data.service';
import { CatalogItem } from './catalog.models';
import { CatalogKind, CatalogService } from './catalog.service';
@Component({ selector: 'app-catalog', templateUrl: './catalog.component.html', standalone: false })
export class CatalogComponent implements OnInit {
  readonly kind: CatalogKind;
  readonly title: string;
  readonly categoryKey: ReferenceKey;
  page: PagedResult<CatalogItem> = emptyPage();
  categories: ReferenceRecord[] = [];
  search = '';
  appliedSearch = '';
  categoryId = '';
  active = '';
  loading = false;
  saving = false;
  editorOpen = false;
  editingId: string | null = null;
  error = '';
  editor = this.emptyEditor();
  constructor(
    route: ActivatedRoute,
    private readonly data: CatalogService,
    private readonly references: ReferenceDataService,
  ) {
    this.kind = route.snapshot.data['kind'] as CatalogKind;
    this.title = this.kind === 'foods' ? 'Foods' : 'Exercises';
    this.categoryKey = this.kind === 'foods' ? 'food-categories' : 'exercise-categories';
  }
  ngOnInit(): void {
    this.references
      .list(this.categoryKey, { pageSize: 100, isActive: true })
      .subscribe((x) => (this.categories = x.items));
    this.load();
  }
  applySearch(): void {
    this.appliedSearch = this.search.trim();
    this.load(1);
  }
  reset(): void {
    this.search = '';
    this.appliedSearch = '';
    this.categoryId = '';
    this.active = '';
    this.load(1);
  }
  load(pageNumber = this.page.pageNumber): void {
    this.loading = true;
    this.error = '';
    this.data
      .list(this.kind, {
        pageNumber,
        pageSize: 10,
        searchTerm: this.appliedSearch,
        categoryId: this.categoryId,
        isActive: this.active,
      })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({ next: (p) => (this.page = p), error: (e) => (this.error = apiErrorMessage(e)) });
  }
  create(): void {
    this.editingId = null;
    this.editor = this.emptyEditor();
    this.editorOpen = true;
  }
  edit(item: CatalogItem): void {
    this.editingId = item.id;
    this.editor = {
      nameEn: item.nameEn,
      nameAr: item.nameAr ?? '',
      categoryId: item.foodCategoryId ?? item.exerciseCategoryId ?? '',
      measurementUnit: item.measurementUnit ?? '',
      calories: item.caloriesPer100 ?? 0,
      protein: item.proteinPer100 ?? 0,
      carbs: item.carbohydratesPer100 ?? 0,
      fat: item.fatPer100 ?? 0,
      youTubeUrl: item.youTubeUrl ?? '',
      mediaId: item.mediaId ?? '',
      isActive: item.isActive,
    };
    this.editorOpen = true;
  }
  close(): void {
    this.editorOpen = false;
  }
  save(): void {
    const common = {
      nameEn: this.editor.nameEn,
      nameAr: this.editor.nameAr || null,
      mediaId: this.editor.mediaId || null,
      isActive: this.editor.isActive,
    };
    const input =
      this.kind === 'foods'
        ? {
            ...common,
            foodCategoryId: this.editor.categoryId,
            measurementUnit: this.editor.measurementUnit,
            caloriesPer100: this.editor.calories,
            proteinPer100: this.editor.protein,
            carbohydratesPer100: this.editor.carbs,
            fatPer100: this.editor.fat,
          }
        : {
            ...common,
            exerciseCategoryId: this.editor.categoryId,
            youTubeUrl: this.editor.youTubeUrl || null,
          };
    this.saving = true;
    const req = this.editingId
      ? this.data.update(this.kind, this.editingId, input)
      : this.data.create(this.kind, input);
    req.pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => {
        this.close();
        this.load();
      },
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  remove(item: CatalogItem): void {
    if (confirm(`Delete ${item.nameEn}?`))
      this.data
        .delete(this.kind, item.id)
        .subscribe({ next: () => this.load(), error: (e) => (this.error = apiErrorMessage(e)) });
  }
  categoryName(id?: string): string {
    return this.categories.find((x) => x.id === id)?.nameEn ?? '—';
  }
  private emptyEditor() {
    return {
      nameEn: '',
      nameAr: '',
      categoryId: '',
      measurementUnit: 'g',
      calories: 0,
      protein: 0,
      carbs: 0,
      fat: 0,
      youTubeUrl: '',
      mediaId: '',
      isActive: true,
    };
  }
}
