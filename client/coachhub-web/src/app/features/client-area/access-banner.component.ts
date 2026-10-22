import { HttpClient } from '@angular/common/http';
import { Component, Injectable, OnInit, computed, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { I18nService } from '../../core/i18n/i18n.service';
import { enumLabelKey } from '../../core/i18n/enum-labels';
import { ClientFeature, FeatureAccess } from '../settings/subscription-access.component';

export type SubscriptionStatus = 'Inactive' | 'Active' | 'Expired' | 'Suspended' | 'Pending';
export interface MyAccess {
  subscriptionStatus: SubscriptionStatus;
  startsOn: string | null;
  endsOn: string | null;
  features: Array<{ feature: ClientFeature; access: FeatureAccess }>;
}

/** The signed-in client's subscription status and what each part of the client area allows (§57). */
@Injectable({ providedIn: 'root' })
export class MyAccessService {
  constructor(private readonly http: HttpClient) {}
  get() { return this.http.get<MyAccess>(`${environment.apiBaseUrl}/me/access`); }
}

/** "Read-only: Nutrition, Chat", in the interface language; null when everything is open. */
export function restrictionText(access: MyAccess, i18n: I18nService): string | null {
  const separator = i18n.language() === 'ar' ? '، ' : ', ';
  const names = (level: FeatureAccess) => access.features.filter(x => x.access === level)
    .map(x => { const key = enumLabelKey('clientFeature', x.feature); return key ? i18n.translate(key) : x.feature; })
    .join(separator);
  const readOnly = names('ReadOnly');
  const blocked = names('Blocked');
  const parts = [
    readOnly ? i18n.translate('client.access.readOnly', { features: readOnly }) : '',
    blocked ? i18n.translate('client.access.blocked', { features: blocked }) : '',
  ].filter(Boolean);
  return parts.length ? parts.join('. ') + '.' : null;
}

/**
 * A notice at the top of the client area when the subscription limits what the client can do (§57), so a refused
 * action is no surprise. Data is never deleted; renewing restores everything.
 */
@Component({
  selector: 'app-access-banner',
  standalone: true,
  template: `
    @if (message(); as text) {
      <div class="access-banner" role="status">
        <strong>{{ headline() }}</strong>
        <span>{{ text }} {{ kept() }}</span>
      </div>
    }
  `,
  styles: [`
    .access-banner { display: grid; gap: 2px; margin-bottom: 12px; padding: 10px 14px; border-radius: 10px;
      background: var(--gray-100); border-inline-start: 4px solid var(--error); }
  `],
})
export class AccessBannerComponent implements OnInit {
  readonly access = signal<MyAccess | null>(null);
  readonly message = computed(() => { const access = this.access(); return access ? restrictionText(access, this.i18n) : null; });
  readonly kept = computed(() => this.i18n.translate('client.access.kept'));
  readonly headline = computed(() => {
    const access = this.access();
    const date = (value: string | null | undefined) => this.i18n.formatDate(value, 'd MMMM y');
    switch (access?.subscriptionStatus) {
      case 'Expired': return access.endsOn ? this.i18n.translate('client.access.ended', { date: date(access.endsOn) }) : this.i18n.translate('client.access.endedNoDate');
      case 'Suspended': return this.i18n.translate('client.access.suspended');
      case 'Pending': return access.startsOn ? this.i18n.translate('client.access.starts', { date: date(access.startsOn) }) : this.i18n.translate('client.access.startsSoon');
      default: return this.i18n.translate('client.access.none');
    }
  });

  constructor(private readonly api: MyAccessService, private readonly i18n: I18nService) {}

  ngOnInit(): void { this.api.get().subscribe({ next: x => this.access.set(x), error: () => undefined }); }
}
