using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Didibood.LocationAccess.Application.Abstractions;

namespace Didibood.LocationAccess.Infrastructure.Services;

public sealed partial class PoiFingerprintService : IPoiFingerprintService
{
    public string ComputeFingerprint(
        string title,
        string category,
        double latitude,
        double longitude,
        string? address)
    {
        var input = string.Join('|', new[]
        {
            NormalizeText(title),
            NormalizeText(category),
            RoundCoord(latitude).ToString("F6", CultureInfo.InvariantCulture),
            RoundCoord(longitude).ToString("F6", CultureInfo.InvariantCulture),
            NormalizeText(address ?? string.Empty)
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeText(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormC).Trim();
        normalized = ZeroWidthRegex().Replace(normalized, string.Empty);
        normalized = WhitespaceRegex().Replace(normalized, " ");
        return normalized.ToLowerInvariant();
    }

    private static double RoundCoord(double value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[\u200B-\u200D\uFEFF]")]
    private static partial Regex ZeroWidthRegex();
}
