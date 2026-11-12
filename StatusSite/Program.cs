using System.Net;
using StatusSite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MonitorOptions>(builder.Configuration.GetSection("Monitor"));
var monitorOptions = builder.Configuration.GetSection("Monitor").Get<MonitorOptions>() ?? new MonitorOptions();

builder.Services.AddSingleton(_ => new HistoryStore(
    monitorOptions.HistoryFile,
    TimeSpan.FromDays(monitorOptions.RetentionDays)));
builder.Services.AddHttpClient("monitor");
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
    var rows = string.Join("\n", statuses.Select(s =>
    {
        var (label, css) = s.Up switch
        {
            true => ("Up", "up"),
            false => ("Down", "down"),
            null => ("No data", "unknown"),
        };
        return $"""
            <tr>
              <td>{WebUtility.HtmlEncode(s.Name)}</td>
              <td><a href="{WebUtility.HtmlEncode(s.Url)}">{WebUtility.HtmlEncode(s.Url)}</a></td>
              <td class="{css}">{label}</td>
              <td>{(s.StatusCode?.ToString() ?? "-")}</td>
              <td>{(s.Uptime24h is double u1 ? $"{u1}%" : "-")}</td>
              <td>{(s.Uptime30d is double u30 ? $"{u30}%" : "-")}</td>
              <td>{(s.AvgResponseMs24h is double ms ? $"{ms} ms" : "-")}</td>
              <td>{(s.CheckedAt is DateTimeOffset t ? t.ToString("yyyy-MM-dd HH:mm 'UTC'") : "-")}</td>
            </tr>
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
      <style>{{Css}}</style>
    </head>
    <body>
      <h1>FourteenFish Status</h1>
      <table>
        <thead>
          <tr><th>Site</th><th>URL</th><th>Status</th><th>HTTP</th><th>Uptime 24h</th><th>Uptime 30d</th><th>Avg response (24h)</th><th>Last checked</th></tr>
        </thead>
        <tbody>
          {{rows}}
        </tbody>
      </table>
    </body>
    </html>
    """;
}

static class PageStyle
{
    public const string Css = """
    body { font-family: system-ui, sans-serif; margin: 2rem; color: #222; }
    table { border-collapse: collapse; width: 100%; }
    th, td { padding: .6rem; border-bottom: 1px solid #ddd; text-align: left; }
    .up { color: #1a7f37; font-weight: 600; }
    .down { color: #cf222e; font-weight: 600; }
    .unknown { color: #6e7781; }
    """;
}
