import { ChangeDetectionStrategy, Component, ElementRef, OnInit, computed, inject, input, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { AppConfig } from '../../core/passkeys/app-config';

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="page-centered">
      <section class="card w-full max-w-md" aria-labelledby="register-title">
        @if (store.awaitingVerification(); as address) {
          <h1 id="register-title" class="page-title">Check your email</h1>
          <p class="page-subtitle">
            We've sent a confirmation link to {{ address }}. Open it to finish setting up your
            account — the link works once and expires in 24 hours.
          </p>
          <p class="prose-note">
            <a routerLink="/login" class="link">Back to sign in</a>
          </p>
        } @else if (!canRegister()) {
          <h1 id="register-title" class="page-title">Registration is closed</h1>
          <p class="page-subtitle">
            Accounts are created by an administrator. Ask for an invitation, or if this is a new
            deployment, use the link written to the application log at startup.
          </p>
          <p class="prose-note">
            <a routerLink="/login" class="link">Back to sign in</a>
          </p>
        } @else {
        <h1 id="register-title" class="page-title">
          {{ inviteToken() ? 'Create the first account' : 'Create account' }}
        </h1>
        <p class="page-subtitle">
          {{
            inviteToken()
              ? 'This account will be the administrator.'
              : 'Set up a new account to get started.'
          }}
        </p>

        @if (store.error(); as err) {
          <div role="alert" class="alert-danger">{{ err.message }}</div>
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
              <label for="displayName" class="field-label">
                Display name <span class="font-normal text-ink-subtle">(optional)</span>
              </label>
              <input
                id="displayName"
                type="text"
                autocomplete="name"
                formControlName="displayName"
                class="input"
              />
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
                  autocomplete="new-password"
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

            <button type="submit" class="btn btn-primary btn-block">
              {{ store.pending() ? 'Creating account…' : 'Create account' }}
            </button>
          </fieldset>
        </form>

        <p class="prose-note mt-6">
          Already have an account?
          <a routerLink="/login" [queryParams]="passThroughReturnUrl()" class="link">Sign in</a>.
        </p>
        }
      </section>
    </main>
  `,
  styleUrl: './auth-shell.css',
})
export default class RegisterPage implements OnInit {
  protected readonly store = inject(AuthStore);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly emailInput = viewChild<ElementRef<HTMLInputElement>>('emailInput');

  /** Bound from the query string; present when following the bootstrap link. */
  readonly token = input('');

  private readonly config = inject(AppConfig);

  protected readonly inviteToken = computed(() => this.token() || null);

  /** Either the door is open, or this caller is carrying an invitation. */
  protected readonly canRegister = computed(
    () => this.config.publicRegistrationEnabled() || this.inviteToken() !== null,
  );

  protected readonly passwordVisible = signal(false);
  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: [''],
    password: ['', [Validators.required, Validators.minLength(12)]],
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
    const raw = this.form.getRawValue();
    // With verification required the server issues no session, so the store
    // reports success without a user and this page shows the next step instead
    // of navigating into the app.
    const ok = await this.store.register({
      email: raw.email,
      password: raw.password,
      displayName: raw.displayName.trim() === '' ? null : raw.displayName.trim(),
      inviteToken: this.inviteToken(),
    });
    if (ok && !this.store.awaitingVerification()) {
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
