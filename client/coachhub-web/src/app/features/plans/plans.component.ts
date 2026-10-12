import { Component, OnDestroy, OnInit } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { finalize, forkJoin } from 'rxjs';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import { CatalogItem } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';
import { Client, ClientsService } from '../clients/clients.service';
import {
  CalculatorStore,
  PlanType,
  PlanVersion,
  PlansService,
  SavedPlan,
  toEditorInput,
} from './plans.service';
const id = () => crypto.randomUUID();
@Component({ selector: 'app-plans', templateUrl: './plans.component.html', standalone: false })
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
  calculator = {
    age: 30,
    sex: 'Male',
    weightKg: 75,
    heightCm: 175,
    activityFactor: 1.55,
    goal: 'Maintain',
    proteinGramsPerKg: 2,
    fatCaloriesPercent: 0.25,
  };
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
    v.meals.push(this.newMeal());
  }
  addFood(m: any) {
    m.foodItems.push({
      id: id(),
      foodItemId: '',
      quantity: 100,
      order: m.foodItems.length,
      notes: null,
    });
  }
  addReplacement(v: any) {
    const meal = v.meals[0];
    if (!meal) return;
    v.replacementGroups.push({
      id: id(),
      targetMealId: meal.id,
      targetMealFoodItemId: null,
      title: 'Alternative',
      order: v.replacementGroups.length,
      options: [
        {
          id: id(),
          replacementFoodItemId: null,
          replacementMealId: null,
          quantity: 100,
          order: 0,
          notes: null,
        },
      ],
    });
  }
  addReplacementOption(g: any) {
    g.options.push({
      id: id(),
      replacementFoodItemId: null,
      replacementMealId: null,
      quantity: 100,
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
    });
  }
  addExercise(d: any) {
    d.exercises.push({
      id: id(),
      exerciseId: '',
      order: d.exercises.length,
      sets: '3',
      repetitions: '10',
      rest: '60 sec',
      tempo: null,
      rpeRir: null,
      notes: null,
    });
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
        });
        v.replacementGroups.forEach((g: any, gi: number) => {
          g.order = gi;
          g.options.forEach((o: any, oi: number) => (o.order = oi));
        });
      });
    else
      this.editor.days.forEach((d: any, di: number) => {
        d.order = di;
        d.exercises.forEach((e: any, ei: number) => (e.order = ei));
      });
  }
  private newMeal() {
    return { id: id(), nameEn: 'Meal', nameAr: null, order: 0, notes: null, foodItems: [] };
  }
  private newDiet() {
    return {
      nameEn: '',
      nameAr: null,
      clientId: null,
      startDate: null,
      endDate: null,
      status: 'Active',
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
        },
      ],
    };
  }
}
