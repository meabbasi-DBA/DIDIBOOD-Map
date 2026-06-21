using System.Collections.Concurrent;
using System.Text.Json;
using Didibood.LocationAccess.Application.Abstractions;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

public sealed class FileCrawlLiveTelemetry : ICrawlLiveTelemetry
{
    private const int MaxQueuedCells = 25;
    private const int MaxRecentCells = 10;
    private const int MaxFailedCells = 10;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string _path;
    private readonly object _fileLock = new();

    public FileCrawlLiveTelemetry()
    {
        _path = Path.Combine(Path.GetTempPath(), "didibood-crawl-live.json");
    }

    public void SetLiveError(Guid executionId, string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return;

        lock (_fileLock)
        {
            var data = ReadUnsafe();
            var entry = GetOrCreate(data, executionId);
            entry.Error = error.Length > 500 ? error[..500] : error;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            WriteUnsafe(data);
        }
    }

    public string? GetLiveError(Guid executionId)
    {
        lock (_fileLock)
        {
            return ReadUnsafe().TryGetValue(executionId.ToString("N"), out var entry)
                ? entry.Error
                : null;
        }
    }

    public void SetQueuedCells(Guid executionId, IReadOnlyList<long> h3Indexes)
    {
        lock (_fileLock)
        {
            var data = ReadUnsafe();
            var entry = GetOrCreate(data, executionId);
            entry.QueuedCells = h3Indexes
                .Distinct()
                .Take(MaxQueuedCells)
                .ToList();
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            WriteUnsafe(data);
        }
    }

    public void SetCurrentCell(Guid executionId, long h3Index, short categoryId, string searchTerm)
    {
        lock (_fileLock)
        {
            var data = ReadUnsafe();
            var entry = GetOrCreate(data, executionId);
            entry.CurrentCell = new LiveCell
            {
                H3Index = h3Index,
                CategoryId = categoryId,
                SearchTerm = searchTerm,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            entry.QueuedCells.Remove(h3Index);
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            WriteUnsafe(data);
        }
    }

    public void RecordCellResult(Guid executionId, long h3Index, bool succeeded, string? error)
    {
        lock (_fileLock)
        {
            var data = ReadUnsafe();
            var entry = GetOrCreate(data, executionId);
            var cell = new LiveCell
            {
                H3Index = h3Index,
                Error = string.IsNullOrWhiteSpace(error) ? null : error.Length > 300 ? error[..300] : error,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            entry.RecentCells.Insert(0, cell);
            if (entry.RecentCells.Count > MaxRecentCells)
                entry.RecentCells.RemoveRange(MaxRecentCells, entry.RecentCells.Count - MaxRecentCells);

            if (!succeeded)
            {
                entry.FailedCells.Insert(0, cell);
                if (entry.FailedCells.Count > MaxFailedCells)
                    entry.FailedCells.RemoveRange(MaxFailedCells, entry.FailedCells.Count - MaxFailedCells);
            }

            if (entry.CurrentCell?.H3Index == h3Index)
                entry.CurrentCell = null;

            entry.UpdatedAt = DateTimeOffset.UtcNow;
            WriteUnsafe(data);
        }
    }

    public CrawlLiveSnapshot GetSnapshot(Guid executionId)
    {
        lock (_fileLock)
        {
            if (!ReadUnsafe().TryGetValue(executionId.ToString("N"), out var entry))
                return new CrawlLiveSnapshot(null, null, [], [], []);

            return new CrawlLiveSnapshot(
                entry.Error,
                ToCell(entry.CurrentCell),
                entry.QueuedCells.ToArray(),
                entry.RecentCells.Select(ToCell).OfType<CrawlLiveCell>().ToArray(),
                entry.FailedCells.Select(ToCell).OfType<CrawlLiveCell>().ToArray());
        }
    }

    public void Clear(Guid executionId)
    {
        lock (_fileLock)
        {
            var data = ReadUnsafe();
            if (data.Remove(executionId.ToString("N")))
                WriteUnsafe(data);
        }
    }

    private Dictionary<string, LiveEntry> ReadUnsafe()
    {
        try
        {
            if (!File.Exists(_path))
                return new Dictionary<string, LiveEntry>(StringComparer.OrdinalIgnoreCase);

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<Dictionary<string, LiveEntry>>(json, JsonOptions)
                   ?? new Dictionary<string, LiveEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, LiveEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void WriteUnsafe(Dictionary<string, LiveEntry> data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(_path, json);
    }

    private static LiveEntry GetOrCreate(Dictionary<string, LiveEntry> data, Guid executionId)
    {
        var key = executionId.ToString("N");
        if (!data.TryGetValue(key, out var entry))
        {
            entry = new LiveEntry();
            data[key] = entry;
        }

        return entry;
    }

    private static CrawlLiveCell? ToCell(LiveCell? cell) =>
        cell is null
            ? null
            : new CrawlLiveCell(cell.H3Index, cell.UpdatedAt, cell.CategoryId, cell.SearchTerm, cell.Error);

    private sealed class LiveEntry
    {
        public string? Error { get; set; }
        public LiveCell? CurrentCell { get; set; }
        public List<long> QueuedCells { get; set; } = [];
        public List<LiveCell> RecentCells { get; set; } = [];
        public List<LiveCell> FailedCells { get; set; } = [];
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class LiveCell
    {
        public long H3Index { get; set; }
        public short? CategoryId { get; set; }
        public string? SearchTerm { get; set; }
        public string? Error { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
