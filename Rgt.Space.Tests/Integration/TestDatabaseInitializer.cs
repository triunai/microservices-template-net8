using Npgsql;
using Rgt.Space.Core.Constants;

namespace Rgt.Space.Tests.Integration;

/// <summary>
/// Helper class to initialize the test database schema using SQL migration scripts.
/// </summary>
public static class TestDatabaseInitializer
{
    private static readonly string[] RequiredFiles =
    {
        "READMEs/SQL/PostgreSQL/Migrations/00-extensions.sql",
        "READMEs/SQL/PostgreSQL/Migrations/01-portal-schema.sql",
        "READMEs/SQL/PostgreSQL/Migrations/03-portal-routing-schema.sql",  // BEFORE 02: creates position_types table
        "READMEs/SQL/PostgreSQL/Migrations/01a-seed-devadmin.sql",         // BEFORE 02: locks DevAdmin ID so 02 skips its random UUID
        "READMEs/SQL/PostgreSQL/Migrations/02-portal-seed.sql",            // AFTER 03: seeds position_types, modules, roles, user_roles
        "READMEs/SQL/PostgreSQL/Migrations/06-seed-permissions.sql",       // AFTER 02: cartesian product of resources x actions
        "READMEs/SQL/PostgreSQL/Migrations/08-fix-overrides-schema.sql",   // AFTER 01: adds updated_at/updated_by to overrides
        "READMEs/SQL/PostgreSQL/Migrations/09-feature-flags.sql",
        "READMEs/SQL/PostgreSQL/Migrations/10-feature-flag-seed.sql",      // AFTER 02: references admin user via email lookup
        "READMEs/SQL/PostgreSQL/Migrations/11-add-missing-fk-indexes.sql", // FK indexes for auth + feature flag hot paths
        "READMEs/SQL/PostgreSQL/Migrations/12-era1-retrofit.sql"           // Zombie-safe indexes, triggers, timestamp defaults
    };

    /// <summary>
    /// Initializes the database by executing the required SQL scripts.
    /// </summary>
    /// <param name="connectionString">The connection string to the target database.</param>
    public static async Task InitializeAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        foreach (var filePath in RequiredFiles)
        {
            if (!System.IO.File.Exists(filePath))
            {
                // Fallback to searching relative to project root if running from bin folder
                var currentDir = Directory.GetCurrentDirectory();
                // Traverse up until we find READMEs or hit root
                var repoRoot = FindRepoRoot(currentDir);
                if (repoRoot == null)
                {
                    throw new FileNotFoundException($"Could not find SQL file: {filePath}");
                }

                var absolutePath = Path.Combine(repoRoot, filePath);
                if (!System.IO.File.Exists(absolutePath))
                {
                    throw new FileNotFoundException($"Could not find SQL file: {absolutePath}");
                }

                await ExecuteScriptAsync(conn, absolutePath);
            }
            else
            {
                await ExecuteScriptAsync(conn, filePath);
            }
        }
    }

    private static async Task ExecuteScriptAsync(NpgsqlConnection conn, string filePath)
    {
        var sql = await System.IO.File.ReadAllTextAsync(filePath);

        // Split by simple go-like separators if needed, but Npgsql usually handles multiple statements in one command
        // unless there are specific PSQL commands (like \c).
        // The provided SQL files seem to be standard SQL.

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static string? FindRepoRoot(string currentDir)
    {
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "READMEs")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
