using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace StatusSite;

public record PingdomDay(DateOnly Date, double AvgResponseMs);

public record PingdomStats(
    double Uptime7d,
    double? AvgResponseMs7d,
    DateTimeOffset FetchedAt,
    IReadOnlyList<PingdomDay>? DailyResponse = null,
    IReadOnlyDictionary<string, double>? CountryResponseMs = null);

public class PingdomCache
{
    private readonly ConcurrentDictionary<string, PingdomStats> _stats = new();

    public void Set(string site, PingdomStats stats) => _stats[site] = stats;

    public PingdomStats? Get(string site) => _stats.TryGetValue(site, out var stats) ? stats : null;

    public IReadOnlyDictionary<string, PingdomStats> All() => new Dictionary<string, PingdomStats>(_stats);
}

public static partial class PingdomParser
{
    [GeneratedRegex(@"Uptime last 7 days</h3>\s*<p[^>]*>\s*([\d.]+)\s*%", RegexOptions.IgnoreCase)]
    private static partial Regex UptimePattern();

    [GeneratedRegex(@"resp\. time last 7 days</h3>\s*<p[^>]*>\s*([\d.]+)\s*ms", RegexOptions.IgnoreCase)]
    private static partial Regex ResponsePattern();

    [GeneratedRegex(@"show_responsetime_chart\(\s*\[\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\]\s*,\s*\[([\d.,\s]*)\]", RegexOptions.IgnoreCase)]
    private static partial Regex DailyPattern();

    [GeneratedRegex(@"var phpArray = \{([^}]*)\}")]
    private static partial Regex CountryBlockPattern();

    [GeneratedRegex(@"""([A-Z]{2})""\s*:\s*([\d.]+)")]
    private static partial Regex CountryPattern();

    // Reads the headline figures from a Pingdom public report page. Returns null when the uptime figure is missing,
    // for example if Pingdom changes the page layout. The daily and per-country figures are optional extras.
    public static PingdomStats? Parse(string html, DateTimeOffset fetchedAt)
    {
        var uptime = UptimePattern().Match(html);
        if (!uptime.Success || !double.TryParse(uptime.Groups[1].Value, CultureInfo.InvariantCulture, out var uptimeValue))
        {
            return null;
        }

        double? response = null;
        var responseMatch = ResponsePattern().Match(html);
        if (responseMatch.Success && double.TryParse(responseMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var ms))
        {
            response = ms;
        }

        return new PingdomStats(uptimeValue, response, fetchedAt, ParseDaily(html), ParseCountries(html));
    }

    private static IReadOnlyList<PingdomDay>? ParseDaily(string html)
    {
        var match = DailyPattern().Match(html);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out var year)
            || !int.TryParse(match.Groups[2].Value, out var month)
            || !int.TryParse(match.Groups[3].Value, out var day))
        {
            return null;
        }

        try
        {
            var start = new DateOnly(year, month, day);
            var values = match.Groups[4].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => double.TryParse(v, CultureInfo.InvariantCulture, out var d) ? d : (double?)null)
                .ToList();
            if (values.Count == 0 || values.Any(v => v is null))
            {
                return null;
            }
            return values.Select((v, i) => new PingdomDay(start.AddDays(i), v!.Value)).ToList();
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, double>? ParseCountries(string html)
    {
        var block = CountryBlockPattern().Match(html);
        if (!block.Success)
        {
            return null;
        }

        var countries = CountryPattern().Matches(block.Groups[1].Value)
            .ToDictionary(m => m.Groups[1].Value, m => double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
        return countries.Count == 0 ? null : countries;
    }
}

public class PingdomMonitor : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly HttpClient _http;
    private readonly MonitorOptions _options;
    private readonly PingdomCache _cache;
    private readonly ILogger<PingdomMonitor> _logger;

    public PingdomMonitor(
        IHttpClientFactory httpClientFactory,
        IOptions<MonitorOptions> options,
        PingdomCache cache,
        ILogger<PingdomMonitor> logger)
    {
        _http = httpClientFactory.CreateClient("pingdom");
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            foreach (var site in _options.Sites.Where(s => !string.IsNullOrWhiteSpace(s.PingdomUrl)))
            {
                await RefreshAsync(site, stoppingToken);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAsync(SiteConfig site, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            var html = await _http.GetStringAsync(site.PingdomUrl, timeout.Token);
            var stats = PingdomParser.Parse(html, DateTimeOffset.UtcNow);
            if (stats is null)
            {
                _logger.LogWarning("Pingdom report for {Site} could not be read", site.Name);
                return;
            }
            _cache.Set(site.Name, stats);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Pingdom report for {Site} failed: {Message}", site.Name, ex.Message);
        }
    }
}
