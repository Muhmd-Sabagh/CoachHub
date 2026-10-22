import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { CoachesComponent } from './coaches.component';

/** Lets the zoneless scheduler run the change detection a signal change queued. */
const settle = () => new Promise<void>(resolve => setTimeout(resolve, 20));

/**
 * P18A: the older screens keep their asynchronous state in signals, so they render an HTTP result by themselves,
 * without the view-refresh interceptor P10A used, a manual change detection pass or any user input.
 */
describe('CoachesComponent (signal state, P18A)', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  function bootstrap(): HTMLElement {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const host = document.createElement('app-coaches');
    document.body.appendChild(host);
    TestBed.inject(ApplicationRef).bootstrap(CoachesComponent, host);
    return host;
  }

  it('shows the coaches once the response arrives', async () => {
    const host = bootstrap();
    await settle();
    TestBed.inject(HttpTestingController).expectOne(x => x.url === '/api/v1/admin/coaches').flush({
      items: [{ id: 'c1', displayName: 'Coach Sam', email: 'sam@example.test', userName: 'sam', isActive: true, clientCount: 3 }],
      pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1,
    });
    await settle();
    expect(host.textContent).toContain('Coach Sam');
    expect(host.textContent).not.toContain('Loading…');
  });

  it('shows an error response the same way', async () => {
    const host = bootstrap();
    await settle();
    TestBed.inject(HttpTestingController).expectOne(x => x.url === '/api/v1/admin/coaches')
      .flush({ title: 'Forbidden', detail: 'Administrators only.' }, { status: 403, statusText: 'Forbidden' });
    await settle();
    expect(host.querySelector('.form-error')).not.toBeNull();
  });
});
