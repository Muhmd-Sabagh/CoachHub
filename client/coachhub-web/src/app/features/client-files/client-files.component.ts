import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
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
  imports: [CommonModule, FormsModule],
  template: `
    @if (!clientId) {
      <section class="dashboard-hero"><div><p class="eyebrow">Files</p><h1>From your coach</h1>
        <p>Guides, videos and documents your coach shared with you.</p></div></section>
    }
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (clientId) {
      <form class="card workbench share" (ngSubmit)="share()">
        <h2>Share a file</h2>
        <label><span>File (PDF, image, video, Word, Excel, PowerPoint or text; up to 50 MB)</span>
          <input type="file" [accept]="accept" (change)="pick($event)" required /></label>
        <label><span>Title</span><input [(ngModel)]="title" name="title" maxlength="200" placeholder="Defaults to the file name" /></label>
        <label><span>Description</span><input [(ngModel)]="description" name="description" maxlength="1000" /></label>
        <button type="submit" class="button button-primary" [disabled]="!file || busy()">{{ busy() ? 'Uploading…' : 'Share with client' }}</button>
      </form>
    }
    <section class="card workbench">
      <ul class="files">
        @for (f of files(); track f.id) {
          <li>
            @if (editing() === f.id) {
              <form class="rename" (ngSubmit)="rename(f)">
                <input [(ngModel)]="editTitle" name="editTitle" maxlength="200" required aria-label="Title" />
                <input [(ngModel)]="editDescription" name="editDescription" maxlength="1000" aria-label="Description" />
                <button type="submit" class="button button-primary">Save</button>
                <button type="button" class="button button-secondary" (click)="editing.set(null)">Cancel</button>
              </form>
            } @else {
              <div class="file">
                <span class="kind">{{ kind(f.contentType) }}</span>
                <div class="info"><strong>{{ f.title }}</strong>
                  @if (f.description) { <span>{{ f.description }}</span> }
                  <small class="muted">{{ f.fileName }} · {{ size(f.sizeBytes) }} · {{ f.sharedAt | date: 'd MMM y' }}</small></div>
                <div class="actions">
                  <button type="button" class="button button-secondary" (click)="open(f)">Open</button>
                  @if (clientId) {
                    <button type="button" class="link-button" (click)="startEdit(f)">Rename</button>
                    <button type="button" class="link-button danger" (click)="remove(f)">Remove</button>
                  }
                </div>
              </div>
            }
          </li>
        } @empty { <li class="muted">{{ clientId ? 'Nothing shared yet.' : 'Your coach has not shared any files yet.' }}</li> }
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
  readonly kind = fileKind;
  readonly files = signal<ClientFile[]>([]);
  readonly editing = signal<string | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  file: File | null = null;
  title = '';
  description = '';
  editTitle = '';
  editDescription = '';

  constructor(private readonly api: ClientFilesService) {}

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
    if (!this.clientId || !confirm(`Remove "${file.title}"? The client will no longer see it.`)) return;
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
