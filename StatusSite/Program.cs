using System.Net;
using StatusSite;

var builder = WebApplication.CreateBuilder(args);

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
    var (bannerClass, bannerText) = (downCount, unknownCount) switch
    {
        (> 0, _) => ("down", $"{downCount} of {statuses.Count} sites are down"),
        (0, > 0) => ("unknown", "Waiting for the first checks"),
        _ => ("up", "All FourteenFish sites are operational"),
    };

    var cards = string.Join("\n", statuses.Select(s =>
    {
        var (label, css) = s.Up switch
        {
            true => ("Operational", "up"),
            false => ("Down", "down"),
            null => ("No data", "unknown"),
        };
        return $"""
            <article class="card">
              <header>
                <div>
                  <h2>{WebUtility.HtmlEncode(s.Name)}</h2>
                  <a class="url" href="{WebUtility.HtmlEncode(s.Url)}">{WebUtility.HtmlEncode(s.Url)}</a>
                </div>
                <span class="badge {css}">{label}</span>
              </header>
              <dl>
                <div><dt>Uptime 24h</dt><dd>{(s.Uptime24h is double u1 ? $"{u1}%" : "–")}</dd></div>
                <div><dt>Uptime 30d</dt><dd>{(s.Uptime30d is double u30 ? $"{u30}%" : "–")}</dd></div>
                <div><dt>Avg response</dt><dd>{(s.AvgResponseMs24h is double ms ? $"{ms} ms" : "–")}</dd></div>
                <div><dt>HTTP</dt><dd>{(s.StatusCode?.ToString() ?? "–")}</dd></div>
              </dl>
              <footer>Last checked {(s.CheckedAt is DateTimeOffset t ? t.ToString("yyyy-MM-dd HH:mm 'UTC'") : "never")}</footer>
            </article>
            """;
    }));

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
            <h1>FourteenFish Status</h1>
            <div class="banner {{bannerClass}}">{{bannerText}}</div>
            <section class="grid">
              {{cards}}
            </section>
            <p class="note">Checked every 5 minutes. Refreshes automatically every 60 seconds.</p>
          </main>
        </body>
        </html>
        """;
}

static class PageStyle
{
    public const string Css = """
    :root {
      --bg: #f6f8fa; --surface: #ffffff; --text: #1f2328; --muted: #656d76; --border: #d0d7de;
      --up: #1a7f37; --up-bg: #dafbe1; --down: #cf222e; --down-bg: #ffebe9; --unknown: #6e7781; --unknown-bg: #eaeef2;
    }
    @media (prefers-color-scheme: dark) {
      :root {
        --bg: #0d1117; --surface: #161b22; --text: #e6edf3; --muted: #8d96a0; --border: #30363d;
        --up: #3fb950; --up-bg: #12261e; --down: #f85149; --down-bg: #2d1619; --unknown: #8d96a0; --unknown-bg: #21262d;
      }
    }
    * { box-sizing: border-box; }
    body { margin: 0; font-family: system-ui, -apple-system, "Segoe UI", sans-serif; background: var(--bg); color: var(--text); line-height: 1.5; }
    main { max-width: 960px; margin: 0 auto; padding: 2.5rem 1.25rem; }
    h1 { font-size: 1.75rem; margin: 0 0 1rem; }
    h2 { font-size: 1.1rem; margin: 0; }
    .banner { padding: .9rem 1.1rem; border-radius: 8px; font-weight: 600; margin-bottom: 1.75rem; border: 1px solid transparent; }
    .banner.up { background: var(--up-bg); color: var(--up); border-color: var(--up); }
    .banner.down { background: var(--down-bg); color: var(--down); border-color: var(--down); }
    .banner.unknown { background: var(--unknown-bg); color: var(--unknown); border-color: var(--unknown); }
    .grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); }
    .card { background: var(--surface); border: 1px solid var(--border); border-radius: 10px; padding: 1.1rem 1.2rem; display: flex; flex-direction: column; gap: .9rem; }
    .card header { display: flex; justify-content: space-between; align-items: flex-start; gap: .75rem; }
    .url { color: var(--muted); font-size: .85rem; text-decoration: none; word-break: break-all; }
    .url:hover { text-decoration: underline; }
    .badge { flex-shrink: 0; font-size: .8rem; font-weight: 600; padding: .2rem .6rem; border-radius: 999px; }
    .badge.up { background: var(--up-bg); color: var(--up); }
    .badge.down { background: var(--down-bg); color: var(--down); }
    .badge.unknown { background: var(--unknown-bg); color: var(--unknown); }
    dl { display: grid; grid-template-columns: 1fr 1fr; gap: .6rem 1rem; margin: 0; }
    dl div { display: flex; flex-direction: column; }
    dt { font-size: .75rem; text-transform: uppercase; letter-spacing: .03em; color: var(--muted); }
    dd { margin: 0; font-weight: 600; font-variant-numeric: tabular-nums; }
    .card footer { font-size: .8rem; color: var(--muted); border-top: 1px solid var(--border); padding-top: .6rem; }
    .note { color: var(--muted); font-size: .85rem; margin-top: 1.75rem; }
    """;
}
