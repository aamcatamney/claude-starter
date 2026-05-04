export interface AuthenticatedUser {
  id: string;
  email: string;
  displayName: string | null;
}

export type AuthStatus = 'unknown' | 'anonymous' | 'authed';

export interface ProblemDetails {
  type?: string;
  title?: string;
  detail?: string;
  status?: number;
}
