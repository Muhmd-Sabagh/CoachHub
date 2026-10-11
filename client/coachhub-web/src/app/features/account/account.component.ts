import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { AccountService, MyProfile, PreferredLanguage, UserSession } from './account.service';

/** My account (§2): personal information, profile picture, password and signed-in devices. Every role. */
@Component({
  selector: 'app-account',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './account.component.html',
  styles: [`
    .account-page { display: grid; gap: 16px; }
    .avatar-row { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; }
    .avatar-large { width: 88px; height: 88px; border-radius: 50%; object-fit: cover; display: grid; place-items: center;
      background: var(--brand-soft); color: var(--brand); font-size: 2rem; font-weight: 700; }
    .session-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
    .session-list li { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 12px;
      border: 1px solid var(--gray-200); border-radius: 8px; }
    .session-list strong, .session-list small { display: block; overflow-wrap: anywhere; }
    @media (max-width: 760px) { .account-page .form-grid { grid-template-columns: 1fr; } }
  `],
})
export class AccountComponent implements OnInit, OnDestroy {
  profile: MyProfile | null = null;
  editor = { displayName: '', phoneNumber: '', preferredLanguage: '' as PreferredLanguage | '' };
  password = { current: '', next: '', confirm: '' };
  sessions: UserSession[] = [];
  avatarUrl: string | null = null;
  saving = false;
  error = '';
  message = '';
  readonly backLink: string;
  /** Clients open this page outside the staff shell, so it brings its own page padding. */
  readonly outsideShell: boolean;

  constructor(
    private readonly account: AccountService,
    private readonly auth: AuthService,
    private readonly i18n: I18nService,
    route: ActivatedRoute,
  ) {
    this.backLink = auth.landingRoute();
    this.outsideShell = route.snapshot.data['outsideShell'] === true;
  }

  ngOnInit(): void {
    this.account.profile().subscribe({ next: profile => this.show(profile), error: e => (this.error = apiErrorMessage(e)) });
    this.loadSessions();
  }

  ngOnDestroy(): void { this.releaseAvatar(); }

  saveProfile(): void {
    this.begin();
    this.account.updateProfile({
      displayName: this.editor.displayName,
      phoneNumber: this.editor.phoneNumber.trim() || null,
      preferredLanguage: this.editor.preferredLanguage || null,
    }).pipe(finalize(() => (this.saving = false))).subscribe({
      next: profile => {
        this.show(profile);
        this.auth.updateDisplayName(profile.displayName);
        if (profile.preferredLanguage) this.i18n.setLanguage(profile.preferredLanguage);
        this.message = 'Your details were saved.';
      },
      error: e => (this.error = apiErrorMessage(e)),
    });
  }

  changePassword(): void {
    if (this.password.next !== this.password.confirm) { this.error = 'The new passwords do not match.'; return; }
    this.begin();
    this.account.changePassword(this.password.current, this.password.next).pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => {
        this.password = { current: '', next: '', confirm: '' };
        this.message = 'Password changed. Your other devices were signed out.';
        this.loadSessions();
      },
      error: e => (this.error = apiErrorMessage(e)),
    });
  }

  uploadAvatar(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.begin();
    this.account.setAvatar(file).pipe(finalize(() => (this.saving = false))).subscribe({
      next: media => { if (this.profile) this.show({ ...this.profile, avatarMediaId: media.id }); this.message = 'Profile picture updated.'; },
      error: e => (this.error = apiErrorMessage(e)),
    });
  }

  removeAvatar(): void {
    this.begin();
    this.account.removeAvatar().pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => { if (this.profile) this.show({ ...this.profile, avatarMediaId: null }); this.message = 'Profile picture removed.'; },
      error: e => (this.error = apiErrorMessage(e)),
    });
  }

  revoke(session: UserSession): void {
    this.begin();
    this.account.revokeSession(session.id).pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => { this.message = 'The device was signed out.'; this.loadSessions(); },
      error: e => (this.error = apiErrorMessage(e)),
    });
  }

  revokeOthers(): void {
    this.begin();
    this.account.revokeOtherSessions().pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => { this.message = 'All other devices were signed out.'; this.loadSessions(); },
      error: e => (this.error = apiErrorMessage(e)),
    });
  }

  device(session: UserSession): string { return describeDevice(session.userAgent); }

  get initial(): string { return (this.profile?.displayName || this.profile?.email || '?').charAt(0).toUpperCase(); }

  private show(profile: MyProfile): void {
    const avatarChanged = profile.avatarMediaId !== this.profile?.avatarMediaId;
    this.profile = profile;
    this.editor = {
      displayName: profile.displayName,
      phoneNumber: profile.phoneNumber ?? '',
      preferredLanguage: profile.preferredLanguage ?? '',
    };
    if (!avatarChanged && this.avatarUrl) return;
    this.releaseAvatar();
    if (profile.avatarMediaId) {
      this.account.mediaObjectUrl(profile.avatarMediaId).subscribe({ next: url => (this.avatarUrl = url), error: () => (this.avatarUrl = null) });
    }
  }

  private loadSessions(): void {
    this.account.sessions().subscribe({ next: sessions => (this.sessions = sessions), error: e => (this.error = apiErrorMessage(e)) });
  }

  private begin(): void { this.saving = true; this.error = ''; this.message = ''; }

  private releaseAvatar(): void {
    if (this.avatarUrl) URL.revokeObjectURL(this.avatarUrl);
    this.avatarUrl = null;
  }
}

/** A readable device label from a user agent, e.g. "Chrome on Android". */
export function describeDevice(userAgent: string | null): string {
  if (!userAgent) return 'Unknown device';
  const browser = /Edg\//.test(userAgent) ? 'Edge' : /Firefox\//.test(userAgent) ? 'Firefox'
    : /Chrome\//.test(userAgent) ? 'Chrome' : /Safari\//.test(userAgent) ? 'Safari' : 'Browser';
  const system = /Android/.test(userAgent) ? 'Android' : /iPhone|iPad/.test(userAgent) ? 'iOS'
    : /Windows/.test(userAgent) ? 'Windows' : /Mac OS X/.test(userAgent) ? 'macOS' : /Linux/.test(userAgent) ? 'Linux' : '';
  return system ? `${browser} on ${system}` : browser === 'Browser' ? userAgent.slice(0, 60) : browser;
}
