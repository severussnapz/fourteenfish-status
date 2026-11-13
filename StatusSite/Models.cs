namespace StatusSite;

// Expect is optional. When set, the page body must contain it (case-insensitive) for the site to count as up.
public record SiteConfig(string Name, string Url, string? Expect = null);

public record CheckResult(
    string Site,
    DateTimeOffset CheckedAt,
    bool Up,
    int? StatusCode,
    long ElapsedMs,
    string? Error,
    bool InMaintenance = false);

public record SiteStatus(
    string Name,
    string Url,
    bool? Up,
    DateTimeOffset? CheckedAt,
    int? StatusCode,
    double? Uptime24h,
    double? Uptime30d,
    double? AvgResponseMs24h,
    IReadOnlyList<DayBar> Days);

public record DayBar(DateOnly Date, double? Uptime);

public record Incident(string Site, DateTimeOffset Start, DateTimeOffset? End, string Cause);

// Site is a configured site name, or "All" to cover every site. End is exclusive.
public record MaintenanceWindow(string Site, DateTimeOffset Start, DateTimeOffset End, string Message);

public class AlertOptions
{
    public bool Enabled { get; set; } = false;
    public int FailuresBeforeAlert { get; set; } = 2;
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "";
    public List<string> Recipients { get; set; } = new();
}

public class MonitorOptions
{
    public int IntervalMinutes { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 60;
    public int RetentionDays { get; set; } = 30;
    public string HistoryFile { get; set; } = "history.jsonl";
    public List<SiteConfig> Sites { get; set; } = new();
    public List<MaintenanceWindow> Maintenance { get; set; } = new();
}
