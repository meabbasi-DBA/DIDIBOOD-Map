using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Didibood.LocationAccess.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260609000000_AddImageDataToStaticMapSnapshot")]
/// <inheritdoc />
public partial class AddImageDataToStaticMapSnapshot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "image_data",
            table: "static_map_snapshots",
            type: "bytea",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "image_data",
            table: "static_map_snapshots");
    }
}
