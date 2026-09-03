# Email links are hashed, single-use, and cannot outrun SMTP

Email verification and password reset both work by mailing a link. Three decisions about them are worth recording, because each looks like something a reader might "simplify".

**Only the hash of a token is stored.** `user_tokens` holds a SHA-256 of the token, never the token itself, for the same reason the `users` table holds no passwords: a leak of the table should not hand anyone a working link. SHA-256 rather than BCrypt is deliberate — these tokens are 256 bits of cryptographic randomness, so there is nothing to brute-force, and a slow hash would only add latency to every redemption. Password hashing is slow because passwords are guessable; these are not.

**Tokens are single-use and short-lived**, one hour for reset and 24 hours for verification, and issuing a new link retires any outstanding one of the same purpose. Redemption is an `UPDATE ... WHERE consumed_at IS NULL`, so two concurrent requests race in the database and exactly one wins. The alternative — stateless signed tokens using the Data Protection keys — needs no table and no cleanup, but cannot be revoked or spent, so a leaked reset link keeps working until it expires.

**Resetting a password rotates the user's security stamp**, which is carried as a claim and checked on every request. Every session opened before the reset stops working. People reset passwords precisely because someone else may be in the account; leaving that session alive defeats the exercise.

**Requiring verification is conditional on SMTP being configured.** `Auth:RequireEmailVerification` is forced to false when `Smtp:Enabled` is false, whatever the setting says. Honouring it literally would mean every account waiting on a confirmation nothing can send — an application that has locked out all of its users, including the person who has to fix it, with no path back in. A setting that can brick an environment should not be obeyed when its precondition is missing.

## Consequences

`user_tokens` grows and nothing prunes it. Rows are harmless once consumed or expired, but a deployment sending a lot of mail will want a periodic delete.

Two settings now interact, and the interaction is not visible from either one alone: turning SMTP off silently disables a verification requirement. It is logged nowhere. The README says so, this file says so, and a test asserts it.
