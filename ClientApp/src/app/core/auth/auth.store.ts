import { computed, inject } from '@angular/core';
import { signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { AuthApi, LoginPayload, RegisterPayload } from './auth.api';
import { AuthError, toAuthError } from './auth-error';
import { AuthStatus, AuthenticatedUser } from './user.model';
import { patchState } from '@ngrx/signals';

interface AuthState {
  user: AuthenticatedUser | null;
  status: AuthStatus;
  error: AuthError | null;
  pending: boolean;
}

const initialState: AuthState = {
  user: null,
  status: 'unknown',
  error: null,
  pending: false,
};

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed((store) => ({
    isAuthed: computed(() => store.status() === 'authed'),
    isResolved: computed(() => store.status() !== 'unknown'),
    displayName: computed(() => {
      const user = store.user();
      if (!user) return null;
      return user.displayName ?? user.email;
    }),
  })),
  withMethods((store, api = inject(AuthApi)) => ({
    async loadMe(): Promise<void> {
      try {
        const user = await firstValueFrom(api.me());
        patchState(store, { user, status: 'authed', error: null });
      } catch (error) {
        patchState(store, { user: null, status: 'anonymous', error: null });
      }
    },
    async login(payload: LoginPayload): Promise<boolean> {
      patchState(store, { pending: true, error: null });
      try {
        const user = await firstValueFrom(api.login(payload));
        patchState(store, { user, status: 'authed', error: null, pending: false });
        return true;
      } catch (error) {
        patchState(store, {
          error: toAuthError(error, 'login'),
          pending: false,
        });
        return false;
      }
    },
    async register(payload: RegisterPayload): Promise<boolean> {
      patchState(store, { pending: true, error: null });
      try {
        const user = await firstValueFrom(api.register(payload));
        patchState(store, { user, status: 'authed', error: null, pending: false });
        return true;
      } catch (error) {
        patchState(store, {
          error: toAuthError(error, 'register'),
          pending: false,
        });
        return false;
      }
    },
    async logout(): Promise<void> {
      patchState(store, { pending: true });
      try {
        await firstValueFrom(api.logout());
      } catch (error) {
        console.warn('Logout server call failed; clearing local state anyway.', error);
      } finally {
        patchState(store, { user: null, status: 'anonymous', error: null, pending: false });
      }
    },
    clearError(): void {
      patchState(store, { error: null });
    },
    markAnonymous(): void {
      patchState(store, { user: null, status: 'anonymous' });
    },
  })),
);

export type AuthStore = InstanceType<typeof AuthStore>;
