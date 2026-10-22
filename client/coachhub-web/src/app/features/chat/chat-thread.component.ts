import { CommonModule } from '@angular/common';
import { AfterViewChecked, Component, ElementRef, Input, OnChanges, OnDestroy, OnInit, ViewChild, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, concatMap, from, map, toArray } from 'rxjs';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { apiErrorMessage } from '../../shared/services/api-error';
import { ClientFilesService } from '../client-files/client-files.service';
import {
  ChatAttachment, ChatMessage, ChatParticipant, ChatService, ChatThread, fileSize, maximumSharedFileBytes, sharedFileAccept,
} from './chat.service';

/**
 * One conversation (§41): the history (oldest at the top, "Show earlier" pages back), new messages live, read status,
 * and a composer with files. With <c>clientId</c> it is the coach's view of that client; without, the signed-in
 * client's own. With <c>archivedId</c> it shows a previous coach's conversation, read-only.
 */
@Component({
  selector: 'app-chat-thread',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    <section class="card workbench thread">
      @if (hasMore()) { <button type="button" class="link-button older" (click)="loadOlder()">Show earlier messages</button> }
      <div #scroller class="messages" role="log" aria-live="polite" [attr.aria-label]="'Conversation with ' + otherName()">
        @for (m of messages(); track m.id) {
          <div class="bubble" [class.mine]="m.sender === me()">
            @if (m.text) { <p>{{ m.text }}</p> }
            @for (a of m.attachments; track a.mediaId) {
              <button type="button" class="attachment" (click)="openFile(a)">📎 {{ a.fileName }} <small>{{ size(a.sizeBytes) }}</small></button>
            }
            <small class="meta">{{ m.sentAt | date: 'd MMM, HH:mm' }}@if (m.sender === me()) { · {{ m.readAt ? 'Read' : 'Sent' }} }</small>
          </div>
        } @empty { <p class="muted empty">{{ archivedId ? 'No messages.' : 'No messages yet. Say hello!' }}</p> }
      </div>
      @if (!archivedId) {
        <form class="composer" (ngSubmit)="send()">
          @if (pending().length) {
            <div class="pending">@for (f of pending(); track f.name) { <span class="chip">{{ f.name }} <button type="button" class="link-button" (click)="removeFile(f)" [attr.aria-label]="'Remove ' + f.name">×</button></span> }</div>
          }
          <label class="file-button" [attr.aria-label]="'Attach files'">📎<input type="file" multiple [accept]="accept" (change)="pick($event)" /></label>
          <textarea [(ngModel)]="text" name="text" rows="2" maxlength="4000" placeholder="Write a message…" aria-label="Message"
            (keydown.enter)="enter($any($event))"></textarea>
          <button type="submit" class="button button-primary" [disabled]="busy() || (!text.trim() && !pending().length)">Send</button>
        </form>
      }
    </section>
  `,
  styles: [`
    .thread { display: grid; gap: 8px; padding: 12px; }
    .older { justify-self: center; }
    .messages { display: flex; flex-direction: column; gap: 8px; max-height: min(60vh, 560px); min-height: 200px; overflow-y: auto; padding: 4px; }
    .bubble { max-width: 80%; align-self: flex-start; display: grid; gap: 4px; padding: 8px 12px; border-radius: 14px;
      background: var(--gray-100); color: var(--gray-900); overflow-wrap: anywhere; }
    .bubble.mine { align-self: flex-end; background: var(--brand); color: white; }
    .bubble p { margin: 0; white-space: pre-line; }
    .meta { opacity: 0.75; font-size: 0.72rem; }
    .attachment { border: 0; background: rgba(255, 255, 255, 0.2); color: inherit; text-align: start; padding: 4px 6px; border-radius: 8px; cursor: pointer; }
    .empty { margin: auto; }
    .composer { display: grid; grid-template-columns: auto 1fr auto; gap: 8px; align-items: end; }
    .composer textarea { width: 100%; min-height: 44px; resize: vertical; }
    .composer .button { min-height: 44px; }
    .pending { grid-column: 1 / -1; display: flex; flex-wrap: wrap; gap: 6px; }
    .chip { padding: 2px 8px; border-radius: 999px; background: var(--gray-100); font-size: 0.8rem; }
    .file-button { display: grid; place-items: center; min-width: 44px; min-height: 44px; border: 1px solid var(--gray-200); border-radius: 8px; cursor: pointer; }
    .file-button input { position: absolute; width: 1px; height: 1px; opacity: 0; }
  `],
})
export class ChatThreadComponent implements OnInit, OnChanges, OnDestroy, AfterViewChecked {
  /** The coach's view of this client; unset for the signed-in client. */
  @Input() clientId?: string;
  /** A previous coach's conversation (client view only), read-only. */
  @Input() archivedId?: string;
  @ViewChild('scroller') scroller?: ElementRef<HTMLElement>;
  readonly accept = sharedFileAccept;
  readonly size = fileSize;
  readonly messages = signal<ChatMessage[]>([]);
  readonly hasMore = signal(false);
  readonly pending = signal<File[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly thread = signal<ChatThread | null>(null);
  text = '';
  private stickToBottom = true;
  private live: Subscription[] = [];

  constructor(private readonly chat: ChatService, private readonly realtime: RealtimeService, private readonly files: ClientFilesService) {}

  me(): ChatParticipant { return this.clientId ? 'Coach' : 'Client'; }
  otherName(): string { return this.clientId ? this.thread()?.clientName ?? 'client' : 'your coach'; }

  private loaded = false;

  /** Without bound inputs (the client's own chat) ngOnChanges never runs, so the first load happens here. */
  ngOnInit(): void { if (!this.loaded) this.ngOnChanges(); }

  ngOnChanges(): void {
    this.loaded = true;
    this.live.forEach(x => x.unsubscribe());
    this.messages.set([]);
    this.error.set('');
    if (this.archivedId) {
      this.chat.archived(this.archivedId).subscribe({ next: page => this.show(page.items, page.hasMore), error: e => this.error.set(apiErrorMessage(e)) });
      return;
    }
    this.chat.thread(this.clientId).subscribe({
      next: thread => {
        this.thread.set(thread);
        this.show(thread.messages.items, thread.messages.hasMore);
        if (thread.unreadCount > 0) this.markRead();
      },
      error: e => this.error.set(apiErrorMessage(e)),
    });
    void this.realtime.start();
    this.live = [
      this.realtime.chatMessages.subscribe(message => this.receive(message)),
      this.realtime.chatReads.subscribe(read => {
        if (read.reader === this.me() || read.conversationId !== this.thread()?.conversationId) return;
        this.messages.update(items => items.map(x => (x.sender === this.me() && !x.readAt ? { ...x, readAt: read.readAt } : x)));
      }),
    ];
  }

  ngOnDestroy(): void { this.live.forEach(x => x.unsubscribe()); }

  ngAfterViewChecked(): void {
    if (this.stickToBottom && this.scroller) {
      this.scroller.nativeElement.scrollTop = this.scroller.nativeElement.scrollHeight;
      this.stickToBottom = false;
    }
  }

  loadOlder(): void {
    const oldest = this.messages()[0];
    if (!oldest) return;
    this.chat.older(oldest.sentAt, this.clientId, this.archivedId).subscribe({
      next: page => {
        this.messages.update(items => [...[...page.items].reverse(), ...items]);
        this.hasMore.set(page.hasMore);
      },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  pick(event: Event): void {
    const input = event.target as HTMLInputElement;
    const chosen = Array.from(input.files ?? []);
    input.value = '';
    const tooBig = chosen.find(f => f.size > maximumSharedFileBytes);
    if (tooBig) { this.error.set(`${tooBig.name} is larger than 50 MB.`); return; }
    this.pending.update(files => [...files, ...chosen].slice(0, 10));
  }

  removeFile(file: File): void { this.pending.update(files => files.filter(f => f !== file)); }

  enter(event: KeyboardEvent): void {
    // Enter sends on wide screens; Shift+Enter (and phones) add a line.
    if (event.shiftKey || window.matchMedia('(max-width: 760px)').matches) return;
    event.preventDefault();
    this.send();
  }

  send(): void {
    const text = this.text.trim();
    const files = this.pending();
    if (!text && !files.length) return;
    this.busy.set(true);
    // Files upload one by one first; an empty list still emits once.
    from(files).pipe(
      concatMap(f => this.chat.upload(f, this.clientId)),
      map(uploaded => uploaded.id),
      toArray(),
      concatMap(ids => this.chat.send(text || null, ids, this.clientId)),
    ).subscribe({
      next: message => {
        this.text = '';
        this.pending.set([]);
        this.busy.set(false);
        this.error.set('');
        this.receive(message);
      },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e)); },
    });
  }

  openFile(attachment: ChatAttachment): void {
    this.files.content(attachment.mediaId).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.target = '_blank';
        link.rel = 'noopener';
        if (!attachment.contentType.startsWith('image/') && !attachment.contentType.startsWith('video/') && attachment.contentType !== 'application/pdf')
          link.download = attachment.fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  private show(newestFirst: ChatMessage[], hasMore: boolean): void {
    this.messages.set([...newestFirst].reverse());
    this.hasMore.set(hasMore);
    this.stickToBottom = true;
  }

  private receive(message: ChatMessage): void {
    if (this.archivedId) return;
    const thread = this.thread();
    if (this.clientId ? message.clientId !== this.clientId : thread?.conversationId && message.conversationId !== thread.conversationId) return;
    if (this.messages().some(x => x.id === message.id)) return;
    if (thread && !thread.conversationId) this.thread.set({ ...thread, conversationId: message.conversationId });
    this.messages.update(items => [...items, message]);
    this.stickToBottom = true;
    if (message.sender !== this.me()) this.markRead();
  }

  private markRead(): void { this.chat.markRead(this.clientId).subscribe({ error: () => undefined }); }
}
