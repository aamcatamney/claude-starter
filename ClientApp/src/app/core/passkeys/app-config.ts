import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

interface ClientConfig {
  passkeysEnabled: boolean;
  publicRegistrationEnabled: boolean;
}

/**
 * Feature flags the client needs before anyone signs in. Fetched once at
 * startup; a failure leaves everything off, which is the safe default.
 */
@Injectable({ providedIn: 'root' })
export class AppConfig {
  private readonly http = inject(HttpClient);
  readonly passkeysEnabled = signal(false);
  readonly publicRegistrationEnabled = signal(false);

  async load(): Promise<void> {
    try {
      const config = await firstValueFrom(this.http.get<ClientConfig>('/api/config'));
      this.passkeysEnabled.set(config.passkeysEnabled);
      this.publicRegistrationEnabled.set(config.publicRegistrationEnabled);
    } catch {
      this.passkeysEnabled.set(false);
      this.publicRegistrationEnabled.set(false);
    }
  }
}
