/**
 * WebAuthn speaks ArrayBuffers; the server speaks base64url JSON. These convert
 * between the two, and nothing else in the app needs to know about either.
 */

export function base64UrlToBytes(value: string): Uint8Array {
  const padded = value.replace(/-/g, '+').replace(/_/g, '/');
  const binary = atob(padded.padEnd(padded.length + ((4 - (padded.length % 4)) % 4), '='));
  return Uint8Array.from(binary, (c) => c.charCodeAt(0));
}

export function bytesToBase64Url(buffer: ArrayBuffer): string {
  const binary = String.fromCharCode(...new Uint8Array(buffer));
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** True when this browser can create and use passkeys at all. */
export function isWebAuthnAvailable(): boolean {
  return typeof PublicKeyCredential !== 'undefined';
}
