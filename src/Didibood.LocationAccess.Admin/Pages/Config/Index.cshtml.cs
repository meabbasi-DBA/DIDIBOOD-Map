using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Configuration;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Didibood.LocationAccess.Admin.Pages.Config;

public class IndexModel(
    AppDbContext db,
    ISystemConfigurationStore configStore,
    ILogger<IndexModel> logger) : PageModel
{
    private static readonly string[] NeshanConfigKeys =
    [
        "neshan.SearchApiKey",
        "neshan.LocationApiKey",
        "neshan.ReverseGeocodeApiKey",
        "neshan.RoutingApiKey"
    ];

    public List<SystemConfiguration> Configs { get; private set; } = [];
    public List<NeshanConfigRow> NeshanConfigs { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await EnsureNeshanConfigsAsync(cancellationToken);
        Configs = await db.SystemConfigurations
            .OrderBy(c => c.ConfigKey)
            .ToListAsync(cancellationToken);
        NeshanConfigs = Configs
            .Where(c => NeshanConfigKeys.Contains(c.ConfigKey, StringComparer.OrdinalIgnoreCase))
            .Select(c => new NeshanConfigRow(
                c.ConfigKey,
                c.ConfigKey["neshan.".Length..],
                NeshanOptions.Mask(c.ConfigValue),
                !string.IsNullOrWhiteSpace(c.ConfigValue),
                c.Description,
                c.UpdatedAt,
                c.UpdatedBy))
            .ToList();
    }

    public async Task<IActionResult> OnPostSaveAsync(
        string configKey,
        string configValue,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configKey))
        {
            TempData["Error"] = "کلید پیکربندی نامعتبر است.";
            return RedirectToPage();
        }

        await EnsureNeshanConfigsAsync(cancellationToken);
        var config = await db.SystemConfigurations.FindAsync(new object[] { configKey }, cancellationToken);
        if (config is null)
        {
            TempData["Error"] = $"کلید «{configKey}» یافت نشد.";
            return RedirectToPage();
        }

        var oldMasked = IsSecret(config) ? NeshanOptions.Mask(config.ConfigValue) : config.ConfigValue;
        await configStore.SetAsync(configKey, configValue ?? string.Empty, "admin", cancellationToken);
        var newMasked = IsSecret(config) ? NeshanOptions.Mask(configValue) : configValue;
        logger.LogInformation(
            "System configuration changed by admin: {ConfigKey}, old={OldValue}, new={NewValue}",
            configKey,
            oldMasked,
            newMasked);

        TempData["Success"] = $"مقدار «{configKey}» با موفقیت ذخیره شد.";
        return RedirectToPage();
    }

    private async Task EnsureNeshanConfigsAsync(CancellationToken cancellationToken)
    {
        var descriptions = new Dictionary<string, string>
        {
            ["neshan.SearchApiKey"] = "Neshan Search API key",
            ["neshan.LocationApiKey"] = "Neshan Location / static map API key",
            ["neshan.ReverseGeocodeApiKey"] = "Neshan Reverse Geocode API key",
            ["neshan.RoutingApiKey"] = "Neshan Routing API key"
        };

        foreach (var key in NeshanConfigKeys)
        {
            if (await db.SystemConfigurations.AnyAsync(c => c.ConfigKey == key, cancellationToken))
                continue;

            db.SystemConfigurations.Add(new SystemConfiguration
            {
                ConfigKey = key,
                ConfigValue = string.Empty,
                ValueType = "secret",
                Description = descriptions[key],
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = "system"
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static bool IsSecret(SystemConfiguration config) =>
        config.ValueType.Equals("secret", StringComparison.OrdinalIgnoreCase)
        || config.ConfigKey.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
        || config.ConfigKey.Contains("password", StringComparison.OrdinalIgnoreCase)
        || config.ConfigKey.Contains("secret", StringComparison.OrdinalIgnoreCase);

    public static string DisplayValue(SystemConfiguration config) =>
        IsSecret(config) ? NeshanOptions.Mask(config.ConfigValue) : config.ConfigValue;

    public sealed record NeshanConfigRow(
        string ConfigKey,
        string Label,
        string MaskedValue,
        bool IsConfigured,
        string? Description,
        DateTimeOffset UpdatedAt,
        string? UpdatedBy);
}
