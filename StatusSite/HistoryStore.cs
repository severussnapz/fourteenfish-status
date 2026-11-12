using System.Text.Json;

namespace StatusSite;

public class HistoryStore
{
    private readonly object _lock = new();
    private readonly List<CheckResult> _results = new();
    private readonly string _path;
    private readonly TimeSpan _retention;

    public HistoryStore(string path, TimeSpan retention)
    {
        _path = path;
        _retention = retention;

        if (File.Exists(path))
        {
            foreach (var line in File.ReadLines(path))
            {
                var result = JsonSerializer.Deserialize<CheckResult>(line);
                if (result is not null)
                {
                    _results.Add(result);
                }
            }
            Prune(DateTimeOffset.UtcNow);
        }
    }

    public void Add(CheckResult result)
    {
        lock (_lock)
        {
            _results.Add(result);
            if (Prune(result.CheckedAt))
            {
                Rewrite();
            }
            else
            {
                File.AppendAllText(_path, JsonSerializer.Serialize(result) + Environment.NewLine);
            }
        }
    }

    public IReadOnlyList<CheckResult> Since(DateTimeOffset since)
    {
        lock (_lock)
        {
            return _results.Where(r => r.CheckedAt >= since).ToList();
        }
    }

    public IReadOnlyList<CheckResult> All()
    {
        lock (_lock)
        {
            return _results.ToList();
        }
    }

    // Returns true if any results were removed.
    private bool Prune(DateTimeOffset now)
    {
        var cutoff = now - _retention;
        return _results.RemoveAll(r => r.CheckedAt < cutoff) > 0;
    }

    private void Rewrite()
    {
        var lines = _results.Select(r => JsonSerializer.Serialize(r));
        File.WriteAllLines(_path, lines);
    }
}
