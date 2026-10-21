import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';

export type ChatParticipant = 'Coach' | 'Client';

export interface ChatAttachment { mediaId: string; fileName: string; contentType: string; sizeBytes: number; }
export interface ChatMessage {
  id: string;
  conversationId: string;
  clientId: string;
  sender: ChatParticipant;
  text: string | null;
  sentAt: string;
  readAt: string | null;
  attachments: ChatAttachment[];
}
export interface ChatPage { items: ChatMessage[]; hasMore: boolean; }
export interface ChatThread { clientId: string; clientName: string; conversationId: string | null; unreadCount: number; messages: ChatPage; }
export interface ArchivedConversation { id: string; coachName: string; archivedAt: string; lastMessageAt: string | null; }
export interface MyChat { current: ChatThread; archived: ArchivedConversation[]; }
export interface ConversationSummary {
  conversationId: string;
  clientId: string;
  clientName: string;
  lastMessageAt: string | null;
  lastMessagePreview: string | null;
  lastSender: ChatParticipant | null;
  unreadCount: number;
}
export interface ChatRead { conversationId: string; reader: ChatParticipant; readAt: string; }
export interface MediaMetadata { id: string; originalFileName: string; contentType: string; sizeBytes: number; }

/** Files accepted in chat and shared files (P14): the same list the API checks, 50 MB at most. */
export const sharedFileAccept = 'image/jpeg,image/png,image/webp,image/gif,application/pdf,video/mp4,video/webm,video/quicktime,'
  + 'application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,'
  + 'application/vnd.openxmlformats-officedocument.presentationml.presentation,text/plain';
export const maximumSharedFileBytes = 50 * 1024 * 1024;

export function fileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/**
 * Coach ↔ client chat (§41). The coach uses <c>/clients/{id}/chat</c>; without a client the signed-in client's own
 * <c>/me/chat</c>. New messages also arrive live (RealtimeService).
 */
@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly api = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  conversations(unreadOnly = false, searchTerm = '', pageNumber = 1) {
    let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', 50);
    if (unreadOnly) params = params.set('unreadOnly', true);
    if (searchTerm) params = params.set('searchTerm', searchTerm);
    return this.http.get<PagedResult<ConversationSummary>>(`${this.api}/conversations`, { params });
  }

  thread(clientId?: string) {
    return clientId
      ? this.http.get<ChatThread>(`${this.base(clientId)}`)
      : this.http.get<MyChat>(this.base()).pipe(map(x => x.current));
  }

  mine() { return this.http.get<MyChat>(this.base()); }

  older(before: string, clientId?: string, conversationId?: string) {
    let params = new HttpParams().set('before', before).set('pageSize', 30);
    if (conversationId) params = params.set('conversationId', conversationId);
    return this.http.get<ChatPage>(`${this.base(clientId)}/messages`, { params });
  }

  archived(conversationId: string) {
    return this.http.get<ChatPage>(`${this.base()}/messages`, { params: new HttpParams().set('conversationId', conversationId).set('pageSize', 50) });
  }

  send(text: string | null, mediaIds: string[], clientId?: string) {
    return this.http.post<ChatMessage>(`${this.base(clientId)}/messages`, { text, mediaIds });
  }

  upload(file: File, clientId?: string) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<MediaMetadata>(`${this.base(clientId)}/attachments`, form);
  }

  markRead(clientId?: string) { return this.http.post<void>(`${this.base(clientId)}/read`, null); }

  private base(clientId?: string) { return clientId ? `${this.api}/clients/${clientId}/chat` : `${this.api}/me/chat`; }
}
