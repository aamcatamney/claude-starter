# Email verification and password reset

Both flows mail a single-use link. With `Smtp:Enabled` false nothing is sent — in Development the message is written to the log instead, so you can exercise the flows without a mail server. It is never logged in any other environment, because those bodies contain working links.

**`Auth:RequireEmailVerification` is ignored while SMTP is disabled**, whatever it is set to. Requiring a confirmation that nothing can send would leave every account — including yours — waiting forever with no way back in. See [ADR 0005](adr/0005-email-links-are-hashed-single-use-and-cannot-outrun-smtp.md).

With verification required:

- Registering creates the account and sends a link but issues **no session**.
- Signing in before confirming returns **403**, not 401 — the credentials were right, the account is not ready — carrying a problem type the client uses to offer a resend.

Reset links last an hour, confirmation links 24 hours, and each works once; requesting a new one retires the old. Completing a reset ends every session opened before it. Spent and expired tokens are deleted by a background sweep, hourly and at startup, once they are 30 days past use — long enough to still answer "was a reset ever requested for this account?"

## Upgrading an existing deployment

Two things happen the first time this runs against a database that predates these features. Neither affects a fresh one.

**Everyone is signed out, once.** Cookies issued earlier carry no security-stamp claim and are rejected. Nothing is wrong; people sign in again.

**Existing users are `email_verified = false`**, because nobody has confirmed those addresses. That only bites if you then enable `Auth:RequireEmailVerification`, which would hold every existing account at the login gate. To grandfather them, decide deliberately and run:

```sql
UPDATE users SET email_verified = true;
```
