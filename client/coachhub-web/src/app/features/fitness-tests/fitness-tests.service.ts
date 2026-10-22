import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';

export type TestKind = 'Mobility' | 'Strength' | 'Endurance';
export type TestUnit = 'Kilograms' | 'Repetitions' | 'Seconds' | 'Meters' | 'Centimeters' | 'Degrees' | 'Points' | 'BeatsPerMinute';
export type TestClassification = 'Good' | 'NeedsImprovement' | 'Poor';
export type TestRecorder = 'Client' | 'Coach';
export type TestTrend = 'Improved' | 'Declined' | 'Unchanged';
export type TestScope = 'All' | 'Platform' | 'Custom';

export const testKinds: TestKind[] = ['Mobility', 'Strength', 'Endurance'];
export const testUnits: TestUnit[] = ['Kilograms', 'Repetitions', 'Seconds', 'Meters', 'Centimeters', 'Degrees', 'Points', 'BeatsPerMinute'];
export const classifications: TestClassification[] = ['Good', 'NeedsImprovement', 'Poor'];
export const unitLabels: Record<TestUnit, string> = {
  Kilograms: 'kg', Repetitions: 'reps', Seconds: 's', Meters: 'm', Centimeters: 'cm', Degrees: '°', Points: 'pts', BeatsPerMinute: 'bpm',
};
export const classificationLabels: Record<TestClassification, string> = { Good: 'Good', NeedsImprovement: 'Needs improvement', Poor: 'Poor' };

export interface TestDefinition {
  id: string;
  kind: TestKind;
  nameEn: string;
  nameAr: string | null;
  unit: TestUnit;
  higherIsBetter: boolean;
  goodThreshold: number | null;
  poorThreshold: number | null;
  instructionsEn: string | null;
  instructionsAr: string | null;
  youTubeUrl: string | null;
  isActive: boolean;
  coachId: string | null;
  isCustom: boolean;
}
export type TestDefinitionInput = Omit<TestDefinition, 'id' | 'coachId' | 'isCustom'>;
export interface TestQuery { pageNumber?: number; searchTerm?: string; kind?: TestKind | ''; scope?: TestScope; isActive?: boolean | null; }
export interface TestResult {
  id: string;
  testDefinitionId: string;
  date: string;
  value: number;
  unit: TestUnit;
  classification: TestClassification | null;
  isClassificationOverridden: boolean;
  recordedBy: TestRecorder;
  notes: string | null;
  createdAt: string;
}
export interface TestPoint { date: string; value: number; }
export interface TestProgress {
  count: number;
  first: TestPoint | null;
  previous: TestPoint | null;
  current: TestPoint | null;
  best: TestPoint | null;
  changeFromPrevious: number | null;
  changeFromFirst: number | null;
  percentChangeFromFirst: number | null;
  trend: TestTrend | null;
}
export interface ClientTest {
  testDefinitionId: string;
  kind: TestKind;
  nameEn: string;
  nameAr: string | null;
  unit: TestUnit;
  higherIsBetter: boolean;
  definition: TestDefinition | null;
  isAssigned: boolean;
  assignedAt: string | null;
  progress: TestProgress;
  results: TestResult[];
}
export interface ClientTests { clientId: string; tests: ClientTest[]; }
export interface TestResultInput { testDefinitionId: string; date: string; value: number; notes?: string | null; }

/** "+3 reps", "-12 s"; null when there is nothing to compare. */
export function changeText(change: number | null, unit: TestUnit): string | null {
  if (change === null || change === undefined) return null;
  const rounded = Math.round(change * 100) / 100;
  return `${rounded > 0 ? '+' : ''}${rounded} ${unitLabels[unit]}`;
}

/** The direction and cut-offs of a test in words, for example "Higher is better · Good from 30 · Poor below 15". */
export function cutOffText(test: Pick<TestDefinition, 'higherIsBetter' | 'goodThreshold' | 'poorThreshold' | 'unit'>): string {
  const direction = test.higherIsBetter ? 'Higher is better' : 'Lower is better';
  if (test.goodThreshold === null || test.poorThreshold === null) return `${direction} · classified by the coach`;
  const unit = unitLabels[test.unit];
  return test.higherIsBetter
    ? `${direction} · Good from ${test.goodThreshold} ${unit} · Poor below ${test.poorThreshold} ${unit}`
    : `${direction} · Good up to ${test.goodThreshold} ${unit} · Poor above ${test.poorThreshold} ${unit}`;
}

