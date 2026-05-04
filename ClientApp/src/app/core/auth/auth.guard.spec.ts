import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Router } from '@angular/router';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import { authGuard, redirectIfAuthedGuard } from './auth.guard';
import { AuthStore } from './auth.store';

interface AuthStoreStub {
  isAuthed: () => boolean;
}

function setupGuardEnv(isAuthed: boolean) {
  const stub: AuthStoreStub = { isAuthed: () => isAuthed };
  const navigate = vi.fn();
  const createUrlTree = vi.fn().mockReturnValue({ kind: 'tree-from-create' } as unknown as UrlTree);
  const parseUrl = vi.fn((url: string) => ({ kind: 'tree-from-parse', url }) as unknown as UrlTree);
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthStore, useValue: stub },
      { provide: Router, useValue: { navigate, createUrlTree, parseUrl } },
    ],
  });
  return { stub, navigate, createUrlTree, parseUrl };
}

function runGuard<T>(fn: () => T): T {
  return TestBed.runInInjectionContext(fn);
}

function snapshot(url: string, queryParams: Record<string, string> = {}): { route: ActivatedRouteSnapshot; state: RouterStateSnapshot } {
  const route = {
    queryParamMap: {
      get: (k: string) => queryParams[k] ?? null,
    },
  } as unknown as ActivatedRouteSnapshot;
  const state = { url } as RouterStateSnapshot;
  return { route, state };
}

describe('authGuard', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('allows authed user', () => {
    setupGuardEnv(true);
    const { route, state } = snapshot('/');
    const result = runGuard(() => authGuard(route, state));
    expect(result).toBe(true);
  });

  it('redirects unauthed user to /login with returnUrl', () => {
    const { createUrlTree } = setupGuardEnv(false);
    const { route, state } = snapshot('/dashboard');
    runGuard(() => authGuard(route, state));
    expect(createUrlTree).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/dashboard' } });
  });
});

describe('redirectIfAuthedGuard', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('allows anonymous user', () => {
    setupGuardEnv(false);
    const { route, state } = snapshot('/login');
    const result = runGuard(() => redirectIfAuthedGuard(route, state));
    expect(result).toBe(true);
  });

  it('parses returnUrl when authed and returnUrl is relative', () => {
    const { parseUrl } = setupGuardEnv(true);
    const { route, state } = snapshot('/login', { returnUrl: '/secret' });
    runGuard(() => redirectIfAuthedGuard(route, state));
    expect(parseUrl).toHaveBeenCalledWith('/secret');
  });

  it('rejects absolute returnUrl (open-redirect protection)', () => {
    const { parseUrl } = setupGuardEnv(true);
    const { route, state } = snapshot('/login', { returnUrl: 'https://evil.example' });
    runGuard(() => redirectIfAuthedGuard(route, state));
    expect(parseUrl).toHaveBeenCalledWith('/');
  });

  it('rejects protocol-relative returnUrl', () => {
    const { parseUrl } = setupGuardEnv(true);
    const { route, state } = snapshot('/login', { returnUrl: '//evil.example' });
    runGuard(() => redirectIfAuthedGuard(route, state));
    expect(parseUrl).toHaveBeenCalledWith('/');
  });

  it('defaults to / when returnUrl missing', () => {
    const { parseUrl } = setupGuardEnv(true);
    const { route, state } = snapshot('/login');
    runGuard(() => redirectIfAuthedGuard(route, state));
    expect(parseUrl).toHaveBeenCalledWith('/');
  });
});
