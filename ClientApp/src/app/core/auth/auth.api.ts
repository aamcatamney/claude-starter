import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthenticatedUser, PendingVerification } from './user.model';

export interface LoginPayload {
  email: string;
  password: string;
  rememberMe: boolean;
}

export interface RegisterPayload {
  email: string;
  password: string;
  displayName: string | null;
}

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/auth';

  me(): Observable<AuthenticatedUser> {
    return this.http.get<AuthenticatedUser>(`${this.base}/me`);
  }

  login(payload: LoginPayload): Observable<AuthenticatedUser> {
    return this.http.post<AuthenticatedUser>(`${this.base}/login`, payload);
  }

  register(payload: RegisterPayload): Observable<AuthenticatedUser | PendingVerification> {
    return this.http.post<AuthenticatedUser | PendingVerification>(`${this.base}/register`, payload);
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post<void>(`${this.base}/forgot-password`, { email });
  }

  resetPassword(token: string, password: string): Observable<void> {
    return this.http.post<void>(`${this.base}/reset-password`, { token, password });
  }

  verifyEmail(token: string): Observable<void> {
    return this.http.post<void>(`${this.base}/verify-email`, { token });
  }

  resendVerification(email: string): Observable<void> {
    return this.http.post<void>(`${this.base}/resend-verification`, { email });
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.base}/logout`, null);
  }
}
