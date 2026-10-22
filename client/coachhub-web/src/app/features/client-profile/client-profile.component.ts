import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, finalize } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { AssessmentsService, SubmissionSummary } from '../assessments/assessments.service';
import { CatalogService } from '../catalog/catalog.service';
import {
  ClientDetail, ClientProfile, ClientProfileInput, ClientStatus, ClientsService, NamedReference, clientStatuses, dietaryRestrictions,
} from '../clients/clients.service';
import { EnergyTargets, MacroMode, PlansService } from '../plans/plans.service';
import { ClientNutritionComponent } from '../nutrition-tracking/client-nutrition.component';
import { ClientTrainingComponent } from '../training-tracking/client-training.component';
import { ClientProgressComponent } from '../progress-tracking/client-progress.component';
import { CheckInsComponent } from '../check-ins/check-ins.component';
import { ClientTestsComponent } from '../fitness-tests/client-tests.component';
import { ChatThreadComponent } from '../chat/chat-thread.component';
import { ClientFilesComponent } from '../client-files/client-files.component';
import { CoachCalendarComponent } from '../calendar/coach-calendar.component';
import { ClientReportsComponent } from '../reports/client-reports.component';
import { ReferenceDataService, ReferenceRecord } from '../settings/reference-data.service';

type Tab = 'overview' | 'profile' | 'workout' | 'nutrition' | 'diary' | 'progress' | 'measurements' | 'photos' | 'checkins' | 'tests' | 'calendar' | 'reports' | 'messages' | 'files';

/** A tab the coach sees (§40); later phases light up the ones marked with their phase. */
interface TabItem { id: Tab; label: string; arrivesIn?: string; }

/** Everything about one client in one place (§40), starting with the structured profile (§3). */
@Component({
  selector: 'app-client-profile', standalone: true, imports: [CommonModule, FormsModule, RouterLink, ClientNutritionComponent, ClientTrainingComponent, ClientProgressComponent, CheckInsComponent, ClientTestsComponent, ChatThreadComponent, ClientFilesComponent, CoachCalendarComponent, ClientReportsComponent], templateUrl: './client-profile.component.html' })
export class ClientProfileComponent implements OnInit {
  readonly tabs: TabItem[] = [
    { id: 'overview', label: 'Overview' }, { id: 'profile', label: 'Profile' },
    { id: 'workout', label: 'Training' }, { id: 'nutrition', label: 'Nutrition' },
    { id: 'diary', label: 'Food diary' }, { id: 'progress', label: 'Progress' },
    { id: 'measurements', label: 'Measurements' }, { id: 'photos', label: 'Photos' },
    { id: 'checkins', label: 'Check-ins' }, { id: 'tests', label: 'Tests' }, { id: 'calendar', label: 'Calendar' }, { id: 'reports', label: 'Reports' },
    { id: 'messages', label: 'Messages' }, { id: 'files', label: 'Files' },
  ];
  readonly statuses = clientStatuses;
  readonly restrictions = dietaryRestrictions;
  readonly goals = ['LoseFat', 'BuildMuscle', 'Recomposition', 'Maintain', 'Performance', 'GeneralHealth'];
  readonly activityLevels = ['Sedentary', 'LightlyActive', 'ModeratelyActive', 'VeryActive', 'ExtraActive'];
  readonly locations = ['Gym', 'Home', 'Outdoor', 'Mixed'];
  readonly tab = signal<Tab>('overview');
  readonly id = signal('');
  readonly detail = signal<ClientDetail | null>(null);
  readonly profile = signal<ClientProfile | null>(null);
  readonly form = signal<ClientProfileInput | null>(null);
  readonly equipment = signal<ReferenceRecord[]>([]);
  readonly chosenEquipment = signal(new Set<string>());
  readonly preferred = signal<NamedReference[]>([]);
  readonly disliked = signal<NamedReference[]>([]);
  readonly foodSearch = signal('');
  readonly foodResults = signal<NamedReference[]>([]);
  readonly submissions = signal<SubmissionSummary[]>([]);
  readonly statusChoice = signal<ClientStatus>('NeedsReview');
  readonly statusUntil = signal('');
  readonly saving = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  /** '' means the coach's defaults (§15). */
  readonly macroMode = signal<'' | MacroMode>('');
  readonly macro = signal(emptyMacro());
  readonly targets = signal<EnergyTargets | null>(null);
  readonly targetsError = signal('');

  constructor(
    route: ActivatedRoute,
    private readonly clients: ClientsService,
    private readonly references: ReferenceDataService,
    private readonly catalog: CatalogService,
    private readonly assessments: AssessmentsService,
    private readonly plans: PlansService,
  ) {
    this.id.set(route.snapshot.paramMap.get('id') ?? '');
  }

  ngOnInit(): void {
    this.loadDetail();
    this.clients.profile(this.id()).subscribe({ next: profile => this.showProfile(profile), error: e => (this.error.set(apiErrorMessage(e))) });
    this.references.list('equipment', { pageSize: 100, isActive: true }).subscribe(page => (this.equipment.set(page.items)));
    this.assessments.listSubmissions({ clientId: this.id(), pageSize: 50 }).subscribe(page => (this.submissions.set(page.items)));
  }

  open(tab: Tab): void { this.tab.set(tab); this.message.set(''); }
  arrivesIn(tab: Tab): string | undefined { return this.tabs.find(item => item.id === tab)?.arrivesIn; }

  invite(): void {
    this.run(this.clients.invite(this.id()), account => {
      this.detail.update(detail => detail && { ...detail, account });
      return 'Invitation sent. The link is valid for 7 days.';
    });
  }

  setStatus(): void {
    this.run(this.clients.setStatus(this.id(), this.statusChoice(), this.statusUntil() || null), client => {
      this.detail.update(detail => detail && { ...detail, client });
      return 'Status set.';
    });
  }

