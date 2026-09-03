import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth/auth.api';
import { toAuthError } from '../../core/auth/auth-error';

@Component({
  selector: 'app-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="page-centered">
      <section class="card w-full max-w-md" aria-labelledby="reset-title">
        <h1 id="reset-title" class="page-title">Set a new password</h1>
        <p class="page-subtitle">
          Choose a password you don't use anywhere else. Signing in again elsewhere will be
          required — this ends every session opened before now.
        </p>

        @if (error(); as message) {
          <div role="alert" class="alert-danger">{{ message }}</div>
        }

        @if (!token()) {
          <p class="prose-note">
            This link is missing its token. Ask for a
            <a routerLink="/forgot-password" class="link">new reset link</a>.
          </p>
        } @else {
          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <fieldset [disabled]="pending()" class="space-y-4">
              <div class="field">
                <div class="field-label-row">
                  <label for="password" class="field-label">New password</label>
                  <span class="field-hint">12+ characters</span>
                </div>
                <div class="relative">
                  <input
                    id="password"
                    [type]="passwordVisible() ? 'text' : 'password'"
                    autocomplete="new-password"
                    formControlName="password"
                    [attr.aria-invalid]="showError() ? 'true' : null"
                    [attr.aria-describedby]="showError() ? 'password-error' : null"
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
                @if (showError()) {
                  <p id="password-error" class="field-error">
                    Password must be at least 12 characters.
                  </p>
                }
              </div>

              <button type="submit" class="btn btn-primary btn-block">
                {{ pending() ? 'Saving…' : 'Save password' }}
              </button>
            </fieldset>
          </form>
        }

        <p class="prose-note mt-6">
          <a routerLink="/login" class="link">Back to sign in</a>
        </p>
      </section>
    </main>
  `,
})
export default class ResetPasswordPage {
  /** Bound from the query string by withComponentInputBinding(). */
  readonly token = input('');

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AuthApi);
  private readonly router = inject(Router);

  protected readonly pending = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly passwordVisible = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    password: ['', [Validators.required, Validators.minLength(12)]],
  });

  protected showError(): boolean {
    const control = this.form.controls.password;
    return control.invalid && (control.dirty || control.touched);
  }

  protected togglePassword(): void {
    this.passwordVisible.update((visible) => !visible);
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.pending.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.resetPassword(this.token(), this.form.getRawValue().password));
      this.router.navigate(['/login'], { queryParams: { reset: 'done' } });
    } catch (error) {
      this.error.set(toAuthError(error, 'login').message);
    } finally {
      this.pending.set(false);
    }
  }
}
