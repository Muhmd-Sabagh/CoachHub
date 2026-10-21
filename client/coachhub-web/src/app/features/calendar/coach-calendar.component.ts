import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { apiErrorMessage } from '../../shared/services/api-error';
import { ClientsService } from '../clients/clients.service';
import { CalendarMonthComponent } from './calendar-month.component';
import { CalendarEntry, CalendarEventInput, CalendarService, Weekday, monthGrid, weekdayNames } from './calendar.service';

interface Draft extends CalendarEventInput { id: string | null; }

/**
 * The coach calendar (§46) on its own page: check-ins, program dates, client events and follow-ups across clients,
 * filterable by client. Inside a client profile (<c>clientId</c>) it is the client's calendar (§45) as the coach
 * edits it: the training weekdays, and events for the client. Coaches add and edit events; selecting a derived
 * entry opens the client.
 */
@Component({
  selector: 'app-coach-calendar',
  standalone: true,
  imports: [CommonModule, FormsModule, CalendarMonthComponent],
  template: `
    @if (!clientId) {
      <section class="dashboard-hero"><div><p class="eyebrow">Coaching</p><h1>Calendar</h1>
        <p>Check-ins, program dates, events and follow-ups across your clients.</p></div>
        <button type="button" class="button button-primary" (click)="startNew()">New event</button></section>
    }
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }

    @if (clientId) {
      <section class="card workbench training">
        <h2>Training days</h2>
        <p class="muted">The workout rotation fills these days in order each week; the other days are rest days.</p>
        <div class="weekdays" role="group" aria-label="Training days">
          @for (d of days; track d) {
            <label><input type="checkbox" [checked]="training().includes(d)" (change)="toggleDay(d)" /> {{ d.slice(0, 3) }}</label>
          }
        </div>
        <div class="actions">
          <button type="button" class="button button-primary" (click)="saveDays()" [disabled]="busy() || !training().length">Save training days</button>
          <button type="button" class="button button-secondary" (click)="startNew()">New event for this client</button>
        </div>
      </section>
    } @else {
      <section class="card workbench filters">
        <label><span>Client</span>
          <select [(ngModel)]="filterClient" name="filterClient" (change)="load()">
            <option [ngValue]="null">All clients</option>
            @for (c of clients(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
          </select></label>
      </section>
    }

    @if (draft(); as d) {
      <form class="card workbench editor" (ngSubmit)="save(d)">
        <h2>{{ d.id ? 'Edit' : 'New' }} {{ d.kind === 'FollowUp' ? 'follow-up' : 'event' }}</h2>
        <div class="form-grid">
          <label><span>Kind</span><select [(ngModel)]="d.kind" name="kind" (change)="d.kind === 'FollowUp' && (d.visibleToClient = false)">
            <option value="Event">Event</option><option value="FollowUp">Follow-up</option></select></label>
          @if (!clientId) {
            <label><span>Client</span><select [(ngModel)]="d.clientId" name="client" [disabled]="!!d.id">
              <option [ngValue]="null">None (my own)</option>
              @for (c of clients(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
            </select></label>
          }
          <label><span>Title</span><input [(ngModel)]="d.title" name="title" required maxlength="200" /></label>
          <label><span>Date</span><input type="date" [(ngModel)]="d.date" name="date" required /></label>
          <label><span>Start</span><input type="time" [(ngModel)]="d.startTime" name="start" /></label>
          <label><span>End</span><input type="time" [(ngModel)]="d.endTime" name="end" /></label>
        </div>
        <label class="wide"><span>Notes</span><textarea [(ngModel)]="d.notes" name="notes" rows="2" maxlength="1000"></textarea></label>
        @if (d.kind === 'Event' && d.clientId) {
          <label class="check"><input type="checkbox" [(ngModel)]="d.visibleToClient" name="visible" /> Show on the client's calendar</label>
        }
        @if (d.kind === 'FollowUp' && d.id) { <label class="check"><input type="checkbox" [(ngModel)]="d.isDone" name="done" /> Done</label> }
        <div class="actions">
          <button type="submit" class="button button-primary" [disabled]="busy()">Save</button>
          <button type="button" class="button button-secondary" (click)="draft.set(null)">Cancel</button>
          @if (d.id) { <button type="button" class="link-button danger" (click)="remove(d)">Delete</button> }
        </div>
      </form>
    }

    <section class="card workbench">
      <app-calendar-month [month]="month()" [entries]="entries()" (monthChange)="go($event)" (select)="open($event)" />
    </section>
  `,
  styles: [`
    .dashboard-hero { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; flex-wrap: wrap; }
    .training, .editor, .filters { display: grid; gap: 10px; margin-bottom: 16px; }
    .training h2, .editor h2 { margin: 0; font-size: 1.05rem; }
    .weekdays { display: flex; flex-wrap: wrap; gap: 12px; }
    .weekdays label, .check { display: flex; gap: 6px; align-items: center; min-height: 40px; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
    .wide, .wide textarea { display: grid; gap: 4px; width: 100%; }
    .filters label { display: grid; gap: 4px; max-width: 320px; }
  `],
})
export class CoachCalendarComponent implements OnInit, OnChanges {
  @Input() clientId?: string;
  readonly days = weekdayNames;
  readonly month = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  readonly entries = signal<CalendarEntry[]>([]);
  readonly training = signal<Weekday[]>([]);
  readonly clients = signal<{ id: string; name: string }[]>([]);
  readonly draft = signal<Draft | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  filterClient: string | null = null;

