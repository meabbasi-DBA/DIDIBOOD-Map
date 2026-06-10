namespace Didibood.LocationAccess.Domain.Entities;

public class PoiCategory
{
    public short Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameFa { get; set; } = string.Empty;
    public string SearchTermsJson { get; set; } = "[]";
    public short DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Poi> Pois { get; set; } = [];
}
