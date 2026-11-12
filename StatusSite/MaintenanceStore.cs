using System.Text.Json;

namespace StatusSite;

// Windows added from the admin page are saved to a JSON file. Configured windows are read-only and always included.
public class MaintenanceStore
{
    private static readonly TimeSpan KeepEndedFor = TimeSpan.FromDays(30);

    private readonly object _lock = new();
    private readonly List<SavedMaintenanceWindow> _saved = new();
    private readonly IReadOnlyList<MaintenanceWindow> _configured;
    private readonly string _path;

    public MaintenanceStore(string path, IEnumerable<MaintenanceWindow> configured)
    {
        _path = path;
        _configured = configured.ToList();

        if (File.Exists(path))
        {
            var loaded = JsonSerializer.Deserialize<List<SavedMaintenanceWindow>>(File.ReadAllText(path));
            if (loaded is not null)
            {
                _saved.AddRange(loaded);
            }
        }
    }

    public IReadOnlyList<MaintenanceWindow> Configured => _configured;

    public IReadOnlyList<SavedMaintenanceWindow> Saved
    {
        get
        {
            lock (_lock)
            {
                return _saved.ToList();
            }
        }
    }

    public IReadOnlyList<MaintenanceWindow> All()
    {
        lock (_lock)
        {
            return _configured.Concat(_saved.Select(s => s.ToWindow())).ToList();
        }
    }

    public SavedMaintenanceWindow Add(MaintenanceWindow window, DateTimeOffset now)
    {
        lock (_lock)
        {
            _saved.RemoveAll(w => w.End < now - KeepEndedFor);
            var saved = new SavedMaintenanceWindow(Guid.NewGuid(), window.Site, window.Start, window.End, window.Message);
            _saved.Add(saved);
            Save();
            return saved;
        }
    }

    public bool Remove(Guid id)
    {
        lock (_lock)
        {
            var removed = _saved.RemoveAll(w => w.Id == id) > 0;
            if (removed)
            {
                Save();
            }
            return removed;
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(_path, JsonSerializer.Serialize(_saved));
    }
}

public record SavedMaintenanceWindow(Guid Id, string Site, DateTimeOffset Start, DateTimeOffset End, string Message)
{
    public MaintenanceWindow ToWindow() => new(Site, Start, End, Message);
}
