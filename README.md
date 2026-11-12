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
| `Sites` | three FourteenFish sites | Sites to monitor (`Name`, `Url`) |

## Testing

```bash
dotnet test
```
