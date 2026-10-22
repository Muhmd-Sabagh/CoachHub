import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { CalendarEntry } from './calendar.service';
import { MyCalendarComponent } from './my-calendar.component';

describe('MyCalendarComponent hints (P18)', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  async function render(trainingWeekdays: string[], entries: Partial<CalendarEntry>[]): Promise<string> {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(MyCalendarComponent);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne(x => x.url === '/api/v1/me/calendar')
      .flush({ clientId: 'c', clientName: 'Sam', from: '', to: '', trainingWeekdays, entries });
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement.textContent as string;
  }

  it('says when the coach has not set training days', async () => {
    expect(await render([], [])).toContain('has not set your training days');
  });

  it('says workouts need a workout plan when training days are set but none are on the calendar', async () => {
    const text = await render(['Monday'], []);
    expect(text).toContain('while you have a workout plan');
    expect(text).not.toContain('has not set your training days');
  });

  it('shows no hint when workouts are on the calendar', async () => {
    const text = await render(['Monday'], [{ date: '2026-10-26', kind: 'WorkoutDay', title: 'Day A' }]);
    expect(text).not.toContain('while you have a workout plan');
    expect(text).not.toContain('has not set your training days');
  });
});
