namespace StatusSite;

public record SiteConfig(string Name, string Url);

public record CheckResult(
    string Site,
    DateTimeOffset CheckedAt,
    bool Up,
    int? StatusCode,
    long ElapsedMs,
    string? Error);

public record SiteStatus(
    string Name,
    string Url,
    bool? Up,
    DateTimeOffset? CheckedAt,
    int? StatusCode,
    double? Uptime24h,
    double? Uptime30d,
    double? AvgResponseMs24h);

public class MonitorOptions
{
    public int IntervalMinutes { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 10;
    public int RetentionDays { get; set; } = 30;
    public string HistoryFile { get; set; } = "history.jsonl";
    public List<SiteConfig> Sites { get; set; } = new();
}
