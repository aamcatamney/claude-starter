import { ChangeDetectionStrategy, Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="min-h-screen flex items-center justify-center bg-slate-50 px-4 py-12">
      <section
        class="w-full max-w-md bg-white border border-slate-200 rounded-xl shadow-sm p-8"
        aria-labelledby="login-title"
      >
        <h1 id="login-title" class="text-2xl font-semibold text-slate-900 mb-1">Sign in</h1>
        <p class="text-sm text-slate-600 mb-6">Welcome back. Enter your credentials to continue.</p>

        @if (store.error(); as err) {
          <div
            role="alert"
            class="mb-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800"
          >
            {{ err.message }}
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <fieldset [disabled]="store.pending()" class="space-y-4">
            <div>
              <label for="email" class="block text-sm font-medium text-slate-700 mb-1">Email</label>
              <input
                #emailInput
                id="email"
                type="email"
                autocomplete="email"
                formControlName="email"
                [attr.aria-invalid]="showError('email') ? 'true' : null"
                [attr.aria-describedby]="showError('email') ? 'email-error' : null"
                class="block w-full rounded-md border border-slate-300 px-3 py-2 text-slate-900 shadow-sm focus:border-slate-900 focus:outline-none focus:ring-2 focus:ring-slate-900"
              />
              @if (showError('email')) {
                <p id="email-error" class="mt-1 text-sm text-red-700">Enter a valid email.</p>
              }
            </div>

            <div>
              <div class="flex items-baseline justify-between mb-1">
                <label for="password" class="block text-sm font-medium text-slate-700">Password</label>
                <span class="text-xs text-slate-500">12+ characters</span>
              </div>
              <div class="relative">
                <input
                  id="password"
                  [type]="passwordVisible() ? 'text' : 'password'"
                  autocomplete="current-password"
                  formControlName="password"
                  [attr.aria-invalid]="showError('password') ? 'true' : null"
                  [attr.aria-describedby]="showError('password') ? 'password-error' : null"
                  class="block w-full rounded-md border border-slate-300 px-3 py-2 pr-20 text-slate-900 shadow-sm focus:border-slate-900 focus:outline-none focus:ring-2 focus:ring-slate-900"
                />
                <button
                  type="button"
                  (click)="togglePassword()"
                  [attr.aria-pressed]="passwordVisible()"
                  aria-label="Show or hide password"
                  class="absolute inset-y-0 right-2 my-1 px-2 rounded text-xs font-medium text-slate-700 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-slate-900"
                >
                  {{ passwordVisible() ? 'Hide' : 'Show' }}
                </button>
              </div>
              @if (showError('password')) {
                <p id="password-error" class="mt-1 text-sm text-red-700">Password must be at least 12 characters.</p>
              }
            </div>

            <label class="flex items-center gap-2 text-sm text-slate-700">
              <input
                type="checkbox"
                formControlName="rememberMe"
                class="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-900"
              />
              Remember me on this device
            </label>

            <button
              type="submit"
              class="w-full rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white shadow-sm hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2 disabled:bg-slate-400"
            >
              {{ store.pending() ? 'Signing in…' : 'Sign in' }}
            </button>
          </fieldset>
        </form>

        <p class="mt-6 text-sm text-slate-600">
          Don't have an account?
          <a routerLink="/register" [queryParams]="passThroughReturnUrl()" class="font-medium text-slate-900 underline">Create one</a>.
        </p>
      </section>
    </main>
  `,
  styleUrl: './auth-shell.css',
})
export default class LoginPage implements OnInit {
  protected readonly store = inject(AuthStore);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly emailInput = viewChild<ElementRef<HTMLInputElement>>('emailInput');

  protected readonly passwordVisible = signal(false);
  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(12)]],
    rememberMe: [false],
  });

  protected readonly passThroughReturnUrl = computed(() => {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    return returnUrl ? { returnUrl } : {};
  });

  ngOnInit(): void {
    this.store.clearError();
    queueMicrotask(() => this.emailInput()?.nativeElement.focus());
  }

  protected togglePassword(): void {
    this.passwordVisible.update((v) => !v);
  }

  protected showError(name: 'email' | 'password'): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.dirty || c.touched);
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const ok = await this.store.login(this.form.getRawValue());
    if (ok) {
      const target = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
      this.router.navigateByUrl(target);
    }
  }
}

function safeReturnUrl(value: string | null): string {
  if (!value) return '/';
  if (!value.startsWith('/') || value.startsWith('//')) return '/';
  return value;
}
