import { Component, Input, OnChanges, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { fileSize, maximumSharedFileBytes, sharedFileAccept } from '../chat/chat.service';
import { ClientFile, ClientFilesService, fileKind } from './client-files.service';

/**
 * Files shared with a client (§43). With <c>clientId</c> the coach shares, renames and removes them; without, the
 * signed-in client lists and opens them (mobile first).
 */
@Component({
  selector: 'app-client-files',
  standalone: true,
  imports: [FormsModule, ...I18N_PIPES],
  template: `
    @if (!clientId) {
      <section class="dashboard-hero"><div><p class="eyebrow">{{ 'files.eyebrow' | translate }}</p><h1>{{ 'files.title' | translate }}</h1>
        <p>{{ 'files.intro' | translate }}</p></div></section>
    }
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (clientId) {
      <form class="card workbench share" (ngSubmit)="share()">
        <h2>{{ 'files.share' | translate }}</h2>
        <label><span>{{ 'files.fileHint' | translate }}</span>
          <input type="file" [accept]="accept" (change)="pick($event)" required /></label>
        <label><span>{{ 'files.fileTitle' | translate }}</span><input [(ngModel)]="title" name="title" maxlength="200" [placeholder]="'files.titlePlaceholder' | translate" /></label>
        <label><span>{{ 'files.description' | translate }}</span><input [(ngModel)]="description" name="description" maxlength="1000" /></label>
        <button type="submit" class="button button-primary" [disabled]="!file || busy()">{{ (busy() ? 'files.uploading' : 'files.shareWithClient') | translate }}</button>
      </form>
    }
    <section class="card workbench">
      <ul class="files">
        @for (f of files(); track f.id) {
          <li>
            @if (editing() === f.id) {
              <form class="rename" (ngSubmit)="rename(f)">
                <input [(ngModel)]="editTitle" name="editTitle" maxlength="200" required [attr.aria-label]="'files.fileTitle' | translate" />
                <input [(ngModel)]="editDescription" name="editDescription" maxlength="1000" [attr.aria-label]="'files.description' | translate" />
                <button type="submit" class="button button-primary">{{ 'files.save' | translate }}</button>
                <button type="button" class="button button-secondary" (click)="editing.set(null)">{{ 'files.cancel' | translate }}</button>
              </form>
            } @else {
              <div class="file">
                <span class="kind">{{ kindLabel(f.contentType) | translate }}</span>
                <div class="info"><strong translate="no">{{ f.title }}</strong>
                  @if (f.description) { <span translate="no">{{ f.description }}</span> }
                  <small class="muted"><span translate="no">{{ f.fileName }}</span> · {{ size(f.sizeBytes) }} · {{ f.sharedAt | localDate: 'd MMM y' }}</small></div>
                <div class="actions">
                  <button type="button" class="button button-secondary" (click)="open(f)">{{ 'files.open' | translate }}</button>
                  @if (clientId) {
                    <button type="button" class="link-button" (click)="startEdit(f)">{{ 'files.rename' | translate }}</button>
                    <button type="button" class="link-button danger" (click)="remove(f)">{{ 'files.remove' | translate }}</button>
                  }
                </div>
              </div>
            }
          </li>
        } @empty { <li class="muted">{{ (clientId ? 'files.noneShared' : 'files.noneFromCoach') | translate }}</li> }
      </ul>
    </section>
  `,
  styles: [`
    .share { display: grid; gap: 10px; margin-bottom: 16px; }
    .share label { display: grid; gap: 4px; }
    .files { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
    .file { display: grid; grid-template-columns: auto 1fr; gap: 10px; align-items: start; }
    .kind { min-width: 64px; padding: 6px; border-radius: 8px; background: var(--gray-100); text-align: center; font-size: 0.75rem; font-weight: 600; }
    .info { display: grid; gap: 2px; overflow-wrap: anywhere; }
    .actions { grid-column: 2; display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
    .actions .button { min-height: 44px; }
    .rename { display: flex; flex-wrap: wrap; gap: 8px; }
  `],
})
export class ClientFilesComponent implements OnInit, OnChanges {
  @Input() clientId?: string;
  readonly accept = sharedFileAccept;
  readonly size = fileSize;
  readonly files = signal<ClientFile[]>([]);
  readonly editing = signal<string | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  file: File | null = null;
  title = '';
  description = '';
  editTitle = '';
  editDescription = '';

  constructor(private readonly api: ClientFilesService, private readonly i18n: I18nService) {}

  /** The file's kind (Image, Video, PDF …) as a label key. */
  kindLabel(contentType: string): TranslationKey { return `files.kind.${fileKind(contentType)}`; }

  ngOnInit(): void { if (!this.clientId) this.load(); }
  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.list(this.clientId).subscribe({ next: files => this.files.set(files), error: e => this.error.set(apiErrorMessage(e)) });
  }

  pick(event: Event): void {
    const chosen = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (chosen && chosen.size > maximumSharedFileBytes) {
      this.error.set(`${chosen.name} is larger than 50 MB.`);
      this.file = null;
      return;
    }
    this.error.set('');
    this.file = chosen;
  }

  share(): void {
    if (!this.clientId || !this.file) return;
    this.run(this.api.share(this.clientId, this.file, this.title, this.description), () => {
      this.file = null;
      this.title = '';
      this.description = '';
    });
  }

  startEdit(file: ClientFile): void {
    this.editing.set(file.id);
    this.editTitle = file.title;
    this.editDescription = file.description ?? '';
  }

  rename(file: ClientFile): void {
    if (!this.clientId) return;
    this.run(this.api.update(this.clientId, file.id, this.editTitle, this.editDescription.trim() || null), () => this.editing.set(null));
  }

  remove(file: ClientFile): void {
    if (!this.clientId || !confirm(this.i18n.translate('files.confirmRemove', { title: file.title }))) return;
    this.run(this.api.delete(this.clientId, file.id));
  }

  open(file: ClientFile): void {
    this.api.content(file.mediaId).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.target = '_blank';
        link.rel = 'noopener';
        if (fileKind(file.contentType) === 'Document' || fileKind(file.contentType) === 'Text') link.download = file.fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  private run(request: Observable<unknown>, done?: () => void): void {
    this.busy.set(true);
    request.subscribe({
      next: () => { this.busy.set(false); this.error.set(''); done?.(); this.load(); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e)); },
    });
  }
}
