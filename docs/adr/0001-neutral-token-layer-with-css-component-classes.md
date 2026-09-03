# Neutral token layer with CSS component classes

This is a template: whatever styling it ships gets inherited by every project cloned from it, and every one of those projects will want a different look. So the client styles nothing directly — colour, radius and surface decisions live as semantic tokens in a Tailwind `@theme` block (`--color-ink`, `--color-surface`, `--color-line`), and the recurring patterns are plain CSS classes in `@layer components` (`.card`, `.input`, `.btn`). Re-skinning a generated project means editing one token block; no component markup has to change.

## Considered options

**Angular components** (`<app-button>`, `<app-form-field>`) would make the label / input / error / `aria-describedby` wiring impossible to get wrong. Rejected because a template should not force its own component API on every project built from it: an API has to be learned, maintained and versioned, and the a11y markup it protects is already correct here. Classes can be deleted without touching a template.

**Attribute directives** sit between the two and were rejected for the same reason, with the added cost of being the least obvious of the three to a newcomer reading the markup.

**A distinctive visual identity** was rejected deliberately. The palette is monochrome — one grey ramp, ink as the only emphasis colour, red reserved for danger — because an identity chosen here would have to be undone by every project rather than extended.

## Consequences

Dark mode is a second block of token values under `prefers-color-scheme`, so it costs nothing per component and cannot drift out of sync. This depends on the tokens being referenced indirectly: `@theme` must not become `@theme inline`, which bakes values into the generated utilities and silently breaks every override.

Nothing enforces that `.input` is used with a matching `<label>` and error element. That guarantee was the main thing the component option bought, and it has been traded away.
