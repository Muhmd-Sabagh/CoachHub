import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { ArchivedConversation, ChatService } from './chat.service';
import { ChatThreadComponent } from './chat-thread.component';

/**
 * The client's chat with their coach (§41), mobile first, and conversations with previous coaches, read-only
 * (owner decision 2026-10-22).
 */
@Component({
  selector: 'app-my-chat',
  standalone: true,
  imports: [RouterLink, ChatThreadComponent, ...I18N_PIPES],
  template: `
    <section class="dashboard-hero"><div><p class="eyebrow">{{ 'chat.eyebrow' | translate }}</p><h1>{{ 'chat.title' | translate }}</h1>
      <p>{{ 'chat.intro' | translate }} <a routerLink="/app/files">{{ 'chat.filesLink' | translate }}</a></p></div></section>
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (viewing(); as archived) {
      <div class="line"><strong>{{ 'chat.archivedWith' | translate: { name: archived.coachName, date: (archived.archivedAt | localDate: 'd MMM y') } }}</strong>
        <button type="button" class="link-button" (click)="viewing.set(null)">{{ 'chat.backToCurrent' | translate }}</button></div>
      <app-chat-thread [archivedId]="archived.id" />
    } @else {
      <app-chat-thread />
    }
    @if (archived().length) {
      <section class="card workbench earlier">
        <h2>{{ 'chat.earlier' | translate }}</h2>
        <ul>
          @for (a of archived(); track a.id) {
            <li><button type="button" class="link-button" (click)="viewing.set(a)" translate="no">{{ a.coachName }}</button>
              <small class="muted">{{ 'chat.ended' | translate: { date: (a.archivedAt | localDate: 'd MMM y') } }}</small></li>
          }
        </ul>
      </section>
    }
  `,
  styles: [`
    .line { display: flex; justify-content: space-between; gap: 8px; flex-wrap: wrap; align-items: center; margin-bottom: 8px; }
    .earlier { margin-top: 16px; }
    .earlier h2 { font-size: 1rem; margin: 0 0 8px; }
    .earlier ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
    .earlier li { display: flex; gap: 8px; align-items: baseline; }
  `],
})
export class MyChatComponent implements OnInit {
  readonly archived = signal<ArchivedConversation[]>([]);
  readonly viewing = signal<ArchivedConversation | null>(null);
  readonly error = signal('');

  constructor(private readonly chat: ChatService) {}

  ngOnInit(): void {
    this.chat.mine().subscribe({ next: chat => this.archived.set(chat.archived), error: e => this.error.set(apiErrorMessage(e)) });
  }
}
