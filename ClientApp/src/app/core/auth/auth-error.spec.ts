import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { toAuthError } from './auth-error';

function http(status: number, body: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: body });
}

describe('toAuthError', () => {
  it('maps 401 on login to invalid credentials', () => {
    const err = toAuthError(http(401), 'login');
    expect(err.kind).toBe('invalid-credentials');
    expect(err.message).toMatch(/Invalid email or password/);
  });

  it('maps 401 on /me to a session-expired message', () => {
    const err = toAuthError(http(401), 'me');
    expect(err.kind).toBe('invalid-credentials');
    expect(err.message).toMatch(/session has expired/i);
  });

  it('maps 409 to email-taken', () => {
    const err = toAuthError(http(409), 'register');
    expect(err.kind).toBe('email-taken');
  });

  it('maps 429 to rate-limited', () => {
    const err = toAuthError(http(429), 'login');
    expect(err.kind).toBe('rate-limited');
  });

  it('maps 0 to network', () => {
    const err = toAuthError(http(0), 'login');
    expect(err.kind).toBe('network');
  });

  it('uses problem detail for 400 when present', () => {
    const err = toAuthError(http(400, { title: 'Invalid password', detail: 'Password must be at least 12 characters.' }), 'register');
    expect(err.kind).toBe('validation');
    expect(err.message).toBe('Password must be at least 12 characters.');
  });

  it('falls back to generic for 400 without detail', () => {
    const err = toAuthError(http(400), 'register');
    expect(err.kind).toBe('validation');
  });

  it('returns unknown for non-HttpErrorResponse', () => {
    const err = toAuthError(new Error('boom'), 'login');
    expect(err.kind).toBe('unknown');
  });

  it('returns unknown for unhandled status', () => {
    const err = toAuthError(http(500), 'login');
    expect(err.kind).toBe('unknown');
  });
});
