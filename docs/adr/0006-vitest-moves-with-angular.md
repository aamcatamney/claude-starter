# Vitest moves with Angular

`@angular/build` runs our unit tests and declares `vitest` as an optional
peer. Its range decides which Vitest we can install, as it does for
TypeScript (see [0002](0002-typescript-version-tracks-angular-peer-range.md)).
At 22.1.x the range was `^4.0.8`; 22.2.0 widened it to `^4.0.8 || ^5.0.0`.
So `vitest` belongs in the Dependabot `angular` group, not in `other`.

Dependabot pull request #69 showed what goes wrong otherwise. It bumped
`vitest` to 5.0.1 in the `other` group while #66 kept Angular on 22.1.x, so
`npm ci` failed:

```
npm error peerOptional vitest@"^4.0.8" from @angular/build@22.1.7
```

Vitest 5 could only land with Angular 22.2, and no single group proposed both.
Both pull requests were closed and replaced by one combined bump.

## Consequences

Unlike TypeScript, Vitest majors are not ignored. Angular widened its Vitest
range in a minor release, so ignoring majors would hide an upgrade we can take.
Because the packages share a group, the bot proposes the new Vitest in the same
pull request as the Angular release that allows it.

The cost: if a Vitest major ships before Angular allows it, the `angular`
group pull request fails as a whole, and the Angular patches in it wait too.
When that happens, add a temporary `ignore` for that Vitest major and remove it
once Angular catches up.
