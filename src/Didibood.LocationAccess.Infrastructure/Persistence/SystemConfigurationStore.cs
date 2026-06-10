using System.Text.Json;
using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Didibood.LocationAccess.Infrastructure.Persistence;

public sealed class SystemConfigurationStore(AppDbContext db, IMemoryCache cache) : ISystemConfigurationStore
{
    private static readonly TimeSpan CacheSliding = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken ct)
    {
        var cacheKey = $"syscfg:{key}";

        if (cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
            return cached;

        var config = await db.SystemConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConfigKey == key, ct);

        if (config is null)
            return defaultValue;

        var parsed = ParseValue(config.ConfigValue, config.ValueType, defaultValue);

        cache.Set(cacheKey, parsed, new MemoryCacheEntryOptions { SlidingExpiration = CacheSliding });

        return parsed;
    }

    public async Task SetAsync(string key, string value, string updatedBy, CancellationToken ct)
    {
        var config = await db.SystemConfigurations.FirstOrDefaultAsync(c => c.ConfigKey == key, ct);

        if (config is null)
        {
            db.SystemConfigurations.Add(new SystemConfiguration
            {
                ConfigKey = key,
                ConfigValue = value,
                ValueType = "string",
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = updatedBy
            });
        }
        else
        {
            config.ConfigValue = value;
            config.UpdatedAt = DateTimeOffset.UtcNow;
            config.UpdatedBy = updatedBy;
        }

        await db.SaveChangesAsync(ct);
        cache.Remove($"syscfg:{key}");
    }

    private static T ParseValue<T>(string value, string valueType, T defaultValue)
    {
        try
        {
            return valueType switch
            {
                "int" when typeof(T) == typeof(int)     => (T)(object)int.Parse(value),
                "int" when typeof(T) == typeof(long)    => (T)(object)long.Parse(value),
                "decimal" when typeof(T) == typeof(decimal) => (T)(object)decimal.Parse(value),
                "decimal" when typeof(T) == typeof(double)  => (T)(object)double.Parse(value),
                "bool" when typeof(T) == typeof(bool)   => (T)(object)bool.Parse(value),
                "json" => JsonSerializer.Deserialize<T>(value, JsonOptions) ?? defaultValue,
                _ => (T)(object)value
            };
        }
        catch
        {
            return defaultValue;
        }
    }
}
