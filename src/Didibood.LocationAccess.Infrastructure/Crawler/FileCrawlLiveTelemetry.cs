using System.Collections.Concurrent;
using System.Text.Json;
using Didibood.LocationAccess.Application.Abstractions;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

public sealed class FileCrawlLiveTelemetry : ICrawlLiveTelemetry
{
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
            data[executionId.ToString("N")] = new LiveEntry
            {
                Error = error.Length > 500 ? error[..500] : error,
                UpdatedAt = DateTimeOffset.UtcNow
            };
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

    private sealed class LiveEntry
    {
        public string Error { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