/** A test unit's short label in the interface language (P19), for example "kg" / "كغ". */
export function localUnit(unit: TestUnit, i18n: I18nService): string {
  return i18n.translate(unitKeys[unit]);
}

const unitKeys: Record<TestUnit, TranslationKey> = {
  Kilograms: 'unit.kg', Repetitions: 'unit.reps', Seconds: 'unit.s', Meters: 'unit.m', Centimeters: 'unit.cm',
  Degrees: 'unit.degrees', Points: 'unit.points', BeatsPerMinute: 'unit.bpm',
};

/** cutOffText in the interface language (P19, client area); the staff screens move to it in P21. */
export function localCutOffText(test: Pick<TestDefinition, 'higherIsBetter' | 'goodThreshold' | 'poorThreshold' | 'unit'>, i18n: I18nService): string {
  const direction = i18n.translate(test.higherIsBetter ? 'tests.higherBetter' : 'tests.lowerBetter');
  if (test.goodThreshold === null || test.poorThreshold === null) return i18n.translate('tests.classifiedByCoach', { direction });
  return i18n.translate(test.higherIsBetter ? 'tests.cutOffsHigher' : 'tests.cutOffsLower',
    { direction, good: test.goodThreshold, poor: test.poorThreshold, unit: localUnit(test.unit, i18n) });
}

/** "+3 reps" in the interface language (P19); null when there is nothing to compare. */
export function localChangeText(change: number | null, unit: TestUnit, i18n: I18nService): string | null {
  if (change === null || change === undefined) return null;
  const rounded = Math.round(change * 100) / 100;
  return `${rounded > 0 ? '+' : ''}${i18n.formatNumber(rounded, '1.0-2')} ${localUnit(unit, i18n)}`;
}

/** The test library (§34–§36) and a client's tests: the coach under /clients/{id}, the client under /me. */
@Injectable({ providedIn: 'root' })
export class FitnessTestsService {
  private readonly api = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  list(query: TestQuery = {}) {
    let params = new HttpParams().set('pageNumber', query.pageNumber ?? 1).set('pageSize', 100);
    if (query.searchTerm) params = params.set('searchTerm', query.searchTerm);
    if (query.kind) params = params.set('kind', query.kind);
    if (query.scope && query.scope !== 'All') params = params.set('scope', query.scope);
    if (query.isActive !== null && query.isActive !== undefined) params = params.set('isActive', query.isActive);
    return this.http.get<PagedResult<TestDefinition>>(`${this.api}/fitness-tests`, { params });
  }

  create(input: TestDefinitionInput) { return this.http.post<TestDefinition>(`${this.api}/fitness-tests`, input); }
  update(id: string, input: TestDefinitionInput) { return this.http.put<TestDefinition>(`${this.api}/fitness-tests/${id}`, input); }
  delete(id: string) { return this.http.delete<void>(`${this.api}/fitness-tests/${id}`); }

  /** The client's tests; without a client, the signed-in client's own. */
  clientTests(clientId?: string) { return this.http.get<ClientTests>(this.base(clientId)); }
  assign(clientId: string, testId: string) { return this.http.put<void>(`${this.base(clientId)}/assignments/${testId}`, null); }
  unassign(clientId: string, testId: string) { return this.http.delete<void>(`${this.base(clientId)}/assignments/${testId}`); }
  record(input: TestResultInput, clientId?: string) { return this.http.post<TestResult>(`${this.base(clientId)}/results`, input); }
  change(resultId: string, input: Omit<TestResultInput, 'testDefinitionId'>, clientId?: string) {
    return this.http.put<TestResult>(`${this.base(clientId)}/results/${resultId}`, input);
  }
  deleteResult(resultId: string, clientId?: string) { return this.http.delete<void>(`${this.base(clientId)}/results/${resultId}`); }
  classify(clientId: string, resultId: string, classification: TestClassification | null) {
    return this.http.put<TestResult>(`${this.base(clientId)}/results/${resultId}/classification`, { classification });
  }

  private base(clientId?: string) {
    return clientId ? `${this.api}/clients/${clientId}/fitness-tests` : `${this.api}/me/fitness-tests`;
  }
}
