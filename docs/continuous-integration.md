# Continuous integration

Workflows run on a **self-hosted runner** labelled `self-hosted, linux, X64`.

`ci.yml` picks its runner per event. Pushes to `main` and pull requests from branches in this repository — all of which already require write access — use the self-hosted runner. **Pull requests from forks fall back to `ubuntu-latest`**: a fork PR is untrusted code, and `npm ci` and `dotnet test` would run it on your own machine, on a runner that persists between jobs. Keep that fallback while the repository is public.

The self-hosted machine needs:

- A .NET 10 SDK reachable by `actions/setup-dotnet` (linux-x64) and Node by `actions/setup-node`
- A running Docker daemon, for Testcontainers and for the release image
- Nothing else — the image targets `linux/amd64`, which is native here, so no QEMU is involved

`template-bootstrap.yml` targets the same runner, which only works while generated repositories can reach it — see [Using this template](../README.md#option-1--use-this-template-button-automatic).

To return to GitHub-hosted runners, set `runs-on: ubuntu-latest` across `.github/workflows/*.yml`.
