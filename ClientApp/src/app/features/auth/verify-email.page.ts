import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth/auth.api';

type VerifyState = 'working' | 'verified' | 'failed';

@Component({
  selector: 'app-verify-email-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="page-centered">
      <section class="card w-full max-w-md" aria-labelledby="verify-title">
        @switch (state()) {
          @case ('working') {
            <h1 id="verify-title" class="page-title">Confirming your email</h1>
            <p class="page-subtitle" aria-live="polite">One moment.</p>
          }
          @case ('verified') {
            <h1 id="verify-title" class="page-title">Email confirmed</h1>
            <p class="page-subtitle" aria-live="polite">Your account is ready.</p>
            <a routerLink="/login" class="btn btn-primary btn-block">Sign in</a>
          }
          @case ('failed') {
            <h1 id="verify-title" class="page-title">This link no longer works</h1>
            <p class="page-subtitle" aria-live="polite">
              Confirmation links work once and expire after 24 hours. Sign in to have a new one
              sent.
            </p>
            <a routerLink="/login" class="btn btn-primary btn-block">Go to sign in</a>
          }
        }
      </section>
    </main>
  `,
})
export default class VerifyEmailPage {
  /** Bound from the query string by withComponentInputBinding(). */
  readonly token = input('');

  private readonly api = inject(AuthApi);

  protected readonly state = signal<VerifyState>('working');

  constructor() {
    // The token arrives in the URL, so verification starts on arrival rather
    // than asking the reader to press a button that could only do one thing.
    effect(() => {
      const token = this.token();
      void this.verify(token);
    });
  }

  private async verify(token: string): Promise<void> {
    if (!token) {
      this.state.set('failed');
      return;
    }

    try {
      await firstValueFrom(this.api.verifyEmail(token));
      this.state.set('verified');
    } catch {
      this.state.set('failed');
    }
  }
}
