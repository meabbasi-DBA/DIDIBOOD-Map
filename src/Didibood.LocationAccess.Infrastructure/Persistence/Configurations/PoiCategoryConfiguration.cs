using Didibood.LocationAccess.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Didibood.LocationAccess.Infrastructure.Persistence.Configurations;

public sealed class PoiCategoryConfiguration : IEntityTypeConfiguration<PoiCategory>
{
    public void Configure(EntityTypeBuilder<PoiCategory> builder)
    {
        builder.ToTable("poi_categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameFa).HasColumnName("name_fa").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SearchTermsJson).HasColumnName("search_terms").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order");
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
