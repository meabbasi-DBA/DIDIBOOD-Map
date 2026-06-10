// EF Core model snapshot — regenerate with: dotnet ef migrations add <Name>
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Didibood.LocationAccess.Infrastructure.Persistence;

#nullable disable

namespace Didibood.LocationAccess.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "9.0.0");
    }
}
