import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './user.model';

export type AuthErrorKind =
  | 'invalid-credentials'
  | 'email-taken'
  | 'validation'
  | 'rate-limited'
  | 'network'
  | 'unknown';

export interface AuthError {
  kind: AuthErrorKind;
  message: string;
}

const messages: Record<AuthErrorKind, string> = {
  'invalid-credentials': 'Invalid email or password.',
  'email-taken': 'That email is already registered.',
  validation: 'Please check the details you entered.',
  'rate-limited': 'Too many attempts. Try again shortly.',
  network: 'Connection failed. Check your network and try again.',
  unknown: 'Something went wrong. Please try again.',
};

export function toAuthError(error: unknown, context: 'login' | 'register' | 'me' | 'logout'): AuthError {
  if (!(error instanceof HttpErrorResponse)) {
    return { kind: 'unknown', message: messages.unknown };
  }

  if (error.status === 0) {
    return { kind: 'network', message: messages.network };
  }

  const problem = error.error as ProblemDetails | null;

  switch (error.status) {
    case 400:
      return {
        kind: 'validation',
        message: problem?.detail ?? problem?.title ?? messages.validation,
      };
    case 401:
      return {
        kind: 'invalid-credentials',
        message: context === 'login' ? messages['invalid-credentials'] : 'Your session has expired. Please sign in again.',
      };
    case 409:
      return { kind: 'email-taken', message: messages['email-taken'] };
    case 429:
      return { kind: 'rate-limited', message: messages['rate-limited'] };
    default:
      return { kind: 'unknown', message: messages.unknown };
  }
}
