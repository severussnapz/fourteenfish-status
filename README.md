# FourteenFish Status

A standalone status page that checks the uptime of the FourteenFish websites:

- fourteenfish.com
- fishbase.co.uk
- ffarms.co.uk

It polls each site every 5 minutes, keeps 30 days of history, and serves:

- `GET /` – HTML status page (auto-refreshes every 60 seconds)
- `GET /api/status` – JSON with latest state, 24h and 30d uptime, and average response time

A site is **up** when it responds with an HTTP status below 500.

## Running

Requires the .NET 8 SDK.

```bash
dotnet run --project StatusSite
```

Then open http://localhost:5000 (or the URL shown in the console).

## Configuration

Settings live in `StatusSite/appsettings.json` under `Monitor`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `IntervalMinutes` | `5` | How often to check each site |
| `TimeoutSeconds` | `10` | Request timeout per check |
| `RetentionDays` | `30` | How long history is kept |
| `HistoryFile` | `history.jsonl` | Where check results are stored |
| `Sites` | three FourteenFish sites | Sites to monitor (`Name`, `Url`, optional `Expect`) |
| `Maintenance` | `[]` | Planned maintenance windows (see below) |

### Content checks

A status code alone can miss broken pages, such as a maintenance or error page that still returns 200. Set `Expect` on a site to require some text in the page body:

```json
{ "Name": "FishBase", "Url": "https://fishbase.co.uk", "Expect": "Welcome" }
```

The match is case-insensitive. A site with a missing `Expect` is judged on its status code only. When the text is missing, the site is down and the reason is recorded as `Expected text "..." not found`.

## Scheduled maintenance

Add planned work under `Monitor:Maintenance` in `appsettings.json`:

```json
"Maintenance": [
  { "Site": "All", "Start": "2026-11-20T22:00:00Z", "End": "2026-11-20T23:00:00Z", "Message": "Database upgrade" }
]
```

`Site` is a site name or `All`. `Start` is inclusive and `End` is exclusive, both in UTC.

During a window the sites are still checked, but those checks are excluded from uptime, incidents, and alerts. The page shows the window under "Scheduled maintenance" and marks affected sites as Maintenance. Maintenance windows are read from configuration, so a change needs a redeploy. The upcoming windows are also available at `/api/maintenance`.


## Email alerts

Alerts are off by default. Configure them under `Alerts` in `appsettings.json`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `false` | Turn email alerts on |
| `FailuresBeforeAlert` | `2` | Consecutive failed checks before a DOWN alert |
| `SmtpHost` / `SmtpPort` | empty / `587` | SMTP server |
| `UseStartTls` | `true` | Use STARTTLS |
| `Username` / `Password` | empty | SMTP credentials |
| `From` | empty | Sender address |
| `Recipients` | `[]` | Addresses to notify |

A DOWN alert is sent once when a site reaches the failure threshold, and a recovery alert is sent when it comes back up. Alerts are not repeated while a site stays down.

Keep the SMTP password out of source control. Supply it through an environment variable instead:

```bash
export Alerts__Password="..."
export Alerts__Enabled=true
```

## Deploying to Render (free tier)

1. Sign in at https://render.com with GitHub and choose **New > Web Service**.
2. Select the `fourteenfish/fourteenfish-status` repository. Render detects the `Dockerfile`.
3. Choose the **Free** instance type and leave the Docker settings as they are.
4. Under **Environment**, add `Alerts__Password` with the Gmail App Password and `Alerts__Enabled=true` when you want alerts live.
5. Deploy. Render gives a public `*.onrender.com` URL.

Free instances sleep after 15 minutes without traffic, which pauses checks, and the local history file is lost on restart. Both are acceptable for testing only.

## Testing

```bash
dotnet test
```
