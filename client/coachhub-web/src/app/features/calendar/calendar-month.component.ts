import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { isoDate } from '../nutrition-tracking/nutrition-tracking.service';
import { CalendarEntry, entryLabels, monthGrid, timeText } from './calendar.service';

interface Cell { date: string; inMonth: boolean; today: boolean; entries: CalendarEntry[]; }

/**
 * A month of calendar entries: a Monday-first grid on wide screens, a day-by-day agenda on phones (mobile first).
 * The parent loads the entries for <c>monthGrid(month)</c> and handles month changes and entry clicks.
 */
@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-calendar-month',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="head">
      <button type="button" class="button button-secondary" (click)="move(-1)" aria-label="Previous month">‹</button>
      <h2>{{ month | date: 'MMMM y' }}</h2>
      <button type="button" class="button button-secondary" (click)="move(1)" aria-label="Next month">›</button>
      <button type="button" class="link-button" (click)="today()">Today</button>
    </div>
    <div class="grid" role="grid" aria-label="Month">
      @for (d of weekdays; track d) { <div class="weekday" role="columnheader">{{ d }}</div> }
      @for (cell of cells; track cell.date) {
        <div class="cell" role="gridcell" [class.out]="!cell.inMonth" [class.today]="cell.today">
          <span class="number">{{ cell.date.slice(8) }}</span>
          @for (e of cell.entries.slice(0, 4); track $index) {
            <button type="button" class="chip" [ngClass]="classOf(e)" (click)="select.emit(e)" [title]="describe(e)">{{ short(e) }}</button>
          }
          @if (cell.entries.length > 4) { <small class="muted">+{{ cell.entries.length - 4 }} more</small> }
        </div>
      }
    </div>
    <ol class="agenda">
      @for (cell of agenda; track cell.date) {
        <li [class.today]="cell.today">
          <strong>{{ cell.date | date: 'EEE d MMM' }}</strong>
          <ul>
            @for (e of cell.entries; track $index) {
              <li><button type="button" class="row" [ngClass]="classOf(e)" (click)="select.emit(e)">
                <span class="tag">{{ labels[e.kind] }}</span> {{ describe(e) }}</button></li>
            }
          </ul>
        </li>
      } @empty { <li class="muted">Nothing this month.</li> }
    </ol>
  `,
  styles: [`
    .head { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; flex-wrap: wrap; }
    .head h2 { margin: 0 8px; font-size: 1.1rem; min-width: 150px; text-align: center; }
    .grid { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); border-top: 1px solid var(--gray-200); border-inline-start: 1px solid var(--gray-200); }
    .weekday { padding: 6px; font-size: 0.75rem; font-weight: 600; color: var(--gray-500); border-inline-end: 1px solid var(--gray-200); border-bottom: 1px solid var(--gray-200); }
    .cell { min-height: 104px; padding: 4px; display: flex; flex-direction: column; gap: 2px; border-inline-end: 1px solid var(--gray-200); border-bottom: 1px solid var(--gray-200); overflow: hidden; }
    .cell.out { background: var(--gray-50); color: var(--gray-400); }
    .cell.today .number { background: var(--brand); color: white; border-radius: 999px; padding: 0 6px; }
    .number { align-self: flex-start; font-size: 0.75rem; font-weight: 600; }
    .chip, .row { border: 0; border-radius: 6px; padding: 2px 6px; font-size: 0.72rem; text-align: start; cursor: pointer; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
      background: var(--gray-100); color: var(--gray-700); }
    .row { width: 100%; white-space: normal; font-size: 0.9rem; padding: 8px; min-height: 40px; }
    .workout { background: var(--brand-soft); color: var(--brand-dark); }
    .done { text-decoration: line-through; opacity: 0.75; }
    .missed { color: var(--error); }
    .rest { background: transparent; color: var(--gray-400); }
    .checkin { background: var(--gray-200); color: var(--gray-900); }
    .program { border: 1px dashed var(--gray-400); background: transparent; }
    .event { background: var(--brand); color: white; }
    .followup { background: var(--gray-700); color: white; }
    .tag { font-weight: 600; }
    .agenda { display: none; list-style: none; margin: 0; padding: 0; }
    .agenda > li { padding: 8px 0; border-bottom: 1px solid var(--gray-200); }
    .agenda > li.today strong { color: var(--brand); }
    .agenda ul { list-style: none; margin: 6px 0 0; padding: 0; display: grid; gap: 4px; }
    @media (max-width: 760px) { .grid { display: none; } .agenda { display: block; } }
  `],
})
export class CalendarMonthComponent implements OnChanges {
  @Input({ required: true }) month!: Date;
  @Input() entries: CalendarEntry[] = [];
  /** Hide rest days in the agenda list, which would otherwise fill it. */
  @Input() agendaSkipsRest = true;
  @Output() monthChange = new EventEmitter<Date>();
  @Output() select = new EventEmitter<CalendarEntry>();
  readonly weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  readonly labels = entryLabels;
  cells: Cell[] = [];
  agenda: Cell[] = [];

  ngOnChanges(): void {
    const grid = monthGrid(this.month);
    const byDate = new Map<string, CalendarEntry[]>();
    for (const entry of this.entries) byDate.set(entry.date, [...(byDate.get(entry.date) ?? []), entry]);
    const todayIso = isoDate(new Date());
    this.cells = grid.days.map(date => ({
      date,
      inMonth: Number(date.slice(5, 7)) === this.month.getMonth() + 1,
      today: date === todayIso,
      entries: byDate.get(date) ?? [],
    }));
    this.agenda = this.cells
      .filter(cell => cell.inMonth)
      .map(cell => ({ ...cell, entries: this.agendaSkipsRest ? cell.entries.filter(e => e.kind !== 'RestDay') : cell.entries }))
      .filter(cell => cell.entries.length);
  }

  move(months: number): void { this.monthChange.emit(new Date(this.month.getFullYear(), this.month.getMonth() + months, 1)); }
  today(): void { const now = new Date(); this.monthChange.emit(new Date(now.getFullYear(), now.getMonth(), 1)); }

  short(entry: CalendarEntry): string {
    const time = timeText(entry);
    const who = entry.clientName && entry.kind !== 'WorkoutDay' && entry.kind !== 'RestDay' ? `${entry.clientName}: ` : '';
    return `${time ? time + ' ' : ''}${who}${entry.kind === 'RestDay' ? 'Rest' : entry.title}`;
  }

  describe(entry: CalendarEntry): string {
    const parts = [entry.title];
    if (entry.clientName) parts.push(entry.clientName);
    if (timeText(entry)) parts.push(timeText(entry));
    if (entry.detail) parts.push(entry.detail);
    if (entry.kind === 'WorkoutDay' && entry.done !== null) parts.push(entry.done ? 'done' : 'missed');
    if (entry.kind === 'FollowUp' && entry.done) parts.push('done');
    return parts.join(' · ');
  }

  classOf(entry: CalendarEntry): Record<string, boolean> {
    return {
      workout: entry.kind === 'WorkoutDay',
      done: entry.done === true && (entry.kind === 'WorkoutDay' || entry.kind === 'FollowUp'),
      missed: entry.done === false && (entry.kind === 'WorkoutDay' || entry.kind === 'CheckInDue'),
      rest: entry.kind === 'RestDay',
      checkin: entry.kind === 'CheckIn' || entry.kind === 'CheckInDue',
      program: entry.kind === 'ProgramStart' || entry.kind === 'ProgramEnd',
      event: entry.kind === 'Event',
      followup: entry.kind === 'FollowUp',
    };
  }
}
