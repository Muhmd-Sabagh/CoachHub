import { HttpClient } from '@angular/common/http';
import { Component, Injectable, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { environment } from '../../../environments/environment';
import { apiErrorMessage } from '../../shared/services/api-error';

export type RestrictableStatus = 'Expired' | 'Suspended' | 'Pending' | 'Inactive';
export type ClientFeature = 'Nutrition' | 'Training' | 'Progress' | 'FormsAndCheckIns' | 'Chat' | 'Files' | 'Calendar';
export type FeatureAccess = 'Full' | 'ReadOnly' | 'Blocked';

export interface AccessRule { status: RestrictableStatus; feature: ClientFeature; access: FeatureAccess; isDefault?: boolean; }
export interface SubscriptionSettings { durationPresets: number[]; rules: AccessRule[]; }

export const statusLabels: Record<RestrictableStatus, string> = { Expired: 'Expired', Suspended: 'Suspended', Pending: 'Pending', Inactive: 'No subscription' };
export const featureLabels: Record<ClientFeature, string> = {
  Nutrition: 'Nutrition', Training: 'Training', Progress: 'Progress & tests', FormsAndCheckIns: 'Forms & check-ins', Chat: 'Chat',
  Files: 'Files', Calendar: 'Calendar',
};

/** Subscription presets and access rules (§47, §57). */
@Injectable({ providedIn: 'root' })
export class SubscriptionSettingsService {
  private readonly url = `${environment.apiBaseUrl}/settings/subscriptions`;
  constructor(private readonly http: HttpClient) {}
  get() { return this.http.get<SubscriptionSettings>(this.url); }
  save(input: SubscriptionSettings) { return this.http.put<SubscriptionSettings>(this.url, input); }
}

/**
 * The Administrator's subscription rules (§47, §57): the duration presets coaches pick from, and for each status other
 * than Active and each part of the client area, Full, Read-only (history visible, nothing new) or Blocked. Rules
 * never delete data.
 */
@Component({
  selector: 'app-subscription-access',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="card workbench access">
      <div class="section-heading"><h2>Subscription access</h2><span>Active subscriptions always have full access</span></div>
      @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
      @if (saved()) { <p class="success-message" role="status">Saved.</p> }
      <label><span>Duration presets (months, comma separated)</span>
        <input [(ngModel)]="presets" name="presets" placeholder="1, 3, 6, 12" dir="ltr" /></label>
      <div class="table-scroll"><table class="access-grid">
        <thead><tr><th>Feature</th>@for (s of statuses; track s) { <th>{{ statusLabel[s] }}</th> }</tr></thead>
        <tbody>
          @for (f of features; track f) {
            <tr><th scope="row">{{ featureLabel[f] }}</th>
              @for (s of statuses; track s) {
                <td [attr.data-label]="statusLabel[s]"><select [ngModel]="cell(s, f)" (ngModelChange)="set(s, f, $event)" [name]="s + f" [attr.aria-label]="featureLabel[f] + ' when ' + statusLabel[s]">
                  <option value="Full">Full</option><option value="ReadOnly">Read-only</option><option value="Blocked">Blocked</option>
                </select></td>
              }
            </tr>
          }
        </tbody>
      </table></div>
      <p class="muted">Read-only keeps the client's history visible and blocks logging, forms and messages. Client data is never deleted.</p>
      <div class="form-actions">
        <button type="button" class="button button-primary" (click)="save()" [disabled]="busy()">Save access rules</button>
        <button type="button" class="button button-secondary" (click)="reset()" [disabled]="busy()">Restore defaults</button>
      </div>
    </section>
  `,
  styles: [`
    .access { display: grid; gap: 12px; } .access label { display: grid; gap: 4px; max-width: 320px; }
    /* P18: on phones each feature becomes a card of labelled choices instead of a sideways-scrolling grid. */
    @media (max-width: 760px) {
      .access-grid { min-width: 0; }
      .access-grid thead { display: none; }
      .access-grid, .access-grid tbody, .access-grid tr, .access-grid th, .access-grid td { display: block; width: 100%; }
      .access-grid tr { padding: 10px 0; border-bottom: 1px solid var(--gray-200); }
      .access-grid th[scope=row] { padding: 0 0 6px; text-align: start; }
      .access-grid td { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 4px 0; border: 0; }
      .access-grid td::before { content: attr(data-label); color: var(--gray-600); font-size: 14px; }
      .access-grid select { max-width: 60%; }
    }
  `],
})
export class SubscriptionAccessComponent implements OnInit {
  readonly statuses: RestrictableStatus[] = ['Expired', 'Suspended', 'Pending', 'Inactive'];
  readonly features: ClientFeature[] = ['Nutrition', 'Training', 'Progress', 'FormsAndCheckIns', 'Chat', 'Files', 'Calendar'];
  readonly statusLabel = statusLabels;
  readonly featureLabel = featureLabels;
  readonly rules = signal<AccessRule[]>([]);
  readonly busy = signal(false);
  readonly saved = signal(false);
  readonly error = signal('');
  presets = '';

  constructor(private readonly api: SubscriptionSettingsService) {}

  ngOnInit(): void { this.api.get().subscribe({ next: x => this.show(x), error: e => this.error.set(apiErrorMessage(e)) }); }

  cell(status: RestrictableStatus, feature: ClientFeature): FeatureAccess {
    return this.rules().find(x => x.status === status && x.feature === feature)?.access ?? 'Full';
  }

  set(status: RestrictableStatus, feature: ClientFeature, access: FeatureAccess): void {
    this.rules.update(rules => rules.map(x => (x.status === status && x.feature === feature ? { ...x, access } : x)));
    this.saved.set(false);
  }

  save(): void { this.send({ durationPresets: parsePresets(this.presets), rules: this.rules().map(({ status, feature, access }) => ({ status, feature, access })) }); }

  reset(): void { this.send({ durationPresets: [1, 3, 6, 12], rules: [] }); }

  private send(input: SubscriptionSettings): void {
    this.busy.set(true);
    this.api.save(input).subscribe({
      next: x => { this.show(x); this.busy.set(false); this.saved.set(true); this.error.set(''); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e)); },
    });
  }

  private show(settings: SubscriptionSettings): void {
    this.rules.set(settings.rules);
    this.presets = settings.durationPresets.join(', ');
  }
}

/** "1, 3, 6,12" → [1, 3, 6, 12]; anything that isn't a whole number is left for the API to reject. */
export function parsePresets(text: string): number[] {
  return text.split(',').map(x => x.trim()).filter(Boolean).map(Number);
}
