using System.Net;
using StatusSite;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.Configure<MonitorOptions>(builder.Configuration.GetSection("Monitor"));
var monitorOptions = builder.Configuration.GetSection("Monitor").Get<MonitorOptions>() ?? new MonitorOptions();

builder.Services.AddSingleton(_ => new HistoryStore(
    monitorOptions.HistoryFile,
    TimeSpan.FromDays(monitorOptions.RetentionDays)));
builder.Services.AddHttpClient("monitor");

builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection("Alerts"));
var alertOptions = builder.Configuration.GetSection("Alerts").Get<AlertOptions>() ?? new AlertOptions();
builder.Services.AddSingleton(_ => new AlertTracker(alertOptions.FailuresBeforeAlert));
builder.Services.AddSingleton<IAlertSender, EmailAlertSender>();
builder.Services.AddSingleton<AlertService>();

builder.Services.AddHostedService<SiteMonitor>();

var app = builder.Build();

app.MapGet("/api/status", (HistoryStore history) =>
    Results.Ok(StatusReport.Build(monitorOptions.Sites, history.All(), DateTimeOffset.UtcNow)));

app.MapGet("/", (HistoryStore history) =>
{
    var statuses = StatusReport.Build(monitorOptions.Sites, history.All(), DateTimeOffset.UtcNow);
    return Results.Content(RenderPage(statuses), "text/html");
});

app.Run();

