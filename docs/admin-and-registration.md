# Administrators and registration

Registration is **closed by default**. A deployment that is reachable is otherwise open to whoever finds it, and that is a poor default for a template people will put on the internet.

```jsonc
"Auth": {
  "AllowPublicRegistration": false   // anyone may sign up when true
}
```

## Creating the first account

While the `users` table is empty, every startup writes a link to the log at warning level:

```
warn: claude_starter.Services.Auth.BootstrapInviteLogger[0]
      No accounts exist yet. Create the first administrator with this link, valid for 7 days:

          https://portal.example.com/register?token=CfDJ8LNVxx83...

      It stops working as soon as an account exists. Restart to issue another.
```

Open it and register. **That account is the administrator** — there is nobody else to grant it.

The link uses `Auth:AppBaseUrl` when set, and `http://localhost:5000` otherwise. Lose it and a restart issues another, as long as no account exists yet.

## What closes the door

The token is signed with the Data Protection keys rather than stored, because the guard that matters is not the token: **it is refused the moment any account exists**, valid signature or not. Nothing to migrate, nothing to prune, and no window where a leaked link outlives its usefulness.

That also means the link is only as private as the log it was written to. It is deliberately not restricted to Development — production is where you most need a way in — so treat a log containing one as sensitive until somebody has registered.

## The admin flag

`users.is_admin` is carried into the session as a role claim, so an endpoint can require it:

```csharp
app.MapGet("/api/admin/thing", Handle)
   .RequireAuthorization(AuthEndpoints.AdminPolicy);
```

`GET /api/auth/me` reports `isAdmin`, and the client shows an Admin chip beside the signed-in name. Nothing in the template is admin-only — there is nothing administrative in a template — so the policy exists unused, which is the part a project would otherwise rewrite.

Granting it to somebody later is a database change; there is no user-administration API here on purpose.

## Opening registration

Set `Auth__AllowPublicRegistration=true`. The register page becomes reachable, the sign-in page offers a "Create one" link again, and accounts created that way are **not** administrators.

## Upgrading an existing deployment

**Registration closes on deploy.** Any project relying on open sign-up must set `Auth:AllowPublicRegistration` to `true` explicitly, or people who could register yesterday get a 403 today.

Existing users are all `is_admin = false`, including whoever runs the place. Grant it deliberately:

```sql
UPDATE users SET is_admin = true WHERE email = 'you@example.com';
```

The bootstrap link is not logged, because accounts already exist.
