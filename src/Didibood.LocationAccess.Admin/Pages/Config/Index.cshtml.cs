using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Admin.Pages.Config;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<SystemConfiguration> Configs { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Configs = await db.SystemConfigurations
            .OrderBy(c => c.ConfigKey)
            .ToListAsync(cancellationToken);
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

        var config = await db.SystemConfigurations.FindAsync(new object[] { configKey }, cancellationToken);
        if (config is null)
        {
            TempData["Error"] = $"کلید «{configKey}» یافت نشد.";
            return RedirectToPage();
        }

        config.ConfigValue = configValue ?? string.Empty;
        config.UpdatedAt   = DateTimeOffset.UtcNow;
        config.UpdatedBy   = "admin";
        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = $"مقدار «{configKey}» با موفقیت ذخیره شد.";
        return RedirectToPage();
    }
}
