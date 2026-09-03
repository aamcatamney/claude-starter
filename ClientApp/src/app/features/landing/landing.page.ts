import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';

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

      <main class="app-main">
        <section class="card">
          <h1 class="text-3xl font-semibold text-ink">You're signed in.</h1>
          <p class="prose-note mt-2 max-w-2xl">
            This is a placeholder home. Build the next thing here — add features under
            <code class="code-inline">src/app/features/</code>
            and wire them into the router.
          </p>
        </section>
      </main>
    </div>
  `,
})
export default class LandingPage {
  protected readonly store = inject(AuthStore);
  private readonly router = inject(Router);

  protected async logout(): Promise<void> {
    await this.store.logout();
    this.router.navigate(['/login']);
  }
}
