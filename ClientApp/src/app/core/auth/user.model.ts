export interface AuthenticatedUser {
  id: string;
  email: string;
  displayName: string | null;
  isAdmin: boolean;
}

/** Returned by register instead of a user when verification is required. */
export interface PendingVerification {
  email: string;
  verificationRequired: true;
}

export function isPendingVerification(
  value: AuthenticatedUser | PendingVerification,
): value is PendingVerification {
  return 'verificationRequired' in value;
}

export type AuthStatus = 'unknown' | 'anonymous' | 'authed';

export interface ProblemDetails {
  type?: string;
  title?: string;
  detail?: string;
  status?: number;
}
