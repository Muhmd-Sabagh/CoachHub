import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { viewRefreshInterceptor } from './view-refresh.interceptor';

/** A screen written the old way: a plain field set in an HTTP callback, no signal, Eager like the older screens. */
@Component({
  selector: 'app-plain-screen',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `@if (name) { <p class="loaded">{{ name }}</p> } @else { <p class="loading">Loading…</p> }`,
})
class PlainScreenComponent implements OnInit {
  name = '';
  constructor(private readonly http: HttpClient) {}
  ngOnInit(): void { this.http.get<{ name: string }>('/api/v1/me').subscribe(x => (this.name = x.name)); }
}

const flushMicrotasks = () => new Promise<void>(resolve => setTimeout(resolve, 0));

/** Bootstraps the screen as the app's root component, as the real app is bootstrapped. */
function render() {
  const host = document.createElement('app-plain-screen');
  document.body.appendChild(host);
  const component = TestBed.inject(ApplicationRef).bootstrap(PlainScreenComponent, host);
  component.changeDetectorRef.detectChanges();
  return { nativeElement: host as HTMLElement };
}

describe('viewRefreshInterceptor (P10A: zoneless screens render HTTP results without user input)', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('renders a plain-field screen as soon as the response arrives', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([viewRefreshInterceptor])), provideHttpClientTesting()],
    });
    const fixture = render();
    expect(fixture.nativeElement.querySelector('.loading')).not.toBeNull();

    TestBed.inject(HttpTestingController).expectOne('/api/v1/me').flush({ name: 'Sam' });
    await flushMicrotasks();

    expect(fixture.nativeElement.querySelector('.loaded')?.textContent).toBe('Sam');
  });

  it('without it, the same screen waits for the next interaction (the zoneless gap it closes)', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = render();
    TestBed.inject(HttpTestingController).expectOne('/api/v1/me').flush({ name: 'Sam' });
    await flushMicrotasks();
    expect(fixture.nativeElement.querySelector('.loading')).not.toBeNull();
  });

  it('also refreshes after an error', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([viewRefreshInterceptor])), provideHttpClientTesting()],
    });
    const http = TestBed.inject(HttpClient);
    let failed = false;
    http.get('/api/v1/broken').subscribe({ error: () => (failed = true) });
    TestBed.inject(HttpTestingController).expectOne('/api/v1/broken').flush(null, { status: 500, statusText: 'Error' });
    await flushMicrotasks();
    expect(failed).toBe(true);
  });
});
