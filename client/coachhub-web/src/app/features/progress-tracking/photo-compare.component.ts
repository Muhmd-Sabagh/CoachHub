import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PhotoPose, ProgressPhoto, ProgressTrackingService, beforeAfter, photoPoses } from './progress-tracking.service';

/**
 * Progress photos (§26): the timeline, and a before/after comparison of any two photos side by side or with a
 * slider. Images are fetched with the session (they are private to the client and coach) and shown as blob URLs.
 */
@Component({
  selector: 'app-photo-compare',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (photos.length) {
      <section class="compare" aria-label="Compare photos">
        <div class="compare-controls">
          <label><span>Before</span>
            <select [ngModel]="beforeId()" (ngModelChange)="choose('before', $event)" name="before">
              @for (p of chronological(); track p.id) { <option [ngValue]="p.id">{{ p.date | date: 'd MMM y' }} · {{ poseName(p) }}</option> }
            </select></label>
          <label><span>After</span>
            <select [ngModel]="afterId()" (ngModelChange)="choose('after', $event)" name="after">
              @for (p of chronological(); track p.id) { <option [ngValue]="p.id">{{ p.date | date: 'd MMM y' }} · {{ poseName(p) }}</option> }
            </select></label>
          <div class="segmented" role="group" aria-label="Comparison view">
            <button type="button" [class.active]="mode() === 'side'" (click)="mode.set('side')">Side by side</button>
            <button type="button" [class.active]="mode() === 'slider'" (click)="mode.set('slider')">Slider</button>
          </div>
        </div>
        @if (mode() === 'side') {
          <div class="side-by-side">
            <figure>@if (url(beforeId()); as src) { <img [src]="src" alt="Before" /> }<figcaption>{{ caption(beforeId()) }}</figcaption></figure>
            <figure>@if (url(afterId()); as src) { <img [src]="src" alt="After" /> }<figcaption>{{ caption(afterId()) }}</figcaption></figure>
          </div>
        } @else {
          <div class="slider-frame">
            @if (url(beforeId()); as src) { <img class="base" [src]="src" alt="Before" /> }
            @if (url(afterId()); as src) { <img class="overlay" [src]="src" alt="After" [style.clip-path]="'inset(0 0 0 ' + split() + '%)'" /> }
            <span class="divider" [style.left.%]="split()" aria-hidden="true"></span>
          </div>
          <label class="slider-control"><span class="muted">{{ caption(beforeId()) }}</span>
            <input type="range" min="0" max="100" [ngModel]="split()" (ngModelChange)="split.set($event)" name="split" aria-label="Before and after" />
            <span class="muted">{{ caption(afterId()) }}</span></label>
        }
      </section>

      <h3>Timeline</h3>
      <ul class="timeline">
        @for (p of photos; track p.id) {
          <li>
            @if (url(p.id); as src) { <img [src]="src" [alt]="poseName(p) + ' ' + p.date" loading="lazy" /> } @else { <span class="placeholder"></span> }
            <small>{{ p.date | date: 'd MMM y' }} · {{ poseName(p) }}</small>
            @if (deletable) { <button class="link-button danger" type="button" (click)="remove(p)">Delete</button> }
          </li>
        }
      </ul>
    } @else { <p class="muted">No progress photos yet.</p> }
  `,
  styles: [`
    .compare { display: grid; gap: 10px; margin-bottom: 16px; }
    .compare-controls { display: flex; flex-wrap: wrap; gap: 8px 12px; align-items: end; }
    .compare-controls label { display: grid; gap: 4px; }
    .segmented { display: inline-flex; border: 1px solid var(--gray-200); border-radius: 8px; overflow: hidden; }
    .segmented button { padding: 6px 12px; border: 0; background: transparent; color: inherit; min-height: 40px; }
    .segmented button.active { background: var(--brand-soft); color: var(--brand-dark); font-weight: 600; }
    .side-by-side { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
    figure { margin: 0; display: grid; gap: 4px; }
    img { width: 100%; border-radius: 10px; object-fit: cover; aspect-ratio: 3 / 4; background: var(--gray-100); }
    figcaption { font-size: 0.8rem; color: var(--gray-500); text-align: center; }
    .slider-frame { position: relative; max-width: 480px; }
    .slider-frame .overlay { position: absolute; inset: 0; }
    .divider { position: absolute; top: 0; bottom: 0; width: 2px; background: var(--brand); transform: translateX(-1px); }
    .slider-control { display: grid; grid-template-columns: auto 1fr auto; gap: 8px; align-items: center; max-width: 480px; font-size: 0.8rem; }
    .timeline { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(110px, 1fr)); gap: 10px; }
    .timeline li { display: grid; gap: 4px; }
    .placeholder { display: block; aspect-ratio: 3 / 4; border-radius: 10px; background: var(--gray-100); }
  `],
})
export class PhotoCompareComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) photos: ProgressPhoto[] = [];
  @Input() deletable = false;
  @Output() readonly deleted = new EventEmitter<ProgressPhoto>();
  readonly beforeId = signal<string | null>(null);
  readonly afterId = signal<string | null>(null);
  readonly mode = signal<'side' | 'slider'>('side');
  readonly split = signal(50);
  private readonly urls = signal<Record<string, string>>({});
  private requested = new Set<string>();

  constructor(private readonly api: ProgressTrackingService) {}

  ngOnChanges(): void {
    const ids = new Set(this.photos.map(x => x.id));
    if (!this.beforeId() || !ids.has(this.beforeId()!) || !this.afterId() || !ids.has(this.afterId()!)) {
      const pair = photoPoses.map(pose => beforeAfter(this.photos, pose as PhotoPose)).find(Boolean)
        ?? (this.photos.length ? [this.chronological().at(0)!, this.chronological().at(-1)!] as const : null);
      this.beforeId.set(pair?.[0].id ?? null);
      this.afterId.set(pair?.[1].id ?? null);
    }
    for (const photo of this.photos) this.load(photo);
  }

  ngOnDestroy(): void {
    for (const url of Object.values(this.urls())) URL.revokeObjectURL(url);
  }

  chronological(): ProgressPhoto[] {
    return this.photos.slice().sort((a, b) => a.date.localeCompare(b.date) || a.createdAt.localeCompare(b.createdAt));
  }

  choose(which: 'before' | 'after', id: string): void {
    (which === 'before' ? this.beforeId : this.afterId).set(id);
  }

  url(photoId: string | null): string | null {
    const photo = this.photos.find(x => x.id === photoId);
    return photo ? this.urls()[photo.mediaId] ?? null : null;
  }

  caption(photoId: string | null): string {
    const photo = this.photos.find(x => x.id === photoId);
    return photo ? `${photo.date} · ${this.poseName(photo)}` : '';
  }

  poseName(photo: ProgressPhoto): string {
    return photo.pose === 'Custom' && photo.label ? photo.label : photo.pose;
  }

  remove(photo: ProgressPhoto): void {
    this.deleted.emit(photo);
  }

  private load(photo: ProgressPhoto): void {
    if (this.requested.has(photo.mediaId)) return;
    this.requested.add(photo.mediaId);
    this.api.image(photo.mediaId).subscribe({
      next: url => this.urls.set({ ...this.urls(), [photo.mediaId]: url }),
      error: () => this.requested.delete(photo.mediaId),
    });
  }
}
