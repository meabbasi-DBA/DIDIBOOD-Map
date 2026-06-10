using Didibood.LocationAccess.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

namespace Didibood.LocationAccess.Infrastructure.Persistence.Configurations;

public sealed class PoiConfiguration : IEntityTypeConfiguration<Poi>
{
    public void Configure(EntityTypeBuilder<Poi> builder)
    {
        builder.ToTable("pois");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PoiFingerprint).HasColumnName("poi_fingerprint").HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.PoiFingerprint).IsUnique();

        builder.Property<Point>("Location")
            .HasColumnName("location")
            .HasColumnType("geography (point,4326)")
            .IsRequired();

        builder.Ignore(x => x.Latitude);
        builder.Ignore(x => x.Longitude);

        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.NeshanType).HasColumnName("neshan_type").HasMaxLength(100);
        builder.Property(x => x.NeshanCategory).HasColumnName("neshan_category").HasMaxLength(50);
        builder.Property(x => x.SourcePayloadJson).HasColumnName("source_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.SupersededAt).HasColumnName("superseded_at");
        builder.Property(x => x.SupersededByPoiId).HasColumnName("superseded_by_poi_id");
        builder.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at");
        builder.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(x => x.Category).WithMany(x => x.Pois).HasForeignKey(x => x.CategoryId);
        builder.HasOne(x => x.SupersededByPoi).WithMany().HasForeignKey(x => x.SupersededByPoiId);
    }
}
