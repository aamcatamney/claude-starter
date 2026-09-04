import { inject, provideAppInitializer } from '@angular/core';
import { AppConfig } from '../passkeys/app-config';
import { AuthStore } from './auth.store';

export function provideAuthInitializer() {
  return provideAppInitializer(async () => {
    const store = inject(AuthStore);
    const config = inject(AppConfig);
    await Promise.all([store.loadMe(), config.load()]);
  });
}
