# TypeScript version tracks Angular's build peer range

`@angular/build` declares a narrow TypeScript peer range — `>=6.0 <6.1` at
Angular 22.1.7, `>=5.9 <6.0` at 21.2 — so the TypeScript version this repo can
install is decided by Angular, not by what is newest on npm. We pin
`typescript` to the range Angular admits and move it only as part of a
deliberate Angular major upgrade.

This is worth recording because the pin looks like neglect. TypeScript 7 has
shipped, and a reader seeing `~6.0` in `package.json` will reasonably assume
nobody got round to updating it. Raising it breaks `npm ci` outright:

```
npm error peer typescript@">=6.0 <6.1" from @angular/build@22.1.7
```

Dependabot pull requests #40 and #28 were both this mistake, arrived at
automatically. #40 bumped TypeScript to 7.0.2 against Angular 21, whose build
peer range stops below 6.0; #28 bumped `@angular/core` to 22 while leaving
`@ngrx/signals` at 21, which peers on `@angular/core@^21`. Neither could pass
CI in any order, because each moved one half of a peer-coupled pair. Both were
closed unmerged in favour of a single combined upgrade.

## Consequences

`.github/dependabot.yml` enforces this rather than relying on review catching
it. `typescript`, `@ngrx/*` and `zone.js` sit in the `angular` group so
peer-coupled packages are proposed in one pull request that can actually
resolve, and TypeScript majors are ignored:

```yaml
ignore:
  - dependency-name: typescript
    update-types: ['version-update:semver-major']
```

The cost is that a TypeScript major stays invisible to the bot. Whoever runs
the next `ng update` is responsible for taking the TypeScript version Angular's
schematics choose — which is the version they would have had to take anyway.
