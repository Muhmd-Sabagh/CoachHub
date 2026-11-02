import { HttpClient } from '@angular/common/http';
import { Component, Input, OnDestroy, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { I18N_PIPES } from '../../pipes/translate.pipe';
import { apiErrorMessage } from '../../services/api-error';

/**
 * An uploaded video (P20: exercises, tests), played inline. Media is served only to signed-in users, so the file is
 * fetched with the session and played from an object URL; it loads when asked, not with the page.
 */
@Component({
  selector: 'app-media-video',
  standalone: true,
  imports: [...I18N_PIPES],
  template: `
    @if (url(); as src) {
      <video class="media-video" controls autoplay playsinline [src]="src" [attr.aria-label]="label || ('video.label' | translate)"></video>
    } @else {
      <button class="link-button" type="button" [disabled]="loading()" (click)="play()">
        {{ (loading() ? 'video.loading' : 'video.play') | translate }}</button>
    }
    @if (error()) { <small class="form-error" role="alert">{{ error() }}</small> }
  `,
  styles: [`
    :host { display: inline-grid; gap: 4px; max-width: 100%; }
    .media-video { width: 100%; max-width: 480px; max-height: 320px; border-radius: 8px; background: var(--gray-900); }
  `],
})
export class MediaVideoComponent implements OnDestroy {
  @Input({ required: true }) mediaId!: string;
  /** What the video shows, for screen readers (for example the exercise's name). */
  @Input() label = '';
  readonly url = signal<string | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');

  constructor(private readonly http: HttpClient) {}

  play(): void {
    this.loading.set(true);
    this.error.set('');
    this.http.get(`${environment.apiBaseUrl}/media/${this.mediaId}`, { responseType: 'blob' }).subscribe({
      next: blob => { this.loading.set(false); this.url.set(URL.createObjectURL(blob)); },
      error: e => { this.loading.set(false); this.error.set(apiErrorMessage(e)); },
    });
  }

  ngOnDestroy(): void {
    const url = this.url();
    if (url) URL.revokeObjectURL(url);
  }
}
