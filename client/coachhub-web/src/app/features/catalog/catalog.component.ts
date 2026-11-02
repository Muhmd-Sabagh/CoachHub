import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import {
  ReferenceDataService,
  ReferenceKey,
  ReferenceRecord,
} from '../settings/reference-data.service';
import { CatalogItem, exerciseDifficulties } from './catalog.models';
import { CatalogKind, CatalogService } from './catalog.service';
@Component({
  selector: 'app-catalog', templateUrl: './catalog.component.html', standalone: false })
export class CatalogComponent implements OnInit {
  readonly kind: CatalogKind;
  readonly titleKey: TranslationKey;
  readonly addKey: TranslationKey;
  readonly categoryKey: ReferenceKey;
  readonly page = signal<PagedResult<CatalogItem>>(emptyPage());
  readonly categories = signal<ReferenceRecord[]>([]);
  readonly search = signal('');
  readonly appliedSearch = signal('');
  readonly categoryId = signal('');
  readonly active = signal('');
  /** Both catalogs, the platform's, or the coach's own (§5, §17). */
  readonly scope = signal('');
  /** Exercises: training the group as primary or secondary muscle. */
  readonly muscleGroupId = signal('');
  readonly muscles = signal<ReferenceRecord[]>([]);
  readonly equipment = signal<ReferenceRecord[]>([]);
  readonly difficulties = exerciseDifficulties;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly editorOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly error = signal('');
  readonly editor = signal(this.emptyEditor());
  /** The edited exercise's uploaded video (P20); changed at once, apart from Save. */
  readonly videoMediaId = signal<string | null>(null);
  readonly uploading = signal(false);
  readonly isAdmin: boolean;
  /** Administrators manage the platform catalogs; coaches add custom foods and exercises of their own (§5, §17). */
  readonly canCreate: boolean;
  constructor(
    route: ActivatedRoute,
    private readonly data: CatalogService,
    private readonly references: ReferenceDataService,
    auth: AuthService,
    private readonly i18n: I18nService,
  ) {
    this.isAdmin = auth.isAdmin();
    this.kind = route.snapshot.data['kind'] as CatalogKind;
    this.canCreate = true;
    this.titleKey = this.kind === 'foods' ? 'catalog.foods' : 'catalog.exercises';
    this.addKey = this.kind === 'foods'
      ? (this.isAdmin ? 'catalog.addFood' : 'catalog.addCustomFood')
      : (this.isAdmin ? 'catalog.addExercise' : 'catalog.addCustomExercise');
    this.categoryKey = this.kind === 'foods' ? 'food-categories' : 'exercise-categories';
  }
  ngOnInit(): void {
    this.references
      .list(this.categoryKey, { pageSize: 100, isActive: true })
      .subscribe((x) => (this.categories.set(x.items)));
    if (this.kind === 'exercises') {
      this.references.list('muscle-groups', { pageSize: 100, isActive: true }).subscribe((x) => (this.muscles.set(x.items)));
      this.references.list('equipment', { pageSize: 100, isActive: true }).subscribe((x) => (this.equipment.set(x.items)));
    }
    this.load();
  }
  applySearch(): void {
    this.appliedSearch.set(this.search().trim());
    this.load(1);
  }
  reset(): void {
    this.search.set('');
    this.appliedSearch.set('');
    this.categoryId.set('');
    this.active.set('');
    this.scope.set('');
    this.muscleGroupId.set('');
    this.load(1);
  }
  load(pageNumber = this.page().pageNumber): void {
    this.loading.set(true);
    this.error.set('');
    this.data
      .list(this.kind, {
        pageNumber,
        pageSize: 10,
        searchTerm: this.appliedSearch(),
        categoryId: this.categoryId(),
        isActive: this.active(),
        scope: this.scope(),
        muscleGroupId: this.kind === 'exercises' ? this.muscleGroupId() : '',
      })
      .pipe(finalize(() => (this.loading.set(false))))
      .subscribe({ next: (p) => (this.page.set(p)), error: (e) => (this.error.set(apiErrorMessage(e))) });
  }
  create(): void {
    this.editingId.set(null);
    this.videoMediaId.set(null);
    this.editor.set(this.emptyEditor());
    this.editorOpen.set(true);
  }
  edit(item: CatalogItem): void {
    this.editingId.set(item.id);
    this.videoMediaId.set(item.videoMediaId ?? null);
    this.editor.set({
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
      difficulty: item.difficulty ?? '',
      primaryMuscleGroupId: item.primaryMuscleGroupId ?? '',
      secondaryMuscleGroupIds: new Set(item.secondaryMuscleGroupIds ?? []),
      equipmentIds: new Set(item.equipmentIds ?? []),
      instructionsEn: item.instructionsEn ?? '',
      instructionsAr: item.instructionsAr ?? '',
      coachingCuesEn: item.coachingCuesEn ?? '',
      coachingCuesAr: item.coachingCuesAr ?? '',
      commonMistakesEn: item.commonMistakesEn ?? '',
      commonMistakesAr: item.commonMistakesAr ?? '',
    });
    this.editorOpen.set(true);
  }
  close(): void {
    this.editorOpen.set(false);
  }
  save(): void {
    const editingId = this.editingId();
    const common = {
      nameEn: this.editor().nameEn,
      nameAr: this.editor().nameAr || null,
      mediaId: this.editor().mediaId || null,
      isActive: this.editor().isActive,
    };
    const input =
      this.kind === 'foods'
        ? {
            ...common,
            foodCategoryId: this.editor().categoryId,
            baseUnit: this.editor().baseUnit,
            pieceNameEn: this.editor().baseUnit === 'Piece' ? this.editor().pieceNameEn || null : null,
            pieceNameAr: this.editor().baseUnit === 'Piece' ? this.editor().pieceNameAr || null : null,
            servingSize: this.editor().servingSize,
            caloriesPer100: this.editor().calories,
            proteinPer100: this.editor().protein,
            carbohydratesPer100: this.editor().carbs,
            fatPer100: this.editor().fat,
            fiberPer100: nullable(this.editor().fiber),
            sugarPer100: nullable(this.editor().sugar),
            sodiumMgPer100: nullable(this.editor().sodium),
            units: this.editor().units.map((u: any) => ({
              id: u.id ?? null,
              nameEn: u.nameEn,
              nameAr: u.nameAr || null,
              baseQuantity: u.baseQuantity,
            })),
          }
        : {
            ...common,
            exerciseCategoryId: this.editor().categoryId,
            youTubeUrl: this.editor().youTubeUrl || null,
            difficulty: this.editor().difficulty || null,
            primaryMuscleGroupId: this.editor().primaryMuscleGroupId || null,
            secondaryMuscleGroupIds: [...this.editor().secondaryMuscleGroupIds].filter(
              (x) => x !== this.editor().primaryMuscleGroupId,
            ),
            equipmentIds: [...this.editor().equipmentIds],
            instructionsEn: this.editor().instructionsEn || null,
            instructionsAr: this.editor().instructionsAr || null,
            coachingCuesEn: this.editor().coachingCuesEn || null,
            coachingCuesAr: this.editor().coachingCuesAr || null,
            commonMistakesEn: this.editor().commonMistakesEn || null,
            commonMistakesAr: this.editor().commonMistakesAr || null,
          };
    this.saving.set(true);
    const req = editingId
      ? this.data.update(this.kind, editingId, input)
      : this.data.create(this.kind, input);
    req.pipe(finalize(() => (this.saving.set(false)))).subscribe({
      next: () => {
        this.close();
        this.load();
      },
      error: (e) => (this.error.set(apiErrorMessage(e))),
    });
  }
  remove(item: CatalogItem): void {
    if (confirm(this.i18n.translate('catalog.confirmDelete', { name: this.i18n.name(item) })))
      this.data
        .delete(this.kind, item.id)
        .subscribe({ next: () => this.load(), error: (e) => (this.error.set(apiErrorMessage(e))) });
  }
  /** Administrators edit anything; a coach edits only their own custom foods and exercises. */
  canEdit(item: CatalogItem): boolean {
    return this.isAdmin || !!item.isCustom;
  }
  toggle(set: Set<string>, id: string): void {
    if (set.has(id)) set.delete(id);
    else set.add(id);
  }
  /** Uploads the exercise's video at once; it replaces the previous one. */
  uploadVideo(id: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.error.set('');
    this.data.uploadVideo(id, file).pipe(finalize(() => { this.uploading.set(false); input.value = ''; })).subscribe({
      next: (item) => { this.videoMediaId.set(item.videoMediaId ?? null); this.load(); },
      error: (e) => this.error.set(apiErrorMessage(e)),
    });
  }
  removeVideo(id: string): void {
    this.uploading.set(true);
    this.data.removeVideo(id).pipe(finalize(() => this.uploading.set(false))).subscribe({
      next: () => { this.videoMediaId.set(null); this.load(); },
      error: (e) => this.error.set(apiErrorMessage(e)),
    });
  }
  localName(record: ReferenceRecord): string {
    return this.i18n.name({ nameEn: record.nameEn ?? record.name ?? '', nameAr: record.nameAr });
  }
  muscleName(id?: string | null): string {
    const muscle = this.muscles().find((x) => x.id === id);
    return muscle ? this.localName(muscle) : '—';
  }
  /** A food's base unit in the interface language: g / جم, ml / مل, or the piece's name. */
  baseUnitLabel(item: CatalogItem): string {
    if (item.baseUnit === 'Piece') return this.i18n.name({ nameEn: item.pieceNameEn ?? item.baseUnitLabel ?? '', nameAr: item.pieceNameAr });
    return this.i18n.translate(item.baseUnit === 'Milliliter' ? 'unit.ml' : 'unit.g');
  }
  /** The editor's base unit: g / ml, or the piece's name (pieces when unnamed). */
  editorUnit(): string {
    const editor = this.editor();
    if (editor.baseUnit === 'Piece') return editor.pieceNameEn || this.i18n.translate('catalog.piecesUnit');
    return this.i18n.translate(editor.baseUnit === 'Gram' ? 'unit.g' : 'unit.ml');
  }
  addUnit(): void {
    this.editor().units.push({ id: null, nameEn: '', nameAr: '', baseQuantity: 1 });
  }
  removeUnit(index: number): void {
    this.editor().units.splice(index, 1);
  }
  /** What "per 100" means for the chosen base unit. */
  per100(): string {
    const editor = this.editor();
    return editor.baseUnit === 'Piece'
      ? this.i18n.translate('catalog.per100Pieces', { piece: editor.pieceNameEn || this.i18n.translate('catalog.piece') })
      : this.i18n.translate('catalog.per100', { unit: this.editorUnit() });
  }
  categoryName(id?: string): string {
    const category = this.categories().find((x) => x.id === id);
    return category ? this.localName(category) : '—';
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
      difficulty: '' as string,
      primaryMuscleGroupId: '',
      secondaryMuscleGroupIds: new Set<string>(),
      equipmentIds: new Set<string>(),
      instructionsEn: '',
      instructionsAr: '',
      coachingCuesEn: '',
      coachingCuesAr: '',
      commonMistakesEn: '',
      commonMistakesAr: '',
    };
  }
}

/** An empty number input means "not recorded". */
function nullable(value: number | string | null | undefined): number | null {
  return value === null || value === undefined || value === '' ? null : Number(value);
}
