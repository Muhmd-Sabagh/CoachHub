import { Component, HostListener, OnDestroy, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { InboxItem, InboxService, ago } from './inbox.service';

/**
 * The in-app notifications (§42) in the top bar of both the staff and the client area: an unread badge that updates
 * live, and a panel with the latest notifications. Opening one marks it read and follows its link.
 */
@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    <div class="bell">
      <button type="button" class="icon-button" [attr.aria-label]="'notifications.unread' | translate: { count: unread() }" [attr.aria-expanded]="open()"
        (click)="toggle($event)">
        <span aria-hidden="true">🔔</span>
        @if (unread() > 0) { <span class="count">{{ unread() > 99 ? '99+' : unread() }}</span> }
      </button>
      @if (open()) {
        <section class="panel card" role="dialog" [attr.aria-label]="'notifications.title' | translate" (click)="$event.stopPropagation()">
          <div class="panel-head">
            <strong>{{ 'notifications.title' | translate }}</strong>
            @if (unread() > 0) { <button type="button" class="link-button" (click)="readAll()">{{ 'notifications.markAllRead' | translate }}</button> }
          </div>
          @if (items().length) {
            <ul>
              @for (n of items(); track n.id) {
                <li>
                  <button type="button" class="item" [class.unread]="!n.readAt" (click)="openItem(n)">
                    <strong translate="no">{{ n.title }}</strong>
                    <span translate="no">{{ n.body }}</span>
                    <small class="muted">{{ when(n.createdAt) }}</small>
                  </button>
                </li>
              }
            </ul>
          } @else { <p class="muted empty">{{ 'notifications.caughtUp' | translate }}</p> }
        </section>
      }
    </div>
  `,
  styles: [`
    .bell { position: relative; }
    .icon-button { position: relative; min-width: 40px; min-height: 40px; }
    .count { position: absolute; top: 2px; inset-inline-end: 0; min-width: 18px; padding: 0 4px; border-radius: 999px;
      background: var(--error); color: white; font-size: 0.7rem; font-weight: 700; line-height: 18px; }
    .panel { position: absolute; inset-inline-end: 0; top: calc(100% + 8px); z-index: 30; width: min(360px, calc(100vw - 32px));
      max-height: 70vh; overflow: auto; padding: 0; color: var(--gray-900); }
    .panel-head { display: flex; justify-content: space-between; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--gray-200); }
    ul { list-style: none; margin: 0; padding: 0; }
    .item { display: grid; gap: 2px; width: 100%; padding: 10px 14px; border: 0; border-bottom: 1px solid var(--gray-100);
      background: transparent; color: inherit; text-align: start; cursor: pointer; }
    .item.unread { background: var(--brand-soft); }
    .item span { font-size: 0.9rem; }
    .empty { padding: 16px; margin: 0; }
  `],
})
export class NotificationBellComponent implements OnInit, OnDestroy {
  readonly items = signal<InboxItem[]>([]);
  readonly unread = signal(0);
  readonly open = signal(false);
  private live?: Subscription;

  constructor(
    private readonly inbox: InboxService,
    private readonly realtime: RealtimeService,
    private readonly router: Router,
    private readonly i18n: I18nService,
  ) {}

  ngOnInit(): void {
    this.refresh();
    void this.realtime.start();
    this.live = this.realtime.notifications.subscribe(item => {
      this.items.update(items => [item, ...items.filter(x => x.id !== item.id)].slice(0, 20));
      this.unread.update(count => count + 1);
    });
  }

  ngOnDestroy(): void {
    this.live?.unsubscribe();
    void this.realtime.stop();
  }

  @HostListener('document:click')
  close(): void { this.open.set(false); }

  @HostListener('document:keydown.escape')
  escape(): void { this.open.set(false); }

  toggle(event: Event): void {
    event.stopPropagation();
    this.open.update(open => !open);
    if (this.open()) this.refresh();
  }

  when(iso: string): string { return ago(iso, this.i18n); }

  openItem(item: InboxItem): void {
    this.open.set(false);
    if (!item.readAt) {
      this.inbox.read(item.id).subscribe(read => {
        this.items.update(items => items.map(x => (x.id === read.id ? read : x)));
        this.unread.update(count => Math.max(0, count - 1));
      });
    }
    if (item.link) void this.router.navigateByUrl(item.link);
  }

  readAll(): void {
    this.inbox.readAll().subscribe(() => {
      const now = new Date().toISOString();
      this.items.update(items => items.map(x => ({ ...x, readAt: x.readAt ?? now })));
      this.unread.set(0);
    });
  }

  private refresh(): void {
    this.inbox.list().subscribe({ next: page => this.items.set(page.items), error: () => undefined });
    this.inbox.unreadCount().subscribe({ next: x => this.unread.set(x.count), error: () => undefined });
  }
}
