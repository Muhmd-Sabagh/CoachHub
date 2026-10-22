import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Whose brand or defaults are being edited: the platform, or one coach. */
export type Scope = { kind: 'platform' } | { kind: 'coach'; coachId: string | null };
export type BrandImage = 'Logo' | 'Favicon' | 'DefaultAvatar';

export interface BrandRecord {
  coachId: string | null;
  isSaved: boolean;
  brandName: string;
  primaryColor: string | null;
  secondaryColor: string | null;
  logoMediaId: string | null;
  faviconMediaId: string | null;
  defaultAvatarMediaId: string | null;
  loginHeadline: string | null;
  loginMessage: string | null;
  dashboardWelcome: string | null;
  emailHeader: string | null;
  emailFooter: string | null;
  subdomain: string | null;
  customDomain: string | null;
}

export type BrandInput = Pick<BrandRecord, 'brandName' | 'primaryColor' | 'secondaryColor' | 'loginHeadline' |
  'loginMessage' | 'dashboardWelcome' | 'emailHeader' | 'emailFooter'>;

export interface ActivityFactors { sedentary: number; lightlyActive: number; moderatelyActive: number; veryActive: number; extraActive: number; }
export interface AdherenceWeights { nutrition: number; workout: number; checkIn: number; steps: number; water: number; sleep: number; }

export type MacroMode = 'GramsPerKg' | 'Percentages' | 'FixedGrams';
export type ReplacementRule = 'Calories' | 'DominantMacro';
/** The estimated one-rep max formula (§22) for PRs and strength analytics. */
export type OneRepMaxFormula = 'Epley' | 'Brzycki';
export type UnitSystem = 'Metric' | 'Imperial';

export interface CoachDefaults {
  activityFactors: ActivityFactors;
  deficitPercent: number;
  surplusPercent: number;
  proteinGramsPerKg: number;
  fatGramsPerKg: number;
  defaultMacroMode: MacroMode;
  adherenceWeights: AdherenceWeights;
  replacementRule: ReplacementRule;
  oneRepMaxFormula: OneRepMaxFormula;
  /** 0–1: how much a set counts for each secondary muscle in muscle analytics (§23); 0 counts the primary only. */
  secondaryMuscleWeight: number;
  /** Default daily targets for clients without their own (P11); each optional. */
  defaultTargets: { steps: number | null; waterMl: number | null; sleepHours: number | null } | null;
  isSaved?: boolean;
}

export interface PlatformSettings {
  defaultLanguage: 'en' | 'ar';
  units: UnitSystem;
  emailNotificationsEnabled: boolean;
  whatsAppNotificationsEnabled: boolean;
  emailSenderName: string | null;
}

/** Branding (§63) and settings (§49) for the platform (Administrator) or a coach. */
@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly branding = `${environment.apiBaseUrl}/branding`;
  private readonly settings = `${environment.apiBaseUrl}/settings`;
  constructor(private readonly http: HttpClient) {}

  brand(scope: Scope) { return this.http.get<BrandRecord>(this.brandUrl(scope)); }
  saveBrand(scope: Scope, input: BrandInput) { return this.http.put<BrandRecord>(this.brandUrl(scope), input); }
  setImage(scope: Scope, image: BrandImage, file: File) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.put<BrandRecord>(this.brandUrl(scope, `/images/${image}`), form);
  }
  removeImage(scope: Scope, image: BrandImage) { return this.http.delete<BrandRecord>(this.brandUrl(scope, `/images/${image}`)); }
  setHosts(coachId: string, subdomain: string | null, customDomain: string | null) {
    return this.http.put<BrandRecord>(`${this.branding}/coach/hosts?coachId=${encodeURIComponent(coachId)}`, { subdomain, customDomain });
  }
  assetUrl(mediaId: string | null): string | null { return mediaId ? `${this.branding}/assets/${mediaId}` : null; }

  coachDefaults(coachId: string | null) { return this.http.get<CoachDefaults>(this.withCoach(`${this.settings}/coach`, coachId)); }
  saveCoachDefaults(coachId: string | null, input: CoachDefaults) {
    return this.http.put<CoachDefaults>(this.withCoach(`${this.settings}/coach`, coachId), input);
  }
  platformSettings() { return this.http.get<PlatformSettings>(`${this.settings}/platform`); }
  savePlatformSettings(input: PlatformSettings) { return this.http.put<PlatformSettings>(`${this.settings}/platform`, input); }

  private brandUrl(scope: Scope, suffix = ''): string {
    return scope.kind === 'platform'
      ? `${this.branding}/platform${suffix}`
      : this.withCoach(`${this.branding}/coach${suffix}`, scope.coachId);
  }
  private withCoach(url: string, coachId: string | null): string {
    return coachId ? `${url}?coachId=${encodeURIComponent(coachId)}` : url;
  }
}

/** The adherence weights must add up to 100 (§30). */
export function weightTotal(weights: AdherenceWeights): number {
  return weights.nutrition + weights.workout + weights.checkIn + weights.steps + weights.water + weights.sleep;
}
