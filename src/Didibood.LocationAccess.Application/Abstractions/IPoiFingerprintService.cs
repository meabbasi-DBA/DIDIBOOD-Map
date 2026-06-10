namespace Didibood.LocationAccess.Application.Abstractions;

public interface IPoiFingerprintService
{
    string ComputeFingerprint(
        string title,
        string category,
        double latitude,
        double longitude,
        string? address);
}
