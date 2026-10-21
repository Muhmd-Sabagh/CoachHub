import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NutritionPeriod } from '../nutrition-tracking/nutrition-tracking.service';
import { PersonalRecord, TrainingPeriod } from '../training-tracking/training-tracking.service';

export type EntrySource = 'Manual' | 'AppleHealth' | 'HealthConnect' | 'Wearable';
export type MeasurementSite = 'Waist' | 'Chest' | 'Arm' | 'Thigh' | 'Hip' | 'Neck' | 'Calf';
export type PhotoPose = 'Front' | 'Side' | 'Back' | 'Custom';

export const measurementSites: MeasurementSite[] = ['Waist', 'Chest', 'Arm', 'Thigh', 'Hip', 'Neck', 'Calf'];
export const photoPoses: PhotoPose[] = ['Front', 'Side', 'Back', 'Custom'];

/** Shown wherever body fat appears (§27). */
export const bodyFatNotice = 'Body fat is an estimate to guide your coaching. It is not a medical diagnosis.';

export interface SeriesPoint { date: string; value: number; }
export interface SeriesSummary {
  start: SeriesPoint | null;
  current: SeriesPoint | null;
  lowest: SeriesPoint | null;
  highest: SeriesPoint | null;
  change: number | null;
  changePercent: number | null;
  count: number;
  weeks: { weekStart: string; average: number; count: number }[];
}
export interface SeriesEntry { id: string; date: string; value: number; source: EntrySource; notes: string | null; }
export interface Series { summary: SeriesSummary; entries: SeriesEntry[]; }
export interface BodyProgress {
  from: string;
  to: string;
  weight: Series;
  bodyFat: Series;
  measurements: (Series & { site: MeasurementSite })[];
  profileStartingWeightKg: number | null;
  profileStartingBodyFatPercent: number | null;
}
export interface LifestyleTargets { steps: number | null; waterMl: number | null; sleepHours: number | null; }
export interface LifestyleTargetsResponse { client: LifestyleTargets; coachDefaults: LifestyleTargets; effective: LifestyleTargets; }
export interface SleepEntry { id: string; date: string; hours: number; quality: number | null; bedtime: string | null; wakeTime: string | null; source: EntrySource; notes: string | null; }
export interface StepsEntry { id: string; date: string; steps: number; source: EntrySource; notes: string | null; }
export interface WaterEntry { id: string; date: string; amountMl: number; source: EntrySource; notes: string | null; createdAt: string; }
export interface LifestyleDay {
  date: string;
  sleep: SleepEntry | null;
  steps: StepsEntry | null;
  waterMl: number;
  water: WaterEntry[];
  sleepCompletionPercent: number | null;
  stepsCompletionPercent: number | null;
  waterCompletionPercent: number | null;
}
export interface LifestyleSummary {
  start: string;
  end: string;
  sleepNights: number;
  averageSleepHours: number | null;
  averageSleepQuality: number | null;
  sleepGoalNights: number;
  sleepGoalPercent: number | null;
  stepsDays: number;
  averageSteps: number | null;
  stepsGoalDays: number;
  stepsGoalPercent: number | null;
  waterDays: number;
  averageWaterMl: number | null;
  waterGoalDays: number;
  waterGoalPercent: number | null;
}
export interface Lifestyle {
  from: string;
  to: string;
  targets: LifestyleTargets;
  days: LifestyleDay[];
  summary: LifestyleSummary;
  weeks: LifestyleSummary[];
}
export interface ProgressPhoto { id: string; date: string; pose: PhotoPose; label: string | null; mediaId: string; notes: string | null; createdAt: string; }
export interface ProgressDashboard {
  from: string;
  to: string;
  body: {
    weight: SeriesSummary;
    bodyFat: SeriesSummary;
    measurements: { site: MeasurementSite; summary: SeriesSummary }[];
    photoCount: number;
    firstPhoto: ProgressPhoto | null;
    latestPhoto: ProgressPhoto | null;
  };
  nutrition: NutritionPeriod;
  training: { summary: TrainingPeriod; recentRecords: PersonalRecord[]; strength: PersonalRecord[] };
  lifestyle: LifestyleSummary;
  targets: LifestyleTargets;
}
export interface SleepInput { hours?: number | null; quality?: number | null; bedtime?: string | null; wakeTime?: string | null; notes?: string | null; }

/**
 * Body (§24–§27) and lifestyle (§31–§33) tracking, progress photos and the progress dashboard (§37). The client
 * writes under /me (the account decides whose data it is); coaches read a client's and set their targets.
 */
@Injectable({ providedIn: 'root' })
export class ProgressTrackingService {
  private readonly me = `${environment.apiBaseUrl}/me/progress`;
  constructor(private readonly http: HttpClient) {}

