import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth/auth.api';

@Component({
  selector: 'app-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="page-centered">
      <section class="card w-full max-w-md" aria-labelledby="forgot-title">
        @if (sent()) {
          <h1 id="forgot-title" class="page-title">Check your email</h1>
          <p class="page-subtitle">
            If an account exists for {{ submittedEmail() }}, a link to set a new password is on its
            way. The link works once and expires in an hour.
          </p>
          <p class="prose-note">
            Nothing arrived? Check the spam folder, or
            <button type="button" class="link" (click)="startOver()">try another address</button>.
          </p>
        } @else {
          <h1 id="forgot-title" class="page-title">Reset your password</h1>
          <p class="page-subtitle">
            Enter the address you signed up with and we'll send you a link to set a new password.
          </p>

          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <fieldset [disabled]="pending()" class="space-y-4">
              <div class="field">
                <label for="email" class="field-label">Email</label>
                <input
                  id="email"
                  type="email"
                  autocomplete="email"
                  formControlName="email"
                  [attr.aria-invalid]="showError() ? 'true' : null"
                  [attr.aria-describedby]="showError() ? 'email-error' : null"
                  class="input"
                />
                @if (showError()) {
                  <p id="email-error" class="field-error">Enter a valid email.</p>
                }
              </div>

              <button type="submit" class="btn btn-primary btn-block">
                {{ pending() ? 'Sending…' : 'Send reset link' }}
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
export default class ForgotPasswordPage {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AuthApi);

  protected readonly pending = signal(false);
  protected readonly sent = signal(false);
  protected readonly submittedEmail = signal('');

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected showError(): boolean {
    const control = this.form.controls.email;
    return control.invalid && (control.dirty || control.touched);
  }

  protected startOver(): void {
    this.sent.set(false);
    this.form.reset();
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const email = this.form.getRawValue().email.trim();
    this.pending.set(true);
    try {
      await firstValueFrom(this.api.forgotPassword(email));
    } catch {
      // The server answers the same way whether or not the address is known,
      // and so does this page — showing a failure here would leak the
      // difference the endpoint is careful not to.
    } finally {
      this.submittedEmail.set(email);
      this.sent.set(true);
      this.pending.set(false);
    }
  }
}
