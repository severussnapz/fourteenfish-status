using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace StatusSite;

public class SiteMonitor : BackgroundService
{
    private readonly HttpClient _http;
    private readonly MonitorOptions _options;
    private readonly HistoryStore _history;
    private readonly AlertService _alerts;
    private readonly ILogger<SiteMonitor> _logger;

    public SiteMonitor(
        IHttpClientFactory httpClientFactory,
        IOptions<MonitorOptions> options,
        HistoryStore history,
        AlertService alerts,
        ILogger<SiteMonitor> logger)
    {
        _http = httpClientFactory.CreateClient("monitor");
        _options = options.Value;
        _history = history;
        _alerts = alerts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.IntervalMinutes));

        await CheckAllAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckAllAsync(stoppingToken);
        }
    }

    public async Task CheckAllAsync(CancellationToken ct)
    {
        var results = await Task.WhenAll(_options.Sites.Select(site => CheckOneAsync(site, ct)));
        foreach (var result in results)
        {
            _history.Add(result);
            _logger.LogInformation("{Site}: {State} ({ElapsedMs} ms){Maintenance}",
                result.Site, result.Up ? "up" : "down", result.ElapsedMs,
                result.InMaintenance ? " [maintenance]" : "");
            if (!result.InMaintenance)
            {
                await _alerts.HandleAsync(result, ct);
            }
        }
    }

    // A site is "up" when it answers with a status code below 500 and, if Expect is set, the body contains it.
    public async Task<CheckResult> CheckOneAsync(SiteConfig site, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            using var response = await _http.GetAsync(site.Url, timeout.Token);
            stopwatch.Stop();

            var code = (int)response.StatusCode;
            var inMaintenance = InMaintenance(site);
            if (code >= 500)
            {
                return new CheckResult(site.Name, DateTimeOffset.UtcNow, false, code, stopwatch.ElapsedMilliseconds, null, inMaintenance);
            }

            if (site.Expect is { } expected)
            {
                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                if (!ContainsExpected(body, expected))
                {
                    return new CheckResult(site.Name, DateTimeOffset.UtcNow, false, code, stopwatch.ElapsedMilliseconds,
                        $"Expected text \"{expected}\" not found", inMaintenance);
                }
            }

            return new CheckResult(site.Name, DateTimeOffset.UtcNow, true, code, stopwatch.ElapsedMilliseconds, null, inMaintenance);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            stopwatch.Stop();
            return new CheckResult(site.Name, DateTimeOffset.UtcNow, false, null, stopwatch.ElapsedMilliseconds, ex.Message, InMaintenance(site));
        }
    }

    public static bool ContainsExpected(string body, string expected) =>
        body.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private bool InMaintenance(SiteConfig site) =>
        Maintenance.IsActive(_options.Maintenance, site.Name, DateTimeOffset.UtcNow);
}