  dashboard(from: string, to: string, clientId?: string) {
    return this.http.get<ProgressDashboard>(`${this.base(clientId)}/dashboard`, { params: { from, to } });
  }
  body(from: string, to: string, clientId?: string) {
    return this.http.get<BodyProgress>(`${this.base(clientId)}/body`, { params: { from, to } });
  }
  lifestyle(from: string, to: string, clientId?: string) {
    return this.http.get<Lifestyle>(`${this.base(clientId)}/lifestyle`, { params: { from, to } });
  }
  photos(clientId?: string) {
    return this.http.get<ProgressPhoto[]>(`${this.base(clientId)}/photos`);
  }
  targets(clientId?: string) {
    return this.http.get<LifestyleTargetsResponse>(`${this.base(clientId)}/targets`);
  }
  /** Coach only: the client's daily targets; null falls back to the coach's default. */
  setTargets(clientId: string, targets: LifestyleTargets) {
    return this.http.put<LifestyleTargetsResponse>(`${this.base(clientId)}/targets`, targets);
  }

  setWeight(date: string, weightKg: number, notes?: string | null) {
    return this.http.put<void>(`${this.me}/weight/${date}`, { weightKg, notes });
  }
  deleteWeight(date: string) {
    return this.http.delete<void>(`${this.me}/weight/${date}`);
  }
  setBodyFat(date: string, percent: number) {
    return this.http.put<void>(`${this.me}/body-fat/${date}`, { percent });
  }
  deleteBodyFat(date: string) {
    return this.http.delete<void>(`${this.me}/body-fat/${date}`);
  }
  setMeasurements(date: string, values: { site: MeasurementSite; valueCm: number | null }[]) {
    return this.http.put<void>(`${this.me}/measurements/${date}`, { values });
  }
  setSleep(date: string, input: SleepInput) {
    return this.http.put<void>(`${this.me}/sleep/${date}`, input);
  }
  deleteSleep(date: string) {
    return this.http.delete<void>(`${this.me}/sleep/${date}`);
  }
  setSteps(date: string, steps: number) {
    return this.http.put<void>(`${this.me}/steps/${date}`, { steps });
  }
  deleteSteps(date: string) {
    return this.http.delete<void>(`${this.me}/steps/${date}`);
  }
  addWater(date: string, amountMl: number) {
    return this.http.post<WaterEntry>(`${this.me}/water`, { date, amountMl });
  }
  deleteWater(id: string) {
    return this.http.delete<void>(`${this.me}/water/${id}`);
  }
  uploadPhoto(file: File, date: string, pose: PhotoPose, label?: string | null) {
    const form = new FormData();
    form.append('file', file);
    form.append('date', date);
    form.append('pose', pose);
    if (label) form.append('label', label);
    return this.http.post<ProgressPhoto>(`${this.me}/photos`, form);
  }
  deletePhoto(id: string) {
    return this.http.delete<void>(`${this.me}/photos/${id}`);
  }
  /** Images are served only to the client and their coach, so they are fetched with the session and shown as blob URLs. */
  image(mediaId: string) {
    return this.http.get(`${environment.apiBaseUrl}/media/${mediaId}`, { responseType: 'blob' }).pipe(map(blob => URL.createObjectURL(blob)));
  }

  private base(clientId?: string) {
    return clientId ? `${environment.apiBaseUrl}/clients/${clientId}/progress` : this.me;
  }
}

/** "−2 kg (−2.4%)", with a sign; "–" without a change. */
export function changeText(summary: SeriesSummary, unit: string): string {
  if (summary.change === null) return '–';
  const sign = summary.change > 0 ? '+' : summary.change < 0 ? '−' : '';
  const percent = summary.changePercent === null ? '' : ` (${sign}${Math.abs(summary.changePercent)}%)`;
  return `${sign}${Math.abs(summary.change)} ${unit}${percent}`;
}

/** "75%" or "–". */
export function percentText(value: number | null | undefined): string {
  return value === null || value === undefined ? '–' : `${value}%`;
}

/** "7 h 30 m" from decimal hours. */
export function hoursText(hours: number | null | undefined): string {
  if (hours === null || hours === undefined) return '–';
  const minutes = Math.round(hours * 60);
  return `${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')} m`;
}

/** The earliest and latest photos of a pose: the default before/after pair (§26). */
export function beforeAfter(photos: ProgressPhoto[], pose: PhotoPose): [ProgressPhoto, ProgressPhoto] | null {
  const ofPose = photos.filter(x => x.pose === pose).sort((a, b) => a.date.localeCompare(b.date) || a.createdAt.localeCompare(b.createdAt));
  return ofPose.length >= 2 ? [ofPose[0], ofPose[ofPose.length - 1]] : null;
}
