namespace Didibood.LocationAccess.Application.Abstractions;

public interface ISystemConfigurationStore
{
    Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken ct);
    Task SetAsync(string key, string value, string updatedBy, CancellationToken ct);
}
