export type Role = 'Administrator' | 'Coach' | 'Client';
/** Sign in with an email address or a user name. */
export interface LoginRequest { login: string; password: string; }
export interface LoginResult { accessToken: string; expiresAt: string; userId: string; email: string; displayName: string; roles: string[]; coachId?: string | null; clientId?: string | null; /** The account's saved language (P19). */ preferredLanguage?: string | null; }
