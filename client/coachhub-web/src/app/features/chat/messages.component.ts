import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { ChatService, ConversationSummary } from './chat.service';
import { ChatThreadComponent } from './chat-thread.component';

/**
 * The coach's messages (§41): conversations with clients, most recent first, unread first to spot, and the selected
 * conversation (<c>?client=</c>). On a phone the list and the conversation take turns.
 */
@Component({
  selector: 'app-messages',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ChatThreadComponent],
  template: `
    <section class="dashboard-hero"><div><p class="eyebrow">Coaching</p><h1>Messages</h1>
      <p>Chat with your clients. Start a conversation from a client's Messages tab.</p></div></section>
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    <div class="layout" [class.has-selection]="!!selected()">
      <section class="card workbench list">
        <form class="filters" (ngSubmit)="load()">
          <input [(ngModel)]="searchTerm" name="search" placeholder="Client name" aria-label="Search clients" />
          <label class="check"><input type="checkbox" [(ngModel)]="unreadOnly" name="unread" (change)="load()" /> Unread</label>
          <button type="submit" class="button button-secondary">Search</button>
        </form>
        <ul>
          @for (c of conversations(); track c.conversationId) {
            <li>
              <button type="button" class="row" [class.active]="selected() === c.clientId" (click)="select(c.clientId)">
                <span class="line"><strong>{{ c.clientName }}</strong>
                  @if (c.unreadCount) { <span class="badge">{{ c.unreadCount }}</span> }</span>
                <span class="muted preview">{{ c.lastSender === 'Coach' ? 'You: ' : '' }}{{ c.lastMessagePreview }}</span>
                <small class="muted">{{ c.lastMessageAt | date: 'd MMM, shortTime' }}</small>
              </button>
            </li>
          } @empty { <li class="muted empty">No conversations yet.</li> }
        </ul>
      </section>
      <div class="conversation">
        @if (selected(); as clientId) {
          <div class="line"><button type="button" class="link-button back" (click)="select(null)">← All conversations</button>
            <a [routerLink]="['/clients', clientId]">Open profile</a></div>
          <app-chat-thread [clientId]="clientId" />
        } @else { <p class="muted placeholder">Choose a conversation.</p> }
      </div>
    </div>
  `,
  styles: [`
    .layout { display: grid; grid-template-columns: minmax(240px, 320px) 1fr; gap: 16px; align-items: start; }
    .list { padding: 12px; }
    .filters { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 8px; }
    .filters input:not([type]) { flex: 1; min-width: 120px; }
    .check { display: flex; gap: 4px; align-items: center; }
    ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; }
    .row { display: grid; gap: 2px; width: 100%; padding: 10px; border: 0; border-radius: 8px; background: transparent; color: inherit; text-align: start; cursor: pointer; }
    .row.active, .row:hover { background: var(--gray-100); }
    .line { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    .preview { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .badge { min-width: 20px; padding: 0 6px; border-radius: 999px; background: var(--brand); color: white; font-size: 0.75rem; text-align: center; }
    .empty, .placeholder { padding: 12px; }
    .back { display: none; }
    @media (max-width: 760px) {
      .layout { grid-template-columns: 1fr; }
      .layout.has-selection .list { display: none; }
      .layout:not(.has-selection) .conversation { display: none; }
      .back { display: inline; }
    }
  `],
})
export class MessagesComponent implements OnInit, OnDestroy {
  readonly conversations = signal<ConversationSummary[]>([]);
  readonly selected = signal<string | null>(null);
  readonly error = signal('');
  searchTerm = '';
  unreadOnly = false;
  private subscriptions: Subscription[] = [];

  constructor(
    private readonly chat: ChatService,
    private readonly realtime: RealtimeService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.subscriptions = [
      this.route.queryParamMap.subscribe(params => this.selected.set(params.get('client'))),
      // A new message moves its conversation to the top.
      this.realtime.chatMessages.subscribe(() => this.load()),
    ];
    this.load();
  }

  ngOnDestroy(): void { this.subscriptions.forEach(x => x.unsubscribe()); }

  load(): void {
    this.chat.conversations(this.unreadOnly, this.searchTerm.trim()).subscribe({
      next: page => this.conversations.set(page.items),
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  select(clientId: string | null): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: { client: clientId }, replaceUrl: true });
    if (clientId) this.conversations.update(items => items.map(x => (x.clientId === clientId ? { ...x, unreadCount: 0 } : x)));
  }
}
