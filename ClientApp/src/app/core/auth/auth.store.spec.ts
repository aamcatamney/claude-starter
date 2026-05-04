import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, throwError } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import { AuthApi } from './auth.api';
import { AuthStore } from './auth.store';
import { AuthenticatedUser } from './user.model';

const sampleUser: AuthenticatedUser = {
  id: '00000000-0000-0000-0000-000000000001',
  email: 'jane@example.com',
  displayName: 'Jane',
};

function asObservable<T>(value: T): Observable<T> {
  return new Observable<T>((subscriber) => {
    subscriber.next(value);
    subscriber.complete();
  });
}

function createApiMock(overrides: Partial<AuthApi>): AuthApi {
  return {
    me: vi.fn(),
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    ...overrides,
  } as unknown as AuthApi;
}

describe('AuthStore', () => {
  let api: AuthApi;

  beforeEach(() => {
    api = createApiMock({});
    TestBed.configureTestingModule({
      providers: [{ provide: AuthApi, useFactory: () => api }],
    });
  });

  it('starts in unknown status', () => {
    const store = TestBed.inject(AuthStore);
    expect(store.status()).toBe('unknown');
    expect(store.isAuthed()).toBe(false);
    expect(store.isResolved()).toBe(false);
  });

  it('loadMe sets authed on success', async () => {
    api = createApiMock({ me: () => asObservable(sampleUser) });
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useFactory: () => api }] });
    const store = TestBed.inject(AuthStore);

    await store.loadMe();

    expect(store.status()).toBe('authed');
    expect(store.user()).toEqual(sampleUser);
    expect(store.isAuthed()).toBe(true);
  });

  it('loadMe sets anonymous on 401', async () => {
    api = createApiMock({ me: () => throwError(() => new HttpErrorResponse({ status: 401 })) });
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useFactory: () => api }] });
    const store = TestBed.inject(AuthStore);

    await store.loadMe();

    expect(store.status()).toBe('anonymous');
    expect(store.user()).toBeNull();
  });

  it('login success patches user + clears error', async () => {
    api = createApiMock({ login: () => asObservable(sampleUser) });
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useFactory: () => api }] });
    const store = TestBed.inject(AuthStore);

    const ok = await store.login({ email: 'a@b.co', password: 'a'.repeat(12), rememberMe: false });

    expect(ok).toBe(true);
    expect(store.user()).toEqual(sampleUser);
    expect(store.error()).toBeNull();
    expect(store.pending()).toBe(false);
  });

  it('login failure stores error', async () => {
    api = createApiMock({
      login: () => throwError(() => new HttpErrorResponse({ status: 401 })),
    });
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useFactory: () => api }] });
    const store = TestBed.inject(AuthStore);

    const ok = await store.login({ email: 'a@b.co', password: 'a'.repeat(12), rememberMe: false });

    expect(ok).toBe(false);
    expect(store.error()?.kind).toBe('invalid-credentials');
    expect(store.user()).toBeNull();
  });

  it('register success signs the user in', async () => {
    api = createApiMock({ register: () => asObservable(sampleUser) });
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useFactory: () => api }] });
    const store = TestBed.inject(AuthStore);

    const ok = await store.register({ email: 'a@b.co', password: 'a'.repeat(12), displayName: 'Jane' });

    expect(ok).toBe(true);
    expect(store.status()).toBe('authed');
  });

  it('register conflict surfaces email-taken', async () => {
    api = createApiMock({
      register: () => throwError(() => new HttpErrorResponse({ status: 409 })),
    });
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useFactory: () => api }] });
    const store = TestBed.inject(AuthStore);

    await store.register({ email: 'a@b.co', password: 'a'.repeat(12), displayName: null });

    expect(store.error()?.kind).toBe('email-taken');
  });

  it('logout clears state even when server fails', async () => {
    api = createApiMock({
      me: () => asObservable(sampleUser),
      logout: () => throwError(() => new HttpErrorResponse({ status: 401 })),
    });
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useFactory: () => api }] });
    const store = TestBed.inject(AuthStore);
    await store.loadMe();
    expect(store.status()).toBe('authed');

    await store.logout();

    expect(store.status()).toBe('anonymous');
    expect(store.user()).toBeNull();
  });

  it('displayName falls back to email when displayName missing', async () => {
    const user: AuthenticatedUser = { ...sampleUser, displayName: null };
    api = createApiMock({ me: () => asObservable(user) });
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useFactory: () => api }] });
    const store = TestBed.inject(AuthStore);

    await store.loadMe();

    expect(store.displayName()).toBe(user.email);
  });
});
