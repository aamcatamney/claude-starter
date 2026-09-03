# Integration tests share a container and isolate by database

One Postgres container is started for the whole assembly. Each test collection then creates its own database inside it, migrates it, and boots one application against it. Tests reset their database between cases with Respawn and take a fresh `HttpClient` per test.

The suite used to start a container once but boot a fresh `WebApplicationFactory` **per test** — about 110ms each, re-running the migrator every time — and put every test in a single collection, so nothing ran in parallel. Wall time grew linearly with the test count and there was no headroom to reclaim.

Isolating by database rather than by container is what makes the collections parallel-safe without paying for a container each: container start dominates, database creation does not.

## Consequences

Tests inside a collection share one application instance, so they share its DI singletons. A test that mutates process-wide state can now affect its neighbours — the reason a class's tests still run in sequence, and why a class that needs bespoke wiring should build its own factory rather than reach into the shared one.

Adding a test class means adding a collection: a `[CollectionDefinition]` and a `[Collection]` attribute. Forgetting the attribute does not fail loudly — xunit reports "constructor parameters did not have matching fixture data", which reads like a DI problem rather than a missing attribute.

Parallelism is bounded by the runner's thread count, so the win scales with cores rather than with collections.
