import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { I18nService } from './i18n.service';

/** Tells the API the interface language, so its messages and errors come back in it (P19). */
export const languageInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith(environment.apiBaseUrl) || request.headers.has('Accept-Language')) return next(request);
  return next(request.clone({ setHeaders: { 'Accept-Language': inject(I18nService).language() } }));
};
