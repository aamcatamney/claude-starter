# Metrics

Off by default. Enabling collection costs an OpenTelemetry pipeline and a push to a collector; leaving it off costs nothing at all — no exporter is registered and nothing is measured.

```jsonc
"Metrics": {
  "Enabled": false,
  "OtlpEndpoint": "http://localhost:4317",
  "ServiceName": "claude-starter"
}
```

```bash
Metrics__Enabled=true
Metrics__OtlpEndpoint=http://collector:4317
```

## Nothing is exposed over HTTP

Measurements are **pushed** to an OTLP collector. The application publishes no `/metrics` route, so there is no endpoint to protect and no way to accidentally serve your traffic volumes and error rates to the internet.

Prometheus reads from the collector rather than from the app. The OpenTelemetry Collector's `prometheus` exporter does that job; so does any OTLP-capable backend. The alternative — scraping the app directly — needs `OpenTelemetry.Exporter.Prometheus.AspNetCore`, which has never shipped a stable release, and a listener to keep off the public port.

## What is measured

From the platform, free: request rate, duration and status by endpoint (ASP.NET Core), plus GC, thread pool and exception counts (runtime).

From this application, the things nothing else can see:

| Instrument | Tags | Answers |
| --- | --- | --- |
| `auth.sign_in` | `outcome`: success, invalid-credentials, inactive, unverified | Are people getting in? Is a credential-stuffing run under way? |
| `auth.registration` | `outcome`: created, pending-verification, duplicate, invalid | Are sign-ups working, and how many stall unverified? |
| `auth.email_sent` | `purpose`: email_verification, password_reset | Is mail actually being handed to the sender? |
| `auth.token_redemption` | `purpose`, `outcome`: redeemed, rejected | Are links working? A rise in rejections means expiry, reuse, or someone guessing |

Rejected redemptions are the one worth an alert. A steady trickle is people clicking stale links from their inbox; a spike is not.

## Instruments exist even when disabled

`AppMetrics` is always registered and endpoints always call it. With collection off nothing subscribes to the instruments, which costs approximately nothing, and no endpoint needs a conditional around its measurement.

The consequence for tests: a `MeterListener` can observe the counters directly, without an exporter or a collector, which is how the metrics tests assert on outcomes.
