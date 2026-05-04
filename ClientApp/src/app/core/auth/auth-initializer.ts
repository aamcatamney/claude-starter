import { inject, provideAppInitializer } from '@angular/core';
import { AuthStore } from './auth.store';

export function provideAuthInitializer() {
  return provideAppInitializer(async () => {
    const store = inject(AuthStore);
    await store.loadMe();
  });
}
