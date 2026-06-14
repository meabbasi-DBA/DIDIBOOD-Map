using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Didibood.LocationAccess.Tests;

public class EfMigrationDiscoveryTests
{
    [Fact]
    public void All_migrations_have_ef_discovery_attributes()
    {
        var migrationAssembly = typeof(Didibood.LocationAccess.Infrastructure.Persistence.AppDbContext).Assembly;
        var migrations = migrationAssembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Migration).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(migrations);

        foreach (var migration in migrations)
        {
            Assert.NotNull(migration.GetCustomAttribute<MigrationAttribute>());
        }
    }
}
