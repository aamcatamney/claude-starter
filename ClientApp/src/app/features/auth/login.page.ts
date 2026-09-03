import { ChangeDetectionStrategy, Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth/auth.api';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="page-centered">
      <section class="card w-full max-w-md" aria-labelledby="login-title">
        <h1 id="login-title" class="page-title">Sign in</h1>
        <p class="page-subtitle">Welcome back. Enter your credentials to continue.</p>

        @if (resetDone()) {
          <div role="status" class="alert-note">
            Password saved. Sign in with your new password.
          </div>
        }

        @if (store.error(); as err) {
          <div role="alert" class="alert-danger">
            {{ err.message }}
            @if (err.kind === 'email-not-verified') {
              <button type="button" class="link ml-1" (click)="resendVerification()">
                {{ resendState() === 'sent' ? 'Link sent' : 'Send a new link' }}
              </button>
            }
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <fieldset [disabled]="store.pending()" class="space-y-4">
            <div class="field">
              <label for="email" class="field-label">Email</label>
              <input
                #emailInput
                id="email"
                type="email"
                autocomplete="email"
                formControlName="email"
                [attr.aria-invalid]="showError('email') ? 'true' : null"
                [attr.aria-describedby]="showError('email') ? 'email-error' : null"
                class="input"
              />
              @if (showError('email')) {
                <p id="email-error" class="field-error">Enter a valid email.</p>
              }
            </div>

            <div class="field">
              <div class="field-label-row">
                <label for="password" class="field-label">Password</label>
                <span class="field-hint">12+ characters</span>
              </div>
              <div class="relative">
                <input
                  id="password"
                  [type]="passwordVisible() ? 'text' : 'password'"
                  autocomplete="current-password"
                  formControlName="password"
                  [attr.aria-invalid]="showError('password') ? 'true' : null"
                  [attr.aria-describedby]="showError('password') ? 'password-error' : null"
                  class="input input-with-affix"
                />
                <button
                  type="button"
                  (click)="togglePassword()"
                  [attr.aria-pressed]="passwordVisible()"
                  aria-label="Show or hide password"
                  class="btn btn-quiet absolute inset-y-0 right-2 my-1 px-2 text-xs"
                >
                  {{ passwordVisible() ? 'Hide' : 'Show' }}
                </button>
              </div>
              @if (showError('password')) {
                <p id="password-error" class="field-error">Password must be at least 12 characters.</p>
              }
            </div>

            <p class="prose-note">
              <a routerLink="/forgot-password" class="link">Forgot your password?</a>
            </p>

            <label class="checkbox-label">
              <input type="checkbox" formControlName="rememberMe" class="checkbox" />
              Remember me on this device
            </label>

            <button type="submit" class="btn btn-primary btn-block">
              {{ store.pending() ? 'Signing in…' : 'Sign in' }}
            </button>
          </fieldset>
        </form>

        <p class="prose-note mt-6">
          Don't have an account?
          <a routerLink="/register" [queryParams]="passThroughReturnUrl()" class="link">Create one</a>.
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

  private readonly api = inject(AuthApi);

  protected readonly passwordVisible = signal(false);
  protected readonly resendState = signal<'idle' | 'sent'>('idle');

  /** Set by reset-password on its way here, so the reader gets told it worked. */
  protected readonly resetDone = computed(
    () => this.route.snapshot.queryParamMap.get('reset') === 'done',
  );
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

  protected async resendVerification(): Promise<void> {
    const email = this.form.getRawValue().email.trim();
    if (!email) return;

    try {
      await firstValueFrom(this.api.resendVerification(email));
    } catch {
      // Nothing useful to say: the endpoint answers identically whether or not
      // the address is known.
    }
    this.resendState.set('sent');
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
