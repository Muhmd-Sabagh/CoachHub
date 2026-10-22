import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { Role } from './auth.models';
import { AuthService } from './auth.service';

/**
 * UX only: the API is the authorization boundary and enforces roles. Refreshes an expired access token first, sends
 * signed-out users to the login page and signed-in users without the role to their landing page.
 */
export const roleGuard = (...roles: Role[]): CanActivateFn => (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.ensureSession().pipe(map(signedIn => {
    if (!signedIn) return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    return roles.some(role => auth.hasRole(role)) ? true : router.parseUrl(auth.landingRoute());
  }));
};
