import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'app-landing-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen flex flex-col bg-slate-50">
      <header class="border-b border-slate-200 bg-white">
        <div class="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <span class="text-sm font-semibold tracking-wide text-slate-900">claude-starter</span>
          <div class="flex items-center gap-4">
            <span class="text-sm text-slate-600" aria-live="polite">
              Hi, <span class="font-medium text-slate-900">{{ store.displayName() }}</span>
            </span>
            <button
              type="button"
              (click)="logout()"
              [disabled]="store.pending()"
              class="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 shadow-sm hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2 disabled:opacity-50"
            >
              {{ store.pending() ? 'Signing out…' : 'Sign out' }}
            </button>
          </div>
        </div>
      </header>

      <main class="mx-auto w-full max-w-5xl flex-1 px-4 py-12">
        <section class="rounded-xl border border-slate-200 bg-white p-10 shadow-sm">
          <h1 class="text-3xl font-semibold text-slate-900">You're signed in.</h1>
          <p class="mt-2 max-w-2xl text-slate-600">
            This is a placeholder home. Build the next thing here — add features under
            <code class="rounded bg-slate-100 px-1 py-0.5 text-sm text-slate-800">src/app/features/</code>
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
