import { HttpInterceptorFn } from '@angular/common/http';
import { ApplicationRef, inject } from '@angular/core';
import { finalize } from 'rxjs';

let scheduled = false;

/**
 * The app runs without zone.js, and Angular 22 makes components OnPush by default (P10A). Signal-based screens refresh
 * themselves. The older screens keep plain fields set in HTTP callbacks; they declare
 * <c>ChangeDetectionStrategy.Eager</c> (as do the shells above them), and this interceptor checks the application from
 * its root components once every response settles (success or error), batched per microtask, as zone.js used to. New
 * screens keep asynchronous state in signals and stay OnPush.
 */
export const viewRefreshInterceptor: HttpInterceptorFn = (request, next) => {
  const app = inject(ApplicationRef);
  // finalize runs after the subscriber's own next/error/complete handlers, so their changes are included.
  return next(request).pipe(finalize(() => refresh(app)));
};

function refresh(app: ApplicationRef): void {
  if (scheduled) return;
  scheduled = true;
  queueMicrotask(() => {
    scheduled = false;
    // detectChanges checks every Eager view below the root (tick() would only refresh views marked dirty).
    for (const component of app.components) {
      try {
        component.changeDetectorRef.detectChanges();
      } catch {
        // A pass already in progress shows the same changes.
      }
    }
  });
}
