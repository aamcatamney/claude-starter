import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthenticatedUser } from '../auth/user.model';
import { base64UrlToBytes, bytesToBase64Url } from './webauthn';

export interface PasskeySummary {
  id: string;
  name: string;
  createdAt: string;
  lastUsedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class PasskeyApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/auth/passkeys';

  list(): Promise<PasskeySummary[]> {
    return firstValueFrom(this.http.get<PasskeySummary[]>(this.base));
  }

  remove(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/${id}`));
  }

  /** Runs the full registration ceremony and stores the result. */
  async register(name: string): Promise<void> {
    const options = await firstValueFrom(
      this.http.post<PublicKeyCredentialCreationOptionsJSON>(`${this.base}/register-options`, {}),
    );

    const credential = (await navigator.credentials.create({
      publicKey: {
        ...options,
        challenge: base64UrlToBytes(options.challenge),
        user: { ...options.user, id: base64UrlToBytes(options.user.id) },
        excludeCredentials: (options.excludeCredentials ?? []).map((c) => ({
          ...c,
          id: base64UrlToBytes(c.id),
        })),
      } as unknown as PublicKeyCredentialCreationOptions,
    })) as PublicKeyCredential | null;

    if (!credential) throw new Error('No credential was created.');

    const attestation = credential.response as AuthenticatorAttestationResponse;

    await firstValueFrom(
      this.http.post<void>(`${this.base}/register`, {
        name,
        response: {
          id: credential.id,
          rawId: bytesToBase64Url(credential.rawId),
          type: credential.type,
          response: {
            attestationObject: bytesToBase64Url(attestation.attestationObject),
            clientDataJSON: bytesToBase64Url(attestation.clientDataJSON),
          },
        },
      }),
    );
  }

  /** Runs the sign-in ceremony. No email is asked for or sent. */
  async signIn(rememberMe: boolean): Promise<AuthenticatedUser> {
    const options = await firstValueFrom(
      this.http.post<PublicKeyCredentialRequestOptionsJSON>(`${this.base}/sign-in-options`, {}),
    );

    const credential = (await navigator.credentials.get({
      publicKey: {
        ...options,
        challenge: base64UrlToBytes(options.challenge),
        allowCredentials: [],
      } as unknown as PublicKeyCredentialRequestOptions,
    })) as PublicKeyCredential | null;

    if (!credential) throw new Error('No passkey was offered.');

    const assertion = credential.response as AuthenticatorAssertionResponse;

    return firstValueFrom(
      this.http.post<AuthenticatedUser>(`${this.base}/sign-in`, {
        rememberMe,
        response: {
          id: credential.id,
          rawId: bytesToBase64Url(credential.rawId),
          type: credential.type,
          response: {
            authenticatorData: bytesToBase64Url(assertion.authenticatorData),
            clientDataJSON: bytesToBase64Url(assertion.clientDataJSON),
            signature: bytesToBase64Url(assertion.signature),
            userHandle: assertion.userHandle ? bytesToBase64Url(assertion.userHandle) : null,
          },
        },
      }),
    );
  }
}

/** Shapes the server sends, with binary fields as base64url strings. */
interface PublicKeyCredentialCreationOptionsJSON {
  challenge: string;
  user: { id: string; name: string; displayName: string };
  excludeCredentials?: { id: string; type: string }[];
  [key: string]: unknown;
}

interface PublicKeyCredentialRequestOptionsJSON {
  challenge: string;
  [key: string]: unknown;
}