static string RenderPage(IReadOnlyList<SiteStatus> statuses)
{
    var downCount = statuses.Count(s => s.Up == false);
    var unknownCount = statuses.Count(s => s.Up is null);
    var (bannerClass, bannerTitle, bannerBody) = (downCount, unknownCount) switch
    {
        (> 0, _) => ("down", "Some systems are down", $"{downCount} of {statuses.Count} sites are not responding."),
        (0, > 0) => ("unknown", "Waiting for the first checks", "Results will appear after the first round of checks."),
        _ => ("up", "We’re fully operational", "We’re not aware of any issues affecting our systems."),
    };

    var icon = """<svg class="icon" viewBox="0 0 16 16" aria-hidden="true"><circle cx="8" cy="8" r="8"/><path d="M4.5 8.2l2.3 2.3 4.7-4.7" fill="none" stroke="#fff" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>""";

    var rows = string.Join("\n", statuses.Select(s =>
    {
        var (label, css) = s.Up switch
        {
            true => ("Operational", "up"),
            false => ("Down", "down"),
            null => ("No data", "unknown"),
        };
        var bars = string.Join("", s.Days.Select(d =>
        {
            var (barClass, text) = d.Uptime switch
            {
                null => ("none", "No data"),
                100 => ("up", "100%"),
                >= 95 => ("warn", $"{d.Uptime}%"),
                _ => ("down", $"{d.Uptime}%"),
            };
            return $"""<span class="bar {barClass}" title="{d.Date:yyyy-MM-dd}: {text}"></span>""";
        }));
        return $"""
            <div class="row">
              <div class="row-head">
                <span class="dot {css}" title="{label}"></span>
                <span class="name">{WebUtility.HtmlEncode(s.Name)}</span>
                <span class="meta">{(s.Uptime30d is double u30 ? $"{u30}% uptime" : "–")}</span>
              </div>
              <div class="bars">{bars}</div>
            </div>
            """;
    }));

    var latest = statuses.Where(s => s.CheckedAt is not null).Select(s => s.CheckedAt!.Value).DefaultIfEmpty().Max();
    var lastChecked = latest == default ? "never" : latest.ToString("yyyy-MM-dd HH:mm 'UTC'");

    return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta http-equiv="refresh" content="60">
          <title>FourteenFish Status</title>
          <style>{{PageStyle.Css}}</style>
        </head>
        <body>
          <main>
            <header class="top">
              <span class="brand">FourteenFish</span>
              <span class="crumb">Status pages / Websites</span>
            </header>
            <p class="intro">Live availability of the FourteenFish websites. Each site is checked every 5 minutes.</p>

            <section class="banner {{bannerClass}}">
              <div class="banner-head">{{icon}}<strong>{{bannerTitle}}</strong></div>
              <div class="banner-body">{{bannerBody}}</div>
            </section>

            <section class="panel">
              <div class="panel-head">
                <span>System status</span>
                <span class="range">Last 30 days</span>
              </div>
              {{rows}}
            </section>

            <p class="note">Last checked {{lastChecked}}. Refreshes automatically every 60 seconds.</p>
          </main>
        </body>
        </html>
        """;
}

static class PageStyle
{
    public const string Css = """
    :root {
      --bg: #ffffff; --text: #1f2328; --muted: #8a8f98; --border: #e5e7eb; --panel: #ffffff;
      --up: #2fbf8a; --up-bg: #d9f7ee; --up-border: #5dd5a8;
      --warn: #f5a524; --down: #f0655b; --down-bg: #fdecea; --down-border: #f5a6a0;
      --none: #e5e7eb; --unknown: #8a8f98; --unknown-bg: #f1f2f4;
    }
    @media (prefers-color-scheme: dark) {
      :root {
        --bg: #0f1115; --text: #e6edf3; --muted: #8d96a0; --border: #2a2f37; --panel: #161a20;
        --up-bg: #12261e; --up-border: #2a7a5a; --down-bg: #2d1619; --down-border: #7a2d2a;
        --none: #2a2f37; --unknown-bg: #1f232b;
      }
    }
    * { box-sizing: border-box; }
    body { margin: 0; font-family: "Inter", system-ui, -apple-system, "Segoe UI", sans-serif; background: var(--bg); color: var(--text); line-height: 1.5; }
    main { max-width: 760px; margin: 0 auto; padding: 2.5rem 1.25rem 3rem; }
    .top { display: flex; align-items: center; gap: .75rem; padding-bottom: 1rem; border-bottom: 1px solid var(--border); }
    .brand { font-weight: 700; font-size: 1.05rem; }
    .crumb { color: var(--muted); font-size: .95rem; }
    .intro { color: var(--muted); font-size: .9rem; margin: 1rem 0 1.5rem; }
    .banner { border: 1px solid var(--up-border); border-radius: 8px; overflow: hidden; margin-bottom: 1.5rem; }
    .banner-head { display: flex; align-items: center; gap: .6rem; padding: .8rem 1rem; background: var(--up-bg); }
    .banner-body { padding: .8rem 1rem; color: var(--text); font-size: .92rem; background: var(--panel); border-top: 1px solid var(--up-border); }
    .banner.down { border-color: var(--down-border); }
    .banner.down .banner-head { background: var(--down-bg); }
    .banner.down .banner-body { border-top-color: var(--down-border); }
    .banner.unknown { border-color: var(--border); }
    .banner.unknown .banner-head { background: var(--unknown-bg); color: var(--unknown); }
    .banner.unknown .banner-body { border-top-color: var(--border); }
    .banner.unknown .icon { fill: var(--unknown); }
    .banner.down .icon { fill: var(--down); }
    .icon { width: 18px; height: 18px; fill: var(--up); flex-shrink: 0; }
    .banner-head strong { font-size: 1rem; font-weight: 600; }
    .panel { border: 1px solid var(--border); border-radius: 8px; background: var(--panel); overflow: hidden; }
    .panel-head { display: flex; justify-content: space-between; align-items: center; padding: .9rem 1rem; border-bottom: 1px solid var(--border); font-weight: 600; }
    .range { color: var(--muted); font-weight: 400; font-size: .85rem; }
    .row { padding: .9rem 1rem 1rem; border-bottom: 1px solid var(--border); }
    .row:last-child { border-bottom: none; }
    .row-head { display: flex; align-items: center; gap: .55rem; margin-bottom: .55rem; }
    .dot { width: 16px; height: 16px; border-radius: 50%; flex-shrink: 0; }
    .dot.up { background: var(--up); }
    .dot.down { background: var(--down); }
    .dot.unknown { background: var(--unknown); }
    .name { font-size: .95rem; }
    .meta { margin-left: auto; color: var(--muted); font-size: .8rem; font-variant-numeric: tabular-nums; }
    .bars { display: flex; gap: 3px; height: 28px; }
    .bar { flex: 1 1 0; min-width: 4px; border-radius: 2px; background: var(--none); }
    .bar.up { background: var(--up); }
    .bar.warn { background: var(--warn); }
    .bar.down { background: var(--down); }
    .bar:hover { opacity: .75; }
    .note { color: var(--muted); font-size: .8rem; margin-top: 1.25rem; text-align: center; }
    """;
}

