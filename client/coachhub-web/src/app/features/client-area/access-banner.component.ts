import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, Injectable, OnInit, computed, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ClientFeature, FeatureAccess, featureLabels } from '../settings/subscription-access.component';

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

/** "Read-only: Nutrition, Chat"; null when everything is open. */
export function restrictionText(access: MyAccess): string | null {
  const readOnly = access.features.filter(x => x.access === 'ReadOnly').map(x => featureLabels[x.feature]);
  const blocked = access.features.filter(x => x.access === 'Blocked').map(x => featureLabels[x.feature]);
  const parts = [readOnly.length ? `Read-only: ${readOnly.join(', ')}` : '', blocked.length ? `Not available: ${blocked.join(', ')}` : ''].filter(Boolean);
  return parts.length ? parts.join('. ') + '.' : null;
}

/**
 * A notice at the top of the client area when the subscription limits what the client can do (§57), so a refused
 * action is no surprise. Data is never deleted; renewing restores everything.
 */
@Component({
  selector: 'app-access-banner',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (message(); as text) {
      <div class="access-banner" role="status">
        <strong>{{ headline() }}</strong>
        <span>{{ text }} Your data is kept; contact your coach to renew.</span>
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
  readonly message = computed(() => (this.access() ? restrictionText(this.access()!) : null));
  readonly headline = computed(() => {
    const access = this.access();
    switch (access?.subscriptionStatus) {
      case 'Expired': return `Your subscription ended${access.endsOn ? ' on ' + access.endsOn : ''}.`;
      case 'Suspended': return 'Your subscription is suspended.';
      case 'Pending': return `Your subscription starts${access.startsOn ? ' on ' + access.startsOn : ' soon'}.`;
      default: return 'You have no active subscription.';
    }
  });

  constructor(private readonly api: MyAccessService) {}

  ngOnInit(): void { this.api.get().subscribe({ next: x => this.access.set(x), error: () => undefined }); }
}
