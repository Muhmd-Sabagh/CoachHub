import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { BrandingService } from '../../core/branding/branding.service';
import { isHexColor } from '../../core/theme/theme.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { Coach, CoachesService } from '../coaches/coaches.service';
import {
  BrandImage, BrandInput, BrandRecord, CoachDefaults, MacroMode, PlatformSettings, ReplacementRule, Scope, WorkspaceService, weightTotal,
} from './workspace.service';

/**
 * Branding (§63) and settings (§49). A coach edits their own brand and calculation defaults; an
 * Administrator edits the platform brand and settings, or any coach's brand, hosts and defaults.
 */
@Component({ selector: 'app-workspace', standalone: true, imports: [CommonModule, FormsModule], templateUrl: './workspace.component.html' })
export class WorkspaceComponent implements OnInit {
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
  coaches: Coach[] = [];
  /** '' is the platform for administrators; otherwise a coach ID. Coaches always edit their own. */
  target = '';
  brand: BrandRecord | null = null;
  brandForm: BrandInput = emptyBrand();
  hosts = { subdomain: '', customDomain: '' };
  defaults: CoachDefaults | null = null;
  platform: PlatformSettings | null = null;
  saving = false;
  error = '';
  message = '';

  constructor(
    private readonly data: WorkspaceService,
    private readonly branding: BrandingService,
    private readonly coachDirectory: CoachesService,
    auth: AuthService,
  ) {
    this.isAdmin = auth.isAdmin();
  }

  ngOnInit(): void {
    if (this.isAdmin) this.coachDirectory.list({ pageSize: 100, isActive: true }).subscribe(page => (this.coaches = page.items));
    this.load();
  }

  get scope(): Scope {
    if (!this.isAdmin) return { kind: 'coach', coachId: null };
    return this.target ? { kind: 'coach', coachId: this.target } : { kind: 'platform' };
  }
  get isCoachScope(): boolean { return this.scope.kind === 'coach'; }
  get weightsTotal(): number { return this.defaults ? weightTotal(this.defaults.adherenceWeights) : 0; }

  load(): void {
    this.error = ''; this.message = '';
    this.data.brand(this.scope).subscribe({ next: brand => this.showBrand(brand), error: e => (this.error = apiErrorMessage(e)) });
    const coachId = this.scope.kind === 'coach' ? this.scope.coachId : null;
    this.defaults = null; this.platform = null;
    if (this.isCoachScope) this.data.coachDefaults(coachId).subscribe({ next: d => (this.defaults = d), error: e => (this.error = apiErrorMessage(e)) });
    else this.data.platformSettings().subscribe({ next: p => (this.platform = p), error: e => (this.error = apiErrorMessage(e)) });
  }

  saveBrand(): void {
    this.run(this.data.saveBrand(this.scope, clean(this.brandForm)), brand => { this.showBrand(brand); this.refreshOwnBrand(); return 'Brand saved.'; });
  }

  saveHosts(): void {
    if (this.scope.kind !== 'coach' || !this.scope.coachId) return;
    this.run(this.data.setHosts(this.scope.coachId, this.hosts.subdomain.trim() || null, this.hosts.customDomain.trim() || null),
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
    if (!this.brand) return null;
    const id = image === 'Logo' ? this.brand.logoMediaId : image === 'Favicon' ? this.brand.faviconMediaId : this.brand.defaultAvatarMediaId;
    return this.data.assetUrl(id);
  }

  saveDefaults(): void {
    if (!this.defaults) return;
    if (this.weightsTotal !== 100) { this.error = 'Adherence weights must add up to 100.'; return; }
    const coachId = this.scope.kind === 'coach' ? this.scope.coachId : null;
    this.run(this.data.saveCoachDefaults(coachId, this.defaults), saved => { this.defaults = saved; return 'Coaching defaults saved.'; });
  }

  savePlatform(): void {
    if (!this.platform) return;
    this.run(this.data.savePlatformSettings({ ...this.platform, emailSenderName: this.platform.emailSenderName?.trim() || null }),
      saved => { this.platform = saved; return 'Platform settings saved.'; });
  }

  colorValue(value: string | null, fallback: string): string { return isHexColor(value) ? value : fallback; }

  private showBrand(brand: BrandRecord): void {
    this.brand = brand;
    this.brandForm = {
      brandName: brand.brandName, primaryColor: brand.primaryColor, secondaryColor: brand.secondaryColor,
      loginHeadline: brand.loginHeadline, loginMessage: brand.loginMessage, dashboardWelcome: brand.dashboardWelcome,
      emailHeader: brand.emailHeader, emailFooter: brand.emailFooter,
    };
    this.hosts = { subdomain: brand.subdomain ?? '', customDomain: brand.customDomain ?? '' };
  }

  /** The page itself shows the editor's own brand: refresh it after editing your own or the platform's. */
  private refreshOwnBrand(): void { this.branding.load().subscribe(); }

  private run<T>(request: Observable<T>, done: (value: T) => string): void {
    this.saving = true; this.error = ''; this.message = '';
    request.pipe(finalize(() => (this.saving = false))).subscribe({
      next: value => (this.message = done(value)),
      error: e => (this.error = apiErrorMessage(e)),
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
