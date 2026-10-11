import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export type PreferredLanguage = 'en' | 'ar';

export interface MyProfile {
  userId: string;
  email: string;
  userName: string;
  displayName: string;
  phoneNumber: string | null;
  preferredLanguage: PreferredLanguage | null;
  role: string;
  coachId: string | null;
  clientId: string | null;
  avatarMediaId: string | null;
}

export interface ProfileInput { displayName: string; phoneNumber: string | null; preferredLanguage: PreferredLanguage | null; }

export interface UserSession {
  id: string;
  userAgent: string | null;
  ipAddress: string | null;
  createdAt: string;
  lastSeenAt: string;
  expiresAt: string;
  isCurrent: boolean;
}

/** The signed-in account's own settings (§2): profile, password, picture and devices. */
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly url = `${environment.apiBaseUrl}/me`;
  constructor(private readonly http: HttpClient) {}

  profile() { return this.http.get<MyProfile>(`${this.url}/profile`); }
  updateProfile(input: ProfileInput) { return this.http.put<MyProfile>(`${this.url}/profile`, input); }
  changePassword(currentPassword: string, newPassword: string) {
    return this.http.put<void>(`${this.url}/password`, { currentPassword, newPassword });
  }

  setAvatar(file: File) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.put<{ id: string }>(`${this.url}/avatar`, form);
  }
  removeAvatar() { return this.http.delete<void>(`${this.url}/avatar`); }

  /** Media is served only to authorized callers, so images are fetched with the token and shown as object URLs. */
  mediaObjectUrl(mediaId: string) {
    return this.http.get(`${environment.apiBaseUrl}/media/${mediaId}`, { responseType: 'blob' }).pipe(map(blob => URL.createObjectURL(blob)));
  }

  sessions() { return this.http.get<UserSession[]>(`${this.url}/sessions`); }
  revokeSession(id: string) { return this.http.delete<void>(`${this.url}/sessions/${id}`); }
  revokeOtherSessions() { return this.http.delete<void>(`${this.url}/sessions`); }
}
