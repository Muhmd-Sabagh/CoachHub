import { ChangeDetectionStrategy, Component, OnDestroy, OnInit } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { finalize, forkJoin } from 'rxjs';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import { CatalogItem } from '../catalog/catalog.models';
import {
  PrescribedSet,
  SetGroupType,
  formatTempo,
  groupLabels,
  nextSet,
  parseTempo,
  setTypes,
  summarizeSets,
  workoutBlocks,
} from './workout-math';
import { CatalogService } from '../catalog/catalog.service';
import { Client, ClientsService } from '../clients/clients.service';
import {
  NutritionFood,
  NutritionTotals,
  countedRows,
  difference,
  macroCalories,
  mealTypes,
  rowTotals,
  sumTotals,
} from './nutrition-math';
import {
  ActivityLevel,
  CalculatorStore,
  PlanType,
  PlanVersion,
  PlansService,
  SavedPlan,
  toEditorInput,
} from './plans.service';
const id = () => crypto.randomUUID();
@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager, selector: 'app-plans', templateUrl: './plans.component.html', standalone: false })
export class PlansComponent implements OnInit, OnDestroy {
  page: PagedResult<SavedPlan> = emptyPage();
  search = '';
  clientSearch = '';
  type = '';
  loading = false;
  saving = false;
  error = '';
  editorOpen = false;
  editorType: PlanType = 'Diet';
  editingId: string | null = null;
  editor: any = null;
  /** The version shown in the editor; archived versions open read-only (D4). */
  version: PlanVersion | null = null;
  currentVersionNumber: number | null = null;
  history: PlanVersion[] = [];
  historyOpen = false;
  clients: Client[] = [];
  foods: CatalogItem[] = [];
  exercises: CatalogItem[] = [];
  pdfUrl: SafeResourceUrl | null = null;
  private pdfObjectUrl: string | null = null;
  readonly mealTypes = mealTypes;
  readonly activityLevels: { value: ActivityLevel; label: string }[] = [
    { value: 'Sedentary', label: 'Sedentary' },
    { value: 'LightlyActive', label: 'Lightly active' },
    { value: 'ModeratelyActive', label: 'Moderately active' },
    { value: 'VeryActive', label: 'Very active' },
    { value: 'ExtraActive', label: 'Extra active' },
  ];
  /** Uses the coach's activity factors, deficit/surplus and g/kg defaults (§14, §49). */
  calculator = {
    age: 30,
    sex: 'Male',
    weightKg: 75,
    heightCm: 175,
    activityLevel: 'ModeratelyActive' as ActivityLevel,
    goal: 'Maintain',
  };
  targetsMessage = '';
  constructor(
    private data: PlansService,
    private clientData: ClientsService,
    private catalogs: CatalogService,
    readonly calc: CalculatorStore,
    private sanitizer: DomSanitizer,
  ) {}
  ngOnInit() {
    forkJoin({
      clients: this.clientData.list({ pageSize: 100, isActive: true }),
      foods: this.catalogs.list('foods', { pageSize: 100, isActive: true }),
      exercises: this.catalogs.list('exercises', { pageSize: 100, isActive: true }),
    }).subscribe((x) => {
      this.clients = x.clients.items;
      this.foods = x.foods.items;
      this.exercises = x.exercises.items;
    });
    this.load();
  }
  ngOnDestroy() {
    if (this.pdfObjectUrl) URL.revokeObjectURL(this.pdfObjectUrl);
  }
  load(pageNumber = 1) {
    this.loading = true;
    this.data
      .list({
        pageNumber,
        pageSize: 10,
        planName: this.search.trim(),
        clientName: this.clientSearch.trim(),
        planType: this.type,
      })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({ next: (x) => (this.page = x), error: (e) => (this.error = apiErrorMessage(e)) });
  }
  reset() {
    this.search = '';
    this.clientSearch = '';
    this.type = '';
    this.load();
  }
  get readOnly() {
    return !!this.version?.isReadOnly;
  }
  create(type: PlanType) {
    this.editorType = type;
    this.editingId = null;
    this.version = null;
    this.currentVersionNumber = null;
    this.historyOpen = false;
    this.editor = type === 'Diet' ? this.newDiet() : this.newWorkout();
    this.editorOpen = true;
  }
  edit(p: SavedPlan) {
    this.editorType = p.planType;
    this.historyOpen = false;
    this.data.get(p.planType, p.id).subscribe({
      next: (x) => {
        this.editingId = p.id;
        this.show(x);
        this.editorOpen = true;
      },
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  /**
   * D4: the coach chooses how to save. "inPlace" updates the current version; "newVersion" archives
   * it (read-only from then on) and creates the next one. New plans start at version 1.
   */
  save(mode: 'inPlace' | 'newVersion' = 'inPlace') {
    if (this.readOnly) return;
    if (this.hasInvalidTempo()) {
      this.error = 'Write tempo as four numbers, for example 3-1-1-0.';
      return;
    }
    this.saving = true;
    this.error = '';
    this.normalizeOrders();
    const request = !this.editingId
      ? this.data.create(this.editorType, this.editor)
      : mode === 'newVersion'
        ? this.data.saveAsNewVersion(this.editorType, this.editingId, this.editor)
        : this.data.saveInPlace(this.editorType, this.editingId, this.editor);
    request.pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => {
        this.editorOpen = false;
        this.load();
      },
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  openHistory() {
    if (!this.editingId) return;
    this.data.versions(this.editorType, this.editingId).subscribe({
      next: (x) => {
        this.history = x;
        this.historyOpen = true;
      },
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  viewVersion(v: PlanVersion) {
    if (!this.editingId) return;
    const request = v.isCurrent
      ? this.data.get(this.editorType, this.editingId)
      : this.data.version(this.editorType, this.editingId, v.versionNumber);
    request.subscribe({
      next: (x) => {
        this.show(x);
        this.historyOpen = false;
      },
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  backToCurrent() {
    if (!this.editingId) return;
    this.data.get(this.editorType, this.editingId).subscribe({
      next: (x) => this.show(x),
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  /** Brings an archived version back as the next version; the archive itself stays untouched. */
  restoreAsNewVersion() {
    if (!this.editingId || !this.version?.isReadOnly) return;
    this.saving = true;
    this.data
      .saveAsNewVersion(this.editorType, this.editingId, { ...this.editor, status: 'Active' })
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: (x) => {
          this.show(x);
          this.load();
        },
        error: (e) => (this.error = apiErrorMessage(e)),
      });
  }
  previewVersion(v: PlanVersion, language = 'English') {
    if (this.editingId) this.openPdf(this.editorType, this.editingId, language, v.versionNumber);
  }
  private show(plan: any) {
    this.editor = toEditorInput(plan);
    this.version = plan.revision ?? null;
    this.currentVersionNumber = plan.currentVersionNumber ?? null;
  }
  copy(p: SavedPlan) {
    const name = prompt('Copy name', `${p.nameEn} Copy`);
    if (name)
      this.data
        .copy(p.planType, p.id, {
          nameEn: name,
          nameAr: p.nameAr ?? null,
          clientId: p.clientId ?? null,
        })
        .subscribe(() => this.load());
  }
  assign(p: SavedPlan) {
    const clientId = prompt('Client ID (leave blank to unassign)', p.clientId ?? '');
    if (clientId !== null)
      this.data.assign(p.planType, p.id, clientId || null).subscribe(() => this.load());
  }
  remove(p: SavedPlan) {
    if (confirm(`Delete ${p.nameEn}?`))
      this.data
        .delete(p.planType, p.id)
        .subscribe({ next: () => this.load(), error: (e) => (this.error = apiErrorMessage(e)) });
  }
  preview(p: SavedPlan, language = 'English') {
    this.openPdf(p.planType, p.id, language);
  }
  private openPdf(type: PlanType, planId: string, language: string, version?: number) {
    this.data.pdf(type, planId, 'preview', language, version).subscribe((blob) => {
      if (this.pdfObjectUrl) URL.revokeObjectURL(this.pdfObjectUrl);
      this.pdfObjectUrl = URL.createObjectURL(blob);
      this.pdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.pdfObjectUrl);
    });
  }
  download(p: SavedPlan, language = 'English') {
    this.data.pdf(p.planType, p.id, 'download', language).subscribe((blob) => {
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `${p.nameEn}.pdf`;
      a.click();
      URL.revokeObjectURL(a.href);
    });
  }
  closePdf() {
    this.pdfUrl = null;
    if (this.pdfObjectUrl) {
      URL.revokeObjectURL(this.pdfObjectUrl);
      this.pdfObjectUrl = null;
    }
  }
  calculate() {
    this.data.calculate(this.calculator).subscribe({
      next: (x) => this.calc.result.set(x),
      error: (e) => (this.error = apiErrorMessage(e)),
    });
  }
  addNote() {
    this.editor.notes.push({ id: id(), text: '', order: this.editor.notes.length, isActive: true });
  }
  addVariant() {
    this.editor.variants.push({
      id: id(),
      nameEn: 'Plan variant',
      nameAr: null,
      order: this.editor.variants.length,
      isActiveForPdf: true,
      notes: null,
      meals: [this.newMeal()],
      replacementGroups: [],
    });
  }
  addMeal(v: any) {
    v.meals.push(this.newMeal('Snack'));
  }
  addFood(m: any) {
    m.foodItems.push({
      id: id(),
      foodItemId: '',
      foodUnitId: null,
      mealOptionId: m.options?.length ? m.options[m.options.length - 1].id : null,
      quantity: 100,
      order: m.foodItems.length,
      notes: null,
    });
  }
  /** A newly chosen food starts at one serving in its base unit (§5). */
  foodChanged(row: any) {
    const food = this.food(row.foodItemId);
    row.foodUnitId = null;
    if (food) row.quantity = food.servingSize;
  }
  food(foodId: string | null | undefined): NutritionFood | undefined {
    return this.foods.find((x) => x.id === foodId) as NutritionFood | undefined;
  }
  // Live totals (§6): recomputed as quantities change; the server's totals win on save.
  rowTotals(row: any): NutritionTotals {
    return rowTotals(this.food(row.foodItemId), row.quantity, row.foodUnitId);
  }
  /** With options (§13) a meal counts its first option, as the server does. */
  mealTotals(meal: any, optionId?: string): NutritionTotals {
    const rows = countedRows(meal.foodItems, meal.options, optionId);
    return sumTotals(rows.map((row: any) => this.rowTotals(row)));
  }
  /** Adds an option to a flexible meal; the first option takes the meal's existing foods. */
  addMealOption(meal: any) {
    meal.options ??= [];
    if (meal.options.length >= 10) return;
    const option = {
      id: id(),
      nameEn: `Option ${String.fromCharCode(65 + meal.options.length)}`,
      nameAr: null,
      order: meal.options.length,
    };
    meal.options.push(option);
    if (meal.options.length === 1) meal.foodItems.forEach((row: any) => (row.mealOptionId = option.id));
  }
  /** Removes an option with its foods; without options left, the meal's foods are plain again. */
  removeMealOption(meal: any, index: number) {
    const [option] = meal.options.splice(index, 1);
    meal.foodItems = meal.foodItems.filter((row: any) => row.mealOptionId !== option.id);
    if (!meal.options.length) meal.foodItems.forEach((row: any) => (row.mealOptionId = null));
  }
  /** Food rows a group can target: those of its target meal. */
  targetRows(variant: any, group: any): any[] {
    return variant.meals.find((m: any) => m.id === group.targetMealId)?.foodItems ?? [];
  }
  foodName(foodId: string): string {
    return this.foods.find((x) => x.id === foodId)?.nameEn ?? 'Food';
  }
  variantTotals(variant: any): NutritionTotals {
    return sumTotals(variant.meals.map((meal: any) => this.mealTotals(meal)));
  }
  mealCount(variant: any, snacks: boolean): number {
    return variant.meals.filter((meal: any) => (meal.type === 'Snack') === snacks).length;
  }
  difference(planned: number, target?: number | null) {
    return difference(planned, target);
  }
  targetMacroCalories(): number {
    const t = this.editor?.targets ?? {};
    return macroCalories(t.protein ?? 0, t.carbohydrates ?? 0, t.fat ?? 0);
  }
  /** Fills the version's targets from the assigned client's profile and macro mode (§4, §14, §15). */
  useClientTargets() {
    if (!this.editor?.clientId) return;
    this.targetsMessage = '';
    this.data.clientTargets(this.editor.clientId).subscribe({
      next: (x) => {
        this.editor.targets = {
          ...(this.editor.targets ?? {}),
          calories: x.goalCalories,
          protein: x.protein,
          carbohydrates: x.carbohydrates,
          fat: x.fat,
        };
        this.targetsMessage = `From the profile: TDEE ${x.maintenanceCalories} kcal, ${x.adjustmentPercent}% for the goal.`;
      },
      error: (e) => (this.targetsMessage = apiErrorMessage(e)),
    });
  }
  addReplacement(v: any) {
    const meal = v.meals[0];
    if (!meal) return;
    // §7: alternatives usually target one food; flexible ones get their quantity calculated on save.
    v.replacementGroups.push({
      id: id(),
      targetMealId: meal.id,
      targetMealFoodItemId: meal.foodItems[0]?.id ?? null,
      title: 'Alternative',
      order: v.replacementGroups.length,
      mode: 'Flexible',
      options: [],
    });
    this.addReplacementOption(v.replacementGroups[v.replacementGroups.length - 1]);
  }
  addReplacementOption(g: any) {
    g.options.push({
      id: id(),
      replacementFoodItemId: null,
      replacementMealId: null,
      foodUnitId: null,
      quantity: g.mode === 'Flexible' ? null : 100,
      order: g.options.length,
      notes: null,
    });
  }
  addDay() {
    this.editor.days.push({
      id: id(),
      nameEn: `Day ${this.editor.days.length + 1}`,
      nameAr: null,
      subtitle: null,
      notes: null,
      order: this.editor.days.length,
      exercises: [],
      setGroups: [],
    });
  }
  readonly workoutBlocks = workoutBlocks;
  readonly setTypes = setTypes;
  /** A new exercise starts in the block of the day's last exercise, with three working sets (§16). */
  addExercise(d: any) {
    const last = d.exercises[d.exercises.length - 1];
    const first = nextSet(id());
    const sets = [first, nextSet(id(), first), nextSet(id(), nextSet(id(), first))];
    d.exercises.push({
      id: id(),
      exerciseId: '',
      order: d.exercises.length,
      block: last?.block ?? 'Main',
      setGroupId: null,
      notes: null,
      sets: sets.map((s, order) => ({ ...s, order })),
    });
  }
  addSet(e: any) {
    e.sets.push(nextSet(id(), e.sets[e.sets.length - 1]));
  }
  /** Supersets take two exercises, giant sets three or more (§16); exercises join them in the editor. */
  addSetGroup(d: any, type: SetGroupType) {
    d.setGroups ??= [];
    d.setGroups.push({ id: id(), type, restSeconds: type === 'Superset' ? 90 : 120, notes: null });
  }
  removeSetGroup(d: any, index: number) {
    const [group] = d.setGroups.splice(index, 1);
    d.exercises.forEach((e: any) => {
      if (e.setGroupId === group.id) e.setGroupId = null;
    });
  }
  groupLabel(d: any, exerciseRowId: string): string {
    return groupLabels(d.exercises).get(exerciseRowId) ?? '';
  }
  groupName(d: any, group: any): string {
    const first = d.exercises.find((e: any) => e.setGroupId === group.id);
    const letter = first ? (groupLabels(d.exercises).get(first.id) ?? '').charAt(0) : '?';
    return `${letter} · ${group.type === 'Superset' ? 'Superset' : 'Giant set'}`;
  }
  setSummary(e: any): string {
    return summarizeSets(e.sets ?? []);
  }
  tempoText(s: any): string {
    return s.tempoText ?? formatTempo(s.tempo);
  }
  /** Tempo is typed as "3-1-1-0"; unreadable text is kept so the coach can fix it, and blocks saving. */
  setTempo(s: any, text: string) {
    s.tempoText = text;
    const tempo = parseTempo(text);
    s.tempoInvalid = tempo === undefined;
    if (tempo !== undefined) s.tempo = tempo;
  }
  hasInvalidTempo(): boolean {
    return this.editorType === 'Workout' && (this.editor?.days ?? []).some((d: any) =>
      d.exercises.some((e: any) => (e.sets ?? []).some((s: PrescribedSet & { tempoInvalid?: boolean }) => s.tempoInvalid)));
  }
  removeFrom(items: any[], index: number) {
    items.splice(index, 1);
  }
  move(items: any[], index: number, delta: number) {
    const j = index + delta;
    if (j < 0 || j >= items.length) return;
    [items[index], items[j]] = [items[j], items[index]];
    this.normalizeOrders();
  }
  name(items: any[], itemId: string) {
    return items.find((x) => x.id === itemId)?.nameEn ?? 'Select';
  }
  private normalizeOrders() {
    // An empty date input yields '', which is not a date; an open program end is null.
    this.editor.startDate ||= null;
    this.editor.endDate ||= null;
    this.editor.notes.forEach((x: any, i: number) => (x.order = i));
    if (this.editorType === 'Diet')
      this.editor.variants.forEach((v: any, vi: number) => {
        v.order = vi;
        v.meals.forEach((m: any, mi: number) => {
          m.order = mi;
          m.foodItems.forEach((f: any, fi: number) => (f.order = fi));
          (m.options ?? []).forEach((o: any, oi: number) => (o.order = oi));
        });
        v.replacementGroups.forEach((g: any, gi: number) => {
          g.order = gi;
          g.options.forEach((o: any, oi: number) => (o.order = oi));
        });
      });
    else
      this.editor.days.forEach((d: any, di: number) => {
        d.order = di;
        d.exercises.forEach((e: any, ei: number) => {
          e.order = ei;
          (e.sets ?? []).forEach((s: any, si: number) => (s.order = si));
        });
      });
  }
  private newMeal(type = 'Breakfast') {
    return {
      id: id(),
      nameEn: type === 'Snack' ? 'Snack' : 'Meal',
      nameAr: null,
      type,
      order: 0,
      notes: null,
      options: [],
      foodItems: [],
    };
  }
  private newDiet() {
    return {
      nameEn: '',
      nameAr: null,
      clientId: null,
      startDate: null,
      endDate: null,
      status: 'Active',
      targets: {},
      notes: [],
      variants: [
        {
          id: id(),
          nameEn: 'Main plan',
          nameAr: null,
          order: 0,
          isActiveForPdf: true,
          notes: null,
          meals: [this.newMeal()],
          replacementGroups: [],
        },
      ],
    };
  }
  private newWorkout() {
    return {
      nameEn: '',
      nameAr: null,
      clientId: null,
      startDate: null,
      endDate: null,
      status: 'Active',
      notes: [],
      days: [
        {
          id: id(),
          nameEn: 'Day 1',
          nameAr: null,
          subtitle: null,
          notes: null,
          order: 0,
          exercises: [],
          setGroups: [],
        },
      ],
    };
  }
}
