using Didibood.LocationAccess.Infrastructure.H3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Didibood.LocationAccess.Infrastructure.Persistence;

public sealed class DatabaseBootstrapper(
    IConfiguration configuration,
    ILogger<DatabaseBootstrapper> logger)
{
    public async Task EnsureDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database ?? "DiDiboodMapDB";

        var adminBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres"
        };

        await using (var conn = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{databaseName}'";
            var exists = await cmd.ExecuteScalarAsync(cancellationToken) is not null;

            if (!exists)
            {
                logger.LogInformation("Creating database {Database}", databaseName);
                await using var create = conn.CreateCommand();
                create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
                await create.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync(cancellationToken);
            foreach (var ext in new[] { "postgis", "hstore", "pgcrypto" })
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE EXTENSION IF NOT EXISTS {ext}";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                logger.LogInformation("Extension {Extension} ensured", ext);
            }
        }
    }

    public async Task MigrateAndSeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await DbSeeder.SeedAsync(db, cancellationToken);
        await H3GridSeeder.EnsureTargetGridAsync(db, logger, cancellationToken);
    }
}
