import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SubscriptionAccessComponent } from './subscription-access.component';
import { Observable, finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { BrandingService } from '../../core/branding/branding.service';
import { isHexColor } from '../../core/theme/theme.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { Coach, CoachesService } from '../coaches/coaches.service';
import {
  BrandImage, BrandInput, BrandRecord, CoachDefaults, MacroMode, OneRepMaxFormula, PlatformSettings, ReplacementRule, Scope, SettingsService, weightTotal,
} from './settings.service';

/**
 * Branding (§63) and settings (§49). A coach edits their own brand and calculation defaults; an
 * Administrator edits the platform brand and settings, or any coach's brand, hosts and defaults.
 */
@Component({
  selector: 'app-branding-defaults', standalone: true, imports: [CommonModule, FormsModule, SubscriptionAccessComponent], templateUrl: './branding-defaults.component.html' })
export class BrandingDefaultsComponent implements OnInit {
  readonly isAdmin: boolean;
  readonly images: { kind: BrandImage; label: string }[] = [
    { kind: 'Logo', label: 'Logo' }, { kind: 'Favicon', label: 'Favicon' }, { kind: 'DefaultAvatar', label: 'Default profile image' },
  ];
  readonly macroModes: { value: MacroMode; label: string }[] = [
    { value: 'GramsPerKg', label: 'Protein and fat in g/kg' },
    { value: 'Percentages', label: 'Percentages of calories' },
    { value: 'FixedGrams', label: 'Fixed grams' },
  ];
  readonly replacementRules: { value: ReplacementRule; label: string }[] = [
    { value: 'Calories', label: 'Match calories' },
    { value: 'DominantMacro', label: 'Match the dominant macro' },
  ];
  readonly oneRepMaxFormulas: { value: OneRepMaxFormula; label: string }[] = [
    { value: 'Epley', label: 'Epley: weight × (1 + reps / 30)' },
    { value: 'Brzycki', label: 'Brzycki: weight × 36 / (37 − reps)' },
  ];
  readonly coaches = signal<Coach[]>([]);
  /** '' is the platform for administrators; otherwise a coach ID. Coaches always edit their own. */
  readonly target = signal('');
  readonly brand = signal<BrandRecord | null>(null);
  readonly brandForm = signal<BrandInput>(emptyBrand());
  readonly hosts = signal({ subdomain: '', customDomain: '' });
  readonly defaults = signal<CoachDefaults | null>(null);
  readonly platform = signal<PlatformSettings | null>(null);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly message = signal('');

  constructor(
    private readonly data: SettingsService,
    private readonly branding: BrandingService,
    private readonly coachDirectory: CoachesService,
    auth: AuthService,
  ) {
    this.isAdmin = auth.isAdmin();
  }

  ngOnInit(): void {
    if (this.isAdmin) this.coachDirectory.list({ pageSize: 100, isActive: true }).subscribe(page => (this.coaches.set(page.items)));
    this.load();
  }

  get scope(): Scope {
    if (!this.isAdmin) return { kind: 'coach', coachId: null };
    return this.target() ? { kind: 'coach', coachId: this.target() } : { kind: 'platform' };
  }
  get isCoachScope(): boolean { return this.scope.kind === 'coach'; }
  get weightsTotal(): number { const defaults = this.defaults(); return defaults ? weightTotal(defaults.adherenceWeights) : 0; }

  load(): void {
    this.error.set(''); this.message.set('');
    this.data.brand(this.scope).subscribe({ next: brand => this.showBrand(brand), error: e => (this.error.set(apiErrorMessage(e))) });
    const coachId = this.scope.kind === 'coach' ? this.scope.coachId : null;
    this.defaults.set(null); this.platform.set(null);
    if (this.isCoachScope) this.data.coachDefaults(coachId).subscribe({ next: d => (this.defaults.set(d)), error: e => (this.error.set(apiErrorMessage(e))) });
    else this.data.platformSettings().subscribe({ next: p => (this.platform.set(p)), error: e => (this.error.set(apiErrorMessage(e))) });
  }

  saveBrand(): void {
    this.run(this.data.saveBrand(this.scope, clean(this.brandForm())), brand => { this.showBrand(brand); this.refreshOwnBrand(); return 'Brand saved.'; });
  }

  saveHosts(): void {
    if (this.scope.kind !== 'coach' || !this.scope.coachId) return;
    this.run(this.data.setHosts(this.scope.coachId, this.hosts().subdomain.trim() || null, this.hosts().customDomain.trim() || null),
      brand => { this.showBrand(brand); return 'Hosts saved.'; });
  }

  upload(image: BrandImage, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.run(this.data.setImage(this.scope, image, file), brand => { this.showBrand(brand); this.refreshOwnBrand(); return 'Image updated.'; });
  }

  removeImage(image: BrandImage): void {
    this.run(this.data.removeImage(this.scope, image), brand => { this.showBrand(brand); this.refreshOwnBrand(); return 'Image removed.'; });
  }

  imageUrl(image: BrandImage): string | null {
    const brand = this.brand();
    if (!brand) return null;
    const id = image === 'Logo' ? brand.logoMediaId : image === 'Favicon' ? brand.faviconMediaId : brand.defaultAvatarMediaId;
    return this.data.assetUrl(id);
  }

  saveDefaults(): void {
    const defaults = this.defaults();
    if (!defaults) return;
    if (this.weightsTotal !== 100) { this.error.set('Adherence weights must add up to 100.'); return; }
    const coachId = this.scope.kind === 'coach' ? this.scope.coachId : null;
    this.run(this.data.saveCoachDefaults(coachId, defaults), saved => { this.defaults.set(saved); return 'Coaching defaults saved.'; });
  }

  savePlatform(): void {
    const platform = this.platform();
    if (!platform) return;
    this.run(this.data.savePlatformSettings({ ...platform, emailSenderName: platform.emailSenderName?.trim() || null }),
      saved => { this.platform.set(saved); return 'Platform settings saved.'; });
  }

  colorValue(value: string | null, fallback: string): string { return isHexColor(value) ? value : fallback; }

  private showBrand(brand: BrandRecord): void {
    this.brand.set(brand);
    this.brandForm.set({
      brandName: brand.brandName, primaryColor: brand.primaryColor, secondaryColor: brand.secondaryColor,
      loginHeadline: brand.loginHeadline, loginMessage: brand.loginMessage, dashboardWelcome: brand.dashboardWelcome,
      emailHeader: brand.emailHeader, emailFooter: brand.emailFooter,
    });
    this.hosts.set({ subdomain: brand.subdomain ?? '', customDomain: brand.customDomain ?? '' });
  }

  /** The page itself shows the editor's own brand: refresh it after editing your own or the platform's. */
  private refreshOwnBrand(): void { this.branding.load().subscribe(); }

  private run<T>(request: Observable<T>, done: (value: T) => string): void {
    this.saving.set(true); this.error.set(''); this.message.set('');
    request.pipe(finalize(() => (this.saving.set(false)))).subscribe({
      next: value => (this.message.set(done(value))),
      error: e => (this.error.set(apiErrorMessage(e))),
    });
  }
}

function emptyBrand(): BrandInput {
  return { brandName: '', primaryColor: null, secondaryColor: null, loginHeadline: null, loginMessage: null, dashboardWelcome: null, emailHeader: null, emailFooter: null };
}

/** Empty text means "use the platform's"; send null rather than "". */
export function clean(input: BrandInput): BrandInput {
  const blank = (value: string | null) => (value && value.trim() ? value.trim() : null);
  return {
    brandName: input.brandName.trim(),
    primaryColor: blank(input.primaryColor), secondaryColor: blank(input.secondaryColor),
    loginHeadline: blank(input.loginHeadline), loginMessage: blank(input.loginMessage),
    dashboardWelcome: blank(input.dashboardWelcome), emailHeader: blank(input.emailHeader), emailFooter: blank(input.emailFooter),
  };
}
