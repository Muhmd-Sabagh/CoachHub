import { HttpErrorResponse } from '@angular/common/http';
export function apiErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) return 'An unexpected error occurred.';
  const problem = error.error as {
    title?: string;
    detail?: string;
    errors?: Record<string, string[]>;
  } | null;
  return (
    (problem?.errors ? Object.values(problem.errors).flat()[0] : undefined) ??
    problem?.detail ??
    problem?.title ??
    `Request failed (${error.status}).`
  );
}
