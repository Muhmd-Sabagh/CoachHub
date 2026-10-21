import { Injectable } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Subject, firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { ChatMessage, ChatRead } from '../../features/chat/chat.service';
import type { InboxItem } from '../../features/notifications/inbox.service';
import { AuthService } from '../auth/auth.service';

/**
 * Live delivery (P14): chat messages, read status and notifications pushed by the API's SignalR hub. The client only
 * listens; writes go through the REST API. SignalR loads when the first signed-in screen asks for it, and reconnects
 * on its own; the access token is refreshed before each (re)connection.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  readonly chatMessages = new Subject<ChatMessage>();
  readonly chatReads = new Subject<ChatRead>();
  readonly notifications = new Subject<InboxItem>();
  private connection: HubConnection | null = null;
  private starting: Promise<void> | null = null;

  constructor(private readonly auth: AuthService) {}

  start(): Promise<void> {
    if (this.connection || !this.auth.isAuthenticated()) return Promise.resolve();
    this.starting ??= this.connect().finally(() => (this.starting = null));
    return this.starting;
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    await connection?.stop().catch(() => undefined);
  }

  private async connect(): Promise<void> {
    const signalR = await import('@microsoft/signalr');
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/realtime`, {
        accessTokenFactory: async () => ((await firstValueFrom(this.auth.ensureSession())) ? this.auth.accessToken() ?? '' : ''),
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    connection.on('chatMessage', (message: ChatMessage) => this.chatMessages.next(message));
    connection.on('chatRead', (read: ChatRead) => this.chatReads.next(read));
    connection.on('notification', (item: InboxItem) => this.notifications.next(item));
    try {
      await connection.start();
      this.connection = connection;
    } catch {
      // Offline or signed out: screens still load through the API; the next screen tries again.
    }
  }
}
