import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { AppConfig } from '../../core/passkeys/app-config';
import { PasskeyApi, PasskeySummary } from '../../core/passkeys/passkey.api';
import { isWebAuthnAvailable } from '../../core/passkeys/webauthn';

@Component({
  selector: 'app-landing-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex min-h-screen flex-col">
      <header class="app-header">
        <div class="app-header-inner">
          <span class="text-sm font-semibold tracking-wide text-ink">claude-starter</span>
          <div class="flex items-center gap-4">
            <span class="prose-note" aria-live="polite">
              Hi, <span class="font-medium text-ink">{{ store.displayName() }}</span>
            </span>
            @if (store.user()?.isAdmin) {
              <span class="chip">Admin</span>
            }
            <button
              type="button"
              (click)="logout()"
              [disabled]="store.pending()"
              class="btn btn-outline"
            >
              {{ store.pending() ? 'Signing out…' : 'Sign out' }}
            </button>
          </div>
        </div>
      </header>

      <main class="app-main space-y-6">
        <section class="card">
          <h1 class="text-3xl font-semibold text-ink">You're signed in.</h1>
          <p class="prose-note mt-2 max-w-2xl">
            This is a placeholder home. Build the next thing here — add features under
            <code class="code-inline">src/app/features/</code>
            and wire them into the router.
          </p>
        </section>

        @if (passkeysAvailable()) {
          <section class="card" aria-labelledby="passkeys-title">
            <div class="flex items-baseline justify-between">
              <h2 id="passkeys-title" class="text-xl font-semibold text-ink">Passkeys</h2>
              <button
                type="button"
                class="btn btn-outline"
                (click)="addPasskey()"
                [disabled]="passkeyPending()"
              >
                {{ passkeyPending() ? 'Waiting for your device…' : 'Add a passkey' }}
              </button>
            </div>

            <p class="prose-note mt-2 max-w-2xl">
              Sign in with your fingerprint, face or screen lock instead of typing a password.
            </p>

            @if (passkeyError(); as message) {
              <div role="alert" class="alert-danger mt-4">{{ message }}</div>
            }

            @if (passkeys().length === 0) {
              <p class="prose-note mt-4">No passkeys yet.</p>
            } @else {
              <ul class="mt-4 divide-y divide-line border-t border-line">
                @for (passkey of passkeys(); track passkey.id) {
                  <li class="flex items-center justify-between py-3">
                    <span class="text-sm text-ink">{{ passkey.name }}</span>
                    <button type="button" class="btn btn-quiet text-xs" (click)="removePasskey(passkey.id)">
                      Remove
                    </button>
                  </li>
                }
              </ul>
            }
          </section>
        }
      </main>
    </div>
  `,
})
export default class LandingPage implements OnInit {
  protected readonly store = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly passkeyApi = inject(PasskeyApi);
  private readonly config = inject(AppConfig);

  protected readonly passkeys = signal<PasskeySummary[]>([]);
  protected readonly passkeyPending = signal(false);
  protected readonly passkeyError = signal<string | null>(null);

  protected readonly passkeysAvailable = computed(
    () => this.config.passkeysEnabled() && isWebAuthnAvailable(),
  );

  ngOnInit(): void {
    if (this.passkeysAvailable()) {
      void this.refreshPasskeys();
    }
  }

  private async refreshPasskeys(): Promise<void> {
    try {
      this.passkeys.set(await this.passkeyApi.list());
    } catch {
      this.passkeys.set([]);
    }
  }

  protected async addPasskey(): Promise<void> {
    this.passkeyPending.set(true);
    this.passkeyError.set(null);
    try {
      await this.passkeyApi.register(defaultPasskeyName());
      await this.refreshPasskeys();
    } catch (error) {
      // Cancelling the prompt is a choice, not an error to report.
      if (!(error instanceof DOMException && error.name === 'NotAllowedError')) {
        this.passkeyError.set('That passkey could not be added. Try again.');
      }
    } finally {
      this.passkeyPending.set(false);
    }
  }

  protected async removePasskey(id: string): Promise<void> {
    this.passkeyError.set(null);
    try {
      await this.passkeyApi.remove(id);
      await this.refreshPasskeys();
    } catch {
      this.passkeyError.set('That passkey could not be removed. Try again.');
    }
  }

  protected async logout(): Promise<void> {
    await this.store.logout();
    this.router.navigate(['/login']);
  }
}

/** Names the device rather than asking, which nobody wants at that moment. */
function defaultPasskeyName(): string {
  const agent = navigator.userAgent;
  if (/iPhone|iPad/i.test(agent)) return 'iPhone or iPad';
  if (/Android/i.test(agent)) return 'Android device';
  if (/Mac OS X/i.test(agent)) return 'Mac';
  if (/Windows/i.test(agent)) return 'Windows device';
  return 'Passkey';
}
