import { HttpErrorResponse } from '@angular/common/http';

/**
 * The message of a failed request: the API's (already in the request's language, P19), else a fallback in the page's
 * language (`<html lang>`, kept by I18nService).
 */
export function apiErrorMessage(error: unknown): string {
  const arabic = typeof document !== 'undefined' && document.documentElement.lang === 'ar';
  if (!(error instanceof HttpErrorResponse)) return arabic ? 'حدث خطأ غير متوقع.' : 'An unexpected error occurred.';
  const problem = error.error as {
    title?: string;
    detail?: string;
    errors?: Record<string, string[]>;
  } | null;
  return (
    (problem?.errors ? Object.values(problem.errors).flat()[0] : undefined) ??
    problem?.detail ??
    problem?.title ??
    (arabic ? `تعذّر تنفيذ الطلب (${error.status}).` : `Request failed (${error.status}).`)
  );
}