  clearStatus(): void {
    this.run(this.clients.clearStatus(this.id()), client => {
      this.detail.update(detail => detail && { ...detail, client });
      return 'Status returned to automatic.';
    });
  }

  toggleEquipment(id: string, on: boolean): void { if (on) this.chosenEquipment().add(id); else this.chosenEquipment().delete(id); }
  toggleRestriction(name: string, on: boolean): void {
    const form = this.form();
    if (!form) return;
    const set = new Set(form.dietaryRestrictions);
    if (on) set.add(name); else set.delete(name);
    form.dietaryRestrictions = [...set];
  }

  searchFoods(): void {
    const term = this.foodSearch().trim();
    if (!term) { this.foodResults.set([]); return; }
    this.catalog.list('foods', { searchTerm: term, pageSize: 10, isActive: true })
      .subscribe(page => (this.foodResults.set(page.items.map(item => ({ id: item.id, nameEn: item.nameEn, nameAr: item.nameAr })))));
  }

  addFood(food: NamedReference, list: 'preferred' | 'disliked'): void {
    // A food is either preferred or disliked, never both.
    this.preferred.set(this.preferred().filter(item => item.id !== food.id));
    this.disliked.set(this.disliked().filter(item => item.id !== food.id));
    (list === 'preferred' ? this.preferred() : this.disliked()).push(food);
  }

  removeFood(food: NamedReference): void {
    this.preferred.set(this.preferred().filter(item => item.id !== food.id));
    this.disliked.set(this.disliked().filter(item => item.id !== food.id));
  }

  saveProfile(): void {
    const form = this.form();
    if (!form) return;
    const macroMode = this.macroMode();
    const input: ClientProfileInput = {
      ...form,
      equipmentIds: [...this.chosenEquipment()],
      preferredFoodIds: this.preferred().map(food => food.id),
      dislikedFoodIds: this.disliked().map(food => food.id),
      bedtime: time(form.bedtime), wakeTime: time(form.wakeTime),
      dateOfBirth: form.dateOfBirth || null,
      macros: macroMode ? { mode: macroMode, ...numbers(this.macro()) } : null,
    };
    this.run(this.clients.saveProfile(this.id(), input), profile => { this.showProfile(profile); this.calculateTargets(); return 'Profile saved.'; });
  }

  /** The client's calorie and macro targets from the saved profile and the coach's settings (§14, §15). */
  calculateTargets(): void {
    this.targetsError.set('');
    this.plans.clientTargets(this.id()).subscribe({
      next: targets => (this.targets.set(targets)),
      error: e => { this.targets.set(null); this.targetsError.set(apiErrorMessage(e)); },
    });
  }

  private loadDetail(): void {
    this.clients.get(this.id()).subscribe({ next: detail => (this.detail.set(detail)), error: e => (this.error.set(apiErrorMessage(e))) });
  }

  private showProfile(profile: ClientProfile): void {
    this.profile.set(profile);
    this.form.set({
      dateOfBirth: profile.dateOfBirth, sex: profile.sex, heightCm: profile.heightCm, startingWeightKg: profile.startingWeightKg,
      startingBodyFatPercent: profile.startingBodyFatPercent, goal: profile.goal, activityLevel: profile.activityLevel,
      trainingDaysPerWeek: profile.trainingDaysPerWeek, averageDailySteps: profile.averageDailySteps,
      trainingLocation: profile.trainingLocation, trainingPreferences: profile.trainingPreferences,
      foodPreferences: profile.foodPreferences, preferredFoodsNote: profile.preferredFoodsNote,
      dislikedFoodsNote: profile.dislikedFoodsNote, dietaryRestrictions: [...profile.dietaryRestrictions],
      dietaryNotes: profile.dietaryNotes, preferredMealCount: profile.preferredMealCount,
      bedtime: profile.bedtime?.slice(0, 5) ?? null, wakeTime: profile.wakeTime?.slice(0, 5) ?? null,
      lifestyleNotes: profile.lifestyleNotes, equipmentIds: [], preferredFoodIds: [], dislikedFoodIds: [],
      macros: profile.macros,
    });
    this.macroMode.set(profile.macros?.mode ?? '');
    this.macro.set({ ...emptyMacro(), ...(profile.macros ?? {}) });
    this.chosenEquipment.set(new Set(profile.equipment.map(item => item.id)));
    this.preferred.set([...profile.preferredFoods]);
    this.disliked.set([...profile.dislikedFoods]);
  }

  private run<T>(request: Observable<T>, done: (value: T) => string): void {
    this.saving.set(true); this.error.set(''); this.message.set('');
    request.pipe(finalize(() => (this.saving.set(false)))).subscribe({
      next: value => (this.message.set(done(value))),
      error: e => (this.error.set(apiErrorMessage(e))),
    });
  }
}

/** "23:30" from a time input becomes "23:30:00" for the API; empty stays null. */
export function time(value: string | null): string | null {
  if (!value) return null;
  return value.length === 5 ? `${value}:00` : value;
}

function emptyMacro() {
  return {
    proteinGramsPerKg: null as number | null, fatGramsPerKg: null as number | null,
    proteinPercent: null as number | null, carbohydratePercent: null as number | null, fatPercent: null as number | null,
    proteinGrams: null as number | null, fatGrams: null as number | null,
  };
}

/** Empty number inputs are "not set"; the API keeps only the chosen mode's values. */
function numbers(values: ReturnType<typeof emptyMacro>) {
  const clean = (value: number | null | string) => (value === null || value === '' ? null : Number(value));
  return Object.fromEntries(Object.entries(values).map(([key, value]) => [key, clean(value)]));
}
