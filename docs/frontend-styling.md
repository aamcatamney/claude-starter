# Frontend styling

`ClientApp/src/styles.css` holds the whole design layer, in two parts:

- **Tokens** — a Tailwind `@theme` block of semantic names (`--color-ink`, `--color-surface`, `--color-line`, `--radius-control`). The palette is deliberately neutral: one grey ramp, ink for emphasis, red only for danger. Brand a project by editing these values; nothing else names a colour. Dark mode is the same tokens redefined under `prefers-color-scheme`, so it costs no per-component work.
- **Component classes** — `.card`, `.input`, `.field-label`, `.btn` and friends in `@layer components`. Templates are plain HTML using those classes; there is no Angular component API to learn or maintain. See [ADR 0001](adr/0001-neutral-token-layer-with-css-component-classes.md).

Do not change `@theme` to `@theme inline`. That inlines token values into the generated utilities, and every dark-mode override silently stops working.
