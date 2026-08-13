import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { catchError, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CoachHubSettings } from './app-config.models';

const defaults: CoachHubSettings = { productName: 'CoachHub', coachName: 'Coach' };

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  readonly settings = signal<CoachHubSettings>(defaults);
  constructor(private readonly http: HttpClient) {}
  load() { return this.http.get<CoachHubSettings>(`${environment.apiBaseUrl}/settings`).pipe(tap(settings => this.settings.set(settings)), catchError(() => of(defaults))); }
}