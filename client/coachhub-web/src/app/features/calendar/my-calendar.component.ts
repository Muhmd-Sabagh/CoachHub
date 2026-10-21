import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { apiErrorMessage } from '../../shared/services/api-error';
import { CalendarMonthComponent } from './calendar-month.component';
import { CalendarEntry, CalendarService, monthGrid } from './calendar.service';

/** The client's calendar (§45), mobile first: workout and rest days, check-ins, diet and workout plan dates, events. */
@Component({
  selector: 'app-my-calendar',
  standalone: true,
  imports: [CommonModule, CalendarMonthComponent],
  template: `
    <section class="dashboard-hero"><div><p class="eyebrow">Calendar</p><h1>My month</h1>
      <p>Your training days, check-ins, plans and events from your coach.</p></div></section>
    @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
    @if (!training() && loaded()) { <p class="muted" role="status">Your coach has not set your training days yet, so workouts are not on the calendar.</p> }
    <section class="card workbench">
      <app-calendar-month [month]="month()" [entries]="entries()" (monthChange)="go($event)" (select)="open($event)" />
    </section>
  `,
})
export class MyCalendarComponent implements OnInit {
  readonly month = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  readonly entries = signal<CalendarEntry[]>([]);
  readonly training = signal(true);
  readonly loaded = signal(false);
  readonly error = signal('');

  constructor(private readonly api: CalendarService, private readonly router: Router) {}

  ngOnInit(): void { this.load(); }

  go(month: Date): void { this.month.set(month); this.load(); }

  load(): void {
    const { from, to } = monthGrid(this.month());
    this.api.client(from, to).subscribe({
      next: c => { this.entries.set(c.entries); this.training.set(c.trainingWeekdays.length > 0); this.loaded.set(true); },
      error: e => this.error.set(apiErrorMessage(e)),
    });
  }

  open(entry: CalendarEntry): void {
    const target = entry.kind === 'WorkoutDay' || entry.kind === 'RestDay' ? '/app/training'
      : entry.kind === 'CheckIn' || entry.kind === 'CheckInDue' ? '/app/forms'
      : entry.planKind === 'Diet' ? '/app/nutrition' : entry.planKind === 'Workout' ? '/app/training' : null;
    if (target) void this.router.navigateByUrl(target);
  }
}