  constructor(private readonly api: CalendarService, private readonly clientsApi: ClientsService, private readonly router: Router) {}

  ngOnInit(): void {
    if (this.clientId) return;
    this.clientsApi.list({ pageSize: 100, isActive: true }).subscribe({
      next: page => this.clients.set(page.items.map(x => ({ id: x.id, name: x.name }))),
      error: () => undefined,
    });
    this.load();
  }

  ngOnChanges(): void { if (this.clientId) this.load(); }

  load(): void {
    const { from, to } = monthGrid(this.month());
    if (this.clientId) {
      this.api.client(from, to, this.clientId).subscribe({
        next: c => { this.entries.set(c.entries); this.training.set(c.trainingWeekdays); },
        error: e => this.error.set(apiErrorMessage(e)),
      });
    } else {
      this.api.coach(from, to, this.filterClient).subscribe({ next: c => this.entries.set(c.entries), error: e => this.error.set(apiErrorMessage(e)) });
    }
  }

  go(month: Date): void { this.month.set(month); this.load(); }

  toggleDay(day: Weekday): void {
    this.training.update(days => (days.includes(day) ? days.filter(x => x !== day) : [...days, day]));
  }

  saveDays(): void {
    if (!this.clientId) return;
    this.run(this.api.setTrainingDays(this.clientId, this.training()));
  }

  startNew(): void {
    const date = new Date();
    this.draft.set({
      id: null, clientId: this.clientId ?? this.filterClient, kind: 'Event', title: '', notes: null,
      date: `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`,
      startTime: null, endTime: null, visibleToClient: !!(this.clientId ?? this.filterClient), isDone: false,
    });
  }

  open(entry: CalendarEntry): void {
    if ((entry.kind === 'Event' || entry.kind === 'FollowUp') && entry.referenceId) {
      this.draft.set({
        id: entry.referenceId, clientId: entry.clientId, kind: entry.kind, title: entry.title, notes: entry.detail, date: entry.date,
        startTime: entry.startTime?.slice(0, 5) ?? null, endTime: entry.endTime?.slice(0, 5) ?? null,
        visibleToClient: entry.visibleToClient === true,
        isDone: entry.done === true,
      });
      return;
    }
    if (!this.clientId && entry.clientId) void this.router.navigate(['/clients', entry.clientId]);
  }

  save(draft: Draft): void {
    const input: CalendarEventInput = {
      clientId: draft.clientId, kind: draft.kind, title: draft.title.trim(), date: draft.date, notes: draft.notes?.trim() || null,
      startTime: draft.startTime || null, endTime: draft.endTime || null,
      visibleToClient: draft.kind === 'Event' && !!draft.clientId && draft.visibleToClient, isDone: !!draft.isDone,
    };
    this.run(draft.id ? this.api.updateEvent(draft.id, input) : this.api.createEvent(input), () => this.draft.set(null));
  }

  remove(draft: Draft): void {
    if (!draft.id || !confirm(`Delete "${draft.title}"?`)) return;
    this.run(this.api.deleteEvent(draft.id), () => this.draft.set(null));
  }

  private run(request: Observable<unknown>, done?: () => void): void {
    this.busy.set(true);
    request.subscribe({
      next: () => { this.busy.set(false); this.error.set(''); done?.(); this.load(); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e)); },
    });
  }
}
