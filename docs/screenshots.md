# Screenshots

Every page, in both themes, generated from fixed data so an image only changes when the interface does.

They live in `docs/screenshots/` and are committed, so the README renders on GitHub without anyone running anything. A project generated from this template starts without them — the rename scripts remove the folder and the README section, because those images show the template's own interface.

## Regenerating

```bash
docker compose up -d
dotnet run                                    # once, to apply migrations
psql "$CONNECTION_STRING" -f scripts/screenshots/seed.sql
npm --prefix scripts/screenshots install
npm --prefix scripts/screenshots run capture
```

`SCREENSHOT_BASE_URL` overrides the target, which defaults to `http://localhost:5000`.

Two things will bite you if you skip them:

**Re-apply `seed.sql` before every capture.** The links it creates are single-use, and `verify-email` redeems its token on page load. A second run against spent tokens photographs "this link no longer works" instead of the success state. The seed is idempotent and only ever touches the two `screenshot@` accounts.

**Raise the rate limit.** Every page load calls `/api/auth/me`, which sits inside the rate-limited `/api/auth` group. Twelve page loads exhaust the default ten-per-minute and the sign-in used for the landing shot comes back 429. Run the app with `RateLimit__Auth__PermitLimit=10000` while capturing.

## Why it is deterministic

Fixed viewport (1280×900), a committed BCrypt hash rather than a generated one, fixed user ids, fixed token values, and animations and the text caret disabled before each shot. A screenshot that changes when nothing changed is a screenshot nobody looks at.

The seed creates two accounts: a verified user with a display name, for pages showing a signed-in person, and an unverified one for the confirmation flow.

## Keeping them current

`CLAUDE.md` asks for a new capture whenever a page is added or changed. The images are the only check on the dark theme in a pull request — nothing else shows whether a change works in both.
