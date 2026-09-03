# Versioning and releases

Versions are CalVer: **`YYYY.M.PATCH`** — `2026.9.0`, then `2026.9.1`. The patch counts releases within the month and restarts when the month turns over. The month is unpadded on purpose, which keeps the version valid semver and therefore sortable by tooling.

Every push to `main` touching anything other than Markdown or `LICENSE` starts a release, which:

1. Reads the existing `v*` tags, takes the highest patch for this month, and adds one. Nothing in the repository stores the version, so there is no bump commit and nothing to conflict over.
2. Builds the image, passing the version as a build arg that the Dockerfile forwards to `dotnet publish /p:Version=`. Local builds default to `0.0.0`.
3. Pushes to GHCR tagged `<version>`, `<year>.<month>`, `latest` and `sha-<short>`, with provenance attested.
4. Creates the git tag and a GitHub release, with generated notes and the image digest.

The tag comes last, so a failed build leaves none behind and the next run reuses the number.

Releases are serialised, and only one run may sit pending. Merging several pull requests in quick succession cancels queued runs that a newer merge overtakes, so those commits get no version of their own — they ship in the next release, which builds the head of `main`. Every commit reaches an image; not every commit gets a version number.

To cut a release without a merge — rebuilding against a new base image, say — run the workflow from the **Actions** tab.
