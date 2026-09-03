namespace claude_starter.Services.Diagnostics;

public sealed class MetricsOptions
{
    public const string SectionName = "Metrics";

    /// <summary>
    /// Off by default. Disabled means no exporter and no collection — the
    /// counters below still exist but nothing listens to them, which costs
    /// approximately nothing.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Where measurements are pushed, e.g. a local collector.</summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>Name this application reports itself under.</summary>
    public string ServiceName { get; set; } = "claude-starter";
}
