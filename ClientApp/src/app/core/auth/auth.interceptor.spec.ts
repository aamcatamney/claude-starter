import { HttpErrorResponse, HttpEventType, HttpHandlerFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { firstValueFrom, of, throwError } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthStore } from './auth.store';

function setup() {
  const navigate = vi.fn();
  const markAnonymous = vi.fn();
  const router = { navigate, url: '/dashboard' };
  const store = { markAnonymous };
  TestBed.configureTestingModule({
    providers: [
      { provide: Router, useValue: router },
      { provide: AuthStore, useValue: store },
    ],
  });
  return { navigate, markAnonymous, router };
}

function intercept(req: HttpRequest<unknown>, next: HttpHandlerFn) {
  return TestBed.runInInjectionContext(() => authInterceptor(req, next));
}

describe('authInterceptor', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('passes through successful responses', async () => {
    setup();
    const req = new HttpRequest('GET', '/api/orders');
    const response = new HttpResponse({ status: 200 });
    const next: HttpHandlerFn = () => of(response);
    const result = await firstValueFrom(intercept(req, next));
    expect(result.type).toBe(HttpEventType.Response);
  });

  it('on 401 to non-auth URL, marks anonymous and redirects to /login with returnUrl', async () => {
    const { navigate, markAnonymous } = setup();
    const req = new HttpRequest('GET', '/api/orders');
    const next: HttpHandlerFn = () => throwError(() => new HttpErrorResponse({ status: 401, url: '/api/orders' }));

    await expect(firstValueFrom(intercept(req, next))).rejects.toBeInstanceOf(HttpErrorResponse);

    expect(markAnonymous).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/dashboard' } });
  });

  it('on 401 to /api/auth/* URL, does NOT redirect', async () => {
    const { navigate, markAnonymous } = setup();
    const req = new HttpRequest('POST', '/api/auth/login', null);
    const next: HttpHandlerFn = () => throwError(() => new HttpErrorResponse({ status: 401, url: '/api/auth/login' }));

    await expect(firstValueFrom(intercept(req, next))).rejects.toBeInstanceOf(HttpErrorResponse);

    expect(markAnonymous).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('on non-401 errors, does not redirect', async () => {
    const { navigate, markAnonymous } = setup();
    const req = new HttpRequest('GET', '/api/orders');
    const next: HttpHandlerFn = () => throwError(() => new HttpErrorResponse({ status: 500 }));

    await expect(firstValueFrom(intercept(req, next))).rejects.toBeInstanceOf(HttpErrorResponse);

    expect(markAnonymous).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });
});
