# Logout is not gated on the antiforgery token

`/api/auth/logout` validates the antiforgery token and signs the user out **either way**, logging a warning when validation fails. Every other state-changing endpoint stays protected; this one is a deliberate exception.

The reason is asymmetry of consequences. When logout rejects a request, `SignOutAsync` never runs and the auth cookie survives — but the client has already cleared its own state, so the user sees a signed-out UI in front of a live session that lasts the full 14-day sliding window. On a shared machine that is a real exposure, and it is invisible: nothing in the UI says the logout failed. We hit this in practice: antiforgery tokens bind to the current claims-based user, and both sign-in endpoints used to mint them before `HttpContext.User` had been assigned, so every freshly registered user's first logout was rejected.

What blocking buys instead is protection against a forced logout via CSRF: an attacker can end a victim's session, and nothing else. No data is read, written or disclosed. It is an annoyance, and OWASP treats logout CSRF as low severity for exactly that reason.

So we trade the low-severity exposure for the removal of a failure mode that silently keeps people signed in.

## Consequences

Logout cannot fail silently: a client that receives 204 knows the cookie is gone.

A failed validation is logged at warning level with the reason, so a burst of them is still visible — that signal is what caught the token-binding bug, and losing it entirely was not acceptable.

A reviewer auditing CSRF coverage will find one unprotected state-changing endpoint. That is intentional, and this file is the answer.
