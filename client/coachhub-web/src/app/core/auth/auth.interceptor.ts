import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, map, of, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

/** Endpoints that manage the session themselves and must never trigger a refresh. */
const sessionEndpoints = ['/auth/login', '/auth/refresh', '/auth/logout', '/auth/password-reset/'];

/**
 * Attaches the access token. An expired token is refreshed before the request; a 401 is retried
 * once after a refresh, and only then does the user have to sign in again.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  if (!request.url.startsWith(environment.apiBaseUrl) || request.headers.has('Authorization') ||
      sessionEndpoints.some(endpoint => request.url.includes(endpoint))) {
    return next(request);
  }

  const send = (token: string | null) => next(token ? withBearer(request, token) : request);
  const refreshedToken = (): Observable<string | null> =>
    auth.refresh().pipe(map(result => result.accessToken), catchError(() => of(null)));

  const token = auth.accessToken();
  const firstToken = token ? of(token) : auth.canRefresh() ? refreshedToken() : of(null);
  return firstToken.pipe(
    switchMap(current => send(current).pipe(catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) return throwError(() => error);
      if (!current || !auth.canRefresh()) {
        auth.expire();
        return throwError(() => error);
      }
      // The token was valid when sent but the server refused it (expired in flight, or the
      // session was revoked elsewhere): one refresh decides which.
      return refreshedToken().pipe(switchMap(fresh => {
        if (!fresh) {
          auth.expire();
          return throwError(() => error);
        }
        return next(withBearer(request, fresh));
      }));
    }))),
  );
};

function withBearer(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}
