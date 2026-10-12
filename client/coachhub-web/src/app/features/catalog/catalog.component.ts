import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
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
  /** Foods only: both catalogs, the platform's, or the coach's own (§5). */
  scope = '';
  loading = false;
  saving = false;
  editorOpen = false;
  editingId: string | null = null;
  error = '';
  editor = this.emptyEditor();
  readonly isAdmin: boolean;
  /**
   * Administrators manage the platform catalogs. Coaches also add custom foods of their own (§5);
   * exercises stay platform-only until P9.
   */
  readonly canCreate: boolean;
  constructor(
    route: ActivatedRoute,
    private readonly data: CatalogService,
    private readonly references: ReferenceDataService,
    auth: AuthService,
  ) {
    this.isAdmin = auth.isAdmin();
    this.kind = route.snapshot.data['kind'] as CatalogKind;
    this.canCreate = this.isAdmin || this.kind === 'foods';
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
    this.scope = '';
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
        scope: this.kind === 'foods' ? this.scope : '',
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
      baseUnit: item.baseUnit ?? 'Gram',
      pieceNameEn: item.pieceNameEn ?? '',
      pieceNameAr: item.pieceNameAr ?? '',
      servingSize: item.servingSize ?? 100,
      calories: item.caloriesPer100 ?? 0,
      protein: item.proteinPer100 ?? 0,
      carbs: item.carbohydratesPer100 ?? 0,
      fat: item.fatPer100 ?? 0,
      fiber: item.fiberPer100 ?? null,
      sugar: item.sugarPer100 ?? null,
      sodium: item.sodiumMgPer100 ?? null,
      units: (item.units ?? []).map((u) => ({ ...u })),
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
            baseUnit: this.editor.baseUnit,
            pieceNameEn: this.editor.baseUnit === 'Piece' ? this.editor.pieceNameEn || null : null,
            pieceNameAr: this.editor.baseUnit === 'Piece' ? this.editor.pieceNameAr || null : null,
            servingSize: this.editor.servingSize,
            caloriesPer100: this.editor.calories,
            proteinPer100: this.editor.protein,
            carbohydratesPer100: this.editor.carbs,
            fatPer100: this.editor.fat,
            fiberPer100: nullable(this.editor.fiber),
            sugarPer100: nullable(this.editor.sugar),
            sodiumMgPer100: nullable(this.editor.sodium),
            units: this.editor.units.map((u: any) => ({
              id: u.id ?? null,
              nameEn: u.nameEn,
              nameAr: u.nameAr || null,
              baseQuantity: u.baseQuantity,
            })),
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
  /** Administrators edit anything; a coach edits only their own custom foods. */
  canEdit(item: CatalogItem): boolean {
    return this.isAdmin || (this.kind === 'foods' && !!item.isCustom);
  }
  addUnit(): void {
    this.editor.units.push({ id: null, nameEn: '', nameAr: '', baseQuantity: 1 });
  }
  removeUnit(index: number): void {
    this.editor.units.splice(index, 1);
  }
  /** What "per 100" means for the chosen base unit. */
  per100(): string {
    return this.editor.baseUnit === 'Gram'
      ? '100 g'
      : this.editor.baseUnit === 'Milliliter'
        ? '100 ml'
        : `100 × ${this.editor.pieceNameEn || 'piece'}`;
  }
  categoryName(id?: string): string {
    return this.categories.find((x) => x.id === id)?.nameEn ?? '—';
  }
  private emptyEditor() {
    return {
      nameEn: '',
      nameAr: '',
      categoryId: '',
      baseUnit: 'Gram' as 'Gram' | 'Milliliter' | 'Piece',
      pieceNameEn: '',
      pieceNameAr: '',
      servingSize: 100,
      calories: 0,
      protein: 0,
      carbs: 0,
      fat: 0,
      fiber: null as number | null,
      sugar: null as number | null,
      sodium: null as number | null,
      units: [] as any[],
      youTubeUrl: '',
      mediaId: '',
      isActive: true,
    };
  }
}

/** An empty number input means "not recorded". */
function nullable(value: number | string | null | undefined): number | null {
  return value === null || value === undefined || value === '' ? null : Number(value);
}
